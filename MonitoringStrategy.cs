namespace MWC_Localization_Core
{
    /// <summary>
    /// Defines how frequently a TextMesh should be monitored for changes
    /// </summary>
    public enum MonitoringStrategy
    {
        /// <summary>
        /// Translate once during initial scene scan, then forget
        /// Use for: Static UI elements that never change
        /// </summary>
        TranslateOnce,

        /// <summary>
        /// Monitor every frame without throttling (highest priority)
        /// Use for: Interaction prompts, subtitles, critical realtime UI
        /// Example: "Press E to interact", subtitle text
        /// </summary>
        EveryFrame,

        /// <summary>
        /// Monitor at 10 FPS (0.1s intervals) for responsive UI
        /// Use for: Active HUD elements that update frequently
        /// Example: Hunger, stress, temperature displays
        /// </summary>
        FastPolling,

        /// <summary>
        /// Monitor at 1 FPS (1.0s intervals) for less critical content
        /// Use for: Teletext, FSM-generated content, weather displays
        /// Example: TV text, bottomlines, weather forecasts
        /// </summary>
        SlowPolling,

        /// <summary>
        /// Keep checking even after translation (for regenerated content)
        /// Use for: Magazine text that game constantly regenerates
        /// Example: Yellow Pages random words
        /// </summary>
        Persistent,

        /// <summary>
        /// Only translate when GameObject becomes active in hierarchy
        /// Use for: UI panels that are shown/hidden
        /// Example: Menus, popup dialogs
        /// </summary>
        OnVisibilityChange
    }

    /// <summary>
    /// Defines which translation method to use
    /// </summary>
    public enum TranslationMode
    {
        /// <summary>
        /// Simple dictionary lookup: "BEER" -> "맥주"
        /// </summary>
        SimpleLookup,

        /// <summary>
        /// Pattern matching with {0}, {1} placeholders
        /// Example: "pakkasta {0} astetta" -> extract variable -> "영하 {0}도"
        /// </summary>
        FsmPattern,

        /// <summary>
        /// Regular expression extraction and replacement
        /// Example: "PRICE TOTAL: 0.00 MK" -> extract "0.00" -> "가격: 0.00 MK"
        /// </summary>
        RegexExtract,

        /// <summary>
        /// Split by comma and translate each word independently
        /// Example: "bucket, oil, hydraulic" -> "양동이, 오일, 유압"
        /// </summary>
        CommaSeparated,

        /// <summary>
        /// Use custom handler function for complex logic
        /// Example: Magazine price/phone lines with special formatting
        /// </summary>
        CustomHandler,

        /// <summary>
        /// Pattern matching with {0}, {1} placeholders AND parameter translation
        /// Example: "Hello! I would like to order a taxi to {0}." 
        /// Extracts "Futufon", translates it to Korean, inserts into Korean template
        /// </summary>
        FsmPatternWithTranslation
    }
}
