using System;
using UnityEngine;

namespace OutsiderDriver.Navigation
{
    /// <summary>
    /// Visual state indicator bound to an authored TrafficSignalApproach.
    /// Updates its indicator color or child lamp renderers to reflect the approach's SignalState.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficSignalLamp : MonoBehaviour
    {
        [Tooltip("The signal approach this lamp reflects.")]
        [SerializeField] private TrafficSignalApproach _approach;

        [Tooltip("Renderer displaying the signal color.")]
        [SerializeField] private SpriteRenderer _indicatorRenderer;

        [Header("Phase Colors")]
        [SerializeField] private Color _redColor = new Color(0.95f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _amberColor = new Color(0.98f, 0.75f, 0.1f, 1f);
        [SerializeField] private Color _greenColor = new Color(0.2f, 0.95f, 0.35f, 1f);

        private SignalState _lastState = (SignalState)(-1);

        public TrafficSignalApproach Approach => _approach;
        public SignalState CurrentState => _approach != null ? _approach.State : SignalState.Red;

        public void Configure(TrafficSignalApproach approach, SpriteRenderer indicatorRenderer = null)
        {
            _approach = approach;
            if (indicatorRenderer != null)
            {
                _indicatorRenderer = indicatorRenderer;
            }
            UpdateVisuals(force: true);
        }

        private void Awake()
        {
            if (_indicatorRenderer == null)
            {
                _indicatorRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            UpdateVisuals(force: true);
        }

        private void Update()
        {
            UpdateVisuals(force: false);
        }

        public void UpdateVisuals(bool force = false)
        {
            var currentState = CurrentState;
            if (!force && currentState == _lastState)
            {
                return;
            }

            _lastState = currentState;
            if (_indicatorRenderer == null)
            {
                return;
            }

            switch (currentState)
            {
                case SignalState.Green:
                    _indicatorRenderer.color = _greenColor;
                    break;
                case SignalState.Amber:
                    _indicatorRenderer.color = _amberColor;
                    break;
                case SignalState.Red:
                case SignalState.AllRed:
                default:
                    _indicatorRenderer.color = _redColor;
                    break;
            }
        }
    }
}
