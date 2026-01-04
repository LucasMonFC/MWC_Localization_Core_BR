namespace MWC_Localization_Core
{
    /// <summary>
    /// Centralized constants for localization system
    /// All timing, retry, and threshold values in one place
    /// </summary>
    public static class LocalizationConstants
    {
        // Scene translation timing
        public const float MAINMENU_SCAN_INTERVAL = 5.0f;           // Scan main menu every 5 seconds (reduced frequency)
        public const float DYNAMIC_UPDATE_INTERVAL = 0.1f;          // Update dynamic UI every 0.1 seconds
        public const float EVERYFRAME_SCAN_INTERVAL = 2.0f;         // Scan every-frame elements every 2 seconds (was 0.0f - no throttling!)
        
        // Teletext translation timing
        public const float TELETEXT_TRANSLATION_DELAY = 1.0f;       // Wait 1 second after scene load
        public const float TELETEXT_RETRY_INTERVAL = 5.0f;          // Retry every 5 seconds
        public const int TELETEXT_MAX_RETRIES = 1;                  // Maximum retry attempts
        
        // Chat message translation
        public const int MAX_FIRST_MESSAGE_RETRIES = 20;            // 20 attempts over ~3 seconds
        public const int FIRST_MESSAGE_CHECK_INTERVAL = 9;          // Check every 9 frames (~3 sec at 60fps)
        
        // FSM monitoring
        public const float FSM_MONITOR_INTERVAL = 1.0f;             // Check FSM strings every 1 second
        public const bool ENABLE_FSM_MONITORING = false;            // Disabled until game fix
        
        // Monitoring strategies timing
        public const float EVERY_FRAME_INTERVAL = 0.0f;             // No throttling
        public const float FAST_POLLING_INTERVAL = 0.1f;            // 10 times per second
        public const float SLOW_POLLING_INTERVAL = 1.0f;            // Once per second
        public const float MONITOR_UPDATE_INTERVAL = 0.05f;         // Update monitor at 20 FPS (was every frame)
        public const float ARRAY_MONITOR_INTERVAL = 1.0f;           // Check arrays every 1 second (was every frame)
        
        // Array translation thresholds
        public const int ARRAY_POPULATION_THRESHOLD_PERCENT = 50;   // 50% of arrays must be populated
        
        // GameObject.Find retry settings (for timing issues at scene load)
        public const float GAMEOBJECT_FIND_RETRY_DELAY = 0.5f;      // Wait 0.5s before first retry
        public const int GAMEOBJECT_FIND_MAX_RETRIES = 5;           // Try up to 5 times
        public const float GAMEOBJECT_FIND_RETRY_INTERVAL = 1.0f;   // Retry every 1 second
    }
}
