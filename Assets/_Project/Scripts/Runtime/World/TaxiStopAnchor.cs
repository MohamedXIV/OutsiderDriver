using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Navigation;

namespace OutsiderDriver.World
{
    /// <summary>
    /// A hand-authored bay and two directed cubic manoeuvres, independent of junction topology.
    /// Controls are never generated. Prepare snapshots ground-metre geometry; reprepare after edits.
    /// Sparse lane/conflict checks do not certify sidewalks, buildings or the swept drivable surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaxiStopAnchor : MonoBehaviour
    {
        private const float EndpointTolerance = 0.15f;
        private const float HeadingAlignment = 0.995f;
        [SerializeField] private string _stopId;
        [SerializeField] private CityLocationAnchor _location;
        [SerializeField] private RoadNode _approachFromNode;
        [SerializeField] private RoadNode _approachNode;
        [SerializeField] private RoadNode _departureNode;
        [SerializeField] private RoadNode _departureNextNode;
        [Header("Manually placed cubic controls (shared stop pose)")]
        [SerializeField] private Transform _entry;
        [SerializeField] private Transform _pullInFirstHandle;
        [SerializeField] private Transform _pullInSecondHandle;
        [SerializeField] private Transform _stopPose;
        [SerializeField] private Transform _pullOutFirstHandle;
        [SerializeField] private Transform _pullOutSecondHandle;
        [SerializeField] private Transform _exit;
        [Tooltip("Position ahead of the stop pose specifying its ground travel direction.")]
        [SerializeField] private Transform _headingMarker;
        [Header("Authored capacity and clearance (ground metres)")]
        [SerializeField] private float _bayWidthMeters = 2.6f;
        [SerializeField] private float _bayLengthMeters = 6f;
        [SerializeField] private float _bodyClearanceMeters = 0.25f;
        [SerializeField] private float _throughLaneWidthMeters = 3f;

        private Curve _pullIn = new Curve();
        private Curve _pullOut = new Curve();
        private Curve _candidatePullIn = new Curve();
        private Curve _candidatePullOut = new Curve();
        private RoadNetwork _preparedNetwork;
        private readonly HashSet<RoadNode> _candidateConflictNodes = new HashSet<RoadNode>();
        private bool _prepared;
        private bool _identityAmbiguous;
        private Vector2 _stopGroundPosition;
        private Vector2 _stopGroundDirection;
        private int _elevationLevel;
        private object _leaseOwner;

        public string StopId => _stopId;
        public CityLocationAnchor Location => _location;
        public RoadNode ApproachFromNode => _approachFromNode;
        public RoadNode ApproachNode => _approachNode;
        public RoadNode DepartureNode => _departureNode;
        public RoadNode DepartureNextNode => _departureNextNode;
        public bool IsPrepared => _prepared && !_identityAmbiguous && isActiveAndEnabled;
        public float PullInLength => IsPrepared ? _pullIn.Length : 0f;
        public float PullOutLength => IsPrepared ? _pullOut.Length : 0f;
        public Vector2 StopGroundPosition => IsPrepared ? _stopGroundPosition : Vector2.zero;
        public Vector2 StopGroundDirection => IsPrepared ? _stopGroundDirection : Vector2.zero;
        public int ElevationLevel => _elevationLevel;
        public float BayWidthMeters => _bayWidthMeters;
        public float BayLengthMeters => _bayLengthMeters;

        private void OnDisable() => _prepared = false;
        private void OnValidate() => _prepared = false;

        internal void SetIdentityAmbiguous(bool ambiguous)
        {
            _identityAmbiguous = ambiguous;
            if (ambiguous) _prepared = false;
        }

        public void Configure(string stopId, CityLocationAnchor location,
            RoadNode approachFromNode, RoadNode approachNode, RoadNode departureNode, RoadNode departureNextNode,
            Transform entry, Transform pullInFirstHandle, Transform pullInSecondHandle, Transform stopPose,
            Transform pullOutFirstHandle, Transform pullOutSecondHandle, Transform exit, Transform headingMarker,
            float bayWidthMeters = 2.6f, float bayLengthMeters = 6f,
            float bodyClearanceMeters = 0.25f, float throughLaneWidthMeters = 3f)
        {
            _stopId = stopId;
            _location = location;
            _approachFromNode = approachFromNode;
            _approachNode = approachNode;
            _departureNode = departureNode;
            _departureNextNode = departureNextNode;
            _entry = entry;
            _pullInFirstHandle = pullInFirstHandle;
            _pullInSecondHandle = pullInSecondHandle;
            _stopPose = stopPose;
            _pullOutFirstHandle = pullOutFirstHandle;
            _pullOutSecondHandle = pullOutSecondHandle;
            _exit = exit;
            _headingMarker = headingMarker;
            _bayWidthMeters = bayWidthMeters;
            _bayLengthMeters = bayLengthMeters;
            _bodyClearanceMeters = bodyClearanceMeters;
            _throughLaneWidthMeters = throughLaneWidthMeters;
            _prepared = false;
        }

        public bool Prepare(RoadNetwork network, float lengthMeters = 4.5f, float widthMeters = 2f) =>
            Validate(network, lengthMeters, widthMeters, out _);

        /// <summary>
        /// Validates and snapshots both cubics for this vehicle's dimensions. Full body clearance
        /// is checked at the stop against every same-level lane; all cached manoeuvre samples must
        /// clear every same-level junction by its radius plus the body's half-diagonal and margin.
        /// Authored capacity includes the configured clearance on each side. A vehicle-specific
        /// rejection preserves an existing prepared snapshot and lease; invalid authored references,
        /// topology or cubic geometry invalidate it. Hot authoring during traffic remains unsupported.
        /// Uses fixed candidate buffers and commits both cubics atomically on success. No physics proof.
        /// </summary>
        public bool Validate(RoadNetwork network, float lengthMeters, float widthMeters, out string diagnostic)
        {
            diagnostic = null;
            if (!isActiveAndEnabled) return FailAuthored("Stop component or GameObject is disabled.", out diagnostic);
            if (string.IsNullOrWhiteSpace(_stopId) || _identityAmbiguous)
                return FailAuthored("StopId is empty or duplicated in its world registry.", out diagnostic);
            if (_location == null || !_location.isActiveAndEnabled || string.IsNullOrWhiteSpace(_location.LocationId) ||
                _approachFromNode == null || _approachNode == null || _departureNode == null || _departureNextNode == null ||
                _entry == null || _pullInFirstHandle == null || _pullInSecondHandle == null || _stopPose == null ||
                _pullOutFirstHandle == null || _pullOutSecondHandle == null || _exit == null || _headingMarker == null)
                return FailAuthored("Missing location, directed road references or authored controls.", out diagnostic);
            if (network == null) return Fail("Missing RoadNetwork argument.", out diagnostic);
            if (_prepared && _preparedNetwork != null && network != _preparedNetwork)
                return Fail("Stop geometry is already prepared for a different RoadNetwork.", out diagnostic);
            if (!Finite(_bayLengthMeters) || !Finite(_bayWidthMeters) || !Finite(_bodyClearanceMeters) ||
                !Finite(_throughLaneWidthMeters) || _bayLengthMeters <= 0f || _bayWidthMeters <= 0f ||
                _bodyClearanceMeters < 0f || _throughLaneWidthMeters <= 0f)
                return FailAuthored("Authored bay dimensions or clearance are invalid.", out diagnostic);
            CacheConflictNodes(network);
            if (!Registered(network, _approachFromNode) || !Registered(network, _approachNode) ||
                !Registered(network, _departureNode) || !Registered(network, _departureNextNode) ||
                !Connected(_approachFromNode, _approachNode) || !Connected(_departureNode, _departureNextNode) ||
                _location.AccessRoadNode != _approachNode || _candidateConflictNodes.Contains(_approachNode) ||
                _candidateConflictNodes.Contains(_departureNode))
                return FailAuthored("Stop requires registered directed edges, a matching location access node and non-junction endpoints.", out diagnostic);
            int elevationLevel = _approachNode.ElevationLevel;
            if (_approachFromNode.ElevationLevel != elevationLevel || _departureNode.ElevationLevel != elevationLevel ||
                _departureNextNode.ElevationLevel != elevationLevel)
                return FailAuthored("Both stop manoeuvres must stay on one authored elevation.", out diagnostic);
            var projection = network.GroundProjection;
            if (!projection.IsValid || !Finite(projection.Origin) || !Finite(projection.AxisX) || !Finite(projection.AxisY))
                return FailAuthored("Ground projection is invalid.", out diagnostic);
            Vector2 stopGroundPosition = projection.WorldToGround(_stopPose.position);
            Curve pullIn = _candidatePullIn;
            Curve pullOut = _candidatePullOut;
            Vector2 heading = projection.WorldToGround(_headingMarker.position) - stopGroundPosition;
            if (!Finite(stopGroundPosition) || !Finite(heading) || heading.sqrMagnitude < 0.000001f ||
                !pullIn.Prepare(projection, _entry, _pullInFirstHandle, _pullInSecondHandle, _stopPose) ||
                !pullOut.Prepare(projection, _stopPose, _pullOutFirstHandle, _pullOutSecondHandle, _exit))
                return FailAuthored("Authored cubic or stop heading is nonfinite, degenerate or has a stationary cusp.", out diagnostic);
            Vector2 stopGroundDirection = heading.normalized;
            Vector2 incoming = projection.WorldToGround(_approachNode.Position) - projection.WorldToGround(_approachFromNode.Position);
            Vector2 outgoing = projection.WorldToGround(_departureNextNode.Position) - projection.WorldToGround(_departureNode.Position);
            if (!Finite(incoming) || !Finite(outgoing) || incoming.sqrMagnitude < 0.000001f || outgoing.sqrMagnitude < 0.000001f ||
                !Finite(_approachNode.LaneOffsetMeters) || !Finite(_departureNextNode.LaneOffsetMeters))
                return FailAuthored("Directed lane geometry is degenerate or nonfinite.", out diagnostic);
            incoming.Normalize();
            outgoing.Normalize();
            Vector2 entry = projection.WorldToGround(_approachNode.Position) + Right(incoming) * _approachNode.LaneOffsetMeters;
            // Existing outgoing lane guidance uses the destination node's lane offset.
            Vector2 exit = projection.WorldToGround(_departureNode.Position) + Right(outgoing) * _departureNextNode.LaneOffsetMeters;
            if (Vector2.Distance(pullIn.Position(0f), entry) > EndpointTolerance ||
                Vector2.Distance(pullOut.Position(pullOut.Length), exit) > EndpointTolerance ||
                Vector2.Dot(pullIn.Tangent(0f), incoming) < HeadingAlignment ||
                Vector2.Dot(pullOut.Tangent(pullOut.Length), outgoing) < HeadingAlignment ||
                Vector2.Dot(pullIn.Tangent(pullIn.Length), stopGroundDirection) < HeadingAlignment ||
                Vector2.Dot(pullOut.Tangent(0f), stopGroundDirection) < HeadingAlignment)
                return FailAuthored("Cubic endpoints or travel tangents do not agree with directed lanes and the stop pose heading.", out diagnostic);
            // Vehicle suitability is transactional: a rejected visitor must not invalidate another
            // vehicle's prepared manoeuvres or lease. Authored-data failures above still fail closed.
            if (!Finite(lengthMeters) || !Finite(widthMeters) || lengthMeters <= 0f || widthMeters <= 0f ||
                lengthMeters + 2f * _bodyClearanceMeters > _bayLengthMeters ||
                widthMeters + 2f * _bodyClearanceMeters > _bayWidthMeters)
                return Fail("Vehicle dimensions exceed authored bay capacity or are invalid.", out diagnostic);
            float bodyRadius = 0.5f * Mathf.Sqrt(lengthMeters * lengthMeters + widthMeters * widthMeters);
            if (!Finite(bodyRadius)) return Fail("Vehicle body dimensions overflow.", out diagnostic);
            for (int i = 0; i < network.AllNodes.Count; i++)
            {
                RoadNode node = network.AllNodes[i];
                if (node == null || node.ElevationLevel != elevationLevel) continue;
                Vector2 from = projection.WorldToGround(node.Position);
                if (!Finite(from)) return FailAuthored("Same-elevation network contains nonfinite geometry.", out diagnostic);
                if (_candidateConflictNodes.Contains(node))
                {
                    float exclusion = node.IntersectionClearanceRadius + bodyRadius + _bodyClearanceMeters;
                    if (!Finite(node.IntersectionClearanceRadius) || node.IntersectionClearanceRadius < 0f)
                        return FailAuthored("Junction exclusion radius is invalid.", out diagnostic);
                    if (!Finite(exclusion) || !pullIn.ClearsCircle(from, exclusion) || !pullOut.ClearsCircle(from, exclusion))
                        return Fail($"Stop or manoeuvre enters junction '{node.NodeId}' body-clearance exclusion.", out diagnostic);
                }
                for (int j = 0; j < node.ConnectedNodes.Count; j++)
                {
                    RoadNode next = node.ConnectedNodes[j];
                    if (next == null || next.ElevationLevel != elevationLevel) continue;
                    Vector2 to = projection.WorldToGround(next.Position);
                    Vector2 edge = to - from;
                    if (!Finite(to) || !Finite(next.LaneOffsetMeters) || edge.sqrMagnitude < 0.000001f)
                        return FailAuthored("Same-elevation lane geometry is invalid.", out diagnostic);
                    Vector2 offset = Right(edge.normalized) * next.LaneOffsetMeters;
                    if (!BodyClearsLane(from + offset, to + offset, stopGroundPosition, stopGroundDirection, lengthMeters, widthMeters))
                        return Fail("Stopped vehicle body overlaps a through lane or its clearance margin.", out diagnostic);
                }
            }
            _candidatePullIn = _pullIn;
            _candidatePullOut = _pullOut;
            _pullIn = pullIn;
            _pullOut = pullOut;
            _stopGroundPosition = stopGroundPosition;
            _stopGroundDirection = stopGroundDirection;
            _elevationLevel = elevationLevel;
            _preparedNetwork = network;
            _prepared = true;
            return true;
        }

        public Vector2 PositionAtDistance(bool departure, float distance) =>
            IsPrepared ? (departure ? _pullOut : _pullIn).Position(distance) : Vector2.zero;
        public Vector2 TangentAtDistance(bool departure, float distance) =>
            IsPrepared ? (departure ? _pullOut : _pullIn).Tangent(distance) : Vector2.zero;
        public float CurvatureAtDistance(bool departure, float distance) =>
            IsPrepared ? (departure ? _pullOut : _pullIn).Curvature(distance) : 0f;
        public bool TryProject(bool departure, Vector2 point, out float distance, out float lateral)
        {
            distance = lateral = 0f;
            return IsPrepared && (departure ? _pullOut : _pullIn).TryProject(point, out distance, out lateral);
        }

        public bool IsAvailableFor(object owner) => owner != null && IsPrepared &&
            (_leaseOwner == null || ReferenceEquals(_leaseOwner, owner));
        public bool TryReserve(object owner)
        {
            if (!IsAvailableFor(owner)) return false;
            _leaseOwner = owner;
            return true;
        }
        public void Release(object owner)
        {
            if (owner != null && ReferenceEquals(_leaseOwner, owner)) _leaseOwner = null;
        }

        private bool BodyClearsLane(Vector2 from, Vector2 to, Vector2 stopGroundPosition, Vector2 stopGroundDirection,
            float length, float width)
        {
            // Distance from the directed lane segment to the stopped oriented body rectangle.
            Vector2 right = Right(stopGroundDirection);
            Vector2 a = new Vector2(Vector2.Dot(from - stopGroundPosition, stopGroundDirection), Vector2.Dot(from - stopGroundPosition, right));
            Vector2 b = new Vector2(Vector2.Dot(to - stopGroundPosition, stopGroundDirection), Vector2.Dot(to - stopGroundPosition, right));
            Vector2 half = new Vector2(length * 0.5f, width * 0.5f);
            if (Inside(a, half) || Inside(b, half)) return false;
            Vector2 c0 = new Vector2(-half.x, -half.y);
            Vector2 c1 = new Vector2(half.x, -half.y);
            Vector2 c2 = new Vector2(half.x, half.y);
            Vector2 c3 = new Vector2(-half.x, half.y);
            float clearance = _throughLaneWidthMeters * 0.5f + _bodyClearanceMeters;
            return SegmentDistance(a, b, c0, c1) >= clearance && SegmentDistance(a, b, c1, c2) >= clearance &&
                SegmentDistance(a, b, c2, c3) >= clearance && SegmentDistance(a, b, c3, c0) >= clearance;
        }

        private static bool Inside(Vector2 point, Vector2 half) => Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y;
        private static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 ab = b - a;
            Vector2 cd = d - c;
            float cross = Cross(ab, cd);
            if (Mathf.Abs(cross) > 0.000001f)
            {
                float t = Cross(c - a, cd) / cross;
                float u = Cross(c - a, ab) / cross;
                if (t >= 0f && t <= 1f && u >= 0f && u <= 1f) return 0f;
            }
            return Mathf.Min(PointDistance(a, c, d), PointDistance(b, c, d), PointDistance(c, a, b), PointDistance(d, a, b));
        }
        private static float PointDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float t = edge.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, edge) / edge.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + t * edge);
        }
        private void CacheConflictNodes(RoadNetwork network)
        {
            _candidateConflictNodes.Clear();
            if (network.AllNodes.Count == 0) network.RebuildNetwork();
            for (int i = 0; i < network.AllNodes.Count; i++)
            {
                var node = network.AllNodes[i];
                if (node == null) continue;
                // Disabled authoring stays discoverable, as in VehicleController conflict gating.
                if (node.IsIntersection || TrafficSignalController.Find(node) != null)
                    _candidateConflictNodes.Add(node);
                for (int j = 0; j < node.ConnectedNodes.Count; j++)
                {
                    var target = node.ConnectedNodes[j];
                    if (target != null && TrafficSignalApproach.Find(target, node) != null)
                        _candidateConflictNodes.Add(target);
                }
            }
        }

        private static bool Registered(RoadNetwork network, RoadNode node)
        {
            if (!node.isActiveAndEnabled || network.GetNode(node.NodeId) != node) return false;
            foreach (string duplicate in network.DuplicateNodeIds)
                if (duplicate == node.NodeId) return false;
            return true;
        }
        private static bool Connected(RoadNode from, RoadNode to)
        {
            for (int i = 0; i < from.ConnectedNodes.Count; i++)
                if (from.ConnectedNodes[i] == to) return true;
            return false;
        }
        private bool FailAuthored(string message, out string diagnostic)
        {
            _prepared = false;
            return Fail(message, out diagnostic);
        }
        private static bool Fail(string message, out string diagnostic) { diagnostic = message; return false; }
        private static Vector2 Right(Vector2 direction) => new Vector2(direction.y, -direction.x);
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        [ContextMenu("Validate Taxi Stop")]
        private void ValidateFromContextMenu()
        {
            var registry = GetComponentInParent<CityWorldRegistry>(includeInactive: true);
            var network = registry != null ? registry.RoadNetwork : GetComponentInParent<RoadNetwork>(includeInactive: true);
            if (registry != null) registry.RebuildRegistry();
            if (Validate(network, 4.5f, 2f, out var diagnostic))
                Debug.Log($"[TaxiStopAnchor] '{_stopId}' passed logical validation. Inspect the full swept body and authored surface locally.", this);
            else Debug.LogError($"[TaxiStopAnchor] '{_stopId}': {diagnostic}", this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.7f, 0.9f);
            DrawControlLine(_entry, _pullInFirstHandle);
            DrawControlLine(_pullInFirstHandle, _pullInSecondHandle);
            DrawControlLine(_pullInSecondHandle, _stopPose);
            Gizmos.color = new Color(0.9f, 0.7f, 0.3f, 0.9f);
            DrawControlLine(_stopPose, _pullOutFirstHandle);
            DrawControlLine(_pullOutFirstHandle, _pullOutSecondHandle);
            DrawControlLine(_pullOutSecondHandle, _exit);
            Gizmos.color = Color.cyan;
            DrawControlLine(_stopPose, _headingMarker);
            if (!IsPrepared) return;
            var registry = GetComponentInParent<CityWorldRegistry>(includeInactive: true);
            var network = registry != null ? registry.RoadNetwork : GetComponentInParent<RoadNetwork>(includeInactive: true);
            if (network == null) return;
            var projection = network.GroundProjection;
            Gizmos.color = Color.green;
            for (int i = 0; i < 32; i++)
            {
                Gizmos.DrawLine(projection.GroundToWorld(_pullIn.Position(_pullIn.Length * i / 32f)),
                    projection.GroundToWorld(_pullIn.Position(_pullIn.Length * (i + 1) / 32f)));
                Gizmos.DrawLine(projection.GroundToWorld(_pullOut.Position(_pullOut.Length * i / 32f)),
                    projection.GroundToWorld(_pullOut.Position(_pullOut.Length * (i + 1) / 32f)));
            }
        }
        private static void DrawControlLine(Transform from, Transform to)
        {
            if (from != null && to != null) Gizmos.DrawLine(from.position, to.position);
        }
#endif

        /// <summary>Fixed 32-interval cubic cache; query work is bounded and allocation-free.</summary>
        private sealed class Curve
        {
            private const int Samples = 32;
            private readonly Vector2[] _points = new Vector2[Samples + 1];
            private readonly float[] _distances = new float[Samples + 1];
            private Vector2 _p0, _p1, _p2, _p3;
            public float Length { get; private set; }
            public bool Prepare(IsometricGroundProjection projection, Transform p0, Transform p1, Transform p2, Transform p3)
            {
                _p0 = projection.WorldToGround(p0.position);
                _p1 = projection.WorldToGround(p1.position);
                _p2 = projection.WorldToGround(p2.position);
                _p3 = projection.WorldToGround(p3.position);
                Length = 0f;
                if (!Finite(_p0) || !Finite(_p1) || !Finite(_p2) || !Finite(_p3) || HasStationaryPoint()) return false;
                for (int i = 0; i <= Samples; i++)
                {
                    float t = i / (float)Samples;
                    _points[i] = Evaluate(t);
                    if (!Finite(_points[i]) || !Finite(Derivative(t)) || Derivative(t).sqrMagnitude < 0.000001f) return false;
                    if (i > 0)
                    {
                        float step = Vector2.Distance(_points[i - 1], _points[i]);
                        if (!Finite(step) || step < 0.0001f) return false;
                        Length += step;
                    }
                    _distances[i] = Length;
                }
                return Finite(Length) && Length > 0f;
            }
            public Vector2 Position(float distance) => Evaluate(Parameter(distance));
            public Vector2 Tangent(float distance) => Derivative(Parameter(distance)).normalized;
            public float Curvature(float distance)
            {
                float t = Parameter(distance);
                Vector2 first = Derivative(t);
                Vector2 second = 6f * ((1f - t) * (_p2 - 2f * _p1 + _p0) + t * (_p3 - 2f * _p2 + _p1));
                return Mathf.Abs(Cross(first, second)) / (first.sqrMagnitude * first.magnitude);
            }
            public bool ClearsCircle(Vector2 center, float radius)
            {
                // Chord distance also catches exclusion crossings between adjacent cache samples.
                for (int i = 0; i < Samples; i++)
                    if (PointDistance(center, _points[i], _points[i + 1]) <= radius) return false;
                return true;
            }
            public bool TryProject(Vector2 point, out float distance, out float lateral)
            {
                distance = lateral = 0f;
                if (!Finite(point)) return false;
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < Samples; i++)
                {
                    Vector2 edge = _points[i + 1] - _points[i];
                    float t = Mathf.Clamp01(Vector2.Dot(point - _points[i], edge) / edge.sqrMagnitude);
                    Vector2 offset = point - (_points[i] + t * edge);
                    float squared = offset.sqrMagnitude;
                    if (squared >= nearest) continue;
                    nearest = squared;
                    distance = Mathf.Lerp(_distances[i], _distances[i + 1], t);
                    lateral = Mathf.Sqrt(squared) * (Vector2.Dot(offset, Right(edge)) < 0f ? -1f : 1f);
                }
                return Finite(nearest) && Finite(distance) && Finite(lateral);
            }
            private float Parameter(float distance)
            {
                if (float.IsNaN(distance) || distance <= 0f) return 0f;
                if (distance >= Length) return 1f;
                int low = 0, high = Samples;
                while (high - low > 1)
                {
                    int middle = (low + high) / 2;
                    if (_distances[middle] < distance) low = middle; else high = middle;
                }
                return (low + (distance - _distances[low]) / (_distances[high] - _distances[low])) / Samples;
            }
            private Vector2 Evaluate(float t)
            {
                float u = 1f - t;
                return u * u * u * _p0 + 3f * u * u * t * _p1 + 3f * u * t * t * _p2 + t * t * t * _p3;
            }
            private Vector2 Derivative(float t)
            {
                float u = 1f - t;
                return 3f * u * u * (_p1 - _p0) + 6f * u * t * (_p2 - _p1) + 3f * t * t * (_p3 - _p2);
            }
            private bool HasStationaryPoint()
            {
                Vector2 a = 3f * (-_p0 + 3f * _p1 - 3f * _p2 + _p3);
                Vector2 b = 6f * (_p0 - 2f * _p1 + _p2);
                Vector2 c = 3f * (_p1 - _p0);
                bool useX = Mathf.Abs(a.x) + Mathf.Abs(b.x) + Mathf.Abs(c.x) >= Mathf.Abs(a.y) + Mathf.Abs(b.y) + Mathf.Abs(c.y);
                float qa = useX ? a.x : a.y, qb = useX ? b.x : b.y, qc = useX ? c.x : c.y;
                if (Mathf.Abs(qa) < 0.000001f) return Mathf.Abs(qb) > 0.000001f && StationaryAt(-qc / qb);
                float discriminant = qb * qb - 4f * qa * qc;
                if (!Finite(discriminant)) return true;
                if (discriminant < 0f) return false;
                float root = Mathf.Sqrt(discriminant);
                return StationaryAt((-qb - root) / (2f * qa)) || StationaryAt((-qb + root) / (2f * qa));
            }
            private bool StationaryAt(float t) => t >= 0f && t <= 1f && Derivative(t).sqrMagnitude < 0.000001f;
        }
    }
}
