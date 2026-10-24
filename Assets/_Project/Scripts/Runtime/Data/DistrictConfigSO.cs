using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Core.State;

namespace OutsiderDriver.Data
{
    /// <summary>
    /// Data-driven ScriptableObject defining an authored city district's stable identity,
    /// development vs. alien naming metadata, atmosphere, traffic/fare settings, and linked locations/road nodes
    /// (Decisions P-010, P-028, P-029; WORLD_CONTENT_DATA_CONTRACT.md).
    /// </summary>
    [CreateAssetMenu(fileName = "DistrictConfig", menuName = "Outsider Driver/Data/District Config")]
    public class DistrictConfigSO : ScriptableObject
    {
        [Header("District Identity (Decision P-028)")]
        [Tooltip("Stable opaque district identifier (e.g. district_01, district_02). Never encode temporary names here.")]
        [SerializeField] private string _districtId = "district_01";

        [Header("Naming & Provenance (Decision P-029 & Open O-027)")]
        [Tooltip("Developer-facing English functional label (e.g. 'Outer Home & Workshop Quarter'). Not diegetic canon.")]
        [SerializeField] private string _workingName = "Outer Home & Workshop Quarter";
        [Tooltip("Localization String Table key for player-facing display.")]
        [SerializeField] private string _displayNameKey = "world.district_01.name";
        [Tooltip("Fallback or transliterated alien display name.")]
        [SerializeField] private string _displayName = "Vesh-Kora";
        [Tooltip("False while final alien toponyms remain OPEN (Decision O-027).")]
        [SerializeField] private bool _isNameApproved = false;
        [Tooltip("Draft alien-language candidate names under editorial review (never English compounds).")]
        [SerializeField] private List<string> _alienNameCandidates = new List<string>();
        [SerializeField] private string _dominantLanguageId = PlayerProfileData.DefaultCommonLanguageId;

        [Header("Descriptions & Lore")]
        [TextArea(2, 4)]
        [SerializeField] private string _summary = "";
        [TextArea(2, 5)]
        [SerializeField] private string _backgroundLore = "";

        [Header("Spatial Footprint")]
        [Tooltip("Authored 2D world-space bounding box of this district.")]
        [SerializeField] private Rect _districtBounds = new Rect(-6f, -2f, 20f, 12f);

        [Header("Atmosphere & Visual Palette (Decision P-010)")]
        [Tooltip("Warm retro-futurist surface/ambient tint associated with this neighborhood.")]
        [SerializeField] private Color _ambientTintColor = new Color(0.95f, 0.85f, 0.72f, 1f);
        [Tooltip("Default local radio frequency or audio station ID when driving through this district.")]
        [SerializeField] private string _defaultRadioStationId = "station_amber_wave";

        [Header("Traffic & Fare Parameters")]
        [Range(0, 16)]
        [SerializeField] private int _targetAmbientVehicles = 5;
        [Min(0)]
        [SerializeField] private int _basePickupFare = 15;
        [Min(0f)]
        [SerializeField] private float _creditsPerUnitDistance = 2.0f;

        [Header("Authored Links (Stable IDs)")]
        [Tooltip("Stable WorldLocationSO IDs belonging to this district (e.g. location_001).")]
        [SerializeField] private List<string> _locationIds = new List<string>();
        [Tooltip("RoadNode IDs belonging to this district for route and schedule resolution.")]
        [SerializeField] private List<string> _landmarkNodeIds = new List<string>();

        public string DistrictId => _districtId;
        public string WorkingName => _workingName;
        public string DisplayNameKey => _displayNameKey;
        public string DisplayName => _displayName;
        public bool IsNameApproved => _isNameApproved;
        public IReadOnlyList<string> AlienNameCandidates => _alienNameCandidates;
        public string DominantLanguageId => _dominantLanguageId;
        public string Summary => _summary;
        public string BackgroundLore => _backgroundLore;
        public Rect DistrictBounds => _districtBounds;
        public Color AmbientTintColor => _ambientTintColor;
        public string DefaultRadioStationId => _defaultRadioStationId;
        public int TargetAmbientVehicles => _targetAmbientVehicles;
        public int BasePickupFare => _basePickupFare;
        public float CreditsPerUnitDistance => _creditsPerUnitDistance;
        public IReadOnlyList<string> LocationIds => _locationIds;
        public IReadOnlyList<string> LandmarkNodeIds => _landmarkNodeIds;

        public void Initialize(
            string districtId,
            string displayName,
            int targetAmbientVehicles,
            int basePickupFare,
            float creditsPerUnitDistance,
            IEnumerable<string> landmarkNodeIds = null)
        {
            _districtId = districtId;
            _displayName = displayName;
            _targetAmbientVehicles = Mathf.Clamp(targetAmbientVehicles, 0, 16);
            _basePickupFare = Mathf.Max(0, basePickupFare);
            _creditsPerUnitDistance = Mathf.Max(0f, creditsPerUnitDistance);

            _landmarkNodeIds.Clear();
            if (landmarkNodeIds != null)
            {
                _landmarkNodeIds.AddRange(landmarkNodeIds);
            }
        }

        public void ConfigureAuthoringMetadata(
            string workingName,
            string displayNameKey,
            string summary,
            string backgroundLore,
            Rect districtBounds,
            Color ambientTintColor,
            string dominantLanguageId,
            string defaultRadioStationId,
            IEnumerable<string> alienNameCandidates = null,
            IEnumerable<string> locationIds = null,
            bool isNameApproved = false)
        {
            _workingName = workingName ?? string.Empty;
            _displayNameKey = displayNameKey ?? string.Empty;
            _summary = summary ?? string.Empty;
            _backgroundLore = backgroundLore ?? string.Empty;
            _districtBounds = districtBounds;
            _ambientTintColor = ambientTintColor;
            _dominantLanguageId = string.IsNullOrEmpty(dominantLanguageId) ? PlayerProfileData.DefaultCommonLanguageId : dominantLanguageId;
            _defaultRadioStationId = defaultRadioStationId ?? string.Empty;
            _isNameApproved = isNameApproved;

            _alienNameCandidates.Clear();
            if (alienNameCandidates != null)
            {
                _alienNameCandidates.AddRange(alienNameCandidates);
            }

            _locationIds.Clear();
            if (locationIds != null)
            {
                _locationIds.AddRange(locationIds);
            }
        }

        /// <summary>
        /// Calculates the raw fare for a ride of the given world distance within this district,
        /// scaled by an optional passenger fare multiplier.
        /// </summary>
        public int CalculateEstimatedFare(float travelDistance, float passengerFareMultiplier = 1.0f)
        {
            float distanceCost = Mathf.Max(0f, travelDistance) * _creditsPerUnitDistance;
            float total = (_basePickupFare + distanceCost) * Mathf.Max(0.1f, passengerFareMultiplier);
            return Mathf.RoundToInt(total);
        }
    }
}
