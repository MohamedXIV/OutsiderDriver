using System;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;

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
        /// Offers a new passenger ride hail between the specified pickup and destination nodes.
        /// Fails if a ride is already active or nodes are invalid.
        /// </summary>
        bool TryOfferHail(PassengerDefinitionSO passenger, RoadNode pickupNode, RoadNode destinationNode, DistrictConfigSO district = null);

        /// <summary>
        /// Declines the currently offered hail and returns to Idle.
        /// </summary>
        bool DeclineHail();

        /// <summary>
        /// Accepts the currently offered hail and routes the taxi to the pickup node
        /// (or transitions directly to AwaitingBoarding if already at the pickup node).
        /// </summary>
        bool AcceptHail();

        /// <summary>
        /// Boards the waiting passenger at the pickup node and begins routing to the destination.
        /// </summary>
        bool BoardPassenger();

        /// <summary>
        /// Allows a mid-ride destination change when roads connect to the new target (GAME_DESIGN §3.A).
        /// </summary>
        bool TryChangeDestination(RoadNode newDestinationNode);

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
