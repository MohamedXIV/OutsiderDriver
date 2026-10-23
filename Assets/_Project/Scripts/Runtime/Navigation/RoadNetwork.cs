using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Navigation
{
    /// <summary>
    /// Manages the authored road graph and calculates valid paths between road nodes.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoadNetwork : MonoBehaviour
    {
        [Tooltip("Root transform containing all road nodes. If null, will scan children.")]
        [SerializeField] private Transform _nodesRoot;

        private readonly List<RoadNode> _registeredNodes = new List<RoadNode>();

        public IReadOnlyList<RoadNode> AllNodes => _registeredNodes;

        private void Awake()
        {
            RebuildNetwork();
        }

        /// <summary>
        /// Scans and indexes all RoadNode components under the network root.
        /// </summary>
        [ContextMenu("Rebuild Network")]
        public void RebuildNetwork()
        {
            _registeredNodes.Clear();
            Transform root = _nodesRoot != null ? _nodesRoot : transform;
            var nodes = root.GetComponentsInChildren<RoadNode>();
            _registeredNodes.AddRange(nodes);
        }

        /// <summary>
        /// Finds the closest RoadNode to a given world position.
        /// </summary>
        public RoadNode FindNearestNode(Vector3 position)
        {
            if (_registeredNodes.Count == 0)
            {
                RebuildNetwork();
            }

            RoadNode nearest = null;
            float minSqrDistance = float.MaxValue;

            for (int i = 0; i < _registeredNodes.Count; i++)
            {
                var node = _registeredNodes[i];
                if (node == null) continue;

                float sqrDist = (node.Position - position).sqrMagnitude;
                if (sqrDist < minSqrDistance)
                {
                    minSqrDistance = sqrDist;
                    nearest = node;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Computes the shortest path of connected nodes between start and destination using A*.
        /// </summary>
        public List<RoadNode> FindPath(RoadNode start, RoadNode destination)
        {
            if (start == null || destination == null)
            {
                return new List<RoadNode>();
            }

            if (start == destination)
            {
                return new List<RoadNode> { start };
            }

            var openSet = new List<RoadNode> { start };
            var cameFrom = new Dictionary<RoadNode, RoadNode>();
            var gScore = new Dictionary<RoadNode, float> { [start] = 0f };
            var fScore = new Dictionary<RoadNode, float> { [start] = Vector3.Distance(start.Position, destination.Position) };

            while (openSet.Count > 0)
            {
                // Find node with lowest fScore
                RoadNode current = openSet[0];
                float lowestF = fScore.TryGetValue(current, out float score) ? score : float.MaxValue;

                for (int i = 1; i < openSet.Count; i++)
                {
                    var candidate = openSet[i];
                    float candidateF = fScore.TryGetValue(candidate, out float f) ? f : float.MaxValue;
                    if (candidateF < lowestF)
                    {
                        current = candidate;
                        lowestF = candidateF;
                    }
                }

                if (current == destination)
                {
                    return ReconstructPath(cameFrom, current);
                }

                openSet.Remove(current);

                foreach (var neighbor in current.ConnectedNodes)
                {
                    if (neighbor == null) continue;

                    float tentativeG = gScore[current] + Vector3.Distance(current.Position, neighbor.Position);
                    float currentNeighborG = gScore.TryGetValue(neighbor, out float ng) ? ng : float.MaxValue;

                    if (tentativeG < currentNeighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Vector3.Distance(neighbor.Position, destination.Position);

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            // Path not found
            return new List<RoadNode>();
        }

        private List<RoadNode> ReconstructPath(Dictionary<RoadNode, RoadNode> cameFrom, RoadNode current)
        {
            var totalPath = new List<RoadNode> { current };
            while (cameFrom.TryGetValue(current, out var prev))
            {
                current = prev;
                totalPath.Insert(0, current);
            }
            return totalPath;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_registeredNodes.Count == 0 && !Application.isPlaying)
            {
                RebuildNetwork();
            }
        }
#endif
    }
}
