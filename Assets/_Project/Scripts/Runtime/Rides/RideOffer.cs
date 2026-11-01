using System;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.World;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// One runtime-generated ordinary taxi offer over authored passengers and real city locations.
    /// This is an offer DTO, not a second ride state machine.
    /// </summary>
    public sealed class RideOffer
    {
        public string OfferId { get; }
        public PassengerDefinitionSO Passenger { get; }
        public RoadNode PickupNode { get; }
        public RoadNode DestinationNode { get; }
        public TaxiStopAnchor PickupStop { get; }
        public TaxiStopAnchor DestinationStop { get; }
        public string PickupStopId { get; }
        public string DestinationStopId { get; }
        public string PickupLocationId { get; }
        public string DestinationLocationId { get; }
        public DistrictConfigSO District { get; }
        public float PlannedDistanceMeters { get; }
        public int QuotedFare { get; }
        public int CreatedAtWorldMinutes { get; }
        public int ExpiresAtWorldMinutes { get; }

        public string Key => Passenger.PassengerId + "|" + PickupStopId + "|" + DestinationStopId;

        public RideOffer(
            string offerId,
            PassengerDefinitionSO passenger,
            RoadNode pickupNode,
            RoadNode destinationNode,
            string pickupLocationId,
            string destinationLocationId,
            DistrictConfigSO district,
            float plannedDistanceMeters,
            int quotedFare,
            int createdAtWorldMinutes,
            int expiresAtWorldMinutes,
            TaxiStopAnchor pickupStop = null,
            TaxiStopAnchor destinationStop = null)
        {
            OfferId = offerId ?? throw new ArgumentNullException(nameof(offerId));
            Passenger = passenger ?? throw new ArgumentNullException(nameof(passenger));
            PickupNode = pickupNode ?? throw new ArgumentNullException(nameof(pickupNode));
            DestinationNode = destinationNode ?? throw new ArgumentNullException(nameof(destinationNode));
            PickupStop = pickupStop;
            DestinationStop = destinationStop;
            PickupStopId = pickupStop != null ? pickupStop.StopId : string.Empty;
            DestinationStopId = destinationStop != null ? destinationStop.StopId : string.Empty;
            PickupLocationId = pickupLocationId ?? string.Empty;
            DestinationLocationId = destinationLocationId ?? string.Empty;
            District = district;
            PlannedDistanceMeters = plannedDistanceMeters;
            QuotedFare = quotedFare;
            CreatedAtWorldMinutes = createdAtWorldMinutes;
            ExpiresAtWorldMinutes = expiresAtWorldMinutes;
        }

        public bool IsExpired(int currentWorldMinutes) => currentWorldMinutes >= ExpiresAtWorldMinutes;
    }
}
