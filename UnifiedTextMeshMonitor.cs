using MSCLoader;
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
        private TextMeshTranslator translator;
        
        // Throttling timers
        private float fastPollingTimer;
        private float slowPollingTimer;

        // Path-based monitoring rules
        private Dictionary<string, MonitoringStrategy> pathRules;
        private Dictionary<string, TextMeshEntry> pathRuleEntries;
        private Dictionary<MonitoringStrategy, List<string>> strategyGroupPaths;
        private List<string> monitoredPaths = new List<string>();

        public UnifiedTextMeshMonitor(TextMeshTranslator translator)
        {
            this.translator = translator;
            pathRules = new Dictionary<string, MonitoringStrategy>();
            pathRuleEntries = new Dictionary<string, TextMeshEntry>();
            strategyGroupPaths = new Dictionary<MonitoringStrategy, List<string>>();

            // Initialize strategy groups
            foreach (MonitoringStrategy strategy in System.Enum.GetValues(typeof(MonitoringStrategy)))
            {
                strategyGroupPaths[strategy] = new List<string>();
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
            AddPathRule("GUI/Indicators/TaxiGUI", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/TaxiGUI/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Gear", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Gear/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/HUD/Thrist/HUDLabel", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/HUD/Thrist/HUDLabel/Shadow", MonitoringStrategy.EveryFrame);
            
            // Active HUD - fast polling (10 FPS)
            AddPathRule("GUI/HUD/Mortal/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Day/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Thirst/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Hunger/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Stress/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Urine/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Fatigue/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Money/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Bodytemp/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Sweat/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Jailtime/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("Systems/TV/TVGraphics/CHAT/Day", MonitoringStrategy.FastPolling);
            AddPathRule("Systems/TV/TVGraphics/CHAT/Moderator", MonitoringStrategy.FastPolling);
            
            // Teletext/FSM - slow polling (1 FPS)
            AddPathRule("Systems/TV/Teletext/VKTekstiTV/PAGES", MonitoringStrategy.SlowPolling);
            AddPathRule("Systems/TV/TVGraphics/CHAT/Generated", MonitoringStrategy.SlowPolling);
            
            // Magazine / Sheets - on visibility change
            AddPathRule("Sheets/UnemployPaper", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/ServiceBrochure", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/ServicePayment", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/YellowPagesMagazine/Page1", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/YellowPagesMagazine/Page2", MonitoringStrategy.OnVisibilityChange);
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
        /// Register all TextMeshes under defined path rules
        /// </summary>
        public void RegisterAllPathRuleElements()
        {
            foreach (string parentPath in pathRules.Keys)
            {
                MonitoringStrategy strategy = pathRules[parentPath];
                if (strategy == MonitoringStrategy.LateTranslateOnce ||
                    strategy == MonitoringStrategy.OnVisibilityChange)
                {
                    monitoredPaths.Add(parentPath);
                }

                Register(parentPath, strategy);
            }
        }

        /// <summary>
        /// Periodically monitor TextMeshes to be available for monitoring
        /// </summary>
        public void MonitorLateRegister()
        {
            foreach (string parentPath in monitoredPaths)
            {
                if (Register(parentPath, MonitoringStrategy.LateTranslateOnce) > 0)
                {
                    monitoredPaths.Remove(parentPath);
                    break; // Avoid modifying collection during iteration
                }
                if (Register(parentPath, MonitoringStrategy.OnVisibilityChange) > 0)
                {
                    monitoredPaths.Remove(parentPath);
                    break; // Avoid modifying collection during iteration
                }
            }
        }

        /// <summary>
        /// Register a TextMesh for monitoring
        /// 
        /// </summary>
        public int Register(string parentPath, MonitoringStrategy? strategy = null)
        {
            int registeredCount = 0;

            // Skip if already registered
            if (pathRuleEntries.ContainsKey(parentPath))
                return registeredCount;
            
            // Determine strategy if not provided
            MonitoringStrategy finalStrategy = strategy ?? pathRules[parentPath];
        
            GameObject parent = GameObject.Find(parentPath);
            if (parent == null)
                return registeredCount;

            // Get all TextMesh components under this parent and apply fonts to ALL of them
            TextMesh[] textMeshes = parent.GetComponentsInChildren<TextMesh>(true);
            foreach (var textMesh in textMeshes)
            {
                if (textMesh == null)
                    continue; // Skip nulls

                string textMeshPath = MLCUtils.GetGameObjectPath(textMesh.gameObject);
                TextMeshEntry entry = new TextMeshEntry(textMesh, textMeshPath, finalStrategy);

                pathRuleEntries[textMeshPath] = entry;
                strategyGroupPaths[finalStrategy].Add(textMeshPath);

                // CRITICAL: Translate immediately upon registration
                // This ensures initial translation happens before monitoring begins
                bool translated = translator.TranslateAndApplyFont(textMesh, textMeshPath, null);
                if (translated)
                {
                    entry.WasTranslated = true;
                    entry.UpdateLastText();
                }
            }
            return registeredCount;
        }

        /// <summary>
        /// Unregister a TextMesh from monitoring
        /// </summary>
        public void Unregister(string path)
        {
            if (path == null || !pathRuleEntries.ContainsKey(path))
                return;
            
            var entry = pathRuleEntries[path];
            strategyGroupPaths[entry.Strategy].Remove(path);
            pathRuleEntries.Remove(path);
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
                MonitorLateRegister(); // Also check for late registrations
                slowPollingTimer = 0f;
            }
            
            // OnVisibilityChange - check when visibility changes
            UpdateVisibilityChangeGroup();
        }

        private void UpdateGroup(MonitoringStrategy strategy)
        {
            var paths = strategyGroupPaths[strategy];
            
            foreach (string path in paths)
            {
                var entry = pathRuleEntries[path];
                
                if (!entry.IsValid())
                    continue;
                
                // Persistent strategy: Check regardless of status
                // Other strategies: Only check if text changed
                bool textChanged = entry.HasTextChanged();
                bool shouldCheck = textChanged || !entry.WasTranslated || strategy == MonitoringStrategy.Persistent;
                if (shouldCheck)
                {
                    bool translated = translator.TranslateAndApplyFont(entry.TextMesh, entry.Path, null);
                    if (translated)
                    {
                        entry.WasTranslated = true;
                        entry.UpdateLastText();
                        
                        // Remove from monitoring if TranslateOnce
                        if (strategy == MonitoringStrategy.TranslateOnce)
                        {
                            paths.Remove(path);
                            pathRuleEntries.Remove(path);
                        }
                    }
                }
            }
        }

        private void UpdateVisibilityChangeGroup()
        {
            var paths = strategyGroupPaths[MonitoringStrategy.OnVisibilityChange];
            
            foreach (string path in paths)
            {
                var entry = pathRuleEntries[path];
                
                if (!entry.IsValid())
                    continue;
                
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
            foreach (var group in strategyGroupPaths.Values)
            {
                group.Clear();
            }
            pathRuleEntries.Clear();
            fastPollingTimer = 0f;
            slowPollingTimer = 0f;
        }
    }
}
