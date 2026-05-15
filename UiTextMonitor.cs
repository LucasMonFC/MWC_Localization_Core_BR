using UnityEngine;
using UnityEngine.UI;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Monitors Unity UI Text components used by mod canvases.
    /// Dirt Track Racing rebuilds its race HUD text at runtime, outside TextMesh/FSM paths.
    /// </summary>
    public class UiTextMonitor : ITranslationSurface
    {
        public string Name { get { return "UiTextMonitor"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.PerFrame; } }
        public bool IsComplete { get { return checkedGameScene && !dirtTrackLoaded; } }

        private TranslationDictionary translations;
        private Text[] dirtTrackTexts;
        private readonly Dictionary<int, int> originalFontSizes = new Dictionary<int, int>();
        private readonly Dictionary<int, Vector2> originalAnchoredPositions = new Dictionary<int, Vector2>();
        private float uiRefreshTimer;
        private bool checkedGameScene;
        private bool dirtTrackLoaded;

        public void Initialize(TranslationContext ctx)
        {
            translations = ctx.Translations;
            dirtTrackTexts = null;
            uiRefreshTimer = 0f;
            checkedGameScene = false;
            dirtTrackLoaded = false;
        }

        public int InitialPass()
        {
            CheckDirtTrackLoadedForGameScene();
            if (!dirtTrackLoaded)
                return 0;

            RegisterDirtTrackUi();
            return dirtTrackTexts != null ? dirtTrackTexts.Length : 0;
        }

        public int MonitorTick(float deltaTime)
        {
            if (Application.loadedLevelName != "GAME")
                return 0;

            CheckDirtTrackLoadedForGameScene();
            if (!dirtTrackLoaded)
                return 0;

            uiRefreshTimer += deltaTime;
            if (!HasValidDirtTrackUi())
            {
                if (uiRefreshTimer < LocalizationConstants.GUI_MONITOR_RETRY_INTERVAL)
                    return 0;

                RegisterDirtTrackUi();
                uiRefreshTimer = 0f;
            }

            if (!HasValidDirtTrackUi())
                return 0;

            int changed = 0;
            for (int i = 0; i < dirtTrackTexts.Length; i++)
            {
                Text text = dirtTrackTexts[i];
                if (text == null || text.gameObject == null || !text.gameObject.activeInHierarchy)
                    continue;

                string current = text.text;
                if (string.IsNullOrEmpty(current))
                    continue;

                string translated = TranslateUiText(current, text);
                if (translated != null && translated != current)
                {
                    text.text = translated;
                    changed++;
                }

                ApplyDirtTrackLayout(text);
            }

            return changed;
        }

        public void Reset()
        {
            dirtTrackTexts = null;
            originalFontSizes.Clear();
            originalAnchoredPositions.Clear();
            uiRefreshTimer = 0f;
            checkedGameScene = false;
            dirtTrackLoaded = false;
        }

        public void ClearTranslations()
        {
        }

        private void RegisterDirtTrackUi()
        {
            if (Application.loadedLevelName != "GAME")
                return;

            GameObject root = LocalizationUtils.FindGameObjectCached("DIRTTRACK_UI");
            dirtTrackTexts = root != null ? root.GetComponentsInChildren<Text>(true) : null;
        }

        private void CheckDirtTrackLoadedForGameScene()
        {
            if (checkedGameScene || Application.loadedLevelName != "GAME")
                return;

            checkedGameScene = true;
            dirtTrackLoaded = IsDirtTrackRacingLoaded();

            if (!dirtTrackLoaded)
                dirtTrackTexts = null;
        }

        private static bool IsDirtTrackRacingLoaded()
        {
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly assembly = assemblies[i];
                    if (assembly == null)
                        continue;

                    AssemblyName name = assembly.GetName();
                    if (name != null && name.Name == "DirtTrackRacing")
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private bool HasValidDirtTrackUi()
        {
            if (dirtTrackTexts == null || dirtTrackTexts.Length == 0)
                return false;

            for (int i = 0; i < dirtTrackTexts.Length; i++)
            {
                if (dirtTrackTexts[i] != null && dirtTrackTexts[i].gameObject != null)
                    return true;
            }

            return false;
        }

        private string TranslateUiText(string current, Text text)
        {
            string translated;
            if (translations.TryGetExact(current, out translated))
                return translated;

            translated = translations.TryMatchPattern(current, GetPath(text));
            if (translated != null)
                return translated;

            return null;
        }

        private void ApplyDirtTrackLayout(Text text)
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
                text.resizeTextMinSize = System.Math.Max(12, originalSize / 2);
                text.resizeTextMaxSize = System.Math.Max(18, (int)(originalSize * 0.72f));
                text.fontSize = text.resizeTextMaxSize;
                roleRect.anchoredPosition = originalPosition + new Vector2(0f, 24f);
                return;
            }

            if (role == "Message footer")
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = System.Math.Max(14, originalSize / 2);
                text.resizeTextMaxSize = System.Math.Max(20, (int)(originalSize * 0.72f));
                text.fontSize = text.resizeTextMaxSize;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                roleRect.anchoredPosition = originalPosition;
                return;
            }

            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = System.Math.Max(18, originalSize / 2);
            text.resizeTextMaxSize = System.Math.Max(24, (int)(originalSize * 0.84f));
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
