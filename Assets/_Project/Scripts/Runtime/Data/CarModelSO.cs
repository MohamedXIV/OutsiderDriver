using UnityEngine;

namespace OutsiderDriver.Data
{
    /// <summary>
    /// Data-driven ScriptableObject defining a ground-contact alien vehicle model's kinematic
    /// movement parameters, sensor spacing, and visual tint (Decisions P-010, P-024).
    /// </summary>
    [CreateAssetMenu(fileName = "CarModel", menuName = "Outsider Driver/Data/Car Model")]
    public class CarModelSO : ScriptableObject
    {
        [Header("Model Identity")]
        [SerializeField] private string _modelId = "car_taxi_mk1";
        [SerializeField] private string _displayName = "Vesperia Civic Cab";

        [Header("Kinematic Movement Specs (Decision P-024)")]
        [Min(0.5f)]
        [SerializeField] private float _maxSpeed = 5.0f;
        [Min(0.5f)]
        [SerializeField] private float _acceleration = 4.0f;
        [Min(0.5f)]
        [SerializeField] private float _deceleration = 6.0f;
        [Min(0.5f)]
        [SerializeField] private float _forwardSensorDistance = 1.8f;

        [Header("Capacity & Visuals")]
        [Range(1, 6)]
        [SerializeField] private int _passengerCapacity = 2;
        [SerializeField] private Sprite _chassisSprite;
        [SerializeField] private Color _bodyTint = new Color(1f, 0.78f, 0.28f, 1f);

        public string ModelId => _modelId;
        public string DisplayName => _displayName;
        public float MaxSpeed => _maxSpeed;
        public float Acceleration => _acceleration;
        public float Deceleration => _deceleration;
        public float ForwardSensorDistance => _forwardSensorDistance;
        public int PassengerCapacity => _passengerCapacity;
        public Sprite ChassisSprite => _chassisSprite;
        public Color BodyTint => _bodyTint;

        public void Initialize(
            string modelId,
            string displayName,
            float maxSpeed,
            float acceleration,
            float deceleration,
            float forwardSensorDistance = 1.8f,
            int passengerCapacity = 2)
        {
            _modelId = modelId;
            _displayName = displayName;
            _maxSpeed = Mathf.Max(0.5f, maxSpeed);
            _acceleration = Mathf.Max(0.5f, acceleration);
            _deceleration = Mathf.Max(0.5f, deceleration);
            _forwardSensorDistance = Mathf.Max(0.5f, forwardSensorDistance);
            _passengerCapacity = Mathf.Clamp(passengerCapacity, 1, 6);
        }
    }
}
