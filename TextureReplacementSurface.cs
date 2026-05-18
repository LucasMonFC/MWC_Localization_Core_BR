using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Replaces Unity material textures with PNG files placed in the language pack.
    /// A file named "foo.png" replaces any material texture whose Unity object name is "foo".
    /// </summary>
    public sealed class TextureReplacementSurface : ITranslationSurface
    {
        private const string TextureFolderName = "textures";
        private const string MainMenuSceneName = "MainMenu";
        private const string GameSceneName = "GAME";
        private const string DriversLicenceTextureName = "drivers_lincence";
        private const string ScreenOverlayTypeName = "UnityStandardAssets.ImageEffects.ScreenOverlay";
        private const string ScreenOverlayTextureFieldName = "texture";
        private const string RallySheetMainTextureProperty = "_MainTex";
        internal const int RallySheetRefreshFrames = 90;

        private static readonly string[] RallySheetDynamicTextureNames = new string[]
        {
            "rally_register",
            "rally_register_jr",
        };

        private static readonly string[] RallySheetTriggerPaths = new string[]
        {
            "Sheets/RallyformAmateur/bg",
            "Sheets/RallyformJunior/bg",
        };

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

        private sealed class OverlayTextureBackup
        {
            public readonly FieldInfo TextureField;
            public readonly Texture OriginalTexture;

            public OverlayTextureBackup(FieldInfo textureField, Texture originalTexture)
            {
                TextureField = textureField;
                OriginalTexture = originalTexture;
            }
        }

        private readonly Dictionary<string, Texture2D> replacementTextures =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<Material, List<MaterialTextureBackup>> originalTextures =
            new Dictionary<Material, List<MaterialTextureBackup>>();

        private readonly Dictionary<MonoBehaviour, OverlayTextureBackup> originalOverlayTextures =
            new Dictionary<MonoBehaviour, OverlayTextureBackup>();

        private readonly HashSet<string> matchedTextureNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> activeTextureNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string texturesFolder;
        private string loadedSceneName;
        private bool hasApplied;
        private bool hasLoadedReplacementTextures;

        public string Name { get { return "TextureReplacementSurface"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.OncePerScene; } }

        public bool IsComplete
        {
            get { return true; }
        }

        public void Initialize(TranslationContext ctx)
        {
            texturesFolder = ctx != null ? Path.Combine(ctx.AssetsFolder, TextureFolderName) : null;
            hasApplied = false;
            loadedSceneName = null;
            activeTextureNames.Clear();
            matchedTextureNames.Clear();
            hasLoadedReplacementTextures = false;
        }

        public int InitialPass()
        {
            string sceneName = Application.loadedLevelName;
            if (!ShouldApplyInScene(sceneName) || hasApplied)
                return 0;

            EnsureReplacementTexturesLoaded(sceneName);
            int applied = ApplyTextures();
            InstallRallySheetTriggers();
            hasApplied = true;
            LogUnmatchedTextures();
            return applied;
        }

        public int MonitorTick(float deltaTime)
        {
            return 0;
        }

        public void Reset()
        {
            hasApplied = false;
            loadedSceneName = null;
            activeTextureNames.Clear();
            matchedTextureNames.Clear();
            hasLoadedReplacementTextures = false;
        }

        public void ClearTranslations()
        {
            RestoreOriginalTextures();
            DestroyReplacementTextures();
            replacementTextures.Clear();
            activeTextureNames.Clear();
            matchedTextureNames.Clear();
            hasApplied = false;
            loadedSceneName = null;
            hasLoadedReplacementTextures = false;
        }

        private void EnsureReplacementTexturesLoaded(string sceneName)
        {
            if (hasLoadedReplacementTextures && loadedSceneName == sceneName)
                return;

            LoadReplacementTextures(sceneName);
        }

        private void LoadReplacementTextures(string sceneName)
        {
            activeTextureNames.Clear();
            loadedSceneName = sceneName;
            hasLoadedReplacementTextures = true;

            if (string.IsNullOrEmpty(texturesFolder) || !Directory.Exists(texturesFolder))
                return;

            string[] files = GetTextureFilesForScene(sceneName);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string textureName = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(textureName))
                    continue;

                activeTextureNames.Add(textureName);

                if (replacementTextures.ContainsKey(textureName))
                    continue;

                Texture2D texture = LoadPng(file, textureName);
                if (IsUnityObjectNull(texture))
                    continue;

                replacementTextures.Add(textureName, texture);
            }

            if (activeTextureNames.Count > 0)
                CoreConsole.Print($"[{Name}] Prepared {activeTextureNames.Count} PNG texture replacement(s) for {sceneName}");
        }

        private static Texture2D LoadPng(string file, string textureName)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                Texture2D texture = new Texture2D(2, 2);
                if (!texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    CoreConsole.Warning($"[TextureReplacementSurface] Failed to decode PNG: {file}");
                    return null;
                }

                texture.name = textureName;
                return texture;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[TextureReplacementSurface] Failed loading PNG '{file}': {ex.Message}");
                return null;
            }
        }

        private int ApplyTextures()
        {
            return ApplyTextures(activeTextureNames);
        }

        private int ApplyTextures(HashSet<string> textureNames)
        {
            if (replacementTextures.Count == 0)
                return 0;
            if (textureNames == null || textureNames.Count == 0)
                return 0;

            Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
            int applied = 0;
            if (materials != null)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    try
                    {
                        if (ShouldSkipMaterial(material))
                            continue;

                        for (int j = 0; j < TexturePropertyNames.Length; j++)
                        {
                            string propertyName = TexturePropertyNames[j];
                            if (!material.HasProperty(propertyName))
                                continue;

                            Texture currentTexture = material.GetTexture(propertyName);
                            if (IsUnityObjectNull(currentTexture))
                                continue;
                            if (IsReplacementTexture(currentTexture))
                            {
                                if (textureNames.Contains(currentTexture.name))
                                    matchedTextureNames.Add(currentTexture.name);
                                continue;
                            }
                            if (!textureNames.Contains(currentTexture.name))
                                continue;

                            Texture2D replacement;
                            if (!replacementTextures.TryGetValue(currentTexture.name, out replacement) || IsUnityObjectNull(replacement))
                                continue;

                            BackupOriginalTexture(material, propertyName, currentTexture);
                            CopyTextureSettings(currentTexture, replacement);
                            material.SetTexture(propertyName, replacement);
                            matchedTextureNames.Add(replacement.name);
                            applied++;

                            CoreConsole.Print($"[{Name}] Replaced {propertyName} '{currentTexture.name}' in material '{material.name}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        CoreConsole.Warning($"[{Name}] Skipped material during texture replacement: {ex.Message}");
                    }
                }
            }

            applied += ApplyScreenOverlayTextures(textureNames);
            return applied;
        }

        private int ApplyScreenOverlayTextures(HashSet<string> textureNames)
        {
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            if (behaviours == null || behaviours.Length == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (IsUnityObjectNull(behaviour))
                    continue;

                try
                {
                    if (IsUnityObjectNull(behaviour.gameObject))
                        continue;

                    Type type = behaviour.GetType();
                    if (type == null || type.FullName != ScreenOverlayTypeName)
                        continue;

                    FieldInfo textureField = type.GetField(ScreenOverlayTextureFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (textureField == null)
                        continue;

                    Texture currentTexture = textureField.GetValue(behaviour) as Texture;
                    if (IsUnityObjectNull(currentTexture))
                        continue;
                    if (IsReplacementTexture(currentTexture))
                    {
                        if (textureNames.Contains(currentTexture.name))
                            matchedTextureNames.Add(currentTexture.name);
                        continue;
                    }
                    if (!textureNames.Contains(currentTexture.name))
                        continue;

                    Texture2D replacement;
                    if (!replacementTextures.TryGetValue(currentTexture.name, out replacement) || IsUnityObjectNull(replacement))
                        continue;

                    BackupOriginalOverlayTexture(behaviour, textureField, currentTexture);
                    CopyTextureSettings(currentTexture, replacement);
                    textureField.SetValue(behaviour, replacement);
                    matchedTextureNames.Add(replacement.name);
                    applied++;

                    CoreConsole.Print($"[{Name}] Replaced ScreenOverlay '{currentTexture.name}' in '{behaviour.gameObject.name}'");
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped ScreenOverlay during texture replacement: {ex.Message}");
                }
            }

            return applied;
        }

        internal int ApplyRallySheetTextures(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0 || replacementTextures.Count == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (IsUnityObjectNull(renderer))
                    continue;

                try
                {
                    Material[] materials = renderer.sharedMaterials;
                    if (materials == null)
                        continue;

                    for (int j = 0; j < materials.Length; j++)
                    {
                        Material material = materials[j];
                        if (ShouldSkipMaterial(material) || !material.HasProperty(RallySheetMainTextureProperty))
                            continue;

                        Texture currentTexture = material.GetTexture(RallySheetMainTextureProperty);
                        if (IsUnityObjectNull(currentTexture))
                            continue;

                        if (IsReplacementTexture(currentTexture))
                        {
                            if (IsRallySheetDynamicTexture(currentTexture.name))
                                matchedTextureNames.Add(currentTexture.name);
                            continue;
                        }

                        if (!IsRallySheetDynamicTexture(currentTexture.name))
                            continue;

                        Texture2D replacement;
                        if (!replacementTextures.TryGetValue(currentTexture.name, out replacement) || IsUnityObjectNull(replacement))
                            continue;

                        BackupOriginalTexture(material, RallySheetMainTextureProperty, currentTexture);
                        CopyTextureSettings(currentTexture, replacement);
                        material.SetTexture(RallySheetMainTextureProperty, replacement);
                        matchedTextureNames.Add(replacement.name);
                        applied++;
                    }
                }
                catch
                {
                }
            }

            return applied;
        }

        private void InstallRallySheetTriggers()
        {
            if (loadedSceneName != GameSceneName || !HasRallySheetDynamicTextures())
                return;

            for (int i = 0; i < RallySheetTriggerPaths.Length; i++)
            {
                GameObject obj = LocalizationUtils.FindGameObjectIncludingInactive(RallySheetTriggerPaths[i]);
                if (IsUnityObjectNull(obj))
                    continue;

                RallySheetTextureRefreshTrigger trigger = obj.GetComponent<RallySheetTextureRefreshTrigger>();
                if (trigger == null)
                    trigger = obj.AddComponent<RallySheetTextureRefreshTrigger>();

                trigger.Initialize(this);
                if (obj.activeInHierarchy)
                    trigger.QueueRefresh();
            }
        }

        private bool HasRallySheetDynamicTextures()
        {
            for (int i = 0; i < RallySheetDynamicTextureNames.Length; i++)
            {
                if (replacementTextures.ContainsKey(RallySheetDynamicTextureNames[i]))
                    return true;
            }

            return false;
        }

        private string[] GetTextureFilesForScene(string sceneName)
        {
            if (sceneName == MainMenuSceneName)
            {
                string file = Path.Combine(texturesFolder, DriversLicenceTextureName + ".png");
                return File.Exists(file) ? new string[] { file } : new string[0];
            }

            string[] files = Directory.GetFiles(texturesFolder, "*.png");
            List<string> gameFiles = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string textureName = Path.GetFileNameWithoutExtension(files[i]);
                if (IsMenuOnlyTexture(textureName))
                    continue;

                gameFiles.Add(files[i]);
            }

            return gameFiles.ToArray();
        }

        private static bool IsMenuOnlyTexture(string textureName)
        {
            return string.Equals(textureName, DriversLicenceTextureName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRallySheetDynamicTexture(string textureName)
        {
            for (int i = 0; i < RallySheetDynamicTextureNames.Length; i++)
            {
                if (string.Equals(textureName, RallySheetDynamicTextureNames[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ShouldApplyInScene(string sceneName)
        {
            return sceneName == GameSceneName || sceneName == MainMenuSceneName;
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

        private bool IsReplacementTexture(Texture texture)
        {
            if (IsUnityObjectNull(texture))
                return false;

            Texture2D replacement;
            return replacementTextures.TryGetValue(texture.name, out replacement)
                && !IsUnityObjectNull(replacement)
                && ReferenceEquals(texture, replacement);
        }

        private void BackupOriginalTexture(Material material, string propertyName, Texture originalTexture)
        {
            List<MaterialTextureBackup> backups;
            if (!originalTextures.TryGetValue(material, out backups))
            {
                backups = new List<MaterialTextureBackup>();
                originalTextures.Add(material, backups);
            }

            for (int i = 0; i < backups.Count; i++)
            {
                if (backups[i].PropertyName == propertyName)
                    return;
            }

            backups.Add(new MaterialTextureBackup(propertyName, originalTexture));
        }

        private void BackupOriginalOverlayTexture(MonoBehaviour behaviour, FieldInfo textureField, Texture originalTexture)
        {
            if (originalOverlayTextures.ContainsKey(behaviour))
                return;

            originalOverlayTextures.Add(behaviour, new OverlayTextureBackup(textureField, originalTexture));
        }

        private static void CopyTextureSettings(Texture source, Texture2D replacement)
        {
            if (IsUnityObjectNull(source) || IsUnityObjectNull(replacement))
                return;

            replacement.filterMode = source.filterMode;
            replacement.anisoLevel = source.anisoLevel;
            replacement.wrapMode = source.wrapMode;
            replacement.mipMapBias = source.mipMapBias;
        }

        private void RestoreOriginalTextures()
        {
            foreach (KeyValuePair<Material, List<MaterialTextureBackup>> pair in originalTextures)
            {
                Material material = pair.Key;
                if (IsUnityObjectNull(material))
                    continue;

                try
                {
                    List<MaterialTextureBackup> backups = pair.Value;
                    for (int i = 0; i < backups.Count; i++)
                    {
                        MaterialTextureBackup backup = backups[i];
                        if (backup == null || string.IsNullOrEmpty(backup.PropertyName))
                            continue;
                        if (!material.HasProperty(backup.PropertyName))
                            continue;

                        material.SetTexture(backup.PropertyName, backup.OriginalTexture);
                    }
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped restoring a material texture: {ex.Message}");
                }
            }

            originalTextures.Clear();

            foreach (KeyValuePair<MonoBehaviour, OverlayTextureBackup> pair in originalOverlayTextures)
            {
                MonoBehaviour behaviour = pair.Key;
                OverlayTextureBackup backup = pair.Value;
                if (IsUnityObjectNull(behaviour) || backup == null || backup.TextureField == null)
                    continue;

                try
                {
                    backup.TextureField.SetValue(behaviour, backup.OriginalTexture);
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped restoring a ScreenOverlay texture: {ex.Message}");
                }
            }

            originalOverlayTextures.Clear();
        }

        private void DestroyReplacementTextures()
        {
            foreach (KeyValuePair<string, Texture2D> pair in replacementTextures)
            {
                if (!IsUnityObjectNull(pair.Value))
                {
                    try
                    {
                        UnityEngine.Object.Destroy(pair.Value);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static bool IsUnityObjectNull(UnityEngine.Object obj)
        {
            return ReferenceEquals(obj, null);
        }

        private void LogUnmatchedTextures()
        {
            foreach (string textureName in activeTextureNames)
            {
                if (matchedTextureNames.Contains(textureName))
                    continue;
                if (IsRallySheetDynamicTexture(textureName))
                    continue;

                CoreConsole.Warning($"[{Name}] PNG did not match any loaded texture in {loadedSceneName}: {textureName}.png");
            }
        }
    }

    public sealed class RallySheetTextureRefreshTrigger : MonoBehaviour
    {
        private TextureReplacementSurface owner;
        private Renderer[] renderers;
        private int refreshFramesRemaining;

        public void Initialize(TextureReplacementSurface surface)
        {
            owner = surface;
            CacheRenderers();
        }

        public void QueueRefresh()
        {
            CacheRenderers();
            refreshFramesRemaining = TextureReplacementSurface.RallySheetRefreshFrames;
        }

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnEnable()
        {
            QueueRefresh();
        }

        private void LateUpdate()
        {
            if (refreshFramesRemaining <= 0)
                return;

            refreshFramesRemaining--;
            if (owner != null)
                owner.ApplyRallySheetTextures(renderers);
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
