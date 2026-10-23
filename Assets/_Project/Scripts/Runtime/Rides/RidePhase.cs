namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Represents the lifecycle state of a taxi fare from hail offer to settlement (Decisions P-002, P-006).
    /// </summary>
    public enum RidePhase
    {
        /// <summary>
        /// Taxi is free with no active hail or passenger.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// A passenger has hailed or requested a ride; awaiting driver acceptance or decline.
        /// </summary>
        HailOffered = 1,

        /// <summary>
        /// Driver accepted the hail and is traveling along the road network to the pickup node.
        /// </summary>
        EnRouteToPickup = 2,

        /// <summary>
        /// Taxi has arrived at the pickup node and the passenger is boarding.
        /// </summary>
        AwaitingBoarding = 3,

        /// <summary>
        /// Passenger is inside the taxi traveling toward the dropoff destination.
        /// </summary>
        InTransit = 4,

        /// <summary>
        /// Taxi has arrived at the dropoff node; awaiting fare settlement and passenger exit.
        /// </summary>
        ArrivedAtDestination = 5
    }
}
