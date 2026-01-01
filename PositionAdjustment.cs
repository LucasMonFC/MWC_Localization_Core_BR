using BepInEx.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Represents a position adjustment rule with path matching conditions
    /// Supports Contains, EndsWith, StartsWith, and negation (!Contains)
    /// </summary>
    public class PositionAdjustment
    {
        public List<PathCondition> Conditions { get; private set; } = new List<PathCondition>();
        public Vector3 Offset { get; private set; }

        public PositionAdjustment(string conditionsString, Vector3 offset)
        {
            Offset = offset;
            ParseConditions(conditionsString);
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
