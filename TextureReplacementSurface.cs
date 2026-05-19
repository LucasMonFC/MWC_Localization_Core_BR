using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HutongGames.PlayMaker.Actions;
using Ionic.Zip;
using MSCLoader;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Replaces Unity material textures with PNG files placed in the language pack.
    /// A file named "foo.png" replaces material textures whose Unity object name is "foo".
    /// </summary>
    public sealed class TextureReplacementSurface : ITranslationSurface
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string GameSceneName = "GAME";
        private const string DriversLicenceTextureName = "drivers_lincence";
        private const string ScreenOverlayTypeName = "UnityStandardAssets.ImageEffects.ScreenOverlay";
        private const string ScreenOverlayTextureFieldName = "texture";
        private const string RallyRegistrationObjectPath = "RallyRegistration";
        private const string RallyRegistrationFsmName = "Setup";
        private const string RallyRegistrationStateName = "Init";

        private static readonly string[] TexturePropertyNames = new string[]
        {
            "_MainTex",
            "_EmissionMap",
            "_BumpMap",
            "_MetallicGlossMap",
            "_OcclusionMap",
            "_SpecGlossMap",
            "_DetailMask",
            "_DetailAlbedoMap",
            "_DetailNormalMap",
        };

        private static readonly string[] IgnoredShaderPrefixes = new string[]
        {
            "Hidden",
            "Particles",
        };

        private sealed class DynamicFsmTextureTarget
        {
            public readonly string ObjectPath;
            public readonly string FsmName;
            public readonly string StateName;

            public DynamicFsmTextureTarget(string objectPath, string fsmName, string stateName)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                StateName = stateName;
            }
        }

        private sealed class DynamicObjectTextureTarget
        {
            public readonly string ObjectPath;
            public readonly string FsmName;
            public readonly string StateName;
            public readonly string TextureKeySuffix;

            public DynamicObjectTextureTarget(string objectPath, string fsmName, string stateName, string textureKeySuffix)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                StateName = stateName;
                TextureKeySuffix = textureKeySuffix;
            }
        }

        private static readonly DynamicFsmTextureTarget[] DynamicFsmTextureTargets = new DynamicFsmTextureTarget[]
        {
            new DynamicFsmTextureTarget("REPAIRSHOP/LOD/dyno/dyno_computer/Computer", "Use", "State 1"),
            new DynamicFsmTextureTarget("Rami-Pokeri", "Game Start", "State 1"),
            new DynamicFsmTextureTarget("Pokeri-MainMenu", "Menu", "State 1"),
            new DynamicFsmTextureTarget("Pokeri-Game", "Dealer Setup", "Play Game Over Sound"),
            new DynamicFsmTextureTarget("Pokeri-Game", "Check Three of a Kind", "THREE OF A KIND"),
            new DynamicFsmTextureTarget("Pokeri-Game", "Check Jacks or Better", "JACKS OR BETTER"),
            new DynamicFsmTextureTarget("Pokeri-Game", "Check Two Pairs", "TWO PAIRS"),
            new DynamicFsmTextureTarget("Pokeri-Game", "Check Jacks or Better", "Double - Wait for Button"),
        };

        private static readonly DynamicObjectTextureTarget[] DynamicObjectTextureTargets = new DynamicObjectTextureTarget[]
        {
            new DynamicObjectTextureTarget("Pokeri-Game", "Dealer Setup", "Bet 1", null),
            new DynamicObjectTextureTarget("Pokeri-Game", "Dealer Setup", "Bet 2", null),
            new DynamicObjectTextureTarget("Pokeri-Game", "Dealer Setup", "Bet 3", null),
            new DynamicObjectTextureTarget("Pokeri-Game", "Dealer Setup", "Bet 4", null),
            new DynamicObjectTextureTarget("Pokeri-Game", "Dealer Setup", "Bet 5", null),
            new DynamicObjectTextureTarget(RallyRegistrationObjectPath, RallyRegistrationFsmName, RallyRegistrationStateName, "card"),
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

        private readonly Dictionary<Material, List<MaterialTextureBackup>> originalTextures =
            new Dictionary<Material, List<MaterialTextureBackup>>();

        private readonly Dictionary<MonoBehaviour, OverlayTextureBackup> originalOverlayTextures =
            new Dictionary<MonoBehaviour, OverlayTextureBackup>();

        private readonly HashSet<string> sceneTextureKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> matchedTextureNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string[] cachedReplacementKeys = new string[0];
        private string assetsFolder;
        private string loadedSceneName;
        private bool hasApplied;
        private bool hasLoadedReplacementTextures;

        private readonly HashSet<string> hookedObjectTargets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string Name { get { return "TextureReplacementSurface"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
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
            int applied = ApplyMaterialTextures();
            applied += ApplyScreenOverlayTextures();
            applied += InstallDynamicTextureHooks(sceneName);
            hasApplied = true;
            LogUnmatchedTextures();
            return applied;
        }

        public int MonitorTick(float deltaTime)
        {
            return 0;
        }

        private int InstallDynamicTextureHooks(string sceneName)
        {
            if (sceneName != GameSceneName || replacementTextures.Count == 0 || sceneTextureKeys.Count == 0)
                return 0;

            int installed = 0;

            for (int i = 0; i < DynamicFsmTextureTargets.Length; i++)
                installed += InstallFsmTextureHook(DynamicFsmTextureTargets[i]);

            for (int i = 0; i < DynamicObjectTextureTargets.Length; i++)
                installed += InstallObjectTextureHook(DynamicObjectTextureTargets[i]);

            return installed;
        }

        private int InstallFsmTextureHook(DynamicFsmTextureTarget target)
        {
            GameObject go;
            PlayMakerFSM fsm;
            HutongGames.PlayMaker.FsmState state;
            if (!TryFindFsmState(target.ObjectPath, target.FsmName, target.StateName, out go, out fsm, out state))
                return 0;

            int injected = AppendFsmTextureHookAction(fsm, state, target);
            if (injected > 0)
                MarkStateTextureMatches(state);

            return injected;
        }

        private int InstallObjectTextureHook(DynamicObjectTextureTarget target)
        {
            GameObject go;
            PlayMakerFSM fsm;
            HutongGames.PlayMaker.FsmState state;
            if (!TryFindFsmState(target.ObjectPath, target.FsmName, target.StateName, out go, out fsm, out state))
                return 0;

            int injected = AppendObjectTextureAction(fsm, state, go, target);
            if (injected > 0)
            {
                ReplaceTexturesOnObject(go, target.TextureKeySuffix);
            }

            return injected;
        }

        private int AppendFsmTextureHookAction(
            PlayMakerFSM fsm,
            HutongGames.PlayMaker.FsmState state,
            DynamicFsmTextureTarget target)
        {
            if (HasAction<ReplaceSetMaterialTexturesInStateAction>(state))
                return 0;

            string label = target.ObjectPath + "|" + target.FsmName + "|" + target.StateName;
            ReplaceSetMaterialTexturesInStateAction action = new ReplaceSetMaterialTexturesInStateAction
            {
                owner = this,
                target = fsm.gameObject,
                fsmName = target.FsmName,
                stateName = target.StateName,
                label = label,
            };

            return MSCLoader.PlayMakerExtensions.FsmInject(fsm, target.StateName, action, 0, false) ? 1 : 0;
        }

        private int AppendObjectTextureAction(
            PlayMakerFSM fsm,
            HutongGames.PlayMaker.FsmState state,
            GameObject targetObject,
            DynamicObjectTextureTarget target)
        {
            string label = target.ObjectPath + "|" + target.FsmName + "|" + target.StateName + "|" + target.TextureKeySuffix;
            if (hookedObjectTargets.Contains(label))
                return 0;

            bool injected = MSCLoader.PlayMakerExtensions.FsmInject(
                targetObject,
                target.FsmName,
                target.StateName,
                (Action)delegate
                {
                    ReplaceTexturesOnObject(targetObject, target.TextureKeySuffix);
                },
                false,
                -1,
                false);

            if (injected)
            {
                hookedObjectTargets.Add(label);
                return 1;
            }

            return 0;
        }

        private void MarkStateTextureMatches(HutongGames.PlayMaker.FsmState state)
        {
            HutongGames.PlayMaker.FsmStateAction[] actions;
            if (!TryGetStateActions(state, out actions))
                return;

            for (int i = 0; i < actions.Length; i++)
            {
                SetMaterialTexture setTexture = actions[i] as SetMaterialTexture;
                if (setTexture == null || setTexture.texture == null || IsUnityObjectNull(setTexture.texture.Value))
                    continue;

                Texture2D replacement;
                string matchedName;
                if (TryGetReplacementTexture(setTexture.texture.Value.name, out replacement, out matchedName))
                    matchedTextureNames.Add(matchedName);
            }
        }

        private int ReplaceSetMaterialTexturesInState(GameObject targetObject, string fsmName, string stateName, string label)
        {
            if (IsUnityObjectNull(targetObject))
                return 0;

            try
            {
                PlayMakerFSM fsm;
                HutongGames.PlayMaker.FsmState state;
                if (!TryFindFsmState(targetObject, fsmName, stateName, out fsm, out state))
                    return 0;

                HutongGames.PlayMaker.FsmStateAction[] actions;
                if (!TryGetStateActions(state, out actions))
                    return 0;

                int applied = 0;
                for (int i = 0; i < actions.Length; i++)
                {
                    SetMaterialTexture setTexture = actions[i] as SetMaterialTexture;
                    if (setTexture == null || setTexture.texture == null || IsUnityObjectNull(setTexture.texture.Value))
                        continue;

                    Texture2D replacement;
                    string matchedName;
                    if (!TryGetReplacementTexture(setTexture.texture.Value.name, out replacement, out matchedName)
                        || IsUnityObjectNull(replacement))
                        continue;

                    CopyTextureSettings(setTexture.texture.Value, replacement);
                    setTexture.texture.Value = replacement;
                    matchedTextureNames.Add(matchedName);
                    applied++;
                }

                return applied;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[{Name}] Dynamic FSM texture hook failed ({label}): {ex.Message}");
                return 0;
            }
        }

        private int ReplaceTexturesOnObject(GameObject obj, string textureKeySuffix)
        {
            if (IsUnityObjectNull(obj))
                return 0;

            int applied = 0;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (!IsUnityObjectNull(renderer) && !IsUnityObjectNull(renderer.sharedMaterial))
                applied += ApplyMaterialProperty(renderer.sharedMaterial, "_MainTex", textureKeySuffix);

            Renderer[] children = obj.GetComponentsInChildren<Renderer>(true);
            if (children == null)
                return applied;

            for (int i = 0; i < children.Length; i++)
            {
                Renderer child = children[i];
                if (IsUnityObjectNull(child) || child == renderer || IsUnityObjectNull(child.sharedMaterial))
                    continue;

                applied += ApplyMaterialProperty(child.sharedMaterial, "_MainTex", textureKeySuffix);
            }

            return applied;
        }

        private static bool HasAction<T>(HutongGames.PlayMaker.FsmState state) where T : HutongGames.PlayMaker.FsmStateAction
        {
            HutongGames.PlayMaker.FsmStateAction[] actions;
            if (!TryGetStateActions(state, out actions))
                return false;

            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] is T)
                    return true;
            }

            return false;
        }

        private static bool TryGetStateActions(
            HutongGames.PlayMaker.FsmState state,
            out HutongGames.PlayMaker.FsmStateAction[] actions)
        {
            actions = null;
            if (state == null)
                return false;

            try
            {
                actions = state.Actions;
                return actions != null;
            }
            catch
            {
                return false;
            }
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

        private static bool TryFindFsmState(
            string objectPath,
            string fsmName,
            string stateName,
            out GameObject go,
            out PlayMakerFSM fsm,
            out HutongGames.PlayMaker.FsmState state)
        {
            go = FindDynamicGameObject(objectPath);
            if (IsUnityObjectNull(go))
            {
                fsm = null;
                state = null;
                return false;
            }

            return TryFindFsmState(go, fsmName, stateName, out fsm, out state);
        }

        private static bool TryFindFsmState(
            GameObject go,
            string fsmName,
            string stateName,
            out PlayMakerFSM fsm,
            out HutongGames.PlayMaker.FsmState state)
        {
            fsm = FindFsmByName(go, fsmName);
            if (IsUnityObjectNull(fsm) || fsm.FsmStates == null)
            {
                state = null;
                return false;
            }

            state = FindState(fsm, stateName);
            return state != null;
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

        private static GameObject FindDynamicGameObject(string path)
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

        public void Reset()
        {
            ResetRuntimeState();
        }

        private void ResetRuntimeState()
        {
            hasApplied = false;
            loadedSceneName = null;
            sceneTextureKeys.Clear();
            matchedTextureNames.Clear();
            hasLoadedReplacementTextures = false;
            hookedObjectTargets.Clear();
        }

        public void ClearTranslations()
        {
            RestoreOriginalTextures();
            DestroyReplacementTextures();
            replacementTextures.Clear();
            cachedReplacementKeys = new string[0];
            ResetRuntimeState();
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
                string textureKey = source.TextureKey;
                if (string.IsNullOrEmpty(textureKey))
                    continue;

                sceneTextureKeys.Add(textureKey);

                if (replacementTextures.ContainsKey(textureKey))
                    continue;

                Texture2D texture = LoadPng(source.Bytes, textureKey, source.DisplayName);
                if (IsUnityObjectNull(texture))
                    continue;

                replacementTextures.Add(textureKey, texture);
            }

            UpdateReplacementCache();

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
                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
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

        private int ApplyMaterialTextures()
        {
            if (replacementTextures.Count == 0 || sceneTextureKeys.Count == 0)
                return 0;

            Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
            if (materials == null || materials.Length == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (ShouldSkipMaterial(material))
                    continue;

                for (int j = 0; j < TexturePropertyNames.Length; j++)
                    applied += ApplyMaterialProperty(material, TexturePropertyNames[j]);
            }

            return applied;
        }

        private int ApplyMaterialProperty(Material material, string propertyName, string textureKeySuffix = null)
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
                if (!TryGetRallyCoverReplacement(material, propertyName, currentTexture, textureKeySuffix, out replacement, out matchedName)
                    && !TryGetReplacementTexture(currentTexture.name, textureKeySuffix, out replacement, out matchedName)
                    || IsUnityObjectNull(replacement))
                {
                    return 0;
                }

                if (ReferenceEquals(currentTexture, replacement))
                {
                    matchedTextureNames.Add(matchedName);
                    return 0;
                }

                BackupOriginalTexture(material, propertyName, currentTexture);
                CopyTextureSettings(currentTexture, replacement);
                material.SetTexture(propertyName, replacement);
                matchedTextureNames.Add(matchedName);
                return 1;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[{Name}] Skipped material '{material.name}' during texture replacement: {ex.Message}");
                return 0;
            }
        }

        private int ApplyScreenOverlayTextures()
        {
            if (replacementTextures.Count == 0 || sceneTextureKeys.Count == 0)
                return 0;

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            if (behaviours == null || behaviours.Length == 0)
                return 0;

            int applied = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (IsUnityObjectNull(behaviour) || IsUnityObjectNull(behaviour.gameObject))
                    continue;

                try
                {
                    Type type = behaviour.GetType();
                    if (type == null || type.FullName != ScreenOverlayTypeName)
                        continue;

                    FieldInfo textureField = type.GetField(
                        ScreenOverlayTextureFieldName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (textureField == null)
                        continue;

                    Texture currentTexture = textureField.GetValue(behaviour) as Texture;
                    if (IsUnityObjectNull(currentTexture))
                        continue;

                    Texture2D replacement;
                    string matchedName;
                    if (!TryGetReplacementTexture(currentTexture.name, out replacement, out matchedName)
                        || IsUnityObjectNull(replacement))
                    {
                        continue;
                    }

                    if (ReferenceEquals(currentTexture, replacement))
                    {
                        matchedTextureNames.Add(matchedName);
                        continue;
                    }

                    BackupOriginalOverlayTexture(behaviour, textureField, currentTexture);
                    CopyTextureSettings(currentTexture, replacement);
                    textureField.SetValue(behaviour, replacement);
                    matchedTextureNames.Add(matchedName);
                    applied++;
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[{Name}] Skipped ScreenOverlay during texture replacement: {ex.Message}");
                }
            }

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

            string normalizedName = NormalizeTextureName(sourceTextureName);
            if (string.IsNullOrEmpty(normalizedName))
                return false;

            if (!string.IsNullOrEmpty(textureKeySuffix))
                normalizedName += textureKeySuffix;

            if (replacementTextures.TryGetValue(normalizedName, out replacement))
            {
                matchedName = normalizedName;
                return true;
            }

            if (!string.IsNullOrEmpty(textureKeySuffix))
            {
                return false;
            }

            for (int i = 0; i < cachedReplacementKeys.Length; i++)
            {
                string key = cachedReplacementKeys[i];
                if (StartsWithTextureKey(normalizedName, key)
                    && replacementTextures.TryGetValue(key, out replacement))
                {
                    matchedName = key;
                    return true;
                }
            }

            return false;
        }

        private void UpdateReplacementCache()
        {
            if (replacementTextures.Count == 0)
            {
                cachedReplacementKeys = new string[0];
                return;
            }

            List<string> keys = new List<string>(replacementTextures.Keys);
            keys.Sort((a, b) => b.Length.CompareTo(a.Length));
            cachedReplacementKeys = keys.ToArray();
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
                    ModConsole.Print($"[MWC_LC] [{Name}] Loaded texture ZIP '{Path.GetFileName(zipFile)}' with {totalPngCount} PNG texture replacement(s)");
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

        private static string NormalizeTextureName(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return textureName;

            return textureName.Replace(" (Instance)", string.Empty).Trim();
        }

        private static bool StartsWithTextureKey(string textureName, string key)
        {
            if (string.IsNullOrEmpty(textureName) || string.IsNullOrEmpty(key))
                return false;
            if (!textureName.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return false;
            if (textureName.Length == key.Length)
                return true;

            char next = textureName[key.Length];
            return char.IsWhiteSpace(next) || next == '_' || next == '-' || next == '(';
        }

        private static bool IsMenuOnlyTexture(string textureName)
        {
            return string.Equals(textureName, DriversLicenceTextureName, StringComparison.OrdinalIgnoreCase);
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

        private bool TryGetRallyCoverReplacement(
            Material material,
            string propertyName,
            Texture currentTexture,
            string textureKeySuffix,
            out Texture2D replacement,
            out string matchedName)
        {
            replacement = null;
            matchedName = null;

            if (!string.IsNullOrEmpty(textureKeySuffix))
                return false;
            if (propertyName != "_MainTex")
                return false;
            if (IsUnityObjectNull(material) || IsUnityObjectNull(currentTexture))
                return false;

            if (NormalizeTextureName(material.name) != "cover 1"
                || NormalizeTextureName(currentTexture.name) != "rally_register")
                return false;

            if (!replacementTextures.TryGetValue("rally_registercard", out replacement))
                return false;

            matchedName = "rally_registercard";
            return true;
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
            if (IsUnityObjectNull(behaviour) || textureField == null || IsUnityObjectNull(originalTexture))
                return;

            if (!originalOverlayTextures.ContainsKey(behaviour))
                originalOverlayTextures.Add(behaviour, new OverlayTextureBackup(textureField, originalTexture));
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

        private void RestoreOriginalTextures()
        {
            foreach (KeyValuePair<Material, List<MaterialTextureBackup>> pair in originalTextures)
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
                        CoreConsole.Warning($"[{Name}] Skipped restoring texture on '{material.name}': {ex.Message}");
                    }
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

        private void LogUnmatchedTextures()
        {
            foreach (string textureName in sceneTextureKeys)
            {
                if (matchedTextureNames.Contains(textureName))
                    continue;

                CoreConsole.Warning($"[{Name}] PNG did not match any loaded texture in {loadedSceneName}: {textureName}.png");
            }
        }

        public class ReplaceSetMaterialTexturesInStateAction : HutongGames.PlayMaker.FsmStateAction
        {
            public TextureReplacementSurface owner;
            public GameObject target;
            public string fsmName;
            public string stateName;
            public string label;

            public override void OnEnter()
            {
                try
                {
                    if (owner != null)
                        owner.ReplaceSetMaterialTexturesInState(target, fsmName, stateName, label);
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning($"[TextureReplacementSurface] Dynamic SetMaterialTexture action failed ({label}): {ex.Message}");
                }

                Finish();
            }
        }

        private static bool IsUnityObjectNull(UnityEngine.Object obj)
        {
            return ReferenceEquals(obj, null) || obj == null;
        }
    }
}
