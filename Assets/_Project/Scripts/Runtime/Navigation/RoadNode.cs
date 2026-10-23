using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Navigation
{
    /// <summary>
    /// Represents a point on the road network (intersection, turn, or landmark pickup/dropoff point).
    /// Also tracks intersection right-of-way priority and current vehicle reservation to prevent gridlock.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class RoadNode : MonoBehaviour
    {
        [Tooltip("Optional human-readable name or landmark identifier.")]
        [SerializeField] private string _nodeId;

        [Tooltip("Right-of-way rule when vehicles approach this node.")]
        [SerializeField] private RoadNodePriority _priority = RoadNodePriority.MainArtery;

        [Tooltip("Radius within which approaching vehicles check or reserve this intersection.")]
        [SerializeField] private float _intersectionClearanceRadius = 1.25f;

        [Tooltip("Connected outgoing nodes reachable from this node.")]
        [SerializeField] private List<RoadNode> _connectedNodes = new List<RoadNode>();

        private object _occupyingVehicle;

        public string NodeId => string.IsNullOrEmpty(_nodeId) ? gameObject.name : _nodeId;
        public Vector3 Position => transform.position;
        public RoadNodePriority Priority
        {
            get => _priority;
            set => _priority = value;
        }

        public float IntersectionClearanceRadius
        {
            get => _intersectionClearanceRadius;
            set => _intersectionClearanceRadius = Mathf.Max(0.1f, value);
        }

        public IReadOnlyList<RoadNode> ConnectedNodes => _connectedNodes;
        public bool IsIntersection => _connectedNodes.Count > 1 || _priority == RoadNodePriority.YieldIntersection;
        public object OccupyingVehicle => _occupyingVehicle;

        public void AddConnection(RoadNode target)
        {
            if (target != null && target != this && !_connectedNodes.Contains(target))
            {
                _connectedNodes.Add(target);
            }
        }

        public void RemoveConnection(RoadNode target)
        {
            _connectedNodes.Remove(target);
        }

        /// <summary>
        /// Returns true if the node can be entered by the requesting vehicle according to right-of-way rules.
        /// </summary>
        public bool CanVehicleEnter(object vehicle)
        {
            if (vehicle == null) return false;
            return _occupyingVehicle == null || ReferenceEquals(_occupyingVehicle, vehicle);
        }

        /// <summary>
        /// Attempts to reserve/occupy the intersection for the specified vehicle.
        /// Main-artery vehicles can claim an unreserved node or override if already theirs;
        /// YieldIntersection vehicles must wait until the node is completely free.
        /// </summary>
        public bool TryReserveIntersection(object vehicle)
        {
            if (vehicle == null) return false;

            if (_occupyingVehicle == null || ReferenceEquals(_occupyingVehicle, vehicle))
            {
                _occupyingVehicle = vehicle;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Releases the intersection reservation if held by the specified vehicle.
        /// </summary>
        public void ReleaseIntersection(object vehicle)
        {
            if (ReferenceEquals(_occupyingVehicle, vehicle))
            {
                _occupyingVehicle = null;
            }
        }

        /// <summary>
        /// Clears any active reservation on this node.
        /// </summary>
        public void ClearReservation()
        {
            _occupyingVehicle = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _priority == RoadNodePriority.YieldIntersection
                ? new Color(1f, 0.65f, 0.15f, 0.9f)
                : new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.25f);

            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.7f);
            foreach (var connected in _connectedNodes)
            {
                if (connected != null)
                {
                    Gizmos.DrawLine(transform.position, connected.transform.position);

                    // Draw a directional pointer halfway
                    Vector3 mid = Vector3.Lerp(transform.position, connected.transform.position, 0.65f);
                    Gizmos.DrawWireSphere(mid, 0.08f);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _intersectionClearanceRadius);
        }
#endif
    }
}
