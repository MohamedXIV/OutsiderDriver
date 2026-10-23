using System;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Immutable snapshot of an ordinary taxi ride request (not a quest/mission per Decision P-006).
    /// </summary>
    [Serializable]
    public sealed class RideRequest
    {
        public PassengerDefinitionSO Passenger { get; }
        public RoadNode PickupNode { get; }
        public RoadNode DestinationNode { get; }
        public DistrictConfigSO District { get; }
        public float PlannedDistance { get; }
        public int QuotedBaseFare { get; }

        public RideRequest(
            PassengerDefinitionSO passenger,
            RoadNode pickupNode,
            RoadNode destinationNode,
            float plannedDistance,
            int quotedBaseFare,
            DistrictConfigSO district = null)
        {
            Passenger = passenger ?? throw new ArgumentNullException(nameof(passenger));
            PickupNode = pickupNode ?? throw new ArgumentNullException(nameof(pickupNode));
            DestinationNode = destinationNode ?? throw new ArgumentNullException(nameof(destinationNode));
            PlannedDistance = Math.Max(0f, plannedDistance);
            QuotedBaseFare = Math.Max(0, quotedBaseFare);
            District = district;
        }
    }

    /// <summary>
    /// Summary of a completed and settled taxi ride.
    /// </summary>
    [Serializable]
    public readonly struct RideSettlementResult
    {
        public readonly PassengerDefinitionSO Passenger;
        public readonly RoadNode PickupNode;
        public readonly RoadNode DestinationNode;
        public readonly int BaseFarePaid;
        public readonly int TipPaid;
        public readonly int TotalCreditsEarned;

        public RideSettlementResult(
            PassengerDefinitionSO passenger,
            RoadNode pickupNode,
            RoadNode destinationNode,
            int baseFarePaid,
            int tipPaid)
        {
            Passenger = passenger;
            PickupNode = pickupNode;
            DestinationNode = destinationNode;
            BaseFarePaid = Math.Max(0, baseFarePaid);
            TipPaid = Math.Max(0, tipPaid);
            TotalCreditsEarned = BaseFarePaid + TipPaid;
        }
    }
}
