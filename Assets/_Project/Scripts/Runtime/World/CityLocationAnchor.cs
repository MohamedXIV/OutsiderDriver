using UnityEngine;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;

namespace OutsiderDriver.World
{
    /// <summary>
    /// Scene-authored spatial anchor for a city building, lot, or landmark (Decision P-028).
    /// Keeps the scene Transform as the single authoritative source of spatial coordinates while
    /// binding to a data-driven WorldLocationSO and its curb/street RoadNode.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class CityLocationAnchor : MonoBehaviour
    {
        [Header("Data Identity")]
        [SerializeField] private WorldLocationSO _locationData;
        [Tooltip("Fallback stable location ID if no WorldLocationSO asset is assigned.")]
        [SerializeField] private string _fallbackLocationId = "location_001";

        [Header("Road & Lot Binding")]
        [Tooltip("Authoritative RoadNode providing street/curb access to this location.")]
        [SerializeField] private RoadNode _accessRoadNode;
        [Tooltip("Isometric grid cell origin of this building lot.")]
        [SerializeField] private Vector2Int _lotOriginCell;
        [Tooltip("Isometric grid footprint size in cells (width X, depth Y).")]
        [SerializeField] private Vector2Int _lotSizeCells = new Vector2Int(4, 4);

        public WorldLocationSO LocationData => _locationData;
        public string LocationId => _locationData != null && !string.IsNullOrEmpty(_locationData.LocationId)
            ? _locationData.LocationId
            : _fallbackLocationId;
        public string DistrictId => _locationData != null ? _locationData.DistrictId : string.Empty;
        public RoadNode AccessRoadNode
        {
            get => _accessRoadNode;
            set => _accessRoadNode = value;
        }
        public Vector3 WorldPosition => transform.position;
        public Vector2Int LotOriginCell => _lotOriginCell;
        public Vector2Int LotSizeCells => _lotSizeCells;

        public void Configure(
            WorldLocationSO locationData,
            RoadNode accessRoadNode,
            Vector2Int lotOriginCell,
            Vector2Int lotSizeCells,
            string fallbackLocationId = null)
        {
            _locationData = locationData;
            _accessRoadNode = accessRoadNode;
            _lotOriginCell = lotOriginCell;
            _lotSizeCells = new Vector2Int(Mathf.Max(1, lotSizeCells.x), Mathf.Max(1, lotSizeCells.y));
            if (!string.IsNullOrEmpty(fallbackLocationId))
            {
                _fallbackLocationId = fallbackLocationId;
            }
            else if (locationData != null)
            {
                _fallbackLocationId = locationData.LocationId;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.75f, 0.25f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);

            if (_accessRoadNode != null)
            {
                Gizmos.color = new Color(0.25f, 0.9f, 0.85f, 0.85f);
                Gizmos.DrawLine(transform.position, _accessRoadNode.Position);
            }
        }
#endif
    }
}
