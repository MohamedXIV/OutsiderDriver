using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Data
{
    /// <summary>
    /// Functional categories for authored city buildings and locations.
    /// </summary>
    public enum WorldLocationCategory
    {
        PlayerHomeGarage = 0,
        WorkshopService = 1,
        ResidentialBlock = 2,
        CommercialMarket = 3,
        CivicTransit = 4
    }

    /// <summary>
    /// Data-driven ScriptableObject defining an authored world location or building
    /// (Decisions P-028, P-029; WORLD_CONTENT_DATA_CONTRACT.md).
    /// Separates stable identity (e.g. location_001) from development labels, alien name candidates,
    /// localization keys, road node connectivity, and scene spatial placement.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldLocation", menuName = "Outsider Driver/Data/World Location")]
    public class WorldLocationSO : ScriptableObject
    {
        [Header("Stable Identity (Decision P-028)")]
        [Tooltip("Stable opaque location ID (e.g. location_001). Never encode display names here.")]
        [SerializeField] private string _locationId = "location_001";
        [Tooltip("Stable district ID containing this location (e.g. district_01).")]
        [SerializeField] private string _districtId = "district_01";
        [SerializeField] private WorldLocationCategory _category = WorldLocationCategory.PlayerHomeGarage;

        [Header("Naming & Provenance (Decision P-029 & Open O-027)")]
        [Tooltip("Developer-facing English functional label (e.g. 'Driver Two-Story Home & Garage'). Development only.")]
        [SerializeField] private string _workingName = "Driver's Two-Story Home & Garage";
        [Tooltip("Localization String Table key for player-facing UI.")]
        [SerializeField] private string _displayNameKey = "world.location_001.name";
        [Tooltip("Transliterated alien name or draft placeholder.")]
        [SerializeField] private string _displayName = "Tarn-Vesh 4";
        [Tooltip("False while final alien toponyms remain OPEN (Decision O-027).")]
        [SerializeField] private bool _isNameApproved = false;
        [Tooltip("Draft alien-language candidate names under editorial review.")]
        [SerializeField] private List<string> _alienNameCandidates = new List<string>();

        [Header("Description & Architectural Metadata")]
        [TextArea(2, 4)]
        [SerializeField] private string _summary = "";
        [Min(1)]
        [Tooltip("Number of stories/floors in this structure (player home + garage is strictly 2 floors).")]
        [SerializeField] private int _floorCount = 2;

        [Header("Road Connectivity")]
        [Tooltip("Stable RoadNode ID where vehicles pick up, drop off, or park for this location.")]
        [SerializeField] private string _connectedRoadNodeId = "node_d01_home_bay";

        public string LocationId => _locationId;
        public string DistrictId => _districtId;
        public WorldLocationCategory Category => _category;
        public string WorkingName => _workingName;
        public string DisplayNameKey => _displayNameKey;
        public string DisplayName => _displayName;
        public bool IsNameApproved => _isNameApproved;
        public IReadOnlyList<string> AlienNameCandidates => _alienNameCandidates;
        public string Summary => _summary;
        public int FloorCount => _floorCount;
        public string ConnectedRoadNodeId => _connectedRoadNodeId;

        public void Configure(
            string locationId,
            string districtId,
            WorldLocationCategory category,
            string workingName,
            string displayNameKey,
            string displayName,
            string connectedRoadNodeId,
            int floorCount = 1,
            string summary = "",
            IEnumerable<string> alienNameCandidates = null,
            bool isNameApproved = false)
        {
            _locationId = locationId ?? string.Empty;
            _districtId = districtId ?? string.Empty;
            _category = category;
            _workingName = workingName ?? string.Empty;
            _displayNameKey = displayNameKey ?? string.Empty;
            _displayName = displayName ?? string.Empty;
            _connectedRoadNodeId = connectedRoadNodeId ?? string.Empty;
            _floorCount = Mathf.Max(1, floorCount);
            _summary = summary ?? string.Empty;
            _isNameApproved = isNameApproved;

            _alienNameCandidates.Clear();
            if (alienNameCandidates != null)
            {
                _alienNameCandidates.AddRange(alienNameCandidates);
            }
        }
    }
}
