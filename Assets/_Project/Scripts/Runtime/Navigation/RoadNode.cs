using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Navigation
{
    /// <summary>
    /// Represents a point on the road network (intersection, turn, or landmark pickup/dropoff point).
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class RoadNode : MonoBehaviour
    {
        [Tooltip("Optional human-readable name or landmark identifier.")]
        [SerializeField] private string _nodeId;

        [Tooltip("Connected outgoing nodes reachable from this node.")]
        [SerializeField] private List<RoadNode> _connectedNodes = new List<RoadNode>();

        public string NodeId => string.IsNullOrEmpty(_nodeId) ? gameObject.name : _nodeId;
        public Vector3 Position => transform.position;
        public IReadOnlyList<RoadNode> ConnectedNodes => _connectedNodes;

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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
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
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
#endif
    }
}
