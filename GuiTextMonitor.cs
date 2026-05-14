using System.Collections.Generic;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Lightweight direct GUI monitor, modeled after the old LanguageFramework HUD pass.
    /// The primary TextMesh is translated once, then copied to shadow/paired meshes.
    /// </summary>
    public class GuiTextMonitor : ITranslationSurface
    {
        public string Name { get { return "GuiTextMonitor"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.PerFrame; } }
        public bool IsComplete { get { return false; } }

        private class GuiTextEntry
        {
            public string PrimaryPath;
            public string[] CopyPaths;
            public TextMesh PrimaryTextMesh;
            public TextMesh[] CopyTextMeshes;
            public string LastText;
            public bool HasProcessedText;
            public bool HasBaseLayout;
            public Vector3 PrimaryBaseLocalPosition;
            public Vector3[] CopyBaseLocalPositions;
            public bool[] CopyHasBaseLayout;
            public float LastLayoutYOffset;

            public GuiTextEntry(string primaryPath, params string[] copyPaths)
            {
                PrimaryPath = primaryPath;
                CopyPaths = copyPaths ?? new string[0];
                CopyTextMeshes = new TextMesh[CopyPaths.Length];
                CopyBaseLocalPositions = new Vector3[CopyPaths.Length];
                CopyHasBaseLayout = new bool[CopyPaths.Length];
                LastText = string.Empty;
                HasProcessedText = false;
                HasBaseLayout = false;
                LastLayoutYOffset = 0f;
            }

            public bool HasPrimary()
            {
                return PrimaryTextMesh != null && PrimaryTextMesh.gameObject != null;
            }
        }

        private TextMeshTranslator translator;
        private readonly List<GuiTextEntry> guiEntries = new List<GuiTextEntry>();
        private readonly Dictionary<string, float> retainedSubtitleDurations = new Dictionary<string, float>();
        private GuiTextEntry interactionEntry;
        private GuiTextEntry partnameEntry;
        private GuiTextEntry subtitlesEntry;
        private float retryTimer;
        private string retainedSubtitleText;
        private float retainedSubtitleUntil;

        public void Initialize(TranslationContext ctx)
        {
            translator = ctx.Translator;
            SubtitleTimingHandler.PopulateRetainedSubtitleDurations(ctx.Translations, retainedSubtitleDurations);
            InitializeGuiEntries();
        }

        public int InitialPass()
        {
            RegisterAll();
            return guiEntries.Count;
        }

        public int MonitorTick(float deltaTime)
        {
            Update(deltaTime);
            return 0;
        }

        public void ClearTranslations()
        {
            // GuiTextMonitor has no owned translation data; cache reset only.
        }

        private void InitializeGuiEntries()
        {
            guiEntries.Clear();
            interactionEntry = null;
            partnameEntry = null;
            subtitlesEntry = null;

            interactionEntry = AddGuiEntry("GUI/Indicators/Interaction", "GUI/Indicators/Interaction/Shadow");
            partnameEntry = AddGuiEntry("GUI/Indicators/Partname", "GUI/Indicators/Partname/Shadow");
            subtitlesEntry = AddGuiEntry("GUI/Indicators/Subtitles", "GUI/Indicators/Subtitles/Shadow");
            AddGuiEntry("GUI/Indicators/RallyCountdown", "GUI/Indicators/RallyCountdown/Shadow");
            AddGuiEntry("GUI/Indicators/Gear", "GUI/Indicators/Gear/Shadow");
            AddGuiEntry("GUI/HUD/Thrist/HUDLabel", "GUI/HUD/Thrist/HUDLabel/Shadow");

            AddGuiEntry("GUI/HUD/Mortal/HUDValue");
            AddGuiEntry("GUI/HUD/Day/HUDValue");
            AddGuiEntry("GUI/HUD/Thirst/HUDValue");
            AddGuiEntry("GUI/HUD/Hunger/HUDValue");
            AddGuiEntry("GUI/HUD/Stress/HUDValue");
            AddGuiEntry("GUI/HUD/Urine/HUDValue");
            AddGuiEntry("GUI/HUD/Fatigue/HUDValue");
            AddGuiEntry("GUI/HUD/Money/HUDValue");
            AddGuiEntry("GUI/HUD/Bodytemp/HUDValue");
            AddGuiEntry("GUI/HUD/Sweat/HUDValue");
            AddGuiEntry("GUI/HUD/Jailtime/HUDValue");
        }

        private GuiTextEntry AddGuiEntry(string primaryPath, params string[] copyPaths)
        {
            GuiTextEntry entry = new GuiTextEntry(primaryPath, copyPaths);
            guiEntries.Add(entry);
            return entry;
        }

        public void RegisterAll()
        {
            if (Application.loadedLevelName != "GAME")
                return;

            for (int i = 0; i < guiEntries.Count; i++)
            {
                RegisterEntry(guiEntries[i]);
            }
        }

        public void Update(float deltaTime)
        {
            if (Application.loadedLevelName != "GAME")
                return;

            retryTimer += deltaTime;
            if (retryTimer >= LocalizationConstants.GUI_MONITOR_RETRY_INTERVAL)
            {
                RegisterAll();
                retryTimer = 0f;
            }

            for (int i = 0; i < guiEntries.Count; i++)
            {
                UpdateEntry(guiEntries[i]);
            }

            UpdatePartnameMultilineLayout();
        }

        public void Reset()
        {
            retryTimer = 0f;
            retainedSubtitleText = null;
            retainedSubtitleUntil = 0f;
            InitializeGuiEntries();
        }

        private void RegisterEntry(GuiTextEntry entry)
        {
            if (entry == null)
                return;

            if (!entry.HasPrimary())
            {
                GameObject primaryObject = LocalizationUtils.FindGameObjectCached(entry.PrimaryPath);
                entry.PrimaryTextMesh = primaryObject != null ? primaryObject.GetComponent<TextMesh>() : null;
                entry.LastText = string.Empty;
                entry.HasProcessedText = false;
                entry.HasBaseLayout = false;
            }

            for (int i = 0; i < entry.CopyPaths.Length; i++)
            {
                TextMesh copyTextMesh = entry.CopyTextMeshes[i];
                if (copyTextMesh != null && copyTextMesh.gameObject != null)
                    continue;

                GameObject copyObject = LocalizationUtils.FindGameObjectCached(entry.CopyPaths[i]);
                entry.CopyTextMeshes[i] = copyObject != null ? copyObject.GetComponent<TextMesh>() : null;
                if (entry.HasBaseLayout && entry.CopyTextMeshes[i] != null)
                    CaptureCopyBasePosition(entry, i);
            }
        }

        private void UpdateEntry(GuiTextEntry entry)
        {
            if (entry == null || !entry.HasPrimary() || !entry.PrimaryTextMesh.gameObject.activeInHierarchy)
                return;

            string sourceText = entry.PrimaryTextMesh.text;
            if (string.IsNullOrEmpty(sourceText))
            {
                if (entry == subtitlesEntry && TryRestoreRetainedSubtitle(entry))
                    return;

                if (entry == subtitlesEntry)
                    ClearRetainedSubtitleState();

                if (!string.IsNullOrEmpty(entry.LastText))
                    ClearCopiedText(entry);

                return;
            }

            if (entry == subtitlesEntry && TryClearExpiredRetainedSubtitle(entry, sourceText))
                return;

            bool textChanged = entry.LastText != sourceText;
            if (!textChanged && entry.HasProcessedText)
            {
                if (entry == subtitlesEntry)
                    translator.ApplyCustomFont(entry.PrimaryTextMesh, entry.PrimaryPath);

                SyncCopiedText(entry, entry.LastText, false);
                return;
            }

            translator.TranslateAndApplyFont(entry.PrimaryTextMesh, entry.PrimaryPath);
            string translatedText = entry.PrimaryTextMesh.text;
            entry.LastText = translatedText;
            entry.HasProcessedText = true;

            if (entry == subtitlesEntry)
            {
                translator.ApplyCustomFont(entry.PrimaryTextMesh, entry.PrimaryPath);
                UpdateRetainedSubtitle(translatedText);
            }

            SyncCopiedText(entry, translatedText, true);
        }

        private void SyncCopiedText(GuiTextEntry entry, string translatedText, bool forceFont)
        {
            for (int i = 0; i < entry.CopyTextMeshes.Length; i++)
            {
                TextMesh copyTextMesh = entry.CopyTextMeshes[i];
                if (copyTextMesh == null || copyTextMesh.gameObject == null)
                    continue;

                bool needsTextSync = copyTextMesh.text != translatedText;
                if (needsTextSync)
                    copyTextMesh.text = translatedText;

                if (forceFont || needsTextSync)
                    translator.ApplyCustomFont(copyTextMesh, entry.CopyPaths[i]);
            }
        }

        private void UpdateRetainedSubtitle(string text)
        {
            float seconds;
            if (!retainedSubtitleDurations.TryGetValue(text, out seconds))
            {
                ClearRetainedSubtitleState();
                return;
            }

            retainedSubtitleText = text;
            retainedSubtitleUntil = Time.time + seconds;
        }

        private bool TryRestoreRetainedSubtitle(GuiTextEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(retainedSubtitleText) || Time.time >= retainedSubtitleUntil)
                return false;

            entry.PrimaryTextMesh.text = retainedSubtitleText;
            entry.LastText = retainedSubtitleText;
            entry.HasProcessedText = true;
            translator.ApplyCustomFont(entry.PrimaryTextMesh, entry.PrimaryPath);
            SyncCopiedText(entry, retainedSubtitleText, true);
            return true;
        }

        private bool TryClearExpiredRetainedSubtitle(GuiTextEntry entry, string sourceText)
        {
            if (entry == null
                || string.IsNullOrEmpty(retainedSubtitleText)
                || Time.time < retainedSubtitleUntil
                || sourceText != retainedSubtitleText)
            {
                return false;
            }

            entry.PrimaryTextMesh.text = string.Empty;
            ClearCopiedText(entry);
            ClearRetainedSubtitleState();
            return true;
        }

        private void ClearRetainedSubtitleState()
        {
            retainedSubtitleText = null;
            retainedSubtitleUntil = 0f;
        }

        private void ClearCopiedText(GuiTextEntry entry)
        {
            entry.LastText = string.Empty;
            entry.HasProcessedText = false;

            for (int i = 0; i < entry.CopyTextMeshes.Length; i++)
            {
                TextMesh copyTextMesh = entry.CopyTextMeshes[i];
                if (copyTextMesh != null && copyTextMesh.gameObject != null)
                    copyTextMesh.text = string.Empty;
            }
        }

        private void UpdatePartnameMultilineLayout()
        {
            if (partnameEntry == null || !partnameEntry.HasPrimary())
                return;

            int maxLineCount = GetVisibleLineCount(partnameEntry);
            maxLineCount = System.Math.Max(maxLineCount, GetVisibleLineCount(interactionEntry));
            maxLineCount = System.Math.Max(maxLineCount, GetVisibleLineCount(subtitlesEntry));

            EnsureBaseLayout(partnameEntry);

            if (maxLineCount <= 1)
            {
                ApplyPartnameOffset(0f);
                return;
            }

            float offset = 0.74f + ((float)(maxLineCount - 2) * 0.50f);
            if (offset > 2.24f)
                offset = 2.24f;

            ApplyPartnameOffset(offset);
        }

        private int GetVisibleLineCount(GuiTextEntry entry)
        {
            if (entry == null || !entry.HasPrimary() || !entry.PrimaryTextMesh.gameObject.activeInHierarchy)
                return 1;

            string text = entry.PrimaryTextMesh.text;
            if (string.IsNullOrEmpty(text))
                return 1;

            return CountLineBreaks(text);
        }

        private int CountLineBreaks(string text)
        {
            int lines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    lines++;
            }

            return lines;
        }

        private void EnsureBaseLayout(GuiTextEntry entry)
        {
            if (entry.HasBaseLayout || !entry.HasPrimary())
                return;

            entry.PrimaryBaseLocalPosition = entry.PrimaryTextMesh.transform.localPosition;
            for (int i = 0; i < entry.CopyTextMeshes.Length; i++)
            {
                CaptureCopyBasePosition(entry, i);
            }

            entry.HasBaseLayout = true;
        }

        private void CaptureCopyBasePosition(GuiTextEntry entry, int index)
        {
            TextMesh copyTextMesh = entry.CopyTextMeshes[index];
            if (copyTextMesh == null || copyTextMesh.gameObject == null)
                return;

            if (entry.HasPrimary() && copyTextMesh.transform.parent == entry.PrimaryTextMesh.transform)
            {
                entry.CopyBaseLocalPositions[index] = copyTextMesh.transform.localPosition;
            }
            else
            {
                entry.CopyBaseLocalPositions[index] =
                    copyTextMesh.transform.localPosition - new Vector3(0f, entry.LastLayoutYOffset, 0f);
            }

            entry.CopyHasBaseLayout[index] = true;
        }

        private void ApplyPartnameOffset(float yOffset)
        {
            Vector3 offset = new Vector3(0f, yOffset, 0f);
            Vector3 primaryPosition = partnameEntry.PrimaryBaseLocalPosition + offset;

            partnameEntry.PrimaryTextMesh.transform.localPosition = primaryPosition;

            for (int i = 0; i < partnameEntry.CopyTextMeshes.Length; i++)
            {
                TextMesh copyTextMesh = partnameEntry.CopyTextMeshes[i];
                if (copyTextMesh == null || copyTextMesh.gameObject == null)
                    continue;

                if (!partnameEntry.CopyHasBaseLayout[i])
                    CaptureCopyBasePosition(partnameEntry, i);

                if (copyTextMesh.transform.parent == partnameEntry.PrimaryTextMesh.transform)
                {
                    copyTextMesh.transform.localPosition = partnameEntry.CopyBaseLocalPositions[i];
                }
                else
                {
                    copyTextMesh.transform.localPosition = partnameEntry.CopyBaseLocalPositions[i] + offset;
                }
            }

            partnameEntry.LastLayoutYOffset = yOffset;
        }
    }
}
