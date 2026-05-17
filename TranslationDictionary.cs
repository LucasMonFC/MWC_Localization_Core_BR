using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Translation lookup with normalized keys, a bounded recent-lookups cache,
    /// and a folded-in placeholder pattern engine.
    /// </summary>
    public sealed class TranslationDictionary
    {
        private readonly Dictionary<string, string> entries = new Dictionary<string, string>();

        // Bounded recent-lookups cache. Key is raw source for global lookups, or
        // scopeId + "\0" + source for scoped lookups, so on-scope and off-scope
        // results for the same source coexist without colliding.
        private readonly Dictionary<string, string> recentLookups = new Dictionary<string, string>(128);
        private const int MaxRecentLookups = 128;

        private readonly List<TranslationPattern> patterns = new List<TranslationPattern>();
        private readonly HashSet<string> patternSignatures = new HashSet<string>();

        // Path-scoped entry sets. Entries here are visible only when the lookup's
        // path starts with one of the scope's prefixes; otherwise they are invisible
        // even to global lookups. See AddScoped / TryGetExact.
        private sealed class TranslationScope
        {
            public readonly string Id;
            public readonly string[] Prefixes;
            public readonly Dictionary<string, string> Entries;

            public TranslationScope(string id, string[] prefixes)
            {
                Id = id;
                Prefixes = prefixes ?? new string[0];
                Entries = new Dictionary<string, string>();
            }
        }

        private readonly List<TranslationScope> scopes = new List<TranslationScope>();
        private readonly Dictionary<string, TranslationScope> scopesById = new Dictionary<string, TranslationScope>();

        public int Count { get { return entries.Count; } }
        public int PatternCount { get { return patterns.Count; } }

        public IEnumerable<KeyValuePair<string, string>> NormalizedEntries { get { return entries; } }

        /// <summary>Add an entry, normalizing the key. Use for one-off additions.</summary>
        public void Add(string rawKey, string value)
        {
            if (string.IsNullOrEmpty(rawKey)) return;
            entries[LocalizationUtils.FormatUpperKey(rawKey)] = value;
            recentLookups.Clear();
        }

        /// <summary>Bulk-import entries whose keys have already been normalized (e.g., from TranslationFileParser).</summary>
        public void AddAll(Dictionary<string, string> normalizedEntries)
        {
            if (normalizedEntries == null) return;
            foreach (KeyValuePair<string, string> pair in normalizedEntries)
                entries[pair.Key] = pair.Value;
            recentLookups.Clear();
        }

        public void Clear()
        {
            entries.Clear();
            scopes.Clear();
            scopesById.Clear();
            recentLookups.Clear();
        }

        /// <summary>
        /// Register (or re-register) a path-scoped entry set. Entries are visible
        /// only when a lookup's path starts with one of the given prefixes;
        /// off-scope lookups will never see them. Re-calling with the same scopeId
        /// replaces both prefixes and entries (used by F8 reload).
        /// </summary>
        public void AddScoped(string scopeId, IList<string> pathPrefixes, Dictionary<string, string> normalizedEntries)
        {
            if (string.IsNullOrEmpty(scopeId)) return;

            string[] prefixes;
            if (pathPrefixes == null || pathPrefixes.Count == 0)
                prefixes = new string[0];
            else
            {
                prefixes = new string[pathPrefixes.Count];
                for (int i = 0; i < pathPrefixes.Count; i++)
                    prefixes[i] = pathPrefixes[i];
            }

            TranslationScope existing;
            if (scopesById.TryGetValue(scopeId, out existing))
            {
                scopes.Remove(existing);
                scopesById.Remove(scopeId);
            }

            TranslationScope scope = new TranslationScope(scopeId, prefixes);
            scopesById[scopeId] = scope;
            scopes.Add(scope);

            if (normalizedEntries != null)
            {
                foreach (KeyValuePair<string, string> pair in normalizedEntries)
                    scope.Entries[pair.Key] = pair.Value;
            }

            recentLookups.Clear();
        }

        /// <summary>
        /// Add a single entry to an existing scope, normalizing the raw key.
        /// No-op if the scope has not been registered via <see cref="AddScoped"/>.
        /// </summary>
        public void AddScopedEntry(string scopeId, string rawKey, string value)
        {
            if (string.IsNullOrEmpty(scopeId) || string.IsNullOrEmpty(rawKey)) return;
            TranslationScope scope;
            if (!scopesById.TryGetValue(scopeId, out scope)) return;
            scope.Entries[LocalizationUtils.FormatUpperKey(rawKey)] = value;
            recentLookups.Clear();
        }

        public void ResetPatterns()
        {
            patterns.Clear();
            patternSignatures.Clear();
            recentLookups.Clear();
        }

        /// <summary>
        /// Dictionary-only lookup with the bounded LRU + already-normalized fast path.
        /// Most callers want this; combine with TryMatchPattern for pattern fallback.
        /// </summary>
        /// <param name="path">
        /// Caller's object/GameObject path. When it starts with a registered scope's
        /// prefix, that scope's entries are consulted first and only the global
        /// dictionary as fallback. When null/empty or off-scope, scoped entries are
        /// invisible.
        /// </param>
        public bool TryGetExact(string source, string path, out string result)
        {
            result = null;
            if (string.IsNullOrEmpty(source))
                return false;

            TranslationScope matched = (scopes.Count == 0 || string.IsNullOrEmpty(path))
                ? null
                : FindMatchingScope(path);

            string cacheKey = matched == null ? source : (matched.Id + "\0" + source);

            string cached;
            if (recentLookups.TryGetValue(cacheKey, out cached))
            {
                result = cached;
                return cached != null;
            }

            string key = IsAlreadyNormalized(source) ? source : LocalizationUtils.FormatUpperKey(source);

            if (matched != null && matched.Entries.TryGetValue(key, out result))
            {
                Remember(cacheKey, result);
                return true;
            }

            if (entries.TryGetValue(key, out result))
            {
                Remember(cacheKey, result);
                return true;
            }

            Remember(cacheKey, null);
            result = null;
            return false;
        }

        private TranslationScope FindMatchingScope(string path)
        {
            for (int i = 0; i < scopes.Count; i++)
            {
                string[] prefixes = scopes[i].Prefixes;
                for (int j = 0; j < prefixes.Length; j++)
                {
                    if (path.StartsWith(prefixes[j], StringComparison.Ordinal))
                        return scopes[i];
                }
            }
            return null;
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
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Dictionary first, then pattern fallback. Convenience for callers that want both.
        /// </summary>
        public bool TryTranslate(string source, string path, out string result)
        {
            if (TryGetExact(source, path, out result))
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

        // For single-slot patterns like "A {0} B = X {0} Y", also store the pieces as
        // plain dictionary entries ("A" -> "X", "B" -> "Y"). The FSM hook substitutes
        // BuildString StringParameters one piece at a time and never sees the assembled
        // string, so without this the {0} pattern alone would never fire.
        private void DerivePieceEntries(string source, string translation)
        {
            if (string.IsNullOrEmpty(source) || translation == null) return;
            if (CountOccurrences(source, "{0}") != 1) return;
            if (CountOccurrences(translation, "{0}") != 1) return;
            if (source.IndexOf("{1}", StringComparison.Ordinal) >= 0) return;
            if (source.IndexOf("{2}", StringComparison.Ordinal) >= 0) return;
            if (translation.IndexOf("{1}", StringComparison.Ordinal) >= 0) return;
            if (translation.IndexOf("{2}", StringComparison.Ordinal) >= 0) return;

            int srcSlot = source.IndexOf("{0}", StringComparison.Ordinal);
            string srcLeft = source.Substring(0, srcSlot).Trim();
            string srcRight = source.Substring(srcSlot + 3).Trim();

            int tSlot = translation.IndexOf("{0}", StringComparison.Ordinal);
            string tLeft = translation.Substring(0, tSlot).Trim();
            string tRight = translation.Substring(tSlot + 3).Trim();

            if (srcLeft.Length > 0) Add(srcLeft, tLeft);
            if (srcRight.Length > 0) Add(srcRight, tRight);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
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

        private void Remember(string source, string result)
        {
            if (recentLookups.Count >= MaxRecentLookups)
                recentLookups.Clear();
            recentLookups[source] = result;
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
