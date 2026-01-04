using BepInEx.Logging;
using HutongGames.PlayMaker;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Represents a translation pattern with placeholders {0}, {1}, {2}
    /// Example: "pakkasta {0} astetta" -> "영하 {0}도"
    /// </summary>
    internal class FsmPattern
    {
        public string OriginalPattern { get; private set; }
        public string TranslationPattern { get; private set; }
        private string[] originalParts;
        private string[] translationParts;
        
        public FsmPattern(string original, string translation)
        {
            OriginalPattern = original;
            TranslationPattern = translation;
            
            // Split patterns by placeholders to get static parts
            // Example: "pakkasta {0} astetta" -> ["pakkasta ", " astetta"]
            originalParts = SplitPattern(original);
            translationParts = SplitPattern(translation);
        }
        
        private string[] SplitPattern(string pattern)
        {
            // Split by {0}, {1}, {2} while keeping track of what we split by
            List<string> parts = new List<string>();
            string current = pattern;
            
            for (int i = 0; i < 10; i++) // Support up to {9}
            {
                string placeholder = "{" + i + "}";
                if (current.Contains(placeholder))
                {
                    int idx = current.IndexOf(placeholder);
                    if (idx >= 0)
                    {
                        parts.Add(current.Substring(0, idx));
                        current = current.Substring(idx + placeholder.Length);
                    }
                }
            }
            
            // Add remaining part
            if (!string.IsNullOrEmpty(current))
                parts.Add(current);
            
            return parts.ToArray();
        }
        
        /// <summary>
        /// Try to match input text against this pattern and extract variables
        /// Returns null if no match, otherwise returns extracted values
        /// </summary>
        public string[] TryExtractValues(string input)
        {
            if (originalParts.Length == 0)
                return null;
            
            List<string> values = new List<string>();
            string remaining = input;
            
            for (int i = 0; i < originalParts.Length; i++)
            {
                string part = originalParts[i];
                
                if (i == originalParts.Length - 1)
                {
                    // Last part - must end with this
                    if (!remaining.EndsWith(part))
                        return null;
                    
                    // Extract everything before this last part
                    if (part.Length < remaining.Length)
                    {
                        values.Add(remaining.Substring(0, remaining.Length - part.Length));
                    }
                }
                else
                {
                    // Middle part - find this part in remaining string
                    int idx = remaining.IndexOf(part);
                    if (idx < 0)
                        return null;
                    
                    // Extract value before this part (if any)
                    if (idx > 0)
                    {
                        values.Add(remaining.Substring(0, idx));
                    }
                    
                    // Move past this part
                    remaining = remaining.Substring(idx + part.Length);
                }
            }
            
            return values.ToArray();
        }
        
        /// <summary>
        /// Apply translation pattern with extracted values
        /// </summary>
        public string ApplyTranslation(string[] values)
        {
            string result = TranslationPattern;
            
            for (int i = 0; i < values.Length; i++)
            {
                result = result.Replace("{" + i + "}", values[i]);
            }
            
            return result;
        }
    }

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
    /// </summary>
    public class TeletextHandler
    {
        private ManualLogSource logger;
        
        // TEMPORARY: Disable FSM monitoring until game developer fixes constant text overwriting
        // Game constantly regenerates FSM text, fighting against translations and killing performance
        // Set to true to re-enable when game is fixed
        private const bool ENABLE_FSM_MONITORING = false;

        // Category-based translations: [referenceName][originalText] = translatedText (for key-based lookup)
        private Dictionary<string, Dictionary<string, string>> categoryTranslations = 
            new Dictionary<string, Dictionary<string, string>>();
        
        // Index-based translations: [categoryName][index] = translatedText (for runtime replacement)
        private Dictionary<string, List<string>> indexBasedTranslations = 
            new Dictionary<string, List<string>>();
        
        // Track which arrays have been translated already
        private HashSet<string> translatedArrays = new HashSet<string>();
        
        // Track if first ChatMessage has been translated (one-time check)
        private bool hasTranslatedFirstChatMessage = false;
        
        // Retry counter for first message translation (prevent infinite loops)
        private int firstMessageRetryCount = 0;
        private int firstMessageFrameCounter = 0;
        private const int MAX_FIRST_MESSAGE_RETRIES = 20; // 20 attempts over 3 seconds
        private const int FIRST_MESSAGE_CHECK_INTERVAL = 9; // Check every 9 frames (~3 sec total at 60fps)
        
        // FSM hardcoded string lookup: [originalString] = translatedString
        // For weather, bottomlines, and other FSM-generated text
        private Dictionary<string, string> fsmStringLookup = new Dictionary<string, string>();
        
        // FSM pattern templates: [originalPattern] = FsmPattern (for dynamic strings with {0}, {1} placeholders)
        // Example: "pakkasta {0} astetta" -> "영하 {0}도"
        private Dictionary<string, FsmPattern> fsmPatterns = new Dictionary<string, FsmPattern>();
        
        // Cached TextMesh components for FSM-driven paths (to avoid repeated Find() calls)
        private Dictionary<string, TextMesh> cachedTextMeshes = new Dictionary<string, TextMesh>();
        
        // Track successfully translated FSM texts (path -> translated text) to avoid re-translation
        private Dictionary<string, string> translatedFsmTexts = new Dictionary<string, string>();
        
        // Track which warning messages we've already logged (to reduce spam)
        private HashSet<string> loggedWarnings = new HashSet<string>();
        
        // FSM monitoring throttle timer
        private float fsmTimeSinceLastCheck = 0f;
        
        // Paths to monitor for FSM string replacement (lightweight targeted monitoring)
        // These are hardcoded paths known to have FSM-driven text that constantly overwrites
        private List<string> fsmMonitorPaths = new List<string>
        {
            // Teletext page bottomlines (navigation/status text)
            "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1",
            
            // Weather display pages
            "Systems/TV/Teletext/VKTekstiTV/PAGES/181/Texts/Bottomline",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Nyt",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Ennuste",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Selite",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Header",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Ennuste/Short",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Ennuste/WeatherType",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Nyt/WeatherTemp",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Ennuste/WeatherTemp",
            
            // Add more known FSM-driven paths here as discovered
            "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1",
        };
        
        // Paths that need pattern matching (dynamic strings with {0} placeholders)
        // Other paths only use exact/partial matching for better performance
        private HashSet<string> fsmPatternPaths = new HashSet<string>
        {
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Nyt/WeatherTemp",
            "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Ennuste/WeatherTemp",
            // Add more pattern-enabled paths here if needed
        };
        
        // GameObject path to category mapping
        private Dictionary<string, string> pathPrefixes = new Dictionary<string, string>
        {
            { "Systems/TV/Teletext/VKTekstiTV/Database", "" },  // Use referenceName directly
            { "Systems/TV/ChatMessages", "ChatMessages" },      // Prefix with "ChatMessages."
            { "Systems/TV/TVGraphics/CHAT/Day", "Chat.Day" }    // Prefix with "Chat.Day."
        };

        public TeletextHandler(ManualLogSource logger)
        {
            this.logger = logger;
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
                logger.LogWarning($"Teletext translation file not found: {filePath}");
                return;
            }

            try
            {
                categoryTranslations.Clear();
                indexBasedTranslations.Clear();
                fsmStringLookup.Clear();
                cachedTextMeshes.Clear(); // Clear cache when reloading
                
                string currentCategory = null;
                Dictionary<string, string> currentDict = null;
                List<string> currentIndexList = null;
                int loadedCount = 0;
                int fsmStringsLoaded = 0;

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
                        
                        // Special handling for [fsm] section
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
                        // Handle [fsm] section - direct string lookup
                        if (currentCategory == "fsm")
                        {
                            string fsmKey = line.Substring(0, equalsIndex).Trim();
                            string fsmValue = line.Substring(equalsIndex + 1).Trim();
                            
                            // Unescape special characters
                            fsmKey = UnescapeString(fsmKey);
                            fsmValue = UnescapeString(fsmValue);
                            
                            if (!string.IsNullOrEmpty(fsmKey) && !string.IsNullOrEmpty(fsmValue))
                            {
                                // Check if this is a pattern (contains {0}, {1}, etc.)
                                if (fsmKey.Contains("{0}") || fsmKey.Contains("{1}") || fsmKey.Contains("{2}"))
                                {
                                    // Store as pattern
                                    FsmPattern pattern = new FsmPattern(fsmKey, fsmValue);
                                    fsmPatterns[fsmKey] = pattern;
                                    fsmStringsLoaded++;
                                }
                                else
                                {
                                    // Store as simple string lookup
                                    fsmStringLookup[fsmKey] = fsmValue;
                                    fsmStringsLoaded++;
                                }
                            }
                            continue;
                        }
                        
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
                    logger.LogInfo($"Created alias: ChatMessages.Messages -> ChatMessages.All ({categoryTranslations["ChatMessages.All"].Count} translations)");
                }

                logger.LogInfo($"Loaded {loadedCount} teletext translations across {categoryTranslations.Count} categories");
                
                if (fsmStringsLoaded > 0)
                {
                    logger.LogInfo($"Loaded {fsmStringsLoaded} FSM hardcoded string translations ({fsmPatterns.Count} patterns, {fsmStringLookup.Count} simple)");
                    
                    if (!ENABLE_FSM_MONITORING)
                    {
                        logger.LogWarning("[FSM] FSM monitoring is DISABLED - game constantly overwrites FSM text, causing performance issues");
                        logger.LogWarning("[FSM] Waiting for game developer to fix this issue. Set ENABLE_FSM_MONITORING=true to re-enable.");
                    }
                }
                
                if (ENABLE_FSM_MONITORING && (fsmStringLookup.Count > 0 || fsmPatterns.Count > 0) && fsmMonitorPaths.Count > 0)
                    logger.LogInfo($"FSM monitoring enabled: {fsmMonitorPaths.Count} paths configured");
            }
            catch (System.Exception ex)
            {
                logger.LogError($"Error loading teletext translations: {ex.Message}");
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
        /// Try to apply FSM pattern matching to input text
        /// Returns true and sets translatedText if a pattern matches
        /// Example: "pakkasta 16 astetta" matches pattern "pakkasta {0} astetta" -> "영하 16도"
        /// </summary>
        private bool TryApplyFsmPattern(string input, out string translatedText)
        {
            translatedText = null;
            
            if (string.IsNullOrEmpty(input) || fsmPatterns.Count == 0)
                return false;
            
            // Try each pattern
            foreach (var kvp in fsmPatterns)
            {
                FsmPattern pattern = kvp.Value;
                
                // Try to extract values from input using this pattern
                string[] extractedValues = pattern.TryExtractValues(input);
                
                if (extractedValues != null)
                {
                    // Pattern matched! Apply translation
                    translatedText = pattern.ApplyTranslation(extractedValues);
                    return true;
                }
            }
            
            return false;
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
            fsmStringLookup.Clear();
            fsmPatterns.Clear();
            cachedTextMeshes.Clear();
            // fsmMonitorPaths is now a predefined list, don't clear it
        }
        
        /// <summary>
        /// Add FSM hardcoded string translation (for weather, bottomlines, etc.)
        /// These are strings that appear in FSM actions/variables, not in arrays
        /// Example: AddFsmString("selkeää", "맑음")
        /// </summary>
        public void AddFsmString(string original, string translation)
        {
            if (!string.IsNullOrEmpty(original) && !string.IsNullOrEmpty(translation))
            {
                fsmStringLookup[original] = translation;
            }
        }
        
        /// <summary>
        /// Lightweight FSM string monitoring - only checks specified paths
        /// Call this from Update() with deltaTime - throttled to ~1 second intervals
        /// Returns number of replacements made this check
        /// </summary>
        public int MonitorFsmStrings(float deltaTime)
        {
            // DISABLED: FSM monitoring causes performance issues due to game constantly regenerating text
            if (!ENABLE_FSM_MONITORING)
                return 0;
            
            // Early return if no translations loaded OR no paths to monitor
            if ((fsmStringLookup.Count == 0 && fsmPatterns.Count == 0) || fsmMonitorPaths.Count == 0)
                return 0;
            
            // Throttle checks to once per second (not every frame)
            const float CHECK_INTERVAL = 1.0f;
            fsmTimeSinceLastCheck += deltaTime;
            
            if (fsmTimeSinceLastCheck < CHECK_INTERVAL)
                return 0; // Skip this frame
            
            fsmTimeSinceLastCheck = 0f; // Reset timer
            int replacements = 0;
            
            foreach (string path in fsmMonitorPaths)
            {
                TextMesh tm = null;
                GameObject obj = null;
                
                // Use cached reference if available
                if (cachedTextMeshes.ContainsKey(path))
                {
                    tm = cachedTextMeshes[path];
                    if (tm == null) // Component was destroyed
                    {
                        cachedTextMeshes.Remove(path);
                        translatedFsmTexts.Remove(path); // Clear translation tracking
                        continue;
                    }
                    obj = tm.gameObject;
                }
                else
                {
                    // Find and cache the TextMesh
                    obj = GameObject.Find(path);
                    if (obj != null)
                    {
                        tm = obj.GetComponent<TextMesh>();
                        if (tm != null)
                        {
                            cachedTextMeshes[path] = tm;
                            logger.LogInfo($"[FSM] Cached new TextMesh: {path}");
                        }
                        else
                        {
                            // Only log this warning once per path
                            string warningKey = $"no_textmesh_{path}";
                            if (!loggedWarnings.Contains(warningKey))
                            {
                                logger.LogWarning($"[FSM] GameObject found but no TextMesh: {path}");
                                loggedWarnings.Add(warningKey);
                            }
                        }
                    }
                    
                    if (tm == null)
                        continue;
                }
                
                // Skip if GameObject is not active (not visible/in use)
                if (obj == null || !obj.activeInHierarchy)
                    continue;
                
                // Check if current text needs translation
                if (!string.IsNullOrEmpty(tm.text))
                {
                    string currentText = tm.text;
                    
                    // Skip if already translated and unchanged
                    if (translatedFsmTexts.TryGetValue(path, out string lastTranslated))
                    {
                        if (lastTranslated == currentText)
                        {
                            continue; // Already translated, no change
                        }
                    }
                    
                    string originalText = currentText;
                    bool translated = false;
                    
                    // First try exact match in simple lookup
                    if (fsmStringLookup.ContainsKey(currentText))
                    {
                        tm.text = fsmStringLookup[currentText];
                        logger.LogInfo($"[FSM] Exact match at {path}: '{originalText}' -> '{tm.text}'");
                        replacements++;
                        translated = true;
                        translatedFsmTexts[path] = tm.text; // Track translation
                    }

                    // Then try pattern matching ONLY for paths that need it (performance optimization)
                    else if (fsmPatternPaths.Contains(path) && fsmPatterns.Count > 0)
                    {
                        if (TryApplyFsmPattern(currentText, out string translatedText))
                        {
                            tm.text = translatedText;
                            logger.LogInfo($"[FSM] Pattern match at {path}: '{originalText}' -> '{tm.text}'");
                            replacements++;
                            translated = true;
                            translatedFsmTexts[path] = tm.text; // Track translation
                        }
                    }
                    
                    // Log if no translation found (only once per unique text)
                    if (!translated)
                    {
                        string warningKey = $"no_translation_{path}_{currentText}";
                        if (!loggedWarnings.Contains(warningKey))
                        {
                            logger.LogWarning($"[FSM] No translation for: '{currentText}' at {path}");
                            loggedWarnings.Add(warningKey);
                        }
                    }
                }
            }
            
            return replacements;
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
                        
                        // Special handling for ChatMessages.Messages: one-time check for first non-empty message
                        // This runs BEFORE the translation check because Messages uses All's translations (via alias)
                        if (categoryName == "ChatMessages.Messages" && currentCount > 0 && !hasTranslatedFirstChatMessage)
                        {
                            firstMessageFrameCounter++;
                            
                            // Only check every FIRST_MESSAGE_CHECK_INTERVAL frames (not every frame)
                            if (firstMessageFrameCounter >= FIRST_MESSAGE_CHECK_INTERVAL)
                            {
                                firstMessageFrameCounter = 0; // Reset counter
                                firstMessageRetryCount++;
                                
                                if (firstMessageRetryCount > MAX_FIRST_MESSAGE_RETRIES)
                                {
                                    logger.LogWarning($"[FirstMessage] Exceeded retry limit ({MAX_FIRST_MESSAGE_RETRIES} attempts over ~3 seconds), giving up");
                                    hasTranslatedFirstChatMessage = true; // Stop retrying
                                }
                                else
                                {
                                    logger.LogInfo($"[FirstMessage] Attempting first message translation (attempt {firstMessageRetryCount}/{MAX_FIRST_MESSAGE_RETRIES})...");
                                    // ChatMessages.Messages is aliased to ChatMessages.All during loading
                                    if (TranslateFirstNonEmptyChatMessage(proxies[i], categoryName))
                                    {
                                        totalTranslated++;
                                        hasTranslatedFirstChatMessage = true;
                                        logger.LogInfo($"[FirstMessage] Success! Flag set - will not check again");
                                    }
                                    else if (firstMessageRetryCount >= MAX_FIRST_MESSAGE_RETRIES)
                                    {
                                        logger.LogWarning($"[FirstMessage] Failed after {MAX_FIRST_MESSAGE_RETRIES} attempts - giving up");
                                        hasTranslatedFirstChatMessage = true; // Prevent infinite retries
                                    }
                                    // If not at limit yet, will retry after next interval
                                }
                            }
                        }
                        
                        // Skip if no translations for this category
                        if (!indexBasedTranslations.ContainsKey(categoryName) || 
                            indexBasedTranslations[categoryName].Count == 0) 
                            continue;
                        
                        if (currentCount > 0)
                        {
                            // Array just populated! Translate it immediately
                            int translated = TranslateArrayListProxy(proxies[i], categoryName);
                            if (translated > 0)
                            {
                                logger.LogInfo($"[Monitor] '{categoryName}' populated with {currentCount} items, replaced with {translated} translations");
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
                logger.LogError($"Error monitoring teletext arrays: {ex.Message}");
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

                    logger.LogInfo($"Found {pathPrefix}: {proxies.Length} arrays");

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
                        if (!indexBasedTranslations.ContainsKey(categoryName) || 
                            indexBasedTranslations[categoryName].Count == 0)
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
                            logger.LogInfo($"  [{i}] '{categoryName}': Translated {translated} items");
                            translatedArrays.Add(arrayKey);
                        }
                    }
                }

                if (totalTranslated > 0)
                {
                    logger.LogInfo($"Successfully translated {totalTranslated} teletext/data items!");
                }
                else
                {
                    logger.LogInfo("No pre-populated arrays to translate. Will monitor for lazy-loaded content.");
                }
                
                return totalTranslated;
            }
            catch (System.Exception ex)
            {
                logger.LogError($"Error translating teletext data: {ex.Message}");
                logger.LogError($"Stack trace: {ex.StackTrace}");
                return 0;
            }
        }

        /// <summary>
        /// Scan entire ChatMessages.Messages array and translate the first non-empty message found
        /// This only runs once for the very first message that appears
        /// Uses key-value lookup for targeted translation
        /// </summary>
        private bool TranslateFirstNonEmptyChatMessage(PlayMakerArrayListProxy proxy, string categoryName)
        {
            if (proxy == null || proxy._arrayList == null || proxy._arrayList.Count == 0)
            {
                logger.LogInfo($"[FirstMessage] Array is null or empty");
                return false;
            }

            try
            {
                logger.LogInfo($"[FirstMessage] Scanning array with {proxy._arrayList.Count} elements");
                
                // Get translation dictionary
                if (!categoryTranslations.ContainsKey(categoryName))
                {
                    logger.LogWarning($"[FirstMessage] No translation dictionary found for: {categoryName}");
                    return false;
                }
                    
                var translationDict = categoryTranslations[categoryName];
                logger.LogInfo($"[FirstMessage] Translation dictionary has {translationDict.Count} entries");
                
                // Scan entire array for first non-empty message
                for (int idx = 0; idx < proxy._arrayList.Count; idx++)
                {
                    object currentValue = proxy._arrayList[idx];
                    
                    // Skip if empty or null
                    if (currentValue == null || string.IsNullOrEmpty(currentValue.ToString()))
                        continue;

                    string currentText = currentValue.ToString().Trim();
                    
                    // Skip if empty after trim
                    if (string.IsNullOrEmpty(currentText))
                        continue;

                    // Found first non-empty message
                    logger.LogInfo($"[FirstMessage] Found non-empty message at index {idx}:");
                    logger.LogInfo($"[FirstMessage] Original text: '{currentText}'");
                    logger.LogInfo($"[FirstMessage] Text length: {currentText.Length} chars");
                    
                    // Try to translate it
                    if (translationDict.ContainsKey(currentText))
                    {
                        string translation = translationDict[currentText];
                        proxy._arrayList[idx] = translation;
                        logger.LogInfo($"[FirstMessage] ✓ Successfully translated at index {idx}");
                        logger.LogInfo($"[FirstMessage] Translation: '{translation}'");
                        return true;
                    }
                    else
                    {
                        // First message found but no translation available
                        logger.LogWarning($"[FirstMessage] ✗ No exact match in dictionary for:");
                        logger.LogWarning($"[FirstMessage] '{currentText}'");
                        logger.LogWarning($"[FirstMessage] Available keys (first 5):");
                        int count = 0;
                        foreach (var key in translationDict.Keys)
                        {
                            logger.LogWarning($"[FirstMessage]   - '{key}'");
                            if (++count >= 5) break;
                        }
                        return false;
                    }
                }
                
                // All elements were empty
                logger.LogInfo($"[FirstMessage] All {proxy._arrayList.Count} elements are empty");
                return false;
            }
            catch (System.Exception ex)
            {
                logger.LogError($"[FirstMessage] Error: {ex.Message}");
                logger.LogError($"[FirstMessage] Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Translate a single PlayMakerArrayListProxy component using index-based translations
        /// Creates NEW ArrayList and replaces it (MSC approach)
        /// Does NOT modify preFillStringList - only runtime _arrayList
        /// </summary>
        private int TranslateArrayListProxy(PlayMakerArrayListProxy proxy, string categoryName)
        {
            if (proxy == null) return 0;

            // Get translations for this category
            if (!indexBasedTranslations.ContainsKey(categoryName))
            {
                return 0;
            }

            List<string> translations = indexBasedTranslations[categoryName];
            if (translations.Count == 0)
            {
                return 0;
            }

            int translatedCount = 0;

            try
            {
                // Translate runtime array by creating NEW ArrayList (MSC approach)
                if (proxy._arrayList != null && proxy._arrayList.Count > 0)
                {
                    int originalCount = proxy._arrayList.Count;
                    ArrayList newArrayList = new ArrayList();
                    
                    // Replace items by index with translations
                    for (int i = 0; i < originalCount; i++)
                    {
                        if (i < translations.Count)
                        {
                            // Use translation at this index
                            newArrayList.Add(translations[i]);
                            translatedCount++;
                        }
                        else
                        {
                            // Keep original if no translation
                            newArrayList.Add(proxy._arrayList[i]);
                        }
                    }
                    
                    // CRITICAL: Replace entire ArrayList (this is what MSC does!)
                    proxy._arrayList = newArrayList;
                    logger.LogInfo($"[Replace] '{categoryName}': Created new ArrayList with {newArrayList.Count} items ({translatedCount} translated)");
                }
                else
                {
                    logger.LogInfo($"[Skip] '{categoryName}': Array empty or not yet populated");
                }
            }
            catch (System.Exception ex)
            {
                logger.LogError($"Error translating category '{categoryName}': {ex.Message}");
            }

            return translatedCount;
        }

        /// <summary>
        /// Reset translation state (useful for testing or scene changes)
        /// </summary>
        public void Reset()
        {
            translatedArrays.Clear();
            hasTranslatedFirstChatMessage = false;
            firstMessageRetryCount = 0;
            firstMessageFrameCounter = 0;
            translatedFsmTexts.Clear();
            loggedWarnings.Clear();
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
                
                logger.LogInfo($"Array population check: {populatedCount}/{expectedCount} arrays populated (threshold: {threshold})");
                foreach (string detail in details)
                {
                    logger.LogInfo(detail);
                }
                logger.LogInfo($"Result: {(isPopulated ? "POPULATED - will translate" : "NOT POPULATED - will retry")}");

                return isPopulated;
            }
            catch (System.Exception ex)
            {
                logger.LogError($"Error checking array population: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get diagnostic info about all data source structures (for debugging)
        /// </summary>
        public string GetTeletextInfo()
        {
            try
            {
                string info = "Data sources found:\n";
                int totalSources = 0;

                foreach (string path in pathPrefixes.Keys)
                {
                    GameObject dataObject = GameObject.Find(path);
                    if (dataObject == null) continue;

                    PlayMakerArrayListProxy[] proxies = dataObject.GetComponents<PlayMakerArrayListProxy>();
                    if (proxies == null || proxies.Length == 0) continue;

                    info += $"\n{path}: {proxies.Length} arrays\n";
                    totalSources++;

                    for (int i = 0; i < proxies.Length; i++)
                    {
                        int count = proxies[i]._arrayList != null ? proxies[i]._arrayList.Count : 0;
                        int preFillCount = proxies[i].preFillStringList != null ? proxies[i].preFillStringList.Count : 0;
                        string refName = proxies[i].referenceName ?? "null";
                        
                        string prefix = pathPrefixes[path];
                        string categoryName = string.IsNullOrEmpty(prefix) ? refName : $"{prefix}.{refName}";
                        bool hasTranslations = categoryTranslations.ContainsKey(categoryName);
                        
                        string checkMark = hasTranslations ? "OK" : "NO";
                        int transCount = hasTranslations ? categoryTranslations[categoryName].Count : 0;
                        
                        info += $"  [{i}] '{refName}' ({categoryName}): {count} runtime items, {preFillCount} preFill items [{checkMark} {transCount} translations]\n";
                    }
                }

                if (totalSources == 0)
                    return "No data sources found in this scene";

                return info;
            }
            catch (System.Exception ex)
            {
                return $"Error getting data source info: {ex.Message}";
            }
        }
    }
}
