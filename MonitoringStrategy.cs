namespace MWC_Localization_Core
{
    /// <summary>
    /// Defines how frequently a TextMesh should be monitored for changes.
    /// </summary>
    public enum MonitoringStrategy
    {
        /// <summary>
        /// Translate once during the initial scene scan, then stop monitoring.
        /// Use for static UI elements that never change.
        /// </summary>
        TranslateOnce,

        /// <summary>
        /// Monitor every frame without throttling.
        /// Use for interaction prompts, subtitles, and other critical real-time UI.
        /// </summary>
        EveryFrame,

        /// <summary>
        /// Monitor at a fast polling interval for responsive UI.
        /// Use for active HUD elements that update frequently.
        /// </summary>
        FastPolling,

        /// <summary>
        /// Monitor at a slow polling interval for less critical content.
        /// Use for teletext, FSM-generated content, and weather displays.
        /// </summary>
        SlowPolling,

        /// <summary>
        /// Keep checking even after translation for content that is frequently rebuilt.
        /// Use for magazine text and similar regenerated content.
        /// </summary>
        Persistent,

        /// <summary>
        /// Translate only when a GameObject becomes active in the hierarchy.
        /// Use for menus, dialogs, and other show/hide UI panels.
        /// </summary>
        OnVisibilityChange,

        /// <summary>
        /// Translate once when the TextMesh becomes available later.
        /// Use for dynamically created TextMeshes that appear after the initial scan.
        /// </summary>
        LateTranslateOnce,
    }

    /// <summary>
    /// Defines which translation method to use.
    /// </summary>
    public enum TranslationMode
    {
        /// <summary>
        /// Simple dictionary lookup for exact text keys.
        /// </summary>
        SimpleLookup,

        /// <summary>
        /// Pattern matching with {0}, {1}, and similar placeholders.
        /// </summary>
        FsmPattern,

        /// <summary>
        /// Regular expression extraction and replacement.
        /// </summary>
        RegexExtract,

        /// <summary>
        /// Split by comma and translate each item independently.
        /// </summary>
        CommaSeparated,

        /// <summary>
        /// Use a custom handler for more complex translation logic.
        /// </summary>
        CustomHandler,

        /// <summary>
        /// Pattern matching with placeholders plus translation of extracted parameters.
        /// </summary>
        FsmPatternWithTranslation
    }
}
