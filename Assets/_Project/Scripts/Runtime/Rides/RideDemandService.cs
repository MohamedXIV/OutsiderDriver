using System;
using System.Collections.Generic;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.World;
using VContainer;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Produces ordinary ride offers from authored passengers, current world time, reachable city
    /// locations and the real road graph. It never creates or drives a ride; RideLifecycleService
    /// remains the single active-ride authority.
    /// </summary>
    public sealed class RideDemandService : IRideDemandService
    {
        private readonly RoadNetwork _roadNetwork;
        private readonly CityWorldRegistry _worldRegistry;
        private readonly ITimeService _timeService;
        private readonly RideDemandConfigSO _config;
        private readonly System.Random _random;
        private readonly List<LocationDestination> _locations = new List<LocationDestination>();
        private readonly Dictionary<string, int> _coolDownUntilMinutes = new Dictionary<string, int>(StringComparer.Ordinal);

        private RideOffer _currentOffer;
        private int _offerSequence;
        private bool _locationsCached;

        [Inject]
        public RideDemandService(
            RoadNetwork roadNetwork,
            CityWorldRegistry worldRegistry,
            ITimeService timeService,
            RideDemandConfigSO config)
            : this(roadNetwork, worldRegistry, timeService, config, null)
        {
        }

        public RideDemandService(
            RoadNetwork roadNetwork,
            CityWorldRegistry worldRegistry,
            ITimeService timeService,
            RideDemandConfigSO config,
            int? seedOverride)
        {
            _roadNetwork = roadNetwork ?? throw new ArgumentNullException(nameof(roadNetwork));
            _worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
            _timeService = timeService;
            _config = config ?? throw new ArgumentNullException(nameof(config));

            int seed = seedOverride ?? (_config.RandomSeed != 0 ? _config.RandomSeed : Environment.TickCount);
            _random = new System.Random(seed);
        }

        public RideOffer CurrentOffer => _currentOffer;

        private int NowMinutes => _timeService != null ? _timeService.CurrentTime.TotalMinutes : 0;
        private int CurrentHour => _timeService != null ? _timeService.CurrentTime.Hour : 0;

        public bool TryEnsureOffer(out RideOffer offer)
        {
            ExpireIfNeeded();

            if (_currentOffer != null)
            {
                offer = _currentOffer;
                return true;
            }

            return TryGenerateOffer(out offer);
        }

        public bool TryGenerateOffer(out RideOffer offer)
        {
            var candidates = GetEligibleOffers();
            if (candidates.Count == 0)
            {
                offer = null;
                return false;
            }

            var chosen = candidates[_random.Next(candidates.Count)];
            _currentOffer = chosen;
            offer = chosen;
            return true;
        }

        public IReadOnlyList<RideOffer> GetEligibleOffers()
        {
            EnsureLocations();
            var offers = new List<RideOffer>();
            if (_roadNetwork == null || _config == null || _config.Passengers == null || _locations.Count == 0)
            {
                return offers;
            }

            int now = NowMinutes;
            int hour = CurrentHour;
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int p = 0; p < _config.Passengers.Count; p++)
            {
                var passenger = _config.Passengers[p];
                if (passenger == null || string.IsNullOrWhiteSpace(passenger.PassengerId))
                {
                    continue;
                }

                if (!passenger.TryGetScheduleForHour(hour, out var schedule))
                {
                    continue;
                }

                var pickupNode = _roadNetwork.GetNode(schedule.PreferredNodeId);
                if (pickupNode == null || pickupNode.ConnectedNodes.Count == 0)
                {
                    continue;
                }

                var pickupLocation = FindLocationByNode(pickupNode);
                var pickupDistrict = ResolveDistrict(pickupLocation != null ? pickupLocation.DistrictId : schedule.DistrictId);

                for (int d = 0; d < _locations.Count; d++)
                {
                    var destination = _locations[d];
                    if (destination.Node == null || ReferenceEquals(destination.Node, pickupNode))
                    {
                        continue;
                    }

                    if (pickupLocation != null && string.Equals(pickupLocation.LocationId, destination.LocationId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var district = pickupDistrict ?? destination.District;
                    if (!RideFareCalculator.TryQuote(_roadNetwork, passenger, pickupNode, destination.Node, district, out var quote))
                    {
                        continue;
                    }

                    if (quote.DistanceMeters < _config.MinimumRideDistanceMeters)
                    {
                        continue;
                    }

                    string key = BuildKey(passenger, pickupNode, destination.Node);
                    if (!seenKeys.Add(key) || IsCoolingDown(key, now))
                    {
                        continue;
                    }

                    string offerId = "offer_" + key + "_" + now + "_" + _offerSequence++;
                    int expiresAt = now + _config.OfferLifetimeMinutes;
                    offers.Add(new RideOffer(
                        offerId,
                        passenger,
                        pickupNode,
                        destination.Node,
                        pickupLocation != null ? pickupLocation.LocationId : string.Empty,
                        destination.LocationId,
                        district,
                        quote.DistanceMeters,
                        quote.QuotedFare,
                        now,
                        expiresAt));
                }
            }

            offers.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
            return offers;
        }

        public bool AcceptOffer(RideOffer offer)
        {
            if (!IsEligibleOffer(offer))
            {
                return false;
            }

            _currentOffer = null;
            return true;
        }

        public bool DeclineOffer(RideOffer offer)
        {
            if (!IsEligibleOffer(offer))
            {
                return false;
            }

            AddCooldown(offer.Key, NowMinutes + _config.DeclineCooldownMinutes);
            _currentOffer = null;
            return true;
        }

        public bool ExpireIfNeeded()
        {
            if (_currentOffer == null || !_currentOffer.IsExpired(NowMinutes))
            {
                return false;
            }

            AddCooldown(_currentOffer.Key, NowMinutes + _config.DeclineCooldownMinutes);
            _currentOffer = null;
            return true;
        }

        private bool IsEligibleOffer(RideOffer offer)
        {
            if (offer == null || offer.IsExpired(NowMinutes))
            {
                return false;
            }

            var eligible = GetEligibleOffers();
            for (int i = 0; i < eligible.Count; i++)
            {
                if (string.Equals(eligible[i].Key, offer.Key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureLocations()
        {
            if (_locationsCached)
            {
                return;
            }

            _locationsCached = true;
            if (_worldRegistry == null)
            {
                return;
            }

            if (_worldRegistry.AllLocations.Count == 0)
            {
                _worldRegistry.RebuildRegistry();
            }

            var seenLocations = new HashSet<string>(StringComparer.Ordinal);
            var seenNodes = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _worldRegistry.AllLocations.Count; i++)
            {
                var anchor = _worldRegistry.AllLocations[i];
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.LocationId))
                {
                    continue;
                }

                if (!seenLocations.Add(anchor.LocationId))
                {
                    continue;
                }

                var node = anchor.AccessRoadNode ?? _worldRegistry.GetAccessNodeForLocation(anchor.LocationId);
                if (node == null || node.ConnectedNodes.Count == 0)
                {
                    continue;
                }

                if (!seenNodes.Add(node.NodeId))
                {
                    continue;
                }

                string districtId = anchor.LocationData != null ? anchor.LocationData.DistrictId : string.Empty;
                _locations.Add(new LocationDestination(
                    anchor.LocationId,
                    node,
                    ResolveDistrict(districtId),
                    districtId));
            }
        }

        private LocationDestination FindLocationByNode(RoadNode node)
        {
            for (int i = 0; i < _locations.Count; i++)
            {
                if (string.Equals(_locations[i].Node.NodeId, node.NodeId, StringComparison.Ordinal))
                {
                    return _locations[i];
                }
            }

            return null;
        }

        private DistrictConfigSO ResolveDistrict(string districtId)
        {
            if (_worldRegistry == null || string.IsNullOrWhiteSpace(districtId))
            {
                return null;
            }

            return _worldRegistry.TryGetDistrict(districtId, out var district) ? district : null;
        }

        private static string BuildKey(PassengerDefinitionSO passenger, RoadNode pickupNode, RoadNode destinationNode)
        {
            return passenger.PassengerId + "|" + pickupNode.NodeId + "|" + destinationNode.NodeId;
        }

        private bool IsCoolingDown(string key, int now)
        {
            if (!_coolDownUntilMinutes.TryGetValue(key, out int until))
            {
                return false;
            }

            if (until <= now)
            {
                _coolDownUntilMinutes.Remove(key);
                return false;
            }

            return true;
        }

        private void AddCooldown(string key, int untilMinutes)
        {
            if (_config.DeclineCooldownMinutes <= 0 || string.IsNullOrEmpty(key))
            {
                return;
            }

            _coolDownUntilMinutes[key] = untilMinutes;
        }

        private sealed class LocationDestination
        {
            public readonly string LocationId;
            public readonly RoadNode Node;
            public readonly DistrictConfigSO District;
            public readonly string DistrictId;

            public LocationDestination(string locationId, RoadNode node, DistrictConfigSO district, string districtId)
            {
                LocationId = locationId;
                Node = node;
                District = district;
                DistrictId = districtId;
            }
        }
    }
}
