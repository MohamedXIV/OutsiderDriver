using UnityEngine;

namespace OutsiderDriver.World
{
    /// <summary>
    /// Categories of bounded on-foot spaces authorized by Decision P-009
    /// (protagonist's home, garage, and immediate starter neighborhood).
    /// </summary>
    public enum WalkableZoneType
    {
        HomeInterior = 0,
        HomeGarage = 1,
        NeighborhoodSidewalk = 2
    }

    /// <summary>
    /// Defines a bounded walkable region in the protagonist's home or local neighborhood (Decision P-009).
    /// Prevents unintended city-wide free-roaming while keeping domestic and street spaces grounded.
    /// </summary>
    [DisallowMultipleComponent]
    public class WalkableZone : MonoBehaviour
    {
        [SerializeField] private string _zoneId = "zone_home_interior";
        [SerializeField] private string _displayName = "Driver's Apartment";
        [SerializeField] private WalkableZoneType _zoneType = WalkableZoneType.HomeInterior;
        [Tooltip("Local-space 2D bounding box around this transform defining the walkable footprint.")]
        [SerializeField] private Rect _localBounds = new Rect(-4f, -3f, 8f, 6f);

        public string ZoneId => _zoneId;
        public string DisplayName => _displayName;
        public WalkableZoneType ZoneType => _zoneType;
        public Rect LocalBounds
        {
            get => _localBounds;
            set => _localBounds = value;
        }

        public void Configure(string zoneId, string displayName, WalkableZoneType zoneType, Rect localBounds)
        {
            _zoneId = zoneId;
            _displayName = displayName;
            _zoneType = zoneType;
            _localBounds = localBounds;
        }

        /// <summary>
        /// Checks whether a 2D world position falls within this zone's walkable bounds.
        /// </summary>
        public bool ContainsWorldPoint(Vector2 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(new Vector3(worldPosition.x, worldPosition.y, transform.position.z));
            return _localBounds.Contains(new Vector2(local.x, local.y));
        }

        /// <summary>
        /// Clamps a 2D world position to stay inside this zone's boundaries.
        /// </summary>
        public Vector2 ClampWorldPoint(Vector2 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(new Vector3(worldPosition.x, worldPosition.y, transform.position.z));
            float clampedX = Mathf.Clamp(local.x, _localBounds.xMin, _localBounds.xMax);
            float clampedY = Mathf.Clamp(local.y, _localBounds.yMin, _localBounds.yMax);
            Vector3 world = transform.TransformPoint(new Vector3(clampedX, clampedY, 0f));
            return new Vector2(world.x, world.y);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _zoneType == WalkableZoneType.HomeInterior
                ? new Color(0.95f, 0.65f, 0.35f, 0.5f)
                : new Color(0.35f, 0.85f, 0.75f, 0.5f);

            Vector3 center = transform.TransformPoint(new Vector3(_localBounds.center.x, _localBounds.center.y, 0f));
            Vector3 size = new Vector3(_localBounds.width, _localBounds.height, 0.05f);
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
