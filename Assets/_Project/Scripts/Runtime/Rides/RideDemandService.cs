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
    /// locations and the real road graph. It exposes a stable visible offer board and an explicit
    /// reserve/release/consume hand-off so a displayed offer keeps its identity until the ride
    /// state machine actually starts. It never creates or drives a ride; RideLifecycleService
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
        private readonly List<RideOffer> _offers = new List<RideOffer>();
        private readonly IReadOnlyList<RideOffer> _offersView;

        private RideOffer _reservedOffer;
        private int _offerSequence;
        private int _lastGenerationAttemptMinutes = int.MinValue;
        private bool _notifying;

        public event Action<IReadOnlyList<RideOffer>> OnOffersChanged;

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
            _offersView = _offers.AsReadOnly();

            int seed = seedOverride ?? (_config.RandomSeed != 0 ? _config.RandomSeed : Environment.TickCount);
            _random = new System.Random(seed);
        }

        public RideOffer CurrentOffer =>
            _reservedOffer ?? (_offers.Count > 0 ? _offers[0] : null);

        private int NowMinutes => _timeService != null ? _timeService.CurrentTime.TotalMinutes : 0;
        private int CurrentHour => _timeService != null ? _timeService.CurrentTime.Hour : 0;

        public bool TryEnsureOffer(out RideOffer offer)
        {
            GetEligibleOffers();
            offer = CurrentOffer;
            return offer != null;
        }

        public IReadOnlyList<RideOffer> GetEligibleOffers()
        {
            int now = NowMinutes;
            bool changed = PruneExpired(now);
            changed |= EnsureBoard(now);
            if (changed)
            {
                NotifyOffersChanged();
            }

            return _offersView;
        }

        public void RefreshOffers()
        {
            int now = NowMinutes;
            bool changed = PruneExpired(now);
            changed |= EnsureBoard(now);
            if (changed)
            {
                NotifyOffersChanged();
            }
        }

        public bool TryGenerateOffer(out RideOffer offer)
        {
            int now = NowMinutes;
            bool pruned = PruneExpired(now);
            if (_reservedOffer != null)
            {
                if (pruned)
                {
                    NotifyOffersChanged();
                }

                offer = _reservedOffer;
                return true;
            }

            BuildBoard(now);
            NotifyOffersChanged();
            offer = _offers.Count > 0 ? _offers[0] : null;
            return offer != null;
        }

        public bool TryReserveOffer(RideOffer offer)
        {
            if (offer == null)
            {
                return false;
            }

            int now = NowMinutes;
            if (offer.IsExpired(now) || !IsOfferValid(offer))
            {
                ExpireIfNeeded();
                return false;
            }

            if (_reservedOffer != null)
            {
                return false;
            }

            int index = IndexOfBoardOffer(offer.OfferId);
            if (index < 0 || !IsOfferValid(_offers[index]))
            {
                return false;
            }

            _reservedOffer = _offers[index];
            _offers.RemoveAt(index);
            MarkBoardChanged();
            NotifyOffersChanged();
            return true;
        }

        public bool ReleaseReservedOffer(RideOffer offer)
        {
            if (offer == null || _reservedOffer == null || !IsSameOffer(_reservedOffer, offer))
            {
                return false;
            }

            int now = NowMinutes;
            if (_reservedOffer.IsExpired(now) || !IsOfferValid(_reservedOffer))
            {
                if (_reservedOffer.IsExpired(now))
                {
                    AddCooldown(_reservedOffer.Passenger.PassengerId, now);
                }
                _reservedOffer = null;
                MarkBoardChanged();
                NotifyOffersChanged();
                return false;
            }

            var released = _reservedOffer;
            _reservedOffer = null;
            _offers.Add(released);
            MarkBoardChanged();
            NotifyOffersChanged();
            return true;
        }

        public bool ConsumeReservedOffer(RideOffer offer)
        {
            if (offer == null || _reservedOffer == null || !IsSameOffer(_reservedOffer, offer))
            {
                return false;
            }

            if (_reservedOffer.IsExpired(NowMinutes) || !IsOfferValid(_reservedOffer))
            {
                PruneExpired(NowMinutes);
                NotifyOffersChanged();
                return false;
            }

            _reservedOffer = null;
            MarkBoardChanged();
            NotifyOffersChanged();
            return true;
        }

        public bool AcceptOffer(RideOffer offer)
        {
            if (!TryRemoveBoardOffer(offer, out _))
            {
                return false;
            }

            MarkBoardChanged();
            NotifyOffersChanged();
            return true;
        }

        public bool DeclineOffer(RideOffer offer)
        {
            if (!TryRemoveBoardOffer(offer, out int now))
            {
                return false;
            }

            AddCooldown(offer.Passenger.PassengerId, now);
            MarkBoardChanged();
            NotifyOffersChanged();
            return true;
        }

        public bool ExpireIfNeeded()
        {
            if (!PruneExpired(NowMinutes))
            {
                return false;
            }

            NotifyOffersChanged();
            return true;
        }

        private bool TryRemoveBoardOffer(RideOffer offer, out int now)
        {
            now = NowMinutes;
            if (offer == null)
            {
                return false;
            }

            if (offer.IsExpired(now) || !IsOfferValid(offer))
            {
                ExpireIfNeeded();
                return false;
            }

            int index = IndexOfBoardOffer(offer.OfferId);
            if (index < 0)
            {
                return false;
            }

            _offers.RemoveAt(index);
            return true;
        }

        private bool EnsureBoard(int now)
        {
            if (_reservedOffer != null || _offers.Count > 0)
            {
                return false;
            }

            if (_lastGenerationAttemptMinutes == now)
            {
                return false;
            }

            return BuildBoard(now);
        }

        private bool BuildBoard(int now)
        {
            _lastGenerationAttemptMinutes = now;
            var candidates = BuildCandidates(now);
            Shuffle(candidates);

            _offers.Clear();
            int maxOffers = Math.Max(1, _config.MaxVisibleOffers);
            for (int i = 0; i < candidates.Count && _offers.Count < maxOffers; i++)
            {
                var candidate = candidates[i];
                string offerId = "offer_" + candidate.Passenger.PassengerId + "_" + now + "_" + _offerSequence++;
                _offers.Add(new RideOffer(
                    offerId,
                    candidate.Passenger,
                    candidate.PickupNode,
                    candidate.DestinationNode,
                    candidate.PickupLocationId,
                    candidate.DestinationLocationId,
                    candidate.District,
                    candidate.DistanceMeters,
                    candidate.QuotedFare,
                    now,
                    now + _config.OfferLifetimeMinutes,
                    candidate.PickupStop,
                    candidate.DestinationStop));
            }

            return _offers.Count > 0;
        }

        private bool PruneExpired(int now)
        {
            bool changed = false;

            if (_reservedOffer != null && (_reservedOffer.IsExpired(now) || !IsOfferValid(_reservedOffer)))
            {
                if (_reservedOffer.IsExpired(now))
                {
                    AddCooldown(_reservedOffer.Passenger.PassengerId, now);
                }
                _reservedOffer = null;
                changed = true;
            }

            for (int i = _offers.Count - 1; i >= 0; i--)
            {
                if (_offers[i].IsExpired(now) || !IsOfferValid(_offers[i]))
                {
                    if (_offers[i].IsExpired(now))
                    {
                        AddCooldown(_offers[i].Passenger.PassengerId, now);
                    }
                    _offers.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                MarkBoardChanged();
            }

            return changed;
        }

        private List<OfferCandidate> BuildCandidates(int now)
        {
            EnsureLocations();
            var candidates = new List<OfferCandidate>();
            if (_roadNetwork == null || _config == null || _config.Passengers == null || _locations.Count == 0)
            {
                return candidates;
            }

            int hour = CurrentHour;

            for (int p = 0; p < _config.Passengers.Count; p++)
            {
                var passenger = _config.Passengers[p];
                if (passenger == null || string.IsNullOrWhiteSpace(passenger.PassengerId))
                {
                    continue;
                }

                if (IsCoolingDown(passenger.PassengerId, now))
                {
                    continue;
                }

                if (!passenger.TryGetScheduleForHour(hour, out var schedule))
                {
                    continue;
                }

                var pickupNode = _roadNetwork.GetNode(schedule.PreferredNodeId);
                var pickupStop = _worldRegistry.GetTaxiStopForAccessNode(pickupNode);
                if (pickupStop == null || !pickupStop.Validate(_roadNetwork, 4.5f, 2f, out _))
                {
                    continue;
                }

                var pickupLocation = FindLocationByStop(pickupStop);
                if (pickupLocation == null)
                {
                    continue;
                }
                var pickupDistrict = ResolveDistrict(pickupLocation != null ? pickupLocation.DistrictId : schedule.DistrictId);

                OfferCandidate chosen = null;
                int validDestinations = 0;

                for (int d = 0; d < _locations.Count; d++)
                {
                    var destination = _locations[d];
                    if (destination.Stop == null || ReferenceEquals(destination.Stop, pickupStop))
                    {
                        continue;
                    }

                    if (pickupLocation != null && string.Equals(pickupLocation.LocationId, destination.LocationId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var district = pickupDistrict ?? destination.District;
                    if (!RideFareCalculator.TryQuote(_roadNetwork, passenger, pickupStop, destination.Stop, district, out var quote))
                    {
                        continue;
                    }

                    if (quote.DistanceMeters < _config.MinimumRideDistanceMeters)
                    {
                        continue;
                    }

                    validDestinations++;
                    if (_random.Next(validDestinations) == 0)
                    {
                        chosen = new OfferCandidate(
                            passenger,
                            pickupNode,
                            destination.Node,
                            pickupLocation != null ? pickupLocation.LocationId : string.Empty,
                            destination.LocationId,
                            district,
                            quote.DistanceMeters,
                            quote.QuotedFare,
                            pickupStop,
                            destination.Stop);
                    }
                }

                if (chosen != null)
                {
                    candidates.Add(chosen);
                }
            }

            return candidates;
        }

        private void Shuffle(List<OfferCandidate> candidates)
        {
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
        }

        private void EnsureLocations()
        {
            _locations.Clear();
            if (_worldRegistry == null)
            {
                return;
            }

            var stops = _worldRegistry.AllTaxiStops;
            for (int i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                if (stop == null || !stop.Validate(_roadNetwork, 4.5f, 2f, out _)
                    || _worldRegistry.GetTaxiStop(stop.StopId) != stop || stop.Location == null)
                {
                    continue;
                }

                var anchor = stop.Location;
                string districtId = anchor.LocationData != null ? anchor.LocationData.DistrictId : string.Empty;
                _locations.Add(new LocationDestination(anchor.LocationId, anchor.AccessRoadNode,
                    ResolveDistrict(districtId), districtId, stop));
            }
        }

        private LocationDestination FindLocationByStop(TaxiStopAnchor stop)
        {
            for (int i = 0; i < _locations.Count; i++)
            {
                if (_locations[i].Stop == stop)
                {
                    return _locations[i];
                }
            }

            return null;
        }

        private bool IsOfferValid(RideOffer offer)
        {
            if (offer == null || offer.PickupStop == null || offer.DestinationStop == null
                || !string.Equals(offer.PickupStopId, offer.PickupStop.StopId, StringComparison.Ordinal)
                || !string.Equals(offer.DestinationStopId, offer.DestinationStop.StopId, StringComparison.Ordinal)
                || _worldRegistry.GetTaxiStop(offer.PickupStopId) != offer.PickupStop
                || _worldRegistry.GetTaxiStop(offer.DestinationStopId) != offer.DestinationStop
                || offer.PickupStop.Location.AccessRoadNode != offer.PickupNode
                || offer.DestinationStop.Location.AccessRoadNode != offer.DestinationNode)
            {
                return false;
            }

            // Authoring changes invalidate the displayed snapshot instead of silently changing
            // its route or asking the player to accept a different fare under the same identity.
            return RideFareCalculator.TryQuote(_roadNetwork, offer.Passenger, offer.PickupStop,
                offer.DestinationStop, offer.District, out var quote)
                && Math.Abs(quote.DistanceMeters - offer.PlannedDistanceMeters) < 0.01f
                && quote.QuotedFare == offer.QuotedFare;
        }

        private DistrictConfigSO ResolveDistrict(string districtId)
        {
            if (_worldRegistry == null || string.IsNullOrWhiteSpace(districtId))
            {
                return null;
            }

            return _worldRegistry.TryGetDistrict(districtId, out var district) ? district : null;
        }

        private int IndexOfBoardOffer(string offerId)
        {
            for (int i = 0; i < _offers.Count; i++)
            {
                if (string.Equals(_offers[i].OfferId, offerId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsSameOffer(RideOffer a, RideOffer b)
        {
            return a != null
                && b != null
                && string.Equals(a.OfferId, b.OfferId, StringComparison.Ordinal);
        }

        private bool IsCoolingDown(string passengerId, int now)
        {
            if (!_coolDownUntilMinutes.TryGetValue(passengerId, out int until))
            {
                return false;
            }

            if (until <= now)
            {
                _coolDownUntilMinutes.Remove(passengerId);
                return false;
            }

            return true;
        }

        private void AddCooldown(string passengerId, int now)
        {
            if (_config.DeclineCooldownMinutes <= 0 || string.IsNullOrEmpty(passengerId))
            {
                return;
            }

            _coolDownUntilMinutes[passengerId] = now + _config.DeclineCooldownMinutes;
        }

        private void MarkBoardChanged()
        {
            _lastGenerationAttemptMinutes = int.MinValue;
        }

        private void NotifyOffersChanged()
        {
            if (_notifying || OnOffersChanged == null)
            {
                return;
            }

            _notifying = true;
            try
            {
                OnOffersChanged(_offersView);
            }
            finally
            {
                _notifying = false;
            }
        }

        private sealed class LocationDestination
        {
            public readonly string LocationId;
            public readonly RoadNode Node;
            public readonly DistrictConfigSO District;
            public readonly string DistrictId;
            public readonly TaxiStopAnchor Stop;

            public LocationDestination(string locationId, RoadNode node, DistrictConfigSO district, string districtId, TaxiStopAnchor stop)
            {
                LocationId = locationId;
                Node = node;
                District = district;
                DistrictId = districtId;
                Stop = stop;
            }
        }

        private sealed class OfferCandidate
        {
            public readonly PassengerDefinitionSO Passenger;
            public readonly RoadNode PickupNode;
            public readonly RoadNode DestinationNode;
            public readonly string PickupLocationId;
            public readonly string DestinationLocationId;
            public readonly DistrictConfigSO District;
            public readonly float DistanceMeters;
            public readonly int QuotedFare;
            public readonly TaxiStopAnchor PickupStop;
            public readonly TaxiStopAnchor DestinationStop;

            public OfferCandidate(
                PassengerDefinitionSO passenger,
                RoadNode pickupNode,
                RoadNode destinationNode,
                string pickupLocationId,
                string destinationLocationId,
                DistrictConfigSO district,
                float distanceMeters,
                int quotedFare,
                TaxiStopAnchor pickupStop,
                TaxiStopAnchor destinationStop)
            {
                Passenger = passenger;
                PickupNode = pickupNode;
                DestinationNode = destinationNode;
                PickupLocationId = pickupLocationId;
                DestinationLocationId = destinationLocationId;
                District = district;
                DistanceMeters = distanceMeters;
                QuotedFare = quotedFare;
                PickupStop = pickupStop;
                DestinationStop = destinationStop;
            }
        }
    }
}
