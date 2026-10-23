using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using OutsiderDriver.Navigation;

namespace OutsiderDriver.Vehicles
{
    /// <summary>
    /// Controls semi-autonomous vehicle navigation along an authored RoadNetwork.
    /// Handles route following, intersection right-of-way yielding, forward vehicle spacing,
    /// speed acceleration/deceleration, and isometric orientation.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class VehicleController : MonoBehaviour
    {
        private static readonly List<VehicleController> ActiveVehicles = new List<VehicleController>();

        [Header("Road & Routing")]
        [SerializeField] private RoadNetwork _roadNetwork;
        [SerializeField] private float _waypointArrivalRadius = 0.25f;

        [Header("Movement Dynamics")]
        [SerializeField] private float _maxSpeed = 5f;
        [SerializeField] private float _acceleration = 4f;
        [SerializeField] private float _deceleration = 6f;

        [Header("Traffic Sensors & Yielding")]
        [Tooltip("Forward distance to check for another vehicle ahead in the same lane.")]
        [SerializeField] private float _forwardSensorDistance = 1.8f;
        [Tooltip("Directional cone angle (degrees) for detecting vehicles directly ahead.")]
        [SerializeField] private float _forwardSensorAngle = 35f;
        [Tooltip("Distance from a YieldIntersection node at which the vehicle checks right-of-way.")]
        [SerializeField] private float _intersectionYieldDistance = 1.4f;

        [Header("Visuals")]
        [SerializeField] private Transform _visualsRoot;

        private readonly List<RoadNode> _activePath = new List<RoadNode>();
        private int _currentPathIndex = 0;
        private float _currentSpeed = 0f;
        private bool _isPaused = false;
        private bool _isYielding = false;
        private Vector3 _forwardDirection = new Vector3(1f, 0.5f, 0f).normalized;
        private IsometricDirection _currentFacing = IsometricDirection.NorthEast;
        private RoadNode _reservedNode;

        public static IReadOnlyList<VehicleController> AllActiveVehicles => ActiveVehicles;
        public static void RegisterActiveVehicle(VehicleController vehicle)
        {
            if (vehicle != null && !ActiveVehicles.Contains(vehicle))
            {
                ActiveVehicles.Add(vehicle);
            }
        }
        public static void ClearActiveVehicles() => ActiveVehicles.Clear();

        public float CurrentSpeed => _currentSpeed;
        public float MaxSpeed
        {
            get => _maxSpeed;
            set => _maxSpeed = Mathf.Max(0.1f, value);
        }
        public float ForwardSensorDistance
        {
            get => _forwardSensorDistance;
            set => _forwardSensorDistance = Mathf.Max(0.1f, value);
        }
        public bool IsMoving => _currentSpeed > 0.05f;
        public bool IsYielding => _isYielding;
        public bool HasActiveRoute => _activePath.Count > 0 && _currentPathIndex < _activePath.Count;
        public RoadNode CurrentTargetNode => HasActiveRoute ? _activePath[_currentPathIndex] : null;
        public RoadNode DestinationNode => _activePath.Count > 0 ? _activePath[_activePath.Count - 1] : null;
        public Vector3 ForwardDirection => _forwardDirection;
        public IsometricDirection CurrentFacing => _currentFacing;

        public event Action<RoadNode> OnDestinationReached;
        public event Action<RoadNode> OnWaypointPassed;
        public event Action<IsometricDirection> OnFacingChanged;
        public event Action<IReadOnlyList<RoadNode>> OnRouteChanged;

        [Inject]
        public void Construct(RoadNetwork roadNetwork)
        {
            if (roadNetwork != null)
            {
                _roadNetwork = roadNetwork;
            }
        }

        private void Awake()
        {
            if (!ActiveVehicles.Contains(this))
            {
                ActiveVehicles.Add(this);
            }
        }

        private void OnEnable()
        {
            if (!ActiveVehicles.Contains(this))
            {
                ActiveVehicles.Add(this);
            }
        }

        private void OnDisable()
        {
            ReleaseReservedNode();
            ActiveVehicles.Remove(this);
        }

        private void Start()
        {
            if (_roadNetwork == null)
            {
                _roadNetwork = FindAnyObjectByType<RoadNetwork>();
            }
        }

        private void Update()
        {
            TickMovement(Time.deltaTime);
        }

        /// <summary>
        /// Advances vehicle movement, sensor checks, and route progression by deltaTime.
        /// Publicly callable for deterministic EditMode/PlayMode testing.
        /// </summary>
        public void TickMovement(float deltaTime)
        {
            if (_isPaused || !HasActiveRoute)
            {
                _isYielding = false;
                ApplyDeceleration(deltaTime);
                return;
            }

            MoveAlongPath(deltaTime);
        }

        /// <summary>
        /// Requests a route to the given destination node and starts travel.
        /// </summary>
        public bool NavigateTo(RoadNode destination)
        {
            if (destination == null) return false;

            if (_roadNetwork == null)
            {
                _roadNetwork = FindAnyObjectByType<RoadNetwork>();
                if (_roadNetwork == null)
                {
                    Debug.LogWarning("[VehicleController] Cannot navigate: No RoadNetwork found.", this);
                    return false;
                }
            }

            RoadNode startNode = CurrentTargetNode ?? _roadNetwork.FindNearestNode(transform.position);
            if (startNode == null)
            {
                Debug.LogWarning("[VehicleController] Cannot navigate: No start node found.", this);
                return false;
            }

            var path = _roadNetwork.FindPath(startNode, destination);
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning($"[VehicleController] No valid path between {startNode.NodeId} and {destination.NodeId}.", this);
                return false;
            }

            ReleaseReservedNode();
            _activePath.Clear();
            _activePath.AddRange(path);
            _currentPathIndex = 0;
            _isPaused = false;
            _isYielding = false;

            OnRouteChanged?.Invoke(_activePath);
            return true;
        }

        public void Stop()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }

        public void CancelRoute()
        {
            ReleaseReservedNode();
            _activePath.Clear();
            _currentPathIndex = 0;
            _isYielding = false;
            OnRouteChanged?.Invoke(_activePath);
        }

        /// <summary>
        /// Checks whether another active vehicle is directly ahead within the forward sensor cone,
        /// or whether the upcoming intersection node requires yielding.
        /// </summary>
        public bool ShouldYield(RoadNode upcomingNode, Vector3 travelDirection, float distanceToNode)
        {
            // 1. Check forward vehicle spacing sensor against other active vehicles
            if (IsVehicleBlockingAhead(travelDirection))
            {
                return true;
            }

            // 2. Check upcoming intersection reservation and priority rules
            if (upcomingNode != null && distanceToNode <= _intersectionYieldDistance)
            {
                if (upcomingNode.Priority == RoadNodePriority.YieldIntersection)
                {
                    // Must yield if another vehicle has reserved the intersection or is crossing on a main artery
                    if (!upcomingNode.CanVehicleEnter(this) || IsCrossTrafficApproaching(upcomingNode))
                    {
                        return true;
                    }
                }
                else if (upcomingNode.IsIntersection && !upcomingNode.CanVehicleEnter(this))
                {
                    // Even on a main artery, do not drive through a node actively occupied by another vehicle
                    return true;
                }

                // Reserve the intersection once within clearance radius and allowed to enter
                if (distanceToNode <= upcomingNode.IntersectionClearanceRadius)
                {
                    if (_reservedNode != upcomingNode)
                    {
                        ReleaseReservedNode();
                        if (upcomingNode.TryReserveIntersection(this))
                        {
                            _reservedNode = upcomingNode;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsVehicleBlockingAhead(Vector3 travelDirection)
        {
            if (travelDirection.sqrMagnitude < 0.0001f) return false;

            Vector3 dirNormalized = travelDirection.normalized;
            Vector3 myPos = transform.position;
            float maxDistSqr = _forwardSensorDistance * _forwardSensorDistance;
            float minCosAngle = Mathf.Cos(_forwardSensorAngle * Mathf.Deg2Rad);

            for (int i = 0; i < ActiveVehicles.Count; i++)
            {
                var other = ActiveVehicles[i];
                if (other == null || ReferenceEquals(other, this)) continue;

                Vector3 toOther = other.transform.position - myPos;
                toOther.z = 0f;
                float distSqr = toOther.sqrMagnitude;

                if (distSqr <= 0.0001f || distSqr > maxDistSqr) continue;

                Vector3 toOtherNormalized = toOther / Mathf.Sqrt(distSqr);
                float dot = Vector3.Dot(dirNormalized, toOtherNormalized);
                if (dot >= minCosAngle)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCrossTrafficApproaching(RoadNode intersectionNode)
        {
            Vector3 nodePos = intersectionNode.Position;
            float checkRadius = intersectionNode.IntersectionClearanceRadius * 1.5f;
            float checkRadiusSqr = checkRadius * checkRadius;

            for (int i = 0; i < ActiveVehicles.Count; i++)
            {
                var other = ActiveVehicles[i];
                if (other == null || ReferenceEquals(other, this)) continue;

                // If another vehicle is targeting this intersection and is closer or moving on a main artery
                if (other.CurrentTargetNode == intersectionNode)
                {
                    Vector3 toNode = nodePos - other.transform.position;
                    toNode.z = 0f;
                    if (toNode.sqrMagnitude <= checkRadiusSqr)
                    {
                        float myDistSqr = (nodePos - transform.position).sqrMagnitude;
                        if (toNode.sqrMagnitude < myDistSqr)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void MoveAlongPath(float deltaTime)
        {
            RoadNode targetNode = _activePath[_currentPathIndex];
            Vector3 targetPos = targetNode.Position;
            Vector3 currentPos = transform.position;

            Vector3 directionToTarget = targetPos - currentPos;
            directionToTarget.z = 0f;
            float distanceToTarget = directionToTarget.magnitude;

            // Check if arrived at current waypoint
            if (distanceToTarget <= _waypointArrivalRadius)
            {
                if (_reservedNode == targetNode)
                {
                    ReleaseReservedNode();
                }

                OnWaypointPassed?.Invoke(targetNode);
                _currentPathIndex++;

                if (_currentPathIndex >= _activePath.Count)
                {
                    RoadNode reached = targetNode;
                    CancelRoute();
                    OnDestinationReached?.Invoke(reached);
                    return;
                }

                targetNode = _activePath[_currentPathIndex];
                targetPos = targetNode.Position;
                directionToTarget = targetPos - currentPos;
                directionToTarget.z = 0f;
                distanceToTarget = directionToTarget.magnitude;
            }

            if (directionToTarget.sqrMagnitude > 0.0001f)
            {
                _forwardDirection = directionToTarget.normalized;
            }

            // Update facing direction
            UpdateIsometricFacing(_forwardDirection);

            // Evaluate forward sensor and intersection right-of-way rules
            _isYielding = ShouldYield(targetNode, _forwardDirection, distanceToTarget);
            if (_isYielding)
            {
                ApplyDeceleration(deltaTime);
            }
            else
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, _maxSpeed, _acceleration * deltaTime);
            }

            if (_currentSpeed > 0f)
            {
                Vector3 movement = _forwardDirection * (_currentSpeed * deltaTime);
                transform.position += movement;
            }
        }

        private void ReleaseReservedNode()
        {
            if (_reservedNode != null)
            {
                _reservedNode.ReleaseIntersection(this);
                _reservedNode = null;
            }
        }

        private void ApplyDeceleration(float deltaTime)
        {
            if (_currentSpeed > 0f)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, _deceleration * deltaTime);
            }
        }

        private void UpdateIsometricFacing(Vector3 moveVector)
        {
            if (moveVector.sqrMagnitude < 0.001f) return;

            IsometricDirection newFacing = DetermineIsometricDirection(moveVector.x, moveVector.y);
            if (newFacing != _currentFacing)
            {
                _currentFacing = newFacing;
                OnFacingChanged?.Invoke(_currentFacing);
            }
        }

        private static IsometricDirection DetermineIsometricDirection(float x, float y)
        {
            // 2:1 isometric projection angle check:
            // NE (+X, +Y), SE (+X, -Y), SW (-X, -Y), NW (-X, +Y)
            if (x >= 0 && y >= 0) return IsometricDirection.NorthEast;
            if (x >= 0 && y < 0) return IsometricDirection.SouthEast;
            if (x < 0 && y < 0) return IsometricDirection.SouthWest;
            return IsometricDirection.NorthWest;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_activePath != null && _activePath.Count > 0 && _currentPathIndex < _activePath.Count)
            {
                Gizmos.color = _isYielding ? new Color(1f, 0.4f, 0.2f, 0.9f) : Color.cyan;
                Vector3 current = transform.position;
                for (int i = _currentPathIndex; i < _activePath.Count; i++)
                {
                    if (_activePath[i] != null)
                    {
                        Gizmos.DrawLine(current, _activePath[i].Position);
                        current = _activePath[i].Position;
                    }
                }
            }

            // Draw forward sensor ray
            Gizmos.color = _isYielding ? Color.red : Color.green;
            Gizmos.DrawRay(transform.position, _forwardDirection * _forwardSensorDistance);
        }
#endif
    }
}
