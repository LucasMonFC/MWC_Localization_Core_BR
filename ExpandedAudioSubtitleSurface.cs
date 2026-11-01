using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Plays Portuguese transcript segments in sync with selected game audio clips.
    /// Segment data lives in expanded_subtitles.txt; only AudioSources whose
    /// clip or GameObject name occurs in that file are monitored after the one-time scan.
    /// </summary>
    public sealed class ExpandedAudioSubtitleSurface : ITranslationSurface
    {
        private const string SubtitlePath = "GUI/Indicators/Subtitles";
        private const string SubtitleShadowName = "Shadow";
        private const float AudioRewindTolerance = 0.25f;
        private const float MaxOverheadDistance = 7f;
        private const float MinimumAudibleSourceVolume = 0.01f;
        private const float OverheadCharacterSize = 0.0125f;
        private const int OverheadFontSize = 48;
        private const int OverheadSingleLineMaxChars = 45;

        private sealed class SubtitleSegment
        {
            public float Timestamp;
            public float Duration;
            public string Text;
        }

        private sealed class OverheadTrack
        {
            public AudioSource Source;
            public List<SubtitleSegment> Segments;
            public int NextSegmentIndex;
            public float LastAudioTime = -1f;
            public float CurrentSegmentEnd;
            public string CurrentCaption;
            public GameObject Root;
            public TextMesh Mesh;
            public TextMesh ShadowMesh;
            public MeshRenderer Renderer;
            public MeshRenderer ShadowRenderer;
        }

        private readonly Dictionary<string, List<SubtitleSegment>> clipSubtitles =
            new Dictionary<string, List<SubtitleSegment>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<SubtitleSegment>> objectSubtitles =
            new Dictionary<string, List<SubtitleSegment>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> overheadClipTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> overheadObjectTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AudioSource> relevantSources = new List<AudioSource>();
        private readonly List<AudioListener> activeListeners = new List<AudioListener>();
        private readonly List<OverheadTrack> overheadTracks = new List<OverheadTrack>();
        private readonly Func<bool> isEnabled;

        private string assetsFolder;
        private TextMesh subtitleMesh;
        private TextMesh shadowMesh;
        private AudioSource currentSource;
        private List<SubtitleSegment> currentSegments;
        private int nextSegmentIndex;
        private float lastAudioTime = -1f;
        private float currentSegmentEnd;
        private string currentCaption;
        private bool sourceScanComplete;
        private bool wasEnabled;
        private bool subtitleDataLoaded;
        private bool warnedMissingFile;
        private bool warnedMissingHud;

        public ExpandedAudioSubtitleSurface(Func<bool> isEnabled)
        {
            this.isEnabled = isEnabled;
        }

        public string Name { get { return "ExpandedAudioSubtitleSurface"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.PerFrame; } }
        public bool IsComplete { get { return false; } }

        public void Initialize(TranslationContext ctx)
        {
            assetsFolder = ctx != null ? ctx.AssetsFolder : null;
            ClearCurrentCaption();
            clipSubtitles.Clear();
            objectSubtitles.Clear();
            overheadClipTargets.Clear();
            overheadObjectTargets.Clear();
            relevantSources.Clear();
            currentSource = null;
            currentSegments = null;
            ClearOverheadTracks();
            sourceScanComplete = false;
            subtitleDataLoaded = false;
            warnedMissingFile = false;
            warnedMissingHud = false;

            wasEnabled = IsEnabled();
            if (wasEnabled)
                LoadSubtitleData();
        }

        public int InitialPass()
        {
            if (!IsEnabled())
            {
                if (wasEnabled)
                    DisableFeature();
                wasEnabled = false;
                return 0;
            }

            if (!wasEnabled)
            {
                wasEnabled = true;
                sourceScanComplete = false;
            }
            LoadSubtitleData();

            if (Application.loadedLevelName != "GAME")
                return 0;

            BindSubtitleMeshes();
            RefreshAudioListeners();
            ScanAudioSources();
            return 0;
        }

        public int MonitorTick(float deltaTime)
        {
            if (!IsEnabled())
            {
                if (wasEnabled)
                    DisableFeature();
                wasEnabled = false;
                return 0;
            }

            if (!wasEnabled)
            {
                wasEnabled = true;
                sourceScanComplete = false;
            }
            LoadSubtitleData();

            if (Application.loadedLevelName != "GAME")
                return 0;

            if (!sourceScanComplete)
            {
                BindSubtitleMeshes();
                ScanAudioSources();
            }

            UpdateOverheadTracks();

            if (currentSource != null && !IsSourceAudibleToPlayer(currentSource))
                StopCurrentTrack();

            if (currentSource == null)
                TryStartTrack();

            if (currentSource == null || currentSegments == null || currentSegments.Count == 0)
                return 0;

            float audioTime = currentSource.time;
            if (lastAudioTime >= 0f && audioTime < lastAudioTime - AudioRewindTolerance)
            {
                nextSegmentIndex = 0;
                HideCurrentCaption();
            }
            lastAudioTime = audioTime;

            if (currentCaption != null && audioTime >= currentSegmentEnd)
                HideCurrentCaption();

            // If playback advanced across more than one timestamp between ticks,
            // show only the newest segment that is currently due.
            while (nextSegmentIndex < currentSegments.Count
                && audioTime >= currentSegments[nextSegmentIndex].Timestamp)
            {
                SubtitleSegment segment = currentSegments[nextSegmentIndex++];
                currentCaption = segment.Text;
                currentSegmentEnd = GetSegmentEnd(segment, nextSegmentIndex, currentSegments, currentSource);
            }

            if (currentCaption != null && audioTime < currentSegmentEnd)
                WriteHudText(currentCaption);

            return 0;
        }

        public void Reset()
        {
            StopCurrentTrack();
            relevantSources.Clear();
            activeListeners.Clear();
            ClearOverheadTracks();
            subtitleMesh = null;
            shadowMesh = null;
            sourceScanComplete = false;
        }

        public void ClearTranslations()
        {
            StopCurrentTrack();
            clipSubtitles.Clear();
            objectSubtitles.Clear();
            overheadClipTargets.Clear();
            overheadObjectTargets.Clear();
            relevantSources.Clear();
            activeListeners.Clear();
            ClearOverheadTracks();
            subtitleMesh = null;
            shadowMesh = null;
            sourceScanComplete = false;
            subtitleDataLoaded = false;
        }

        private int LoadSubtitleFile()
        {
            if (string.IsNullOrEmpty(assetsFolder))
                return 0;

            string path = Path.Combine(assetsFolder, "expanded_subtitles.txt");
            if (!File.Exists(path))
            {
                if (!warnedMissingFile)
                {
                    warnedMissingFile = true;
                    CoreConsole.Warning("[ExpandedAudioSubtitleSurface] Arquivo não encontrado: " + path);
                }
                return 0;
            }

            int loaded = 0;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line) || line.Trim().Length == 0)
                    continue;

                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("OVERHEAD|", StringComparison.Ordinal))
                {
                    string[] overheadFields = line.Split(new char[] { '|' }, 3);
                    if (overheadFields.Length == 3 && overheadFields[2].Trim().Length > 0)
                    {
                        if (overheadFields[1] == "CLIP")
                            overheadClipTargets.Add(overheadFields[2].Trim());
                        else if (overheadFields[1] == "OBJECT")
                            overheadObjectTargets.Add(overheadFields[2].Trim());
                        else
                            CoreConsole.Warning("[ExpandedAudioSubtitleSurface] Tipo overhead inválido na linha " + (i + 1));
                    }
                    else
                    {
                        CoreConsole.Warning("[ExpandedAudioSubtitleSurface] Metadado overhead inválido na linha " + (i + 1));
                    }
                    continue;
                }

                string[] fields = line.Split(new char[] { '|' }, 5);
                if (fields.Length != 5)
                {
                    CoreConsole.Warning("[ExpandedAudioSubtitleSurface] Linha " + (i + 1) + " inválida em expanded_subtitles.txt");
                    continue;
                }

                string targetType = fields[0].Trim();
                string targetName = fields[1].Trim();
                float timestamp;
                float duration;
                if ((targetType != "CLIP" && targetType != "OBJECT")
                    || targetName.Length == 0
                    || !float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out timestamp)
                    || !float.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out duration)
                    || timestamp < 0f || duration < 0f || fields[4].Length == 0)
                {
                    CoreConsole.Warning("[ExpandedAudioSubtitleSurface] Linha " + (i + 1) + " inválida em expanded_subtitles.txt");
                    continue;
                }

                Dictionary<string, List<SubtitleSegment>> destination = targetType == "CLIP" ? clipSubtitles : objectSubtitles;
                List<SubtitleSegment> script;
                if (!destination.TryGetValue(targetName, out script))
                {
                    script = new List<SubtitleSegment>();
                    destination[targetName] = script;
                }

                script.Add(new SubtitleSegment
                {
                    Timestamp = timestamp,
                    Duration = duration,
                    Text = fields[4]
                });
                loaded++;
            }

            SortScripts(clipSubtitles);
            SortScripts(objectSubtitles);
            return loaded;
        }

        private bool IsEnabled()
        {
            return isEnabled == null || isEnabled();
        }

        private void LoadSubtitleData()
        {
            if (subtitleDataLoaded || !IsEnabled())
                return;

            int segmentCount = LoadSubtitleFile();
            subtitleDataLoaded = true;
            int scriptCount = clipSubtitles.Count + objectSubtitles.Count;
            if (segmentCount > 0)
                CoreConsole.Print("[ExpandedAudioSubtitleSurface] Carregou " + scriptCount + " roteiros de áudio e " + segmentCount + " trechos");
        }

        private void DisableFeature()
        {
            StopCurrentTrack();
            ClearOverheadTracks();
            clipSubtitles.Clear();
            objectSubtitles.Clear();
            overheadClipTargets.Clear();
            overheadObjectTargets.Clear();
            relevantSources.Clear();
            activeListeners.Clear();
            subtitleMesh = null;
            shadowMesh = null;
            sourceScanComplete = false;
            subtitleDataLoaded = false;
        }

        private static void SortScripts(Dictionary<string, List<SubtitleSegment>> scripts)
        {
            foreach (List<SubtitleSegment> segments in scripts.Values)
                segments.Sort(delegate(SubtitleSegment left, SubtitleSegment right) { return left.Timestamp.CompareTo(right.Timestamp); });
        }

        private void BindSubtitleMeshes()
        {
            if (subtitleMesh != null)
                return;

            GameObject subtitleObject = LocalizationUtils.FindGameObjectIncludingInactive(SubtitlePath);
            if (subtitleObject != null)
            {
                subtitleMesh = subtitleObject.GetComponent<TextMesh>();
                Transform shadow = subtitleObject.transform.Find(SubtitleShadowName);
                if (shadow != null)
                    shadowMesh = shadow.GetComponent<TextMesh>();
            }

            if (subtitleMesh == null && !warnedMissingHud)
            {
                warnedMissingHud = true;
                CoreConsole.Warning("[ExpandedAudioSubtitleSurface] HUD de legendas não encontrado em " + SubtitlePath);
            }
        }

        private void ScanAudioSources()
        {
            relevantSources.Clear();
            AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                List<SubtitleSegment> ignored;
                if (source != null && TryResolveScript(source, out ignored))
                    relevantSources.Add(source);
            }

            sourceScanComplete = true;
            CoreConsole.Print("[ExpandedAudioSubtitleSurface] Fontes de áudio monitoradas: " + relevantSources.Count);
        }

        private void TryStartTrack()
        {
            for (int i = 0; i < relevantSources.Count; i++)
            {
                AudioSource source = relevantSources[i];
                if (source == null)
                {
                    relevantSources.RemoveAt(i--);
                    continue;
                }
                if (!IsSourceAudibleToPlayer(source))
                    continue;

                if (IsOverheadSource(source))
                    continue;

                List<SubtitleSegment> script;
                if (!TryResolveScript(source, out script) || script.Count == 0)
                    continue;

                currentSource = source;
                currentSegments = script;
                nextSegmentIndex = 0;
                lastAudioTime = -1f;
                currentSegmentEnd = 0f;
                currentCaption = null;
                return;
            }
        }

        private void RefreshAudioListeners()
        {
            activeListeners.Clear();
            AudioListener[] listeners = Resources.FindObjectsOfTypeAll<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener != null && listener.isActiveAndEnabled
                    && listener.gameObject != null && listener.gameObject.activeInHierarchy)
                    activeListeners.Add(listener);
            }
        }

        private bool IsSourceAudibleToPlayer(AudioSource source)
        {
            if (source == null || !source.isActiveAndEnabled || source.gameObject == null
                || !source.gameObject.activeInHierarchy || !source.isPlaying || source.mute
                || source.volume < MinimumAudibleSourceVolume || AudioListener.pause
                || AudioListener.volume < MinimumAudibleSourceVolume)
                return false;

            // PL marks overhead scripts separately. Their labels follow the source
            // and are hidden beyond 7 m, but a playing clip remains tracked so it
            // can appear if the player approaches before playback ends.
            if (IsOverheadSource(source))
                return true;

            // Respect the AudioSource's own 3D attenuation range for other local audio.
            if (source.spatialBlend > 0.01f)
            {
                float nearestDistance = GetNearestListenerDistance(source.transform.position);
                if (float.IsPositiveInfinity(nearestDistance))
                    return false;
                return source.maxDistance <= 0f || nearestDistance <= source.maxDistance;
            }

            return true;
        }

        private float GetNearestListenerDistance(Vector3 sourcePosition)
        {
            if (activeListeners.Count == 0)
                RefreshAudioListeners();

            float nearest = float.PositiveInfinity;
            for (int i = activeListeners.Count - 1; i >= 0; i--)
            {
                AudioListener listener = activeListeners[i];
                if (listener == null || !listener.isActiveAndEnabled
                    || listener.gameObject == null || !listener.gameObject.activeInHierarchy)
                {
                    activeListeners.RemoveAt(i);
                    continue;
                }

                float distance = Vector3.Distance(sourcePosition, listener.transform.position);
                if (distance < nearest)
                    nearest = distance;
            }

            return nearest;
        }

        private bool IsOverheadSource(AudioSource source)
        {
            if (source == null)
                return false;

            string clipName = source.clip != null ? source.clip.name : string.Empty;
            string objectName = source.gameObject != null ? source.gameObject.name : string.Empty;
            return overheadClipTargets.Contains(clipName) || overheadObjectTargets.Contains(objectName);
        }

        private bool TryResolveScript(AudioSource source, out List<SubtitleSegment> script)
        {
            script = null;
            if (source == null)
                return false;

            if (source.gameObject != null && objectSubtitles.TryGetValue(source.gameObject.name, out script))
                return true;

            return source.clip != null && clipSubtitles.TryGetValue(source.clip.name, out script);
        }

        private static float GetSegmentEnd(
            SubtitleSegment segment,
            int nextIndex,
            List<SubtitleSegment> segments,
            AudioSource source)
        {
            if (segment.Duration > 0f)
                return segment.Timestamp + segment.Duration;

            if (nextIndex < segments.Count)
                return Math.Max(segment.Timestamp, segments[nextIndex].Timestamp);

            if (source != null && source.clip != null)
                return Math.Max(segment.Timestamp, source.clip.length);

            return segment.Timestamp + 3f;
        }

        private void WriteHudText(string text)
        {
            if (subtitleMesh == null)
                BindSubtitleMeshes();
            if (subtitleMesh != null && subtitleMesh.text != text)
                subtitleMesh.text = text;
            if (shadowMesh != null && shadowMesh.text != text)
                shadowMesh.text = text;
        }

        private void ClearCurrentCaption()
        {
            HideCurrentCaption();
        }

        private void HideCurrentCaption()
        {
            string captionToClear = currentCaption;
            if (captionToClear != null && subtitleMesh != null && subtitleMesh.text == captionToClear)
                subtitleMesh.text = string.Empty;
            if (captionToClear != null && shadowMesh != null && shadowMesh.text == captionToClear)
                shadowMesh.text = string.Empty;
            currentCaption = null;
            currentSegmentEnd = 0f;
        }

        private void StopCurrentTrack()
        {
            ClearCurrentCaption();
            currentSource = null;
            currentSegments = null;
            nextSegmentIndex = 0;
            lastAudioTime = -1f;
        }

        private void UpdateOverheadTracks()
        {
            for (int i = overheadTracks.Count - 1; i >= 0; i--)
            {
                OverheadTrack track = overheadTracks[i];
                if (!IsSourceAudibleToPlayer(track.Source))
                {
                    DestroyOverheadLabel(track);
                    overheadTracks.RemoveAt(i);
                }
            }

            for (int sourceIndex = 0; sourceIndex < relevantSources.Count; sourceIndex++)
            {
                AudioSource source = relevantSources[sourceIndex];
                if (source == null || !IsOverheadSource(source) || !IsSourceAudibleToPlayer(source))
                    continue;

                OverheadTrack track = FindOverheadTrack(source);
                if (track != null)
                    continue;

                List<SubtitleSegment> script;
                if (TryResolveScript(source, out script) && script.Count > 0)
                    overheadTracks.Add(new OverheadTrack { Source = source, Segments = script });
            }

            for (int i = 0; i < overheadTracks.Count; i++)
                TickOverheadTrack(overheadTracks[i]);
        }

        private OverheadTrack FindOverheadTrack(AudioSource source)
        {
            for (int i = 0; i < overheadTracks.Count; i++)
            {
                if (overheadTracks[i].Source == source)
                    return overheadTracks[i];
            }
            return null;
        }

        private void TickOverheadTrack(OverheadTrack track)
        {
            if (track == null || track.Source == null || track.Segments == null || track.Segments.Count == 0)
                return;

            float audioTime = track.Source.time;
            if (track.LastAudioTime >= 0f && audioTime < track.LastAudioTime - AudioRewindTolerance)
            {
                track.NextSegmentIndex = 0;
                HideOverheadCaption(track);
            }
            track.LastAudioTime = audioTime;

            if (track.CurrentCaption != null && audioTime >= track.CurrentSegmentEnd)
                HideOverheadCaption(track);

            while (track.NextSegmentIndex < track.Segments.Count
                && audioTime >= track.Segments[track.NextSegmentIndex].Timestamp)
            {
                SubtitleSegment segment = track.Segments[track.NextSegmentIndex++];
                track.CurrentCaption = segment.Text;
                track.CurrentSegmentEnd = GetSegmentEnd(segment, track.NextSegmentIndex, track.Segments, track.Source);
            }

            if (track.CurrentCaption != null && audioTime < track.CurrentSegmentEnd)
                WriteOverheadText(track, track.CurrentCaption);
        }

        private void WriteOverheadText(OverheadTrack track, string text)
        {
            if (track == null || track.Source == null || string.IsNullOrEmpty(text))
            {
                HideOverheadLabel(track);
                return;
            }

            EnsureOverheadLabel(track);
            if (track.Root == null || track.Mesh == null)
                return;

            Camera camera = Camera.main;
            if (camera == null)
            {
                HideOverheadLabel(track);
                return;
            }

            string formatted = WrapOverheadText("\"" + text + "\"", OverheadSingleLineMaxChars);
            if (track.Mesh.text != formatted)
                track.Mesh.text = formatted;
            if (track.ShadowMesh != null && track.ShadowMesh.text != formatted)
                track.ShadowMesh.text = formatted;

            Vector3 sourcePosition = track.Source.transform.position;
            float distance = Vector3.Distance(camera.transform.position, sourcePosition);
            bool visible = distance <= MaxOverheadDistance;
            if (track.Renderer != null)
                track.Renderer.enabled = visible;
            if (track.ShadowRenderer != null)
                track.ShadowRenderer.enabled = visible;

            track.Root.transform.position = sourcePosition + Vector3.up * 0.6f;
            track.Root.transform.rotation = camera.transform.rotation;
            float scale = Mathf.Clamp(distance * 0.15f, 1f, 3f);
            track.Root.transform.localScale = new Vector3(scale, scale, scale);
        }

        private static string WrapOverheadText(string text, int maxLineLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLineLength)
                return text;

            for (int i = 1; i < text.Length - 1; i++)
            {
                if ((text[i] == '-' || text[i] == '–') && text[i - 1] == ' ' && text[i + 1] == ' ')
                {
                    return text.Substring(0, i).TrimEnd() + "\n" + text.Substring(i).TrimStart();
                }
            }

            int midpoint = text.Length / 2;
            int nearestSpace = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ' && (nearestSpace < 0
                    || Mathf.Abs(i - midpoint) < Mathf.Abs(nearestSpace - midpoint)))
                    nearestSpace = i;
            }
            if (nearestSpace < 0)
                return text;
            return text.Substring(0, nearestSpace) + "\n" + text.Substring(nearestSpace + 1);
        }

        private void EnsureOverheadLabel(OverheadTrack track)
        {
            if (track == null || track.Root != null)
                return;

            if (subtitleMesh == null || subtitleMesh.font == null)
                BindSubtitleMeshes();
            if (subtitleMesh == null || subtitleMesh.font == null)
                return;

            track.Root = new GameObject("MWC_Localization_OverheadSubtitle");
            UnityEngine.Object.DontDestroyOnLoad(track.Root);
            track.Mesh = track.Root.AddComponent<TextMesh>();
            ConfigureOverheadMesh(track.Mesh, false);
            track.Renderer = track.Root.GetComponent<MeshRenderer>();

            GameObject shadowObject = new GameObject("Shadow");
            shadowObject.transform.parent = track.Root.transform;
            shadowObject.transform.localPosition = new Vector3(1f / 160f, -1f / 160f, 0.0125f);
            track.ShadowMesh = shadowObject.AddComponent<TextMesh>();
            ConfigureOverheadMesh(track.ShadowMesh, true);
            track.ShadowRenderer = shadowObject.GetComponent<MeshRenderer>();
        }

        private void ConfigureOverheadMesh(TextMesh mesh, bool shadow)
        {
            if (mesh == null)
                return;

            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = OverheadFontSize;
            mesh.characterSize = OverheadCharacterSize;
            mesh.richText = false;
            mesh.fontStyle = subtitleMesh != null ? subtitleMesh.fontStyle : FontStyle.Normal;
            mesh.color = shadow
                ? (shadowMesh != null ? shadowMesh.color : Color.black)
                : (subtitleMesh != null ? subtitleMesh.color : Color.white);
            if (subtitleMesh != null && subtitleMesh.font != null)
                mesh.font = subtitleMesh.font;

            MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null && subtitleMesh != null)
            {
                MeshRenderer sourceRenderer = subtitleMesh.GetComponent<MeshRenderer>();
                if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
                    renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }
        }

        private void HideOverheadCaption(OverheadTrack track)
        {
            if (track == null)
                return;
            track.CurrentCaption = null;
            track.CurrentSegmentEnd = 0f;
            HideOverheadLabel(track);
        }

        private void HideOverheadLabel(OverheadTrack track)
        {
            if (track == null)
                return;
            if (track.Renderer != null)
                track.Renderer.enabled = false;
            if (track.ShadowRenderer != null)
                track.ShadowRenderer.enabled = false;
            if (track.Mesh != null)
                track.Mesh.text = string.Empty;
            if (track.ShadowMesh != null)
                track.ShadowMesh.text = string.Empty;
        }

        private void DestroyOverheadLabel(OverheadTrack track)
        {
            if (track == null)
                return;
            HideOverheadLabel(track);
            if (track.Root != null)
                UnityEngine.Object.Destroy(track.Root);
            track.Root = null;
            track.Mesh = null;
            track.ShadowMesh = null;
            track.Renderer = null;
            track.ShadowRenderer = null;
        }

        private void ClearOverheadTracks()
        {
            for (int i = 0; i < overheadTracks.Count; i++)
                DestroyOverheadLabel(overheadTracks[i]);
            overheadTracks.Clear();
        }
    }
}
