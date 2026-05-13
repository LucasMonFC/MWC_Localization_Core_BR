using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// MonoBehaviour driver for surface MonitorTicks.
    /// Runs in LateUpdate() so source changes happen after the game's Update().
    /// </summary>
    public class LateUpdateHandler : MonoBehaviour
    {
        private List<ITranslationSurface> surfaces;
        private float[] nextTickTimes;
        private LocalizationConfig config;
        private SceneTranslationManager sceneManager;
        private bool isInitialized;

        public void Initialize(
            List<ITranslationSurface> surfaces,
            LocalizationConfig config,
            SceneTranslationManager sceneManager)
        {
            this.surfaces = surfaces;
            this.config = config;
            this.sceneManager = sceneManager;
            nextTickTimes = surfaces != null ? new float[surfaces.Count] : new float[0];

            // Stagger Slow surfaces so they don't all fire on the same frame.
            // Each Slow surface fires every ARRAY_MONITOR_INTERVAL, but consecutive
            // ones are offset by ARRAY_MONITOR_STEP_INTERVAL — same effective load
            // as the old hardcoded 4-step rotation.
            float now = Time.time;
            int slowIndex = 0;
            for (int i = 0; i < nextTickTimes.Length; i++)
            {
                float offset = 0f;
                if (surfaces[i].Cadence == SurfaceCadence.Slow)
                {
                    offset = slowIndex * LocalizationConstants.ARRAY_MONITOR_STEP_INTERVAL;
                    slowIndex++;
                }
                nextTickTimes[i] = now + offset;
            }

            isInitialized = true;
        }

        private void LateUpdate()
        {
            if (!isInitialized)
                return;

            string currentScene = Application.loadedLevelName;
            if (currentScene != "GAME" || !sceneManager.HasSceneBeenTranslated("GAME"))
                return;

            if (config != null)
                config.RefreshDriftTrackedAdjustments();

            float now = Time.time;
            float dt = Time.deltaTime;
            for (int i = 0; i < surfaces.Count; i++)
            {
                ITranslationSurface s = surfaces[i];
                switch (s.Cadence)
                {
                    case SurfaceCadence.PerFrame:
                        s.MonitorTick(dt);
                        break;

                    case SurfaceCadence.Fast:
                        if (now >= nextTickTimes[i])
                        {
                            s.MonitorTick(dt);
                            nextTickTimes[i] = now + LocalizationConstants.FSM_SOURCE_POLL_INTERVAL;
                        }
                        break;

                    case SurfaceCadence.Slow:
                        if (now >= nextTickTimes[i])
                        {
                            int translated = s.MonitorTick(dt);
                            if (translated > 0)
                                CoreConsole.Print($"[LateUpdateHandler] {s.Name}: translated {translated} item(s)");
                            nextTickTimes[i] = now + LocalizationConstants.ARRAY_MONITOR_INTERVAL;
                        }
                        break;

                    case SurfaceCadence.OncePerScene:
                        // No tick.
                        break;
                }
            }
        }

        /// <summary>
        /// Reset scheduler state when scene changes. Surfaces' own Reset() is called by the main class.
        /// </summary>
        public void ClearCache()
        {
            isInitialized = false;
        }
    }
}
