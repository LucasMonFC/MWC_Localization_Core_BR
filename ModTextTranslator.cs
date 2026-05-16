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

        private sealed class TextMeshGroup
        {
            public readonly string AssemblyName;
            public readonly string[] Paths;

            public TextMeshGroup(string assemblyName, params string[] paths)
            {
                AssemblyName = assemblyName;
                Paths = paths;
            }
        }

        private static readonly TextMeshGroup[] TextMeshGroups = new TextMeshGroup[]
        {
            new TextMeshGroup(
                "LetItRust",
                "GUI/HUD/FleetariDialoguePrompt",
                "GUI/HUD/FleetariDialoguePrompt/HUDLabelShadow",
                "GUI/HUD/VenttiDialoguePrompt",
                "GUI/HUD/VenttiDialoguePrompt/HUDLabelShadow"),

            new TextMeshGroup(
                "FishingMod",
                "selltext",
                "comptext"),
        };

        private readonly HashSet<string> attachedTextMeshKeys = new HashSet<string>();
        private TranslationDictionary translations;
        private TextMeshTranslator textMeshTranslator;
        private int textMeshAttachAttempts;
        private int dirtTrackAttachAttempts;
        private bool dirtTrackAttached;

        public string Name { get { return "ModTextTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete
        {
            get
            {
                int activeTextMeshTargets = CountActiveTextMeshTargets();
                bool textMeshComplete = activeTextMeshTargets == 0
                    || attachedTextMeshKeys.Count >= activeTextMeshTargets
                    || textMeshAttachAttempts >= MaxAttachAttempts;

                bool dirtTrackComplete = !IsAssemblyLoaded(DirtTrackAssemblyName)
                    || dirtTrackAttached
                    || dirtTrackAttachAttempts >= MaxAttachAttempts;

                return textMeshComplete && dirtTrackComplete;
            }
        }

        public void Initialize(TranslationContext ctx)
        {
            translations = ctx.Translations;
            textMeshTranslator = ctx.Translator;
            Reset();
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
            attachedTextMeshKeys.Clear();
            textMeshAttachAttempts = 0;
            dirtTrackAttachAttempts = 0;
            dirtTrackAttached = false;
        }

        public void ClearTranslations()
        {
            Reset();
        }

        private int TryAttach()
        {
            if (Application.loadedLevelName != "GAME")
                return 0;

            int attached = 0;
            attached += TryAttachTextMeshes();
            attached += TryAttachDirtTrackUi();
            return attached;
        }

        private int TryAttachTextMeshes()
        {
            if (textMeshTranslator == null || CountActiveTextMeshTargets() == 0 || textMeshAttachAttempts >= MaxAttachAttempts)
                return 0;

            textMeshAttachAttempts++;
            int attached = 0;
            for (int i = 0; i < TextMeshGroups.Length; i++)
            {
                TextMeshGroup group = TextMeshGroups[i];
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
            if (attachedTextMeshKeys.Contains(key))
                return false;

            GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(path);
            if (go == null)
                return false;

            TextMesh textMesh = go.GetComponent<TextMesh>();
            if (textMesh == null)
            {
                attachedTextMeshKeys.Add(key);
                return false;
            }

            DirectTextMeshTranslator component = go.GetComponent<DirectTextMeshTranslator>();
            if (component == null)
                component = go.AddComponent<DirectTextMeshTranslator>();

            component.Configure(textMesh, path, textMeshTranslator);
            component.TranslateNow();
            attachedTextMeshKeys.Add(key);
            return true;
        }

        private int TryAttachDirtTrackUi()
        {
            if (translations == null
                || dirtTrackAttached
                || dirtTrackAttachAttempts >= MaxAttachAttempts
                || !IsAssemblyLoaded(DirtTrackAssemblyName))
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

        private static int CountActiveTextMeshTargets()
        {
            int count = 0;
            for (int i = 0; i < TextMeshGroups.Length; i++)
            {
                TextMeshGroup group = TextMeshGroups[i];
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
            private string lastText;

            public void Configure(TextMesh target, string path, TextMeshTranslator translator)
            {
                this.target = target;
                this.path = path;
                this.translator = translator;
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
                try
                {
                    if (target == null || target.gameObject == null || translator == null)
                        return;

                    string current = target.text;
                    if (string.IsNullOrEmpty(current))
                    {
                        lastText = current;
                        return;
                    }

                    if (current == lastText)
                        return;

                    translator.TranslateAndApplyFont(target, path);
                    translator.ApplyCustomFont(target, path);
                    lastText = target.text;
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
            private readonly Dictionary<int, int> originalFontSizes = new Dictionary<int, int>();
            private readonly Dictionary<int, Vector2> originalAnchoredPositions = new Dictionary<int, Vector2>();
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

                ApplyDirtTrackLayout(target, originalFontSizes, originalAnchoredPositions);
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

            return null;
        }

        private static void ApplyDirtTrackLayout(
            Text text,
            Dictionary<int, int> originalFontSizes,
            Dictionary<int, Vector2> originalAnchoredPositions)
        {
            string role = GetDirtTrackTextRole(text);
            if (role != "Message header" && role != "Message" && role != "Message footer")
                return;

            RectTransform roleRect = GetDirtTrackRoleRect(text, role);

            int instanceID = text.GetInstanceID();
            int originalSize;
            if (!originalFontSizes.TryGetValue(instanceID, out originalSize))
            {
                originalSize = text.fontSize;
                originalFontSizes[instanceID] = originalSize;
            }

            int roleRectID = roleRect.GetInstanceID();
            Vector2 originalPosition;
            if (!originalAnchoredPositions.TryGetValue(roleRectID, out originalPosition))
            {
                originalPosition = roleRect.anchoredPosition;
                originalAnchoredPositions[roleRectID] = originalPosition;
            }

            if (role == "Message header")
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = Math.Max(12, originalSize / 2);
                text.resizeTextMaxSize = Math.Max(18, (int)(originalSize * 0.72f));
                text.fontSize = text.resizeTextMaxSize;
                roleRect.anchoredPosition = originalPosition + new Vector2(0f, 24f);
                return;
            }

            if (role == "Message footer")
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = Math.Max(14, originalSize / 2);
                text.resizeTextMaxSize = Math.Max(20, (int)(originalSize * 0.72f));
                text.fontSize = text.resizeTextMaxSize;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                roleRect.anchoredPosition = originalPosition;
                return;
            }

            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(18, originalSize / 2);
            text.resizeTextMaxSize = Math.Max(24, (int)(originalSize * 0.84f));
            text.fontSize = text.resizeTextMaxSize;
            roleRect.anchoredPosition = originalPosition;
        }

        private static RectTransform GetDirtTrackRoleRect(Text text, string role)
        {
            Transform current = text.transform;
            if (current.name == role)
                return text.rectTransform;

            Transform parent = current.parent;
            if (parent != null && parent.name == role)
            {
                RectTransform parentRect = parent as RectTransform;
                if (parentRect != null)
                    return parentRect;
            }

            return text.rectTransform;
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
