using MSCLoader;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Handles TextMesh translation, font application, and position adjustments
    /// Centralizes all translation logic for better maintainability
    /// </summary>
    public class TextMeshTranslator
    {
        private Dictionary<string, string> translations;
        private Dictionary<string, Font> customFonts;
        private MagazineTextHandler magazineHandler;
        private PatternMatcher patternMatcher;
        private LocalizationConfig config;

        private List<string> ExcludedPath = new List<string>
        {
            "HOMENEW/Functions/FunctionsDisable/Stereos/Player/Screen/Settings/Bass/LCD",
            "CARPARTS/VINPlate",
            "Sheets/ServiceBrochure/PagePaintRims/Buttons/CustomColors",
            "Sheets/ServiceBrochure/PagePaintCar/Buttons/CustomColors"
        };

        public TextMeshTranslator(
            Dictionary<string, string> translations,
            Dictionary<string, Font> customFonts,
            MagazineTextHandler magazineHandler,
            LocalizationConfig config
        )
        {
            this.translations = translations;
            this.customFonts = customFonts;
            this.magazineHandler = magazineHandler;
            this.config = config;
            
            // Initialize unified pattern matcher
            this.patternMatcher = new PatternMatcher(translations);
        }

        /// <summary>
        /// Translate TextMesh and apply custom font + position adjustments
        /// </summary>
        /// <param name="translatedTextMeshes">HashSet tracking which TextMesh objects have been translated</param>
        /// <returns>True if text was translated or already localized</returns>
        public bool TranslateAndApplyFont(TextMesh textMesh, string path, HashSet<TextMesh> translatedTextMeshes)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            // Skip if already translated (language-agnostic check)
            if (translatedTextMeshes != null && translatedTextMeshes.Contains(textMesh))
                return true;

            // Skip excluded paths
            foreach(string excluded in ExcludedPath)
            {
                if (path.StartsWith(excluded))
                    return false;
            }

            // Try complex text handling first (e.g., magazine text, cashier price)
            if (HandleComplexTextMesh(textMesh, path))
            {
                // Complex text was handled, apply font and position
                ApplyCustomFont(textMesh, path);
                return true;
            }

            // Use standard translation
            if (ApplyTranslation(textMesh, path))
                return true;

            return false;
        }

        /// <summary>
        /// Apply custom font and position adjustment to TextMesh
        /// </summary>
        public void ApplyCustomFont(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return;

            // Get custom font based on original font name
            string originalFontName = textMesh.font != null ? textMesh.font.name : "unknown";
            Font customFont = GetCustomFont(originalFontName);

            if (customFont != null)
            {
                textMesh.font = customFont;
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && customFont.material != null && customFont.material.mainTexture != null)
                {
                    renderer.material.mainTexture = customFont.material.mainTexture;
                }
                config.ApplyTextAdjustment(textMesh, path);
            }
        }

        /// <summary>
        /// Handle complex text patterns (magazine text, cashier price line)
        /// Now uses unified pattern matcher for all pattern-based translations
        /// </summary>
        bool HandleComplexTextMesh(TextMesh textMesh, string path)
        {
            // Check magazine text FIRST - it requires special handling (comma-separated words, price/phone format)
            if (magazineHandler.IsMagazineText(path))
            {
                return magazineHandler.HandleMagazineText(textMesh);
            }
            
            // Try pattern matching for other complex texts (FSM, Price patterns)
            string patternResult = patternMatcher.TryTranslateWithPattern(textMesh.text, path);
            if (patternResult != null)
            {
                textMesh.text = patternResult;
                return true;
            }

            return false; // Not handled, use standard translation
        }

        /// <summary>
        /// Apply standard translation to TextMesh
        /// </summary>
        /// <param name="forceUpdate">Force update even if text hasn't changed</param>
        public bool ApplyTranslation(TextMesh textMesh, string path, bool forceUpdate = false)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            // Normalize current text for lookup
            string currentText = textMesh.text;
            string normalizedKey = MLCUtils.FormatUpperKey(currentText);

            // Check if translation exists
            if (!translations.TryGetValue(normalizedKey, out string translation))
                return false;

            // Skip translation if already translated (unless forced)
            if (!forceUpdate && currentText == translation)
                return false;

            // Apply custom font first
            ApplyCustomFont(textMesh, path);

            // Apply translation
            textMesh.text = translation;

            return true;
        }

        /// <summary>
        /// Get custom font for the given original font name
        /// </summary>
        Font GetCustomFont(string originalFontName)
        {
            // First try direct match
            if (customFonts.ContainsKey(originalFontName))
            {
                return customFonts[originalFontName];
            }

            // Use original if it exists in the dictionary as value
            else if (customFonts.Values.Any(f => f.name == originalFontName))
            {
                return customFonts.Values.FirstOrDefault(f => f.name == originalFontName);
            }

            // Return first loaded font as fallback
            //else if (customFonts.Count > 0)
            //{
            //    return customFonts.Values.First();
            //}

            return null;
        }

        /// <summary>
        /// Apply custom font without translating text (for teletext display)
        /// </summary>
        public bool ApplyFontOnly(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return false;

            string originalFontName = textMesh.font != null ? textMesh.font.name : "unknown";
            Font customFont = GetCustomFont(originalFontName);

            if (customFont != null)
            {
                textMesh.font = customFont;

                // Update material texture
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null && customFont.material != null && customFont.material.mainTexture != null)
                {
                    renderer.material.mainTexture = customFont.material.mainTexture;
                }

                // Adjust position for better rendering with Korean font
                config.ApplyTextAdjustment(textMesh, path);
                
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Load FSM patterns from teletext translation file
        /// </summary>
        public void LoadFsmPatterns(string filePath)
        {
            patternMatcher.LoadPatternsFromFile(filePath);
        }
    }
}
