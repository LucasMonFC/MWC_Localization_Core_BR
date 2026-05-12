namespace MWC_Localization_Core
{
    /// <summary>
    /// Centralized timing constants for the localization system.
    /// </summary>
    public static class LocalizationConstants
    {
        public const float ARRAY_MONITOR_INTERVAL = 2.0f;                                  // Check arrays every 2 seconds
        public const float ARRAY_MONITOR_STEP_INTERVAL = ARRAY_MONITOR_INTERVAL / 4f;      // Four staggered passes share one full array/proxy monitoring cycle.
        public const float FSM_SOURCE_POLL_INTERVAL = 0.2f;                                // Check dynamic FSM sources 5 times per second
        public const float GUI_MONITOR_RETRY_INTERVAL = 1.0f;                              // Retry missing GUI TextMesh references once per second
        public const float DRIFT_TRACKED_DISCOVERY_INTERVAL = 0.35f;                       // Rediscover rebuilt drift-tracked TextMeshes without scanning every frame
        public const float FSM_INDEX_REFRESH_INTERVAL = 1.0f;                              // Refresh inactive FSM lookup index once per second
    }
}
