using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Stores original TextMesh state before adjustment
    /// </summary>
    public struct OriginalTextMeshState
    {
        public Vector3 LocalPosition;
        public float CharacterSize;
        public float LineSpacing;
        public float WidthScale;

        public OriginalTextMeshState(TextMesh textMesh)
        {
            LocalPosition = textMesh.transform.localPosition;
            CharacterSize = textMesh.characterSize;
            LineSpacing = textMesh.lineSpacing;
            WidthScale = textMesh.transform.localScale.x;
        }
    }

    /// <summary>
    /// Represents a position adjustment rule with path matching conditions
    /// Supports Contains, EndsWith, StartsWith, and negation (!Contains)
    /// </summary>
    public class TextAdjustment
    {
        // Paths whose transforms the game rewrites repeatedly. TextMeshes whose path
        // matches any entry opt into drift refresh after a normal adjustment applies.
        // Keep this list small - every touched TextMesh is checked during LateUpdate.
        internal static readonly string[] DriftTrackedPathPatterns =
        {
            "Sheets/Magazine/Products",
        };

        private const float PositionToleranceSqr = 1e-7f;

        internal static bool IsDriftTrackedPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            for (int i = 0; i < DriftTrackedPathPatterns.Length; i++)
            {
                if (path.Contains(DriftTrackedPathPatterns[i]))
                    return true;
            }
            return false;
        }

        public List<PathCondition> Conditions { get; private set; } = new List<PathCondition>();
        public Vector3 Offset { get; private set; }
        public float? FontSize { get; private set; }
        public float? LineSpacing { get; private set; }
        public float? WidthScale { get; private set; }

        // Track which TextMesh objects have been adjusted to prevent duplicate adjustments
        private HashSet<TextMesh> adjustedTextMeshes = new HashSet<TextMesh>();

        // Store original state of adjusted TextMesh objects for restoration
        private Dictionary<TextMesh, OriginalTextMeshState> originalStates = new Dictionary<TextMesh, OriginalTextMeshState>();

        // Last position written by this rule, for drift-tracked TextMeshes only.
        // Presence in this dict is the marker that a mesh participates in Refresh().
        private Dictionary<TextMesh, Vector3> lastAppliedLocalPositions = new Dictionary<TextMesh, Vector3>();

        // Pre-allocated buffers for Refresh(); avoids per-frame heap allocation.
        private readonly List<TextMesh> refreshSnapshot = new List<TextMesh>();
        private readonly List<TextMesh> staleBuffer = new List<TextMesh>();

        public bool HasDriftTrackedMeshes
        {
            get { return lastAppliedLocalPositions.Count > 0; }
        }

        public TextAdjustment(string conditionsString, Vector3 offset, float? fontSize = null, float? lineSpacing = null, float? widthScale = null)
        {
            Offset = offset;
            FontSize = fontSize;
            LineSpacing = lineSpacing;
            WidthScale = widthScale;
            ParseConditions(conditionsString);
        }

        /// <summary>
        /// Apply position adjustment to TextMesh if not already adjusted
        /// Uses HashSet to prevent duplicate adjustments
        /// </summary>
        /// <returns>True if adjustment was applied, false if already adjusted</returns>
        public bool ApplyAdjustment(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return false;

            // Skip if already adjusted
            if (adjustedTextMeshes.Contains(textMesh))
                return false;

            // Store original state before applying adjustments (only once, on first application)
            if (!originalStates.ContainsKey(textMesh))
                originalStates[textMesh] = new OriginalTextMeshState(textMesh);

            // Apply the position offset
            Vector3 currentPosition = textMesh.transform.localPosition;
            Vector3 newPosition = currentPosition + Offset;
            textMesh.transform.localPosition = newPosition;

            // Apply font size if specified
            if (FontSize.HasValue)
            {
                textMesh.characterSize = FontSize.Value;
            }

            // Apply line spacing if specified
            if (LineSpacing.HasValue)
            {
                textMesh.lineSpacing = LineSpacing.Value;
            }

            // Apply width scale if specified (scales X axis to make text wider/narrower)
            if (WidthScale.HasValue)
            {
                Vector3 scale = textMesh.transform.localScale;
                scale.x = WidthScale.Value;
                textMesh.transform.localScale = scale;
            }

            // Note: Unity 5.x TextMesh supports: characterSize, fontSize, lineSpacing, transform.localScale
            // Character width is controlled via transform.localScale.x

            // Mark as adjusted
            adjustedTextMeshes.Add(textMesh);

            // Opt this mesh into Refresh() only if its path is on the drift-tracked whitelist.
            if (IsDriftTrackedPath(path))
                lastAppliedLocalPositions[textMesh] = newPosition;

            return true;
        }

        /// <summary>
        /// Re-pin drift-tracked TextMeshes whose local position the game has rewritten
        /// since the last frame. Caller invokes every LateUpdate. No-op when nothing is tracked.
        /// </summary>
        public void Refresh()
        {
            if (lastAppliedLocalPositions.Count == 0)
                return;

            // Snapshot keys into the pre-allocated buffer so we can mutate the dict
            // during the pass without allocating a new List each frame.
            refreshSnapshot.Clear();
            refreshSnapshot.AddRange(lastAppliedLocalPositions.Keys);
            staleBuffer.Clear();

            foreach (TextMesh tm in refreshSnapshot)
            {
                if (tm == null)
                {
                    staleBuffer.Add(tm);
                    continue;
                }

                Vector3 currentPos = tm.transform.localPosition;
                Vector3 lastApplied = lastAppliedLocalPositions[tm];
                if ((currentPos - lastApplied).sqrMagnitude <= PositionToleranceSqr)
                    continue;

                // Game wrote a new local position. Treat it as the new baseline so we
                // don't accumulate drift, then re-apply the offset.
                OriginalTextMeshState state;
                if (originalStates.TryGetValue(tm, out state))
                {
                    state.LocalPosition = currentPos;
                    originalStates[tm] = state;
                }

                Vector3 newPos = currentPos + Offset;
                tm.transform.localPosition = newPos;
                lastAppliedLocalPositions[tm] = newPos;
            }

            // Release snapshot refs promptly so Unity can manage TextMesh lifetime.
            refreshSnapshot.Clear();

            for (int i = 0; i < staleBuffer.Count; i++)
            {
                lastAppliedLocalPositions.Remove(staleBuffer[i]);
                originalStates.Remove(staleBuffer[i]);
                adjustedTextMeshes.Remove(staleBuffer[i]);
            }
        }

        /// <summary>
        /// Restore TextMesh to its original state before adjustment
        /// </summary>
        /// <returns>True if restored, false if no original state exists</returns>
        public bool RestoreOriginal(TextMesh textMesh)
        {
            if (textMesh == null || !originalStates.ContainsKey(textMesh))
                return false;

            OriginalTextMeshState originalState = originalStates[textMesh];

            // Restore original values
            textMesh.transform.localPosition = originalState.LocalPosition;
            textMesh.characterSize = originalState.CharacterSize;
            textMesh.lineSpacing = originalState.LineSpacing;

            Vector3 scale = textMesh.transform.localScale;
            scale.x = originalState.WidthScale;
            textMesh.transform.localScale = scale;

            // Remove from adjusted set so it can be re-adjusted
            adjustedTextMeshes.Remove(textMesh);
            lastAppliedLocalPositions.Remove(textMesh);

            return true;
        }

        /// <summary>
        /// Clear the cache of adjusted TextMesh objects and restore all to original state
        /// Useful for F8 reload functionality.
        /// </summary>
        public void ClearCache()
        {
            // Restore all adjusted TextMeshes to their original state before clearing
            foreach (var textMesh in new List<TextMesh>(originalStates.Keys))
            {
                if (textMesh != null)
                    RestoreOriginal(textMesh);
            }
            // adjustedTextMeshes is already cleared by RestoreOriginal() calls
            originalStates.Clear();
            lastAppliedLocalPositions.Clear();
        }

        /// <summary>
        /// Parse condition string (e.g., "Contains(GUI/HUD) & EndsWith(/HUDLabel)")
        /// </summary>
        private void ParseConditions(string conditionsString)
        {
            string[] parts = conditionsString.Split('&');

            foreach (string part in parts)
            {
                string trimmed = part.Trim();

                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Check for negation
                bool negate = trimmed.StartsWith("!");
                if (negate)
                    trimmed = trimmed.Substring(1).Trim();

                // Parse condition type and value
                if (trimmed.StartsWith("Contains(") && trimmed.EndsWith(")"))
                {
                    string value = trimmed.Substring(9, trimmed.Length - 10);
                    Conditions.Add(new PathCondition(ConditionType.Contains, value, negate));
                }
                else if (trimmed.StartsWith("EndsWith(") && trimmed.EndsWith(")"))
                {
                    string value = trimmed.Substring(9, trimmed.Length - 10);
                    Conditions.Add(new PathCondition(ConditionType.EndsWith, value, negate));
                }
                else if (trimmed.StartsWith("StartsWith(") && trimmed.EndsWith(")"))
                {
                    string value = trimmed.Substring(11, trimmed.Length - 12);
                    Conditions.Add(new PathCondition(ConditionType.StartsWith, value, negate));
                }
                else if (trimmed.StartsWith("Equals(") && trimmed.EndsWith(")"))
                {
                    string value = trimmed.Substring(7, trimmed.Length - 8);
                    Conditions.Add(new PathCondition(ConditionType.Equals, value, negate));
                }
            }
        }

        /// <summary>
        /// Check if the given path matches all conditions
        /// </summary>
        public bool Matches(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            foreach (PathCondition condition in Conditions)
            {
                if (!condition.Evaluate(path))
                    return false;
            }

            return Conditions.Count > 0; // At least one condition must exist
        }
    }

    /// <summary>
    /// Represents a single path matching condition
    /// </summary>
    public class PathCondition
    {
        public ConditionType Type { get; private set; }
        public string Value { get; private set; }
        public bool Negate { get; private set; }

        public PathCondition(ConditionType type, string value, bool negate = false)
        {
            Type = type;
            Value = value;
            Negate = negate;
        }

        /// <summary>
        /// Evaluate if the path satisfies this condition
        /// </summary>
        public bool Evaluate(string path)
        {
            bool result = false;

            switch (Type)
            {
                case ConditionType.Contains:
                    result = path.Contains(Value);
                    break;

                case ConditionType.EndsWith:
                    result = path.EndsWith(Value);
                    break;

                case ConditionType.StartsWith:
                    result = path.StartsWith(Value);
                    break;

                case ConditionType.Equals:
                    result = path == Value;
                    break;
            }

            // Apply negation if needed
            return Negate ? !result : result;
        }
    }

    /// <summary>
    /// Types of path matching conditions
    /// </summary>
    public enum ConditionType
    {
        Contains,
        EndsWith,
        StartsWith,
        Equals
    }
}
