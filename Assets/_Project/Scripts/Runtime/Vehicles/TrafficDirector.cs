using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using OutsiderDriver.Navigation;

namespace OutsiderDriver.Vehicles
{
    /// <summary>
    /// GTA-style camera-bubble traffic director (Decision P-019).
    /// Spawns ambient ground-contact vehicles within an outer ring around the focus target
    /// and recycles them into an ObjectPool when they travel outside the despawn radius.
    /// Avoids simulating distant off-screen vehicles across the city.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrafficDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoadNetwork _roadNetwork;
        [Tooltip("Center of the active simulation bubble (typically the player taxi or main camera).")]
        [SerializeField] private Transform _bubbleCenter;
        [Tooltip("Optional prefab for ambient vehicles. If null, a lightweight runtime vehicle object is created.")]
        [SerializeField] private VehicleController _ambientVehiclePrefab;

        [Header("Bubble & Pool Settings")]
        [Tooltip("Maximum number of active ambient vehicles inside the bubble at one time.")]
        [SerializeField] private int _maxActiveVehicles = 5;
        [Tooltip("Inner radius around bubble center; ambient cars will not spawn closer than this.")]
        [SerializeField] private float _minSpawnRadius = 6f;
        [Tooltip("Outer radius around bubble center within which new ambient cars can spawn.")]
        [SerializeField] private float _maxSpawnRadius = 18f;
        [Tooltip("Radius beyond which active ambient cars are recycled back into the pool.")]
        [SerializeField] private float _despawnRadius = 24f;
        [Tooltip("Minimum clearance distance from any existing active vehicle when choosing a spawn node.")]
        [SerializeField] private float _spawnClearanceRadius = 2.5f;
        [Tooltip("Interval in seconds between bubble spawn/despawn evaluations.")]
        [SerializeField] private float _evaluationInterval = 0.5f;
        [Tooltip("Cruise speed assigned to spawned ambient vehicles.")]
        [SerializeField] private float _ambientCruiseSpeed = 3.8f;

        private ObjectPool<VehicleController> _vehiclePool;
        private readonly List<VehicleController> _activeAmbientVehicles = new List<VehicleController>();
        private float _evaluationTimer;

        public IReadOnlyList<VehicleController> ActiveAmbientVehicles => _activeAmbientVehicles;
        public int ActiveCount => _activeAmbientVehicles.Count;
        public int InactivePoolCount => _vehiclePool != null ? _vehiclePool.CountInactive : 0;

        public int MaxActiveVehicles
        {
            get => _maxActiveVehicles;
            set => _maxActiveVehicles = Mathf.Max(0, value);
        }

        public float MinSpawnRadius
        {
            get => _minSpawnRadius;
            set => _minSpawnRadius = Mathf.Max(0f, value);
        }

        public float MaxSpawnRadius
        {
            get => _maxSpawnRadius;
            set => _maxSpawnRadius = Mathf.Max(_minSpawnRadius + 0.5f, value);
        }

        public float DespawnRadius
        {
            get => _despawnRadius;
            set => _despawnRadius = Mathf.Max(_maxSpawnRadius + 0.5f, value);
        }

        public Transform BubbleCenter
        {
            get => _bubbleCenter;
            set => _bubbleCenter = value;
        }

        [Inject]
        public void Construct(RoadNetwork roadNetwork, VehicleController playerTaxi = null)
        {
            if (roadNetwork != null)
            {
                _roadNetwork = roadNetwork;
            }

            if (_bubbleCenter == null && playerTaxi != null)
            {
                _bubbleCenter = playerTaxi.transform;
            }
        }

        private void Awake()
        {
            EnsurePoolInitialized();
        }

        private void Start()
        {
            if (_roadNetwork == null)
            {
                _roadNetwork = FindAnyObjectByType<RoadNetwork>();
            }

            if (_bubbleCenter == null && Camera.main != null)
            {
                _bubbleCenter = Camera.main.transform;
            }
        }

        private void OnDestroy()
        {
            DespawnAll();
            _vehiclePool?.Dispose();
        }

        private void Update()
        {
            _evaluationTimer += Time.deltaTime;
            if (_evaluationTimer >= _evaluationInterval)
            {
                _evaluationTimer = 0f;
                EvaluateBubble();
            }
        }

        /// <summary>
        /// Evaluates the active traffic bubble: recycles vehicles that exited the despawn radius,
        /// re-routes idle vehicles, and spawns pooled vehicles on valid ring nodes up to MaxActiveVehicles.
        /// Publicly callable for deterministic EditMode/PlayMode testing.
        /// </summary>
        public void EvaluateBubble()
        {
            EnsurePoolInitialized();

            if (_roadNetwork == null || _roadNetwork.AllNodes.Count < 2)
            {
                return;
            }

            Vector3 center = GetBubbleCenterPosition();
            float despawnRadiusSqr = _despawnRadius * _despawnRadius;

            // 1. Recycle ambient vehicles that left the despawn bubble or keep idle ones moving
            for (int i = _activeAmbientVehicles.Count - 1; i >= 0; i--)
            {
                var vehicle = _activeAmbientVehicles[i];
                if (vehicle == null)
                {
                    _activeAmbientVehicles.RemoveAt(i);
                    continue;
                }

                Vector3 offset = vehicle.transform.position - center;
                offset.z = 0f;
                if (offset.sqrMagnitude > despawnRadiusSqr)
                {
                    RecycleVehicle(vehicle);
                    continue;
                }

                if (!vehicle.HasActiveRoute)
                {
                    AssignRandomDestination(vehicle);
                }
            }

            // 2. Spawn new ambient vehicles if below capacity
            int needed = _maxActiveVehicles - _activeAmbientVehicles.Count;
            for (int i = 0; i < needed; i++)
            {
                RoadNode spawnNode = FindValidSpawnNode(center);
                if (spawnNode == null)
                {
                    break;
                }

                SpawnVehicleAtNode(spawnNode);
            }
        }

        /// <summary>
        /// Immediately returns all active ambient vehicles to the object pool.
        /// </summary>
        public void DespawnAll()
        {
            if (_vehiclePool == null)
            {
                _activeAmbientVehicles.Clear();
                return;
            }

            for (int i = _activeAmbientVehicles.Count - 1; i >= 0; i--)
            {
                var vehicle = _activeAmbientVehicles[i];
                if (vehicle != null)
                {
                    _vehiclePool.Release(vehicle);
                }
            }

            _activeAmbientVehicles.Clear();
        }

        private void EnsurePoolInitialized()
        {
            if (_vehiclePool != null) return;

            _vehiclePool = new ObjectPool<VehicleController>(
                createFunc: CreatePooledVehicle,
                actionOnGet: OnGetVehicleFromPool,
                actionOnRelease: OnReleaseVehicleToPool,
                actionOnDestroy: OnDestroyPooledVehicle,
                collectionCheck: false,
                defaultCapacity: Mathf.Max(4, _maxActiveVehicles),
                maxSize: Mathf.Max(16, _maxActiveVehicles * 2));
        }

        private VehicleController CreatePooledVehicle()
        {
            VehicleController instance;
            if (_ambientVehiclePrefab != null)
            {
                instance = Instantiate(_ambientVehiclePrefab, transform);
            }
            else
            {
                var go = new GameObject("AmbientVehicle_Pooled");
                go.transform.SetParent(transform);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.85f, 0.55f, 0.35f, 1f);

                instance = go.AddComponent<VehicleController>();
            }

            instance.Construct(_roadNetwork);
            instance.MaxSpeed = _ambientCruiseSpeed;
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void OnGetVehicleFromPool(VehicleController vehicle)
        {
            if (vehicle == null) return;
            vehicle.Construct(_roadNetwork);
            vehicle.MaxSpeed = _ambientCruiseSpeed;
            vehicle.gameObject.SetActive(true);
            VehicleController.RegisterActiveVehicle(vehicle);
        }

        private void OnReleaseVehicleToPool(VehicleController vehicle)
        {
            if (vehicle == null) return;
            vehicle.CancelRoute();
            vehicle.gameObject.SetActive(false);
        }

        private void OnDestroyPooledVehicle(VehicleController vehicle)
        {
            if (vehicle == null) return;
            if (Application.isPlaying)
            {
                Destroy(vehicle.gameObject);
            }
            else
            {
                DestroyImmediate(vehicle.gameObject);
            }
        }

        private VehicleController SpawnVehicleAtNode(RoadNode spawnNode)
        {
            var vehicle = _vehiclePool.Get();
            vehicle.transform.position = spawnNode.Position;
            _activeAmbientVehicles.Add(vehicle);

            AssignRandomDestination(vehicle, spawnNode);
            return vehicle;
        }

        private void RecycleVehicle(VehicleController vehicle)
        {
            if (_activeAmbientVehicles.Remove(vehicle))
            {
                _vehiclePool.Release(vehicle);
            }
        }

        private RoadNode FindValidSpawnNode(Vector3 center)
        {
            var allNodes = _roadNetwork.AllNodes;
            float minSqr = _minSpawnRadius * _minSpawnRadius;
            float maxSqr = _maxSpawnRadius * _maxSpawnRadius;
            float clearanceSqr = _spawnClearanceRadius * _spawnClearanceRadius;

            for (int i = 0; i < allNodes.Count; i++)
            {
                var candidate = allNodes[i];
                if (candidate == null || candidate.ConnectedNodes.Count == 0) continue;
                if (candidate.OccupyingVehicle != null) continue;

                Vector3 offset = candidate.Position - center;
                offset.z = 0f;
                float distSqr = offset.sqrMagnitude;
                if (distSqr < minSqr || distSqr > maxSqr) continue;

                if (IsPositionClearOfVehicles(candidate.Position, clearanceSqr))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsPositionClearOfVehicles(Vector3 position, float clearanceSqr)
        {
            var activeVehicles = VehicleController.AllActiveVehicles;
            for (int i = 0; i < activeVehicles.Count; i++)
            {
                var v = activeVehicles[i];
                if (v == null || !v.gameObject.activeInHierarchy) continue;

                Vector3 diff = v.transform.position - position;
                diff.z = 0f;
                if (diff.sqrMagnitude < clearanceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private void AssignRandomDestination(VehicleController vehicle, RoadNode currentNode = null)
        {
            if (_roadNetwork == null || _roadNetwork.AllNodes.Count < 2) return;

            RoadNode start = currentNode ?? _roadNetwork.FindNearestNode(vehicle.transform.position);
            var allNodes = _roadNetwork.AllNodes;

            // Pick a valid destination distinct from start
            int startIndex = Random.Range(0, allNodes.Count);
            for (int i = 0; i < allNodes.Count; i++)
            {
                var candidate = allNodes[(startIndex + i) % allNodes.Count];
                if (candidate != null && candidate != start)
                {
                    if (vehicle.NavigateTo(candidate))
                    {
                        return;
                    }
                }
            }
        }

        private Vector3 GetBubbleCenterPosition()
        {
            return _bubbleCenter != null ? _bubbleCenter.position : transform.position;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = GetBubbleCenterPosition();
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(center, _minSpawnRadius);

            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.45f);
            Gizmos.DrawWireSphere(center, _maxSpawnRadius);

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(center, _despawnRadius);
        }
#endif
    }
}
