using System;
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
        private const string ModsShopAssemblyName = "ModsShop";
        private const string ModsShopCartComponentName = "ModsShop.ShoppingCartUI";

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
        private TextMeshTranslator textMeshTranslator;
        private int activeTextMeshTargetCount;
        private bool dirtTrackAssemblyLoaded;
        private bool modsShopAssemblyLoaded;
        private int textMeshAttachAttempts;
        private int dirtTrackAttachAttempts;
        private int modsShopAttachAttempts;
        private bool dirtTrackAttached;
        private bool modsShopAttached;
        private bool hasSupportedModAssemblyLoaded;

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
            textMeshTranslator = ctx.Translator;
            Reset();
            RefreshActiveModState(Application.loadedLevelName);
        }

        public int InitialPass()
        {
            return TryAttach();
        }

        public int MonitorTick(float deltaTime)
        {
            return TryAttach();
        }

        public void Reset()
        {
            attachedTextMeshComponents.Clear();
            activeTextMeshTargetCount = 0;
            dirtTrackAssemblyLoaded = false;
            modsShopAssemblyLoaded = false;
            textMeshAttachAttempts = 0;
            dirtTrackAttachAttempts = 0;
            modsShopAttachAttempts = 0;
            dirtTrackAttached = false;
            modsShopAttached = false;
            hasSupportedModAssemblyLoaded = false;
        }

        public void ClearTranslations()
        {
            Reset();
        }

        private int TryAttach()
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
                attached += TryAttachModsShopUi();
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
                if (behaviourType == null || behaviourType.FullName != ModsShopCartComponentName)
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

            return textMeshComplete && dirtTrackComplete && modsShopComplete;
        }

        private void RefreshActiveModState(string sceneName)
        {
            activeTextMeshTargetCount = CountActiveTextMeshTargets(sceneName);
            dirtTrackAssemblyLoaded = sceneName == "GAME" && IsAssemblyLoaded(DirtTrackAssemblyName);
            modsShopAssemblyLoaded = sceneName == "GAME" && IsAssemblyLoaded(ModsShopAssemblyName);
            hasSupportedModAssemblyLoaded = activeTextMeshTargetCount > 0 || dirtTrackAssemblyLoaded || modsShopAssemblyLoaded;
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
                    CoreConsole.Warning("[ModTextTranslator] Failed translating " + path + ": " + ex.Message);
                }
            }
        }

        public sealed class DirtTrackTextTranslator : MonoBehaviour
        {
            private Text target;
            private TranslationDictionary translations;
            private string lastText;

            public void Configure(Text target, TranslationDictionary translations)
            {
                this.target = target;
                this.translations = translations;
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

        private static string TranslateUiText(string current, Text text, TranslationDictionary translations)
        {
            string translated;
            if (translations.TryGetExact(current, out translated))
                return translated;

            translated = translations.TryMatchPattern(current, GetPath(text));
            if (translated != null)
                return translated;

            translated = TryTranslateRichQuantity(current, text, translations);
            if (translated != null)
                return translated;

            return null;
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
            if (text == null || text.gameObject == null || translations == null)
                return false;

            bool created = false;
            DirtTrackTextTranslator component = text.gameObject.GetComponent<DirtTrackTextTranslator>();
            if (component == null)
            {
                component = text.gameObject.AddComponent<DirtTrackTextTranslator>();
                created = true;
            }

            component.Configure(text, translations);
            component.TranslateNow();
            return created;
        }

        private static int AttachUiTextTranslators(GameObject root, TranslationDictionary translations)
        {
            if (root == null || translations == null)
                return 0;

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            if (texts == null || texts.Length == 0)
                return 0;

            int attached = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                if (AttachUiTextTranslator(texts[i], translations))
                    attached++;
            }

            return attached;
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
