using BepInEx.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Represents a TextMesh component being monitored for translation
    /// </summary>
    public class TextMeshEntry
    {
        public TextMesh TextMesh;
        public GameObject GameObject;
        public string Path;
        public MonitoringStrategy Strategy;
        public string LastText;
        public bool WasTranslated;
        public bool IsVisible;
        
        public TextMeshEntry(TextMesh textMesh, string path, MonitoringStrategy strategy)
        {
            TextMesh = textMesh;
            GameObject = textMesh != null ? textMesh.gameObject : null;
            Path = path;
            Strategy = strategy;
            LastText = textMesh != null ? textMesh.text : "";
            WasTranslated = false;
            IsVisible = GameObject != null && GameObject.activeInHierarchy;
        }

        public bool IsValid()
        {
            return TextMesh != null && GameObject != null;
        }

        public bool HasTextChanged()
        {
            if (TextMesh == null || string.IsNullOrEmpty(TextMesh.text))
                return false;
            
            return LastText != TextMesh.text;
        }

        public void UpdateLastText()
        {
            if (TextMesh != null)
            {
                LastText = TextMesh.text;
            }
        }

        public void UpdateVisibility()
        {
            IsVisible = GameObject != null && GameObject.activeInHierarchy;
        }
    }

    /// <summary>
    /// Unified monitoring system for all TextMesh translation
    /// Replaces UpdatePriorityTextMeshes, UpdateDynamicTextMeshes, and FSM monitoring
    /// </summary>
    public class UnifiedTextMeshMonitor
    {
        private Dictionary<MonitoringStrategy, List<TextMeshEntry>> strategyGroups;
        private Dictionary<TextMesh, TextMeshEntry> textMeshToEntry;
        private TextMeshTranslator translator;
        private ManualLogSource logger;
        
        // Throttling timers
        private float fastPollingTimer;
        private float slowPollingTimer;

        // Path-based monitoring rules
        private Dictionary<string, MonitoringStrategy> pathRules;

        public UnifiedTextMeshMonitor(TextMeshTranslator translator, ManualLogSource logger)
        {
            this.translator = translator;
            this.logger = logger;
            
            strategyGroups = new Dictionary<MonitoringStrategy, List<TextMeshEntry>>();
            textMeshToEntry = new Dictionary<TextMesh, TextMeshEntry>();
            pathRules = new Dictionary<string, MonitoringStrategy>();
            
            // Initialize strategy groups
            foreach (MonitoringStrategy strategy in System.Enum.GetValues(typeof(MonitoringStrategy)))
            {
                strategyGroups[strategy] = new List<TextMeshEntry>();
            }
            
            InitializeDefaultPathRules();
        }

        private void InitializeDefaultPathRules()
        {
            // Critical UI - every frame
            AddPathRule("GUI/Indicators/Interaction", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Interaction/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Partname", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Partname/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Subtitles", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Subtitles/Shadow", MonitoringStrategy.EveryFrame);
            
            // Active HUD - fast polling (10 FPS)
            AddPathRule("GUI/HUD/Day/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/", MonitoringStrategy.FastPolling);
            
            // Teletext/FSM - slow polling (1 FPS)
            AddPathRule("Systems/TV/Teletext/", MonitoringStrategy.SlowPolling);
            AddPathRule("Systems/TV/TVGraphics/CHAT/", MonitoringStrategy.SlowPolling);
            
            // Magazine - persistent (always check)
            AddPathRule("Sheets/YellowPagesMagazine/", MonitoringStrategy.Persistent);
        }

        public void AddPathRule(string pathPattern, MonitoringStrategy strategy)
        {
            pathRules[pathPattern] = strategy;
        }

        /// <summary>
        /// Determine monitoring strategy for a given path
        /// Matches most specific (longest) rule first to avoid ambiguity
        /// </summary>
        public MonitoringStrategy DetermineStrategy(string path)
        {
            string longestMatch = null;
            MonitoringStrategy matchedStrategy = MonitoringStrategy.TranslateOnce;
            
            // Find the longest (most specific) matching rule
            foreach (var rule in pathRules)
            {
                if (path.Contains(rule.Key))
                {
                    // Use longest match (most specific)
                    if (longestMatch == null || rule.Key.Length > longestMatch.Length)
                    {
                        longestMatch = rule.Key;
                        matchedStrategy = rule.Value;
                    }
                }
            }
            
            return matchedStrategy;
        }

        /// <summary>
        /// Register a TextMesh for monitoring
        /// </summary>
        public void Register(TextMesh textMesh, string path, MonitoringStrategy? strategy = null)
        {
            if (textMesh == null)
                return;
            
            // Skip if already registered
            if (textMeshToEntry.ContainsKey(textMesh))
                return;
            
            // Determine strategy if not provided
            MonitoringStrategy finalStrategy = strategy ?? DetermineStrategy(path);
            
            // Create entry
            var entry = new TextMeshEntry(textMesh, path, finalStrategy);
            
            // Add to tracking
            textMeshToEntry[textMesh] = entry;
            strategyGroups[finalStrategy].Add(entry);
            
            // CRITICAL: Translate immediately upon registration
            // This ensures initial translation happens before monitoring begins
            bool translated = translator.TranslateAndApplyFont(textMesh, path, null);
            if (translated)
            {
                entry.WasTranslated = true;
                entry.UpdateLastText();
            }
        }

        /// <summary>
        /// Unregister a TextMesh from monitoring
        /// </summary>
        public void Unregister(TextMesh textMesh)
        {
            if (textMesh == null || !textMeshToEntry.ContainsKey(textMesh))
                return;
            
            var entry = textMeshToEntry[textMesh];
            strategyGroups[entry.Strategy].Remove(entry);
            textMeshToEntry.Remove(textMesh);
        }

        /// <summary>
        /// Main update loop - called every frame
        /// </summary>
        public void Update(float deltaTime)
        {
            // Always update EveryFrame and Persistent
            UpdateGroup(MonitoringStrategy.EveryFrame);
            UpdateGroup(MonitoringStrategy.Persistent);
            
            // Throttled fast polling (0.1s)
            fastPollingTimer += deltaTime;
            if (fastPollingTimer >= LocalizationConstants.FAST_POLLING_INTERVAL)
            {
                UpdateGroup(MonitoringStrategy.FastPolling);
                fastPollingTimer = 0f;
            }
            
            // Throttled slow polling (1.0s)
            slowPollingTimer += deltaTime;
            if (slowPollingTimer >= LocalizationConstants.SLOW_POLLING_INTERVAL)
            {
                UpdateGroup(MonitoringStrategy.SlowPolling);
                slowPollingTimer = 0f;
            }
            
            // OnVisibilityChange - check when visibility changes
            UpdateVisibilityChangeGroup();
        }

        private void UpdateGroup(MonitoringStrategy strategy)
        {
            var entries = strategyGroups[strategy];
            
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                
                // Remove invalid entries (destroyed objects)
                if (!entry.IsValid())
                {
                    entries.RemoveAt(i);
                    textMeshToEntry.Remove(entry.TextMesh);
                    continue;
                }
                
                // Persistent strategy: Check if text changed OR not yet translated
                // Other strategies: Only check if text changed
                bool textChanged = entry.HasTextChanged();
                bool shouldCheck = textChanged || (strategy == MonitoringStrategy.Persistent && !entry.WasTranslated);
                
                if (!shouldCheck)
                    continue;
                
                // For Persistent: If text changed, game regenerated it - translate again
                // For others: If text changed, translate
                if (textChanged || !entry.WasTranslated)
                {
                    bool translated = translator.TranslateAndApplyFont(entry.TextMesh, entry.Path, null);
                    
                    if (translated)
                    {
                        entry.WasTranslated = true;
                        entry.UpdateLastText();
                        
                        // Remove from monitoring if TranslateOnce
                        if (strategy == MonitoringStrategy.TranslateOnce)
                        {
                            entries.RemoveAt(i);
                            textMeshToEntry.Remove(entry.TextMesh);
                        }
                    }
                }
            }
        }

        private void UpdateVisibilityChangeGroup()
        {
            var entries = strategyGroups[MonitoringStrategy.OnVisibilityChange];
            
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                
                // Remove invalid entries
                if (!entry.IsValid())
                {
                    entries.RemoveAt(i);
                    textMeshToEntry.Remove(entry.TextMesh);
                    continue;
                }
                
                bool wasVisible = entry.IsVisible;
                entry.UpdateVisibility();
                
                // Only translate when visibility changes from hidden to visible
                if (!wasVisible && entry.IsVisible)
                {
                    bool translated = translator.TranslateAndApplyFont(entry.TextMesh, entry.Path, null);
                    if (translated)
                    {
                        entry.WasTranslated = true;
                        entry.UpdateLastText();
                    }
                }
            }
        }

        /// <summary>
        /// Clear all monitored entries
        /// </summary>
        public void Clear()
        {
            foreach (var group in strategyGroups.Values)
            {
                group.Clear();
            }
            textMeshToEntry.Clear();
            fastPollingTimer = 0f;
            slowPollingTimer = 0f;
        }

        /// <summary>
        /// Get diagnostic information
        /// </summary>
        public string GetDiagnostics()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("Unified TextMesh Monitor Status:");
            
            foreach (MonitoringStrategy strategy in System.Enum.GetValues(typeof(MonitoringStrategy)))
            {
                int count = strategyGroups[strategy].Count;
                if (count > 0)
                {
                    info.AppendLine($"  {strategy}: {count} entries");
                }
            }
            
            info.AppendLine($"Total monitored: {textMeshToEntry.Count}");
            
            return info.ToString();
        }

        /// <summary>
        /// Get all paths configured for EveryFrame monitoring
        /// Used by dynamic scanning to efficiently find new UI elements
        /// </summary>
        public System.Collections.Generic.List<string> GetEveryFramePaths()
        {
            var paths = new System.Collections.Generic.List<string>();
            
            foreach (var kvp in pathRules)
            {
                if (kvp.Value == MonitoringStrategy.EveryFrame)
                {
                    paths.Add(kvp.Key);
                }
            }
            
            return paths;
        }
    }
}
