using System;
using System.Collections.Generic;
using System.IO;
using HutongGames.PlayMaker.Actions;
using Ionic.Zip;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.ImageEffects;

namespace MWC_Localization_Core
{
    public sealed class TextureReplacementSurface : ITranslationSurface
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string GameSceneName = "GAME";
        private const string DriversLicenceTextureName = "drivers_lincence";
        private const string RallyRegistrationObjectPath = "RallyRegistration";
        private const string RallyRegistrationFsmName = "Setup";
        private const string RallyRegistrationStateName = "Init";
        private const string RallyCoverMaterialName = "cover 1";
        private const string RallyCoverReplacementTextureName = "rally_registercard";
        private const int ModTextureDelayFrames = 1;

        private static readonly string[] TexturePropertyNames = new string[]
        {
            "_MainTex",
            "_MetallicGlossMap",
            "_BumpMap",
            "_EmissionMap",
            "_DetailMask",
            "_DetailAlbedoMap",
            "_DetailNormalMap",
            "_SpecGlossMap",
            "_Detail",
            "_DecalTex",
        };

        private static readonly string[] IgnoredShaderPrefixes = new string[]
        {
            "Hidden",
            "Particles",
        };

        private sealed class MaterialTextureBackup
        {
            public readonly string PropertyName;
            public readonly Texture OriginalTexture;

            public MaterialTextureBackup(string propertyName, Texture originalTexture)
            {
                PropertyName = propertyName;
                OriginalTexture = originalTexture;
            }
        }

        private sealed class TextureFileSource
        {
            public readonly string TextureKey;
            public readonly string DisplayName;
            public readonly byte[] Bytes;

            public TextureFileSource(string textureKey, string displayName, byte[] bytes)
            {
                TextureKey = textureKey;
                DisplayName = displayName;
                Bytes = bytes;
            }
        }

        private readonly Dictionary<string, Texture2D> replacementTextures =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<Material, List<MaterialTextureBackup>> originalMaterialTextures =
            new Dictionary<Material, List<MaterialTextureBackup>>();

        private readonly Dictionary<ScreenOverlay, Texture2D> originalOverlayTextures =
            new Dictionary<ScreenOverlay, Texture2D>();

        private readonly Dictionary<Image, Sprite> originalImageSprites =
            new Dictionary<Image, Sprite>();

        private readonly Dictionary<Sprite, Sprite> replacementSprites =
            new Dictionary<Sprite, Sprite>();

        private readonly HashSet<Sprite> replacementSpriteSet =
            new HashSet<Sprite>();

        private readonly HashSet<string> sceneTextureKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> matchedTextureNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Func<bool> getLoadTextureMods;
        private Material[] capturedMaterials;
        private ScreenOverlay[] capturedOverlays;
        private Image[] capturedImages;
        private string assetsFolder;
        private string loadedSceneName;
        private bool hasApplied;
        private bool hasLoadedReplacementTextures;
        private bool hasInstalledRallyRefreshHook;
        private bool pendingModTextureApply;
        private int modTextureDelayFramesRemaining;

        public TextureReplacementSurface()
            : this(null)
        {
        }

        public TextureReplacementSurface(Func<bool> getLoadTextureMods)
        {
            this.getLoadTextureMods = getLoadTextureMods;
        }

        public string Name { get { return "TextureReplacementSurface"; } }
        public SurfaceCadence Cadence { get { return ShouldLoadTextureMods() ? SurfaceCadence.PerFrame : SurfaceCadence.OncePerScene; } }
        public bool IsComplete { get { return hasApplied; } }

        public void Initialize(TranslationContext ctx)
        {
            assetsFolder = ctx != null ? ctx.AssetsFolder : null;
            ResetRuntimeState();
        }

        public int InitialPass()
        {
            string sceneName = Application.loadedLevelName;
            if (!ShouldApplyInScene(sceneName) || hasApplied)
                return 0;

            EnsureReplacementTexturesLoaded(sceneName);
            if (sceneName == GameSceneName && ShouldLoadTextureMods())
            {
                if (replacementTextures.Count == 0 || sceneTextureKeys.Count == 0)
                {
                    hasApplied = true;
                    return 0;
                }

                pendingModTextureApply = true;
                modTextureDelayFramesRemaining = ModTextureDelayFrames;
                CoreConsole.Print($"[{Name}] Aguardando um frame para incluir alvos de textura de mods");
                return 0;
            }

            return ApplyLoadedTextures();
        }

        private int ApplyLoadedTextures()
        {
            if (hasApplied)
                return 0;

            int applied = 0;
            applied += ApplyMaterialTextures();
            applied += ApplyCameraOverlays();
            applied += ApplyImageSprites();
            applied += InstallRallyRefreshHook(loadedSceneName);

            pendingModTextureApply = false;
            hasApplied = true;
            LogUnmatchedTextures();
            return applied;
        }

        public int MonitorTick(float deltaTime)
        {
            if (!pendingModTextureApply)
                return 0;

            if (Application.loadedLevelName != GameSceneName)
            {
                pendingModTextureApply = false;
                hasApplied = true;
                return 0;
            }

            if (modTextureDelayFramesRemaining > 0)
            {
                modTextureDelayFramesRemaining--;
                return 0;
            }

            return ApplyLoadedTextures();
        }

        public void Reset()
        {
            ResetRuntimeState();
        }

        public void ClearTranslations()
        {
            RestoreOriginalImageSprites();
            RestoreOriginalTextures();
            DestroyReplacementSprites();
            DestroyReplacementTextures();
            replacementTextures.Clear();
            ResetRuntimeState();
        }

        internal void CaptureTextureTargetsBeforeMods()
        {
            if (ShouldLoadTextureMods())
            {
                ClearCapturedTextureTargets();
                return;
            }

            CaptureTextureTargets();
        }

        private void ResetRuntimeState()
        {
            loadedSceneName = null;
            hasApplied = false;
            hasLoadedReplacementTextures = false;
            hasInstalledRallyRefreshHook = false;
            pendingModTextureApply = false;
            modTextureDelayFramesRemaining = 0;
            sceneTextureKeys.Clear();
            matchedTextureNames.Clear();
        }

        private void CaptureTextureTargets()
        {
            capturedMaterials = Resources.FindObjectsOfTypeAll<Material>();
            capturedOverlays = Resources.FindObjectsOfTypeAll<ScreenOverlay>();
            capturedImages = Resources.FindObjectsOfTypeAll<Image>();
            CoreConsole.Print($"[{Name}] Capturou alvos iniciais de textura: {Count(capturedMaterials)} materiais, {Count(capturedOverlays)} overlays, {Count(capturedImages)} imagens");
        }

        private void ClearCapturedTextureTargets()
        {
            capturedMaterials = null;
            capturedOverlays = null;
            capturedImages = null;
        }

        private Material[] GetMaterialTargets()
        {
            if (loadedSceneName == GameSceneName && !ShouldLoadTextureMods() && capturedMaterials != null)
                return capturedMaterials;

            return Resources.FindObjectsOfTypeAll<Material>();
        }

        private ScreenOverlay[] GetOverlayTargets()
        {
            if (loadedSceneName == GameSceneName && !ShouldLoadTextureMods() && capturedOverlays != null)
                return capturedOverlays;

            return Resources.FindObjectsOfTypeAll<ScreenOverlay>();
        }

        private Image[] GetImageTargets()
        {
            if (loadedSceneName == GameSceneName && !ShouldLoadTextureMods() && capturedImages != null)
                return capturedImages;

            return Resources.FindObjectsOfTypeAll<Image>();
        }

        private bool ShouldLoadTextureMods()
        {
            return getLoadTextureMods != null && getLoadTextureMods();
        }

        private void EnsureReplacementTexturesLoaded(string sceneName)
        {
            if (hasLoadedReplacementTextures && loadedSceneName == sceneName)
                return;

            LoadReplacementTextures(sceneName);
        }

        private void LoadReplacementTextures(string sceneName)
        {
            sceneTextureKeys.Clear();
            matchedTextureNames.Clear();

            loadedSceneName = sceneName;
            hasLoadedReplacementTextures = true;

            if (string.IsNullOrEmpty(assetsFolder) || !Directory.Exists(assetsFolder))
                return;

            List<TextureFileSource> sources = GetTextureSourcesForScene(sceneName);
            for (int i = 0; i < sources.Count; i++)
            {
                TextureFileSource source = sources[i];
                if (source == null || string.IsNullOrEmpty(source.TextureKey))
                    continue;

                sceneTextureKeys.Add(source.TextureKey);

                if (replacementTextures.ContainsKey(source.TextureKey))
                    continue;

                Texture2D texture = LoadPng(source.Bytes, source.TextureKey, source.DisplayName);
                if (IsUnityObjectNull(texture))
                    continue;

                replacementTextures.Add(source.TextureKey, texture);
            }
        }

        private List<TextureFileSource> GetTextureSourcesForScene(string sceneName)
        {
            List<TextureFileSource> sources = new List<TextureFileSource>();
            AddTextureSourcesFromZipFiles(sources, sceneName);
            return sources;
        }

        private void AddTextureSourcesFromZipFiles(List<TextureFileSource> sources, string sceneName)
        {
            if (string.IsNullOrEmpty(assetsFolder) || !Directory.Exists(assetsFolder))
                return;

            string[] zipFiles = Directory.GetFiles(assetsFolder, "*.zip", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < zipFiles.Length; i++)
                AddTextureSourcesFromZip(sources, zipFiles[i], sceneName);
        }

        private void AddTextureSourcesFromZip(List<TextureFileSource> sources, string zipFile, string sceneName)
        {
            try
            {
                if (!ZipFile.IsZipFile(zipFile))
                {
                    CoreConsole.Warning($"[{Name}] ZIP de texturas inválido: {zipFile}");
                    return;
                }

                int totalPngCount = 0;
                using (ZipFile zip = ZipFile.Read(zipFile))
                {
                    foreach (ZipEntry entry in zip)
                    {
                        if (entry == null || entry.IsDirectory || !IsPngPath(entry.FileName))
                            continue;

                        totalPngCount++;
                        string textureName = Path.GetFileNameWithoutExtension(NormalizeZipPath(entry.FileName));
                        if (!ShouldLoadTextureInScene(textureName, sceneName))
                            continue;

                        using (MemoryStream stream = new MemoryStream())
                        {
                            entry.Extract(stream);
                            sources.Add(new TextureFileSource(textureName, zipFile + "::" + entry.FileName, stream.ToArray()));
                        }
                    }
                }

                if (totalPngCount > 0)
                    CoreConsole.Print($"[{Name}] Carregou ZIP de texturas '{Path.GetFileName(zipFile)}' com {totalPngCount} substituição(ões) PNG");
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[{Name}] Falha ao ler ZIP de texturas '{zipFile}': {ex.Message}");
            }
        }

        private static Texture2D LoadPng(byte[] bytes, string textureName, string displayName)
        {
            if (bytes == null || bytes.Length == 0)
            {
                CoreConsole.Warning($"[TextureReplacementSurface] Dados PNG vazios: {displayName}");
                return null;
            }

            try
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    CoreConsole.Warning($"[TextureReplacementSurface] Falha ao decodificar PNG: {displayName}");
                    return null;
                }

                texture.name = textureName;
                return texture;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[TextureReplacementSurface] Falha ao carregar PNG '{displayName}': {ex.Message}");
                return null;
            }
        }

        private int ApplyMaterialTextures()
        {
            if (replacementTextures.Count == 0 || sceneTextureKeys.Count == 0)
                return 0;

            Material[] materials = GetMaterialTargets();
            if (materials == null || materials.Length == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (ShouldSkipMaterial(material))
                    continue;

                for (int j = 0; j < TexturePropertyNames.Length; j++)
                    applied += ApplyMaterialProperty(material, TexturePropertyNames[j], null);
            }

            return applied;
        }

        private int ApplyMaterialProperty(Material material, string propertyName, string textureKeySuffix)
        {
            try
            {
                if (!material.HasProperty(propertyName))
                    return 0;

                Texture currentTexture = material.GetTexture(propertyName);
                if (IsUnityObjectNull(currentTexture))
                    return 0;

                Texture2D replacement;
                string matchedName;
                if (!TryGetRallyCoverReplacement(material, propertyName, currentTexture, out replacement, out matchedName)
                    && !TryGetReplacementTexture(currentTexture.name, textureKeySuffix, out replacement, out matchedName))
                {
                    return 0;
                }

                if (IsUnityObjectNull(replacement))
                    return 0;

                if (ReferenceEquals(currentTexture, replacement))
                {
                    matchedTextureNames.Add(matchedName);
                    return 0;
                }

                BackupOriginalMaterialTexture(material, propertyName, currentTexture);
                CopyTextureSettings(currentTexture, replacement);
                material.SetTexture(propertyName, replacement);
                matchedTextureNames.Add(matchedName);

                CoreConsole.Print($"[{Name}] Substituiu {propertyName} '{currentTexture.name}' no material '{material.name}'");
                return 1;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[{Name}] Pulou material '{material.name}' durante substituição de textura: {ex.Message}");
                return 0;
            }
        }

        private int ApplyCameraOverlays()
        {
            if (replacementTextures.Count == 0 || sceneTextureKeys.Count == 0)
                return 0;

            ScreenOverlay[] overlays = GetOverlayTargets();
            if (overlays == null || overlays.Length == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < overlays.Length; i++)
            {
                ScreenOverlay overlay = overlays[i];
                if (IsUnityObjectNull(overlay) || IsUnityObjectNull(overlay.texture))
                    continue;

                Texture2D replacement;
                string matchedName;
                if (!TryGetReplacementTexture(overlay.texture.name, out replacement, out matchedName) || IsUnityObjectNull(replacement))
                    continue;

                if (ReferenceEquals(overlay.texture, replacement))
                {
                    matchedTextureNames.Add(matchedName);
                    continue;
                }

                if (!originalOverlayTextures.ContainsKey(overlay))
                    originalOverlayTextures.Add(overlay, overlay.texture);

                CopyTextureSettings(overlay.texture, replacement);
                string oldName = overlay.texture.name;
                overlay.texture = replacement;
                matchedTextureNames.Add(matchedName);
                applied++;

                CoreConsole.Print($"[{Name}] Substituiu ScreenOverlay '{oldName}'");
            }

            return applied;
        }

        private int ApplyImageSprites()
        {
            if (replacementTextures.Count == 0 || sceneTextureKeys.Count == 0)
                return 0;

            Image[] images = GetImageTargets();
            if (images == null || images.Length == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (IsUnityObjectNull(image))
                    continue;

                try
                {
                    Sprite currentSprite = image.sprite;
                    if (IsUnityObjectNull(currentSprite) || IsReplacementSprite(currentSprite))
                        continue;

                    Texture2D replacementTexture;
                    string matchedName;
                    if (!TryGetReplacementTextureForSprite(currentSprite, out replacementTexture, out matchedName)
                        || IsUnityObjectNull(replacementTexture))
                    {
                        continue;
                    }

                    if (!originalImageSprites.ContainsKey(image))
                        originalImageSprites.Add(image, currentSprite);

                    CopyTextureSettings(currentSprite.texture, replacementTexture);
                    Sprite replacementSprite = GetReplacementSprite(currentSprite, replacementTexture);
                    if (IsUnityObjectNull(replacementSprite))
                        continue;

                    image.sprite = replacementSprite;
                    matchedTextureNames.Add(matchedName);
                    applied++;

                    CoreConsole.Print($"[{Name}] Substituiu sprite de UI Image '{currentSprite.name}' pela textura '{matchedName}'");
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Pulou UI Image durante substituição de textura: {ex.Message}");
                }
            }

            return applied;
        }

        private int InstallRallyRefreshHook(string sceneName)
        {
            if (sceneName != GameSceneName || replacementTextures.Count == 0 || sceneTextureKeys.Count == 0)
                return 0;

            GameObject go = FindGameObject(RallyRegistrationObjectPath);
            if (IsUnityObjectNull(go))
                return 0;

            PlayMakerFSM fsm = FindFsmByName(go, RallyRegistrationFsmName);
            if (IsUnityObjectNull(fsm) || fsm.FsmStates == null)
                return 0;

            HutongGames.PlayMaker.FsmState state = FindState(fsm, RallyRegistrationStateName);
            if (state == null || hasInstalledRallyRefreshHook)
                return 0;

            bool injected = MSCLoader.PlayMakerExtensions.FsmInject(
                go,
                RallyRegistrationFsmName,
                RallyRegistrationStateName,
                (Action)delegate
                {
                    ApplyTexturesOnObject(go, "card");
                },
                false,
                -1,
                false);

            if (injected)
            {
                hasInstalledRallyRefreshHook = true;
                ApplyTexturesOnObject(go, "card");
                CoreConsole.Print($"[{Name}] Instalou hook de atualização de textura do rally");
            }

            return injected ? 1 : 0;
        }

        private int ApplyTexturesOnObject(GameObject obj, string textureKeySuffix)
        {
            if (IsUnityObjectNull(obj))
                return 0;

            int applied = 0;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (!IsUnityObjectNull(renderer))
                applied += ApplyRendererTextures(renderer, textureKeySuffix);

            Renderer[] childRenderers = obj.GetComponentsInChildren<Renderer>(true);
            if (childRenderers == null)
                return applied;

            for (int i = 0; i < childRenderers.Length; i++)
            {
                Renderer child = childRenderers[i];
                if (IsUnityObjectNull(child) || child == renderer)
                    continue;

                applied += ApplyRendererTextures(child, textureKeySuffix);
            }

            return applied;
        }

        private int ApplyRendererTextures(Renderer renderer, string textureKeySuffix)
        {
            if (IsUnityObjectNull(renderer) || IsUnityObjectNull(renderer.sharedMaterial))
                return 0;

            int applied = 0;
            Material material = renderer.sharedMaterial;
            if (ShouldSkipMaterial(material))
                return 0;

            for (int i = 0; i < TexturePropertyNames.Length; i++)
                applied += ApplyMaterialProperty(material, TexturePropertyNames[i], textureKeySuffix);

            return applied;
        }

        private bool TryGetReplacementTexture(string sourceTextureName, out Texture2D replacement, out string matchedName)
        {
            return TryGetReplacementTexture(sourceTextureName, null, out replacement, out matchedName);
        }

        private bool TryGetReplacementTexture(
            string sourceTextureName,
            string textureKeySuffix,
            out Texture2D replacement,
            out string matchedName)
        {
            replacement = null;
            matchedName = null;

            string textureName = NormalizeTextureName(sourceTextureName);
            if (string.IsNullOrEmpty(textureName))
                return false;

            if (!string.IsNullOrEmpty(textureKeySuffix))
                textureName += textureKeySuffix;

            if (replacementTextures.TryGetValue(textureName, out replacement))
            {
                matchedName = textureName;
                return true;
            }

            return false;
        }

        private bool TryGetReplacementTextureForSprite(Sprite sprite, out Texture2D replacement, out string matchedName)
        {
            replacement = null;
            matchedName = null;

            if (IsUnityObjectNull(sprite))
                return false;

            if (TryGetReplacementTexture(sprite.name, out replacement, out matchedName))
                return true;

            Texture2D sourceTexture = sprite.texture;
            return !IsUnityObjectNull(sourceTexture)
                && TryGetReplacementTexture(sourceTexture.name, out replacement, out matchedName);
        }

        private Sprite GetReplacementSprite(Sprite sourceSprite, Texture2D replacementTexture)
        {
            Sprite replacementSprite;
            if (replacementSprites.TryGetValue(sourceSprite, out replacementSprite) && !IsUnityObjectNull(replacementSprite))
                return replacementSprite;

            Rect sourceRect = sourceSprite.rect;
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            if (sourceRect.width > 0f && sourceRect.height > 0f)
                pivot = new Vector2(sourceSprite.pivot.x / sourceRect.width, sourceSprite.pivot.y / sourceRect.height);

            Rect replacementRect = new Rect(0f, 0f, replacementTexture.width, replacementTexture.height);
            Texture2D sourceTexture = sourceSprite.texture;
            if (!IsUnityObjectNull(sourceTexture)
                && sourceTexture.width == replacementTexture.width
                && sourceTexture.height == replacementTexture.height)
            {
                replacementRect = sourceRect;
            }

            replacementSprite = Sprite.Create(
                replacementTexture,
                replacementRect,
                pivot,
                sourceSprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                sourceSprite.border);
            if (IsUnityObjectNull(replacementSprite))
                return null;

            replacementSprite.name = sourceSprite.name;
            replacementSprites[sourceSprite] = replacementSprite;
            replacementSpriteSet.Add(replacementSprite);
            return replacementSprite;
        }

        private bool TryGetRallyCoverReplacement(
            Material material,
            string propertyName,
            Texture currentTexture,
            out Texture2D replacement,
            out string matchedName)
        {
            replacement = null;
            matchedName = null;

            if (propertyName != "_MainTex")
                return false;
            if (IsUnityObjectNull(material) || IsUnityObjectNull(currentTexture))
                return false;
            if (NormalizeTextureName(material.name) != RallyCoverMaterialName)
                return false;
            if (!replacementTextures.TryGetValue(RallyCoverReplacementTextureName, out replacement))
                return false;

            matchedName = RallyCoverReplacementTextureName;
            return true;
        }

        private void BackupOriginalMaterialTexture(Material material, string propertyName, Texture originalTexture)
        {
            List<MaterialTextureBackup> backups;
            if (!originalMaterialTextures.TryGetValue(material, out backups))
            {
                backups = new List<MaterialTextureBackup>();
                originalMaterialTextures.Add(material, backups);
            }

            for (int i = 0; i < backups.Count; i++)
            {
                if (backups[i].PropertyName == propertyName)
                    return;
            }

            backups.Add(new MaterialTextureBackup(propertyName, originalTexture));
        }

        private void RestoreOriginalTextures()
        {
            foreach (KeyValuePair<Material, List<MaterialTextureBackup>> pair in originalMaterialTextures)
            {
                Material material = pair.Key;
                if (IsUnityObjectNull(material))
                    continue;

                List<MaterialTextureBackup> backups = pair.Value;
                for (int i = 0; i < backups.Count; i++)
                {
                    MaterialTextureBackup backup = backups[i];
                    try
                    {
                        if (material.HasProperty(backup.PropertyName))
                            material.SetTexture(backup.PropertyName, backup.OriginalTexture);
                    }
                    catch (Exception ex)
                    {
                        CoreConsole.Warning($"[{Name}] Pulou restauração de textura em '{material.name}': {ex.Message}");
                    }
                }
            }

            originalMaterialTextures.Clear();

            foreach (KeyValuePair<ScreenOverlay, Texture2D> pair in originalOverlayTextures)
            {
                ScreenOverlay overlay = pair.Key;
                if (IsUnityObjectNull(overlay))
                    continue;

                overlay.texture = pair.Value;
            }

            originalOverlayTextures.Clear();
        }

        private void RestoreOriginalImageSprites()
        {
            foreach (KeyValuePair<Image, Sprite> pair in originalImageSprites)
            {
                Image image = pair.Key;
                if (IsUnityObjectNull(image))
                    continue;

                try
                {
                    image.sprite = pair.Value;
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Pulou restauração de sprite de UI Image: {ex.Message}");
                }
            }

            originalImageSprites.Clear();
        }

        private void DestroyReplacementSprites()
        {
            foreach (KeyValuePair<Sprite, Sprite> pair in replacementSprites)
            {
                if (IsUnityObjectNull(pair.Value))
                    continue;

                try
                {
                    UnityEngine.Object.Destroy(pair.Value);
                }
                catch
                {
                }
            }

            replacementSprites.Clear();
            replacementSpriteSet.Clear();
        }

        private void DestroyReplacementTextures()
        {
            foreach (KeyValuePair<string, Texture2D> pair in replacementTextures)
            {
                if (IsUnityObjectNull(pair.Value))
                    continue;

                try
                {
                    UnityEngine.Object.Destroy(pair.Value);
                }
                catch
                {
                }
            }
        }

        private static void CopyTextureSettings(Texture source, Texture2D replacement)
        {
            if (IsUnityObjectNull(source) || IsUnityObjectNull(replacement))
                return;

            replacement.wrapMode = source.wrapMode;
            replacement.filterMode = source.filterMode;
            replacement.anisoLevel = source.anisoLevel;
            replacement.mipMapBias = source.mipMapBias;
        }

        private void LogUnmatchedTextures()
        {
            foreach (string textureName in sceneTextureKeys)
            {
                if (matchedTextureNames.Contains(textureName))
                    continue;

                CoreConsole.Print($"[{Name}] PNG não correspondeu a nenhuma textura carregada em {loadedSceneName}: {textureName}.png");
            }
        }

        private static bool IsPngPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && string.Equals(Path.GetExtension(NormalizeZipPath(path)), ".png", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeZipPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('/', Path.DirectorySeparatorChar);
        }

        private static bool ShouldApplyInScene(string sceneName)
        {
            return sceneName == MainMenuSceneName || sceneName == GameSceneName;
        }

        private static bool ShouldLoadTextureInScene(string textureName, string sceneName)
        {
            bool isMenuOnlyTexture = IsMenuOnlyTexture(textureName);
            if (sceneName == MainMenuSceneName)
                return isMenuOnlyTexture;

            return !isMenuOnlyTexture;
        }

        private static bool IsMenuOnlyTexture(string textureName)
        {
            return string.Equals(textureName, DriversLicenceTextureName, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTextureName(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return textureName;

            return textureName.Replace(" (Instance)", string.Empty).Trim();
        }

        private static bool ShouldSkipMaterial(Material material)
        {
            if (IsUnityObjectNull(material))
                return true;

            Shader shader = material.shader;
            if (IsUnityObjectNull(shader))
                return true;

            string shaderName = shader.name;
            if (string.IsNullOrEmpty(shaderName))
                return false;

            for (int i = 0; i < IgnoredShaderPrefixes.Length; i++)
            {
                if (shaderName.StartsWith(IgnoredShaderPrefixes[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static GameObject FindGameObject(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            GameObject found = GameObject.Find(path);
            if (!IsUnityObjectNull(found))
                return found;

            string leafName = path.Substring(path.LastIndexOf('/') + 1);
            bool exactPathRequired = path.IndexOf('/') >= 0;
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != leafName)
                    continue;

                if (!exactPathRequired)
                    return t.gameObject;

                if (LocalizationUtils.GetGameObjectPath(t.gameObject) == path)
                    return t.gameObject;
            }

            return null;
        }

        private static PlayMakerFSM FindFsmByName(GameObject go, string fsmName)
        {
            if (IsUnityObjectNull(go))
                return null;

            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (!IsUnityObjectNull(fsm) && FsmUtils.GetFsmName(fsm) == fsmName)
                    return fsm;
            }

            return null;
        }

        private static HutongGames.PlayMaker.FsmState FindState(PlayMakerFSM fsm, string stateName)
        {
            if (IsUnityObjectNull(fsm) || fsm.FsmStates == null)
                return null;

            HutongGames.PlayMaker.FsmState[] states = fsm.FsmStates;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && states[i].Name == stateName)
                    return states[i];
            }

            return null;
        }

        private static bool IsUnityObjectNull(UnityEngine.Object obj)
        {
            return ReferenceEquals(obj, null) || obj == null;
        }

        private bool IsReplacementSprite(Sprite sprite)
        {
            return !IsUnityObjectNull(sprite) && replacementSpriteSet.Contains(sprite);
        }

        private static int Count(Array values)
        {
            return values != null ? values.Length : 0;
        }
    }
}
