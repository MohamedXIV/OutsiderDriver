using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace OutsiderDriver.World
{
    /// <summary>
    /// Coordinates the protagonist's authored home interior, garage, and starter neighborhood street
    /// using Unity's native 2D Isometric Grid and Tilemaps (Decisions P-009, P-014, P-025).
    /// </summary>
    [DisallowMultipleComponent]
    public class HomeNeighborhoodLayout : MonoBehaviour
    {
        public static readonly Vector3 DefaultIsometricCellSize = new Vector3(1f, 0.5f, 1f);

        [Header("Native Unity 2D Isometric Grid & Tilemaps")]
        [SerializeField] private Grid _isometricGrid;
        [SerializeField] private Tilemap _streetSurfaceTilemap;
        [SerializeField] private Tilemap _homeFloorTilemap;
        [SerializeField] private Tilemap _obstaclesTilemap;

        [Header("Key Anchors")]
        [SerializeField] private Transform _homeInteriorSpawnPoint;
        [SerializeField] private Transform _streetSidewalkSpawnPoint;
        [SerializeField] private Transform _parkedTaxiBayPoint;

        private readonly List<WalkableZone> _walkableZones = new List<WalkableZone>();
        private readonly List<NeighborhoodInteractable> _interactables = new List<NeighborhoodInteractable>();
        private readonly HashSet<Vector3Int> _blockedCells = new HashSet<Vector3Int>();

        public Grid IsometricGrid => _isometricGrid;
        public Tilemap StreetSurfaceTilemap => _streetSurfaceTilemap;
        public Tilemap HomeFloorTilemap => _homeFloorTilemap;
        public Tilemap ObstaclesTilemap => _obstaclesTilemap;

        public Transform HomeInteriorSpawnPoint => _homeInteriorSpawnPoint;
        public Transform StreetSidewalkSpawnPoint => _streetSidewalkSpawnPoint;
        public Transform ParkedTaxiBayPoint => _parkedTaxiBayPoint;

        public IReadOnlyList<WalkableZone> WalkableZones => _walkableZones;
        public IReadOnlyList<NeighborhoodInteractable> Interactables => _interactables;

        private void Awake()
        {
            EnsureIsometricGridConfigured();
            RebuildLayoutRegistry();
        }

        /// <summary>
        /// Ensures the attached or child Unity Grid uses native Isometric Z as Y layout with 2:1 cell proportions.
        /// </summary>
        public void EnsureIsometricGridConfigured()
        {
            if (_isometricGrid == null)
            {
                _isometricGrid = GetComponentInChildren<Grid>();
                if (_isometricGrid == null)
                {
                    var gridGo = new GameObject("IsometricGrid");
                    gridGo.transform.SetParent(transform, false);
                    _isometricGrid = gridGo.AddComponent<Grid>();
                }
            }

            _isometricGrid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            _isometricGrid.cellSize = DefaultIsometricCellSize;
            _isometricGrid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
        }

        public void AssignTilemaps(Tilemap streetSurface, Tilemap homeFloor, Tilemap obstacles)
        {
            _streetSurfaceTilemap = streetSurface;
            _homeFloorTilemap = homeFloor;
            _obstaclesTilemap = obstacles;
        }

        public void AssignAnchors(Transform homeSpawn, Transform sidewalkSpawn, Transform taxiBay)
        {
            _homeInteriorSpawnPoint = homeSpawn;
            _streetSidewalkSpawnPoint = sidewalkSpawn;
            _parkedTaxiBayPoint = taxiBay;
        }

        /// <summary>
        /// Scans child WalkableZones and NeighborhoodInteractables to populate runtime lookups.
        /// </summary>
        [ContextMenu("Rebuild Layout Registry")]
        public void RebuildLayoutRegistry()
        {
            _walkableZones.Clear();
            _walkableZones.AddRange(GetComponentsInChildren<WalkableZone>(includeInactive: false));

            _interactables.Clear();
            _interactables.AddRange(GetComponentsInChildren<NeighborhoodInteractable>(includeInactive: false));
        }

        /// <summary>
        /// Marks or unmarks a specific isometric grid cell as blocked by furniture, walls, or curbs.
        /// </summary>
        public void SetCellBlocked(Vector3Int cellPosition, bool blocked)
        {
            if (blocked)
            {
                _blockedCells.Add(cellPosition);
            }
            else
            {
                _blockedCells.Remove(cellPosition);
            }
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            EnsureIsometricGridConfigured();
            return _isometricGrid.WorldToCell(worldPosition);
        }

        public Vector3 CellToWorldCenter(Vector3Int cellPosition)
        {
            EnsureIsometricGridConfigured();
            return _isometricGrid.GetCellCenterWorld(cellPosition);
        }

        /// <summary>
        /// Evaluates whether the driver can walk at the given 2D world position:
        /// must fall within an authorized WalkableZone (Decision P-009) and not be blocked by an obstacle tile/cell.
        /// </summary>
        public bool CanWalkAt(Vector2 worldPosition, out WalkableZone containingZone)
        {
            containingZone = null;

            Vector3 world3D = new Vector3(worldPosition.x, worldPosition.y, 0f);
            Vector3Int cell = WorldToCell(world3D);

            if (_blockedCells.Contains(cell))
            {
                return false;
            }

            if (_obstaclesTilemap != null && _obstaclesTilemap.HasTile(cell))
            {
                return false;
            }

            for (int i = 0; i < _walkableZones.Count; i++)
            {
                var zone = _walkableZones[i];
                if (zone != null && zone.ContainsWorldPoint(worldPosition))
                {
                    containingZone = zone;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the closest interactive object within its interaction radius of the actor's position.
        /// </summary>
        public NeighborhoodInteractable FindNearestInteractable(Vector2 actorWorldPosition)
        {
            NeighborhoodInteractable nearest = null;
            float bestDistSqr = float.MaxValue;

            for (int i = 0; i < _interactables.Count; i++)
            {
                var candidate = _interactables[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;

                if (!candidate.IsWithinRange(actorWorldPosition)) continue;

                float distSqr = (candidate.Position2D - actorWorldPosition).sqrMagnitude;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
