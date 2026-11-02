using System;
using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Navigation;
using OutsiderDriver.World;

namespace OutsiderDriver.Vehicles
{
    // One movement controller, split only to keep the curb manoeuvre/state code readable.
    public partial class VehicleController
    {
        private enum CurbMotion { None, PullIn, Parked, PullOut }
        private CurbMotion _curbMotion;
        private TaxiStopAnchor _targetStop;
        private TaxiStopAnchor _parkedStop;
        private TaxiStopAnchor _maneuverStop;
        private TaxiStopAnchor _pendingStopAfterTurn;
        private List<RoadNode> _curbContinuationPath;
        private float _curbDistance;
        private bool _stopRouting;
        private bool _installingStopRoute;
        private bool _curbFailed;

        public float WidthMeters => _carModel != null ? _carModel.WidthMeters : 2f;
        private float CurbSpeed => Mathf.Min(_maxSpeed, _carModel != null ? _carModel.CurbSpeed : 2f);
        public TaxiStopAnchor ParkedStop => _curbMotion == CurbMotion.Parked ? _parkedStop : null;
        public bool IsCurbManeuvering => _curbMotion == CurbMotion.PullIn || _curbMotion == CurbMotion.PullOut;
        public bool HasPendingStopMotion => _stopRouting || IsCurbManeuvering || _curbContinuationPath != null;
        public RoadNode StopRouteContinuationNode => _maneuverStop != null ? _maneuverStop.DepartureNode
            : (_parkedStop != null ? _parkedStop.DepartureNode
                : (_turnTraversing && _currentPathIndex + 1 < _activePath.Count ? _activePath[_currentPathIndex + 1] : CurrentTargetNode));
        public RoadNode StopRouteContinuationNextNode => _maneuverStop != null ? _maneuverStop.DepartureNextNode
            : (_parkedStop != null ? _parkedStop.DepartureNextNode : null);
        public float RemainingCurbTravelMeters => _maneuverStop != null
            ? (_curbMotion == CurbMotion.PullIn ? Mathf.Max(0, _maneuverStop.PullInLength - _curbDistance) + _maneuverStop.PullOutLength
                : Mathf.Max(0, _maneuverStop.PullOutLength - _curbDistance))
            : (_parkedStop != null ? _parkedStop.PullOutLength : 0f);
        public event Action<TaxiStopAnchor> OnStopReached;
        public event Action<TaxiStopAnchor> OnStopNavigationFailed;

        public bool IsAtStop(TaxiStopAnchor stop)
        {
            return stop != null && stop.IsPrepared && _curbMotion == CurbMotion.Parked && _parkedStop == stop
                && !_curbFailed && _currentSpeed <= 0.001f && !HasPendingStopMotion
                && (GroundProjection.WorldToGround(transform.position) - stop.StopGroundPosition).sqrMagnitude <= 0.000001f
                && Vector2.Dot(_forwardGroundDirection, stop.StopGroundDirection) >= 0.999f;
        }

        public bool NavigateToStop(TaxiStopAnchor stop)
        {
            if (_roadNetwork == null || stop == null || _curbFailed
                || !stop.Prepare(_roadNetwork, LengthMeters, WidthMeters)) return false;
            if (IsAtStop(stop)) return true;
            if (_curbMotion == CurbMotion.Parked && _parkedStop == stop
                && _currentSpeed <= 0.001f
                && (GroundProjection.WorldToGround(transform.position) - stop.StopGroundPosition).sqrMagnitude <= 0.000001f
                && Vector2.Dot(_forwardGroundDirection, stop.StopGroundDirection) >= 0.999f)
            {
                // A replacement ride can revoke a departure that has not physically begun.
                var previousGoal = _targetStop;
                _targetStop = stop;
                _curbContinuationPath = null;
                _stopRouting = false;
                _isPaused = false;
                ReleaseSupersededStop(previousGoal);
                return true;
            }
            if (_curbMotion == CurbMotion.PullIn && _maneuverStop == stop)
            {
                // A new fare may want the same bay that a cancelled fare is still entering.
                // Finish this single approach rather than scheduling a needless departure loop.
                var previousGoal = _targetStop;
                _targetStop = stop;
                _curbContinuationPath = null;
                _stopRouting = false;
                _isPaused = false;
                ReleaseSupersededStop(previousGoal);
                return true;
            }
            if (!BuildStopPath(stop, out var path) || !stop.TryReserve(this)) return false;
            TaxiStopAnchor previous = _targetStop;
            if (IsCurbManeuvering || _curbMotion == CurbMotion.Parked)
            {
                _targetStop = stop;
                _curbContinuationPath = path;
                _isPaused = false;
                ReleaseSupersededStop(previous);
                return true;
            }
            if (_turnTraversing)
            {
                _pendingPath = path;
                _pendingStopAfterTurn = stop;
                _targetStop = stop;
                _stopRouting = true;
                _cancelAfterTurn = false;
                _isPaused = false;
                ReleaseSupersededStop(previous);
                return true;
            }
            if (!InstallStopPath(path, stop))
            {
                if (stop != previous) stop.Release(this);
                return false;
            }
            ReleaseSupersededStop(previous);
            return true;
        }

        private bool BuildStopPath(TaxiStopAnchor stop, out List<RoadNode> path)
        {
            RoadNode source = StopRouteContinuationNode ?? _roadNetwork.FindNearestNode(transform.position);
            return BuildDirectedRoute(source, StopRouteContinuationNextNode, stop.ApproachFromNode,
                stop.ApproachNode, false, false, out path);
        }

        private bool BuildDirectedRoute(RoadNode source, RoadNode forcedNext, RoadNode approachFrom, RoadNode destination,
            bool alternative, bool allowFallback, out List<RoadNode> route)
        {
            route = null;
            if (source == null || destination == null) return false;
            RoadNode start = forcedNext ?? source;
            RoadNode end = approachFrom ?? destination;
            var middle = alternative ? _roadNetwork.FindAlternativePath(start, end) : _roadNetwork.FindPath(start, end);
            if (alternative && (middle == null || middle.Count == 0) && allowFallback)
                middle = _roadNetwork.FindPath(start, end);
            if (middle == null || middle.Count == 0) return false;
            route = new List<RoadNode>();
            if (forcedNext != null) route.Add(source);
            route.AddRange(middle);
            if (route[route.Count - 1] != destination) route.Add(destination);
            return true;
        }

        private bool InstallStopPath(IReadOnlyList<RoadNode> path, TaxiStopAnchor stop)
        {
            _installingStopRoute = true;
            bool installed;
            try { installed = NavigateAlongPath(path); }
            finally { _installingStopRoute = false; }
            if (!installed) return false;
            _targetStop = stop;
            _pendingStopAfterTurn = null;
            _stopRouting = true;
            return true;
        }

        private bool QueueGraphDeparture(RoadNode destination, bool alternative, bool allowFallback)
        {
            if (_curbFailed || !BuildDirectedRoute(StopRouteContinuationNode, StopRouteContinuationNextNode,
                null, destination, alternative, allowFallback, out var path)) return false;
            TaxiStopAnchor previous = _targetStop;
            _targetStop = null;
            _stopRouting = false;
            _curbContinuationPath = path;
            _isPaused = false;
            ReleaseSupersededStop(previous);
            return true;
        }

        private void ReleaseSupersededStop(TaxiStopAnchor stop)
        {
            if (stop != null && stop != _targetStop && stop != _parkedStop && stop != _maneuverStop)
                stop.Release(this);
        }

        private void AbandonStopGoal()
        {
            var previous = _targetStop;
            _targetStop = null;
            _pendingStopAfterTurn = null;
            _stopRouting = false;
            ReleaseSupersededStop(previous);
        }

        private void ClearCurbState()
        {
            if (_targetStop != null) _targetStop.Release(this);
            if (_parkedStop != null) _parkedStop.Release(this);
            if (_maneuverStop != null) _maneuverStop.Release(this);
            _targetStop = _parkedStop = _maneuverStop = _pendingStopAfterTurn = null;
            _curbContinuationPath = null;
            _curbMotion = CurbMotion.None;
            _stopRouting = _curbFailed = false;
        }

        private void FailStopMotion()
        {
            if (_curbFailed) return;
            _curbFailed = true;
            _currentSpeed = 0f;
            _isYielding = true;
            OnStopNavigationFailed?.Invoke(_targetStop ?? _maneuverStop);
            // Retain physical pose and lease. Invalid authored data cannot authorize a recovery teleport.
        }

        private void BeginCurbArrival(TaxiStopAnchor stop)
        {
            UpdateReservationClearance();
            if (_reservedNode != null || stop == null || !stop.IsPrepared || !stop.IsAvailableFor(this))
            {
                FailStopMotion();
                return;
            }
            _activePath.Clear(); _currentPathIndex = 0; _initialIncomingNode = null;
            _turnPath = null; _turnTraversing = false; _turnSelectionIndex = -1;
            _stopRouting = false;
            _maneuverStop = stop;
            _curbDistance = 0f;
            _curbMotion = CurbMotion.PullIn;
        }

        private void ApplyCurbApproachLimit(ref float targetSpeed)
        {
            if (!_stopRouting || _targetStop == null || !_targetStop.IsPrepared
                || CurrentTargetNode != _targetStop.ApproachNode) return;
            float toEntry = (GroundProjection.WorldToGround(transform.position)
                - _targetStop.PositionAtDistance(false, 0)).magnitude;
            // Brake on the road before entering the bay; reaching the access node is not arrival.
            targetSpeed = Mathf.Min(targetSpeed, Mathf.Sqrt(CurbSpeed * CurbSpeed + 2f * _deceleration * toEntry));
        }

        private bool TickCurbMotion(float deltaTime)
        {
            if (_curbFailed) return true;
            if (_targetStop != null && !_targetStop.IsPrepared) { FailStopMotion(); return true; }
            if (_curbMotion == CurbMotion.None) return false;
            if (_isPaused) { ApplyDeceleration(deltaTime); return true; }
            if (_curbMotion == CurbMotion.Parked)
            {
                _currentSpeed = 0f;
                UpdateVisualHeading(deltaTime);
                if (_curbContinuationPath == null) return true;
                if (_parkedStop == null || !_parkedStop.IsPrepared) { FailStopMotion(); return true; }
                if (!DepartureLaneClear(_parkedStop)) { _isYielding = true; return true; }
                _maneuverStop = _parkedStop;
                _curbMotion = CurbMotion.PullOut;
                _curbDistance = 0f;
            }
            bool departure = _curbMotion == CurbMotion.PullOut;
            if (_maneuverStop == null || !_maneuverStop.IsPrepared) { FailStopMotion(); return true; }
            float length = departure ? _maneuverStop.PullOutLength : _maneuverStop.PullInLength;
            float remaining = Mathf.Max(0f, length - _curbDistance);
            _forwardGroundDirection = _maneuverStop.TangentAtDistance(departure, _curbDistance);
            _forwardDirection = GroundProjection.GroundDeltaToWorld(_forwardGroundDirection).normalized;
            float target = CurbSpeed;
            // Sample an anticipatory lateral-acceleration envelope on the same authored cubic.
            for (int i = 0; i <= 32; i++)
            {
                float distance = length * i / 32f;
                if (distance < _curbDistance) continue;
                float curvature = _maneuverStop.CurvatureAtDistance(departure, distance);
                if (curvature <= 0.000001f) continue;
                float turnLimitSquared = LateralAcceleration / curvature;
                target = Mathf.Min(target, Mathf.Sqrt(turnLimitSquared + 2f * _deceleration * (distance - _curbDistance)));
            }
            if (!departure) target = Mathf.Min(target, Mathf.Sqrt(2f * _deceleration * remaining));
            GetCurbFollowingLimit(departure, ref target, out float clearTravel);
            _isYielding = target < CurbSpeed - 0.01f;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, target,
                (target < _currentSpeed ? _deceleration : _acceleration) * deltaTime);
            _currentSpeed = Mathf.Min(_currentSpeed, clearTravel / deltaTime);
            float travel = Mathf.Min(_currentSpeed * deltaTime, remaining);
            _curbDistance += travel;
            Vector2 world = GroundProjection.GroundToWorld(_maneuverStop.PositionAtDistance(departure, _curbDistance));
            transform.position = new Vector3(world.x, world.y, transform.position.z);
            _forwardGroundDirection = _maneuverStop.TangentAtDistance(departure, _curbDistance);
            _forwardDirection = GroundProjection.GroundDeltaToWorld(_forwardGroundDirection).normalized;
            UpdateVisualHeading(deltaTime);
            if (_curbDistance >= length)
            {
                if (!departure)
                {
                    _currentSpeed = 0f;
                    _parkedStop = _maneuverStop;
                    _maneuverStop = null;
                    _curbMotion = CurbMotion.Parked;
                    _idleElevation = _parkedStop.ElevationLevel;
                    // A newer target may already be queued; listeners must identify the actual stop.
                    OnStopReached?.Invoke(_parkedStop);
                }
                else
                {
                    var origin = _maneuverStop;
                    var path = _curbContinuationPath;
                    var goal = _targetStop;
                    _curbContinuationPath = null;
                    _curbMotion = CurbMotion.None;
                    _maneuverStop = _parkedStop = null;
                    if (origin != goal) origin.Release(this); // a queued return still owns its target bay
                    _idleElevation = origin.ElevationLevel;
                    if (path == null || (goal != null ? !InstallStopPath(path, goal) : !NavigateAlongPath(path)))
                        FailStopMotion();
                }
            }
            return true;
        }

        private void GetCurbFollowingLimit(bool departure, ref float target, out float clearTravel)
        {
            clearTravel = float.PositiveInfinity;
            float horizon = Mathf.Max(_forwardSensorDistance,
                LengthMeters + MinimumGap + _currentSpeed * TimeHeadway + _currentSpeed * _currentSpeed / (2f * _deceleration));
            for (int i = 0; i < ActiveVehicles.Count; i++)
            {
                var other = ActiveVehicles[i];
                if (other == null || other == this || !other.isActiveAndEnabled || other._roadNetwork != _roadNetwork
                    || Mathf.Abs(other.TrafficElevation - _maneuverStop.ElevationLevel) >= 0.5f) continue;
                Vector2 point = GroundProjection.WorldToGround(other.transform.position);
                if (!_maneuverStop.TryProject(departure, point, out float arc, out float lateral)) continue;
                float ahead = arc - _curbDistance;
                if (ahead <= 0f || ahead > horizon || Mathf.Abs(lateral) > BodySensorHalfWidth(_maneuverStop.TangentAtDistance(departure, arc), other)) continue;
                if (other.HasActiveRoute && !other.IsCurbManeuvering
                    && Vector2.Dot(_maneuverStop.TangentAtDistance(departure, arc), other._forwardGroundDirection) <= 0.5f) continue;
                float gap = ahead - (LengthMeters + other.LengthMeters) * 0.5f;
                float speed = other._isPaused || !other.HasActiveRoute ? 0f : other._currentSpeed;
                target = Mathf.Min(target, CarFollowing.TargetSpeed(CurbSpeed, gap, speed,
                    other.Deceleration, _deceleration, MinimumGap, TimeHeadway));
                clearTravel = Mathf.Min(clearTravel, Mathf.Max(0f, gap - MinimumGap));
            }
        }

        private float BodySensorHalfWidth(Vector2 direction, VehicleController other)
        {
            // Include a merging body's swept lateral extent, even when its centre has
            // just left the old fixed corridor. Dedicated 3m opposing lanes remain separate.
            Vector2 dirNorm = direction.normalized;
            float parallel = Mathf.Abs(Vector2.Dot(dirNorm, other._forwardGroundDirection));
            float cross = Mathf.Abs(dirNorm.x * other._forwardGroundDirection.y - dirNorm.y * other._forwardGroundDirection.x);
            float otherHalfWidth = 0.5f * (other.WidthMeters * parallel + other.LengthMeters * cross);
            return Mathf.Min(2.85f, Mathf.Max(_forwardSensorHalfWidth, WidthMeters * 0.5f + otherHalfWidth + 0.1f));
        }

        private bool DepartureLaneClear(TaxiStopAnchor stop)
        {
            Vector2 exit = stop.PositionAtDistance(true, stop.PullOutLength);
            Vector2 direction = stop.TangentAtDistance(true, stop.PullOutLength);
            float slowestCruise = CurbSpeed;
            for (int sample = 0; sample <= 32; sample++)
            {
                float curvature = stop.CurvatureAtDistance(true, stop.PullOutLength * sample / 32f);
                if (curvature > 0.000001f) slowestCruise = Mathf.Min(slowestCruise, Mathf.Sqrt(LateralAcceleration / curvature));
            }
            slowestCruise = Mathf.Max(0.01f, slowestCruise);
            float acceleration = Mathf.Max(0.1f, _acceleration);
            float accelerationTime = Mathf.Min(slowestCruise / acceleration, Mathf.Sqrt(2f * stop.PullOutLength / acceleration));
            float accelerationDistance = 0.5f * acceleration * accelerationTime * accelerationTime;
            float manoeuvreTime = accelerationTime + Mathf.Max(0f, stop.PullOutLength - accelerationDistance) / slowestCruise;
            for (int i = 0; i < ActiveVehicles.Count; i++)
            {
                var other = ActiveVehicles[i];
                if (other == null || other == this || !other.isActiveAndEnabled || other._roadNetwork != _roadNetwork
                    || Mathf.Abs(other.TrafficElevation - stop.ElevationLevel) >= 0.5f) continue;
                Vector2 offset = GroundProjection.WorldToGround(other.transform.position) - exit;
                float along = Vector2.Dot(offset, direction);
                if (Mathf.Abs(Vector2.Dot(offset, new Vector2(direction.y, -direction.x))) > BodySensorHalfWidth(direction, other)) continue;
                if (other.HasActiveRoute && Vector2.Dot(direction, other._forwardGroundDirection) < -0.25f) continue;
                float speed = other._isPaused || !other.HasActiveRoute ? 0 : other.CurrentSpeed;
                float requiredGap = (LengthMeters + other.LengthMeters) * 0.5f + MinimumGap;
                requiredGap += along < 0f ? speed * (manoeuvreTime + TimeHeadway) + speed * speed / (2f * other.Deceleration)
                    : CurbSpeed * TimeHeadway;
                if (Mathf.Abs(along) < requiredGap) return false;
            }
            return true;
        }
    }
}
