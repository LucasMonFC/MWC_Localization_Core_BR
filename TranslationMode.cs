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
}
