using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Navigation
{
    public enum SignalState
    {
        Red = 0,
        Green = 1,
        Amber = 2,
        AllRed = 3
    }

    /// <summary>An explicitly authored incoming movement and its world-space stop line.</summary>
    [DisallowMultipleComponent]
    public sealed class TrafficSignalApproach : MonoBehaviour
    {
        private static readonly Dictionary<(RoadNode, RoadNode), List<TrafficSignalApproach>> Registry =
            new Dictionary<(RoadNode, RoadNode), List<TrafficSignalApproach>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRegistry()
        {
            Registry.Clear();
        }

        [SerializeField] private TrafficSignalController _controller;
        [SerializeField] private RoadNode _junction;
        [SerializeField] private RoadNode _incoming;
        [Tooltip("Authored position before the junction on this incoming lane, in world space.")]
        [SerializeField] private Transform _stopLine;
        [Tooltip("Unique zero-based phase group. One approach per group until explicit movement-conflict metadata exists.")]
        [SerializeField] private int _phaseGroup;

        private (RoadNode, RoadNode) _registeredKey;
        private bool _registered;

        public TrafficSignalController Controller => _controller;
        public RoadNode Junction => _junction;
        public RoadNode Incoming => _incoming;
        public Transform StopLine => _stopLine;
        public int PhaseGroup => _phaseGroup;
        // Presentation can read State; permission remains separate from a junction reservation.
        public SignalState State => _controller != null ? _controller.GetState(this) : SignalState.Red;
        public bool AllowsEntry => State == SignalState.Green;

        public void Configure(TrafficSignalController controller, RoadNode junction, RoadNode incoming,
            Transform stopLine, int phaseGroup)
        {
            var previousController = _controller;
            Unregister();
            _controller = controller;
            _junction = junction;
            _incoming = incoming;
            _stopLine = stopLine;
            _phaseGroup = phaseGroup;
            Register();
            if (previousController != null) previousController.ResetCycle();
            if (_controller != null) _controller.ResetCycle();
        }

        /// <summary>Returns authored controls even when disabled or invalid, so they fail closed.</summary>
        public static TrafficSignalApproach Find(RoadNode junction, RoadNode incoming)
        {
            if (junction == null || incoming == null) return null;
            if (!Registry.TryGetValue((junction, incoming), out var approaches)) return null;
            approaches.RemoveAll(a => a == null);
            for (int i = 0; i < approaches.Count; i++)
                if (approaches[i] != null) return approaches[i];
            return null;
        }

        public bool IsAuthoringValid
        {
            get
            {
                if (!isActiveAndEnabled || _controller == null || _junction == null || _incoming == null ||
                    _incoming == _junction || _stopLine == null || _phaseGroup < 0 ||
                    !_incoming.CanTraverseElevationTo(_junction)) return false;
                var point = _stopLine.position;
                if (!IsFinite(point.x) || !IsFinite(point.y) || !IsFinite(point.z)) return false;
                bool connected = false;
                for (int i = 0; i < _incoming.ConnectedNodes.Count; i++)
                    if (_incoming.ConnectedNodes[i] == _junction) connected = true;
                if (!connected || !Registry.TryGetValue((_junction, _incoming), out var matches)) return false;
                matches.RemoveAll(m => m == null);
                return matches.Count == 1 && matches[0] == this;
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void Awake()
        {
            // Awake also registers an initially disabled component on an active signal object.
            Register();
        }

        private void OnEnable()
        {
            Unregister();
            Register();
            if (_controller != null) _controller.ResetCycle();
        }

        private void OnDisable()
        {
            // Keep the authored control discoverable: disabling a light must not create an uncontrolled road.
            if (_controller != null) _controller.ResetCycle();
        }

        private void OnDestroy()
        {
            Unregister();
            if (_controller != null) _controller.ResetCycle();
        }

        private void OnValidate()
        {
            Unregister();
            Register();
            if (_controller != null) _controller.ResetCycle();
        }

        private void Register()
        {
            if (_junction == null || _incoming == null) return;
            _registeredKey = (_junction, _incoming);
            if (!Registry.TryGetValue(_registeredKey, out var entries))
            {
                entries = new List<TrafficSignalApproach>();
                Registry.Add(_registeredKey, entries);
            }
            if (!entries.Contains(this)) entries.Add(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered) return;
            if (Registry.TryGetValue(_registeredKey, out var entries))
            {
                entries.Remove(this);
                if (entries.Count == 0) Registry.Remove(_registeredKey);
            }
            _registered = false;
        }
    }
}
