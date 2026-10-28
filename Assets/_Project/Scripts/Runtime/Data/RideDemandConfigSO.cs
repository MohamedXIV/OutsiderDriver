using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Data
{
    /// <summary>
    /// Authored rules and passenger pool for the systemic ride-demand source.
    /// The city layout and residents are authored; only individual offers are generated at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "RideDemandConfig", menuName = "Outsider Driver/Data/Ride Demand Config")]
    public class RideDemandConfigSO : ScriptableObject
    {
        [Header("Authored Passenger Pool")]
        [Tooltip("Stable authored resident definitions eligible to generate ordinary taxi work.")]
        [SerializeField] private List<PassengerDefinitionSO> _passengers = new List<PassengerDefinitionSO>();

        [Header("Demand Rules")]
        [Tooltip("Minimum ground-metre route distance for a meaningful paid ride.")]
        [Min(0f)]
        [SerializeField] private float _minimumRideDistanceMeters = 25f;

        [Min(1)]
        [Tooltip("In-game minutes before an unaccepted offer expires.")]
        [SerializeField] private int _offerLifetimeMinutes = 12;

        [Min(1)]
        [Tooltip("Maximum number of simultaneously visible, stable offers.")]
        [SerializeField] private int _maxVisibleOffers = 3;

        [Min(0)]
        [Tooltip("In-game minutes before a declined or expired offer may be offered again.")]
        [SerializeField] private int _declineCooldownMinutes = 20;

        [Tooltip("0 derives a runtime seed. Tests may pass an explicit seed for reproducibility.")]
        [SerializeField] private int _randomSeed = 0;

        public IReadOnlyList<PassengerDefinitionSO> Passengers => _passengers;
        public float MinimumRideDistanceMeters => _minimumRideDistanceMeters;
        public int OfferLifetimeMinutes => _offerLifetimeMinutes;
        public int MaxVisibleOffers => _maxVisibleOffers;
        public int DeclineCooldownMinutes => _declineCooldownMinutes;
        public int RandomSeed => _randomSeed;

        public void Configure(
            IEnumerable<PassengerDefinitionSO> passengers,
            float minimumRideDistanceMeters = 25f,
            int offerLifetimeMinutes = 12,
            int declineCooldownMinutes = 20,
            int randomSeed = 0,
            int maxVisibleOffers = 3)
        {
            _passengers.Clear();
            if (passengers != null)
            {
                _passengers.AddRange(passengers);
            }

            _minimumRideDistanceMeters = Mathf.Max(0f, minimumRideDistanceMeters);
            _offerLifetimeMinutes = Mathf.Max(1, offerLifetimeMinutes);
            _declineCooldownMinutes = Mathf.Max(0, declineCooldownMinutes);
            _randomSeed = randomSeed;
            _maxVisibleOffers = Mathf.Max(1, maxVisibleOffers);
        }
    }
}
