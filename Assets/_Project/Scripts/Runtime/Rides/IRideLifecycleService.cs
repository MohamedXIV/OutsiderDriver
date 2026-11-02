using System;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.World;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Authoritative domain service coordinating passenger hails, pickup routing,
    /// boarding, in-transit destination changes, arrival, and fare settlement.
    /// </summary>
    public interface IRideLifecycleService
    {
        RidePhase CurrentPhase { get; }
        RideRequest ActiveRequest { get; }
        PassengerDefinitionSO CurrentPassenger => ActiveRequest?.Passenger;
        bool HasActiveRide => CurrentPhase != RidePhase.Idle;

        event Action<RidePhase> OnPhaseChanged;
        event Action<RideRequest> OnHailOffered;
        event Action<RideRequest> OnHailDeclined;
        event Action<RideRequest> OnHailAccepted;
        event Action<RideRequest> OnPassengerBoarded;
        event Action<RideRequest, RoadNode> OnDestinationUpdated;
        event Action<RideRequest> OnArrivedAtDestination;
        event Action<RideSettlementResult> OnRideCompleted;
        event Action<RideRequest> OnRideCancelled;

        /// <summary>
        /// Compatibility entry point resolving nodes to explicit stops in the injected world registry.
        /// Fails if either node has no unique valid authored curb stop.
        /// </summary>
        bool TryOfferHail(PassengerDefinitionSO passenger, RoadNode pickupNode, RoadNode destinationNode, DistrictConfigSO district = null);

        /// <summary>
        /// Offers a ride between explicit authored curb stops. Implementations without stop
        /// navigation fail closed; callers must not fall back to arbitrary road nodes.
        /// </summary>
        bool TryOfferHail(PassengerDefinitionSO passenger, TaxiStopAnchor pickupStop, TaxiStopAnchor destinationStop, DistrictConfigSO district = null) => false;

        /// <summary>Declines the currently offered hail and returns to Idle.</summary>
        bool DeclineHail();

        /// <summary>
        /// Accepts the currently offered hail and routes the taxi to its pickup bay.
        /// AwaitingBoarding requires the taxi to be physically parked at that stop.
        /// </summary>
        bool AcceptHail();

        /// <summary>
        /// Boards the waiting passenger at the pickup stop and begins routing to the destination bay.
        /// </summary>
        bool BoardPassenger();

        /// <summary>
        /// Allows a mid-ride destination change when roads connect to the new target (GAME_DESIGN §3.A).
        /// </summary>
        bool TryChangeDestination(RoadNode newDestinationNode);

        bool TryChangeDestination(TaxiStopAnchor newDestinationStop) => false;

        /// <summary>
        /// Completes the ride at the destination, settles base fare + tip into PlayerStateService,
        /// records the resident encounter, and resets to Idle.
        /// </summary>
        bool CompleteAndSettleRide(int bonusTipCredits = 0, int affinityDelta = 0, int trustDelta = 0, int suspicionDelta = 0);

        /// <summary>
        /// Cancels the active ride or hail and returns to Idle without paying fare.
        /// </summary>
        void CancelActiveRide();
    }
}
