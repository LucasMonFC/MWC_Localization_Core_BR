using MSCLoader;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MWC_Localization_Core
{
    public class MWC_Localization_Core : Mod
    {
        private enum ProgressBarMode
        {
            None,
            Existing,
            Created,
        }

        // Mod metadata
        public override string ID => "MWC_Localization_Core_BR";
        public override string Name => "MWC_Localization_Core";
        public override string Author => "potatosalad775&LucasMonOficial";
        public override string Version => "1.5.0";
        public override string Description => "Núcleo de localização multilíngue para My Winter Car";
        public override Game SupportedGames => Game.MyWinterCar;

        private static readonly string[] MainTranslationFiles = new string[]
        {
            "translate_msc.txt",
            "translate.txt",
            "translate_mod.txt"
        };

        // translate_magazine.txt entries are restricted to these object-path prefixes
        // so that short magazine-only tokens (e.g. "h.") cannot leak into unrelated
        // game text. See TranslationDictionary.AddScoped.
        private const string MagazineScopeId = "magazine";
        private static readonly string[] MagazineScopePathPrefixes = new string[]
        {
            "CARPARTS/PARTSYSTEM/PostSystem/",
            "Sheets/YellowPagesMagazine/",
        };

        // Shared state
        private TranslationDictionary translations = new TranslationDictionary();
        private Dictionary<string, Font> customFonts = new Dictionary<string, Font>();
        private LocalizationConfig config;
        private TextMeshTranslator translator;
        private TranslationContext ctx;
        private List<ITranslationSurface> surfaces;

        // Scene state (formerly SceneTranslationManager).
        // Leaving a scene clears its translated flag so we re-translate on return.
        private string currentScene = string.Empty;
        private readonly HashSet<string> translatedScenes = new HashSet<string>();

        // LateUpdate driver
        private GameObject lateUpdateHandlerObject;
        private LateUpdateHandler lateUpdateHandler;
        private GameObject reloadHandlerObject;
        private bool isReloading;

        // Font bundle (static so it persists across MSCLoader instance recreation)
        private static AssetBundle fontBundle;
        private bool hasLoadedTranslations = false;

        // MSCLoader settings
        private const string ReloadKeyDisplayPrefix = "Atalho atual de recarga: ";
        private SettingsKeybind reloadKey;
        private SettingsText reloadKeyDisplay;
        private string lastReloadKeyLabel = string.Empty;
        private SettingsCheckBox showDebugLogs;
        private SettingsCheckBox showWarningLogs;
        private SettingsCheckBox loadTextureMods;
        private TextureReplacementSurface textureReplacementSurface;

        public override void ModSetup()
        {
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.ModSettingsLoaded, Mod_SettingsLoaded);
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
            SetupFunction(Setup.PreLoad, Mod_PreLoad, "Capturando texturas originais...");
            SetupFunction(Setup.PostLoad, Mod_PostLoad, "Aplicando localização...");
            SetupFunction(Setup.Update, Mod_Update);
        }

        private void Mod_Settings()
        {
            Keybind.AddHeader("Atalhos do plugin de localização");
            reloadKey = Keybind.Add("reloadKey", "Recarregar traduções", KeyCode.F8);

            Settings.AddHeader("Configurações");
            reloadKeyDisplay = Settings.AddText(ReloadKeyDisplayPrefix + GetReloadKeybindLabel());
            UpdateReloadKeyDisplay();

            Settings.AddHeader("Opções diversas");
            showDebugLogs = Settings.AddCheckBox("showDebugLogs", "Mostrar mensagens de depuração no console", false);
            showWarningLogs = Settings.AddCheckBox("showWarningLogs", "Mostrar avisos / erros no console", false);
            loadTextureMods = Settings.AddCheckBox("loadTextureMods", "Carregar texturas de mods", false);
        }

        private void Mod_SettingsLoaded()
        {
            UpdateReloadKeyDisplay();
        }

        private void Mod_OnMenuLoad()
        {
            ModConsole.Print($"[{Name}] Menu principal carregado - inicializando núcleo de localização...");

            translations = new TranslationDictionary();
            customFonts = new Dictionary<string, Font>();

            config = new LocalizationConfig();
            config.LoadConfig(Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt"));

            CoreConsole.Initialize(showDebugLogs, showWarningLogs);

            translatedScenes.Clear();
            currentScene = string.Empty;

            LoadCustomFonts();

            translator = new TextMeshTranslator(translations, customFonts, config);

            LoadAllMainTranslationFiles();
            LoadMagazineFormatTranslations();

            ctx = new TranslationContext(
                translations,
                customFonts,
                config,
                translator,
                ModLoader.GetModAssetsFolder(this));

            textureReplacementSurface = new TextureReplacementSurface(() => loadTextureMods != null && loadTextureMods.GetValue());

            surfaces = new List<ITranslationSurface>
            {
                new FsmGuiTranslator(),
                new FsmArrayTranslator(),
                new ArrayListProxyHandler(),
                new HashTableProxyHandler(),
                new FsmTextHook(),
                textureReplacementSurface,
            };

            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].Initialize(ctx);

            CoreConsole.Print($"[{Name}] Atalho de recarga atual: {GetReloadKeybindLabel()}");
            CoreConsole.Print($"[{Name}] Traduzindo Menu Principal...");
            TranslateScene();
            MarkSceneTranslated("MainMenu");
            RunSurfaceInitialPasses(null);
            config.ApplyGameObjectAdjustments();
        }

        private void Mod_PreLoad()
        {
            if (textureReplacementSurface != null)
                textureReplacementSurface.CaptureTextureTargetsBeforeMods();
        }

        private void Mod_PostLoad()
        {
            ModConsole.Print($"[{Name}] Jogo totalmente carregado - traduzindo...");
            TranslateScene();
            MarkSceneTranslated("GAME");
            RunSurfaceInitialPasses(null);
            config.ApplyGameObjectAdjustments();

            // ALL continuous monitoring runs in LateUpdate to get correct timing relative to game updates.
            lateUpdateHandlerObject = new GameObject("MWC_LateUpdateHandler");
            lateUpdateHandler = lateUpdateHandlerObject.AddComponent<LateUpdateHandler>();
            lateUpdateHandler.Initialize(surfaces, () => HasSceneBeenTranslated("GAME"));
        }

        private void Mod_Update()
        {
            UpdateReloadKeyDisplay();

            if (!hasLoadedTranslations)
                return;

            if (reloadKey != null && reloadKey.GetKeybindDown())
            {
                StartReloadTranslations();
                return;
            }

            string sceneName = Application.loadedLevelName;
            bool sceneChanged = UpdateScene(sceneName);

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

                CoreConsole.Print($"[{Name}] Cena alterada para '{sceneName}' - caches limpos");
            }

            // Initial translation pass for the current scene (covers hot reloads where
            // OnMenuLoad / PostLoad already fired but the scene was reset).
            if (sceneName == "MainMenu" && ShouldTranslateScene("MainMenu"))
            {
                CoreConsole.Print($"[{Name}] Traduzindo Menu Principal...");
                TranslateScene();
                MarkSceneTranslated("MainMenu");
                RunSurfaceInitialPasses(null);
                config.ApplyGameObjectAdjustments();
            }
            else if (sceneName == "GAME" && ShouldTranslateScene("GAME"))
            {
                CoreConsole.Print($"[{Name}] Traduzindo cena do jogo...");
                TranslateScene();
                MarkSceneTranslated("GAME");
                RunSurfaceInitialPasses("Initial ");
                config.ApplyGameObjectAdjustments();
            }
        }

        private string GetReloadKeybindLabel()
        {
            return reloadKey != null ? reloadKey.GetKeybindValue : "F8";
        }

        private void UpdateReloadKeyDisplay()
        {
            if (reloadKeyDisplay == null)
                return;

            string label = GetReloadKeybindLabel();
            if (label == lastReloadKeyLabel)
                return;

            lastReloadKeyLabel = label;
            reloadKeyDisplay.SetValue(ReloadKeyDisplayPrefix + label);
        }

        private ProgressBarMode BeginProgress(string title, int maxValue, string status)
        {
            try
            {
                if (ModUI.isProgressBarActive)
                {
                    ModUI.UpdateProgressBarSetup(title, maxValue);
                    ModUI.UpdateProgressBar(0, status);
                    return ProgressBarMode.Existing;
                }

                ModUI.CreateProgressBar(title, maxValue, status);
                return ProgressBarMode.Created;
            }
            catch
            {
                return ProgressBarMode.None;
            }
        }

        private void UpdateProgress(ProgressBarMode progressMode, int progress, string status)
        {
            if (progressMode == ProgressBarMode.None)
                return;

            try
            {
                ModUI.UpdateProgressBar(progress, status);
            }
            catch
            {
            }
        }

        private void CloseProgress(ProgressBarMode progressMode)
        {
            if (progressMode != ProgressBarMode.Created)
                return;

            try
            {
                ProgressBarCloseHandler.Schedule(1.5f);
            }
            catch
            {
            }
        }

        private void StartReloadTranslations()
        {
            if (isReloading)
                return;

            if (reloadHandlerObject != null)
                Object.Destroy(reloadHandlerObject);

            reloadHandlerObject = new GameObject("MWC_ReloadTranslationsHandler");
            ReloadTranslationsHandler handler = reloadHandlerObject.AddComponent<ReloadTranslationsHandler>();
            handler.Initialize(this);
        }

        // Scene tracking (formerly SceneTranslationManager). Leaving a known scene
        // clears its translated flag so returning to it re-runs the initial pass.
        private bool UpdateScene(string newScene)
        {
            if (string.IsNullOrEmpty(newScene) || currentScene == newScene)
                return false;

            if (!string.IsNullOrEmpty(currentScene))
                translatedScenes.Remove(currentScene);

            currentScene = newScene;
            return true;
        }

        private bool ShouldTranslateScene(string scene) { return !translatedScenes.Contains(scene); }
        private bool HasSceneBeenTranslated(string scene) { return translatedScenes.Contains(scene); }
        private void MarkSceneTranslated(string scene) { translatedScenes.Add(scene); }

        private void RunSurfaceInitialPasses(string logPrefix)
        {
            if (surfaces == null) return;
            for (int i = 0; i < surfaces.Count; i++)
            {
                int count = surfaces[i].InitialPass();
                if (count > 0)
                    CoreConsole.Print($"[{Name}] {logPrefix ?? string.Empty}{surfaces[i].Name}: traduziu {count}");
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
                    CoreConsole.Warning($"[{Name}] Arquivo de tradução não encontrado: {path}");
                    continue;
                }

                Dictionary<string, string> loaded = TranslationFileParser.ParseKeyValueFile(
                    path,
                    normalizeKeys: true,
                    overwriteExisting: true);
                translations.AddAll(loaded);
                translations.LoadPatternsFromFile(path);

                hasLoadedTranslations = true;
                CoreConsole.Print($"[{Name}] Carregou {loaded.Count} traduções de {fileName} ({translations.Count} no total)");
            }
        }

        private void LoadMagazineFormatTranslations()
        {
            string path = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_magazine.txt");
            Dictionary<string, string> loaded = TranslationFileParser.ParseKeyValueFile(
                path,
                normalizeKeys: true,
                overwriteExisting: true);

            string phoneLabel;
            if (!loaded.TryGetValue("PHONE", out phoneLabel) || string.IsNullOrEmpty(phoneLabel))
                phoneLabel = "PHONE";

            translations.AddScoped(MagazineScopeId, MagazineScopePathPrefixes, loaded);
            translations.AddScopedEntry(MagazineScopeId, "h.", string.Empty);
            translations.AddScopedEntry(MagazineScopeId, ",- puh.", " MK, " + phoneLabel + " -");
            hasLoadedTranslations = true;

            if (File.Exists(path))
                CoreConsole.Print($"[{Name}] Carregou {loaded.Count} traduções de revista de translate_magazine.txt (com escopo)");
            else
                CoreConsole.Warning($"[{Name}] Arquivo de formato de revista não encontrado: {path}; usando rótulo padrão de telefone");
        }

        bool LoadCustomFonts()
        {
            CoreConsole.Print($"[{Name}] Carregando fontes...");

            if (config.FontMappings.Count == 0)
            {
                CoreConsole.Print($"[{Name}] Nenhum mapeamento de fonte configurado - usando fontes padrão");
                return false;
            }

            try
            {
                if (fontBundle == null)
                {
                    fontBundle = LoadAssets.LoadBundle(this, "fonts.unity3d");
                    CoreConsole.Print($"[{Name}] Bundle carregado, resultado: {(fontBundle == null ? "NULL" : "NOT NULL")}");
                }

                if (fontBundle == null)
                {
                    CoreConsole.Warning($"[{Name}] Falha ao carregar bundle de fontes");
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
                        CoreConsole.Print($"[{Name}] Fonte carregada: {pair.Value} para {pair.Key}");
                    }
                    else
                    {
                        CoreConsole.Warning($"[{Name}] Falha ao carregar asset de fonte: {pair.Value}");
                    }
                }

                if (customFonts.Count > 0)
                {
                    CoreConsole.Print($"[{Name}] Carregou {customFonts.Count} fontes customizadas");
                    return true;
                }

                CoreConsole.Warning($"[{Name}] Nenhuma fonte carregada do bundle");
                return false;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[{Name}] Falha ao carregar fontes: {ex.Message}");
                return false;
            }
        }

        private sealed class ReloadTranslationsHandler : MonoBehaviour
        {
            public void Initialize(MWC_Localization_Core owner)
            {
                Object.DontDestroyOnLoad(gameObject);
                StartCoroutine(owner.ReloadTranslationsRoutine());
            }
        }

        private IEnumerator ReloadTranslationsRoutine()
        {
            isReloading = true;
            int reappliedCount = 0;
            ProgressBarMode progressMode = BeginProgress("MWC Localization Core", 7, "Recarregando traduções...");
            try
            {
                CoreConsole.Print($"[{Name}] [F8] Recarregando traduções...");
                yield return null;

                UpdateProgress(progressMode, 1, "Limpando caches...");
                yield return null;

                // Drop translation data
                translations.Clear();
                translations.ResetPatterns();
                if (surfaces != null)
                {
                    for (int i = 0; i < surfaces.Count; i++)
                        surfaces[i].ClearTranslations();
                }

                // Clear global + service caches; let surfaces reset their own runtime state.
                LocalizationUtils.ClearCaches();
                translator.ClearRuntimeCaches();
                config.ClearTextAdjustmentCaches();
                if (surfaces != null)
                {
                    for (int i = 0; i < surfaces.Count; i++)
                        surfaces[i].Reset();
                }

                UpdateProgress(progressMode, 2, "Recarregando configuração e fontes...");
                yield return null;

                // Reload config + fonts + main translation files
                config.LoadConfig(Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt"));
                customFonts.Clear();
                LoadCustomFonts();

                UpdateProgress(progressMode, 3, "Recarregando arquivos de tradução...");
                yield return null;
                LoadAllMainTranslationFiles();
                LoadMagazineFormatTranslations();

                UpdateProgress(progressMode, 4, "Reinicializando superficies...");
                yield return null;

                // Re-init surfaces (each loads its own translation files in Initialize)
                if (surfaces != null)
                {
                    for (int i = 0; i < surfaces.Count; i++)
                        surfaces[i].Initialize(ctx);
                }

                translatedScenes.Clear();

                UpdateProgress(progressMode, 5, "Reaplicando fontes...");
                yield return null;

                // Reapply fonts to existing TextMeshes (re-translate happens via the per-scene initial pass)
                TextMesh[] allTextMeshes = LocalizationUtils.GetAllTextMeshesIncludingInactive();
                const int fontReapplyBatchSize = 64;
                for (int i = 0; i < allTextMeshes.Length; i++)
                {
                    TextMesh tm = allTextMeshes[i];
                    if (tm == null || string.IsNullOrEmpty(tm.text))
                        continue;

                    string path = LocalizationUtils.GetGameObjectPath(tm.gameObject);
                    translator.ApplyCustomFont(tm, path);
                    reappliedCount++;

                    if (i > 0 && i % fontReapplyBatchSize == 0)
                    {
                        UpdateProgress(progressMode, 5, "Reaplicando fontes... " + i + "/" + allTextMeshes.Length);
                        yield return null;
                    }
                }

                UpdateProgress(progressMode, 6, "Executando passes iniciais...");
                yield return null;

                // Force initial passes for the current scene if applicable
                string sceneName = Application.loadedLevelName;
                if (sceneName == "MainMenu" || sceneName == "GAME")
                {
                    currentScene = sceneName;
                    MarkSceneTranslated(sceneName);
                    RunSurfaceInitialPasses("Recarregar ");
                    config.ApplyGameObjectAdjustments();
                }

                // Restart LateUpdate driver with the new surface list (instance hasn't changed but its tick state has)
                if (lateUpdateHandler != null)
                {
                    lateUpdateHandler.ClearCache();
                    lateUpdateHandler.Initialize(surfaces, () => HasSceneBeenTranslated("GAME"));
                }

                UpdateProgress(progressMode, 7, "Reload concluido.");
                CoreConsole.Print($"[{Name}] [F8] Recarregou {translations.Count} traduções. Reaplicou fontes/ajustes em {reappliedCount} TextMeshes.");
                yield return null;
            }
            finally
            {
                CloseProgress(progressMode);
                isReloading = false;

                if (reloadHandlerObject != null)
                {
                    Object.Destroy(reloadHandlerObject);
                    reloadHandlerObject = null;
                }
            }
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

                if (LocalizationConfig.IsForcedFontPath(path) && translator.ApplyFontOnly(tm, path))
                    forcedFontAppliedCount++;
            }

            CoreConsole.Print($"[{Name}] Tradução da cena concluída: {translatedCount}/{allTextMeshes.Length} objetos TextMesh traduzidos, passe de fonte forçada: {forcedFontAppliedCount}");
        }
    }

    internal sealed class ProgressBarCloseHandler : MonoBehaviour
    {
        private const string ObjectName = "MWC_Localization_Core_ProgressCloser";
        private float closeTime;

        public static void Schedule(float delaySeconds)
        {
            GameObject existing = GameObject.Find(ObjectName);
            if (existing != null)
                UnityEngine.Object.Destroy(existing);

            GameObject go = new GameObject(ObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);

            ProgressBarCloseHandler handler = go.AddComponent<ProgressBarCloseHandler>();
            handler.closeTime = Time.realtimeSinceStartup + delaySeconds;
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < closeTime)
                return;

            try
            {
                ModUI.CloseProgressBar();
            }
            catch
            {
            }

            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
