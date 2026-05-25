using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Translates text owned by optional mods that write directly to TextMesh or
    /// Unity UI Text instead of using the vanilla PlayMaker GUI FSMs.
    /// </summary>
    public sealed class ModTextTranslator : ITranslationSurface
    {
        private const int MaxAttachAttempts = 15;
        private const string DirtTrackAssemblyName = "DirtTrackRacing";
        private const string DirtTrackRootPath = "DIRTTRACK_UI";
        private const string MscModApiAssemblyName = "MscModApi";
        private const string MscModApiShopTypeName = "MscModApi.Shopping.Shop";
        private const string SatsumaTurboChargerAssemblyName = "SatsumaTurboCharger";
        private const string ModsShopAssemblyName = "ModsShop";
        private const string ModsShopCartComponentName = "ModsShop.ShoppingCartUI";
        private const string DeliveryJobsAssemblyName = "DeliveryJobs";
        private const string DeliveryJobsJobMapComponentName = "DeliveryJobs.JobMap";
        private const string DeliveryJobsPackageComponentName = "DeliveryJobs.DeliveryPackage";
        private const string DeliveryJobsDestinationSignComponentName = "DeliveryJobs.DestinationSign";
        private const string DeliveryJobsCanvasPath = "DeliveryJobs Canvas";
        private const string DeliveryJobsAccentFontName = "Alphabetized";
        private const string DeliveryJobsDestinationSignKeyPrefix = "DeliveryJobs|DestinationSign|";

        private sealed class TextMeshGroup
        {
            public readonly string AssemblyName;
            public readonly string SceneName;
            public readonly string[] Paths;

            public TextMeshGroup(string assemblyName, string sceneName, params string[] paths)
            {
                AssemblyName = assemblyName;
                SceneName = sceneName;
                Paths = paths;
            }
        }

        private static readonly TextMeshGroup[] TextMeshGroups = new TextMeshGroup[]
        {
            new TextMeshGroup(
                "LetItRust",
                "GAME",
                "GUI/HUD/FleetariDialoguePrompt",
                "GUI/HUD/FleetariDialoguePrompt/HUDLabelShadow",
                "GUI/HUD/VenttiDialoguePrompt",
                "GUI/HUD/VenttiDialoguePrompt/HUDLabelShadow"),

            new TextMeshGroup(
                "FishingMod",
                "GAME",
                "selltext",
                "comptext"),

            new TextMeshGroup(
                "BetterMSC",
                "GAME",
                "GUI/Indicators/AnswerHint",
                "GUI/Indicators/AnswerHint/Shadow",
                "GUI/Indicators/AnswerHint/HUDLabelShadow"),

            new TextMeshGroup(
                "CDPlayer",
                "MainMenu",
                "Interface/Songs/LoadingCD"),
        };

        private readonly Dictionary<string, DirectTextMeshTranslator> attachedTextMeshComponents = new Dictionary<string, DirectTextMeshTranslator>();
        private TranslationDictionary translations;
        private Dictionary<string, Font> customFonts;
        private TextMeshTranslator textMeshTranslator;
        private int activeTextMeshTargetCount;
        private bool dirtTrackAssemblyLoaded;
        private bool mscModApiShopAssemblyLoaded;
        private bool modsShopAssemblyLoaded;
        private bool deliveryJobsAssemblyLoaded;
        private int textMeshAttachAttempts;
        private int dirtTrackAttachAttempts;
        private int mscModApiShopAttachAttempts;
        private int modsShopAttachAttempts;
        private int deliveryJobsAttachAttempts;
        private int deliveryJobsSignAttachAttempts;
        private int deliveryJobsPackagePrefabHookAttempts;
        private bool dirtTrackAttached;
        private bool mscModApiShopAttached;
        private bool modsShopAttached;
        private bool deliveryJobsAttached;
        private bool deliveryJobsUiAttached;
        private bool deliveryJobsSignTextAttached;
        private bool deliveryJobsPackagePrefabHooked;
        private bool hasSupportedModAssemblyLoaded;
        private readonly Dictionary<int, float> deliveryJobsPackageInfoBaseCharacterSizes = new Dictionary<int, float>();

        public string Name { get { return "ModTextTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete
        {
            get
            {
                if (!hasSupportedModAssemblyLoaded)
                    return true;

                return IsCurrentlyComplete();
            }
        }

        public void Initialize(TranslationContext ctx)
        {
            translations = ctx.Translations;
            customFonts = ctx.CustomFonts;
            textMeshTranslator = ctx.Translator;
            Reset();
            RefreshActiveModState(Application.loadedLevelName);
        }

        public int InitialPass()
        {
            return TryAttach(true);
        }

        public int MonitorTick(float deltaTime)
        {
            return TryAttach(false);
        }

        public void Reset()
        {
            attachedTextMeshComponents.Clear();
            activeTextMeshTargetCount = 0;
            dirtTrackAssemblyLoaded = false;
            mscModApiShopAssemblyLoaded = false;
            modsShopAssemblyLoaded = false;
            deliveryJobsAssemblyLoaded = false;
            textMeshAttachAttempts = 0;
            dirtTrackAttachAttempts = 0;
            mscModApiShopAttachAttempts = 0;
            modsShopAttachAttempts = 0;
            deliveryJobsAttachAttempts = 0;
            deliveryJobsSignAttachAttempts = 0;
            deliveryJobsPackagePrefabHookAttempts = 0;
            dirtTrackAttached = false;
            mscModApiShopAttached = false;
            modsShopAttached = false;
            deliveryJobsAttached = false;
            deliveryJobsUiAttached = false;
            deliveryJobsSignTextAttached = false;
            deliveryJobsPackagePrefabHooked = false;
            hasSupportedModAssemblyLoaded = false;
            deliveryJobsPackageInfoBaseCharacterSizes.Clear();
        }

        public void ClearTranslations()
        {
            Reset();
        }

        private int TryAttach(bool includeDeliveryJobsPackageInfo)
        {
            string sceneName = Application.loadedLevelName;
            bool isGameScene = sceneName == "GAME";
            bool isMainMenu = sceneName == "MainMenu";
            if (!isGameScene && !isMainMenu)
                return 0;

            RefreshActiveModState(sceneName);

            if (!hasSupportedModAssemblyLoaded)
                return 0;

            int attached = 0;
            attached += TryAttachTextMeshes(sceneName);
            if (isGameScene)
            {
                attached += TryAttachDirtTrackUi();
                attached += TryAttachMscModApiShopUi();
                attached += TryAttachModsShopUi();
                attached += TryAttachDeliveryJobsUi(includeDeliveryJobsPackageInfo);
            }
            return attached;
        }

        private int TryAttachTextMeshes(string sceneName)
        {
            if (textMeshTranslator == null || activeTextMeshTargetCount == 0 || textMeshAttachAttempts >= MaxAttachAttempts)
                return 0;

            textMeshAttachAttempts++;
            int attached = 0;
            for (int i = 0; i < TextMeshGroups.Length; i++)
            {
                TextMeshGroup group = TextMeshGroups[i];
                if (group.SceneName != sceneName)
                    continue;
                if (!IsAssemblyLoaded(group.AssemblyName))
                    continue;

                for (int j = 0; j < group.Paths.Length; j++)
                {
                    if (TryAttachTextMeshPath(group.AssemblyName, group.Paths[j]))
                        attached++;
                }
            }

            return attached;
        }

        private bool TryAttachTextMeshPath(string assemblyName, string path)
        {
            string key = assemblyName + "|" + path;
            DirectTextMeshTranslator existing;
            if (attachedTextMeshComponents.TryGetValue(key, out existing))
            {
                if (existing != null)
                    return false;

                attachedTextMeshComponents.Remove(key);
            }

            GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(path);
            if (go == null)
                return false;

            TextMesh textMesh = go.GetComponent<TextMesh>();
            if (textMesh == null)
                return false;

            DirectTextMeshTranslator component = go.GetComponent<DirectTextMeshTranslator>();
            if (component == null)
                component = go.AddComponent<DirectTextMeshTranslator>();

            component.Configure(textMesh, path, textMeshTranslator);
            component.TranslateNow();
            attachedTextMeshComponents[key] = component;
            return true;
        }

        private int TryAttachDirtTrackUi()
        {
            if (translations == null
                || dirtTrackAttachAttempts >= MaxAttachAttempts
                || !dirtTrackAssemblyLoaded)
            {
                return 0;
            }

            dirtTrackAttachAttempts++;

            GameObject root = LocalizationUtils.FindGameObjectIncludingInactive(DirtTrackRootPath);
            if (root == null)
                return 0;

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            if (texts == null || texts.Length == 0)
                return 0;

            int attached = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || text.gameObject == null)
                    continue;

                DirtTrackTextTranslator component = text.gameObject.GetComponent<DirtTrackTextTranslator>();
                if (component == null)
                {
                    component = text.gameObject.AddComponent<DirtTrackTextTranslator>();
                    attached++;
                }

                component.Configure(text, translations);
                component.TranslateNow();
            }

            dirtTrackAttached = true;
            return attached;
        }

        private int TryAttachMscModApiShopUi()
        {
            if (translations == null
                || mscModApiShopAttached
                || mscModApiShopAttachAttempts >= MaxAttachAttempts
                || !mscModApiShopAssemblyLoaded)
            {
                return 0;
            }

            mscModApiShopAttachAttempts++;

            object shopInterface = GetMscModApiShopInterface();
            if (shopInterface == null)
                return 0;

            Type shopInterfaceType = shopInterface.GetType();
            GameObject root = GetFieldValue<GameObject>(shopInterface, shopInterfaceType, "gameObject");
            if (root == null)
                return 0;

            int attached = 0;
            MscModApiShopTextTranslator component = root.GetComponent<MscModApiShopTextTranslator>();
            if (component == null)
            {
                component = root.AddComponent<MscModApiShopTextTranslator>();
                attached++;
            }

            component.Configure(shopInterface, translations);
            attached += component.AttachNow();
            mscModApiShopAttached = true;
            return attached;
        }

        private int TryAttachModsShopUi()
        {
            if (translations == null
                || modsShopAttached
                || modsShopAttachAttempts >= MaxAttachAttempts
                || !modsShopAssemblyLoaded)
            {
                return 0;
            }

            modsShopAttachAttempts++;

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            if (behaviours == null || behaviours.Length == 0)
                return 0;

            int attached = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.gameObject == null)
                    continue;

                Type behaviourType = behaviour.GetType();
                if (!IsComponentType(behaviourType, ModsShopCartComponentName))
                    continue;

                ModsShopCartTextTranslator component = behaviour.gameObject.GetComponent<ModsShopCartTextTranslator>();
                if (component == null)
                {
                    component = behaviour.gameObject.AddComponent<ModsShopCartTextTranslator>();
                    attached++;
                }

                component.Configure(behaviour, translations);
                attached += component.AttachNow();
                modsShopAttached = true;
            }

            return attached;
        }

        private int TryAttachDeliveryJobsUi(bool includePackageInfo)
        {
            if (translations == null || !deliveryJobsAssemblyLoaded)
                return 0;

            int attached = 0;
            attached += TryAttachDeliveryJobsCanvasUi();
            attached += TryAttachDeliveryJobsSignTextMeshes();
            attached += TryHookDeliveryJobsPackagePrefab();
            if (includePackageInfo)
                attached += TranslateDeliveryJobsPackageInfoText();
            deliveryJobsAttached = deliveryJobsUiAttached && deliveryJobsSignTextAttached && deliveryJobsPackagePrefabHooked;
            return attached;
        }

        private int TryAttachDeliveryJobsCanvasUi()
        {
            if (translations == null
                || deliveryJobsUiAttached
                || deliveryJobsAttachAttempts >= MaxAttachAttempts
                || !deliveryJobsAssemblyLoaded)
            {
                return 0;
            }

            deliveryJobsAttachAttempts++;

            GameObject root = FindGameObjectByNameIncludingInactive(DeliveryJobsCanvasPath);
            if (root == null)
                return 0;

            int attached = AttachUiTextTranslators(root, translations);

            GameObject advertUi = FindDeliveryJobsAdvertUi();
            if (advertUi != null)
                attached += AttachUiTextTranslators(advertUi, translations, GetCustomFontByName(DeliveryJobsAccentFontName), true);

            deliveryJobsUiAttached = attached > 0 && advertUi != null;
            return attached;
        }

        private int TryAttachDeliveryJobsSignTextMeshes()
        {
            if (textMeshTranslator == null
                || deliveryJobsSignTextAttached
                || deliveryJobsSignAttachAttempts >= MaxAttachAttempts
                || !deliveryJobsAssemblyLoaded)
            {
                return 0;
            }

            deliveryJobsSignAttachAttempts++;

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            if (behaviours == null || behaviours.Length == 0)
                return 0;

            int attached = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.gameObject == null)
                    continue;

                Type behaviourType = behaviour.GetType();
                if (!IsComponentType(behaviourType, DeliveryJobsDestinationSignComponentName))
                    continue;

                Transform transform = behaviour.transform;
                if (transform == null || transform.childCount == 0)
                    continue;

                TextMesh textMesh = transform.GetChild(0).GetComponent<TextMesh>();
                if (textMesh == null || textMesh.gameObject == null)
                    continue;

                string key = DeliveryJobsDestinationSignKeyPrefix + textMesh.GetInstanceID();
                DirectTextMeshTranslator existing;
                if (attachedTextMeshComponents.TryGetValue(key, out existing))
                {
                    if (existing != null)
                        continue;

                    attachedTextMeshComponents.Remove(key);
                }

                DirectTextMeshTranslator component = textMesh.gameObject.GetComponent<DirectTextMeshTranslator>();
                if (component == null)
                {
                    component = textMesh.gameObject.AddComponent<DirectTextMeshTranslator>();
                    attached++;
                }

                component.Configure(textMesh, LocalizationUtils.GetGameObjectPath(textMesh.gameObject), textMeshTranslator);
                component.TranslateNow();
                attachedTextMeshComponents[key] = component;
            }

            if (attachedTextMeshComponents.Count > 0)
                deliveryJobsSignTextAttached = HasAttachedDeliveryJobsSignText();

            return attached;
        }

        private int TryHookDeliveryJobsPackagePrefab()
        {
            if (translations == null
                || deliveryJobsPackagePrefabHooked
                || deliveryJobsPackagePrefabHookAttempts >= MaxAttachAttempts
                || !deliveryJobsAssemblyLoaded)
            {
                return 0;
            }

            deliveryJobsPackagePrefabHookAttempts++;

            Type storageType = FindLoadedType(DeliveryJobsAssemblyName, DeliveryJobsAssemblyName + ".Storage");
            if (storageType == null)
                return 0;

            FieldInfo boxesSrcField = storageType.GetField("boxesSrc", BindingFlags.Public | BindingFlags.Static);
            if (boxesSrcField == null)
                return 0;

            GameObject boxesSrc = boxesSrcField.GetValue(null) as GameObject;
            if (boxesSrc == null || boxesSrc.transform == null)
                return 0;

            DeliveryJobsPackageInfoTranslator.Configure(translations);

            int attached = 0;
            for (int i = 0; i < boxesSrc.transform.childCount; i++)
            {
                Transform child = boxesSrc.transform.GetChild(i);
                if (child == null || child.gameObject == null)
                    continue;

                DeliveryJobsPackageInfoTranslator component =
                    child.gameObject.GetComponent<DeliveryJobsPackageInfoTranslator>();
                if (component != null)
                    continue;

                child.gameObject.AddComponent<DeliveryJobsPackageInfoTranslator>();
                attached++;
            }

            if (attached > 0)
                deliveryJobsPackagePrefabHooked = true;

            return attached;
        }

        private int TranslateDeliveryJobsPackageInfoText()
        {
            if (translations == null
                || !deliveryJobsAssemblyLoaded)
            {
                return 0;
            }

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            if (behaviours == null || behaviours.Length == 0)
                return 0;

            int packageCount = 0;
            int translatedCount = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.gameObject == null)
                    continue;

                Type behaviourType = behaviour.GetType();
                if (!IsComponentType(behaviourType, DeliveryJobsPackageComponentName))
                    continue;

                packageCount++;
                TextMesh additionalInfo = GetFieldValue<TextMesh>(behaviour, behaviourType, "additionalInfo");
                if (additionalInfo == null || additionalInfo.gameObject == null)
                    continue;

                if (!ShouldTranslateDeliveryJobsPackageInfoText(additionalInfo))
                {
                    AdjustDeliveryJobsPackageInfoLayout(additionalInfo, behaviour.transform);
                    continue;
                }

                if (TranslateDeliveryJobsPackageInfoText(additionalInfo))
                {
                    AdjustDeliveryJobsPackageInfoLayout(additionalInfo, behaviour.transform);
                    translatedCount++;
                }
            }

            return translatedCount;
        }

        private static bool ShouldTranslateDeliveryJobsPackageInfoText(TextMesh textMesh)
        {
            if (textMesh == null || textMesh.gameObject == null)
                return false;

            string current = textMesh.text;
            if (string.IsNullOrEmpty(current) || current == "----")
                return false;

            return current.IndexOf("Mailbox", StringComparison.OrdinalIgnoreCase) >= 0
                || current.IndexOf("Fragille", StringComparison.OrdinalIgnoreCase) >= 0
                || current.IndexOf("Fragile", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool TranslateDeliveryJobsPackageInfoText(TextMesh textMesh)
        {
            return TranslateDeliveryJobsPackageInfoText(textMesh, translations);
        }

        private static bool TranslateDeliveryJobsPackageInfoText(TextMesh textMesh, TranslationDictionary translations)
        {
            if (translations == null)
                return false;

            string current = textMesh.text ?? string.Empty;
            string translated = TranslateDeliveryJobsPackageInfoLines(
                current,
                LocalizationUtils.GetGameObjectPath(textMesh.gameObject),
                translations);
            if (translated == current)
                return false;

            textMesh.text = translated;
            return true;
        }

        private void AdjustDeliveryJobsPackageInfoLayout(TextMesh textMesh, Transform packageTransform)
        {
            if (textMesh == null)
                return;

            int instanceId = textMesh.GetInstanceID();
            float baseCharacterSize;
            if (!deliveryJobsPackageInfoBaseCharacterSizes.TryGetValue(instanceId, out baseCharacterSize))
            {
                baseCharacterSize = textMesh.characterSize;
                deliveryJobsPackageInfoBaseCharacterSizes[instanceId] = baseCharacterSize;
            }

            if (packageTransform != null && packageTransform.parent != null)
            {
                textMesh.characterSize = baseCharacterSize;
                return;
            }

            int longestLineLength = GetLongestPlainLineLength(textMesh.text);
            float scale = 1f;
            if (longestLineLength > 18)
                scale = Mathf.Max(0.62f, 18f / longestLineLength);

            textMesh.characterSize = baseCharacterSize * scale;
        }

        private static int GetLongestPlainLineLength(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int longest = 0;
            int current = 0;
            bool insideTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    insideTag = true;
                    continue;
                }
                if (c == '>')
                {
                    insideTag = false;
                    continue;
                }
                if (insideTag || c == '\r')
                    continue;
                if (c == '\n')
                {
                    if (current > longest)
                        longest = current;
                    current = 0;
                    continue;
                }

                current++;
            }

            return current > longest ? current : longest;
        }

        private static string TranslateDeliveryJobsPackageInfoLines(
            string text,
            string path,
            TranslationDictionary translations)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string[] lines = text.Split('\n');
            bool changed = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace("\r", string.Empty);
                if (line.Length == 0)
                    continue;

                string translated;
                if (!translations.TryGetExact(line, out translated))
                    translated = translations.TryMatchPattern(line, path);

                if (translated == null || translated == line)
                    continue;

                lines[i] = translated;
                changed = true;
            }

            return changed ? string.Join("\n", lines) : text;
        }

        private bool IsCurrentlyComplete()
        {
            bool textMeshComplete = activeTextMeshTargetCount == 0
                || CountLiveAttachedTextMeshes() >= activeTextMeshTargetCount
                || textMeshAttachAttempts >= MaxAttachAttempts;

            bool dirtTrackComplete = !dirtTrackAssemblyLoaded
                || dirtTrackAttached
                || dirtTrackAttachAttempts >= MaxAttachAttempts;

            bool modsShopComplete = !modsShopAssemblyLoaded
                || modsShopAttached
                || modsShopAttachAttempts >= MaxAttachAttempts;

            bool mscModApiShopComplete = !mscModApiShopAssemblyLoaded
                || mscModApiShopAttached
                || mscModApiShopAttachAttempts >= MaxAttachAttempts;

            bool deliveryJobsUiComplete = deliveryJobsUiAttached
                || deliveryJobsAttachAttempts >= MaxAttachAttempts;

            bool deliveryJobsSignTextComplete = deliveryJobsSignTextAttached
                || deliveryJobsSignAttachAttempts >= MaxAttachAttempts;

            bool deliveryJobsPackagePrefabComplete = deliveryJobsPackagePrefabHooked
                || deliveryJobsPackagePrefabHookAttempts >= MaxAttachAttempts;

            bool deliveryJobsComplete = !deliveryJobsAssemblyLoaded
                || (deliveryJobsUiComplete && deliveryJobsSignTextComplete && deliveryJobsPackagePrefabComplete);

            return textMeshComplete && dirtTrackComplete && mscModApiShopComplete && modsShopComplete && deliveryJobsComplete;
        }

        private void RefreshActiveModState(string sceneName)
        {
            activeTextMeshTargetCount = CountActiveTextMeshTargets(sceneName);
            dirtTrackAssemblyLoaded = sceneName == "GAME" && IsAssemblyLoaded(DirtTrackAssemblyName);
            mscModApiShopAssemblyLoaded = sceneName == "GAME"
                && IsAssemblyLoaded(MscModApiAssemblyName)
                && IsAssemblyLoaded(SatsumaTurboChargerAssemblyName);
            modsShopAssemblyLoaded = sceneName == "GAME" && IsAssemblyLoaded(ModsShopAssemblyName);
            deliveryJobsAssemblyLoaded = sceneName == "GAME" && IsAssemblyLoaded(DeliveryJobsAssemblyName);
            hasSupportedModAssemblyLoaded = activeTextMeshTargetCount > 0
                || dirtTrackAssemblyLoaded
                || mscModApiShopAssemblyLoaded
                || modsShopAssemblyLoaded
                || deliveryJobsAssemblyLoaded;
        }

        private int CountLiveAttachedTextMeshes()
        {
            int count = 0;
            foreach (KeyValuePair<string, DirectTextMeshTranslator> pair in attachedTextMeshComponents)
            {
                if (pair.Value != null)
                    count++;
            }

            return count;
        }

        private static int CountActiveTextMeshTargets(string sceneName)
        {
            int count = 0;
            for (int i = 0; i < TextMeshGroups.Length; i++)
            {
                TextMeshGroup group = TextMeshGroups[i];
                if (group.SceneName != sceneName)
                    continue;
                if (IsAssemblyLoaded(group.AssemblyName))
                    count += group.Paths.Length;
            }

            return count;
        }

        private static bool IsAssemblyLoaded(string assemblyName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                    continue;

                AssemblyName name = assembly.GetName();
                if (name != null && string.Equals(name.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool HasAttachedDeliveryJobsSignText()
        {
            foreach (KeyValuePair<string, DirectTextMeshTranslator> pair in attachedTextMeshComponents)
            {
                if (pair.Key.StartsWith(DeliveryJobsDestinationSignKeyPrefix, StringComparison.Ordinal)
                    && pair.Value != null)
                {
                    return true;
                }
            }

            return false;
        }

        public sealed class DirectTextMeshTranslator : MonoBehaviour
        {
            private TextMesh target;
            private string path;
            private TextMeshTranslator translator;
            private string lastSourceText;
            private string lastTranslatedText;

            public void Configure(TextMesh target, string path, TextMeshTranslator translator)
            {
                this.target = target;
                this.path = path;
                this.translator = translator;
                lastSourceText = null;
                lastTranslatedText = null;
            }

            private void OnEnable()
            {
                TranslateNow();
            }

            private void LateUpdate()
            {
                TranslateNow();
            }

            public void TranslateNow()
            {
                try
                {
                    if (target == null || target.gameObject == null || translator == null)
                        return;

                    string current = target.text;
                    if (string.IsNullOrEmpty(current))
                    {
                        lastSourceText = current;
                        lastTranslatedText = current;
                        return;
                    }

                    if (current == lastTranslatedText)
                        return;

                    if (current == lastSourceText && lastTranslatedText != null)
                    {
                        target.text = lastTranslatedText;
                        return;
                    }

                    string source = current;
                    translator.TranslateAndApplyFont(target, path);
                    translator.ApplyCustomFont(target, path);
                    lastSourceText = source;
                    lastTranslatedText = target.text;
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning("[ModTextTranslator] Falha ao traduzir " + path + ": " + ex.Message);
                }
            }
        }

        public sealed class DeliveryJobsPackageInfoTranslator : MonoBehaviour
        {
            private static TranslationDictionary translations;

            public static void Configure(TranslationDictionary activeTranslations)
            {
                translations = activeTranslations;
            }

            private IEnumerator Start()
            {
                yield return null;
                TranslateNow();
            }

            private void TranslateNow()
            {
                try
                {
                    if (translations == null)
                        return;

                    TextMesh additionalInfo = ResolveAdditionalInfo();
                    if (additionalInfo == null || !ShouldTranslateDeliveryJobsPackageInfoText(additionalInfo))
                        return;

                    TranslateDeliveryJobsPackageInfoText(additionalInfo, translations);
                }
                catch (Exception ex)
                {
                    CoreConsole.Warning("[ModTextTranslator] Falha ao traduzir etiqueta do DeliveryJobs: " + ex.Message);
                }
            }

            private TextMesh ResolveAdditionalInfo()
            {
                MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour == null)
                        continue;

                    Type type = behaviour.GetType();
                    if (!IsComponentType(type, DeliveryJobsPackageComponentName))
                        continue;

                    return GetFieldValue<TextMesh>(behaviour, type, "additionalInfo");
                }

                return null;
            }
        }

        public sealed class DirtTrackTextTranslator : MonoBehaviour
        {
            private Text target;
            private TranslationDictionary translations;
            private Font fallbackFont;
            private bool fallbackOnlyForNonAscii;
            private Font originalFont;
            private bool hasOriginalFont;
            private string lastText;

            public void Configure(Text target, TranslationDictionary translations)
            {
                Configure(target, translations, null, false);
            }

            public void Configure(Text target, TranslationDictionary translations, Font fallbackFont, bool fallbackOnlyForNonAscii)
            {
                this.target = target;
                this.translations = translations;
                this.fallbackFont = fallbackFont;
                this.fallbackOnlyForNonAscii = fallbackOnlyForNonAscii;
                if (!hasOriginalFont && target != null)
                {
                    originalFont = target.font;
                    hasOriginalFont = true;
                }
                lastText = null;
            }

            private void OnEnable()
            {
                TranslateNow();
            }

            private void LateUpdate()
            {
                TranslateNow();
            }

            public void TranslateNow()
            {
                if (target == null || target.gameObject == null || translations == null)
                    return;

                string current = target.text;
                if (string.IsNullOrEmpty(current))
                {
                    lastText = current;
                    return;
                }

                if (current != lastText)
                {
                    string translated = TranslateUiText(current, target, translations);
                    if (translated != null && translated != current)
                    {
                        target.text = translated;
                        current = translated;
                    }

                    lastText = current;
                }

                ApplyFallbackFontIfNeeded(target, fallbackFont, fallbackOnlyForNonAscii, originalFont, hasOriginalFont);
                PreventDirtTrackLineWrap(target);
            }
        }

        public sealed class ModsShopCartTextTranslator : MonoBehaviour
        {
            private MonoBehaviour cart;
            private TranslationDictionary translations;
            private GameObject uiRoot;
            private GameObject listView;
            private GameObject cartItemPrefab;
            private Text priceText;

            public void Configure(MonoBehaviour cart, TranslationDictionary translations)
            {
                this.cart = cart;
                this.translations = translations;
                ResolveFields();
            }

            public int AttachNow()
            {
                ResolveFields();

                int attached = 0;
                attached += AttachUiTextTranslator(priceText, translations) ? 1 : 0;
                attached += AttachUiTextTranslators(uiRoot, translations);
                attached += AttachUiTextTranslators(cartItemPrefab, translations);
                attached += AttachUiTextTranslators(listView, translations);
                return attached;
            }

            private void LateUpdate()
            {
                if (cart == null || translations == null)
                    return;

                if (uiRoot == null || listView == null || priceText == null)
                    ResolveFields();

                if (uiRoot == null || !uiRoot.activeInHierarchy)
                    return;

                AttachUiTextTranslator(priceText, translations);
                AttachUiTextTranslators(listView, translations);
            }

            private void ResolveFields()
            {
                if (cart == null)
                    return;

                Type type = cart.GetType();
                uiRoot = GetFieldValue<GameObject>(cart, type, "ui");
                listView = GetFieldValue<GameObject>(cart, type, "listView");
                cartItemPrefab = GetFieldValue<GameObject>(cart, type, "cartItem");
                priceText = GetFieldValue<Text>(cart, type, "priceText");
            }
        }

        public sealed class MscModApiShopTextTranslator : MonoBehaviour
        {
            private object shopInterface;
            private Type shopInterfaceType;
            private TranslationDictionary translations;
            private GameObject root;
            private GameObject partsPanel;
            private GameObject modsPanel;
            private GameObject cartPanel;
            private GameObject partsList;
            private GameObject modsList;
            private GameObject cartList;
            private Text btnBuyTextComp;
            private Text moneyComp;
            private Text totalCostComp;

            public void Configure(object shopInterface, TranslationDictionary translations)
            {
                this.shopInterface = shopInterface;
                this.translations = translations;
                shopInterfaceType = shopInterface != null ? shopInterface.GetType() : null;
                ResolveFields();
            }

            public int AttachNow()
            {
                ResolveFields();

                int attached = 0;
                attached += AttachUiTextTranslators(root, translations);
                attached += AttachUiTextTranslators(partsPanel, translations);
                attached += AttachUiTextTranslators(modsPanel, translations);
                attached += AttachUiTextTranslators(cartPanel, translations);
                attached += AttachUiTextTranslators(partsList, translations);
                attached += AttachUiTextTranslators(modsList, translations);
                attached += AttachUiTextTranslators(cartList, translations);
                attached += AttachUiTextTranslator(btnBuyTextComp, translations) ? 1 : 0;
                attached += AttachUiTextTranslator(moneyComp, translations) ? 1 : 0;
                attached += AttachUiTextTranslator(totalCostComp, translations) ? 1 : 0;
                return attached;
            }

            private void LateUpdate()
            {
                if (shopInterface == null || translations == null)
                    return;

                if (root == null || partsList == null || modsList == null || cartList == null)
                    ResolveFields();

                if (root == null || !root.activeInHierarchy)
                    return;

                AttachUiTextTranslators(partsList, translations);
                AttachUiTextTranslators(modsList, translations);
                AttachUiTextTranslators(cartList, translations);
                AttachUiTextTranslator(btnBuyTextComp, translations);
                AttachUiTextTranslator(moneyComp, translations);
                AttachUiTextTranslator(totalCostComp, translations);
            }

            private void ResolveFields()
            {
                if (shopInterface == null)
                    return;

                if (shopInterfaceType == null)
                    shopInterfaceType = shopInterface.GetType();

                root = GetFieldValue<GameObject>(shopInterface, shopInterfaceType, "gameObject");
                partsPanel = GetFieldValue<GameObject>(shopInterface, shopInterfaceType, "partsPanel");
                modsPanel = GetFieldValue<GameObject>(shopInterface, shopInterfaceType, "modsPanel");
                cartPanel = GetFieldValue<GameObject>(shopInterface, shopInterfaceType, "cartPanel");
                partsList = GetFieldValue<GameObject>(shopInterface, shopInterfaceType, "partsList");
                modsList = GetFieldValue<GameObject>(shopInterface, shopInterfaceType, "modsList");
                cartList = GetFieldValue<GameObject>(shopInterface, shopInterfaceType, "cartList");
                btnBuyTextComp = GetFieldValue<Text>(shopInterface, shopInterfaceType, "btnBuyTextComp");
                moneyComp = GetFieldValue<Text>(shopInterface, shopInterfaceType, "moneyComp");
                totalCostComp = GetFieldValue<Text>(shopInterface, shopInterfaceType, "totalCostComp");
            }
        }

        private static string TranslateUiText(string current, Text text, TranslationDictionary translations)
        {
            string translated;
            if (translations.TryGetExact(current, out translated))
                return translated;

            string path = GetPath(text);
            if (current.IndexOf('\n') >= 0)
            {
                translated = TryTranslateUiTextByLines(current, path, translations);
                if (translated != null)
                    return translated;

                return null;
            }

            translated = TryTranslateUiLine(current, path, translations);
            if (translated != null)
                return translated;

            translated = TryTranslateRichQuantity(current, text, translations);
            if (translated != null)
                return translated;

            return null;
        }

        private static string TryTranslateUiTextByLines(string current, string path, TranslationDictionary translations)
        {
            if (string.IsNullOrEmpty(current) || current.IndexOf('\n') < 0)
                return null;

            string[] lines = current.Split('\n');
            bool changed = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace("\r", string.Empty);
                string translated = TryTranslateUiLine(line, path, translations);

                if (translated == null || translated == line)
                    continue;

                lines[i] = translated;
                changed = true;
            }

            return changed ? string.Join("\n", lines) : null;
        }

        private static string TryTranslateUiLine(string line, string path, TranslationDictionary translations)
        {
            if (string.IsNullOrEmpty(line) || translations == null)
                return null;

            string translated;
            if (translations.TryGetExact(line, out translated))
                return translated;

            translated = TryTranslateColonPrefix(line, translations);
            if (translated != null)
                return translated;

            return translations.TryMatchPattern(line, path);
        }

        private static string TryTranslateColonPrefix(string line, TranslationDictionary translations)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
                return null;

            string prefix = line.Substring(0, colonIndex).TrimEnd();
            if (string.IsNullOrEmpty(prefix))
                return null;

            string translatedPrefix;
            if (!translations.TryGetExact(prefix, out translatedPrefix)
                || string.IsNullOrEmpty(translatedPrefix)
                || translatedPrefix == prefix)
            {
                return null;
            }

            return translatedPrefix + line.Substring(colonIndex);
        }

        private static string TryTranslateRichQuantity(string current, Text text, TranslationDictionary translations)
        {
            const string quantityMarker = " <color=yellow>x";
            const string colorClose = "</color>";

            if (string.IsNullOrEmpty(current) || !current.EndsWith(colorClose))
                return null;

            int markerIndex = current.LastIndexOf(quantityMarker);
            if (markerIndex <= 0)
                return null;

            string itemName = current.Substring(0, markerIndex);
            string suffix = current.Substring(markerIndex);
            string translatedName;
            if (translations.TryGetExact(itemName, out translatedName))
                return translatedName + suffix;

            translatedName = translations.TryMatchPattern(itemName, GetPath(text));
            if (translatedName != null)
                return translatedName + suffix;

            return null;
        }

        private static bool AttachUiTextTranslator(Text text, TranslationDictionary translations)
        {
            return AttachUiTextTranslator(text, translations, null, false);
        }

        private static bool AttachUiTextTranslator(Text text, TranslationDictionary translations, Font fallbackFont, bool fallbackOnlyForNonAscii)
        {
            if (text == null || text.gameObject == null || translations == null)
                return false;

            bool created = false;
            DirtTrackTextTranslator component = text.gameObject.GetComponent<DirtTrackTextTranslator>();
            if (component == null)
            {
                component = text.gameObject.AddComponent<DirtTrackTextTranslator>();
                created = true;
            }

            component.Configure(text, translations, fallbackFont, fallbackOnlyForNonAscii);
            component.TranslateNow();
            return created;
        }

        private static int AttachUiTextTranslators(GameObject root, TranslationDictionary translations)
        {
            return AttachUiTextTranslators(root, translations, null, false);
        }

        private static int AttachUiTextTranslators(GameObject root, TranslationDictionary translations, Font fallbackFont, bool fallbackOnlyForNonAscii)
        {
            if (root == null || translations == null)
                return 0;

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            if (texts == null || texts.Length == 0)
                return 0;

            int attached = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                if (AttachUiTextTranslator(texts[i], translations, fallbackFont, fallbackOnlyForNonAscii))
                    attached++;
            }

            return attached;
        }

        private Font GetCustomFontByName(string fontName)
        {
            if (string.IsNullOrEmpty(fontName) || customFonts == null)
                return null;

            Font font;
            if (customFonts.TryGetValue(fontName, out font) && font != null)
                return font;

            foreach (KeyValuePair<string, Font> pair in customFonts)
            {
                if (pair.Value != null && pair.Value.name == fontName)
                    return pair.Value;
            }

            return null;
        }

        private static T GetFieldValue<T>(object target, Type type, string fieldName) where T : class
        {
            if (target == null || type == null)
                return null;

            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return null;

            return field.GetValue(target) as T;
        }

        private static bool IsComponentType(Type type, string expectedFullName)
        {
            return type != null && string.Equals(type.FullName, expectedFullName, StringComparison.Ordinal);
        }

        private static object GetMscModApiShopInterface()
        {
            Type shopType = FindLoadedType(MscModApiAssemblyName, MscModApiShopTypeName);
            if (shopType == null)
                return null;

            FieldInfo field = shopType.GetField("shopInterface", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return null;

            return field.GetValue(null);
        }

        private static Type FindLoadedType(string assemblyName, string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                    continue;

                AssemblyName name = assembly.GetName();
                if (name == null || !string.Equals(name.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                Type type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static GameObject FindGameObjectByNameIncludingInactive(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject go = objects[i];
                if (go != null && go.name == name)
                    return go;
            }

            return null;
        }

        private static GameObject FindDeliveryJobsAdvertUi()
        {
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            if (behaviours == null || behaviours.Length == 0)
                return null;

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.gameObject == null)
                    continue;

                Type behaviourType = behaviour.GetType();
                if (!IsComponentType(behaviourType, DeliveryJobsJobMapComponentName))
                    continue;

                GameObject advertUi = GetFieldValue<GameObject>(behaviour, behaviourType, "advertJobUI");
                if (advertUi != null)
                    return advertUi;
            }

            return null;
        }

        private static void ApplyFallbackFontIfNeeded(Text text, Font fallbackFont, bool onlyForNonAscii, Font originalFont, bool hasOriginalFont)
        {
            if (text == null || fallbackFont == null)
                return;

            bool shouldUseFallback = true;
            if (onlyForNonAscii && !ContainsNonAscii(text.text))
                shouldUseFallback = false;

            if (!shouldUseFallback)
            {
                if (hasOriginalFont && originalFont != null && text.font == fallbackFont)
                    text.font = originalFont;
                return;
            }

            if (text.font != fallbackFont)
                text.font = fallbackFont;
        }

        private static bool ContainsNonAscii(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] > 127)
                    return true;
            }

            return false;
        }

        private static void PreventDirtTrackLineWrap(Text text)
        {
            string role = GetDirtTrackTextRole(text);
            if (role != "Message header" && role != "Message" && role != "Message footer")
                return;

            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static string GetDirtTrackTextRole(Text text)
        {
            if (text == null || text.gameObject == null)
                return string.Empty;

            string name = text.gameObject.name;
            if (name == "Message" || name == "Message header" || name == "Message footer")
                return name;

            Transform parent = text.transform.parent;
            if (parent != null)
            {
                string parentName = parent.name;
                if (parentName == "Message" || parentName == "Message header" || parentName == "Message footer")
                    return parentName;
            }

            return string.Empty;
        }

        private static string GetPath(Text text)
        {
            if (text == null)
                return string.Empty;

            Transform current = text.transform;
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
        }
    }
}
