using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using OutsiderDriver.Navigation;

namespace OutsiderDriver.Vehicles
{
    /// <summary>
    /// Demonstrates and validates semi-autonomous taxi travel across road nodes in the test sandbox.
    /// Can automatically route between random or sequential landmarks on the road network.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class TaxiTestDriver : MonoBehaviour
    {
        [Tooltip("If true, automatically picks a new destination when reaching the current one.")]
        [SerializeField] private bool _autoPatrol = true;

        [Tooltip("Delay in seconds at destination before heading to the next stop.")]
        [SerializeField] private float _stopDuration = 1.5f;

        [SerializeField] private RoadNetwork _roadNetwork;

        private VehicleController _vehicle;
        private float _stopTimer = 0f;
        private bool _isWaiting = false;

        private void Awake()
        {
            _vehicle = GetComponent<VehicleController>();
        }

        private void Start()
        {
            if (_roadNetwork == null)
            {
                _roadNetwork = FindAnyObjectByType<RoadNetwork>();
            }

            _vehicle.OnDestinationReached += HandleDestinationReached;

            // Start navigating to a random destination if auto-patrol is enabled
            if (_autoPatrol)
            {
                NavigateToRandomNode();
            }
        }

        private void OnDestroy()
        {
            if (_vehicle != null)
            {
                _vehicle.OnDestinationReached -= HandleDestinationReached;
            }
        }

        private void Update()
        {
            if (_isWaiting)
            {
                _stopTimer -= Time.deltaTime;
                if (_stopTimer <= 0f)
                {
                    _isWaiting = false;
                    NavigateToRandomNode();
                }
            }

            // Quick manual trigger with spacebar using modern Input System
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _isWaiting = false;
                NavigateToRandomNode();
            }
        }

        private void HandleDestinationReached(RoadNode destination)
        {
            if (_autoPatrol)
            {
                _isWaiting = true;
                _stopTimer = _stopDuration;
            }
        }

        public void NavigateToRandomNode()
        {
            if (_roadNetwork == null || _roadNetwork.AllNodes.Count == 0) return;

            var allNodes = _roadNetwork.AllNodes;
            var availableNodes = new List<RoadNode>();

            RoadNode currentNode = _vehicle.DestinationNode ?? _roadNetwork.FindNearestNode(transform.position);

            foreach (var node in allNodes)
            {
                if (node != null && node != currentNode)
                {
                    availableNodes.Add(node);
                }
            }

            if (availableNodes.Count > 0)
            {
                int randomIndex = Random.Range(0, availableNodes.Count);
                _vehicle.NavigateTo(availableNodes[randomIndex]);
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 320, 150), GUI.skin.box);
            GUILayout.Label("<b>Outsider Driver - Taxi Prototype</b>");
            GUILayout.Label($"Speed: {_vehicle.CurrentSpeed:F1} / {_vehicle.MaxSpeed:F1} m/s");
            GUILayout.Label($"Facing: {_vehicle.CurrentFacing}");
            GUILayout.Label($"Target: {(_vehicle.CurrentTargetNode != null ? _vehicle.CurrentTargetNode.NodeId : "None")}");
            GUILayout.Label($"Destination: {(_vehicle.DestinationNode != null ? _vehicle.DestinationNode.NodeId : "Idle")}");
            if (GUILayout.Button("Pick New Random Destination (Space)"))
            {
                _isWaiting = false;
                NavigateToRandomNode();
            }
            GUILayout.EndArea();
        }
    }
}

