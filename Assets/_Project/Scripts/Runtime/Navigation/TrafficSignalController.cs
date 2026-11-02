using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Navigation
{
    /// <summary>Local, exclusive authored signal groups; clearance waits for the existing junction reservation.</summary>
    [DisallowMultipleComponent]
    public sealed class TrafficSignalController : MonoBehaviour
    {
        private static readonly Dictionary<RoadNode, List<TrafficSignalController>> Registry =
            new Dictionary<RoadNode, List<TrafficSignalController>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRegistry()
        {
            Registry.Clear();
        }

        [SerializeField] private RoadNode _junction;
        [SerializeField] private TrafficSignalApproach[] _approaches = new TrafficSignalApproach[0];
        [Min(0.01f), SerializeField] private float _greenSeconds = 10f;
        [Min(0.01f), SerializeField] private float _amberSeconds = 2f;
        [Min(0.01f), SerializeField] private float _allRedSeconds = 1f;

        private RoadNode _registeredJunction;
        private SignalState _state = SignalState.AllRed;
        private int _currentGroup = -1;
        private int _groupCount;
        private double _remainingSeconds;
        private bool _configurationValid;

        public RoadNode Junction => _junction;
        public SignalState State => IsOperational ? _state : SignalState.Red;
        public int ActivePhaseGroup => IsOperational && _state != SignalState.AllRed ? _currentGroup : -1;
        public bool IsOperational => isActiveAndEnabled && _configurationValid && IsUniqueController;

        public void Initialize(RoadNode junction, TrafficSignalApproach[] approaches,
            float greenSeconds = 10f, float amberSeconds = 2f, float allRedSeconds = 1f)
        {
            Unregister();
            _junction = junction;
            _approaches = approaches != null ? (TrafficSignalApproach[])approaches.Clone() : null;
            _greenSeconds = greenSeconds;
            _amberSeconds = amberSeconds;
            _allRedSeconds = allRedSeconds;
            Register();
            ResetCycle();
        }

        /// <summary>Includes disabled authored controllers; missing approaches at their junction must stop.</summary>
        public static TrafficSignalController Find(RoadNode junction)
        {
            if (junction == null || !Registry.TryGetValue(junction, out var entries)) return null;
            entries.RemoveAll(e => e == null);
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null) return entries[i];
            return null;
        }

        public SignalState GetState(TrafficSignalApproach approach)
        {
            if (!IsOperational || approach == null || !approach.IsAuthoringValid ||
                approach.Controller != this || approach.Junction != _junction) return SignalState.Red;
            bool included = false;
            for (int i = 0; i < _approaches.Length; i++)
                if (_approaches[i] == approach) included = true;
            if (!included) return SignalState.Red;
            if (_state == SignalState.AllRed) return SignalState.AllRed;
            return approach.PhaseGroup == _currentGroup ? _state : SignalState.Red;
        }

        private bool IsJunctionOccupied => _junction != null && _junction.OccupyingVehicle != null &&
            !(_junction.OccupyingVehicle is UnityEngine.Object uo && uo == null);

        /// <summary>Uses elapsed game seconds. Invalid deltas do nothing; large deltas skip whole empty cycles.</summary>
        public void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled) return;
            if (!ValidateConfiguration())
            {
                ResetCycle();
                return;
            }
            if (!_configurationValid)
            {
                ResetCycle();
            }
            if (!IsPositiveFinite(deltaTime) && deltaTime != 0f) return;

            double elapsed = deltaTime;
            // No entry/exit history occurs within one synchronous tick. Whole unoccupied cycles are equivalent.
            double cycleSeconds = _groupCount * ((double)_greenSeconds + _amberSeconds + _allRedSeconds);
            if (!IsJunctionOccupied && elapsed >= cycleSeconds)
                elapsed %= cycleSeconds;

            while (elapsed >= _remainingSeconds)
            {
                elapsed -= _remainingSeconds;
                if (_state == SignalState.AllRed)
                {
                    if (IsJunctionOccupied)
                    {
                        _remainingSeconds = 0d;
                        return;
                    }
                    _currentGroup = (_currentGroup + 1) % _groupCount;
                    _state = SignalState.Green;
                    _remainingSeconds = _greenSeconds;
                }
                else if (_state == SignalState.Green)
                {
                    _state = SignalState.Amber;
                    _remainingSeconds = _amberSeconds;
                }
                else
                {
                    _state = SignalState.AllRed;
                    _remainingSeconds = _allRedSeconds;
                }
            }
            _remainingSeconds -= elapsed;
        }

        public void ResetCycle()
        {
            _state = SignalState.AllRed;
            _currentGroup = -1;
            _remainingSeconds = IsPositiveFinite(_allRedSeconds) ? _allRedSeconds : 1d;
            _configurationValid = ValidateConfiguration();
        }

        private bool IsUniqueController
        {
            get
            {
                if (_junction == null || !Registry.TryGetValue(_junction, out var entries)) return false;
                entries.RemoveAll(e => e == null);
                return entries.Count == 1 && entries[0] == this;
            }
        }

        private bool ValidateConfiguration()
        {
            if (!IsUniqueController || !IsPositiveFinite(_greenSeconds) || !IsPositiveFinite(_amberSeconds) ||
                !IsPositiveFinite(_allRedSeconds) || _approaches == null || _approaches.Length == 0) return false;
            int maximumGroup = -1;
            for (int i = 0; i < _approaches.Length; i++)
            {
                var approach = _approaches[i];
                if (approach == null || !approach.IsAuthoringValid || approach.Controller != this ||
                    approach.Junction != _junction || approach.PhaseGroup >= _approaches.Length) return false;
                for (int j = 0; j < i; j++)
                    if (_approaches[j] == approach || _approaches[j].PhaseGroup == approach.PhaseGroup) return false;
                maximumGroup = Mathf.Max(maximumGroup, approach.PhaseGroup);
            }
            for (int group = 0; group <= maximumGroup; group++)
            {
                bool found = false;
                for (int i = 0; i < _approaches.Length; i++)
                    if (_approaches[i].PhaseGroup == group) found = true;
                if (!found) return false;
            }
            _groupCount = maximumGroup + 1;
            return true;
        }

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private void Update() => Tick(Time.deltaTime);
        private void Awake()
        {
            // Register initially disabled components too; their junction remains controlled and blocked.
            Register();
            ResetCycle();
        }
        private void OnEnable()
        {
            Unregister();
            Register();
            ResetCycle();
        }
        private void OnDisable() => ResetCycle();
        private void OnDestroy() => Unregister();
        private void OnValidate()
        {
            Unregister();
            Register();
            ResetCycle();
        }

        private void Register()
        {
            if (_junction == null) return;
            _registeredJunction = _junction;
            if (!Registry.TryGetValue(_junction, out var entries))
            {
                entries = new List<TrafficSignalController>();
                Registry.Add(_junction, entries);
            }
            if (!entries.Contains(this)) entries.Add(this);
        }

        private void Unregister()
        {
            if (ReferenceEquals(_registeredJunction, null)) return;
            if (Registry.TryGetValue(_registeredJunction, out var entries))
            {
                entries.Remove(this);
                if (entries.Count == 0) Registry.Remove(_registeredJunction);
            }
            _registeredJunction = null;
        }
    }
}
