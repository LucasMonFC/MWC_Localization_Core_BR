using System;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Defines which translation method to use.
    /// </summary>
    public enum TranslationMode
    {
        /// <summary>
        /// Pattern matching with {0}, {1}, and similar placeholders.
        /// </summary>
        FsmPattern,

        /// <summary>
        /// Use a custom handler for more complex translation logic.
        /// </summary>
        CustomHandler,

        /// <summary>
        /// Pattern matching with placeholders plus translation of extracted parameters.
        /// </summary>
        FsmPatternWithTranslation
    }

    /// <summary>
    /// Represents a translation pattern with extraction and formatting logic
    /// Supports placeholders and custom handlers
    /// </summary>
    public class TranslationPattern
    {
        public TranslationMode Mode { get; private set; }
        public string OriginalPattern { get; private set; }
        public string TranslatedTemplate { get; private set; }
        
        // For path-based matching
        public Func<string, bool> PathMatcher { get; set; }
        public Func<string, bool> TextMatcher { get; set; }
        
        // For FsmPattern mode
        private string[] originalParts;
        // For CustomHandler mode
        public Func<string, string, TranslationDictionary, CustomHandlerResult> CustomHandler { get; set; }
        
        // Result struct for custom handlers (NET35 compatible)
        public struct CustomHandlerResult
        {
            public bool Success;
            public string Result;
            
            public CustomHandlerResult(bool success, string result)
            {
                Success = success;
                Result = result;
            }
        }

        public TranslationPattern(string name, TranslationMode mode, string originalPattern, string translatedTemplate)
        {
            Mode = mode;
            OriginalPattern = originalPattern;
            TranslatedTemplate = translatedTemplate;
            
            Initialize();
        }

        private void Initialize()
        {
            switch (Mode)
            {
                case TranslationMode.FsmPattern:
                case TranslationMode.FsmPatternWithTranslation:
                    InitializeFsmPattern();
                    break;
            }
        }

        private void InitializeFsmPattern()
        {
            // Split patterns by placeholders to get static parts
            // Example: "pakkasta {0} astetta" -> ["pakkasta ", " astetta"]
            originalParts = SplitPattern(OriginalPattern);
        }

        private string[] SplitPattern(string pattern)
        {
            List<string> parts = new List<string>();
            int lastIndex = 0;
            
            // Find sequential placeholders {0}, {1}, {2}, ... in the pattern.
            // more than ten values. Keep a generous cap while avoiding unbounded
            // placeholder probing for malformed translation entries.
            for (int i = 0; i < 20; i++)
            {
                string placeholder = "{" + i + "}";
                int index = pattern.IndexOf(placeholder, lastIndex);
                
                if (index >= 0)
                {
                    // Add text before this placeholder
                    parts.Add(pattern.Substring(lastIndex, index - lastIndex));
                    lastIndex = index + placeholder.Length;
                }
                else
                {
                    // No more placeholders found - break early
                    break;
                }
            }
            
            // Add remaining text after last placeholder
            if (lastIndex < pattern.Length)
            {
                parts.Add(pattern.Substring(lastIndex));
            }
            else if (lastIndex == pattern.Length && parts.Count > 0)
            {
                // Pattern ends with a placeholder - add empty string
                parts.Add("");
            }
            
            return parts.ToArray();
        }

        /// <summary>
        /// Check if this pattern matches the given text and path
        /// </summary>
        public bool Matches(string text, string path)
        {
            // Check path matcher if defined
            if (PathMatcher != null && !PathMatcher(path))
                return false;
            
            // Check text matcher if defined
            if (TextMatcher != null && !TextMatcher(text))
                return false;
            
            // Mode-specific matching
            switch (Mode)
            {
                case TranslationMode.FsmPattern:
                case TranslationMode.FsmPatternWithTranslation:
                    return TryExtractFsmValues(text) != null;

                case TranslationMode.CustomHandler:
                    return CustomHandler != null;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Extract values from text and apply translation template
        /// Returns null if extraction failed
        /// </summary>
        public string TryTranslate(string text, string path, TranslationDictionary translations)
        {
            if (!Matches(text, path))
                return null;

            switch (Mode)
            {
                case TranslationMode.FsmPattern:
                    return TranslateWithFsmPattern(text);

                case TranslationMode.FsmPatternWithTranslation:
                    return TranslateWithFsmPatternAndTranslateParams(text, translations);
                case TranslationMode.CustomHandler:
                    if (CustomHandler != null)
                    {
                        var result = CustomHandler(text, path, translations);
                        return result.Success ? result.Result : null;
                    }
                    break;
            }

            return null;
        }

        private string TranslateWithFsmPattern(string text)
        {
            string[] values = TryExtractFsmValues(text);
            if (values == null)
                return null;
            
            string result = TranslatedTemplate;
            for (int i = 0; i < values.Length; i++)
            {
                result = result.Replace("{" + i + "}", values[i]);
            }
            
            return result;
        }

        private string TranslateWithFsmPatternAndTranslateParams(string text, TranslationDictionary translations)
        {
            string[] values = TryExtractFsmValues(text);
            if (values == null)
                return null;

            string result = TranslatedTemplate;
            for (int i = 0; i < values.Length; i++)
            {
                string originalValue = values[i];

                // Try to translate the parameter value
                string translatedValue = originalValue;
                string key = LocalizationUtils.FormatUpperKey(originalValue);

                if (translations != null && translations.TryGetByNormalizedKey(key, out string translation))
                {
                    translatedValue = translation;
                }

                result = result.Replace("{" + i + "}", translatedValue);
            }

            return result;
        }

        private string[] TryExtractFsmValues(string input)
        {
            if (originalParts == null || originalParts.Length == 0)
                return null;

            // originalParts holds the static text before {0}, between every
            // placeholder, and after the final one. Extract one value for every
            // separator, even if that value is empty: dynamic HUD strings can have
            // optional segments (for example, reputation stars between "mk" and a
            // newline). Omitting an empty segment shifts all later placeholders.
            string firstPart = originalParts[0];
            if (!input.StartsWith(firstPart))
                return null;

            List<string> values = new List<string>();
            string remaining = input.Substring(firstPart.Length);

            for (int i = 1; i < originalParts.Length; i++)
            {
                string part = originalParts[i];

                if (i == originalParts.Length - 1)
                {
                    if (!remaining.EndsWith(part))
                        return null;
                    values.Add(remaining.Substring(0, remaining.Length - part.Length));
                }
                else
                {
                    int idx = remaining.IndexOf(part);
                    if (idx < 0)
                        return null;
                    values.Add(remaining.Substring(0, idx));
                    remaining = remaining.Substring(idx + part.Length);
                }
            }

            return values.ToArray();
        }
    }
}
