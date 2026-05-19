using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ionic.Zip;
using MSCLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Replaces Unity material textures with PNG files placed in the language pack.
    /// A file named "foo.png" replaces any material texture whose Unity object name is "foo".
    /// </summary>
    public sealed class TextureReplacementSurface : ITranslationSurface
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string GameSceneName = "GAME";
        private const string DriversLicenceTextureName = "drivers_lincence";
        private const string ScreenOverlayTypeName = "UnityStandardAssets.ImageEffects.ScreenOverlay";
        private const string ScreenOverlayTextureFieldName = "texture";
        private const string RallySheetMainTextureProperty = "_MainTex";
        internal const float LateTextureRefreshDurationSeconds = 10f;
        internal const float LateTextureRefreshIntervalSeconds = 1f;
        private const string LateTextureRefreshObjectName = "MSC_TextureLateRefresh";

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

        private sealed class TextureFileSource
        {
            public readonly string TextureName;
            public readonly string DisplayName;
            public readonly byte[] Bytes;

            public TextureFileSource(string textureName, string displayName, byte[] bytes)
            {
                TextureName = textureName;
                DisplayName = displayName;
                Bytes = bytes;
            }
        }

        private readonly Dictionary<string, Texture2D> replacementTextures =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<Material, List<MaterialTextureBackup>> originalTextures =
            new Dictionary<Material, List<MaterialTextureBackup>>();

        private readonly Dictionary<MonoBehaviour, OverlayTextureBackup> originalOverlayTextures =
            new Dictionary<MonoBehaviour, OverlayTextureBackup>();

        private readonly Dictionary<RawImage, Texture> originalRawImageTextures =
            new Dictionary<RawImage, Texture>();

        private readonly Dictionary<Image, Sprite> originalImageSprites =
            new Dictionary<Image, Sprite>();

        private readonly Dictionary<SpriteRenderer, Sprite> originalSpriteRendererSprites =
            new Dictionary<SpriteRenderer, Sprite>();

        private readonly Dictionary<Sprite, Sprite> replacementSprites =
            new Dictionary<Sprite, Sprite>();

        private readonly HashSet<Sprite> replacementSpriteSet =
            new HashSet<Sprite>();

        private readonly HashSet<string> matchedTextureNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> activeTextureNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string assetsFolder;
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
            assetsFolder = ctx != null ? ctx.AssetsFolder : null;
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
            bool hasPendingLateRefresh = InstallLateTextureRefreshTrigger();
            hasApplied = true;
            if (!hasPendingLateRefresh)
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
            RestoreOriginalRawImageTextures();
            RestoreOriginalSprites();
            DestroyReplacementSprites();
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

            if (string.IsNullOrEmpty(assetsFolder) || !Directory.Exists(assetsFolder))
                return;

            List<TextureFileSource> sources = GetTextureSourcesForScene(sceneName);
            for (int i = 0; i < sources.Count; i++)
            {
                TextureFileSource source = sources[i];
                string textureName = source.TextureName;
                if (string.IsNullOrEmpty(textureName))
                    continue;

                activeTextureNames.Add(textureName);

                if (replacementTextures.ContainsKey(textureName))
                    continue;

                Texture2D texture = LoadPng(source.Bytes, textureName, source.DisplayName);
                if (IsUnityObjectNull(texture))
                    continue;

                replacementTextures.Add(textureName, texture);
            }

            if (activeTextureNames.Count > 0)
                CoreConsole.Print($"[{Name}] Prepared {activeTextureNames.Count} PNG texture replacement(s) for {sceneName}");
        }

        private static Texture2D LoadPng(byte[] bytes, string textureName, string displayName)
        {
            if (bytes == null || bytes.Length == 0)
            {
                CoreConsole.Warning($"[TextureReplacementSurface] Empty PNG data: {displayName}");
                return null;
            }

            try
            {
                Texture2D texture = new Texture2D(2, 2);
                if (!texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    CoreConsole.Warning($"[TextureReplacementSurface] Failed to decode PNG: {displayName}");
                    return null;
                }

                texture.name = textureName;
                return texture;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[TextureReplacementSurface] Failed loading PNG '{displayName}': {ex.Message}");
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

            int applied = ApplyMaterialTextures(textureNames);
            applied += ApplyNonMaterialTextures(textureNames);
            return applied;
        }

        private int ApplyMaterialTextures(HashSet<string> textureNames)
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

            return applied;
        }

        private int ApplyNonMaterialTextures(HashSet<string> textureNames)
        {
            if (replacementTextures.Count == 0)
                return 0;
            if (textureNames == null || textureNames.Count == 0)
                return 0;

            int applied = 0;
            applied += ApplyScreenOverlayTextures(textureNames);
            applied += ApplyRawImageTextures(textureNames);
            applied += ApplyImageSprites(textureNames);
            applied += ApplySpriteRendererSprites(textureNames);
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

        private int ApplyRawImageTextures(HashSet<string> textureNames)
        {
            RawImage[] images = GetComponentsIncludingInactive<RawImage>();
            if (images == null || images.Length == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < images.Length; i++)
            {
                RawImage image = images[i];
                if (IsUnityObjectNull(image))
                    continue;

                try
                {
                    Texture currentTexture = image.texture;
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

                    BackupOriginalRawImageTexture(image, currentTexture);
                    CopyTextureSettings(currentTexture, replacement);
                    image.texture = replacement;
                    matchedTextureNames.Add(replacement.name);
                    applied++;

                    CoreConsole.Print($"[{Name}] Replaced RawImage texture '{currentTexture.name}' in '{image.gameObject.name}'");
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped RawImage during texture replacement: {ex.Message}");
                }
            }

            return applied;
        }

        private int ApplyImageSprites(HashSet<string> textureNames)
        {
            Image[] images = GetComponentsIncludingInactive<Image>();
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
                    string matchedTextureName;
                    if (!TryGetReplacementTextureForSprite(currentSprite, textureNames, out replacementTexture, out matchedTextureName))
                        continue;

                    BackupOriginalImageSprite(image, currentSprite);
                    Texture2D sourceTexture = currentSprite.texture;
                    CopyTextureSettings(sourceTexture, replacementTexture);
                    Sprite replacementSprite = GetReplacementSprite(currentSprite, replacementTexture);
                    if (IsUnityObjectNull(replacementSprite))
                        continue;

                    image.sprite = replacementSprite;
                    matchedTextureNames.Add(matchedTextureName);
                    applied++;

                    CoreConsole.Print($"[{Name}] Replaced UI Image sprite '{currentSprite.name}' from texture '{matchedTextureName}'");
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped UI Image during texture replacement: {ex.Message}");
                }
            }

            return applied;
        }

        private int ApplySpriteRendererSprites(HashSet<string> textureNames)
        {
            SpriteRenderer[] renderers = GetComponentsIncludingInactive<SpriteRenderer>();
            if (renderers == null || renderers.Length == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (IsUnityObjectNull(renderer))
                    continue;

                try
                {
                    Sprite currentSprite = renderer.sprite;
                    if (IsUnityObjectNull(currentSprite) || IsReplacementSprite(currentSprite))
                        continue;

                    Texture2D replacementTexture;
                    string matchedTextureName;
                    if (!TryGetReplacementTextureForSprite(currentSprite, textureNames, out replacementTexture, out matchedTextureName))
                        continue;

                    BackupOriginalSpriteRendererSprite(renderer, currentSprite);
                    Texture2D sourceTexture = currentSprite.texture;
                    CopyTextureSettings(sourceTexture, replacementTexture);
                    Sprite replacementSprite = GetReplacementSprite(currentSprite, replacementTexture);
                    if (IsUnityObjectNull(replacementSprite))
                        continue;

                    renderer.sprite = replacementSprite;
                    matchedTextureNames.Add(matchedTextureName);
                    applied++;

                    CoreConsole.Print($"[{Name}] Replaced SpriteRenderer sprite '{currentSprite.name}' from texture '{matchedTextureName}'");
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped SpriteRenderer during texture replacement: {ex.Message}");
                }
            }

            return applied;
        }

        private bool TryGetReplacementTextureForSprite(Sprite sprite, HashSet<string> textureNames, out Texture2D replacementTexture, out string matchedTextureName)
        {
            replacementTexture = null;
            matchedTextureName = null;

            if (IsUnityObjectNull(sprite))
                return false;

            if (TryGetReplacementTextureByName(sprite.name, textureNames, out replacementTexture, out matchedTextureName))
                return true;

            Texture2D sourceTexture = sprite.texture;
            return !IsUnityObjectNull(sourceTexture)
                && TryGetReplacementTextureByName(sourceTexture.name, textureNames, out replacementTexture, out matchedTextureName);
        }

        private bool TryGetReplacementTextureByName(string textureName, HashSet<string> textureNames, out Texture2D replacementTexture, out string matchedTextureName)
        {
            replacementTexture = null;
            matchedTextureName = null;

            if (string.IsNullOrEmpty(textureName) || !textureNames.Contains(textureName))
                return false;

            Texture2D replacement;
            if (!replacementTextures.TryGetValue(textureName, out replacement) || IsUnityObjectNull(replacement))
                return false;

            replacementTexture = replacement;
            matchedTextureName = textureName;
            return true;
        }

        private static T[] GetComponentsIncludingInactive<T>() where T : Component
        {
            List<T> components = new List<T>();
            HashSet<T> seen = new HashSet<T>();

            T[] directComponents = Resources.FindObjectsOfTypeAll<T>();
            AddUniqueComponents(directComponents, components, seen);

            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            if (objects != null)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    GameObject obj = objects[i];
                    if (IsUnityObjectNull(obj))
                        continue;

                    try
                    {
                        T[] childComponents = obj.GetComponentsInChildren<T>(true);
                        AddUniqueComponents(childComponents, components, seen);
                    }
                    catch
                    {
                    }
                }
            }

            return components.ToArray();
        }

        private static void AddUniqueComponents<T>(T[] source, List<T> destination, HashSet<T> seen) where T : Component
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                T item = source[i];
                if (IsUnityObjectNull(item) || seen.Contains(item))
                    continue;

                seen.Add(item);
                destination.Add(item);
            }
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

                HookRallySheetFsmTextures(obj);
            }
        }

        private int HookRallySheetFsmTextures(GameObject sheetBackground)
        {
            GameObject sheetRoot = FindRallySheetRoot(sheetBackground);
            if (IsUnityObjectNull(sheetRoot))
                return 0;

            int hooked = 0;
            PlayMakerFSM[] fsms = sheetRoot.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (!EnsureFsmInitialized(fsm) || FsmUtils.GetFsmName(fsm) != "Setup")
                    continue;

                HutongGames.PlayMaker.FsmState state = FindFsmState(fsm, "Init");
                if (state == null || state.Actions == null)
                    continue;

                for (int actionIndex = 0; actionIndex < state.Actions.Length; actionIndex++)
                {
                    HutongGames.PlayMaker.FsmStateAction action = state.Actions[actionIndex];
                    if (action == null || action.GetType().FullName != "HutongGames.PlayMaker.Actions.SetMaterialTexture")
                        continue;

                    if (HookRallySheetTextureAction(action))
                        hooked++;
                }
            }

            if (hooked > 0)
                ApplyRallySheetTextures(sheetRoot.GetComponentsInChildren<Renderer>(true));

            return hooked;
        }

        private GameObject FindRallySheetRoot(GameObject sheetBackground)
        {
            if (IsUnityObjectNull(sheetBackground))
                return null;

            Transform current = sheetBackground.transform;
            while (current != null && current.parent != null && current.parent.name != "Sheets")
                current = current.parent;

            return current != null ? current.gameObject : sheetBackground;
        }

        private bool HookRallySheetTextureAction(HutongGames.PlayMaker.FsmStateAction action)
        {
            FieldInfo textureField = FsmUtils.GetField(action.GetType(), "texture");
            if (textureField == null)
                return false;

            HutongGames.PlayMaker.FsmTexture textureValue = textureField.GetValue(action) as HutongGames.PlayMaker.FsmTexture;
            if (textureValue == null || IsUnityObjectNull(textureValue.Value))
                return false;

            Texture currentTexture = textureValue.Value;
            if (!IsRallySheetDynamicTexture(currentTexture.name))
                return false;

            Texture2D replacement;
            if (!replacementTextures.TryGetValue(currentTexture.name, out replacement) || IsUnityObjectNull(replacement))
                return false;

            if (IsReplacementTexture(currentTexture))
                return false;

            CopyTextureSettings(currentTexture, replacement);
            textureValue.Value = replacement;
            matchedTextureNames.Add(replacement.name);
            return true;
        }

        private bool EnsureFsmInitialized(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null)
                return false;
            if (fsm.Fsm.Initialized)
                return fsm.FsmStates != null;

            try
            {
                fsm.Fsm.InitData();
            }
            catch
            {
            }

            return fsm.FsmStates != null;
        }

        private static HutongGames.PlayMaker.FsmState FindFsmState(PlayMakerFSM fsm, string stateName)
        {
            if (fsm == null || fsm.FsmStates == null)
                return null;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state != null && state.Name == stateName)
                    return state;
            }

            return null;
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

        private bool InstallLateTextureRefreshTrigger()
        {
            if (loadedSceneName != GameSceneName || !HasGenericTextures())
                return false;

            GameObject obj = GameObject.Find(LateTextureRefreshObjectName);
            if (IsUnityObjectNull(obj))
                obj = new GameObject(LateTextureRefreshObjectName);

            LateTextureRefreshTrigger trigger = obj.GetComponent<LateTextureRefreshTrigger>();
            if (trigger == null)
                trigger = obj.AddComponent<LateTextureRefreshTrigger>();

            trigger.Initialize(this);
            return true;
        }

        private bool HasGenericTextures()
        {
            foreach (string textureName in activeTextureNames)
            {
                if (IsRallySheetDynamicTexture(textureName))
                    continue;
                if (replacementTextures.ContainsKey(textureName))
                    return true;
            }

            return false;
        }

        private HashSet<string> GetGenericTextureNames()
        {
            HashSet<string> textureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string textureName in activeTextureNames)
            {
                if (IsRallySheetDynamicTexture(textureName))
                    continue;
                if (replacementTextures.ContainsKey(textureName))
                    textureNames.Add(textureName);
            }

            return textureNames;
        }

        private HashSet<string> GetUnmatchedGenericTextureNames()
        {
            HashSet<string> unmatchedTextureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string textureName in activeTextureNames)
            {
                if (IsRallySheetDynamicTexture(textureName))
                    continue;
                if (replacementTextures.ContainsKey(textureName) && !matchedTextureNames.Contains(textureName))
                    unmatchedTextureNames.Add(textureName);
            }

            return unmatchedTextureNames;
        }

        internal int ApplyLateTextureRefresh()
        {
            if (replacementTextures.Count == 0 || activeTextureNames.Count == 0)
                return 0;

            int applied = ApplyMaterialTextures(GetGenericTextureNames());

            HashSet<string> unmatchedTextureNames = GetUnmatchedGenericTextureNames();
            if (unmatchedTextureNames.Count > 0)
                applied += ApplyNonMaterialTextures(unmatchedTextureNames);

            return applied;
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
                    CoreConsole.Warning($"[TextureReplacementSurface] Invalid texture ZIP: {zipFile}");
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
                    ModConsole.Print($"[MSC_LC] [{Name}] Loaded texture ZIP '{Path.GetFileName(zipFile)}' with {totalPngCount} PNG texture replacement(s)");
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[TextureReplacementSurface] Failed reading texture ZIP '{zipFile}': {ex.Message}");
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

        private static bool ShouldLoadTextureInScene(string textureName, string sceneName)
        {
            if (sceneName == MainMenuSceneName)
                return IsMenuOnlyTexture(textureName);

            return !IsMenuOnlyTexture(textureName);
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

        private void BackupOriginalRawImageTexture(RawImage image, Texture originalTexture)
        {
            if (originalRawImageTextures.ContainsKey(image))
                return;

            originalRawImageTextures.Add(image, originalTexture);
        }

        private void BackupOriginalImageSprite(Image image, Sprite originalSprite)
        {
            if (originalImageSprites.ContainsKey(image))
                return;

            originalImageSprites.Add(image, originalSprite);
        }

        private void BackupOriginalSpriteRendererSprite(SpriteRenderer renderer, Sprite originalSprite)
        {
            if (originalSpriteRendererSprites.ContainsKey(renderer))
                return;

            originalSpriteRendererSprites.Add(renderer, originalSprite);
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

        private void RestoreOriginalRawImageTextures()
        {
            foreach (KeyValuePair<RawImage, Texture> pair in originalRawImageTextures)
            {
                RawImage image = pair.Key;
                if (IsUnityObjectNull(image))
                    continue;

                try
                {
                    image.texture = pair.Value;
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped restoring a RawImage texture: {ex.Message}");
                }
            }

            originalRawImageTextures.Clear();
        }

        private void RestoreOriginalSprites()
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
                    CoreConsole.Warning($"[{Name}] Skipped restoring a UI Image sprite: {ex.Message}");
                }
            }

            originalImageSprites.Clear();

            foreach (KeyValuePair<SpriteRenderer, Sprite> pair in originalSpriteRendererSprites)
            {
                SpriteRenderer renderer = pair.Key;
                if (IsUnityObjectNull(renderer))
                    continue;

                try
                {
                    renderer.sprite = pair.Value;
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped restoring a SpriteRenderer sprite: {ex.Message}");
                }
            }

            originalSpriteRendererSprites.Clear();
        }

        private void DestroyReplacementSprites()
        {
            foreach (KeyValuePair<Sprite, Sprite> pair in replacementSprites)
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

            replacementSprites.Clear();
            replacementSpriteSet.Clear();
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

        private bool IsReplacementSprite(Sprite sprite)
        {
            return !IsUnityObjectNull(sprite) && replacementSpriteSet.Contains(sprite);
        }

        internal void LogUnmatchedTextures()
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

    public sealed class LateTextureRefreshTrigger : MonoBehaviour
    {
        private TextureReplacementSurface owner;
        private bool hasStarted;
        private float refreshSecondsRemaining;
        private float refreshSecondsUntilNextPass;

        public void Initialize(TextureReplacementSurface surface)
        {
            owner = surface;
            hasStarted = false;
            refreshSecondsRemaining = TextureReplacementSurface.LateTextureRefreshDurationSeconds;
            refreshSecondsUntilNextPass = 0f;
        }

        private void LateUpdate()
        {
            if (owner == null)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            float deltaTime = Time.deltaTime;
            if (!hasStarted)
            {
                hasStarted = true;
                deltaTime = 0f;
            }

            if (refreshSecondsUntilNextPass <= 0f)
            {
                refreshSecondsUntilNextPass = TextureReplacementSurface.LateTextureRefreshIntervalSeconds;
                owner.ApplyLateTextureRefresh();
            }
            else
            {
                refreshSecondsUntilNextPass -= deltaTime;
            }

            refreshSecondsRemaining -= deltaTime;
            if (refreshSecondsRemaining <= 0f)
            {
                owner.LogUnmatchedTextures();
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
