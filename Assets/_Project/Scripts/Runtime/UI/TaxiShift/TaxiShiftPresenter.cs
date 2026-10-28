using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;
using OutsiderDriver.Localization;
using OutsiderDriver.Navigation;
using OutsiderDriver.Rides;
using OutsiderDriver.World;

namespace OutsiderDriver.UI.TaxiShift
{
    /// <summary>
    /// Production ride orchestration and MVP presenter for the taxi shift (Issues #20/#21).
    /// It consumes the stable demand board, reserves one exact displayed offer before handing it
    /// to RideLifecycleService, releases it when activation fails, and never auto-accepts,
    /// auto-boards or auto-settles a fare. All labels are event-driven and localization-aware.
    /// </summary>
    public sealed class TaxiShiftPresenter : IStartable, IDisposable
    {
        private readonly IRideDemandService _demandService;
        private readonly IRideLifecycleService _rideService;
        private readonly IPlayerStateService _playerStateService;
        private readonly ILocalizationService _localizationService;
        private readonly ITaxiShiftView _view;
        private readonly CityWorldRegistry _worldRegistry;
        private readonly ITimeService _timeService;

        private bool _started;
        private bool _rendering;
        private bool _renderQueued;
        private bool _activationInProgress;
        private ShiftFeedback _feedback = ShiftFeedback.None;
        private int _feedbackCredits;

        [Inject]
        public TaxiShiftPresenter(
            IRideDemandService demandService,
            IRideLifecycleService rideService,
            IPlayerStateService playerStateService,
            ILocalizationService localizationService,
            ITaxiShiftView view,
            CityWorldRegistry worldRegistry = null,
            ITimeService timeService = null)
        {
            _demandService = demandService ?? throw new ArgumentNullException(nameof(demandService));
            _rideService = rideService ?? throw new ArgumentNullException(nameof(rideService));
            _playerStateService = playerStateService;
            _localizationService = localizationService;
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _worldRegistry = worldRegistry;
            _timeService = timeService;
        }

        public void Start()
        {
            _view.EnsureBuilt();
            _view.OnAcceptClicked += HandleAcceptClicked;
            _view.OnDeclineClicked += HandleDeclineClicked;
            _view.OnBoardClicked += HandleBoardClicked;
            _view.OnCollectClicked += HandleCollectClicked;

            _demandService.OnOffersChanged += HandleOffersChanged;
            _rideService.OnPhaseChanged += HandlePhaseChanged;
            _rideService.OnRideCompleted += HandleRideCompleted;
            _rideService.OnRideCancelled += HandleRideCancelled;

            if (_playerStateService != null)
            {
                _playerStateService.OnCreditsChanged += HandleCreditsChanged;
                _view.SetCredits(FormatCredits(_playerStateService.Credits));
            }

            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged += HandleLocaleChanged;
            }

            if (_timeService != null)
            {
                _timeService.OnMinuteTicked += HandleMinuteTicked;
            }

            ApplyLocalizedChrome();
            _started = true;
            RenderAll();
        }

        public void Dispose()
        {
            _view.OnAcceptClicked -= HandleAcceptClicked;
            _view.OnDeclineClicked -= HandleDeclineClicked;
            _view.OnBoardClicked -= HandleBoardClicked;
            _view.OnCollectClicked -= HandleCollectClicked;

            _demandService.OnOffersChanged -= HandleOffersChanged;
            _rideService.OnPhaseChanged -= HandlePhaseChanged;
            _rideService.OnRideCompleted -= HandleRideCompleted;
            _rideService.OnRideCancelled -= HandleRideCancelled;

            if (_playerStateService != null)
            {
                _playerStateService.OnCreditsChanged -= HandleCreditsChanged;
            }

            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged -= HandleLocaleChanged;
            }

            if (_timeService != null)
            {
                _timeService.OnMinuteTicked -= HandleMinuteTicked;
            }
        }

        private void HandleAcceptClicked(RideOffer offer)
        {
            if (offer == null || _rideService.HasActiveRide || _activationInProgress)
            {
                return;
            }

            _activationInProgress = true;
            try
            {
                SetFeedback(ShiftFeedback.None);
                if (!_demandService.TryReserveOffer(offer))
                {
                    SetFeedback(ShiftFeedback.OfferUnavailable);
                    RequestRender();
                    return;
                }

                bool activated = _rideService.TryOfferHail(
                        offer.Passenger,
                        offer.PickupNode,
                        offer.DestinationNode,
                        offer.District)
                    && _rideService.AcceptHail();

                if (!activated)
                {
                    if (_rideService.HasActiveRide)
                    {
                        _rideService.CancelActiveRide();
                    }

                    _demandService.ReleaseReservedOffer(offer);
                    SetFeedback(ShiftFeedback.RideStartFailed);
                    RequestRender();
                    return;
                }

                if (!_demandService.ConsumeReservedOffer(offer))
                {
                    _demandService.ReleaseReservedOffer(offer);
                }

                RequestRender();
            }
            finally
            {
                _activationInProgress = false;
            }
        }

        private void HandleDeclineClicked(RideOffer offer)
        {
            if (offer == null || _rideService.HasActiveRide || _activationInProgress)
            {
                return;
            }

            SetFeedback(ShiftFeedback.None);
            if (!_demandService.DeclineOffer(offer))
            {
                SetFeedback(ShiftFeedback.OfferUnavailable);
                RequestRender();
            }
        }

        private void HandleBoardClicked()
        {
            if (_rideService.CurrentPhase != RidePhase.AwaitingBoarding)
            {
                return;
            }

            SetFeedback(ShiftFeedback.None);
            if (!_rideService.BoardPassenger())
            {
                SetFeedback(ShiftFeedback.RideStartFailed);
                RequestRender();
            }
        }

        private void HandleCollectClicked()
        {
            if (_rideService.CurrentPhase != RidePhase.ArrivedAtDestination)
            {
                return;
            }

            if (!_rideService.CompleteAndSettleRide())
            {
                SetFeedback(ShiftFeedback.RideStartFailed);
                RequestRender();
            }
        }

        private void HandleOffersChanged(IReadOnlyList<RideOffer> offers)
        {
            RequestRender();
        }

        private void HandlePhaseChanged(RidePhase phase)
        {
            if (phase != RidePhase.Idle)
            {
                SetFeedback(ShiftFeedback.None);
            }

            RequestRender();
        }

        private void HandleRideCompleted(RideSettlementResult settlement)
        {
            SetFeedback(ShiftFeedback.FareCollected, settlement.TotalCreditsEarned);
            _demandService.TryGenerateOffer(out _);
            RequestRender();
        }

        private void HandleRideCancelled(RideRequest request)
        {
            SetFeedback(ShiftFeedback.RideCancelled);
            _demandService.TryGenerateOffer(out _);
            RequestRender();
        }

        private void HandleCreditsChanged(int credits)
        {
            _view.SetCredits(FormatCredits(credits));
        }

        private void HandleLocaleChanged(string locale)
        {
            ApplyLocalizedChrome();
            RenderFeedback();
            RequestRender();
        }

        private void HandleMinuteTicked(WorldTime time)
        {
            if (!_started)
            {
                return;
            }

            if (_rideService.CurrentPhase == RidePhase.Idle)
            {
                _demandService.RefreshOffers();
            }
            else
            {
                _demandService.ExpireIfNeeded();
            }

            RequestRender();
        }

        private void ApplyLocalizedChrome()
        {
            _view.SetTitle(Localize("hud_taxi_shift_title"));
            _view.SetActiveTitle(Localize("hud_active_fare"));
            _view.SetButtonLabels(
                Localize("hud_accept_offer"),
                Localize("hud_decline_offer"),
                Localize("hud_board_passenger"),
                Localize("hud_collect_fare"));
            _view.SetNoWorkText(Localize("hud_no_work_title"), Localize("hud_no_work_hint"));
            _view.ApplyTextDirection(_localizationService != null && _localizationService.IsRtl);
        }

        private void RequestRender()
        {
            if (!_started)
            {
                return;
            }

            if (_rendering)
            {
                _renderQueued = true;
                return;
            }

            RenderAll();
        }

        private void RenderAll()
        {
            if (!_started)
            {
                return;
            }

            _rendering = true;
            try
            {
                RenderContent();
            }
            finally
            {
                _rendering = false;
            }

            if (_renderQueued)
            {
                _renderQueued = false;
                RenderAll();
            }
        }

        private void RenderContent()
        {
            var request = _rideService.ActiveRequest;
            bool hasActiveRide = request != null && _rideService.CurrentPhase != RidePhase.Idle;

            if (hasActiveRide)
            {
                _view.SetActiveRide(BuildActiveRideCard(request));
                _view.SetStatus(PhaseLabel(_rideService.CurrentPhase), IsAttentionPhase(_rideService.CurrentPhase));
                return;
            }

            _view.ClearActiveRide();
            var offers = _demandService.GetEligibleOffers();
            var cards = new List<TaxiOfferCard>(offers.Count);
            for (int i = 0; i < offers.Count; i++)
            {
                cards.Add(BuildOfferCard(offers[i]));
            }

            _view.SetOffers(cards);
            _view.SetNoWorkVisible(cards.Count == 0);
            _view.SetStatus(
                cards.Count > 0
                    ? Localize("hud_status_offers", cards.Count)
                    : Localize("hud_status_no_work"),
                attention: cards.Count == 0);
        }

        private TaxiOfferCard BuildOfferCard(RideOffer offer)
        {
            return new TaxiOfferCard(
                offer,
                ResolvePassengerLabel(offer.Passenger),
                FormatCredits(offer.QuotedFare),
                Localize("hud_pickup_row", ResolveOfferPickupLabel(offer)),
                Localize("hud_destination_row", ResolveLocationLabel(offer.DestinationLocationId) ?? offer.DestinationNode.NodeId),
                Localize("hud_distance_m", Mathf.RoundToInt(offer.PlannedDistanceMeters)));
        }

        private TaxiActiveRideCard BuildActiveRideCard(RideRequest request)
        {
            var phase = _rideService.CurrentPhase;
            return new TaxiActiveRideCard(
                ResolvePassengerLabel(request.Passenger),
                FormatCredits(request.QuotedBaseFare),
                Localize("hud_pickup_row", ResolveNodeLabel(request.PickupNode)),
                Localize("hud_destination_row", ResolveNodeLabel(request.DestinationNode)),
                PhaseLabel(phase),
                phase == RidePhase.AwaitingBoarding,
                phase == RidePhase.ArrivedAtDestination);
        }

        private string ResolveOfferPickupLabel(RideOffer offer)
        {
            var location = ResolveLocationLabel(offer.PickupLocationId);
            return !string.IsNullOrEmpty(location) ? location : ResolveNodeLabel(offer.PickupNode);
        }

        private string ResolveLocationLabel(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return null;
            }

            if (_worldRegistry != null && _worldRegistry.TryGetLocation(locationId, out var anchor) && anchor != null)
            {
                var data = anchor.LocationData;
                if (data != null && !string.IsNullOrWhiteSpace(data.DisplayName))
                {
                    return data.DisplayName;
                }

                return anchor.LocationId;
            }

            return locationId;
        }

        private string ResolveNodeLabel(RoadNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (_worldRegistry != null)
            {
                var locations = _worldRegistry.AllLocations;
                for (int i = 0; i < locations.Count; i++)
                {
                    var anchor = locations[i];
                    if (anchor == null || anchor.AccessRoadNode != node)
                    {
                        continue;
                    }

                    var data = anchor.LocationData;
                    if (data != null && !string.IsNullOrWhiteSpace(data.DisplayName))
                    {
                        return data.DisplayName;
                    }

                    if (!string.IsNullOrWhiteSpace(anchor.LocationId))
                    {
                        return anchor.LocationId;
                    }
                }

                if (!string.IsNullOrWhiteSpace(node.DistrictId)
                    && _worldRegistry.TryGetDistrict(node.DistrictId, out var district)
                    && district != null
                    && !string.IsNullOrWhiteSpace(district.DisplayName))
                {
                    return Localize("hud_street_stop", district.DisplayName);
                }
            }

            return node.NodeId;
        }

        private static string ResolvePassengerLabel(PassengerDefinitionSO passenger)
        {
            if (passenger == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(passenger.DisplayName)
                ? passenger.PassengerId
                : passenger.DisplayName;
        }

        private string PhaseLabel(RidePhase phase)
        {
            switch (phase)
            {
                case RidePhase.EnRouteToPickup:
                    return Localize("hud_phase_enroute");
                case RidePhase.AwaitingBoarding:
                    return Localize("hud_phase_boarding");
                case RidePhase.InTransit:
                    return Localize("hud_phase_in_transit");
                case RidePhase.ArrivedAtDestination:
                    return Localize("hud_phase_arrived");
                default:
                    return Localize("hud_status_idle");
            }
        }

        private static bool IsAttentionPhase(RidePhase phase)
        {
            return phase == RidePhase.AwaitingBoarding || phase == RidePhase.ArrivedAtDestination;
        }

        private string FormatCredits(int amount)
        {
            return Localize("hud_fare_credits", amount);
        }

        private string Localize(string key, params object[] args)
        {
            if (_localizationService == null)
            {
                return key;
            }

            return _localizationService.GetLocalizedString(LocalizationService.TableUI, key, args);
        }

        private void SetFeedback(ShiftFeedback feedback, int credits = 0)
        {
            _feedback = feedback;
            _feedbackCredits = credits;
            RenderFeedback();
        }

        private void RenderFeedback()
        {
            switch (_feedback)
            {
                case ShiftFeedback.FareCollected:
                    _view.SetFeedback(Localize("hud_fare_collected", FormatCredits(_feedbackCredits)));
                    break;
                case ShiftFeedback.RideCancelled:
                    _view.SetFeedback(Localize("hud_ride_cancelled"));
                    break;
                case ShiftFeedback.OfferUnavailable:
                    _view.SetFeedback(Localize("hud_offer_unavailable"));
                    break;
                case ShiftFeedback.RideStartFailed:
                    _view.SetFeedback(Localize("hud_ride_start_failed"));
                    break;
                default:
                    _view.SetFeedback(string.Empty);
                    break;
            }
        }

        private enum ShiftFeedback
        {
            None,
            FareCollected,
            RideCancelled,
            OfferUnavailable,
            RideStartFailed
        }
    }
}
