using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using OutsiderDriver.Data;
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
        private const float UncontrolledIntersectionTieMeters = 0.5f;
        private static readonly List<VehicleController> ActiveVehicles = new List<VehicleController>();

        [Header("Road & Routing")]
        [SerializeField] private RoadNetwork _roadNetwork;
        [Tooltip("Arrival tolerance in metres at a node with no lane offset. Lane-offset nodes use the offset plus a small margin so passing beside the node registers.")]
        [SerializeField] private float _waypointArrivalRadius = 0.6f;

        [Header("Movement Dynamics & Data Model (metres / seconds)")]
        [SerializeField] private CarModelSO _carModel;
        [SerializeField] private float _maxSpeed = 9f;
        [SerializeField] private float _acceleration = 3.5f;
        [SerializeField] private float _deceleration = 5f;

        [Header("Traffic Sensors & Yielding (metres)")]
        [Tooltip("Forward distance to check for another vehicle ahead in the same lane.")]
        [SerializeField] private float _forwardSensorDistance = 9f;
        [Tooltip("Half-width of the forward sensor corridor. Vehicles in adjacent lanes are ignored.")]
        [SerializeField] private float _forwardSensorHalfWidth = 1.8f;
        [Tooltip("Distance from a YieldIntersection node at which the vehicle checks right-of-way.")]
        [SerializeField] private float _intersectionYieldDistance = 9f;

        [Header("Visuals")]
        [SerializeField] private Transform _visualsRoot;

        private readonly List<RoadNode> _activePath = new List<RoadNode>();
        private int _currentPathIndex = 0;
        private float _currentSpeed = 0f;
        private bool _isPaused = false;
        private bool _isYielding = false;
        private Vector3 _forwardDirection = new Vector3(1f, 0.5f, 0f).normalized;
        private Vector2 _forwardGroundDirection = Vector2.right;
        private IsometricDirection _currentFacing = IsometricDirection.NorthEast;
        public float LengthMeters => _carModel != null ? _carModel.LengthMeters : 4.5f;
        private float MinimumGap => _carModel != null ? _carModel.MinimumGapMeters : 2f;
        private float TimeHeadway => _carModel != null ? _carModel.TimeHeadwaySeconds : 1.2f;
        private float _idleElevation;
        private RoadNode _reservedNode;
        private bool _junctionCommitted;
        private bool _passedReservedNode;
        private int _laneMergePathIndex = -1;
        private bool _laneMergeComplete;

        public static IReadOnlyList<VehicleController> AllActiveVehicles => ActiveVehicles;
        public static void RegisterActiveVehicle(VehicleController vehicle)
        {
            if (vehicle != null && !ActiveVehicles.Contains(vehicle))
            {
                ActiveVehicles.Add(vehicle);
            }
        }
        public static void ClearActiveVehicles() => ActiveVehicles.Clear();

        public CarModelSO CarModel => _carModel;
        public float CurrentSpeed => _currentSpeed;
        public float MaxSpeed
        {
            get => _maxSpeed;
            set => _maxSpeed = Mathf.Max(0.1f, value);
        }
        public float Acceleration
        {
            get => _acceleration;
            set => _acceleration = Mathf.Max(0.1f, value);
        }
        public float Deceleration
        {
            get => _deceleration;
            set => _deceleration = Mathf.Max(0.1f, value);
        }
        public float ForwardSensorDistance
        {
            get => _forwardSensorDistance;
            set => _forwardSensorDistance = Mathf.Max(0.1f, value);
        }
        public float ForwardSensorHalfWidth
        {
            get => _forwardSensorHalfWidth;
            set => _forwardSensorHalfWidth = Mathf.Max(0.1f, value);
        }
        public float WaypointArrivalRadius
        {
            get => _waypointArrivalRadius;
            set => _waypointArrivalRadius = Mathf.Max(0.05f, value);
        }
        public float IntersectionYieldDistance
        {
            get => _intersectionYieldDistance;
            set => _intersectionYieldDistance = Mathf.Max(0.1f, value);
        }
        public bool IsMoving => _currentSpeed > 0.05f;
        public bool IsYielding => _isYielding;
        public bool HasActiveRoute => _activePath.Count > 0 && _currentPathIndex < _activePath.Count;
        public IReadOnlyList<RoadNode> ActivePath => _activePath;
        public RoadNode CurrentTargetNode => HasActiveRoute ? _activePath[_currentPathIndex] : null;
        public RoadNode DestinationNode => _activePath.Count > 0 ? _activePath[_activePath.Count - 1] : null;
        public Vector3 ForwardDirection => _forwardDirection;
        public Vector2 ForwardGroundDirection => _forwardGroundDirection;
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

        /// <summary>
        /// Applies kinematic movement and sensor specs from a data-driven CarModelSO asset.
        /// </summary>
        public void ApplyCarModel(CarModelSO model)
        {
            if (model == null) return;

            _carModel = model;
            _maxSpeed = model.MaxSpeed;
            _acceleration = model.Acceleration;
            _deceleration = model.Deceleration;
            _forwardSensorDistance = model.ForwardSensorDistance;
        }

        private void Awake()
        {
            if (_carModel != null)
            {
                ApplyCarModel(_carModel);
            }

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
            if (deltaTime <= 0f) return;
            UpdateReservationClearance();

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

            return NavigateAlongPath(path);
        }

        /// <summary>
        /// Requests a valid alternative route (distinct from the shortest path) to the given destination node.
        /// Falls back to the primary path only if allowFallbackToPrimary is true.
        /// </summary>
        public bool NavigateToAlternative(RoadNode destination, bool allowFallbackToPrimary = false)
        {
            if (destination == null) return false;

            if (_roadNetwork == null)
            {
                _roadNetwork = FindAnyObjectByType<RoadNetwork>();
                if (_roadNetwork == null) return false;
            }

            RoadNode startNode = CurrentTargetNode ?? _roadNetwork.FindNearestNode(transform.position);
            if (startNode == null) return false;

            var altPath = _roadNetwork.FindAlternativePath(startNode, destination);
            if (altPath != null && altPath.Count >= 2)
            {
                return NavigateAlongPath(altPath);
            }

            return allowFallbackToPrimary && NavigateTo(destination);
        }

        /// <summary>
        /// Starts vehicle navigation along a precomputed sequence of connected RoadNodes.
        /// </summary>
        public bool NavigateAlongPath(IReadOnlyList<RoadNode> path)
        {
            if (path == null || path.Count == 0)
            {
                return false;
            }

            // A route change/cancellation is not physical clearance of a junction.
            if (!_junctionCommitted) ReleaseReservedNode();
            _activePath.Clear();
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] != null)
                {
                    _activePath.Add(path[i]);
                }
            }

            if (_activePath.Count == 0)
            {
                return false;
            }

            _currentPathIndex = 0;
            _isPaused = false;
            _isYielding = false;
            _laneMergePathIndex = -1;
            _laneMergeComplete = false;

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
            _idleElevation = TrafficElevation;
            // A route change/cancellation is not physical clearance of a junction.
            if (!_junctionCommitted) ReleaseReservedNode();
            _activePath.Clear();
            _currentPathIndex = 0;
            _isYielding = false;
            _laneMergePathIndex = -1;
            _laneMergeComplete = false;
            OnRouteChanged?.Invoke(_activePath);
        }

        private IsometricGroundProjection GroundProjection =>
            _roadNetwork != null ? _roadNetwork.GroundProjection : IsometricGroundProjection.Default;

        /// <summary>
        /// Checks whether another active vehicle is directly ahead within the forward sensor cone,
        /// or whether the upcoming intersection node requires yielding.
        /// All distances are ground-plane metres.
        /// </summary>
        public bool ShouldYield(RoadNode upcomingNode, Vector2 travelGroundDirection, float distanceToNode)
        {
            GetFollowingLimit(travelGroundDirection,
                distanceToNode <= _intersectionYieldDistance ? upcomingNode : null,
                out float followingSpeed, out _);
            return EvaluateYield(upcomingNode, distanceToNode, followingSpeed);
        }

        private bool EvaluateYield(RoadNode upcomingNode, float distanceToNode, float followingSpeed)
        {
            if (upcomingNode == null || !upcomingNode.IsIntersection)
                return followingSpeed < _maxSpeed - 0.01f;

            // Commitment is physical occupancy, not a speed or waypoint event. A committed
            // owner can slow/stop in the junction without granting perpendicular clearance.
            if (_reservedNode == upcomingNode && _junctionCommitted)
                return followingSpeed < _maxSpeed - 0.01f;
            if (_reservedNode != null && _junctionCommitted)
                return true; // retain the previous conflict until the rear has cleared it

            float entryRadius = upcomingNode.IntersectionClearanceRadius + LengthMeters * 0.5f;
            float reserveDistance = Mathf.Max(_intersectionYieldDistance, entryRadius + MinimumGap);
            if (distanceToNode > reserveDistance) return followingSpeed < _maxSpeed - 0.01f;

            if (!upcomingNode.CanVehicleEnter(this)
                || (upcomingNode.Priority == RoadNodePriority.YieldIntersection && IsCrossTrafficApproaching(upcomingNode)))
                return true;

            if (followingSpeed < _maxSpeed - 0.01f)
            {
                // Only uncommitted blocked approaches may surrender a speculative reservation.
                if (!_junctionCommitted) ReleaseReservedNode();
                return true;
            }
            if (_reservedNode != upcomingNode)
            {
                ReleaseReservedNode();
                if (!upcomingNode.TryReserveIntersection(this)) return true;
                _reservedNode = upcomingNode;
                _passedReservedNode = false;
            }
            if (distanceToNode <= entryRadius) _junctionCommitted = true;
            return false;
        }

        private void UpdateReservationClearance()
        {
            if (_reservedNode == null || !_passedReservedNode) return;
            float distance = (GroundProjection.WorldToGround(transform.position)
                - GroundProjection.WorldToGround(_reservedNode.Position)).magnitude;
            if (distance > _reservedNode.IntersectionClearanceRadius + LengthMeters * 0.5f)
                ReleaseReservedNode();
        }

        private float TrafficElevation
        {
            get
            {
                var target = CurrentTargetNode;
                if (target == null) return _idleElevation;
                if (_currentPathIndex == 0) return target.ElevationLevel;
                var previous = _activePath[_currentPathIndex - 1];
                var projection = GroundProjection;
                Vector2 start = projection.WorldToGround(previous.Position);
                Vector2 segment = projection.WorldToGround(target.Position) - start;
                float progress = segment.sqrMagnitude > 0.0001f
                    ? Vector2.Dot(projection.WorldToGround(transform.position) - start, segment) / segment.sqrMagnitude : 1f;
                return Mathf.Lerp(previous.ElevationLevel, target.ElevationLevel, Mathf.Clamp01(progress));
            }
        }

        private void GetFollowingLimit(Vector2 travelDirection, RoadNode priorityIntersection,
            out float speedLimit, out float clearTravel)
        {
            speedLimit = _maxSpeed;
            clearTravel = float.PositiveInfinity;
            if (travelDirection.sqrMagnitude < 0.0001f) return;
            var projection = GroundProjection;
            Vector2 origin = projection.WorldToGround(transform.position);
            // Existing sensor setting is a minimum, never shorter than the stopping horizon.
            float horizon = Mathf.Max(_forwardSensorDistance,
                LengthMeters + MinimumGap + _currentSpeed * TimeHeadway
                + _currentSpeed * _currentSpeed / (2f * Mathf.Max(0.1f, _deceleration)));

            for (int i = 0; i < ActiveVehicles.Count; i++)
            {
                var other = ActiveVehicles[i];
                if (other == null || other == this || !other.isActiveAndEnabled) continue;
                if (other._roadNetwork != _roadNetwork) continue;
                Vector2 otherGround = projection.WorldToGround(other.transform.position);
                if ((otherGround - origin).sqrMagnitude > horizon * horizon) continue;

                // Preserve 3029c2d: crossing waiters cannot block the tie-break winner.
                if (priorityIntersection != null && other.CurrentTargetNode == priorityIntersection
                    && other._currentSpeed < 0.1f
                    && Mathf.Abs(Vector2.Dot(travelDirection, other._forwardGroundDirection)) < 0.5f) continue;

                Vector2 segmentStart = origin;
                float travelled = 0f;
                // Follow the existing lane polyline around corners, bounded by the sensor horizon.
                for (int index = _currentPathIndex; index < _activePath.Count && travelled < horizon; index++)
                {
                    var node = _activePath[index];
                    Vector2 end = projection.WorldToGround(node.Position);
                    Vector2 tangent = index > 0
                        ? end - projection.WorldToGround(_activePath[index - 1].Position)
                        : travelDirection;
                    if (tangent.sqrMagnitude < 0.0001f) continue;
                    tangent.Normalize();
                    Vector2 laneOffset = new Vector2(tangent.y, -tangent.x) * node.LaneOffsetMeters;
                    end += laneOffset;
                    if (index > _currentPathIndex)
                    {
                        // Use the outgoing lane line, not a diagonal chord between two
                        // lane endpoints (which can sweep through the opposing lane).
                        Vector2 laneStart = projection.WorldToGround(_activePath[index - 1].Position) + laneOffset;
                        travelled += (laneStart - segmentStart).magnitude;
                        segmentStart = laneStart;
                    }
                    Vector2 segment = end - segmentStart;
                    float length = segment.magnitude;
                    if (length < 0.001f) continue;
                    Vector2 direction = segment / length;
                    Vector2 offset = otherGround - segmentStart;
                    float along = Vector2.Dot(offset, direction);
                    float lateral = Mathf.Abs(direction.x * offset.y - direction.y * offset.x);
                    bool aligned = !other.HasActiveRoute
                        || Vector2.Dot(direction, other._forwardGroundDirection) > 0.5f;
                    float startElevation = index == _currentPathIndex ? TrafficElevation : _activePath[index - 1].ElevationLevel;
                    float pathElevation = Mathf.Lerp(startElevation, node.ElevationLevel, Mathf.Clamp01(along / length));
                    bool sameElevation = Mathf.Abs(other.TrafficElevation - pathElevation) < 0.5f;
                    // Include bodies just beyond the terminal node: their rear can still
                    // obstruct the destination pose even though their centre is off-route.
                    bool withinSegment = along <= length || index == _activePath.Count - 1;
                    if (sameElevation && aligned && along > 0f && withinSegment && travelled + along <= horizon
                        && lateral <= _forwardSensorHalfWidth)
                    {
                        float gap = travelled + along - (LengthMeters + other.LengthMeters) * 0.5f;
                        float leadSpeed = other._isPaused || !other.HasActiveRoute ? 0f : other._currentSpeed;
                        speedLimit = Mathf.Min(speedLimit, CarFollowing.TargetSpeed(_maxSpeed, gap,
                            leadSpeed, other.Deceleration, _deceleration, MinimumGap, TimeHeadway));
                        clearTravel = Mathf.Min(clearTravel, Mathf.Max(0f, gap - MinimumGap));
                        break;
                    }
                    travelled += length;
                    segmentStart = end;
                }
            }
        }

        private bool IsCrossTrafficApproaching(RoadNode intersectionNode)
        {
            var projection = GroundProjection;
            Vector2 nodeGround = projection.WorldToGround(intersectionNode.Position);
            float checkRadius = intersectionNode.IntersectionClearanceRadius * 1.5f;
            float checkRadiusSqr = checkRadius * checkRadius;
            Vector2 myGroundPosition = projection.WorldToGround(transform.position);

            for (int i = 0; i < ActiveVehicles.Count; i++)
            {
                var other = ActiveVehicles[i];
                if (other == null || ReferenceEquals(other, this)) continue;

                // If another vehicle is targeting this intersection, use distance and a deterministic tie-break
                if (other.CurrentTargetNode == intersectionNode)
                {
                    Vector2 otherGround = projection.WorldToGround(other.transform.position);
                    Vector2 toNode = nodeGround - otherGround;
                    if (toNode.sqrMagnitude <= checkRadiusSqr)
                    {
                        float myDist = Mathf.Sqrt((nodeGround - myGroundPosition).sqrMagnitude);
                        float otherDist = Mathf.Sqrt(toNode.sqrMagnitude);

                        if (otherDist < myDist - UncontrolledIntersectionTieMeters)
                        {
                            return true;
                        }

                        // Cross traffic arriving within half a metre is a genuine tie. Without a
                        // deterministic winner both cars yield forever and gridlock the junction.
                        if (Mathf.Abs(otherDist - myDist) <= UncontrolledIntersectionTieMeters
                            && Mathf.Abs(Vector2.Dot(_forwardGroundDirection, other._forwardGroundDirection)) < 0.5f
                            && OtherHasTieBreakPriority(otherGround, myGroundPosition))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool OtherHasTieBreakPriority(Vector2 otherGround, Vector2 myGround)
        {
            if (otherGround.x < myGround.x - 0.001f)
            {
                return true;
            }

            return Mathf.Abs(otherGround.x - myGround.x) <= 0.001f
                && otherGround.y < myGround.y - 0.001f;
        }

        private void MoveAlongPath(float deltaTime)
        {
            var projection = GroundProjection;
            RoadNode targetNode = _activePath[_currentPathIndex];
            if (_laneMergePathIndex != _currentPathIndex)
            {
                _laneMergePathIndex = _currentPathIndex;
                _laneMergeComplete = false;
            }

            Vector2 currentGround = projection.WorldToGround(transform.position);
            Vector2 nodeGround = projection.WorldToGround(targetNode.Position);
            Vector2 laneTargetGround = GetLaneTargetGround(projection, targetNode, currentGround);
            Vector2 directionToTarget = laneTargetGround - currentGround;
            float distanceToNode = (nodeGround - currentGround).magnitude;
            float arrivalRadius = Mathf.Max(_waypointArrivalRadius, targetNode.LaneOffsetMeters + 0.9f);

            // Check if arrived at the authored waypoint. The car may pass beside the node in its lane,
            // so this is measured to the node while steering uses the lane-parallel target.
            bool terminalPoseClear = true;
            if (_currentPathIndex == _activePath.Count - 1 && distanceToNode <= arrivalRadius)
            {
                GetFollowingLimit(directionToTarget.sqrMagnitude > 0.0001f ? directionToTarget.normalized : _forwardGroundDirection,
                    null, out _, out float terminalClearTravel);
                terminalPoseClear = (GetLaneEndGround(projection, targetNode) - currentGround).magnitude <= terminalClearTravel;
            }
            // Ownership checks precede *all* waypoint advancement, including a route
            // beginning inside the arrival radius of somebody else's reserved junction.
            GetFollowingLimit(directionToTarget.sqrMagnitude > 0.0001f ? directionToTarget.normalized : _forwardGroundDirection,
                distanceToNode <= _intersectionYieldDistance ? targetNode : null,
                out float approachFollowingSpeed, out _);
            EvaluateYield(targetNode, distanceToNode, approachFollowingSpeed);
            bool ownsWaypoint = !targetNode.IsIntersection || targetNode.OccupyingVehicle == this;
            if (distanceToNode <= arrivalRadius && terminalPoseClear && ownsWaypoint)
            {
                if (_reservedNode == targetNode)
                {
                    _junctionCommitted = true;
                    _passedReservedNode = true;
                }

                _idleElevation = targetNode.ElevationLevel;
                OnWaypointPassed?.Invoke(targetNode);
                _currentPathIndex++;

                if (_currentPathIndex >= _activePath.Count)
                {
                    RoadNode reached = targetNode;
                    Vector2 finalGround = GetLaneEndGround(projection, targetNode);
                    Vector2 finalWorld = projection.GroundToWorld(finalGround);
                    transform.position = new Vector3(finalWorld.x, finalWorld.y, transform.position.z);
                    CancelRoute();
                    OnDestinationReached?.Invoke(reached);
                    return;
                }

                targetNode = _activePath[_currentPathIndex];
                _laneMergePathIndex = _currentPathIndex;
                _laneMergeComplete = false;
                nodeGround = projection.WorldToGround(targetNode.Position);
                laneTargetGround = GetLaneTargetGround(projection, targetNode, currentGround);
                directionToTarget = laneTargetGround - currentGround;
                distanceToNode = (nodeGround - currentGround).magnitude;
            }

            if (directionToTarget.sqrMagnitude > 0.0001f)
            {
                _forwardGroundDirection = directionToTarget.normalized;
                _forwardDirection = projection.GroundDeltaToWorld(_forwardGroundDirection).normalized;
            }

            // Update facing direction
            UpdateIsometricFacing(_forwardGroundDirection);

            GetFollowingLimit(_forwardGroundDirection,
                distanceToNode <= _intersectionYieldDistance ? targetNode : null,
                out float followingSpeed, out float clearTravel);
            _isYielding = EvaluateYield(targetNode, distanceToNode, followingSpeed);
            // ShouldYield remains the public compatibility query. Following is continuous;
            // only an actual right-of-way conflict commands a full stop.
            bool intersectionBlocked = targetNode.IsIntersection && targetNode.OccupyingVehicle != this
                && distanceToNode <= Mathf.Max(_intersectionYieldDistance,
                    targetNode.IntersectionClearanceRadius + LengthMeters * 0.5f + MinimumGap);
            float targetSpeed = intersectionBlocked ? 0f : followingSpeed;
            // A moving queue may follow at a reduced speed, but cannot pass an unowned
            // junction. Stop before waypoint advancement and reacquire once the queue clears.
            if (targetNode.IsIntersection && targetNode.OccupyingVehicle != this)
            {
                float entryTravel = Mathf.Max(0f, distanceToNode - targetNode.IntersectionClearanceRadius - LengthMeters * 0.5f - 0.1f);
                clearTravel = Mathf.Min(clearTravel, entryTravel);
                targetSpeed = Mathf.Min(targetSpeed, Mathf.Sqrt(2f * _deceleration * entryTravel));
            }
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed,
                (targetSpeed < _currentSpeed ? _deceleration : _acceleration) * deltaTime);
            // Last-resort discrete-step guard for abrupt stops / long frames. Never teleport
            // to repair an existing overlap; bound new travel to the remaining free gap.
            _currentSpeed = Mathf.Min(_currentSpeed, clearTravel / deltaTime);

            if (_currentSpeed > 0f)
            {
                float step = _currentSpeed * deltaTime;
                float targetDistance = directionToTarget.magnitude;
                if (step > targetDistance)
                {
                    step = targetDistance;
                }

                Vector2 groundMovement = _forwardGroundDirection * step;
                transform.position += (Vector3)projection.GroundDeltaToWorld(groundMovement);
                if (_reservedNode != null && !_passedReservedNode
                    && (projection.WorldToGround(transform.position) - projection.WorldToGround(_reservedNode.Position)).magnitude
                        <= _reservedNode.IntersectionClearanceRadius + LengthMeters * 0.5f)
                    _junctionCommitted = true;
            }
        }

        /// <summary>
        /// Returns the next ground-plane point on the lane-parallel path for the current segment.
        /// The lane line is offset from the authored centerline for the whole segment, so opposing
        /// vehicles merge into their own lane near the junction instead of driving diagonally at each other.
        /// </summary>
        private Vector2 GetLaneTargetGround(IsometricGroundProjection projection, RoadNode targetNode, Vector2 currentGround)
        {
            Vector2 targetGround = projection.WorldToGround(targetNode.Position);
            float laneOffset = targetNode.LaneOffsetMeters;
            if (laneOffset <= 0.0001f)
            {
                return targetGround;
            }

            int previousIndex = Mathf.Max(0, _currentPathIndex - 1);
            Vector2 previousGround = projection.WorldToGround(_activePath[previousIndex].Position);
            Vector2 segmentDirection = targetGround - previousGround;
            if (segmentDirection.sqrMagnitude < 0.0001f)
            {
                return targetGround;
            }

            segmentDirection.Normalize();
            Vector2 right = new Vector2(segmentDirection.y, -segmentDirection.x);
            Vector2 laneStart = previousGround + right * laneOffset;

            // The merge is a one-time state per segment. A per-frame lateral threshold
            // oscillates when a fast vehicle overshoots the lane line between ticks.
            if (!_laneMergeComplete)
            {
                Vector2 offset = currentGround - laneStart;
                float lateralDistance = Mathf.Abs(Vector2.Dot(offset, right));
                float progress = Vector2.Dot(offset, segmentDirection);
                if (lateralDistance <= 0.6f || progress >= 0.6f)
                {
                    _laneMergeComplete = true;
                }

                if (!_laneMergeComplete)
                {
                    return laneStart;
                }
            }

            // Pure-pursuit the lane line with a short lookahead so residual lateral error
            // converges quickly instead of decaying over the whole segment.
            Vector2 laneEnd = targetGround + right * laneOffset;
            float segmentLength = (laneEnd - laneStart).magnitude;
            float longitudinal = Vector2.Dot(currentGround - laneStart, segmentDirection);
            float lookahead = longitudinal + 8f;
            if (lookahead >= segmentLength)
            {
                return laneEnd;
            }

            return laneStart + segmentDirection * lookahead;
        }

        private Vector2 GetLaneEndGround(IsometricGroundProjection projection, RoadNode targetNode)
        {
            Vector2 targetGround = projection.WorldToGround(targetNode.Position);
            float laneOffset = targetNode.LaneOffsetMeters;
            if (laneOffset <= 0.0001f)
            {
                return targetGround;
            }

            int previousIndex = Mathf.Max(0, _currentPathIndex - 1);
            Vector2 previousGround = projection.WorldToGround(_activePath[previousIndex].Position);
            Vector2 segmentDirection = targetGround - previousGround;
            if (segmentDirection.sqrMagnitude < 0.0001f)
            {
                return targetGround;
            }

            segmentDirection.Normalize();
            Vector2 right = new Vector2(segmentDirection.y, -segmentDirection.x);
            return targetGround + right * laneOffset;
        }

        private void ReleaseReservedNode()
        {
            if (_reservedNode != null)
            {
                _reservedNode.ReleaseIntersection(this);
                _reservedNode = null;
            }
            _junctionCommitted = false;
            _passedReservedNode = false;
        }

        private void ApplyDeceleration(float deltaTime)
        {
            if (_currentSpeed > 0f)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, _deceleration * deltaTime);
            }
        }

        private void UpdateIsometricFacing(Vector2 groundDirection)
        {
            if (groundDirection.sqrMagnitude < 0.001f) return;

            IsometricDirection newFacing = DetermineIsometricDirection(groundDirection.x, groundDirection.y);
            if (newFacing != _currentFacing)
            {
                _currentFacing = newFacing;
                OnFacingChanged?.Invoke(_currentFacing);
            }
        }

        private static IsometricDirection DetermineIsometricDirection(float groundX, float groundY)
        {
            // Ground axes map to the four screen diagonals in a 2:1 isometric projection.
            if (Mathf.Abs(groundX) >= Mathf.Abs(groundY))
            {
                return groundX >= 0f ? IsometricDirection.NorthEast : IsometricDirection.SouthWest;
            }

            return groundY >= 0f ? IsometricDirection.NorthWest : IsometricDirection.SouthEast;
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
