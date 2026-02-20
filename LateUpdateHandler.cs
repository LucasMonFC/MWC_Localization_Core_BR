using MSCLoader;
using UnityEngine;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    /// <summary>
    /// MonoBehaviour component for ALL continuous translation monitoring
    /// Must run in LateUpdate() to translate AFTER game's Update() regenerates text
    /// DIRECT COPY from Plugin.cs LateUpdate - all monitoring logic moved here
    /// Same pattern as legacy LanguageFramework's MainObject for My Summer Car
    /// </summary>
    public class LateUpdateHandler : MonoBehaviour
    {
        // Dependencies
        private TextMeshTranslator translator;
        private UnifiedTextMeshMonitor textMeshMonitor;
        private TeletextHandler teletextHandler;
        private ArrayListProxyHandler arrayListHandler;
        private SceneTranslationManager sceneManager;
        
        // Cached references for critical UI (EveryFrame monitoring)
        private class CriticalUIReference
        {
            public string Path;
            public TextMesh TextMesh;
            public int RetryCount;
            public float NextRetryTime;
            public bool IsRegistered;
        }

        private bool isInitialized = false;
        
        // Throttling timers (MOVED from MWC_Localization_Core.cs)
        private float lastArrayCheckTime = 0f;

        public void Initialize(
            TextMeshTranslator translatorInstance,
            UnifiedTextMeshMonitor textMeshMonitorInstance,
            TeletextHandler teletextHandlerInstance,
            ArrayListProxyHandler arrayListHandlerInstance,
            SceneTranslationManager sceneManagerInstance)
        {
            translator = translatorInstance;
            textMeshMonitor = textMeshMonitorInstance;
            teletextHandler = teletextHandlerInstance;
            arrayListHandler = arrayListHandlerInstance;
            sceneManager = sceneManagerInstance;
            isInitialized = true;
        }

        /// <summary>
        /// LateUpdate runs AFTER all Update() calls (including MSCLoader and game's Update)
        /// This ensures we translate AFTER the game regenerates the text
        /// </summary>
        private void LateUpdate()
        {
            if (!isInitialized)
                return;

            string currentScene = Application.loadedLevelName;

            // GAME scene monitoring - EXACT COPY from Mod_Update
            if (currentScene == "GAME" && sceneManager.HasSceneBeenTranslated("GAME"))
            {
                // Throttled monitoring for regular TextMesh elements
                textMeshMonitor.Update(Time.deltaTime);
                
                // Throttled array monitoring (teletext, PlayMaker ArrayLists)
                if (Time.time - lastArrayCheckTime >= LocalizationConstants.ARRAY_MONITOR_INTERVAL)
                {
                    // Monitor teletext arrays for lazy-loaded content
                    int translated = teletextHandler.MonitorAndTranslateArrays();
                    if (translated > 0)
                    {
                        CoreConsole.Print($"[LateUpdateHandler] Translated {translated} newly-loaded teletext items");
                        // Apply Korean font to teletext display immediately after translation
                        ApplyTeletextFonts();
                    }
                    
                    // Monitor and disable FSM-driven Bottomlines (handles late initialization)
                    int fsmDisabled = teletextHandler.DisableTeletextFSMs(translator);
                    if (fsmDisabled > 0)
                    {
                        CoreConsole.Print($"[LateUpdateHandler] Disabled {fsmDisabled} Bottomline FSMs");
                    }
                    
                    // Monitor generic arrays for lazy-loaded content
                    int arrayTranslated = arrayListHandler.MonitorAndTranslateArrays();
                    if (arrayTranslated > 0)
                    {
                        CoreConsole.Print($"[LateUpdateHandler] Translated {arrayTranslated} newly-loaded array items");
                    }
                    
                    // Monitor and apply fonts to late-initialized TextMesh components
                    arrayListHandler.ApplyFontsToArrayElements();
                    
                    lastArrayCheckTime = Time.time;
                }
            }

            // Main menu monitoring - EXACT COPY from Mod_Update
            else if (currentScene == "MainMenu" && sceneManager.HasSceneBeenTranslated("MainMenu"))
            {
                // Monitor for dynamic changes in main menu
                textMeshMonitor.Update(Time.deltaTime);
            }
        }
        
        /// <summary>
        /// Helper method - Apply fonts to teletext display
        /// </summary>
        private void ApplyTeletextFonts()
        {
            List<string> teletextRootPaths = new List<string>
            {
                "Systems/TV/Teletext/VKTekstiTV/PAGES",
                "Systems/TV/TVGraphics/CHAT/Generated/Lines"
            };

            int fontChangedCount = 0;

            foreach (string rootPath in teletextRootPaths)
            {
                GameObject root = MLCUtils.FindGameObjectCached(rootPath);
                if (root == null)
                    continue;

                // Get all TextMesh components in teletext display
                TextMesh[] textMeshes = root.GetComponentsInChildren<TextMesh>(true);

                foreach (TextMesh textMesh in textMeshes)
                {
                    if (textMesh == null)
                        continue;

                    string path = MLCUtils.GetGameObjectPath(textMesh.gameObject);

                    // Apply font using the translator's font mapping logic
                    if (translator.ApplyFontOnly(textMesh, path))
                    {
                        fontChangedCount++;
                    }
                }
            }
        }
        
        /// <summary>
        /// Helper method - Scan for new main menu elements
        /// </summary>
        private void ScanForNewMainMenuElements()
        {
            GameObject mainMenuRoot = MLCUtils.FindGameObjectCached("MainMenu");
            if (mainMenuRoot == null)
                return;

            TextMesh[] allTextMeshes = mainMenuRoot.GetComponentsInChildren<TextMesh>(true);
            
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null || string.IsNullOrEmpty(tm.text))
                    continue;

                string path = MLCUtils.GetGameObjectPath(tm.gameObject);
                translator.TranslateAndApplyFont(tm, path, null);
            }
        }

        /// <summary>
        /// Clear cache when scene changes
        /// </summary>
        public void ClearCache()
        {
            lastArrayCheckTime = 0f;
            isInitialized = false;
        }
    }
}
