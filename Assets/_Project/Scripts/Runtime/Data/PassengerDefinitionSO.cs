using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Core.State;

namespace OutsiderDriver.Data
{
    /// <summary>
    /// Data-driven ScriptableObject defining an alien resident / passenger's static identity,
    /// language, portrait visuals, fare parameters, Ink story asset, and coarse daily schedule.
    /// Runtime mutable state (affinity, trust, suspicion) is stored separately in ResidentRelationshipData.
    /// </summary>
    [CreateAssetMenu(fileName = "PassengerDefinition", menuName = "Outsider Driver/Data/Passenger Definition")]
    public class PassengerDefinitionSO : ScriptableObject
    {
        [Header("Identity & Culture")]
        [SerializeField] private string _passengerId = "passenger_default";
        [Tooltip("Localization key or fallback display name for this resident.")]
        [SerializeField] private string _displayName = "Unknown Resident";
        [Tooltip("Alien species identifier (kept independent from language and culture per H-007).")]
        [SerializeField] private string _speciesId = "Vesperian";
        [Tooltip("Cultural community or social background identifier (H-007).")]
        [SerializeField] private string _cultureId = "OldRingCivic";
        [Tooltip("Primary language spoken by this passenger (e.g., 'Common', 'Vesperian', 'Krellish').")]
        [SerializeField] private string _nativeLanguageId = PlayerProfileData.DefaultCommonLanguageId;

        [Header("Presentation & Narrative")]
        [SerializeField] private Sprite _portraitSprite;
        [SerializeField] private Sprite _worldSprite;
        [Tooltip("Compiled Ink JSON asset containing contextual dialogue knots for this passenger.")]
        [SerializeField] private TextAsset _inkStoryJson;
        [Tooltip("Default entry knot in the Ink story when boarding the taxi.")]
        [SerializeField] private string _defaultEntryKnot = "ride_start";

        [Header("Ride & Fare Profile")]
        [Min(0.1f)]
        [SerializeField] private float _fareMultiplier = 1.0f;
        [Range(0f, 1f)]
        [SerializeField] private float _tipGenerosity = 0.25f;

        [Header("Daily Schedule (Coarse Tick Virtualization)")]
        [SerializeField] private List<PassengerScheduleEntry> _schedule = new List<PassengerScheduleEntry>();

        public string PassengerId => _passengerId;
        public string DisplayName => _displayName;
        public string SpeciesId => _speciesId;
        public string CultureId => _cultureId;
        public string NativeLanguageId => string.IsNullOrWhiteSpace(_nativeLanguageId)
            ? PlayerProfileData.DefaultCommonLanguageId
            : _nativeLanguageId;

        public Sprite PortraitSprite => _portraitSprite;
        public Sprite WorldSprite => _worldSprite;
        public TextAsset InkStoryJson => _inkStoryJson;
        public string DefaultEntryKnot => _defaultEntryKnot;
        public float FareMultiplier => _fareMultiplier;
        public float TipGenerosity => _tipGenerosity;
        public IReadOnlyList<PassengerScheduleEntry> Schedule => _schedule;

        /// <summary>
        /// Configures core fields programmatically (useful for Editor asset builders and EditMode unit tests).
        /// </summary>
        public void Initialize(
            string passengerId,
            string displayName,
            string speciesId,
            string nativeLanguageId,
            float fareMultiplier = 1.0f,
            float tipGenerosity = 0.25f,
            TextAsset inkStoryJson = null)
        {
            _passengerId = passengerId;
            _displayName = displayName;
            _speciesId = speciesId;
            _nativeLanguageId = nativeLanguageId;
            _fareMultiplier = Mathf.Max(0.1f, fareMultiplier);
            _tipGenerosity = Mathf.Clamp01(tipGenerosity);
            _inkStoryJson = inkStoryJson;
        }

        public void SetSchedule(IEnumerable<PassengerScheduleEntry> entries)
        {
            _schedule.Clear();
            if (entries != null)
            {
                _schedule.AddRange(entries);
            }
        }

        /// <summary>
        /// Resolves the first matching schedule entry for the given hour (0-23).
        /// </summary>
        public bool TryGetScheduleForHour(int hour, out PassengerScheduleEntry matchedEntry)
        {
            for (int i = 0; i < _schedule.Count; i++)
            {
                if (_schedule[i].IsActiveAtHour(hour))
                {
                    matchedEntry = _schedule[i];
                    return true;
                }
            }

            matchedEntry = default;
            return false;
        }
    }
}
