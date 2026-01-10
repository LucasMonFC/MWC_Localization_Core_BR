using MSCLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MWC_Localization_Core
{
    public class MWC_Localization_Core : Mod
    {
        // Mod metadata
        public override string ID => "MWC_Localization_Core";
        public override string Name => "MWC_Localization_Core";
        public override string Author => "potatosalad775";
        public override string Version => "1.0.0";
        public override string Description => "Multi-language core localization framework for My Winter Car";
        public override Game SupportedGames => Game.MyWinterCar;

        // Translation data
        private Dictionary<string, string> translations = new Dictionary<string, string>();
        private bool hasLoadedTranslations = false;

        // Core handlers
        private MagazineTextHandler magazineHandler;
        private TeletextHandler teletextHandler;
        private ArrayListProxyHandler arrayListHandler;
        private TextMeshTranslator translator;

        // Unified managers
        private SceneTranslationManager sceneManager;
        private UnifiedTextMeshMonitor textMeshMonitor;
        
        // Critical UI monitor (MonoBehaviour for LateUpdate access)
        private GameObject lateUpdateHandlerObject;
        private LateUpdateHandler lateUpdateHandler;
        
        // Teletext translation tracking
        private float teletextTranslationTime = 0f;
        private int teletextRetryCount = 0;

        // Font management
        private static AssetBundle fontBundle;  // Static to persist across MSCLoader instance recreation
        private Dictionary<string, Font> customFonts = new Dictionary<string, Font>();

        // Localization configuration
        private LocalizationConfig config;

        // GameObject path cache for performance
        private Dictionary<GameObject, string> pathCache = new Dictionary<GameObject, string>();
        
        // GameObject reference cache (avoid repeated GameObject.Find calls)
        private Dictionary<string, TextMesh> textMeshCache = new Dictionary<string, TextMesh>();

        // MSCLoader settings
        private SettingsKeybind reloadKey;
        private SettingsCheckBox showDebugLogs;
        private SettingsCheckBox showWarningLogs;

        // Registration phase - NO logic here, only SetupFunction calls!
        public override void ModSetup()
        {
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
            SetupFunction(Setup.PostLoad, Mod_PostLoad);
            SetupFunction(Setup.Update, Mod_Update);
        }

        // Define settings UI
        private void Mod_Settings()
        {
            // Keybind for reloading translations
            Keybind.AddHeader("Localization Plugin Hotkeys");
            reloadKey = Keybind.Add("reloadKey", "Reload Translations", KeyCode.F8);

            // Show debug console messages
            Settings.AddHeader("Miscellaneous Options");
            showDebugLogs = Settings.AddCheckBox("showDebugLogs", "Show debug messages in console", false);
            showWarningLogs = Settings.AddCheckBox("showWarningLogs", "Show warning / error messages in console", false);
        }

        // Main menu loaded - initialize everything
        private void Mod_OnMenuLoad()
        {
            ModConsole.Print($"[{Name}] Main Menu loaded - initializing localization core...");
            // Initialize collections
            translations = new Dictionary<string, string>();
            customFonts = new Dictionary<string, Font>();

            // Initialize configuration
            config = new LocalizationConfig();
            string configPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt");
            config.LoadConfig(configPath);

            // Initialize core console
            CoreConsole.Initialize(showDebugLogs, showWarningLogs);

            // Initialize handlers
            magazineHandler = new MagazineTextHandler();
            teletextHandler = new TeletextHandler();

            // Initialize scene manager
            sceneManager = new SceneTranslationManager();

            // Load translations immediately
            LoadTranslations();

            // Load magazine translations from separate file
            string magazinePath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);
            
            // Load teletext translations from separate file
            string teletextPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_teletext.txt");
            teletextHandler.LoadTeletextTranslations(teletextPath);

            // Load fonts
            LoadCustomFonts();

            // Initialize translator after fonts are loaded
            translator = new TextMeshTranslator(translations, customFonts, magazineHandler, config);
            
            // Initialize array handler with translation dictionaries and translator
            arrayListHandler = new ArrayListProxyHandler(translations, magazineHandler, translator);
            arrayListHandler.InitializeArrayPaths();
            
            // Load FSM patterns into pattern matcher from main translation file
            string mainTranslatePath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate.txt");
            translator.LoadFsmPatterns(mainTranslatePath);
            
            // Load additional FSM patterns from teletext file
            translator.LoadFsmPatterns(teletextPath);
            
            // Initialize unified text mesh monitor
            textMeshMonitor = new UnifiedTextMeshMonitor(translator);

            // Translate main menu
            CoreConsole.Print($"[{Name}] Translating Main Menu...");
            TranslateScene();
            sceneManager.MarkSceneTranslated("MainMenu");
        }

        // Game fully loaded - translate everything
        private void Mod_PostLoad()
        {
            ModConsole.Print($"[{Name}] Game fully loaded - translating...");
            
            // Translate game scene
            TranslateScene();
            sceneManager.MarkSceneTranslated("GAME");
            
            // Reset handlers
            teletextHandler.Reset();
            arrayListHandler.Reset();

            // Translate static arrays immediately
            int arrayTranslated = arrayListHandler.TranslateAllArrays();
            if (arrayTranslated > 0)
            {
                CoreConsole.Print($"[{Name}] Translated {arrayTranslated} array items");
                arrayListHandler.ApplyFontsToArrayElements();
            }
            
            // Schedule teletext translation check
            teletextTranslationTime = Time.time + LocalizationConstants.TELETEXT_TRANSLATION_DELAY;
            teletextRetryCount = 0;
            
            // Create MonoBehaviour for LateUpdate monitoring
            // ALL continuous monitoring logic runs in LateUpdate to ensure correct timing
            lateUpdateHandlerObject = new GameObject("MWC_LateUpdateHandler");
            lateUpdateHandler = lateUpdateHandlerObject.AddComponent<LateUpdateHandler>();
            lateUpdateHandler.Initialize(
                this, 
                translator, 
                textMeshMonitor, 
                teletextHandler, 
                arrayListHandler, 
                sceneManager,
                textMeshCache
            );
        }

        // Every frame - scheduling and scene management ONLY
        // All monitoring logic moved to LateUpdateHandler.LateUpdate()
        private void Mod_Update()
        {
            if (!hasLoadedTranslations)
                return;

            // Hotkey check - F8 to reload translations
            if (reloadKey != null && reloadKey.GetKeybindDown())
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
                // Clear MonoBehaviour cache and destroy old monitor
                if (lateUpdateHandler != null)
                {
                    lateUpdateHandler.ClearCache();
                }
                if (lateUpdateHandlerObject != null)
                {
                    Object.Destroy(lateUpdateHandlerObject);
                    lateUpdateHandlerObject = null;
                    lateUpdateHandler = null;
                }

                // Clear path and TextMesh caches
                pathCache.Clear();
                textMeshCache.Clear();
                
                CoreConsole.Print($"[{Name}] Scene changed to '{currentScene}' - cleared caches");
            }

            // Initial translation pass for Splash Screen (Required for hot reloads)
            if (currentScene == "SplashScreen" && sceneManager.ShouldTranslateScene("SplashScreen"))
            {
                CoreConsole.Print($"[{Name}] Translating Splash Screen...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("SplashScreen");
            }

            // Initial translation pass for Main Menu (Required for hot reloads)
            if (currentScene == "MainMenu" && sceneManager.ShouldTranslateScene("MainMenu"))
            {
                CoreConsole.Print($"[{Name}] Translating Main Menu...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("MainMenu");
            }

            // Initial translation pass for Game scene (Required for hot reloads)
            if (currentScene == "GAME" && sceneManager.ShouldTranslateScene("GAME"))
            {
                CoreConsole.Print($"[{Name}] Translating Game scene...");
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
                    CoreConsole.Print($"[{Name}] [Arrays] Initial translation: {arrayTranslated} items");
                    // Apply Korean fonts to TextMesh components using array data
                    arrayListHandler.ApplyFontsToArrayElements();
                }
                
                // Schedule teletext translation after delay
                teletextTranslationTime = Time.time + LocalizationConstants.TELETEXT_TRANSLATION_DELAY;
            }

            // Teletext translation monitoring (Game scene only)
            // Scheduling logic - actual monitoring moved to LateUpdate
            if (currentScene == "GAME" && sceneManager.HasSceneBeenTranslated("GAME") && 
                teletextTranslationTime > 0 && Time.time >= teletextTranslationTime)
            {
                CoreConsole.Print($"[{Name}] Attempting teletext translation (retry {teletextRetryCount + 1}/{LocalizationConstants.TELETEXT_MAX_RETRIES})...");
                
                if (teletextHandler.IsTeletextAvailable())
                {
                    // Check if arrays are populated before translating
                    bool arraysPopulated = teletextHandler.AreTeletextArraysPopulated();
                    
                    if (arraysPopulated)
                    {
                        // Arrays have data, do the translation
                        int translatedCount = teletextHandler.TranslateTeletextData();
                        CoreConsole.Print($"[{Name}] Arrays populated! Translated {translatedCount} items.");
                        
                        teletextTranslationTime = 0f;  // Done
                        teletextRetryCount = 0;
                    }
                    else if (teletextRetryCount >= LocalizationConstants.TELETEXT_MAX_RETRIES - 1)
                    {
                        // Gave up waiting
                        CoreConsole.Print($"[{Name}] Arrays still empty after {LocalizationConstants.TELETEXT_MAX_RETRIES} retries. They may populate later.");
                        // Try anyway in case some arrays have data
                        int translatedCount = teletextHandler.TranslateTeletextData();
                        if (translatedCount > 0)
                        {
                            CoreConsole.Print($"[{Name}] Translated {translatedCount} items from partially populated arrays.");
                        }
                        teletextTranslationTime = 0f;
                        teletextRetryCount = 0;
                    }
                    else
                    {
                        // Retry - arrays still empty
                        teletextRetryCount++;
                        teletextTranslationTime = Time.time + LocalizationConstants.TELETEXT_RETRY_INTERVAL;
                        CoreConsole.Print($"[{Name}] Arrays not yet populated, will retry in {LocalizationConstants.TELETEXT_RETRY_INTERVAL}s...");
                    }
                }
                else
                {
                    CoreConsole.Print($"[{Name}] Teletext not available in this scene.");
                    teletextTranslationTime = 0f;
                    teletextRetryCount = 0;
                }
            }
        }

        bool LoadCustomFonts()
        {
            CoreConsole.Print($"[{Name}] Loading fonts...");

            // Skip font loading if no font mappings configured
            if (config.FontMappings.Count == 0)
            {
                CoreConsole.Print($"[{Name}] No font mappings configured - using default fonts");
                return false;
            }

            try
            {
                // Load bundle only if not already loaded
                if (fontBundle == null)
                {
                    fontBundle = LoadAssets.LoadBundle(this, "fonts.unity3d");
                    CoreConsole.Print($"[{Name}] Bundle loaded, result: {(fontBundle == null ? "NULL" : "NOT NULL")}");
                }
                
                if (fontBundle == null)
                {
                    CoreConsole.Warning($"[{Name}] Failed to load font bundle");
                    return false;
                }

                foreach (var pair in config.FontMappings)
                {
                    Font font = fontBundle.LoadAsset(pair.Value, typeof(Font)) as Font;
                    if (font != null)
                    {
                        customFonts[pair.Key] = font;
                        CoreConsole.Print($"[{Name}] Loaded font: {pair.Value} for {pair.Key}");
                    }
                    else
                    {
                        CoreConsole.Warning($"[{Name}] Failed to load font asset: {pair.Value}");
                    }
                }

                if (customFonts.Count > 0)
                {
                    CoreConsole.Print($"[{Name}] Loaded {customFonts.Count} custom fonts");
                    return true;
                }
                else
                {
                    CoreConsole.Warning($"[{Name}] No fonts loaded from bundle");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[{Name}] Font loading failed: {ex.Message}");
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
                    if (string.IsNullOrEmpty(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex > 0)
                    {
                        string key = line.Substring(0, separatorIndex).Trim();
                        string value = line.Substring(separatorIndex + 1).Trim();

                        string normalizedKey = StringHelper.FormatUpperKey(key);
                        string processedValue = value.Replace("\\n", "\n");

                        if (!string.IsNullOrEmpty(normalizedKey))
                        {
                            translations[normalizedKey] = processedValue;
                        }
                    }
                }

                hasLoadedTranslations = true;
                CoreConsole.Print($"[{Name}] Loaded {translations.Count} translations from {Path.GetFileName(translationPath)}");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[{Name}] Failed to load translations: {ex.Message}");
            }
        }

        void LoadTranslations()
        {
            // Load translation file used in My Summer Car first
            string mscTranslationPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_msc.txt");
            
            if (!File.Exists(mscTranslationPath))
            {
                CoreConsole.Warning($"[{Name}] Translation file not found: {mscTranslationPath}");
            }
            else 
            {
                InsertTranslationLines(mscTranslationPath);
            }

            // Load main translation file for My Winter Car
            string translationPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate.txt");

            if (!File.Exists(translationPath))
            {
                CoreConsole.Warning($"[{Name}] Translation file not found: {translationPath}");
            }
            else 
            {
                InsertTranslationLines(translationPath);
            }

            // Load mod translation file for My Winter Car
            string modTranslationPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_mod.txt");

            if (!File.Exists(modTranslationPath))
            {
                CoreConsole.Warning($"[{Name}] Translation file not found: {modTranslationPath}");
            }
            else 
            {
                InsertTranslationLines(modTranslationPath);
            }
        }

        void ReloadTranslations()
        {
            CoreConsole.Print($"[{Name}] [F8] Reloading translations...");

            // Clear existing translations
            translations.Clear();
            magazineHandler.ClearTranslations();
            arrayListHandler.ClearTranslations();

            // Reset text adjustment caches and reload config
            config.ClearTextAdjustmentCaches();
            string configPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt");
            config.LoadConfig(configPath);

            // Reload from file
            LoadTranslations();

            // Reload magazine translations
            string magazinePath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);
            
            // Reload teletext translations
            string teletextPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_teletext.txt");
            teletextHandler.LoadTeletextTranslations(teletextPath);
            
            // Reload FSM patterns from main file first
            string mainTranslatePath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate.txt");
            translator.LoadFsmPatterns(mainTranslatePath);
            
            // Reload additional FSM patterns from teletext file
            translator.LoadFsmPatterns(teletextPath);

            // Clear all caches
            pathCache.Clear();
            textMeshCache.Clear();
            
            // Reset and re-initialize LateUpdateHandler to find critical UI again
            if (lateUpdateHandler != null)
            {
                lateUpdateHandler.ClearCache();
                // Re-initialize to find critical UI components again
                lateUpdateHandler.Initialize(
                    this, 
                    translator, 
                    textMeshMonitor, 
                    teletextHandler, 
                    arrayListHandler, 
                    sceneManager,
                    textMeshCache
                );
            }

            // Reset managers
            sceneManager.ResetAll();
            textMeshMonitor.Clear();
            
            // Reset teletext handler
            teletextHandler.Reset();
            arrayListHandler.Reset();
            teletextTranslationTime = 0f;
            teletextRetryCount = 0;

            // Reapply fonts and adjustments to all TextMeshes (after restore)
            TextMesh[] allTextMeshes = Resources.FindObjectsOfTypeAll<TextMesh>();
            int reappliedCount = 0;
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm != null && !string.IsNullOrEmpty(tm.text))
                {
                    string path = GetGameObjectPath(tm.gameObject);
                    translator.ApplyCustomFont(tm, path);
                    reappliedCount++;
                }
            }

            CoreConsole.Print($"[{Name}] [F8] Reloaded {translations.Count} translations. Reapplied fonts/adjustments to {reappliedCount} TextMeshes.");
        }

        void TranslateScene()
        {
            // Find all TextMesh components in the scene
            TextMesh[] allTextMeshes = Resources.FindObjectsOfTypeAll<TextMesh>();
            int translatedCount = 0;

            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null || string.IsNullOrEmpty(tm.text))
                    continue;

                // Get GameObject path
                string path = GetGameObjectPath(tm.gameObject);

                // Translate and apply font
                bool translated = translator.TranslateAndApplyFont(tm, path, null);
                
                // Register with unified monitor
                // CRITICAL: Register magazine texts even if not translated yet (placeholder handling)
                if (translated || path.Contains("YellowPagesMagazine") || path.Contains("Systems/TV"))
                {
                    if (translated)
                        translatedCount++;

                    // Cache this TextMesh
                    textMeshCache[path] = tm;

                    // Register for monitoring
                    textMeshMonitor.Register(tm, path);
                }
            }

            CoreConsole.Print($"[{Name}] Scene translation complete: {translatedCount}/{allTextMeshes.Length} TextMesh objects translated");
        }

        string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return string.Empty;

            // Check cache first
            if (pathCache.TryGetValue(obj, out string cachedPath))
                return cachedPath;

            // Build path using List + Reverse
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
    }
}
