using System;
using UnityEngine;
using VContainer;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Concrete domain service managing the passenger ride lifecycle:
    /// HailOffered -> EnRouteToPickup -> AwaitingBoarding -> InTransit -> ArrivedAtDestination -> Idle.
    /// Decoupled from UI; Presenters subscribe to its events (Systems Architecture §1 & §6).
    /// </summary>
    public sealed class RideLifecycleService : IRideLifecycleService, IDisposable
    {
        private readonly RoadNetwork _roadNetwork;
        private readonly VehicleController _playerTaxi;
        private readonly IPlayerStateService _playerStateService;
        private readonly ITimeService _timeService;
        private readonly CityWorldRegistry _worldRegistry;
        private TaxiStopAnchor _activeStopTarget;

        private RidePhase _currentPhase = RidePhase.Idle;
        private RideRequest _activeRequest;

        public RidePhase CurrentPhase => _currentPhase;
        public RideRequest ActiveRequest => _activeRequest;

        public event Action<RidePhase> OnPhaseChanged;
        public event Action<RideRequest> OnHailOffered;
        public event Action<RideRequest> OnHailDeclined;
        public event Action<RideRequest> OnHailAccepted;
        public event Action<RideRequest> OnPassengerBoarded;
        public event Action<RideRequest, RoadNode> OnDestinationUpdated;
        public event Action<RideRequest> OnArrivedAtDestination;
        public event Action<RideSettlementResult> OnRideCompleted;
        public event Action<RideRequest> OnRideCancelled;

        [Inject]
        public RideLifecycleService(
            RoadNetwork roadNetwork,
            VehicleController playerTaxi,
            IPlayerStateService playerStateService,
            ITimeService timeService = null,
            CityWorldRegistry worldRegistry = null)
        {
            _roadNetwork = roadNetwork;
            _playerTaxi = playerTaxi;
            _playerStateService = playerStateService;
            _timeService = timeService;
            _worldRegistry = worldRegistry;

            if (_playerTaxi != null)
            {
                _playerTaxi.OnStopReached += HandleStopReached;
                _playerTaxi.OnStopNavigationFailed += HandleStopNavigationFailed;
            }
        }

        public void Dispose()
        {
            if (_playerTaxi != null)
            {
                _playerTaxi.OnStopReached -= HandleStopReached;
                _playerTaxi.OnStopNavigationFailed -= HandleStopNavigationFailed;
            }
        }

        public bool TryOfferHail(
            PassengerDefinitionSO passenger,
            RoadNode pickupNode,
            RoadNode destinationNode,
            DistrictConfigSO district = null)
        {
            return TryOfferHail(passenger,
                _worldRegistry != null ? _worldRegistry.GetTaxiStopForAccessNode(pickupNode) : null,
                _worldRegistry != null ? _worldRegistry.GetTaxiStopForAccessNode(destinationNode) : null,
                district);
        }

        public bool TryOfferHail(
            PassengerDefinitionSO passenger,
            TaxiStopAnchor pickupStop,
            TaxiStopAnchor destinationStop,
            DistrictConfigSO district = null)
        {
            if (_currentPhase != RidePhase.Idle || !IsValidStop(pickupStop) || !IsValidStop(destinationStop)
                || !RideFareCalculator.TryQuote(_roadNetwork, passenger, pickupStop, destinationStop, district,
                    out var quote, TaxiLength, TaxiWidth))
            {
                return false;
            }

            _activeRequest = new RideRequest(
                passenger, pickupStop.Location.AccessRoadNode, destinationStop.Location.AccessRoadNode,
                quote.DistanceMeters, quote.QuotedFare, district, pickupStop, destinationStop);
            SetPhase(RidePhase.HailOffered);
            OnHailOffered?.Invoke(_activeRequest);
            return true;
        }

        public bool DeclineHail()
        {
            if (_currentPhase != RidePhase.HailOffered || _activeRequest == null)
            {
                return false;
            }

            var declined = _activeRequest;
            _activeRequest = null;
            _activeStopTarget = null;
            SetPhase(RidePhase.Idle);
            OnHailDeclined?.Invoke(declined);
            return true;
        }

        public bool AcceptHail()
        {
            if (_currentPhase != RidePhase.HailOffered || _activeRequest == null || _playerTaxi == null)
            {
                return false;
            }

            if (!IsValidRequest()
                || !RideFareCalculator.TryQuote(_roadNetwork, _activeRequest.Passenger,
                    _activeRequest.PickupStop, _activeRequest.DestinationStop, _activeRequest.District,
                    out var quote, TaxiLength, TaxiWidth)
                || Mathf.Abs(quote.DistanceMeters - _activeRequest.PlannedDistance) >= 0.01f
                || quote.QuotedFare != _activeRequest.QuotedBaseFare)
            {
                return false;
            }

            _activeStopTarget = _activeRequest.PickupStop;
            if (_playerTaxi.IsAtStop(_activeStopTarget))
            {
                SetPhase(RidePhase.AwaitingBoarding);
            }
            else
            {
                SetPhase(RidePhase.EnRouteToPickup);
                if (!_playerTaxi.NavigateToStop(_activeStopTarget))
                {
                    CancelActiveRide();
                    return false;
                }
            }

            if (_activeRequest == null)
            {
                return false;
            }
            HandleStopReached(_activeStopTarget);
            if (_activeRequest == null) return false;
            OnHailAccepted?.Invoke(_activeRequest);
            return true;
        }

        public bool BoardPassenger()
        {
            if (_currentPhase != RidePhase.AwaitingBoarding || _activeRequest == null)
            {
                return false;
            }

            if (_playerTaxi == null || !IsValidRequest() || !_playerTaxi.IsAtStop(_activeRequest.PickupStop))
            {
                CancelActiveRide();
                return false;
            }

            _activeStopTarget = _activeRequest.DestinationStop;
            SetPhase(RidePhase.InTransit);
            if (!_playerTaxi.NavigateToStop(_activeStopTarget))
            {
                CancelActiveRide();
                return false;
            }

            if (_activeRequest == null)
            {
                return false;
            }
            OnPassengerBoarded?.Invoke(_activeRequest);
            return true;
        }

        public bool TryChangeDestination(RoadNode newDestinationNode)
        {
            return TryChangeDestination(_worldRegistry != null
                ? _worldRegistry.GetTaxiStopForAccessNode(newDestinationNode)
                : null);
        }

        public bool TryChangeDestination(TaxiStopAnchor newDestinationStop)
        {
            if (_currentPhase != RidePhase.InTransit || _activeRequest == null || _playerTaxi == null
                || !IsValidStop(newDestinationStop))
            {
                return false;
            }

            if (newDestinationStop == _activeRequest.DestinationStop)
            {
                return true;
            }

            var currentStart = _playerTaxi.StopRouteContinuationNode;
            var forcedNext = _playerTaxi.StopRouteContinuationNextNode;
            if (currentStart == null || !newDestinationStop.Prepare(_roadNetwork, TaxiLength, TaxiWidth))
            {
                return false;
            }

            var newPath = _roadNetwork.FindPath(forcedNext ?? currentStart, newDestinationStop.ApproachFromNode);
            bool remainsInBay = _playerTaxi.ParkedStop == newDestinationStop;
            if ((newPath == null || newPath.Count == 0) && !remainsInBay)
            {
                return false;
            }

            float departureEdgeDistance = forcedNext != null
                ? _roadNetwork.GroundProjection.Distance(currentStart.Position, forcedNext.Position)
                : 0f;
            float additionalDistance = _playerTaxi.RemainingCurbTravelMeters + departureEdgeDistance
                + RideFareCalculator.CalculateGroundDistance(_roadNetwork.GroundProjection, newPath)
                + _roadNetwork.GroundProjection.Distance(newDestinationStop.ApproachFromNode.Position, newDestinationStop.ApproachNode.Position)
                + newDestinationStop.PullInLength;
            if (float.IsNaN(additionalDistance) || float.IsInfinity(additionalDistance))
            {
                return false;
            }
            if (_playerTaxi.ParkedStop == newDestinationStop)
                additionalDistance = 0f; // revoke an unstarted departure, not a fictitious road loop
            float updatedDistance = _activeRequest.PlannedDistance + additionalDistance;
            int updatedFare = RideFareCalculator.CalculateFare(updatedDistance,
                _activeRequest.Passenger.FareMultiplier, _activeRequest.District);

            // A rejected route leaves both the active request and its quotation intact.
            var previousTarget = _activeStopTarget;
            var previousRequest = _activeRequest;
            _activeStopTarget = newDestinationStop;
            if (!_playerTaxi.NavigateToStop(newDestinationStop))
            {
                _activeStopTarget = previousTarget;
                return false;
            }

            if (_activeRequest != previousRequest || _currentPhase != RidePhase.InTransit)
            {
                return false;
            }

            _activeRequest = new RideRequest(_activeRequest.Passenger, _activeRequest.PickupNode,
                newDestinationStop.Location.AccessRoadNode, updatedDistance, updatedFare, _activeRequest.District,
                _activeRequest.PickupStop, newDestinationStop, _activeRequest.PickupStopId, newDestinationStop.StopId);
            OnDestinationUpdated?.Invoke(_activeRequest, _activeRequest.DestinationNode);
            HandleStopReached(newDestinationStop);
            return true;
        }

        public bool CompleteAndSettleRide(
            int bonusTipCredits = 0,
            int affinityDelta = 0,
            int trustDelta = 0,
            int suspicionDelta = 0)
        {
            if (_currentPhase != RidePhase.ArrivedAtDestination || _activeRequest == null)
            {
                return false;
            }

            if (_playerTaxi == null || !IsValidDestinationBinding()
                || !_playerTaxi.IsAtStop(_activeRequest.DestinationStop))
            {
                CancelActiveRide();
                return false;
            }

            var request = _activeRequest;
            int baseFare = request.QuotedBaseFare;
            int defaultTip = Mathf.RoundToInt(baseFare * request.Passenger.TipGenerosity);
            int totalTip = Mathf.Max(0, defaultTip + bonusTipCredits);

            var settlement = new RideSettlementResult(
                passenger: request.Passenger,
                pickupNode: request.PickupNode,
                destinationNode: request.DestinationNode,
                baseFarePaid: baseFare,
                tipPaid: totalTip);

            if (_playerStateService != null)
            {
                _playerStateService.AddCredits(settlement.TotalCreditsEarned);

                int currentMinutes = _timeService != null ? _timeService.CurrentTime.TotalMinutes : 0;
                _playerStateService.RecordResidentInteraction(
                    residentId: request.Passenger.PassengerId,
                    affinityDelta: affinityDelta,
                    trustDelta: trustDelta,
                    suspicionDelta: suspicionDelta,
                    currentTotalMinutes: currentMinutes);
            }

            _activeRequest = null;
            _activeStopTarget = null;
            SetPhase(RidePhase.Idle);
            OnRideCompleted?.Invoke(settlement);
            return true;
        }

        public void CancelActiveRide()
        {
            if (_currentPhase == RidePhase.Idle)
            {
                return;
            }

            var cancelled = _activeRequest;
            _activeRequest = null;
            _activeStopTarget = null;

            if (_playerTaxi != null && _playerTaxi.HasActiveRoute)
            {
                _playerTaxi.CancelRoute();
            }

            SetPhase(RidePhase.Idle);
            if (cancelled != null)
            {
                OnRideCancelled?.Invoke(cancelled);
            }
        }

        private void HandleStopReached(TaxiStopAnchor reachedStop)
        {
            if (_activeRequest == null || reachedStop != _activeStopTarget
                || (_currentPhase != RidePhase.EnRouteToPickup && _currentPhase != RidePhase.InTransit))
            {
                return;
            }

            bool bindingsValid = _currentPhase == RidePhase.EnRouteToPickup
                ? IsValidRequest()
                : IsValidDestinationBinding();
            // Activation/retargeting probes this handler while the taxi is still en route.
            // Such a probe is not an invalid arrival and must not cancel its own request.
            if (_playerTaxi != null && !_playerTaxi.IsAtStop(reachedStop)) return;
            if (_playerTaxi == null || !bindingsValid)
            {
                CancelActiveRide();
                return;
            }

            if (_currentPhase == RidePhase.EnRouteToPickup && reachedStop == _activeRequest.PickupStop)
            {
                SetPhase(RidePhase.AwaitingBoarding);
            }
            else if (_currentPhase == RidePhase.InTransit && reachedStop == _activeRequest.DestinationStop)
            {
                SetPhase(RidePhase.ArrivedAtDestination);
                OnArrivedAtDestination?.Invoke(_activeRequest);
            }
        }

        private void HandleStopNavigationFailed(TaxiStopAnchor failedStop)
        {
            if (_activeRequest != null && failedStop == _activeStopTarget
                && (_currentPhase == RidePhase.EnRouteToPickup || _currentPhase == RidePhase.AwaitingBoarding
                    || _currentPhase == RidePhase.InTransit || _currentPhase == RidePhase.ArrivedAtDestination))
            {
                CancelActiveRide();
            }
        }

        private float TaxiLength => _playerTaxi != null ? _playerTaxi.LengthMeters : 4.5f;
        private float TaxiWidth => _playerTaxi != null ? _playerTaxi.WidthMeters : 2f;

        private bool IsValidStop(TaxiStopAnchor stop)
        {
            return stop != null && stop.Validate(_roadNetwork, TaxiLength, TaxiWidth, out _)
                && (_worldRegistry == null || _worldRegistry.GetTaxiStop(stop.StopId) == stop);
        }

        private bool IsValidRequest()
        {
            return _activeRequest != null
                && IsValidStop(_activeRequest.PickupStop)
                && _activeRequest.PickupStop.Location.AccessRoadNode == _activeRequest.PickupNode
                && string.Equals(_activeRequest.PickupStopId, _activeRequest.PickupStop.StopId, StringComparison.Ordinal)
                && IsValidDestinationBinding();
        }

        private bool IsValidDestinationBinding()
        {
            // Boarding finishes the pickup interaction. Its immutable request fields remain
            // history; later changes to that authored stop must not strand the passenger.
            return _activeRequest != null
                && IsValidStop(_activeRequest.DestinationStop)
                && _activeRequest.DestinationStop.Location.AccessRoadNode == _activeRequest.DestinationNode
                && string.Equals(_activeRequest.DestinationStopId, _activeRequest.DestinationStop.StopId, StringComparison.Ordinal);
        }

        private void SetPhase(RidePhase nextPhase)
        {
            if (_currentPhase == nextPhase) return;
            _currentPhase = nextPhase;
            OnPhaseChanged?.Invoke(_currentPhase);
        }
    }
}
