namespace MWC_Localization_Core
{
    /// <summary>
    /// Centralized timing constants for the localization system.
    /// </summary>
    public static class LocalizationConstants
    {
        public const float ARRAY_MONITOR_INTERVAL = 2.0f;                                 // Check arrays every 2 seconds
        public const float ARRAY_MONITOR_STEP_INTERVAL = ARRAY_MONITOR_INTERVAL / 4f;     // Four staggered passes share one full array/proxy monitoring cycle.
        public const float FSM_SOURCE_POLL_INTERVAL = 0.2f;                               // Check dynamic FSM sources 5 times per second
    }
}
