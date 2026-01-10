using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Handles direct translation of Teletext/TV content by modifying underlying data sources
    /// This is MUCH more efficient than constantly updating TextMesh components
    /// Based on My Summer Car's ExtraMod.cs approach
    /// 
    /// Supports category-based translations from translate_teletext.txt:
    /// [day]
    /// Monday = 월요일
    /// [kotimaa]
    /// News headline = 뉴스 헤드라인
    /// 
    /// NOTE: FSM pattern matching moved to unified PatternMatcher system
    /// </summary>
    public class TeletextHandler
    {
        // Category-based translations: [referenceName][originalText] = translatedText (for key-based lookup)
        private Dictionary<string, Dictionary<string, string>> categoryTranslations = 
            new Dictionary<string, Dictionary<string, string>>();
        
        // Index-based translations: [categoryName][index] = translatedText (for runtime replacement)
        private Dictionary<string, List<string>> indexBasedTranslations = 
            new Dictionary<string, List<string>>();
        
        // Track which arrays have been translated already
        private HashSet<string> translatedArrays = new HashSet<string>();
        
        // Track disabled FSM paths (to avoid re-disabling)
        private HashSet<string> disabledFsmPaths = new HashSet<string>();
        
        // GameObject path to category mapping
        private Dictionary<string, string> pathPrefixes = new Dictionary<string, string>
        {
            { "Systems/TV/Teletext/VKTekstiTV/Database", "" },  // Use referenceName directly
            { "Systems/TV/ChatMessages", "ChatMessages" },      // Prefix with "ChatMessages."
            { "Systems/TV/TVGraphics/CHAT/Day", "Chat.Day" }    // Prefix with "Chat.Day."
        };

        public TeletextHandler()
        {
        }

        /// <summary>
        /// Load teletext translations from INI-style file with category sections
        /// Supports both key-value pairs and index-based translations (in order)
        /// Special [fsm] section for FSM hardcoded strings (weather, UI labels, etc.)
        /// </summary>
        public void LoadTeletextTranslations(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                CoreConsole.Warning($"Teletext translation file not found: {filePath}");
                return;
            }

            try
            {
                categoryTranslations.Clear();
                indexBasedTranslations.Clear();
                
                string currentCategory = null;
                Dictionary<string, string> currentDict = null;
                List<string> currentIndexList = null;
                int loadedCount = 0;

                string[] lines = System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                
                List<string> keyLines = new List<string>();
                List<string> valueLines = new List<string>();
                bool readingValue = false;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.Trim();

                    // Skip comments
                    if (trimmed.StartsWith("#"))
                        continue;

                    // Check for category header [categoryName]
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        // Save previous entry if exists
                        if (keyLines.Count > 0 && currentDict != null)
                        {
                            SaveEntry(currentDict, currentIndexList, keyLines, valueLines, ref loadedCount);
                        }
                        
                        keyLines.Clear();
                        valueLines.Clear();
                        readingValue = false;
                        
                        currentCategory = trimmed.Substring(1, trimmed.Length - 2);
                        
                        // Skip [fsm] section - now handled by PatternMatcher
                        if (currentCategory == "fsm")
                        {
                            currentDict = null;
                            currentIndexList = null;
                            continue;
                        }
                        
                        if (!categoryTranslations.ContainsKey(currentCategory))
                            categoryTranslations[currentCategory] = new Dictionary<string, string>();
                        
                        if (!indexBasedTranslations.ContainsKey(currentCategory))
                            indexBasedTranslations[currentCategory] = new List<string>();
                        
                        currentDict = categoryTranslations[currentCategory];
                        currentIndexList = indexBasedTranslations[currentCategory];
                        continue;
                    }

                    // Skip empty lines outside of key/value context
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        // Empty line between entries - save current entry
                        if (keyLines.Count > 0 && currentDict != null)
                        {
                            SaveEntry(currentDict, currentIndexList, keyLines, valueLines, ref loadedCount);
                            keyLines.Clear();
                            valueLines.Clear();
                            readingValue = false;
                        }
                        continue;
                    }

                    // Check if this line is just "=" (separator)
                    if (trimmed == "=")
                    {
                        readingValue = true;
                        continue;
                    }

                    // Check if line contains unescaped "=" (single-line format)
                    int equalsIndex = FindUnescapedEquals(line);
                    if (equalsIndex > 0 && !readingValue)
                    {
                        // Skip [fsm] section processing - handled by PatternMatcher
                        if (currentCategory == "fsm")
                            continue;
                        
                        // Single-line format: KEY = VALUE
                        string key = line.Substring(0, equalsIndex).Trim();
                        string value = line.Substring(equalsIndex + 1).Trim();
                        
                        // Unescape special characters
                        key = UnescapeString(key);
                        value = UnescapeString(value);

                        if (!string.IsNullOrEmpty(key) && currentDict != null)
                        {
                            currentDict[key] = value;
                            currentIndexList.Add(value); // Add to index list in order
                            loadedCount++;
                        }
                        continue;
                    }

                    // Accumulate lines for multi-line key or value
                    if (currentDict != null)
                    {
                        if (readingValue)
                        {
                            valueLines.Add(line);
                        }
                        else
                        {
                            keyLines.Add(line);
                        }
                    }
                }

                // Save last entry if exists
                if (keyLines.Count > 0 && currentDict != null)
                {
                    SaveEntry(currentDict, currentIndexList, keyLines, valueLines, ref loadedCount);
                }

                // Create alias: ChatMessages.Messages uses ChatMessages.All translations
                if (categoryTranslations.ContainsKey("ChatMessages.All"))
                {
                    categoryTranslations["ChatMessages.Messages"] = categoryTranslations["ChatMessages.All"];
                    //CoreConsole.Print($"Created alias: ChatMessages.Messages -> ChatMessages.All ({categoryTranslations["ChatMessages.All"].Count} translations)");
                }

                CoreConsole.Print($"[Teletext] Loaded {loadedCount} teletext translations across {categoryTranslations.Count} categories");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[Teletext] Error loading teletext translations: {ex.Message}");
            }
        }

        /// <summary>
        /// Find the index of the first unescaped '=' character
        /// Returns -1 if no unescaped '=' is found
        /// </summary>
        private int FindUnescapedEquals(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '=')
                {
                    // Check if it's escaped (preceded by backslash)
                    if (i > 0 && line[i - 1] == '\\')
                    {
                        // This equals is escaped, skip it
                        continue;
                    }
                    return i;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// Unescape special characters: \= -> =, \n -> newline
        /// </summary>
        private string UnescapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            // Replace escape sequences
            return input.Replace("\\=", "=").Replace("\\n", "\n");
        }
        
        /// <summary>
        /// Helper method to save a multi-line entry
        /// </summary>
        private void SaveEntry(Dictionary<string, string> dict, List<string> indexList, List<string> keyLines, List<string> valueLines, ref int count)
        {
            if (keyLines.Count == 0) return;

            // Join lines with newlines, preserving original formatting
            string key = string.Join("\n", keyLines.ToArray());
            string value = valueLines.Count > 0 ? string.Join("\n", valueLines.ToArray()) : "";

            // Trim trailing/leading empty lines but preserve internal structure
            key = key.Trim();
            value = value.Trim();
            
            // Unescape special characters in both key and value
            key = UnescapeString(key);
            value = UnescapeString(value);

            if (!string.IsNullOrEmpty(key))
            {
                dict[key] = value;
                indexList.Add(value); // Add to index list in order
                count++;
            }
        }

        /// <summary>
        /// Clear all loaded translations
        /// </summary>
        public void ClearTranslations()
        {
            categoryTranslations.Clear();
            indexBasedTranslations.Clear();
        }

        /// <summary>
        /// Monitor and translate teletext arrays as they populate (call from Update/FixedUpdate)
        /// This is the MSC approach - wait for arrays to populate, then immediately replace
        /// Returns number of new items translated
        /// </summary>
        public int MonitorAndTranslateArrays()
        {
            try
            {
                int totalTranslated = 0;

                foreach (var pathPrefix in pathPrefixes.Keys)
                {
                    GameObject dataObject = GameObject.Find(pathPrefix);
                    if (dataObject == null) continue;

                    PlayMakerArrayListProxy[] proxies = dataObject.GetComponents<PlayMakerArrayListProxy>();
                    if (proxies == null || proxies.Length == 0) continue;

                    for (int i = 0; i < proxies.Length; i++)
                    {
                        string refName = proxies[i].referenceName;
                        if (string.IsNullOrEmpty(refName)) continue;

                        string prefix = pathPrefixes[pathPrefix];
                        string categoryName = string.IsNullOrEmpty(prefix) ? refName : $"{prefix}.{refName}";
                        
                        // Create unique key for this array
                        string arrayKey = $"{pathPrefix}[{i}]:{refName}";
                        
                        // Skip if already translated
                        if (translatedArrays.Contains(arrayKey))
                            continue;

                        // Check if array has been populated by the game
                        int currentCount = proxies[i]._arrayList != null ? proxies[i]._arrayList.Count : 0;
                        
                        // Skip if no translations for this category
                        if (!categoryTranslations.ContainsKey(categoryName) || 
                            categoryTranslations[categoryName].Count == 0) 
                            continue;
                        
                        if (currentCount > 0)
                        {
                            // Array just populated! Translate it immediately
                            int translated = TranslateArrayListProxy(proxies[i], categoryName);
                            if (translated > 0)
                            {
                                CoreConsole.Print($"[Teletext] '{categoryName}' populated with {currentCount} items, replaced with {translated} translations");
                                totalTranslated += translated;
                                translatedArrays.Add(arrayKey); // Mark as translated
                            }
                        }
                    }
                }

                return totalTranslated;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[Teletext] Error monitoring teletext arrays: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Translate teletext data by modifying the underlying ArrayList structures
        /// Call this once per scene load - translates already-populated arrays only
        /// Use MonitorAndTranslateArrays() in Update for lazy-loaded arrays
        /// Returns the number of items translated
        /// </summary>
        public int TranslateTeletextData()
        {
            try
            {
                int totalTranslated = 0;

                // Translate all known data source paths
                foreach (var pathPrefix in pathPrefixes.Keys)
                {
                    GameObject dataObject = GameObject.Find(pathPrefix);
                    
                    if (dataObject == null)
                        continue;

                    PlayMakerArrayListProxy[] proxies = dataObject.GetComponents<PlayMakerArrayListProxy>();
                    
                    if (proxies == null || proxies.Length == 0)
                        continue;

                    CoreConsole.Print($"[Teletext] Found {pathPrefix}: {proxies.Length} arrays");

                    // Translate each array based on its referenceName
                    for (int i = 0; i < proxies.Length; i++)
                    {
                        string refName = proxies[i].referenceName;
                        if (string.IsNullOrEmpty(refName))
                            continue;

                        // Build category name (e.g., "day", "ChatMessages.All")
                        string prefix = pathPrefixes[pathPrefix];
                        string categoryName = string.IsNullOrEmpty(prefix) ? refName : $"{prefix}.{refName}";

                        // Skip if no translations
                        if (!categoryTranslations.ContainsKey(categoryName) || 
                            categoryTranslations[categoryName].Count == 0)
                            continue;

                        // Only translate if array is already populated
                        int currentCount = proxies[i]._arrayList != null ? proxies[i]._arrayList.Count : 0;
                        if (currentCount == 0)
                            continue;

                        string arrayKey = $"{pathPrefix}[{i}]:{refName}";
                        if (translatedArrays.Contains(arrayKey))
                            continue; // Already translated

                        int translated = TranslateArrayListProxy(proxies[i], categoryName);
                        totalTranslated += translated;
                        
                        if (translated > 0)
                        {
                            CoreConsole.Print($"[Teletext]  [{i}] '{categoryName}': Translated {translated} items");
                            translatedArrays.Add(arrayKey);
                        }
                    }
                }

                if (totalTranslated > 0)
                {
                    CoreConsole.Print($"[Teletext] Successfully translated {totalTranslated} teletext/data items!");
                }
                else
                {
                    CoreConsole.Print("[Teletext] No pre-populated arrays to translate. Will monitor for lazy-loaded content.");
                }
                
                return totalTranslated;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[Teletext] Error translating teletext data: {ex.Message}");
                CoreConsole.Error($"[Teletext] Stack trace: {ex.StackTrace}");
                return 0;
            }
        }

        /// <summary>
        /// Translate a single PlayMakerArrayListProxy component using key-value lookup
        /// Loops through original array and looks up each element's translation
        /// Preserves empty/null elements naturally
        /// </summary>
        private int TranslateArrayListProxy(PlayMakerArrayListProxy proxy, string categoryName)
        {
            if (proxy == null || proxy._arrayList == null) 
                return 0;

            // Get translation dictionary for this category
            if (!categoryTranslations.ContainsKey(categoryName))
                return 0;

            Dictionary<string, string> translations = categoryTranslations[categoryName];
            if (translations.Count == 0)
                return 0;

            int translatedCount = 0;
            try
            {
                // Translate array in-place by looking up each element
                ArrayList arrayList = proxy._arrayList;
                
                for (int i = 0; i < arrayList.Count; i++)
                {
                    // Skip null or empty elements
                    if (arrayList[i] == null)
                        continue;
                    
                    string original = arrayList[i].ToString();
                    if (string.IsNullOrEmpty(original))
                        continue;
                    
                    // Look up translation by normalized key
                    string normalizedOriginal = original.Trim();
                    if (translations.TryGetValue(normalizedOriginal, out string translation))
                    {
                        arrayList[i] = translation;
                        translatedCount++;
                    }
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[Teletext] Error translating category '{categoryName}': {ex.Message}");
            }

            return translatedCount;
        }

        /// <summary>
        /// Disable FSM components on specific teletext paths to prevent text regeneration
        /// Then translate the TextMesh once - solves flickering issue
        /// Can be called multiple times (uses tracking to avoid re-processing)
        /// Only disables FSM when text has valid data (not default/placeholder values)
        /// Returns number of NEW FSMs disabled this call
        /// </summary>
        public int DisableBottomlineFSMs(TextMeshTranslator translator)
        {
            // Paths where FSMs constantly regenerate text (causing flickering)
            string[] bottomlinePaths = new string[]
            {
                "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1",
                "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1",
                "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1",
                "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1",
            };

            int disabledCount = 0;
            int translatedCount = 0;

            foreach (string path in bottomlinePaths)
            {
                // Skip if already processed
                if (disabledFsmPaths.Contains(path))
                    continue;
                    
                try
                {
                    GameObject obj = GameObject.Find(path);
                    if (obj == null)
                        continue; // Not found yet - will retry later

                    // Check TextMesh content FIRST - only proceed if it has valid data
                    TextMesh textMesh = obj.GetComponent<TextMesh>();
                    if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                        continue; // No text yet - FSM hasn't run
                    
                    string currentText = textMesh.text;
                    
                    // Extract number from text using regex
                    // Valid patterns: number should be 1-5
                    var match = System.Text.RegularExpressions.Regex.Match(currentText, @"\b(\d+)\b");
                    if (!match.Success)
                    {
                        // No number found in text
                        continue;
                    }
                    
                    int extractedNumber = int.Parse(match.Groups[1].Value);
                    
                    // Validate: number must be 1-5
                    if (extractedNumber < 1 || extractedNumber > 5)
                    {
                        // Invalid round number
                        //CoreConsole.Print($"[FSM Disable] Waiting for valid number (1-5) at {path} (current: '{currentText}', extracted: {extractedNumber})");
                        continue;
                    }

                    // Text looks valid! Disable FSM to prevent regeneration
                    PlayMakerFSM[] fsms = obj.GetComponents<PlayMakerFSM>();
                    if (fsms != null && fsms.Length > 0)
                    {
                        foreach (var fsm in fsms)
                        {
                            fsm.enabled = false;
                            disabledCount++;
                            CoreConsole.Print($"[Teletext] [FSM Disable] Disabled FSM '{fsm.FsmName}' at {path}");
                        }
                    }

                    // Translate the TextMesh once (now that FSM won't fight us)
                    if (translator != null)
                    {
                        if (translator.TranslateAndApplyFont(textMesh, path, null))
                        {
                            translatedCount++;
                            CoreConsole.Print($"[Teletext] [FSM Disable] Translated: '{textMesh.text}' at {path}");
                        }
                    }
                    
                    // Mark as processed
                    disabledFsmPaths.Add(path);
                }
                catch (System.Exception ex)
                {
                    CoreConsole.Error($"[Teletext] [FSM Disable] Error processing {path}: {ex.Message}");
                }
            }

            if (disabledCount > 0)
            {
                CoreConsole.Print($"[Teletext] [FSM Disable] Successfully disabled {disabledCount} FSMs and translated {translatedCount} Bottomline texts");
            }
            
            return disabledCount;
        }

        /// <summary>
        /// Reset translation state (useful for testing or scene changes)
        /// </summary>
        public void Reset()
        {
            translatedArrays.Clear();
            disabledFsmPaths.Clear();
        }

        /// <summary>
        /// Check if any teletext/data systems exist in current scene
        /// </summary>
        public bool IsTeletextAvailable()
        {
            foreach (string path in pathPrefixes.Keys)
            {
                if (GameObject.Find(path) != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if teletext arrays are populated with data
        /// Returns true if at least one array that has translations is populated
        /// </summary>
        public bool AreTeletextArraysPopulated()
        {
            try
            {
                int populatedCount = 0;
                int expectedCount = 0;
                List<string> details = new List<string>();

                foreach (string path in pathPrefixes.Keys)
                {
                    GameObject dataObject = GameObject.Find(path);
                    if (dataObject == null)
                    {
                        details.Add($"  {path}: GameObject not found");
                        continue;
                    }

                    PlayMakerArrayListProxy[] proxies = dataObject.GetComponents<PlayMakerArrayListProxy>();
                    if (proxies == null || proxies.Length == 0)
                    {
                        details.Add($"  {path}: No proxies found");
                        continue;
                    }

                    for (int i = 0; i < proxies.Length; i++)
                    {
                        string refName = proxies[i].referenceName ?? "null";
                        string prefix = pathPrefixes[path];
                        string categoryName = string.IsNullOrEmpty(prefix) ? refName : $"{prefix}.{refName}";
                        
                        int runtimeCount = proxies[i]._arrayList != null ? proxies[i]._arrayList.Count : 0;
                        int preFillCount = proxies[i].preFillStringList != null ? proxies[i].preFillStringList.Count : 0;
                        bool hasTranslations = categoryTranslations.ContainsKey(categoryName);
                        
                        // Only check arrays we have translations for
                        if (hasTranslations)
                        {
                            expectedCount++;
                            // Consider populated if EITHER runtime OR preFill has items
                            if (runtimeCount > 0 || preFillCount > 0)
                            {
                                populatedCount++;
                                details.Add($"  ✓ {categoryName}: {runtimeCount} runtime, {preFillCount} preFill (POPULATED)");
                            }
                            else
                            {
                                details.Add($"  ✗ {categoryName}: 0 runtime, 0 preFill (EMPTY)");
                            }
                        }
                        else
                        {
                            details.Add($"  - {categoryName}: {runtimeCount} runtime, {preFillCount} preFill (no translations)");
                        }
                    }
                }

                int threshold = expectedCount / 2;
                bool isPopulated = expectedCount > 0 && populatedCount >= threshold;
                
                CoreConsole.Print($"Array population check: {populatedCount}/{expectedCount} arrays populated (threshold: {threshold})");
                foreach (string detail in details)
                {
                    //CoreConsole.Print(detail);
                }
                CoreConsole.Print($"Result: {(isPopulated ? "POPULATED - will translate" : "NOT POPULATED - will retry")}");

                return isPopulated;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"Error checking array population: {ex.Message}");
                return false;
            }
        }
    }
}
