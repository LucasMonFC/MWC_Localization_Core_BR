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
        private MWC_Localization_Core mod;
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
        private List<CriticalUIReference> criticalUIPaths = new List<CriticalUIReference>();

        private bool isInitialized = false;

        // Caches to avoid repeated GameObject.Find calls
        private Dictionary<string, TextMesh> textMeshCache;
        private Dictionary<string, GameObject> gameObjectCache = new Dictionary<string, GameObject>();
        
        // Throttling timers (MOVED from MWC_Localization_Core.cs)
        private float lastMonitorUpdateTime = 0f;
        private float lastArrayCheckTime = 0f;
        private float lastMainMenuScanTime = 0f;

        public void Initialize(
            MWC_Localization_Core modInstance, 
            TextMeshTranslator translatorInstance,
            UnifiedTextMeshMonitor textMeshMonitorInstance,
            TeletextHandler teletextHandlerInstance,
            ArrayListProxyHandler arrayListHandlerInstance,
            SceneTranslationManager sceneManagerInstance,
            Dictionary<string, TextMesh> textMeshCache)
        {
            mod = modInstance;
            translator = translatorInstance;
            textMeshMonitor = textMeshMonitorInstance;
            teletextHandler = teletextHandlerInstance;
            arrayListHandler = arrayListHandlerInstance;
            sceneManager = sceneManagerInstance;
            this.textMeshCache = textMeshCache;

            // Initialize critical UI paths (EveryFrame monitoring)
            InitializeCriticalUIPaths();
            
            isInitialized = true;
            CoreConsole.Print($"[{mod.Name}] LateUpdateHandler initialized");
        }
        
        void InitializeCriticalUIPaths()
        {
            // Hardcoded list of critical UI paths that need EveryFrame monitoring
            // These are interaction prompts, part names, subtitles that change constantly
            string[] paths = new string[]
            {
                "GUI/Indicators/Interaction",
                "GUI/Indicators/Interaction/Shadow",
                "GUI/Indicators/Partname",
                "GUI/Indicators/Partname/Shadow",
                "GUI/Indicators/Subtitles",
                "GUI/Indicators/Subtitles/Shadow",
                "GUI/Indicators/TaxiGUI",
                "GUI/Indicators/TaxiGUI/Shadow",
                "GUI/HUD/Thrist/HUDLabel",
                "GUI/HUD/Thrist/HUDLabel/Shadow",
            };
            
            foreach (string path in paths)
            {
                criticalUIPaths.Add(new CriticalUIReference
                {
                    Path = path,
                    TextMesh = null,
                    RetryCount = 0,
                    NextRetryTime = 0f,
                    IsRegistered = false
                });
            }
            
            CoreConsole.Print($"Initialized {criticalUIPaths.Count} critical UI paths for monitoring");
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
                // Register critical UI elements with retry logic (handles timing issues)
                RegisterCriticalUIElements();
                
                // CRITICAL: Check critical UI every frame (game constantly regenerates these)
                // This prevents flickering between English and translation
                UpdateCriticalUIEveryFrame();

                // Throttled monitoring for regular TextMesh elements
                if (Time.time - lastMonitorUpdateTime >= LocalizationConstants.MONITOR_UPDATE_INTERVAL)
                {
                    textMeshMonitor.Update(Time.deltaTime);
                    lastMonitorUpdateTime = Time.time;
                }
                
                // Throttled array monitoring (teletext, PlayMaker ArrayLists)
                if (Time.time - lastArrayCheckTime >= LocalizationConstants.ARRAY_MONITOR_INTERVAL)
                {
                    // Monitor teletext arrays for lazy-loaded content
                    int translated = teletextHandler.MonitorAndTranslateArrays();
                    if (translated > 0)
                    {
                        CoreConsole.Print($"[{mod.Name}] [Runtime] Translated {translated} newly-loaded teletext items");
                        // Apply Korean font to teletext display immediately after translation
                        ApplyTeletextFonts();
                    }
                    
                    // Monitor and disable FSM-driven Bottomlines (handles late initialization)
                    int fsmDisabled = teletextHandler.DisableTeletextFSMs(translator);
                    if (fsmDisabled > 0)
                    {
                        CoreConsole.Print($"[{mod.Name}] [Runtime] Disabled {fsmDisabled} Bottomline FSMs");
                    }
                    
                    // Monitor generic arrays for lazy-loaded content
                    int arrayTranslated = arrayListHandler.MonitorAndTranslateArrays();
                    if (arrayTranslated > 0)
                    {
                        CoreConsole.Print($"[{mod.Name}] [Runtime] Translated {arrayTranslated} newly-loaded array items");
                    }
                    
                    // Monitor and apply fonts to late-initialized TextMesh components
                    arrayListHandler.ApplyFontsToArrayElements();
                    
                    lastArrayCheckTime = Time.time;
                }
            }

            // Main menu monitoring - EXACT COPY from Mod_Update
            else if (currentScene == "MainMenu" && sceneManager.HasSceneBeenTranslated("MainMenu"))
            {
                // Scan for new main menu elements (throttled)
                if (Time.time - lastMainMenuScanTime >= LocalizationConstants.MAINMENU_SCAN_INTERVAL)
                {
                    ScanForNewMainMenuElements();
                    lastMainMenuScanTime = Time.time;
                }
                
                // Monitor for dynamic changes in main menu (throttled)
                if (Time.time - lastMonitorUpdateTime >= LocalizationConstants.MONITOR_UPDATE_INTERVAL)
                {
                    textMeshMonitor.Update(Time.deltaTime);
                    lastMonitorUpdateTime = Time.time;
                }
            }
        }
        
        /// <summary>
        /// Update critical UI elements every frame (not throttled)
        /// Game constantly regenerates interaction prompts, so we need to fight back
        /// </summary>
        void UpdateCriticalUIEveryFrame()
        {
            foreach (var uiRef in criticalUIPaths)
            {
                // Skip if not registered yet
                if (!uiRef.IsRegistered || uiRef.TextMesh == null)
                    continue;
                
                // Check if TextMesh still exists (might be destroyed)
                if (uiRef.TextMesh.gameObject == null)
                {
                    uiRef.TextMesh = null;
                    uiRef.IsRegistered = false;
                    continue;
                }
                
                // Always translate - game regenerates these constantly
                translator.TranslateAndApplyFont(uiRef.TextMesh, uiRef.Path, null);
            }
        }
        
        /// <summary>
        /// Register critical UI elements with cached references and retry logic
        /// Uses hardcoded allowlist to avoid expensive scene scanning
        /// </summary>
        void RegisterCriticalUIElements()
        {
            foreach (var uiRef in criticalUIPaths)
            {
                // Already registered - skip
                if (uiRef.IsRegistered && uiRef.TextMesh != null)
                    continue;
                
                // Not yet time for retry - skip
                if (uiRef.RetryCount > 0 && Time.time < uiRef.NextRetryTime)
                    continue;
                
                // Max retries reached - give up
                if (uiRef.RetryCount >= LocalizationConstants.GAMEOBJECT_FIND_MAX_RETRIES)
                    continue;
                
                // Try to find GameObject (cached)
                GameObject obj = FindGameObjectCached(uiRef.Path);
                if (obj == null)
                {
                    // Not found - schedule retry
                    uiRef.RetryCount++;
                    uiRef.NextRetryTime = Time.time + LocalizationConstants.GAMEOBJECT_FIND_RETRY_INTERVAL;
                    continue;
                }
                
                // Get TextMesh component
                TextMesh textMesh = obj.GetComponent<TextMesh>();
                if (textMesh == null)
                {
                    // No TextMesh - don't retry
                    uiRef.IsRegistered = true;
                    continue;
                }
                
                // Cache and register (but DON'T register with UnifiedTextMeshMonitor)
                // We'll handle these manually every frame to prevent flickering
                uiRef.TextMesh = textMesh;
                textMeshCache[uiRef.Path] = textMesh;
                uiRef.IsRegistered = true;
                CoreConsole.Print($"[Critical UI] Registered for every-frame checking: {uiRef.Path}");
            }
        }
        
        /// <summary>
        /// Find GameObject with caching to avoid repeated GameObject.Find calls
        /// </summary>
        GameObject FindGameObjectCached(string path)
        {
            // Check cache first
            if (gameObjectCache.TryGetValue(path, out GameObject cached))
            {
                // Verify object still exists
                if (cached != null)
                    return cached;
                
                // Object was destroyed - remove from cache
                gameObjectCache.Remove(path);
            }
            
            // Find and cache
            GameObject obj = GameObject.Find(path);
            if (obj != null)
            {
                gameObjectCache[path] = obj;
            }
            
            return obj;
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
                GameObject root = GameObject.Find(rootPath);
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
            GameObject mainMenuRoot = GameObject.Find("MainMenu");
            if (mainMenuRoot == null)
                return;

            TextMesh[] allTextMeshes = mainMenuRoot.GetComponentsInChildren<TextMesh>(true);
            
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null || string.IsNullOrEmpty(tm.text))
                    continue;

                string path = MLCUtils.GetGameObjectPath(tm.gameObject);

                // Translate and cache
                if (translator.TranslateAndApplyFont(tm, path, null))
                {
                    textMeshMonitor.Register(tm, path);
                }
            }
        }

        /// <summary>
        /// Clear cache when scene changes
        /// </summary>
        public void ClearCache()
        {
            lastMonitorUpdateTime = 0f;
            lastArrayCheckTime = 0f;
            lastMainMenuScanTime = 0f;
            gameObjectCache.Clear();
                
            // Reset critical UI references for new scene
            foreach (var uiRef in criticalUIPaths)
            {
                uiRef.TextMesh = null;
                uiRef.RetryCount = 0;
                uiRef.NextRetryTime = Time.time + LocalizationConstants.GAMEOBJECT_FIND_RETRY_DELAY;
                uiRef.IsRegistered = false;
            }

            isInitialized = false;
        }
    }
}
