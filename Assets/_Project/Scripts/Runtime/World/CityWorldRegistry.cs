using System;
using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.World
{
    /// <summary>
    /// Scene-scoped registry of authored districts, locations/buildings, and road network links
    /// (Decisions P-014, P-028, P-029; WORLD_CONTENT_DATA_CONTRACT.md).
    /// Provides lookup by stable opaque IDs and validates content/spatial integrity without
    /// duplicating mutable coordinates.
    /// </summary>
    [DisallowMultipleComponent]
    public class CityWorldRegistry : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RoadNetwork _roadNetwork;
        [SerializeField] private HomeNeighborhoodLayout _homeNeighborhoodLayout;

        [Header("Authored Districts (Stable IDs: district_01, district_02, ...)")]
        [SerializeField] private List<DistrictConfigSO> _districts = new List<DistrictConfigSO>();

        [Header("Key Production Locations")]
        [SerializeField] private CityLocationAnchor _playerHomeGarageAnchor;
        [SerializeField] private CityLocationAnchor _primaryCommercialDestinationAnchor;

        private readonly Dictionary<string, DistrictConfigSO> _districtsById = new Dictionary<string, DistrictConfigSO>(StringComparer.Ordinal);
        private readonly Dictionary<string, CityLocationAnchor> _locationsById = new Dictionary<string, CityLocationAnchor>(StringComparer.Ordinal);
        private readonly List<CityLocationAnchor> _allLocations = new List<CityLocationAnchor>();
        private readonly List<TaxiStopAnchor> _allTaxiStops = new List<TaxiStopAnchor>();
        private readonly HashSet<string> _duplicateTaxiStopIds = new HashSet<string>(StringComparer.Ordinal);

        public RoadNetwork RoadNetwork => _roadNetwork;
        public HomeNeighborhoodLayout HomeNeighborhoodLayout => _homeNeighborhoodLayout;
        public IReadOnlyList<DistrictConfigSO> Districts => _districts;
        public IReadOnlyList<CityLocationAnchor> AllLocations => _allLocations;
        /// <summary>Includes invalid and disabled authoring for diagnostics; query methods filter eligibility.</summary>
        public IReadOnlyList<TaxiStopAnchor> AllTaxiStops => _allTaxiStops;
        public CityLocationAnchor PlayerHomeGarageAnchor => _playerHomeGarageAnchor;
        public CityLocationAnchor PrimaryCommercialDestinationAnchor => _primaryCommercialDestinationAnchor;

        private void Awake()
        {
            RebuildRegistry();
        }

        public void Configure(
            RoadNetwork roadNetwork,
            HomeNeighborhoodLayout homeNeighborhoodLayout,
            IEnumerable<DistrictConfigSO> districts,
            CityLocationAnchor playerHomeGarageAnchor = null,
            CityLocationAnchor primaryCommercialDestinationAnchor = null)
        {
            _roadNetwork = roadNetwork;
            _homeNeighborhoodLayout = homeNeighborhoodLayout;

            _districts.Clear();
            if (districts != null)
            {
                _districts.AddRange(districts);
            }

            _playerHomeGarageAnchor = playerHomeGarageAnchor;
            _primaryCommercialDestinationAnchor = primaryCommercialDestinationAnchor;
            RebuildRegistry();
        }

        /// <summary>
        /// Indexes all authored DistrictConfigSO assets and scene CityLocationAnchor components,
        /// resolving their road node references against the RoadNetwork.
        /// </summary>
        [ContextMenu("Rebuild World Registry")]
        public void RebuildRegistry()
        {
            if (_roadNetwork == null)
            {
                _roadNetwork = FindAnyObjectByType<RoadNetwork>();
            }

            if (_homeNeighborhoodLayout == null)
            {
                _homeNeighborhoodLayout = FindAnyObjectByType<HomeNeighborhoodLayout>();
            }

            if (_roadNetwork != null)
            {
                _roadNetwork.RebuildNetwork();
            }

            _districtsById.Clear();
            for (int i = 0; i < _districts.Count; i++)
            {
                var district = _districts[i];
                if (district != null && !string.IsNullOrEmpty(district.DistrictId) && !_districtsById.ContainsKey(district.DistrictId))
                {
                    _districtsById[district.DistrictId] = district;
                }
            }

            _locationsById.Clear();
            _allLocations.Clear();

            var anchors = GetComponentsInChildren<CityLocationAnchor>(includeInactive: false);
            if (anchors.Length == 0)
            {
                anchors = FindObjectsByType<CityLocationAnchor>(FindObjectsSortMode.None);
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null) continue;
                var owner = anchor.GetComponentInParent<CityWorldRegistry>(includeInactive: true);
                if (owner != null && owner != this) continue;

                if (anchor.AccessRoadNode == null && _roadNetwork != null && anchor.LocationData != null &&
                    !string.IsNullOrEmpty(anchor.LocationData.ConnectedRoadNodeId))
                {
                    anchor.AccessRoadNode = _roadNetwork.GetNode(anchor.LocationData.ConnectedRoadNodeId);
                }

                _allLocations.Add(anchor);
                string id = anchor.LocationId;
                if (!string.IsNullOrEmpty(id) && !_locationsById.ContainsKey(id))
                {
                    _locationsById[id] = anchor;
                }

                if (_playerHomeGarageAnchor == null && anchor.LocationData != null &&
                    anchor.LocationData.Category == WorldLocationCategory.PlayerHomeGarage)
                {
                    _playerHomeGarageAnchor = anchor;
                }

                if (_primaryCommercialDestinationAnchor == null && anchor.LocationData != null &&
                    anchor.LocationData.Category == WorldLocationCategory.CommercialMarket)
                {
                    _primaryCommercialDestinationAnchor = anchor;
                }
            }

            _allTaxiStops.Clear();
            _duplicateTaxiStopIds.Clear();
            var stopIds = new HashSet<string>(StringComparer.Ordinal);
            var stops = GetComponentsInChildren<TaxiStopAnchor>(includeInactive: true);
            for (int i = 0; i < stops.Length; i++)
            {
                var stop = stops[i];
                // A nested registry owns its own stops. Never import foreign scene stops globally.
                if (stop == null || stop.GetComponentInParent<CityWorldRegistry>(includeInactive: true) != this) continue;
                _allTaxiStops.Add(stop);
                if (!string.IsNullOrWhiteSpace(stop.StopId) && !stopIds.Add(stop.StopId))
                    _duplicateTaxiStopIds.Add(stop.StopId);
            }
            for (int i = 0; i < _allTaxiStops.Count; i++)
            {
                var stop = _allTaxiStops[i];
                stop.SetIdentityAmbiguous(!string.IsNullOrWhiteSpace(stop.StopId) && _duplicateTaxiStopIds.Contains(stop.StopId));
                stop.Prepare(_roadNetwork);
            }
        }

        /// <summary>Returns only a sole valid authored stop. No global scene scan is performed.</summary>
        public TaxiStopAnchor GetTaxiStop(string stopId)
        {
            if (string.IsNullOrWhiteSpace(stopId) || _duplicateTaxiStopIds.Contains(stopId)) return null;
            TaxiStopAnchor result = null;
            for (int i = 0; i < _allTaxiStops.Count; i++)
            {
                var stop = _allTaxiStops[i];
                if (stop == null || stop.StopId != stopId || !EligibleStop(stop)) continue;
                if (result != null) return null;
                result = stop;
            }
            return result;
        }

        public TaxiStopAnchor GetTaxiStopForLocation(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId)) return null;
            TaxiStopAnchor result = null;
            for (int i = 0; i < _allTaxiStops.Count; i++)
            {
                var stop = _allTaxiStops[i];
                if (!EligibleStop(stop) || stop.Location.LocationId != locationId) continue;
                if (result != null) return null;
                result = stop;
            }
            return result;
        }

        public TaxiStopAnchor GetTaxiStopForAccessNode(RoadNode accessNode)
        {
            if (accessNode == null) return null;
            TaxiStopAnchor result = null;
            for (int i = 0; i < _allTaxiStops.Count; i++)
            {
                var stop = _allTaxiStops[i];
                if (!EligibleStop(stop) || stop.ApproachNode != accessNode) continue;
                if (result != null) return null;
                result = stop;
            }
            return result;
        }

        private bool EligibleStop(TaxiStopAnchor stop)
        {
            if (stop == null || !stop.IsPrepared || stop.Location == null || !stop.Location.isActiveAndEnabled ||
                string.IsNullOrWhiteSpace(stop.StopId) || _duplicateTaxiStopIds.Contains(stop.StopId) ||
                stop.Location.AccessRoadNode != stop.ApproachNode ||
                stop.Location.GetComponentInParent<CityWorldRegistry>(includeInactive: true) != this) return false;
            // Resolve location identity in this registry; duplicated location IDs are ambiguous too.
            CityLocationAnchor location = null;
            for (int i = 0; i < _allLocations.Count; i++)
            {
                var candidate = _allLocations[i];
                if (candidate == null || candidate.LocationId != stop.Location.LocationId) continue;
                if (location != null) return false;
                location = candidate;
            }
            return location == stop.Location;
        }

        public bool TryGetDistrict(string districtId, out DistrictConfigSO district)
        {
            district = null;
            if (string.IsNullOrEmpty(districtId)) return false;
            if (_districtsById.Count == 0 && _districts.Count > 0) RebuildRegistry();
            return _districtsById.TryGetValue(districtId, out district);
        }

        public bool TryGetLocation(string locationId, out CityLocationAnchor locationAnchor)
        {
            locationAnchor = null;
            if (string.IsNullOrEmpty(locationId)) return false;
            if (_locationsById.Count == 0) RebuildRegistry();
            return _locationsById.TryGetValue(locationId, out locationAnchor);
        }

        public RoadNode GetAccessNodeForLocation(string locationId)
        {
            if (!TryGetLocation(locationId, out var anchor) || anchor == null)
            {
                return null;
            }

            if (anchor.AccessRoadNode != null)
            {
                return anchor.AccessRoadNode;
            }

            if (_roadNetwork != null && anchor.LocationData != null && !string.IsNullOrEmpty(anchor.LocationData.ConnectedRoadNodeId))
            {
                return _roadNetwork.GetNode(anchor.LocationData.ConnectedRoadNodeId);
            }

            return null;
        }

        /// <summary>
        /// Directs the specified vehicle to navigate to an authored location by stable ID,
        /// using either the primary shortest route or a valid alternative route.
        /// </summary>
        public bool NavigateVehicleToLocation(VehicleController vehicle, string locationId, bool useAlternativeRoute = false)
        {
            if (vehicle == null) return false;

            RoadNode targetNode = GetAccessNodeForLocation(locationId);
            if (targetNode == null) return false;

            return useAlternativeRoute
                ? vehicle.NavigateToAlternative(targetNode, allowFallbackToPrimary: false)
                : vehicle.NavigateTo(targetNode);
        }

        /// <summary>
        /// Validates all registered districts, locations, and road node links for duplicate IDs or broken references
        /// (WORLD_CONTENT_DATA_CONTRACT.md §B & §D).
        /// </summary>
        public bool ValidateWorldData(out List<string> issues)
        {
            issues = new List<string>();
            RebuildRegistry();

            if (_roadNetwork == null)
            {
                issues.Add("CityWorldRegistry is missing a RoadNetwork reference.");
            }
            else if (!_roadNetwork.ValidateNetwork(out var networkIssues))
            {
                issues.AddRange(networkIssues);
            }

            var seenDistrictIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _districts.Count; i++)
            {
                var d = _districts[i];
                if (d == null)
                {
                    issues.Add($"Null DistrictConfigSO at index {i}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(d.DistrictId))
                {
                    issues.Add($"DistrictConfigSO '{d.name}' has an empty DistrictId.");
                }
                else if (!seenDistrictIds.Add(d.DistrictId))
                {
                    issues.Add($"Duplicate DistrictId detected: '{d.DistrictId}'.");
                }

                if (_roadNetwork != null)
                {
                    for (int j = 0; j < d.LandmarkNodeIds.Count; j++)
                    {
                        string nodeId = d.LandmarkNodeIds[j];
                        if (!_roadNetwork.TryGetNode(nodeId, out _))
                        {
                            issues.Add($"District '{d.DistrictId}' references non-existent landmark RoadNode '{nodeId}'.");
                        }
                    }
                }
            }

            var seenLocationIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _allLocations.Count; i++)
            {
                var loc = _allLocations[i];
                if (loc == null) continue;

                if (string.IsNullOrWhiteSpace(loc.LocationId))
                {
                    issues.Add($"CityLocationAnchor on '{loc.name}' has an empty LocationId.");
                }
                else if (!seenLocationIds.Add(loc.LocationId))
                {
                    issues.Add($"Duplicate LocationId detected: '{loc.LocationId}'.");
                }

                if (loc.AccessRoadNode == null)
                {
                    issues.Add($"Location '{loc.LocationId}' ('{loc.name}') has no resolved AccessRoadNode.");
                }

                if (loc.LocationData != null)
                {
                    if (!string.IsNullOrEmpty(loc.LocationData.DistrictId) && !seenDistrictIds.Contains(loc.LocationData.DistrictId))
                    {
                        issues.Add($"Location '{loc.LocationId}' references unregistered DistrictId '{loc.LocationData.DistrictId}'.");
                    }

                    if (loc.LocationData.Category == WorldLocationCategory.PlayerHomeGarage && loc.LocationData.FloorCount != 2)
                    {
                        issues.Add($"PlayerHomeGarage location '{loc.LocationId}' must occupy a 2-story building (found FloorCount={loc.LocationData.FloorCount}).");
                    }
                }
            }

            for (int i = 0; i < _allTaxiStops.Count; i++)
            {
                var stop = _allTaxiStops[i];
                if (stop == null) continue;
                if (!stop.Validate(_roadNetwork, 4.5f, 2f, out var diagnostic))
                    issues.Add($"TaxiStopAnchor '{stop.StopId}' ('{stop.name}'): {diagnostic}");
                else if (!EligibleStop(stop))
                    issues.Add($"TaxiStopAnchor '{stop.StopId}' references an unregistered or ambiguous location identity.");
            }

            return issues.Count == 0;
        }
    }
}
