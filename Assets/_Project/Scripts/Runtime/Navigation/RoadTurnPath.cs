using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Navigation
{
    /// <summary>
    /// A designer-authored cubic Bezier for one directed incoming/junction/outgoing movement.
    /// Controls are ordinary manually placed scene Transforms. Prepare snapshots them in ground
    /// metres; queries never read scene geometry or allocate. No geometry is generated from nodes.
    ///
    /// Designers must place the entire vehicle's swept envelope inside the visible driveable area,
    /// clear of sidewalks, opposing lanes and waiting traffic. Lane endpoint checks cannot prove
    /// that envelope from the sparse graph. Junction ownership is the caller's responsibility.
    /// Keep authoring GameObjects active at scene load so Awake registers even disabled components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadTurnPath : MonoBehaviour
    {
        private const int SampleCount = 32;
        private const float MinimumSegmentLength = 0.0001f;
        private const float LaneAlignmentTolerance = 0.15f;
        private const float MinimumHeadingAlignment = 0.995f;
        private static readonly List<RoadTurnPath> AuthoredPaths = new List<RoadTurnPath>();

        [SerializeField] private RoadNode _incoming;
        [SerializeField] private RoadNode _junction;
        [SerializeField] private RoadNode _outgoing;
        [Tooltip("Start on the incoming lane before the junction; manually author vehicle clearance.")]
        [SerializeField] private Transform _start;
        [Tooltip("First Bezier handle: start-to-handle direction must match incoming travel.")]
        [SerializeField] private Transform _firstHandle;
        [Tooltip("Second Bezier handle: handle-to-end direction must match outgoing travel.")]
        [SerializeField] private Transform _secondHandle;
        [Tooltip("End on the outgoing lane after the junction, with sufficient vehicle clearance.")]
        [SerializeField] private Transform _end;

        private Vector2 _p0;
        private Vector2 _p1;
        private Vector2 _p2;
        private Vector2 _p3;
        private float[] _distances;
        private Vector2[] _samples;
        private float _length;
        private bool _prepared;

        public RoadNode Incoming => _incoming;
        public RoadNode Junction => _junction;
        public RoadNode Outgoing => _outgoing;
        public bool IsValid => _prepared && isActiveAndEnabled;
        public float Length => _prepared ? _length : 0f;

        private void Awake() => RegisterAuthored();

        private void OnEnable()
        {
            RegisterAuthored();
        }

        private void OnDisable()
        {
            // Retain authored identity so unavailable geometry cannot become an uncontrolled turn.
            Invalidate();
        }

        private void OnDestroy()
        {
            AuthoredPaths.Remove(this);
        }

        private void OnValidate() => Invalidate();

        private void RegisterAuthored()
        {
            if (!AuthoredPaths.Contains(this)) AuthoredPaths.Add(this);
        }

        /// <summary>
        /// Returns the sole enabled authored movement, or null if missing/ambiguous. Does not prepare
        /// or validate geometry. Use HasAuthoredMovement to distinguish unavailable authored data
        /// from a movement with no authored curve, rather than silently cutting across a junction.
        /// </summary>
        public static RoadTurnPath Find(RoadNode incoming, RoadNode junction, RoadNode outgoing)
        {
            RoadTurnPath match = null;
            for (int i = 0; i < AuthoredPaths.Count; i++)
            {
                RoadTurnPath candidate = AuthoredPaths[i];
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.Matches(incoming, junction, outgoing)) continue;
                if (match != null) return null;
                match = candidate;
            }
            return match;
        }

        /// <summary>Includes registered disabled components so callers can fail closed.</summary>
        public static bool HasAuthoredMovement(RoadNode incoming, RoadNode junction, RoadNode outgoing)
        {
            for (int i = 0; i < AuthoredPaths.Count; i++)
            {
                RoadTurnPath candidate = AuthoredPaths[i];
                if (candidate != null && candidate.Matches(incoming, junction, outgoing)) return true;
            }
            return false;
        }

        private bool Matches(RoadNode incoming, RoadNode junction, RoadNode outgoing) =>
            incoming != null && junction != null && outgoing != null &&
            _incoming == incoming && _junction == junction && _outgoing == outgoing;

        /// <summary>Assigns authored scene references; callers must Prepare again after edits.</summary>
        public void Configure(RoadNode incoming, RoadNode junction, RoadNode outgoing,
            Transform start, Transform firstHandle, Transform secondHandle, Transform end)
        {
            _incoming = incoming;
            _junction = junction;
            _outgoing = outgoing;
            _start = start;
            _firstHandle = firstHandle;
            _secondHandle = secondHandle;
            _end = end;
            RegisterAuthored();
            Invalidate();
        }

        /// <summary>
        /// Snapshots manually authored controls and builds a bounded arc-length approximation.
        /// Call once when selecting a route, not every tick. Geometry and lane alignment use ground
        /// metres, with junction/outgoing offsets matching existing right-hand lane guidance.
        /// Rejects missing topology, degenerate/nonfinite curves and mismatched entry/exit headings.
        /// This does not certify sidewalk, curb, body clearance or conflict-area safety.
        /// </summary>
        public bool Prepare(IsometricGroundProjection projection)
        {
            Invalidate();
            if (!isActiveAndEnabled || !projection.IsValid || !Finite(projection.Origin) ||
                !Finite(projection.AxisX) || !Finite(projection.AxisY) ||
                _incoming == null || _junction == null || _outgoing == null ||
                _incoming == _junction || _junction == _outgoing || _incoming == _outgoing ||
                _start == null || _firstHandle == null || _secondHandle == null || _end == null ||
                !Connected(_incoming, _junction) || !Connected(_junction, _outgoing) ||
                !_incoming.CanTraverseElevationTo(_junction) || !_junction.CanTraverseElevationTo(_outgoing) ||
                _incoming.ElevationLevel != _junction.ElevationLevel || _outgoing.ElevationLevel != _junction.ElevationLevel)
                return false;

            _p0 = projection.WorldToGround(_start.position);
            _p1 = projection.WorldToGround(_firstHandle.position);
            _p2 = projection.WorldToGround(_secondHandle.position);
            _p3 = projection.WorldToGround(_end.position);
            if (!Finite(_p0) || !Finite(_p1) || !Finite(_p2) || !Finite(_p3) || HasStationaryPoint()) return false;

            Vector2 incoming = projection.WorldToGround(_incoming.Position);
            Vector2 junction = projection.WorldToGround(_junction.Position);
            Vector2 outgoing = projection.WorldToGround(_outgoing.Position);
            if (!AlignedEndpoint(_p0, _p1 - _p0, incoming, junction, _junction.LaneOffsetMeters) ||
                !AlignedEndpoint(_p3, _p3 - _p2, junction, outgoing, _outgoing.LaneOffsetMeters))
                return false;

            if (_distances == null) _distances = new float[SampleCount + 1];
            if (_samples == null) _samples = new Vector2[SampleCount + 1];
            _distances[0] = 0f;
            Vector2 previous = _p0;
            float length = 0f;
            for (int i = 0; i <= SampleCount; i++)
            {
                float t = i / (float)SampleCount;
                Vector2 position = Evaluate(t);
                Vector2 derivative = Derivative(t);
                float curvature = Curvature(t);
                if (!Finite(position) || !Finite(derivative) || derivative.sqrMagnitude < 0.000001f ||
                    !Finite(curvature)) return false;
                if (i > 0)
                {
                    float segmentLength = Vector2.Distance(previous, position);
                    if (!Finite(segmentLength) || segmentLength < MinimumSegmentLength) return false;
                    length += segmentLength;
                    if (!Finite(length)) return false;
                    _distances[i] = length;
                }
                _samples[i] = position;
                previous = position;
            }
            _length = length;
            _prepared = true;
            return true;
        }

        /// <summary>Ground-space position, clamped to the prepared endpoints. Check IsValid first.</summary>
        public Vector2 PositionAtDistance(float distance) => IsValid ? Evaluate(ParameterAtDistance(distance)) : Vector2.zero;

        /// <summary>Unit ground-space travel direction, continuous on a valid authored cubic.</summary>
        public Vector2 TangentAtDistance(float distance) => IsValid ? Derivative(ParameterAtDistance(distance)).normalized : Vector2.zero;

        /// <summary>Unsigned ground-plane curvature in inverse metres.</summary>
        public float CurvatureAtDistance(float distance) => IsValid ? Curvature(ParameterAtDistance(distance)) : 0f;

        /// <summary>
        /// Local speed limit in metres/second from lateral acceleration in metres/second squared.
        /// The caller must also brake in advance of future curvature and obey queues/junction gates.
        /// </summary>
        public float SpeedLimitAtDistance(float distance, float lateralAcceleration, float maxSpeed)
        {
            if (!IsValid || !Finite(lateralAcceleration) || !Finite(maxSpeed) || maxSpeed < 0f || lateralAcceleration < 0f)
                return 0f;
            float curvature = CurvatureAtDistance(distance);
            return curvature <= 0.000001f ? maxSpeed : Mathf.Min(maxSpeed, Mathf.Sqrt(lateralAcceleration / curvature));
        }

        /// <summary>
        /// Projects a ground-space point onto the cached 32-segment arc. Returns along-arc metres
        /// and signed distance to the closest point (positive on the travel direction's right).
        /// Endpoint distance includes longitudinal overshoot, so distant points beyond an endpoint
        /// are not mistaken for nearby in-lane vehicles. Bounded and allocation-free.
        /// </summary>
        public bool TryProject(Vector2 groundPoint, out float distance, out float lateral)
        {
            distance = 0f;
            lateral = 0f;
            if (!IsValid || !Finite(groundPoint)) return false;
            float nearestSquared = float.PositiveInfinity;
            for (int i = 0; i < SampleCount; i++)
            {
                Vector2 segment = _samples[i + 1] - _samples[i];
                float fraction = Mathf.Clamp01(Vector2.Dot(groundPoint - _samples[i], segment) / segment.sqrMagnitude);
                Vector2 offset = groundPoint - (_samples[i] + fraction * segment);
                float squared = offset.sqrMagnitude;
                if (squared >= nearestSquared) continue;
                nearestSquared = squared;
                distance = Mathf.Lerp(_distances[i], _distances[i + 1], fraction);
                float side = Vector2.Dot(offset, new Vector2(segment.y, -segment.x));
                lateral = Mathf.Sqrt(squared) * (side < 0f ? -1f : 1f);
            }
            return Finite(nearestSquared) && Finite(distance) && Finite(lateral);
        }

        // The cubic's derivative is quadratic. Check candidate roots of one nonconstant
        // component so a cusp between cache samples cannot masquerade as a smooth curve.
        private bool HasStationaryPoint()
        {
            Vector2 a = 3f * (-_p0 + 3f * _p1 - 3f * _p2 + _p3);
            Vector2 b = 6f * (_p0 - 2f * _p1 + _p2);
            Vector2 c = 3f * (_p1 - _p0);
            bool useX = Mathf.Abs(a.x) + Mathf.Abs(b.x) + Mathf.Abs(c.x) >=
                Mathf.Abs(a.y) + Mathf.Abs(b.y) + Mathf.Abs(c.y);
            float qa = useX ? a.x : a.y;
            float qb = useX ? b.x : b.y;
            float qc = useX ? c.x : c.y;
            if (Mathf.Abs(qa) < 0.000001f)
                return Mathf.Abs(qb) > 0.000001f && StationaryAt(-qc / qb);
            float discriminant = qb * qb - 4f * qa * qc;
            if (discriminant < 0f) return false;
            float root = Mathf.Sqrt(discriminant);
            return StationaryAt((-qb - root) / (2f * qa)) || StationaryAt((-qb + root) / (2f * qa));
        }

        private bool StationaryAt(float t) => t >= 0f && t <= 1f && Derivative(t).sqrMagnitude < 0.000001f;

        private float ParameterAtDistance(float distance)
        {
            if (float.IsNaN(distance) || distance <= 0f) return 0f;
            if (distance >= _length) return 1f;
            int low = 0;
            int high = SampleCount;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (_distances[middle] < distance) low = middle;
                else high = middle;
            }
            return (low + (distance - _distances[low]) / (_distances[high] - _distances[low])) / SampleCount;
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

        private float Curvature(float t)
        {
            Vector2 first = Derivative(t);
            Vector2 second = 6f * ((1f - t) * (_p2 - 2f * _p1 + _p0) + t * (_p3 - 2f * _p2 + _p1));
            float denominator = first.sqrMagnitude * first.magnitude;
            return denominator > 0f ? Mathf.Abs(first.x * second.y - first.y * second.x) / denominator : float.PositiveInfinity;
        }

        private static bool AlignedEndpoint(Vector2 point, Vector2 tangent, Vector2 from, Vector2 to, float laneOffset)
        {
            Vector2 segment = to - from;
            float length = segment.magnitude;
            if (!Finite(from) || !Finite(to) || !Finite(length) || !Finite(laneOffset) ||
                length < 0.001f || tangent.sqrMagnitude < 0.000001f) return false;
            Vector2 direction = segment / length;
            Vector2 right = new Vector2(direction.y, -direction.x);
            Vector2 laneStart = from + right * laneOffset;
            Vector2 delta = point - laneStart;
            float along = Vector2.Dot(delta, direction);
            return along >= 0f && along <= length &&
                Mathf.Abs(Vector2.Dot(delta, right)) <= LaneAlignmentTolerance &&
                Vector2.Dot(tangent.normalized, direction) >= MinimumHeadingAlignment;
        }

        private static bool Connected(RoadNode from, RoadNode to)
        {
            for (int i = 0; i < from.ConnectedNodes.Count; i++)
                if (from.ConnectedNodes[i] == to) return true;
            return false;
        }

        private void Invalidate()
        {
            _prepared = false;
            _length = 0f;
        }

        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
