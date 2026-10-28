using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Concrete domain service managing the passenger ride lifecycle:
    /// HailOffered -> EnRouteToPickup -> AwaitingBoarding -> InTransit -> ArrivedAtDestination -> Idle.
    /// Decoupled from UI; Presenters subscribe to its events (Systems Architecture §1 & §6).
    /// </summary>
    public sealed class RideLifecycleService : IRideLifecycleService, IDisposable
    {
        private const float MinimumPickupProximityMeters = 1.0f;

        private readonly RoadNetwork _roadNetwork;
        private readonly VehicleController _playerTaxi;
        private readonly IPlayerStateService _playerStateService;
        private readonly ITimeService _timeService;

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
            ITimeService timeService = null)
        {
            _roadNetwork = roadNetwork;
            _playerTaxi = playerTaxi;
            _playerStateService = playerStateService;
            _timeService = timeService;

            if (_playerTaxi != null)
            {
                _playerTaxi.OnDestinationReached += HandleVehicleDestinationReached;
            }
        }

        public void Dispose()
        {
            if (_playerTaxi != null)
            {
                _playerTaxi.OnDestinationReached -= HandleVehicleDestinationReached;
            }
        }

        public bool TryOfferHail(
            PassengerDefinitionSO passenger,
            RoadNode pickupNode,
            RoadNode destinationNode,
            DistrictConfigSO district = null)
        {
            if (_currentPhase != RidePhase.Idle)
            {
                return false;
            }

            if (passenger == null || pickupNode == null || destinationNode == null || pickupNode == destinationNode)
            {
                return false;
            }

            if (!RideFareCalculator.TryQuote(_roadNetwork, passenger, pickupNode, destinationNode, district, out var quote))
            {
                return false;
            }

            _activeRequest = new RideRequest(
                passenger: passenger,
                pickupNode: pickupNode,
                destinationNode: destinationNode,
                plannedDistance: quote.DistanceMeters,
                quotedBaseFare: quote.QuotedFare,
                district: district);

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

            OnHailAccepted?.Invoke(_activeRequest);

            // Check if the taxi is already at the pickup approach in ground metres.
            // Lane offset is included so a curb-side stop beside the node still counts.
            float pickupDistanceMeters = _roadNetwork.GroundProjection.Distance(
                _playerTaxi.transform.position,
                _activeRequest.PickupNode.Position);
            float pickupProximityMeters = GetPickupProximityMeters(_activeRequest.PickupNode);

            if (pickupDistanceMeters <= pickupProximityMeters)
            {
                SetPhase(RidePhase.AwaitingBoarding);
                return true;
            }

            SetPhase(RidePhase.EnRouteToPickup);
            if (!_playerTaxi.NavigateTo(_activeRequest.PickupNode))
            {
                CancelActiveRide();
                return false;
            }

            return true;
        }

        public bool BoardPassenger()
        {
            if (_currentPhase != RidePhase.AwaitingBoarding || _activeRequest == null || _playerTaxi == null)
            {
                return false;
            }

            SetPhase(RidePhase.InTransit);
            if (!_playerTaxi.NavigateTo(_activeRequest.DestinationNode))
            {
                CancelActiveRide();
                return false;
            }

            OnPassengerBoarded?.Invoke(_activeRequest);
            return true;
        }

        public bool TryChangeDestination(RoadNode newDestinationNode)
        {
            if (_currentPhase != RidePhase.InTransit || _activeRequest == null || _playerTaxi == null || newDestinationNode == null)
            {
                return false;
            }

            if (newDestinationNode == _activeRequest.DestinationNode)
            {
                return true;
            }

            RoadNode currentStart = _playerTaxi.CurrentTargetNode ?? _roadNetwork.FindNearestNode(_playerTaxi.transform.position);
            if (currentStart == null)
            {
                return false;
            }

            var newPath = _roadNetwork.FindPath(currentStart, newDestinationNode);
            if (newPath == null || newPath.Count == 0)
            {
                return false;
            }

            float additionalDistance = RideFareCalculator.CalculateGroundDistance(
                _roadNetwork.GroundProjection,
                newPath);
            int updatedFare = RideFareCalculator.CalculateFare(
                _activeRequest.PlannedDistance + additionalDistance,
                _activeRequest.Passenger.FareMultiplier,
                _activeRequest.District);

            _activeRequest = new RideRequest(
                passenger: _activeRequest.Passenger,
                pickupNode: _activeRequest.PickupNode,
                destinationNode: newDestinationNode,
                plannedDistance: _activeRequest.PlannedDistance + additionalDistance,
                quotedBaseFare: updatedFare,
                district: _activeRequest.District);

            _playerTaxi.NavigateTo(newDestinationNode);
            OnDestinationUpdated?.Invoke(_activeRequest, newDestinationNode);
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

        private void HandleVehicleDestinationReached(RoadNode reachedNode)
        {
            if (_activeRequest == null) return;

            if (_currentPhase == RidePhase.EnRouteToPickup && reachedNode == _activeRequest.PickupNode)
            {
                SetPhase(RidePhase.AwaitingBoarding);
            }
            else if (_currentPhase == RidePhase.InTransit && reachedNode == _activeRequest.DestinationNode)
            {
                SetPhase(RidePhase.ArrivedAtDestination);
                OnArrivedAtDestination?.Invoke(_activeRequest);
            }
        }

        private static float GetPickupProximityMeters(RoadNode pickupNode)
        {
            float laneOffset = pickupNode != null ? pickupNode.LaneOffsetMeters : 0f;
            return Mathf.Max(MinimumPickupProximityMeters, laneOffset + 1.0f);
        }

        private void SetPhase(RidePhase nextPhase)
        {
            if (_currentPhase == nextPhase) return;
            _currentPhase = nextPhase;
            OnPhaseChanged?.Invoke(_currentPhase);
        }
    }
}
