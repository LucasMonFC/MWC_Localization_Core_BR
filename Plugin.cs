// My Winter Car - Localization Plugin
// BepInEx Plugin for multi-language translation support
// Installation: Place compiled DLL in BepInEx/plugins/

using BepInEx;
using BepInEx.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MWC_Localization_Core
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class LocalizationPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.potatosalad.mwc_localization_core";
        public const string PluginName = "MWC Localization Core";
        public const string Version = "0.4.0";

        private static ManualLogSource _logger;

        // Translation data
        private Dictionary<string, string> translations = new Dictionary<string, string>();
        private bool hasLoadedTranslations = false;

        // Core handlers
        private MagazineTextHandler magazineHandler;
        private TeletextHandler teletextHandler;
        private ArrayListProxyHandler arrayListHandler;
        private TextMeshTranslator translator;

        // NEW: Unified managers
        private SceneTranslationManager sceneManager;
        private UnifiedTextMeshMonitor textMeshMonitor;
        
        // Teletext translation tracking
        private float teletextTranslationTime = 0f;
        private int teletextRetryCount = 0;

        // Font management
        private AssetBundle fontBundle;
        private Dictionary<string, Font> customFonts = new Dictionary<string, Font>();

        // Localization configuration
        private LocalizationConfig config;

        // GameObject path cache for performance
        private Dictionary<GameObject, string> pathCache = new Dictionary<GameObject, string>();
        
        // GameObject reference cache (avoid repeated GameObject.Find calls)
        private Dictionary<string, GameObject> gameObjectCache = new Dictionary<string, GameObject>();
        private Dictionary<string, TextMesh> textMeshCache = new Dictionary<string, TextMesh>();
        
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
        
        // Minimal tracking for scene scanning
        private float lastMainMenuScanTime = 0f;
        private float lastMonitorUpdateTime = 0f;
        private float lastArrayCheckTime = 0f;

        void Awake()
        {
            _logger = Logger;
            _logger.LogInfo($"{PluginName} v{Version} - Unified Architecture loaded!");

            // Initialize configuration
            config = new LocalizationConfig(_logger);
            string configPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "config.txt");
            config.LoadConfig(configPath);

            // Initialize handlers
            magazineHandler = new MagazineTextHandler(_logger);
            teletextHandler = new TeletextHandler(_logger);

            // Initialize scene manager
            sceneManager = new SceneTranslationManager(_logger);

            // Load translations immediately
            LoadTranslations();

            // Load magazine translations from separate file
            string magazinePath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);
            
            // Load teletext translations from separate file
            string teletextPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_teletext.txt");
            teletextHandler.LoadTeletextTranslations(teletextPath);
            
        }

        void Start()
        {
            // Load fonts in Start() instead of Awake() - Unity's AssetBundle system
            // needs to be fully initialized before CreateFromMemoryImmediate works
            _logger.LogInfo("Start() - Loading fonts...");

            // Try asset bundle first, fallback to Default font
            LoadCustomFonts();

            // Initialize translator after fonts are loaded
            translator = new TextMeshTranslator(translations, customFonts, magazineHandler, config, _logger);
            
            // Initialize array handler with translation dictionaries and translator
            arrayListHandler = new ArrayListProxyHandler(_logger, translations, magazineHandler, translator);
            arrayListHandler.InitializeArrayPaths();
            
            // Load FSM patterns into pattern matcher
            string teletextPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_teletext.txt");
            translator.LoadFsmPatterns(teletextPath);
            
            // Initialize unified text mesh monitor
            textMeshMonitor = new UnifiedTextMeshMonitor(translator, _logger);
            
            // Initialize critical UI paths (EveryFrame monitoring)
            InitializeCriticalUIPaths();
            
            _logger.LogInfo("Unified architecture initialized successfully");
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
                "GUI/Indicators/Subtitles/Shadow"
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
            
            _logger.LogInfo($"Initialized {criticalUIPaths.Count} critical UI paths for monitoring");
        }

        bool LoadCustomFonts()
        {
            // Skip font loading if no font mappings configured
            if (config.FontMappings.Count == 0)
            {
                _logger.LogInfo("No font mappings configured - using default fonts");
                return false;
            }

            string bundlePath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "fonts.unity3d");

            if (!File.Exists(bundlePath))
            {
                _logger.LogWarning($"Font bundle not found: {bundlePath}");
                return false;
            }

            try
            {
                _logger.LogInfo($"Loading font bundle from: {bundlePath}");
                fontBundle = LoadBundle(bundlePath);

                if (fontBundle == null)
                {
                    _logger.LogError("Failed to create AssetBundle from file");
                    return false;
                }

                _logger.LogInfo($"AssetBundle loaded successfully");

                // Load fonts from config mappings
                foreach (var pair in config.FontMappings)
                {
                    string originalFontName = pair.Key;
                    string assetFontName = pair.Value;

                    Font font = fontBundle.LoadAsset(assetFontName, typeof(Font)) as Font;
                    if (font != null)
                    {
                        customFonts[originalFontName] = font;
                        _logger.LogInfo($"Loaded {assetFontName} for {originalFontName}");
                    }
                }

                if (customFonts.Count > 0)
                {
                    _logger.LogInfo($"Successfully loaded {customFonts.Count} custom fonts from asset bundle");
                    return true;
                }
                else
                {
                    _logger.LogWarning("No fonts were loaded from asset bundle");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Failed to load font bundle: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        void InsertTranslationLines(string translationPath)
        {
            try
            {
                string[] lines = File.ReadAllLines(translationPath, Encoding.UTF8);
                foreach (string line in lines)
                {
                    // Skip empty lines and comments
                    if (line.IsNullOrWhiteSpace() || line.TrimStart().StartsWith("#"))
                        continue;

                    // Parse KEY=VALUE format
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = line.Substring(0, equalsIndex).Trim();
                        string value = line.Substring(equalsIndex + 1).Trim();

                        // Normalize key (remove spaces, convert to uppercase)
                        key = StringHelper.FormatUpperKey(key);

                        // Handle escaped newlines in value
                        value = value.Replace("\\n", "\n");

                        if (!translations.ContainsKey(key))
                        {
                            translations[key] = value;
                        }
                    }
                }
                _logger.LogInfo($"Loaded {translations.Count} translations from {translationPath}");
                hasLoadedTranslations = true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Failed to load translations: {ex.Message}");
            }
        }

        void LoadTranslations()
        {
            // Load translation file used in My Summer Car first
            string mscTranslationPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_msc.txt");
            
            if (!File.Exists(mscTranslationPath))
            {
                _logger.LogWarning($"Translation file not found: {mscTranslationPath}");
            }
            else 
            {
                InsertTranslationLines(mscTranslationPath);
            }

            // Load main translation file for My Winter Car
            string translationPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate.txt");

            if (!File.Exists(translationPath))
            {
                _logger.LogWarning($"Translation file not found: {translationPath}");
                return;
            }
            else 
            {
                InsertTranslationLines(translationPath);
            }
        }

        void ReloadTranslations()
        {
            _logger.LogInfo("[F8] Reloading translations...");

            // Clear existing translations
            translations.Clear();
            magazineHandler.ClearTranslations();
            arrayListHandler.ClearTranslations();

            // Reload from file
            LoadTranslations();

            // Reload magazine translations
            string magazinePath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);
            
            // Reload teletext translations
            string teletextPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_teletext.txt");
            teletextHandler.LoadTeletextTranslations(teletextPath);
            
            // Reload FSM patterns
            translator.LoadFsmPatterns(teletextPath);

            // Clear all caches
            pathCache.Clear();
            gameObjectCache.Clear();
            textMeshCache.Clear();
            lastMainMenuScanTime = 0f;
            lastMonitorUpdateTime = 0f;
            lastArrayCheckTime = 0f;
            
            // Reset critical UI references
            foreach (var uiRef in criticalUIPaths)
            {
                uiRef.TextMesh = null;
                uiRef.RetryCount = 0;
                uiRef.NextRetryTime = 0f;
                uiRef.IsRegistered = false;
            }

            // Clear position adjustment caches
            config.ClearPositionAdjustmentCaches();

            // Reset managers
            sceneManager.ResetAll();
            textMeshMonitor.Clear();
            
            // Reset teletext handler
            teletextHandler.Reset();
            arrayListHandler.Reset();
            teletextTranslationTime = 0f;
            teletextRetryCount = 0;

            _logger.LogInfo($"[F8] Reloaded {translations.Count} translations. Current scene will be re-translated.");
        }

        void Update()
        {
            if (!hasLoadedTranslations)
                return;

            // F8 key: Reload translations at runtime
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ReloadTranslations();
                return;
            }

            string currentScene = Application.loadedLevelName;
            
            // Update scene manager and handle scene changes
            bool sceneChanged = sceneManager.UpdateScene(currentScene);
            
            // Clear caches when entering a new scene
            if (sceneChanged)
            {
                pathCache.Clear();
                gameObjectCache.Clear();
                textMeshCache.Clear();
                
                // Reset critical UI references for new scene
                foreach (var uiRef in criticalUIPaths)
                {
                    uiRef.TextMesh = null;
                    uiRef.RetryCount = 0;
                    uiRef.NextRetryTime = Time.time + LocalizationConstants.GAMEOBJECT_FIND_RETRY_DELAY;
                    uiRef.IsRegistered = false;
                }
                
                _logger.LogInfo($"Scene changed to '{currentScene}' - cleared caches");
            }

            // Initial translation pass for Splash Screen
            if (currentScene == "SplashScreen" && sceneManager.ShouldTranslateScene("SplashScreen"))
            {
                _logger.LogInfo("Translating Splash Screen...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("SplashScreen");
            }

            // Initial translation pass for Main Menu
            if (currentScene == "MainMenu" && sceneManager.ShouldTranslateScene("MainMenu"))
            {
                _logger.LogInfo("Translating Main Menu...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("MainMenu");
            }

            // Initial translation pass for Game scene
            if (currentScene == "GAME" && sceneManager.ShouldTranslateScene("GAME"))
            {
                _logger.LogInfo("Translating Game scene...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("GAME");
                
                // Reset teletext handler and retry tracking for new scene
                teletextHandler.Reset();
                arrayListHandler.Reset();
                teletextRetryCount = 0;
                
                // Translate static arrays immediately (HUD, menus, etc.)
                int arrayTranslated = arrayListHandler.TranslateAllArrays();
                if (arrayTranslated > 0)
                {
                    _logger.LogInfo($"[Arrays] Initial translation: {arrayTranslated} items");
                    // Apply Korean fonts to TextMesh components using array data
                    arrayListHandler.ApplyFontsToArrayElements();
                }
                
                // Schedule teletext translation after delay
                teletextTranslationTime = Time.time + LocalizationConstants.TELETEXT_TRANSLATION_DELAY;
            }
            
            // Translate teletext data after delay (allow scene to fully initialize)
            // Uses retry logic because teletext arrays populate gradually
            if (currentScene == "GAME" && sceneManager.HasSceneBeenTranslated("GAME") && 
                teletextTranslationTime > 0 && Time.time >= teletextTranslationTime)
            {
                _logger.LogInfo($"Attempting teletext translation (retry {teletextRetryCount + 1}/{LocalizationConstants.TELETEXT_MAX_RETRIES})...");
                
                if (teletextHandler.IsTeletextAvailable())
                {
                    string info = teletextHandler.GetTeletextInfo();
                    _logger.LogInfo(info);
                    
                    // Check if arrays are populated before translating
                    bool arraysPopulated = teletextHandler.AreTeletextArraysPopulated();
                    
                    if (arraysPopulated)
                    {
                        // Arrays have data, do the translation
                        int translatedCount = teletextHandler.TranslateTeletextData();
                        _logger.LogInfo($"Arrays populated! Translated {translatedCount} items.");
                        teletextTranslationTime = 0f;  // Done
                        teletextRetryCount = 0;
                    }
                    else if (teletextRetryCount >= LocalizationConstants.TELETEXT_MAX_RETRIES - 1)
                    {
                        // Gave up waiting
                        _logger.LogWarning($"Arrays still empty after {LocalizationConstants.TELETEXT_MAX_RETRIES} retries. They may populate later.");
                        // Try anyway in case some arrays have data
                        int translatedCount = teletextHandler.TranslateTeletextData();
                        if (translatedCount > 0)
                        {
                            _logger.LogInfo($"Translated {translatedCount} items from partially populated arrays.");
                        }
                        teletextTranslationTime = 0f;
                        teletextRetryCount = 0;
                    }
                    else
                    {
                        // Retry - arrays still empty
                        teletextRetryCount++;
                        teletextTranslationTime = Time.time + LocalizationConstants.TELETEXT_RETRY_INTERVAL;
                        _logger.LogInfo($"Arrays not yet populated, will retry in {LocalizationConstants.TELETEXT_RETRY_INTERVAL}s...");
                    }
                }
                else
                {
                    _logger.LogInfo("Teletext not available in this scene.");
                    teletextTranslationTime = 0f;
                    teletextRetryCount = 0;
                }
            }
        }

        void LateUpdate()
        {
            string currentScene = Application.loadedLevelName;

            if (currentScene == "GAME" && sceneManager.HasSceneBeenTranslated("GAME"))
            {
                // Register critical UI elements with retry logic (handles timing issues)
                RegisterCriticalUIElements();
                
                // CRITICAL: Check critical UI every frame (game constantly regenerates these)
                // This prevents flickering between English and translation
                UpdateCriticalUIEveryFrame();
                
                // Throttle unified TextMesh monitor to 20 FPS for non-critical elements
                if (Time.time - lastMonitorUpdateTime >= LocalizationConstants.MONITOR_UPDATE_INTERVAL)
                {
                    textMeshMonitor.Update(Time.deltaTime);
                    lastMonitorUpdateTime = Time.time;
                }

                // Throttle array monitoring to 1 second instead of every frame
                if (Time.time - lastArrayCheckTime >= LocalizationConstants.ARRAY_MONITOR_INTERVAL)
                {
                    // Monitor teletext arrays for lazy-loaded content
                    int translated = teletextHandler.MonitorAndTranslateArrays();
                    if (translated > 0)
                    {
                        _logger.LogInfo($"[Runtime] Translated {translated} newly-loaded teletext items");
                        // Apply Korean font to teletext display immediately after translation
                        ApplyTeletextFonts();
                    }
                    
                    // Monitor generic arrays for lazy-loaded content
                    int arrayTranslated = arrayListHandler.MonitorAndTranslateArrays();
                    if (arrayTranslated > 0)
                    {
                        _logger.LogInfo($"[Runtime] Translated {arrayTranslated} newly-loaded array items");
                    }
                    
                    // Monitor and apply fonts to late-initialized TextMesh components
                    arrayListHandler.ApplyFontsToArrayElements();
                    
                    lastArrayCheckTime = Time.time;
                }
            }
            else if (currentScene == "MainMenu" && sceneManager.HasSceneBeenTranslated("MainMenu"))
            {
                // Throttle main menu scanning to every 5 seconds
                if (Time.time - lastMainMenuScanTime >= LocalizationConstants.MAINMENU_SCAN_INTERVAL)
                {
                    ScanForNewMainMenuElements();
                    lastMainMenuScanTime = Time.time;
                }
                
                // Throttle monitor update
                if (Time.time - lastMonitorUpdateTime >= LocalizationConstants.MONITOR_UPDATE_INTERVAL)
                {
                    textMeshMonitor.Update(Time.deltaTime);
                    lastMonitorUpdateTime = Time.time;
                }
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
                _logger.LogInfo($"[Critical UI] Registered for every-frame checking: {uiRef.Path}");
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

        void ScanForNewMainMenuElements()
        {
            // Simplified main menu scanning - only check known paths
            // Avoid expensive Resources.FindObjectsOfTypeAll()
            
            // Known main menu UI paths (add more as discovered)
            string[] knownMainMenuPaths = new string[]
            {
                "GUI/MainMenu",
                "GUI/OptionsMenu",
                "GUI/CreditsMenu"
            };
            
            foreach (string basePath in knownMainMenuPaths)
            {
                GameObject baseObj = FindGameObjectCached(basePath);
                if (baseObj == null)
                    continue;
                
                // Get all TextMesh components under this path
                TextMesh[] textMeshes = baseObj.GetComponentsInChildren<TextMesh>(true);
                foreach (TextMesh textMesh in textMeshes)
                {
                    if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                        continue;
                    
                    // Skip if already cached
                    string path = GetGameObjectPath(textMesh.gameObject);
                    if (textMeshCache.ContainsKey(path))
                        continue;
                    
                    // Translate and cache
                    if (translator.TranslateAndApplyFont(textMesh, path, null))
                    {
                        textMeshCache[path] = textMesh;
                        textMeshMonitor.Register(textMesh, path);
                    }
                }
            }
        }

        void ApplyTeletextFonts()
        {
            List<string> teletextRootPaths = new List<string>
            {
                "Systems/TV/Teletext/VKTekstiTV/PAGES",
                "Systems/TV/TVGraphics/CHAT/Generated/Lines"
            };

            foreach (string rootPath in teletextRootPaths)
            {
                GameObject teletextRoot = GameObject.Find(rootPath);
                
                if (teletextRoot == null)
                    continue;

                TextMesh[] teletextTextMeshes = teletextRoot.GetComponentsInChildren<TextMesh>(true);
                int fontChangedCount = 0;

                foreach (TextMesh textMesh in teletextTextMeshes)
                {
                    if (textMesh == null)
                        continue;

                    string path = GetGameObjectPath(textMesh.gameObject);
                    
                    // Apply font using the translator's font mapping logic
                    if (translator.ApplyFontOnly(textMesh, path))
                    {
                        fontChangedCount++;
                    }
                }

                if (fontChangedCount > 0)
                {
                    _logger.LogInfo($"[Teletext Fonts] Applied Korean font to {fontChangedCount} teletext elements under {rootPath}");
                }
            }
        }

        void TranslateScene()
        {
            // Find all TextMesh components in the scene
            TextMesh[] allTextMeshes = Resources.FindObjectsOfTypeAll<TextMesh>();
            int translatedCount = 0;

            foreach (TextMesh textMesh in allTextMeshes)
            {
                if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                    continue;

                // Get GameObject path
                string path = GetGameObjectPath(textMesh.gameObject);

                // Translate and apply font
                if (translator.TranslateAndApplyFont(textMesh, path, null))
                {
                    translatedCount++;
                    
                    // Cache this TextMesh
                    textMeshCache[path] = textMesh;
                    
                    // Register with unified monitor
                    textMeshMonitor.Register(textMesh, path);
                }
            }

            _logger.LogInfo($"Scene translation complete: {translatedCount} strings translated");
            _logger.LogInfo(textMeshMonitor.GetDiagnostics());
        }

        string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return "";

            // Check cache first
            if (pathCache.TryGetValue(obj, out string cachedPath))
                return cachedPath;

            // Build path using List + Reverse (faster than StringBuilder.Insert(0))
            List<string> pathParts = new List<string>();
            Transform current = obj.transform;

            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            // Reverse and join
            pathParts.Reverse();
            string path = string.Join("/", pathParts.ToArray());

            // Cache the path (limit cache size to prevent memory bloat)
            if (pathCache.Count < 1000)
            {
                pathCache[obj] = path;
            }

            return path;
        }

        AssetBundle LoadBundle(string assetBundlePath)
        {
            // Match MSCLoader exactly - keep it simple
            if (!File.Exists(assetBundlePath))
            {
                throw new System.Exception($"<b>LoadBundle() Error:</b> File not found: <b>{assetBundlePath}</b>");
            }

            _logger.LogInfo($"Loading Asset: {Path.GetFileName(assetBundlePath)}...");
            AssetBundle ab = AssetBundle.CreateFromMemoryImmediate(File.ReadAllBytes(assetBundlePath));

            if (ab == null)
            {
                throw new System.Exception("<b>LoadBundle() Error:</b> CreateFromMemoryImmediate returned null");
            }

            // Log asset names like MSCLoader does
            string[] assetNames = ab.GetAllAssetNames();
            _logger.LogInfo($"Bundle contains {assetNames.Length} assets");

            return ab;
        }
    }
}

