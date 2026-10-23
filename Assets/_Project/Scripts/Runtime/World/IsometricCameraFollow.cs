using UnityEngine;

namespace OutsiderDriver.World
{
    /// <summary>
    /// Smooth isometric 2D camera follow with customizable damping and lead-ahead offset.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraFollow : MonoBehaviour
    {
        [Header("Target & Framing")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        [Header("Damping")]
        [SerializeField] private float _smoothTime = 0.25f;

        private Vector3 _currentVelocity = Vector3.zero;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 destination = _target.position + _offset;
            destination.z = _offset.z; // Maintain 2D camera Z plane

            transform.position = Vector3.SmoothDamp(transform.position, destination, ref _currentVelocity, _smoothTime);
        }

        public void SnapToTarget()
        {
            if (_target != null)
            {
                transform.position = new Vector3(_target.position.x + _offset.x, _target.position.y + _offset.y, _offset.z);
                _currentVelocity = Vector3.zero;
            }
        }
    }
}
