using System;

namespace OutsiderDriver.Localization
{
    /// <summary>
    /// Core localization service handling multilingual string tables, locale switching,
    /// and bidirectional Arabic RTL text shaping for UI Toolkit labels and HUD prompts
    /// (GAME_DESIGN §5, Decision P-019, Systems Architecture §7).
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Current ISO 639-1 language code (e.g., "en", "ar").
        /// </summary>
        string CurrentLocaleCode { get; }

        /// <summary>
        /// True if the current language renders Right-To-Left (e.g., Arabic).
        /// </summary>
        bool IsRtl { get; }

        /// <summary>
        /// Fired whenever the active locale changes.
        /// </summary>
        event Action<string> OnLocaleChanged;

        /// <summary>
        /// Sets the active locale (e.g., "en" or "ar") and updates RTL state.
        /// </summary>
        void SetLocale(string localeCode);

        /// <summary>
        /// Retrieves a localized string from a table collection by key, formatting optional arguments.
        /// Falls back to English or the key itself if not found.
        /// </summary>
        string GetLocalizedString(string tableCollection, string entryKey, params object[] args);

        /// <summary>
        /// Formats any string for display in UI Toolkit elements, applying Arabic contextual letter shaping
        /// and bidirectional reordering when the current language is RTL.
        /// </summary>
        string FormatTextForDisplay(string rawText);

        /// <summary>
        /// Registers or overrides a string entry in the in-memory localization table.
        /// </summary>
        void RegisterString(string tableCollection, string entryKey, string localeCode, string translatedText);
    }
}
