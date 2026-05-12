namespace MWC_Localization_Core
{
    /// <summary>
    /// Centralized timing constants for the localization system.
    /// </summary>
    public static class LocalizationConstants
    {
        public const float ARRAY_MONITOR_INTERVAL = 2.0f;           // Check arrays every 2 seconds
        // Four staggered passes share one full array/proxy monitoring cycle.
        public const float ARRAY_MONITOR_STEP_INTERVAL = ARRAY_MONITOR_INTERVAL / 4f;
    }
}
