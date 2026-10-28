using System;
using System.Collections.Generic;
using VContainer;

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

        [Inject]
        public LocalizationService() : this(DefaultLocale)
        {
        }

        public LocalizationService(string initialLocale)
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

            // --- Taxi Shift HUD (Placeholder currency label; currency remains OPEN) ---
            RegisterString(TableUI, "hud_taxi_shift_title", "en", "Taxi Shift");
            RegisterString(TableUI, "hud_taxi_shift_title", "ar", "وردية الأجرة");

            RegisterString(TableUI, "hud_status_offers", "en", "{0} fares waiting");
            RegisterString(TableUI, "hud_status_offers", "ar", "{0} رحلات بانتظارك");

            RegisterString(TableUI, "hud_status_idle", "en", "Waiting for a fare");
            RegisterString(TableUI, "hud_status_idle", "ar", "بانتظار راكب");

            RegisterString(TableUI, "hud_status_no_work", "en", "No fares right now");
            RegisterString(TableUI, "hud_status_no_work", "ar", "لا توجد رحلات الآن");

            RegisterString(TableUI, "hud_pickup_row", "en", "Pickup · {0}");
            RegisterString(TableUI, "hud_pickup_row", "ar", "الالتقاط · {0}");

            RegisterString(TableUI, "hud_destination_row", "en", "Destination · {0}");
            RegisterString(TableUI, "hud_destination_row", "ar", "الوجهة · {0}");

            RegisterString(TableUI, "hud_distance_m", "en", "{0} m");
            RegisterString(TableUI, "hud_distance_m", "ar", "{0} م");

            RegisterString(TableUI, "hud_street_stop", "en", "{0} street stop");
            RegisterString(TableUI, "hud_street_stop", "ar", "موقف {0}");

            RegisterString(TableUI, "hud_accept_offer", "en", "Accept");
            RegisterString(TableUI, "hud_accept_offer", "ar", "قبول");

            RegisterString(TableUI, "hud_decline_offer", "en", "Decline");
            RegisterString(TableUI, "hud_decline_offer", "ar", "رفض");

            RegisterString(TableUI, "hud_board_passenger", "en", "Board passenger");
            RegisterString(TableUI, "hud_board_passenger", "ar", "اصطحاب الراكب");

            RegisterString(TableUI, "hud_collect_fare", "en", "Collect fare");
            RegisterString(TableUI, "hud_collect_fare", "ar", "استلام الأجرة");

            RegisterString(TableUI, "hud_active_fare", "en", "Active fare");
            RegisterString(TableUI, "hud_active_fare", "ar", "الرحلة الجارية");

            RegisterString(TableUI, "hud_phase_enroute", "en", "En route to pickup");
            RegisterString(TableUI, "hud_phase_enroute", "ar", "في الطريق إلى نقطة الالتقاط");

            RegisterString(TableUI, "hud_phase_boarding", "en", "Passenger waiting at the curb");
            RegisterString(TableUI, "hud_phase_boarding", "ar", "الراكب ينتظر عند الرصيف");

            RegisterString(TableUI, "hud_phase_in_transit", "en", "Driving to destination");
            RegisterString(TableUI, "hud_phase_in_transit", "ar", "في الطريق إلى الوجهة");

            RegisterString(TableUI, "hud_phase_arrived", "en", "Arrived — collect the fare");
            RegisterString(TableUI, "hud_phase_arrived", "ar", "وصلت — استلم الأجرة");

            RegisterString(TableUI, "hud_offer_unavailable", "en", "That fare is no longer available.");
            RegisterString(TableUI, "hud_offer_unavailable", "ar", "هذه الرحلة لم تعد متاحة.");

            RegisterString(TableUI, "hud_ride_start_failed", "en", "That fare could not start. Try another.");
            RegisterString(TableUI, "hud_ride_start_failed", "ar", "تعذر بدء الرحلة. جرّب رحلة أخرى.");

            RegisterString(TableUI, "hud_fare_collected", "en", "Fare collected: {0}");
            RegisterString(TableUI, "hud_fare_collected", "ar", "تم تحصيل {0}");

            RegisterString(TableUI, "hud_ride_cancelled", "en", "Ride ended without payment.");
            RegisterString(TableUI, "hud_ride_cancelled", "ar", "انتهت الرحلة دون دفع.");

            RegisterString(TableUI, "hud_no_work_title", "en", "No fares right now");
            RegisterString(TableUI, "hud_no_work_title", "ar", "لا توجد رحلات الآن");

            RegisterString(TableUI, "hud_no_work_hint", "en", "New requests arrive as the city moves through its day.");
            RegisterString(TableUI, "hud_no_work_hint", "ar", "تصل الطلبات الجديدة مع تقدم المدينة في يومها.");

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
