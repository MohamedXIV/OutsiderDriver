using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Core.State;

namespace OutsiderDriver.Data
{
    /// <summary>
    /// Data-driven ScriptableObject defining an authored city district's visual atmosphere,
    /// traffic/pedestrian budget, base fare rates, and local radio/language characteristics.
    /// </summary>
    [CreateAssetMenu(fileName = "DistrictConfig", menuName = "Outsider Driver/Data/District Config")]
    public class DistrictConfigSO : ScriptableObject
    {
        [Header("District Identity")]
        [SerializeField] private string _districtId = "district_amber_terrace";
        [Tooltip("Localization key or fallback display name for this district.")]
        [SerializeField] private string _displayName = "Amber Terrace";
        [SerializeField] private string _dominantLanguageId = PlayerProfileData.DefaultCommonLanguageId;

        [Header("Atmosphere & Visual Palette (Decision P-010)")]
        [Tooltip("Warm retro-futurist surface/ambient tint associated with this neighborhood.")]
        [SerializeField] private Color _ambientTintColor = new Color(0.95f, 0.85f, 0.72f, 1f);
        [Tooltip("Default local radio frequency or audio station ID when driving through this district.")]
        [SerializeField] private string _defaultRadioStationId = "station_civic_wave";

        [Header("Traffic & Fare Parameters")]
        [Range(0, 16)]
        [SerializeField] private int _targetAmbientVehicles = 5;
        [Min(0)]
        [SerializeField] private int _basePickupFare = 15;
        [Min(0f)]
        [SerializeField] private float _creditsPerUnitDistance = 2.0f;

        [Header("Notable Landmarks")]
        [Tooltip("RoadNode IDs belonging to this district for route and schedule resolution.")]
        [SerializeField] private List<string> _landmarkNodeIds = new List<string>();

        public string DistrictId => _districtId;
        public string DisplayName => _displayName;
        public string DominantLanguageId => _dominantLanguageId;
        public Color AmbientTintColor => _ambientTintColor;
        public string DefaultRadioStationId => _defaultRadioStationId;
        public int TargetAmbientVehicles => _targetAmbientVehicles;
        public int BasePickupFare => _basePickupFare;
        public float CreditsPerUnitDistance => _creditsPerUnitDistance;
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
