using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Audio
{
    /// <summary>
    /// Authored data definition for an in-world alien radio station (GAME_DESIGN §5).
    /// Provides strange/beautiful local music, talk, culture, news, and traffic atmosphere.
    /// </summary>
    [CreateAssetMenu(fileName = "RadioStation_", menuName = "Outsider Driver/Audio/Radio Station")]
    public class RadioStationSO : ScriptableObject
    {
        [Header("Station Identity")]
        [SerializeField] private string _stationId = "station_amber_wave";
        [SerializeField] private string _stationName = "Amber Wave Transit Radio";
        [SerializeField] private string _frequencyLabel = "94.6 MHz";
        [Tooltip("Primary broadcast language ID (e.g., 'Common', 'Local', 'TradeCant').")]
        [SerializeField] private string _primaryLanguageId = "Common";

        [Header("Broadcast Schedule")]
        [SerializeField] private List<RadioProgramEntry> _programs = new List<RadioProgramEntry>();

        public string StationId => _stationId;
        public string StationName => _stationName;
        public string FrequencyLabel => _frequencyLabel;
        public string PrimaryLanguageId => _primaryLanguageId;
        public IReadOnlyList<RadioProgramEntry> Programs => _programs;

        public void Configure(
            string stationId,
            string stationName,
            string frequencyLabel,
            string primaryLanguageId,
            IEnumerable<RadioProgramEntry> programs = null)
        {
            _stationId = stationId ?? string.Empty;
            _stationName = stationName ?? string.Empty;
            _frequencyLabel = frequencyLabel ?? string.Empty;
            _primaryLanguageId = primaryLanguageId ?? string.Empty;

            _programs.Clear();
            if (programs != null)
            {
                _programs.AddRange(programs);
            }
        }

        /// <summary>
        /// Resolves the broadcast program scheduled for the specified in-game hour (0..23).
        /// Falls back to the first program if no specific hour window matches.
        /// </summary>
        public bool TryGetProgramForHour(int hour, out RadioProgramEntry activeProgram)
        {
            for (int i = 0; i < _programs.Count; i++)
            {
                if (_programs[i].IsActiveAtHour(hour))
                {
                    activeProgram = _programs[i];
                    return true;
                }
            }

            if (_programs.Count > 0)
            {
                activeProgram = _programs[0];
                return true;
            }

            activeProgram = default;
            return false;
        }
    }
}
