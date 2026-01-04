// Generic handler for translating PlayMakerArrayListProxy data
// Supports any GameObject with array-based content (HUD, menus, etc.)

using BepInEx.Logging;
using HutongGames.PlayMaker;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MWC_Localization_Core
{
    public class ArrayListProxyHandler
    {
        private ManualLogSource logger;
        
        // Reference to main translation dictionaries (from Plugin)
        private Dictionary<string, string> mainTranslations;
        private MagazineTextHandler magazineHandler;
        private TextMeshTranslator translator;
        
        // Config: List of array paths to translate (path:index)
        // Example: "GUI/HUD/Day/HUDValue:0"
        private HashSet<string> arrayPaths = new HashSet<string>();
        
        // Track which arrays we've already translated
        private HashSet<string> translatedArrays = new HashSet<string>();
        
        // Track which TextMesh instances already have fonts applied (by instance ID)
        private HashSet<int> fontAppliedInstances = new HashSet<int>();
        
        // Track which parent paths have been fully processed (all TextMeshes found)
        private HashSet<string> completedParentPaths = new HashSet<string>();
        
        // Parent paths to search for TextMesh components (for performance)
        // Apply fonts to ALL TextMeshes under these paths
        private List<string> parentSearchPaths;
        
        // Cache for array proxies to avoid repeated lookups
        private Dictionary<string, PlayMakerArrayListProxy> arrayProxyCache 
            = new Dictionary<string, PlayMakerArrayListProxy>();

        public ArrayListProxyHandler(ManualLogSource log, Dictionary<string, string> translations, MagazineTextHandler magazineHandler, TextMeshTranslator translator)
        {
            logger = log;
            this.mainTranslations = translations;
            this.magazineHandler = magazineHandler;
            this.translator = translator;
        }

        public void InitializeArrayPaths()
        {
            // Hardcoded array paths discovered from game data
            // Format: "GameObject/Path:ComponentIndex"
            arrayPaths.Clear();
            
            // HUD Elements
            arrayPaths.Add("GUI/HUD/Day/HUDValue:0");  // Day names: MONDAY, TUESDAY, etc.
            
            // Bank System
            arrayPaths.Add("Systems/BankAccount:0");  // Transaction types: Nosto, Asumistuki, etc.
            
            // Magazine System
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsFI:0"); // LinesSelected (FI)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsFI:1"); // LinesRandom1 (FI)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsFI:2"); // LinesRandom2 (FI)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsEN:0"); // LinesSelected (EN)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsEN:1"); // LinesRandom1 (EN)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsEN:2"); // LinesRandom2 (EN)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/VINLIST_TirePics:1"); // Tire picture descriptions (FI)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/VINLIST_TirePics:2"); // Tire picture descriptions (EN)

            // Subtitles
            arrayPaths.Add("PERAJARVI/Kunnalliskoti/Functions/RoomUncle/Sitting/UncleDrinking/Uncle:1"); // Uncle
            arrayPaths.Add("PERAPORTTI/Building/LOD/Staff/AlaCarteRunnerPIVOT/Jouni/Audio:3"); // Food Court AngrySub
            arrayPaths.Add("PERAPORTTI/Building/LOD/Staff/AlaCarteRunnerPIVOT/Jouni/Audio:4"); // Food Court DeliverSub
            arrayPaths.Add("PERAPORTTI/Building/LOD/Staff/AlaCarteRunnerPIVOT/Jouni/Audio:5"); // Food Court EnjoySub
            arrayPaths.Add("STORE_AREA/TeimoInBar/Pivot/Speak:1"); // Pub Nappo HelloSub
            arrayPaths.Add("STORE_AREA/TeimoInBar/Pivot/Speak:3"); // Pub Nappo GoodbyeSub
            arrayPaths.Add("STORE_AREA/TeimoInBar/Pivot/Speak:5"); // Pub Nappo RandomSub
            arrayPaths.Add("STORE_AREA/TeimoInBar/Pivot/Speak:7"); // Pub Nappo CoffeeSub
            arrayPaths.Add("STORE_AREA/Stuff/LOD/GFX_Pub/PubCashRegister/CashRegisterLogic:1"); // Pub Nappo CoffeeSub
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PhoneNumbers:2"); // Car parts phone call subtitles
            
            // Add more paths here as discovered from game dumps
            // Use BepInEx Dumper to find: GameObject path + component index
            
            logger.LogInfo($"Initialized {arrayPaths.Count} array paths to monitor");
            
            // Initialize TextMesh display path mappings
            InitializeTextMeshMappings();
        }

        private void InitializeTextMeshMappings()
        {
            // Parent paths to search under - apply fonts to ALL TextMeshes under these paths
            // Paths are specific enough that all children should get Korean fonts
            parentSearchPaths = new List<string>
            {
                "GUI/HUD/Day",                            // HUD Day Display
                "PERAPORTTI/ATMs/MoneyATM/Screen",        // Bank Account Display
                "Sheets/YellowPagesMagazine/Page1/Row1",  // Magazine Page 1 Row 1
                "Sheets/YellowPagesMagazine/Page1/Row2",  // Magazine Page 1 Row 2
                "Sheets/YellowPagesMagazine/Page2/Row3",  // Magazine Page 2 Row 3
                "Sheets/YellowPagesMagazine/Page2/Row4",  // Magazine Page 2 Row 4
                // Add more parent paths as needed
            };
            
            logger.LogInfo($"Initialized {parentSearchPaths.Count} parent search paths for font application");
        }

        public void ClearTranslations()
        {
            translatedArrays.Clear();
            arrayProxyCache.Clear();
            fontAppliedInstances.Clear();
            completedParentPaths.Clear();
        }

        public void Reset()
        {
            translatedArrays.Clear();
            arrayProxyCache.Clear();
            fontAppliedInstances.Clear();
            completedParentPaths.Clear();
        }

        // Translate all configured arrays (call once per scene)
        public int TranslateAllArrays()
        {
            int totalTranslated = 0;

            foreach (string arrayKey in arrayPaths)
            {
                // Skip if already translated
                if (translatedArrays.Contains(arrayKey))
                    continue;

                int translated = TranslateArray(arrayKey);
                if (translated > 0)
                {
                    totalTranslated += translated;
                    translatedArrays.Add(arrayKey);
                }
            }

            return totalTranslated;
        }

        // Translate a specific array by path:index key
        private int TranslateArray(string arrayKey)
        {
            // Parse arrayKey: "GameObject/Path:ComponentIndex"
            if (!arrayKey.Contains(":"))
            {
                logger.LogWarning($"Invalid array key format (expected 'path:index'): {arrayKey}");
                return 0;
            }

            string[] parts = arrayKey.Split(':');
            string objectPath = parts[0];
            int componentIndex;

            if (!int.TryParse(parts[1], out componentIndex))
            {
                logger.LogWarning($"Invalid component index in array key: {arrayKey}");
                return 0;
            }

            // Find GameObject
            GameObject obj = GameObject.Find(objectPath);
            if (obj == null)
            {
                // Not available yet - this is normal for lazy-loaded content
                return 0;
            }

            // Get PlayMakerArrayListProxy component
            PlayMakerArrayListProxy[] proxies = obj.GetComponents<PlayMakerArrayListProxy>();
            if (proxies == null || componentIndex >= proxies.Length)
            {
                logger.LogWarning($"PlayMakerArrayListProxy[{componentIndex}] not found at {objectPath}");
                return 0;
            }

            PlayMakerArrayListProxy proxy = proxies[componentIndex];
            if (proxy == null || proxy.arrayList == null)
            {
                return 0;
            }

            // Cache the proxy for later monitoring
            arrayProxyCache[arrayKey] = proxy;

            // Translate array contents using existing translation dictionaries
            int translatedCount = 0;
            ArrayList arrayList = proxy.arrayList;

            for (int i = 0; i < arrayList.Count; i++)
            {
                if (arrayList[i] == null)
                    continue;

                string original = arrayList[i].ToString();
                if (string.IsNullOrEmpty(original))
                    continue;

                string translation = FindTranslation(original);
                if (translation != null)
                {
                    arrayList[i] = translation;
                    translatedCount++;
                }
            }

            if (translatedCount > 0)
            {
                logger.LogInfo($"[Array] Translated {translatedCount}/{arrayList.Count} items in {arrayKey}");
            }

            return translatedCount;
        }

        // Monitor arrays for runtime changes (like teletext lazy-loading)
        // Returns number of newly translated items
        public int MonitorAndTranslateArrays()
        {
            int totalTranslated = 0;

            foreach (string arrayKey in arrayPaths)
            {
                // Try to translate arrays that failed before (not yet loaded)
                if (!translatedArrays.Contains(arrayKey))
                {
                    int translated = TranslateArray(arrayKey);
                    if (translated > 0)
                    {
                        totalTranslated += translated;
                        translatedArrays.Add(arrayKey);
                    }
                }
                // Also check cached arrays for new content
                else if (arrayProxyCache.ContainsKey(arrayKey))
                {
                    PlayMakerArrayListProxy proxy = arrayProxyCache[arrayKey];
                    if (proxy != null && proxy.arrayList != null)
                    {
                        int translated = TranslateCachedArray(proxy);
                        if (translated > 0)
                        {
                            totalTranslated += translated;
                        }
                    }
                }
            }

            return totalTranslated;
        }

        // Check a cached array for untranslated items (handles dynamic content)
        private int TranslateCachedArray(PlayMakerArrayListProxy proxy)
        {
            if (proxy == null || proxy.arrayList == null)
                return 0;

            int translatedCount = 0;
            ArrayList arrayList = proxy.arrayList;

            for (int i = 0; i < arrayList.Count; i++)
            {
                if (arrayList[i] == null)
                    continue;

                string current = arrayList[i].ToString();
                if (string.IsNullOrEmpty(current))
                    continue;

                // Check if this is Finnish (original) text that needs translation
                string translation = FindTranslation(current);
                if (translation != null)
                {
                    // Found untranslated Finnish text - translate it
                    arrayList[i] = translation;
                    translatedCount++;
                }
            }

            return translatedCount;
        }

        // Find translation from main translations or magazine translations
        private string FindTranslation(string original)
        {
            // Try main translations first (with normalized key)
            string normalizedKey = StringHelper.FormatUpperKey(original);
            if (mainTranslations.TryGetValue(normalizedKey, out string translation))
            {
                return translation;
            }

            // Try magazine translations (exact match)
            translation = magazineHandler.GetTranslation(original);
            if (translation != null)
            {
                return translation;
            }

            return null;
        }

        // Apply Korean fonts to TextMesh components displaying array data
        // Call this once during scene initialization, then periodically until all paths are complete
        public int ApplyFontsToArrayElements()
        {
            if (translator == null)
                return 0;

            // Early exit if all parent paths have been fully processed
            if (completedParentPaths.Count >= parentSearchPaths.Count)
                return 0;

            int fontsApplied = 0;

            // Search only under known parent paths (MUCH faster than FindObjectsOfType)
            foreach (string parentPath in parentSearchPaths)
            {
                // Skip already completed parent paths (huge performance boost)
                if (completedParentPaths.Contains(parentPath))
                    continue;

                GameObject parent = GameObject.Find(parentPath);
                if (parent == null)
                    continue; // Not loaded yet - will try again later

                // Get all TextMesh components under this parent and apply fonts to ALL of them
                TextMesh[] textMeshes = parent.GetComponentsInChildren<TextMesh>(true);
                
                bool anyNewFonts = false;

                foreach (TextMesh textMesh in textMeshes)
                {
                    if (textMesh == null)
                        continue;

                    // Skip if already processed this instance
                    int instanceId = textMesh.GetInstanceID();
                    if (fontAppliedInstances.Contains(instanceId))
                        continue;

                    string textMeshPath = GetGameObjectPath(textMesh.gameObject);

                    // Apply font to this TextMesh
                    if (translator.ApplyFontOnly(textMesh, textMeshPath))
                    {
                        fontsApplied++;
                        fontAppliedInstances.Add(instanceId);
                        anyNewFonts = true;
                    }
                }

                // Mark this parent path as complete if we found the parent and processed all its TextMeshes
                // (If anyNewFonts is false, it means all TextMeshes were already processed)
                if (!anyNewFonts)
                {
                    completedParentPaths.Add(parentPath);
                }
            }

            if (fontsApplied > 0)
            {
                logger.LogInfo($"[Array Fonts] Applied Korean font to {fontsApplied} TextMesh components ({completedParentPaths.Count}/{parentSearchPaths.Count} paths complete)");
            }

            return fontsApplied;
        }

        // Helper to get full GameObject path
        private string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return "";

            StringBuilder pathBuilder = new StringBuilder();
            Transform current = obj.transform;

            while (current != null)
            {
                if (pathBuilder.Length > 0)
                    pathBuilder.Insert(0, "/");
                pathBuilder.Insert(0, current.name);
                current = current.parent;
            }

            return pathBuilder.ToString();
        }

        // Diagnostic info
        public string GetDiagnostics()
        {
            return $"[ArrayListProxy] {translatedArrays.Count}/{arrayPaths.Count} arrays translated, {fontAppliedInstances.Count} fonts applied, {arrayProxyCache.Count} cached";
        }
    }
}
