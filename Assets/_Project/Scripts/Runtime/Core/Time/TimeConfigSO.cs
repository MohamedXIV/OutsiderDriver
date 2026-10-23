using UnityEngine;

namespace OutsiderDriver.Core.Time
{
    /// <summary>
    /// Data-driven configuration for the continuous simulation clock.
    /// </summary>
    [CreateAssetMenu(fileName = "TimeConfig", menuName = "Outsider Driver/Core/Time Config")]
    public class TimeConfigSO : ScriptableObject
    {
        [Tooltip("How many real-time seconds equal one in-game minute.")]
        [Min(0.05f)]
        [SerializeField] private float _realSecondsPerInGameMinute = 1.0f;

        [Header("Starting World Time")]
        [Min(1)]
        [SerializeField] private int _startDay = 1;

        [Range(0, 23)]
        [SerializeField] private int _startHour = 18;

        [Range(0, 59)]
        [SerializeField] private int _startMinute = 0;

        public float RealSecondsPerInGameMinute => _realSecondsPerInGameMinute;
        public WorldTime InitialWorldTime => new WorldTime(_startDay, _startHour, _startMinute);
    }
}
