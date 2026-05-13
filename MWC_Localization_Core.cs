using MSCLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace MWC_Localization_Core
{
    public class MWC_Localization_Core : Mod
    {
        // Mod metadata
        public override string ID => "MWC_Localization_Core";
        public override string Name => "MWC_Localization_Core";
        public override string Author => "potatosalad775";
        public override string Version => "1.0.9";
        public override string Description => "Multi-language core localization framework for My Winter Car";
        public override Game SupportedGames => Game.MyWinterCar;

        private static readonly string[] MainTranslationFiles = new string[]
        {
            "translate_msc.txt",
            "translate.txt",
            "translate_mod.txt"
        };

        private static readonly string[] ForcedFontPathPrefixes = new string[]
        {
            "Systems/TV/Teletext/VKTekstiTV/PAGES",
            "Systems/TV/Teletext/VKTekstiTV/HEADER",
            "COMPUTER/SYSTEM/POS",
            "Sheets/UnemployPaper",
            "Systems/TV/TVGraphics/CHAT",
            "Sheets/ServicePayment",
            "Sheets/RallyRegistration/Functions/Class",
            "Systems/TV/TVGraphics/GFXTanaanWeek/Text",
            "Systems/TV/TVGraphics/GFXTanaanSat1/Text",
            "Systems/TV/TVGraphics/GFXTanaanSat2/Text",
            "Systems/TV/TVGraphics/GFXTanaanSun1/Text",
            "Systems/TV/TVGraphics/GFXTanaanSun2/Text"
        };

        // Shared state
        private TranslationDictionary translations = new TranslationDictionary();
        private Dictionary<string, Font> customFonts = new Dictionary<string, Font>();
        private LocalizationConfig config;
        private TextMeshTranslator translator;
        private SceneTranslationManager sceneManager;
        private MagazineTextHandler magazineHandler;
        private TranslationContext ctx;
        private List<ITranslationSurface> surfaces;

        // LateUpdate driver
        private GameObject lateUpdateHandlerObject;
        private LateUpdateHandler lateUpdateHandler;

        // Font bundle (static so it persists across MSCLoader instance recreation)
        private static AssetBundle fontBundle;
        private bool hasLoadedTranslations = false;

        // MSCLoader settings
        private SettingsKeybind reloadKey;
        private SettingsCheckBox showDebugLogs;
        private SettingsCheckBox showWarningLogs;

        public override void ModSetup()
        {
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
            SetupFunction(Setup.PostLoad, Mod_PostLoad);
            SetupFunction(Setup.Update, Mod_Update);
        }

        private void Mod_Settings()
        {
            Keybind.AddHeader("Localization Plugin Hotkeys");
            reloadKey = Keybind.Add("reloadKey", "Reload Translations", KeyCode.F8);

            Settings.AddHeader("Miscellaneous Options");
            showDebugLogs = Settings.AddCheckBox("showDebugLogs", "Show debug messages in console", false);
            showWarningLogs = Settings.AddCheckBox("showWarningLogs", "Show warning / error messages in console", false);
        }

        private void Mod_OnMenuLoad()
        {
            ModConsole.Print($"[{Name}] Main Menu loaded - initializing localization core...");

            translations = new TranslationDictionary();
            customFonts = new Dictionary<string, Font>();

            config = new LocalizationConfig();
            config.LoadConfig(Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt"));

            CoreConsole.Initialize(showDebugLogs, showWarningLogs);

            magazineHandler = new MagazineTextHandler();
            sceneManager = new SceneTranslationManager();

            LoadCustomFonts();

            translator = new TextMeshTranslator(translations, customFonts, magazineHandler, config);

            LoadAllMainTranslationFiles();

            ctx = new TranslationContext(
                translations,
                customFonts,
                config,
                translator,
                magazineHandler,
                ModLoader.GetModAssetsFolder(this));

            // MagazineTextHandler is both a service (used by other surfaces via ctx.Magazine)
            // and itself a surface, so the same instance appears in both places.
            surfaces = new List<ITranslationSurface>
            {
                new GuiTextMonitor(),
                magazineHandler,
                new TeletextHandler(),
                new ArrayListProxyHandler(),
                new HashTableProxyHandler(),
                new FsmTextHook(),
            };

            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].Initialize(ctx);

            CoreConsole.Print($"[{Name}] Translating Main Menu...");
            TranslateScene();
            sceneManager.MarkSceneTranslated("MainMenu");
            RunSurfaceInitialPasses(null);
        }

        private void Mod_PostLoad()
        {
            ModConsole.Print($"[{Name}] Game fully loaded - translating...");
            TranslateScene();
            sceneManager.MarkSceneTranslated("GAME");
            RunSurfaceInitialPasses(null);

            // ALL continuous monitoring runs in LateUpdate to get correct timing relative to game updates.
            lateUpdateHandlerObject = new GameObject("MWC_LateUpdateHandler");
            lateUpdateHandler = lateUpdateHandlerObject.AddComponent<LateUpdateHandler>();
            lateUpdateHandler.Initialize(surfaces, config, sceneManager);
        }

        private void Mod_Update()
        {
            if (!hasLoadedTranslations)
                return;

            if (reloadKey != null && reloadKey.GetKeybindDown())
            {
                ReloadTranslations();
                return;
            }

            string currentScene = Application.loadedLevelName;
            bool sceneChanged = sceneManager.UpdateScene(currentScene);

            if (sceneChanged)
            {
                LocalizationUtils.PruneCaches();

                if (lateUpdateHandler != null)
                    lateUpdateHandler.ClearCache();
                if (translator != null)
                    translator.ClearRuntimeCaches();

                if (surfaces != null)
                {
                    for (int i = 0; i < surfaces.Count; i++)
                        surfaces[i].Reset();
                }

                if (lateUpdateHandlerObject != null)
                {
                    Object.Destroy(lateUpdateHandlerObject);
                    lateUpdateHandlerObject = null;
                    lateUpdateHandler = null;
                }

                CoreConsole.Print($"[{Name}] Scene changed to '{currentScene}' - cleared caches");
            }

            // Initial translation pass for the current scene (covers hot reloads where
            // OnMenuLoad / PostLoad already fired but the scene was reset).
            if (currentScene == "MainMenu" && sceneManager.ShouldTranslateScene("MainMenu"))
            {
                CoreConsole.Print($"[{Name}] Translating Main Menu...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("MainMenu");
                RunSurfaceInitialPasses(null);
            }
            else if (currentScene == "GAME" && sceneManager.ShouldTranslateScene("GAME"))
            {
                CoreConsole.Print($"[{Name}] Translating Game scene...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("GAME");
                RunSurfaceInitialPasses("Initial ");
            }
        }

        private void RunSurfaceInitialPasses(string logPrefix)
        {
            if (surfaces == null) return;
            for (int i = 0; i < surfaces.Count; i++)
            {
                int count = surfaces[i].InitialPass();
                if (count > 0)
                    CoreConsole.Print($"[{Name}] {logPrefix ?? string.Empty}{surfaces[i].Name}: translated {count}");
            }
        }

        private void LoadAllMainTranslationFiles()
        {
            string assets = ModLoader.GetModAssetsFolder(this);
            foreach (string fileName in MainTranslationFiles)
            {
                string path = Path.Combine(assets, fileName);
                if (!File.Exists(path))
                {
                    CoreConsole.Warning($"[{Name}] Translation file not found: {path}");
                    continue;
                }

                Dictionary<string, string> loaded = TranslationFileParser.ParseKeyValueFile(
                    path,
                    normalizeKeys: true,
                    overwriteExisting: true);
                translations.AddAll(loaded);
                translations.LoadPatternsFromFile(path);

                hasLoadedTranslations = true;
                CoreConsole.Print($"[{Name}] Loaded {loaded.Count} translations from {fileName} ({translations.Count} total)");
            }
        }

        bool LoadCustomFonts()
        {
            CoreConsole.Print($"[{Name}] Loading fonts...");

            if (config.FontMappings.Count == 0)
            {
                CoreConsole.Print($"[{Name}] No font mappings configured - using default fonts");
                return false;
            }

            try
            {
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
                    if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                        continue;

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

                CoreConsole.Warning($"[{Name}] No fonts loaded from bundle");
                return false;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[{Name}] Font loading failed: {ex.Message}");
                return false;
            }
        }

        void ReloadTranslations()
        {
            CoreConsole.Print($"[{Name}] [F8] Reloading translations...");

            // Drop translation data
            translations.Clear();
            translations.ResetPatterns();
            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].ClearTranslations();

            // Clear global + service caches; let surfaces reset their own runtime state.
            LocalizationUtils.ClearCaches();
            translator.ClearRuntimeCaches();
            config.ClearTextAdjustmentCaches();
            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].Reset();

            // Reload config + fonts + main translation files
            config.LoadConfig(Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt"));
            customFonts.Clear();
            LoadCustomFonts();
            LoadAllMainTranslationFiles();

            // Re-init surfaces (each loads its own translation files in Initialize)
            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].Initialize(ctx);

            sceneManager.ResetAll();

            // Reapply fonts to existing TextMeshes (re-translate happens via the per-scene initial pass)
            TextMesh[] allTextMeshes = LocalizationUtils.GetAllTextMeshesIncludingInactive();
            int reappliedCount = 0;
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null || string.IsNullOrEmpty(tm.text))
                    continue;
                string path = LocalizationUtils.GetGameObjectPath(tm.gameObject);
                translator.ApplyCustomFont(tm, path);
                reappliedCount++;
            }

            // Force initial passes for the current scene if applicable
            string currentScene = Application.loadedLevelName;
            if (currentScene == "MainMenu" || currentScene == "GAME")
            {
                sceneManager.MarkSceneTranslated(currentScene);
                RunSurfaceInitialPasses("Reload ");
            }

            // Restart LateUpdate driver with the new surface list (instance hasn't changed but its tick state has)
            if (lateUpdateHandler != null)
            {
                lateUpdateHandler.ClearCache();
                lateUpdateHandler.Initialize(surfaces, config, sceneManager);
            }

            CoreConsole.Print($"[{Name}] [F8] Reloaded {translations.Count} translations. Reapplied fonts/adjustments to {reappliedCount} TextMeshes.");
        }

        void TranslateScene()
        {
            // Find all TextMesh components in the scene, including inactive objects.
            TextMesh[] allTextMeshes = LocalizationUtils.GetAllTextMeshesIncludingInactive();
            int translatedCount = 0;
            int forcedFontAppliedCount = 0;

            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null)
                    continue;

                string path = LocalizationUtils.GetGameObjectPath(tm.gameObject);

                if (!string.IsNullOrEmpty(tm.text))
                {
                    if (translator.TranslateAndApplyFont(tm, path))
                        translatedCount++;
                }

                if (PathStartsWithAny(path, ForcedFontPathPrefixes) && translator.ApplyFontOnly(tm, path))
                    forcedFontAppliedCount++;
            }

            CoreConsole.Print($"[{Name}] Scene translation complete: {translatedCount}/{allTextMeshes.Length} TextMesh objects translated, forced font pass: {forcedFontAppliedCount}");
        }

        private bool PathStartsWithAny(string path, string[] prefixes)
        {
            if (string.IsNullOrEmpty(path) || prefixes == null || prefixes.Length == 0)
                return false;

            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!string.IsNullOrEmpty(prefix) && path.StartsWith(prefix))
                    return true;
            }

            return false;
        }
    }
}
