using System;
using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Navigation;

namespace OutsiderDriver.Vehicles
{
    /// <summary>
    /// Controls semi-autonomous vehicle navigation along an authored RoadNetwork.
    /// Handles route following, speed acceleration/deceleration, and isometric orientation.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class VehicleController : MonoBehaviour
    {
        [Header("Road & Routing")]
        [SerializeField] private RoadNetwork _roadNetwork;
        [SerializeField] private float _waypointArrivalRadius = 0.25f;

        [Header("Movement Dynamics")]
        [SerializeField] private float _maxSpeed = 5f;
        [SerializeField] private float _acceleration = 4f;
        [SerializeField] private float _deceleration = 6f;

        [Header("Visuals")]
        [SerializeField] private Transform _visualsRoot;

        private readonly List<RoadNode> _activePath = new List<RoadNode>();
        private int _currentPathIndex = 0;
        private float _currentSpeed = 0f;
        private bool _isPaused = false;
        private IsometricDirection _currentFacing = IsometricDirection.NorthEast;

        public float CurrentSpeed => _currentSpeed;
        public float MaxSpeed => _maxSpeed;
        public bool IsMoving => _currentSpeed > 0.05f;
        public bool HasActiveRoute => _activePath.Count > 0 && _currentPathIndex < _activePath.Count;
        public RoadNode CurrentTargetNode => HasActiveRoute ? _activePath[_currentPathIndex] : null;
        public RoadNode DestinationNode => _activePath.Count > 0 ? _activePath[_activePath.Count - 1] : null;
        public IsometricDirection CurrentFacing => _currentFacing;

        public event Action<RoadNode> OnDestinationReached;
        public event Action<RoadNode> OnWaypointPassed;
        public event Action<IsometricDirection> OnFacingChanged;
        public event Action<IReadOnlyList<RoadNode>> OnRouteChanged;

        private void Start()
        {
            if (_roadNetwork == null)
            {
                _roadNetwork = FindAnyObjectByType<RoadNetwork>();
            }
        }

        private void Update()
        {
            if (_isPaused || !HasActiveRoute)
            {
                ApplyDeceleration(Time.deltaTime);
                return;
            }

            MoveAlongPath(Time.deltaTime);
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

            _activePath.Clear();
            _activePath.AddRange(path);
            _currentPathIndex = 0;
            _isPaused = false;

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
            _activePath.Clear();
            _currentPathIndex = 0;
            OnRouteChanged?.Invoke(_activePath);
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
                OnWaypointPassed?.Invoke(targetNode);
                _currentPathIndex++;

                if (_currentPathIndex >= _activePath.Count)
                {
                    // Reached destination
                    RoadNode reached = targetNode;
                    CancelRoute();
                    OnDestinationReached?.Invoke(reached);
                    return;
                }

                targetNode = _activePath[_currentPathIndex];
                targetPos = targetNode.Position;
                directionToTarget = targetPos - currentPos;
                directionToTarget.z = 0f;
            }

            // Update facing direction
            UpdateIsometricFacing(directionToTarget);

            // Accelerate towards max speed
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _maxSpeed, _acceleration * deltaTime);

            // Move position
            Vector3 movement = directionToTarget.normalized * (_currentSpeed * deltaTime);
            transform.position += movement;
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
                Gizmos.color = Color.cyan;
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
        }
#endif
    }
}
