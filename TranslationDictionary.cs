using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Translation lookup with normalized keys, a bounded recent-lookups cache,
    /// and a folded-in placeholder pattern engine.
    /// </summary>
    public sealed class TranslationDictionary
    {
        private readonly Dictionary<string, string> entries = new Dictionary<string, string>();

        // Bounded recent-lookups cache keyed on raw source. Absorbs the per-frame
        // repeated lookups from HUD monitors etc. Stores nulls so misses also short-circuit.
        private readonly Dictionary<string, string> recentLookups = new Dictionary<string, string>(128);
        private const int MaxRecentLookups = 128;

        private readonly List<TranslationPattern> patterns = new List<TranslationPattern>();
        private readonly HashSet<string> patternSignatures = new HashSet<string>();
        private readonly Dictionary<string, string> recentPatternLookups = new Dictionary<string, string>(128);

        public int Count { get { return entries.Count; } }
        public int PatternCount { get { return patterns.Count; } }

        public IEnumerable<KeyValuePair<string, string>> NormalizedEntries { get { return entries; } }

        /// <summary>Add an entry, normalizing the key. Use for one-off additions.</summary>
        public void Add(string rawKey, string value)
        {
            if (string.IsNullOrEmpty(rawKey)) return;
            entries[LocalizationUtils.FormatUpperKey(rawKey)] = value;
            recentLookups.Clear();
            recentPatternLookups.Clear();
        }

        /// <summary>Bulk-import entries whose keys have already been normalized (e.g., from TranslationFileParser).</summary>
        public void AddAll(Dictionary<string, string> normalizedEntries)
        {
            if (normalizedEntries == null) return;
            foreach (KeyValuePair<string, string> pair in normalizedEntries)
                entries[pair.Key] = pair.Value;
            recentLookups.Clear();
            recentPatternLookups.Clear();
        }

        public void Clear()
        {
            entries.Clear();
            recentLookups.Clear();
            recentPatternLookups.Clear();
        }

        public void ResetPatterns()
        {
            patterns.Clear();
            patternSignatures.Clear();
            recentLookups.Clear();
            recentPatternLookups.Clear();
        }

        /// <summary>
        /// Dictionary-only lookup with the bounded LRU + already-normalized fast path.
        /// Most callers want this; combine with TryMatchPattern for pattern fallback.
        /// </summary>
        public bool TryGetExact(string source, out string result)
        {
            result = null;
            if (string.IsNullOrEmpty(source))
                return false;

            string cached;
            if (recentLookups.TryGetValue(source, out cached))
            {
                result = cached;
                return cached != null;
            }

            string key = IsAlreadyNormalized(source) ? source : LocalizationUtils.FormatUpperKey(source);
            if (entries.TryGetValue(key, out result))
            {
                Remember(source, result);
                return true;
            }

            Remember(source, null);
            result = null;
            return false;
        }

        /// <summary>
        /// Direct access to the normalized-key dictionary. Used by TranslationPattern's
        /// inner-parameter translation; do not call from hot paths without normalizing first.
        /// </summary>
        public bool TryGetByNormalizedKey(string normalizedKey, out string value)
        {
            if (string.IsNullOrEmpty(normalizedKey))
            {
                value = null;
                return false;
            }
            return entries.TryGetValue(normalizedKey, out value);
        }

        /// <summary>
        /// Pattern-only lookup. Returns null on miss.
        /// </summary>
        public string TryMatchPattern(string source, string path)
        {
            if (string.IsNullOrEmpty(source) || patterns.Count == 0)
                return null;

            string cacheKey = (path ?? string.Empty) + "\n" + source;
            string cached;
            if (recentPatternLookups.TryGetValue(cacheKey, out cached))
                return cached;

            string upperText = null;
            for (int i = 0; i < patterns.Count; i++)
            {
                TranslationPattern pattern = patterns[i];
                string inputText;
                if (pattern.Mode != TranslationMode.CustomHandler)
                {
                    if (upperText == null)
                        upperText = source.ToUpperInvariant();
                    inputText = upperText;
                }
                else
                {
                    inputText = source;
                }

                string result = pattern.TryTranslate(inputText, path, this);
                if (result != null)
                {
                    RememberPattern(cacheKey, result);
                    return result;
                }
            }

            RememberPattern(cacheKey, null);
            return null;
        }

        /// <summary>
        /// Dictionary first, then pattern fallback. Convenience for callers that want both.
        /// </summary>
        public bool TryTranslate(string source, string path, out string result)
        {
            if (TryGetExact(source, out result))
                return true;

            result = TryMatchPattern(source, path);
            return result != null;
        }

        public void LoadPatternsFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                CoreConsole.Print("[TranslationDictionary] No pattern file found, no file patterns loaded");
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                int loadedCount = 0;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                        continue;

                    TranslationPattern pattern;
                    if (TryParseFsmPattern(trimmed, out pattern) && AddPattern(pattern))
                    {
                        loadedCount++;
                        DerivePieceEntries(pattern.OriginalPattern, pattern.TranslatedTemplate);
                    }
                }

                CoreConsole.Print($"[TranslationDictionary] Loaded {loadedCount} patterns from {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                CoreConsole.Error($"[TranslationDictionary] Failed to load patterns: {ex.Message}");
            }
        }

        private bool TryParseFsmPattern(string line, out TranslationPattern pattern)
        {
            pattern = null;

            int equalsIndex = TranslationFileParser.FindKeyValueSeparator(line);
            if (equalsIndex <= 0)
                return false;

            string original = line.Substring(0, equalsIndex).Trim().ToUpperInvariant();
            string translation = line.Substring(equalsIndex + 1).Trim();

            original = TranslationFileParser.UnescapeString(original);
            translation = TranslationFileParser.UnescapeString(translation);

            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(translation))
                return false;

            if (!original.Contains("{0}") && !original.Contains("{1}") && !original.Contains("{2}"))
                return false;

            bool translationHasPlaceholders = translation.Contains("{0}") || translation.Contains("{1}") || translation.Contains("{2}");

            pattern = new TranslationPattern(
                "FSM_" + original.Substring(0, System.Math.Min(20, original.Length)),
                translationHasPlaceholders ? TranslationMode.FsmPatternWithTranslation : TranslationMode.FsmPattern,
                original,
                translation
            );
            return true;
        }

        private bool AddPattern(TranslationPattern pattern)
        {
            if (pattern == null) return false;

            string signature = ((int)pattern.Mode).ToString()
                + "|" + (pattern.OriginalPattern ?? string.Empty)
                + "|" + (pattern.TranslatedTemplate ?? string.Empty);
            if (!patternSignatures.Add(signature))
                return false;

            // Prepend file-loaded patterns so later files win when patterns overlap.
            patterns.Insert(0, pattern);
            return true;
        }

        // For single-slot patterns like "A {0} B = X {0} Y", also store the static
        // pieces as dictionary entries. Some PlayMaker FSMs build the text one part
        // at a time, so the full pattern is never visible to the normal matcher.
        private void DerivePieceEntries(string source, string translation)
        {
            if (string.IsNullOrEmpty(source) || translation == null) return;
            if (CountOccurrences(source, "{0}") != 1) return;
            if (CountOccurrences(translation, "{0}") != 1) return;
            if (source.IndexOf("{1}", StringComparison.Ordinal) >= 0) return;
            if (source.IndexOf("{2}", StringComparison.Ordinal) >= 0) return;
            if (translation.IndexOf("{1}", StringComparison.Ordinal) >= 0) return;
            if (translation.IndexOf("{2}", StringComparison.Ordinal) >= 0) return;

            int sourceSlot = source.IndexOf("{0}", StringComparison.Ordinal);
            string sourceLeft = source.Substring(0, sourceSlot).Trim();
            string sourceRight = source.Substring(sourceSlot + 3).Trim();

            int translationSlot = translation.IndexOf("{0}", StringComparison.Ordinal);
            string translationLeft = translation.Substring(0, translationSlot).Trim();
            string translationRight = translation.Substring(translationSlot + 3).Trim();

            if (sourceLeft.Length > 0) Add(sourceLeft, translationLeft);
            if (sourceRight.Length > 0) Add(sourceRight, translationRight);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int index = 0;
            while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }

        private void Remember(string source, string result)
        {
            if (recentLookups.Count >= MaxRecentLookups)
                recentLookups.Clear();
            recentLookups[source] = result;
        }

        private void RememberPattern(string cacheKey, string result)
        {
            if (recentPatternLookups.Count >= MaxRecentLookups)
                recentPatternLookups.Clear();
            recentPatternLookups[cacheKey] = result;
        }

        /// <summary>
        /// True when the string has no whitespace and no ASCII lowercase letters and no non-ASCII chars.
        /// In that case FormatUpperKey would return the same string, so we can skip the allocation.
        /// Conservative: any non-ASCII char forces the FormatUpperKey path because char.ToUpperInvariant
        /// may rewrite it (e.g. Finnish lowercase a-umlaut).
        /// </summary>
        private static bool IsAlreadyNormalized(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
                    return false;
                if (c >= 'a' && c <= 'z')
                    return false;
                if (c >= 128)
                    return false;
            }
            return true;
        }
    }
}
