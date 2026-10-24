using System;
using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Data;

namespace OutsiderDriver.Navigation
{
    /// <summary>
    /// Manages the authored road graph and calculates valid paths between road nodes.
    /// All navigation distances are expressed in ground-plane metres, independent of the
    /// isometric world projection (one authored grid cell equals one metre).
    /// </summary>
    [DisallowMultipleComponent]
    public class RoadNetwork : MonoBehaviour
    {
        [Tooltip("Root transform containing all road nodes. If null, will scan children.")]
        [SerializeField] private Transform _nodesRoot;

        [Tooltip("Isometric grid used to convert between world space and authored ground metres. Found automatically when empty.")]
        [SerializeField] private Grid _isometricGrid;

        [Tooltip("Authored baseline street class used by the current production streets.")]
        [SerializeField] private StreetProfileSO _defaultStreetProfile;

        private readonly List<RoadNode> _registeredNodes = new List<RoadNode>();
        private readonly Dictionary<string, RoadNode> _nodesById = new Dictionary<string, RoadNode>(StringComparer.Ordinal);
        private readonly HashSet<RoadNode> _registeredNodeSet = new HashSet<RoadNode>();
        private readonly HashSet<string> _duplicateNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private IsometricGroundProjection _groundProjection;

        public IReadOnlyList<RoadNode> AllNodes => _registeredNodes;
        public IReadOnlyCollection<string> DuplicateNodeIds => _duplicateNodeIds;
        public StreetProfileSO DefaultStreetProfile => _defaultStreetProfile;
        public IsometricGroundProjection GroundProjection
        {
            get
            {
                EnsureGroundProjection();
                return _groundProjection;
            }
        }

        private void Awake()
        {
            EnsureGroundProjection();
            RebuildNetwork();

            if (_duplicateNodeIds.Count > 0)
            {
                Debug.LogError($"[RoadNetwork] Duplicate RoadNode IDs found: {string.Join(", ", _duplicateNodeIds)}. Duplicate nodes are excluded from ID lookup.", this);
            }
        }

        public void ConfigureGrid(Grid isometricGrid)
        {
            _isometricGrid = isometricGrid;
            _groundProjection = isometricGrid != null
                ? IsometricGroundProjection.FromGrid(isometricGrid)
                : IsometricGroundProjection.Default;
        }

        /// <summary>
        /// Scans and indexes all RoadNode components under the network root.
        /// </summary>
        [ContextMenu("Rebuild Network")]
        public void RebuildNetwork()
        {
            EnsureGroundProjection();

            _registeredNodes.Clear();
            _nodesById.Clear();
            _registeredNodeSet.Clear();
            _duplicateNodeIds.Clear();

            Transform root = _nodesRoot != null ? _nodesRoot : transform;
            var nodes = root.GetComponentsInChildren<RoadNode>();
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null) continue;

                _registeredNodes.Add(node);
                _registeredNodeSet.Add(node);

                string id = node.NodeId;
                if (string.IsNullOrEmpty(id)) continue;

                if (_nodesById.ContainsKey(id))
                {
                    _duplicateNodeIds.Add(id);
                    continue;
                }

                _nodesById[id] = node;
            }
        }

        /// <summary>
        /// Looks up a registered RoadNode by its stable NodeId.
        /// </summary>
        public bool TryGetNode(string nodeId, out RoadNode node)
        {
            node = null;
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (_registeredNodes.Count == 0)
            {
                RebuildNetwork();
            }

            return _nodesById.TryGetValue(nodeId, out node);
        }

        /// <summary>
        /// Returns the RoadNode with the given stable NodeId, or null if not found.
        /// </summary>
        public RoadNode GetNode(string nodeId)
        {
            return TryGetNode(nodeId, out var node) ? node : null;
        }

        /// <summary>
        /// Finds the closest RoadNode to a given world position using ground-plane distance.
        /// </summary>
        public RoadNode FindNearestNode(Vector3 position)
        {
            if (_registeredNodes.Count == 0)
            {
                RebuildNetwork();
            }

            var projection = GroundProjection;
            RoadNode nearest = null;
            float minSqrDistance = float.MaxValue;
            Vector2 groundPosition = projection.WorldToGround(position);

            for (int i = 0; i < _registeredNodes.Count; i++)
            {
                var node = _registeredNodes[i];
                if (node == null) continue;

                float sqrDist = (projection.WorldToGround(node.Position) - groundPosition).sqrMagnitude;
                if (sqrDist < minSqrDistance)
                {
                    minSqrDistance = sqrDist;
                    nearest = node;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Computes the shortest path of connected nodes between start and destination using A*,
        /// optionally avoiding a specified set of intermediate nodes.
        /// Respects road elevation rules (visually overlapping roads at different elevations cannot connect without a ramp).
        /// </summary>
        public List<RoadNode> FindPath(RoadNode start, RoadNode destination, ICollection<RoadNode> avoidedNodes = null)
        {
            if (start == null || destination == null)
            {
                return new List<RoadNode>();
            }

            if (start == destination)
            {
                return new List<RoadNode> { start };
            }

            if (_registeredNodes.Count == 0)
            {
                RebuildNetwork();
            }

            var projection = GroundProjection;
            var openSet = new List<RoadNode> { start };
            var cameFrom = new Dictionary<RoadNode, RoadNode>();
            var gScore = new Dictionary<RoadNode, float> { [start] = 0f };
            var fScore = new Dictionary<RoadNode, float> { [start] = projection.Distance(start.Position, destination.Position) };

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

                    // Never route through a node that is not part of this authored network.
                    if (!_registeredNodeSet.Contains(neighbor)) continue;

                    // Enforce elevation separation unless an authored ramp connects the levels
                    if (!current.CanTraverseElevationTo(neighbor))
                    {
                        continue;
                    }

                    // Skip avoided intermediate nodes (start and destination themselves are never skipped)
                    if (avoidedNodes != null && neighbor != start && neighbor != destination && avoidedNodes.Contains(neighbor))
                    {
                        continue;
                    }

                    float tentativeG = gScore[current] + projection.Distance(current.Position, neighbor.Position);
                    float currentNeighborG = gScore.TryGetValue(neighbor, out float ng) ? ng : float.MaxValue;

                    if (tentativeG < currentNeighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + projection.Distance(neighbor.Position, destination.Position);

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

        /// <summary>
        /// Computes a valid alternative route between start and destination that differs from the primary route.
        /// Evaluates candidate detours by avoiding intermediate intersection nodes from the primary path
        /// and returns the shortest distinct route.
        /// </summary>
        public List<RoadNode> FindAlternativePath(RoadNode start, RoadNode destination, IReadOnlyList<RoadNode> primaryPath = null)
        {
            if (start == null || destination == null || start == destination)
            {
                return new List<RoadNode>();
            }

            var referencePath = primaryPath ?? FindPath(start, destination);
            if (referencePath == null || referencePath.Count < 3)
            {
                return new List<RoadNode>();
            }

            List<RoadNode> bestAlternative = null;
            float bestAlternativeLength = float.MaxValue;
            var avoided = new HashSet<RoadNode>();

            // First try avoiding each intermediate node individually to find the cleanest detour at an intersection
            for (int i = 1; i < referencePath.Count - 1; i++)
            {
                avoided.Clear();
                avoided.Add(referencePath[i]);

                var candidate = FindPath(start, destination, avoided);
                if (candidate.Count >= 2 && !ArePathsIdentical(referencePath, candidate))
                {
                    float length = CalculatePathLength(candidate);
                    if (length < bestAlternativeLength)
                    {
                        bestAlternativeLength = length;
                        bestAlternative = candidate;
                    }
                }
            }

            return bestAlternative ?? new List<RoadNode>();
        }

        /// <summary>
        /// Computes a route from start to destination passing through a specific intermediate waypoint node.
        /// </summary>
        public List<RoadNode> FindPathViaWaypoint(RoadNode start, RoadNode waypoint, RoadNode destination)
        {
            if (start == null || waypoint == null || destination == null)
            {
                return new List<RoadNode>();
            }

            var firstLeg = FindPath(start, waypoint);
            if (firstLeg.Count == 0)
            {
                return new List<RoadNode>();
            }

            var secondLeg = FindPath(waypoint, destination);
            if (secondLeg.Count == 0)
            {
                return new List<RoadNode>();
            }

            var combined = new List<RoadNode>(firstLeg.Count + secondLeg.Count - 1);
            combined.AddRange(firstLeg);
            for (int i = 1; i < secondLeg.Count; i++)
            {
                combined.Add(secondLeg[i]);
            }
            return combined;
        }

        /// <summary>
        /// Validates the authored road network for duplicate IDs, missing references, illegal elevation
        /// transitions, one-way links without a return path, and nodes disconnected from the main graph.
        /// </summary>
        public bool ValidateNetwork(out List<string> issues)
        {
            issues = new List<string>();
            EnsureGroundProjection();
            RebuildNetwork();

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var weakAdjacency = new Dictionary<RoadNode, List<RoadNode>>();

            for (int i = 0; i < _registeredNodes.Count; i++)
            {
                var node = _registeredNodes[i];
                if (node == null) continue;

                if (!weakAdjacency.ContainsKey(node))
                {
                    weakAdjacency[node] = new List<RoadNode>();
                }

                if (string.IsNullOrWhiteSpace(node.NodeId))
                {
                    issues.Add($"RoadNode at {node.Position} has an empty NodeId.");
                }
                else if (!seenIds.Add(node.NodeId))
                {
                    issues.Add($"Duplicate RoadNode ID detected: '{node.NodeId}'. Duplicate identities make lookups and authored links ambiguous.");
                }

                var connections = node.ConnectedNodes;
                if (connections.Count == 0)
                {
                    issues.Add($"RoadNode '{node.NodeId}' has no outgoing connections.");
                }

                var seenNeighbors = new HashSet<RoadNode>();
                for (int j = 0; j < connections.Count; j++)
                {
                    var neighbor = connections[j];
                    if (neighbor == null)
                    {
                        issues.Add($"RoadNode '{node.NodeId}' contains a null connection reference.");
                        continue;
                    }

                    if (ReferenceEquals(neighbor, node))
                    {
                        issues.Add($"RoadNode '{node.NodeId}' is connected to itself.");
                        continue;
                    }

                    if (!seenNeighbors.Add(neighbor))
                    {
                        issues.Add($"RoadNode '{node.NodeId}' lists duplicate connection '{neighbor.NodeId}'.");
                    }

                    if (!_registeredNodeSet.Contains(neighbor))
                    {
                        issues.Add($"RoadNode '{node.NodeId}' connects to unregistered RoadNode '{neighbor.NodeId}'. Keep both nodes under the RoadNetwork root.");
                        continue;
                    }

                    if (!weakAdjacency.TryGetValue(neighbor, out var neighborLinks))
                    {
                        neighborLinks = new List<RoadNode>();
                        weakAdjacency[neighbor] = neighborLinks;
                    }

                    weakAdjacency[node].Add(neighbor);
                    neighborLinks.Add(node);

                    if (!node.CanTraverseElevationTo(neighbor))
                    {
                        issues.Add($"RoadNode '{node.NodeId}' (elevation {node.ElevationLevel}) connects to '{neighbor.NodeId}' (elevation {neighbor.ElevationLevel}) without an authored ramp.");
                    }
                    else if (!HasConnection(neighbor, node))
                    {
                        issues.Add($"RoadNode '{node.NodeId}' has a one-way link to '{neighbor.NodeId}' with no return link. Author the reverse link or remove the one-way link.");
                    }
                }
            }

            AddDisconnectedComponentIssues(issues, weakAdjacency);
            return issues.Count == 0;
        }

        private static bool HasConnection(RoadNode from, RoadNode to)
        {
            var connections = from.ConnectedNodes;
            for (int i = 0; i < connections.Count; i++)
            {
                if (ReferenceEquals(connections[i], to))
                {
                    return true;
                }
            }
            return false;
        }

        private void AddDisconnectedComponentIssues(List<string> issues, Dictionary<RoadNode, List<RoadNode>> weakAdjacency)
        {
            if (_registeredNodes.Count < 2)
            {
                return;
            }

            RoadNode first = null;
            for (int i = 0; i < _registeredNodes.Count; i++)
            {
                if (_registeredNodes[i] != null)
                {
                    first = _registeredNodes[i];
                    break;
                }
            }

            if (first == null) return;

            var visited = new HashSet<RoadNode> { first };
            var queue = new Queue<RoadNode>();
            queue.Enqueue(first);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!weakAdjacency.TryGetValue(current, out var links)) continue;

                for (int i = 0; i < links.Count; i++)
                {
                    var linked = links[i];
                    if (linked != null && visited.Add(linked))
                    {
                        queue.Enqueue(linked);
                    }
                }
            }

            for (int i = 0; i < _registeredNodes.Count; i++)
            {
                var node = _registeredNodes[i];
                if (node != null && !visited.Contains(node))
                {
                    issues.Add($"RoadNode '{node.NodeId}' is disconnected from the main road network component.");
                }
            }
        }

        private float CalculatePathLength(IReadOnlyList<RoadNode> path)
        {
            if (path == null || path.Count < 2) return 0f;

            var projection = GroundProjection;
            float total = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (path[i] != null && path[i + 1] != null)
                {
                    total += projection.Distance(path[i].Position, path[i + 1].Position);
                }
            }
            return total;
        }

        private static bool ArePathsIdentical(IReadOnlyList<RoadNode> a, IReadOnlyList<RoadNode> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
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

        private void EnsureGroundProjection()
        {
            if (_groundProjection.IsValid) return;

            if (_isometricGrid == null)
            {
                _isometricGrid = FindAnyObjectByType<Grid>();
            }

            _groundProjection = _isometricGrid != null
                ? IsometricGroundProjection.FromGrid(_isometricGrid)
                : IsometricGroundProjection.Default;
        }

        [ContextMenu("Validate Network")]
        private void ValidateNetworkFromContextMenu()
        {
            if (ValidateNetwork(out var issues))
            {
                Debug.Log($"[RoadNetwork] Validation passed for {_registeredNodes.Count} nodes.", this);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogError($"[RoadNetwork] {issues[i]}", this);
            }
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
