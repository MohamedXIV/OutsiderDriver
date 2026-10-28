using System.Collections.Generic;
using OutsiderDriver.Navigation;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Ground-metre route estimate and quoted fare for a candidate ride.
    /// Shared by RideLifecycleService and the systemic demand source so both use one calculation.
    /// </summary>
    public readonly struct RideQuote
    {
        public readonly IReadOnlyList<RoadNode> Path;
        public readonly float DistanceMeters;
        public readonly int QuotedFare;

        public bool IsValid => Path != null && Path.Count >= 2 && DistanceMeters > 0.01f;

        public RideQuote(IReadOnlyList<RoadNode> path, float distanceMeters, int quotedFare)
        {
            Path = path;
            DistanceMeters = distanceMeters;
            QuotedFare = quotedFare;
        }
    }
}
