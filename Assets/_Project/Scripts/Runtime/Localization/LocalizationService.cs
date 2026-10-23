using System;
using System.Collections.Generic;

namespace OutsiderDriver.Localization
{
    /// <summary>
    /// Core localization service implementing ILocalizationService.
    /// Manages multilingual string tables for English and Arabic with bidirectional RTL shaping
    /// (GAME_DESIGN §5, Decision P-019, Systems Architecture §7).
    /// </summary>
    public sealed class LocalizationService : ILocalizationService
    {
        public const string DefaultLocale = "en";
        public const string ArabicLocale = "ar";

        public const string TableUI = "UI";
        public const string TablePrompts = "Prompts";
        public const string TableDistricts = "Districts";
        public const string TableLanguages = "Languages";
        public const string TableDialogue = "Dialogue";

        // key: (table, key, locale) -> translated string
        private readonly Dictionary<(string table, string key, string locale), string> _stringTable =
            new Dictionary<(string table, string key, string locale), string>();

        private string _currentLocaleCode = DefaultLocale;
        private bool _isRtl = false;

        public string CurrentLocaleCode => _currentLocaleCode;
        public bool IsRtl => _isRtl;

        public event Action<string> OnLocaleChanged;

        public LocalizationService(string initialLocale = DefaultLocale)
        {
            RegisterDefaultStringTables();
            SetLocale(string.IsNullOrEmpty(initialLocale) ? DefaultLocale : initialLocale);
        }

        public void SetLocale(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                localeCode = DefaultLocale;
            }

            localeCode = localeCode.Trim().ToLowerInvariant();
            if (localeCode.Length > 2)
            {
                localeCode = localeCode.Substring(0, 2);
            }

            bool changed = !string.Equals(_currentLocaleCode, localeCode, StringComparison.OrdinalIgnoreCase);
            _currentLocaleCode = localeCode;
            _isRtl = IsRtlLanguage(localeCode);

            if (changed)
            {
                OnLocaleChanged?.Invoke(_currentLocaleCode);
            }
        }

        public string GetLocalizedString(string tableCollection, string entryKey, params object[] args)
        {
            if (string.IsNullOrEmpty(entryKey)) return string.Empty;
            if (string.IsNullOrEmpty(tableCollection)) tableCollection = TableUI;

            string normTable = tableCollection.Trim().ToLowerInvariant();
            string normKey = entryKey.Trim().ToLowerInvariant();

            string result;

            // 1. Try current locale
            if (!_stringTable.TryGetValue((normTable, normKey, _currentLocaleCode), out result))
            {
                // 2. Fallback to English
                if (!_stringTable.TryGetValue((normTable, normKey, DefaultLocale), out result))
                {
                    // 3. Fallback to entryKey
                    result = entryKey;
                }
            }

            if (args != null && args.Length > 0)
            {
                try
                {
                    result = string.Format(result, args);
                }
                catch (FormatException)
                {
                    // If format string fails, retain unformatted template
                }
            }

            return FormatTextForDisplay(result);
        }

        public string FormatTextForDisplay(string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return string.Empty;

            if (_isRtl || ArabicRtlShaper.ContainsArabic(rawText))
            {
                return ArabicRtlShaper.ShapeAndReorder(rawText);
            }

            return rawText;
        }

        public void RegisterString(string tableCollection, string entryKey, string localeCode, string translatedText)
        {
            if (string.IsNullOrEmpty(tableCollection) || string.IsNullOrEmpty(entryKey) || string.IsNullOrEmpty(localeCode))
            {
                return;
            }

            string normTable = tableCollection.Trim().ToLowerInvariant();
            string normKey = entryKey.Trim().ToLowerInvariant();
            string normalizedLocale = localeCode.Trim().ToLowerInvariant();
            if (normalizedLocale.Length > 2)
            {
                normalizedLocale = normalizedLocale.Substring(0, 2);
            }

            _stringTable[(normTable, normKey, normalizedLocale)] = translatedText ?? string.Empty;
        }

        public static bool IsRtlLanguage(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode)) return false;
            return localeCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ||
                   localeCode.StartsWith("fa", StringComparison.OrdinalIgnoreCase) ||
                   localeCode.StartsWith("he", StringComparison.OrdinalIgnoreCase) ||
                   localeCode.StartsWith("ur", StringComparison.OrdinalIgnoreCase);
        }

        private void RegisterDefaultStringTables()
        {
            // --- UI Toolkit HUD & Dialogue Elements ---
            RegisterString(TableUI, "ui_continue", "en", "Continue");
            RegisterString(TableUI, "ui_continue", "ar", "متابعة");

            RegisterString(TableUI, "ui_stay_silent", "en", "Stay Silent");
            RegisterString(TableUI, "ui_stay_silent", "ar", "التزام الصمت");

            RegisterString(TableUI, "hud_fare_credits", "en", "{0} Credits");
            RegisterString(TableUI, "hud_fare_credits", "ar", "{0} رصيد");

            RegisterString(TableUI, "hud_arrival_banner", "en", "Destination Reached");
            RegisterString(TableUI, "hud_arrival_banner", "ar", "تم الوصول إلى الوجهة");

            RegisterString(TableUI, "hud_untranslated_badge", "en", "Untranslated");
            RegisterString(TableUI, "hud_untranslated_badge", "ar", "غير مترجم");

            RegisterString(TableUI, "hud_translated_badge", "en", "Translated");
            RegisterString(TableUI, "hud_translated_badge", "ar", "مترجم");

            // --- Everyday Domestic & Neighborhood Prompts ---
            RegisterString(TablePrompts, "prompt_board_taxi", "en", "Board / Drive Taxi");
            RegisterString(TablePrompts, "prompt_board_taxi", "ar", "ركوب سيارة الأجرة");

            RegisterString(TablePrompts, "prompt_exit_taxi", "en", "Exit Taxi");
            RegisterString(TablePrompts, "prompt_exit_taxi", "ar", "النزول من سيارة الأجرة");

            RegisterString(TablePrompts, "prompt_home_bed", "en", "Rest / Pass Time");
            RegisterString(TablePrompts, "prompt_home_bed", "ar", "الراحة / قضاء الوقت");

            RegisterString(TablePrompts, "prompt_apartment_door", "en", "Enter / Exit Apartment");
            RegisterString(TablePrompts, "prompt_apartment_door", "ar", "دخول / خروج من الشقة");

            RegisterString(TablePrompts, "prompt_translation_bench", "en", "Translator & Radio Console");
            RegisterString(TablePrompts, "prompt_translation_bench", "ar", "جهاز الترجمة والراديو");

            // --- District Names ---
            RegisterString(TableDistricts, "district_amber_terrace", "en", "Amber Terrace");
            RegisterString(TableDistricts, "district_amber_terrace", "ar", "شرفة الكهرمان");

            RegisterString(TableDistricts, "district_central_junction", "en", "Central Junction");
            RegisterString(TableDistricts, "district_central_junction", "ar", "المفترق المركزي");

            RegisterString(TableDistricts, "district_market_square", "en", "Market Square");
            RegisterString(TableDistricts, "district_market_square", "ar", "ساحة السوق");

            RegisterString(TableDistricts, "district_riverside_vista", "en", "Riverside Vista");
            RegisterString(TableDistricts, "district_riverside_vista", "ar", "إطلالة النهر");

            RegisterString(TableDistricts, "district_transit_terminal", "en", "Transit Terminal");
            RegisterString(TableDistricts, "district_transit_terminal", "ar", "محطة العبور");

            // --- Alien City Languages ---
            RegisterString(TableLanguages, "lang_trade_cant", "en", "Trade Cant");
            RegisterString(TableLanguages, "lang_trade_cant", "ar", "لهجة التجارة");

            RegisterString(TableLanguages, "lang_common", "en", "Common Alien");
            RegisterString(TableLanguages, "lang_common", "ar", "الكلام المشترك");

            RegisterString(TableLanguages, "lang_local", "en", "Local Dialect");
            RegisterString(TableLanguages, "lang_local", "ar", "اللهجة المحلية");
        }
    }
}
