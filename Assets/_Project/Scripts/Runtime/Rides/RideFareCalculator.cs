using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.World;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Single authority for ground-metre route distance and fare quotation.
    /// Uses RoadNetwork's sampled isometric ground projection, never raw scene-space distance.
    /// </summary>
    public static class RideFareCalculator
    {
        public const int DefaultBasePickupFare = 15;
        public const float DefaultCreditsPerUnitDistance = 2.0f;

        public static bool TryQuote(
            RoadNetwork roadNetwork,
            PassengerDefinitionSO passenger,
            RoadNode pickupNode,
            RoadNode destinationNode,
            DistrictConfigSO district,
            out RideQuote quote)
        {
            quote = default;

            if (roadNetwork == null || passenger == null || pickupNode == null || destinationNode == null)
            {
                return false;
            }

            if (ReferenceEquals(pickupNode, destinationNode))
            {
                return false;
            }

            var path = roadNetwork.FindPath(pickupNode, destinationNode);
            if (path == null || path.Count < 2)
            {
                return false;
            }

            float distanceMeters = CalculateGroundDistance(roadNetwork.GroundProjection, path);
            if (distanceMeters <= 0.01f)
            {
                return false;
            }

            quote = new RideQuote(
                path,
                distanceMeters,
                CalculateFare(distanceMeters, passenger.FareMultiplier, district));
            return true;
        }

        /// <summary>Paid travel starts at the pickup bay and ends at the destination bay.</summary>
        public static bool TryQuote(
            RoadNetwork roadNetwork,
            PassengerDefinitionSO passenger,
            TaxiStopAnchor pickupStop,
            TaxiStopAnchor destinationStop,
            DistrictConfigSO district,
            out RideQuote quote,
            float vehicleLengthMeters = 4.5f,
            float vehicleWidthMeters = 2f)
        {
            quote = default;
            if (roadNetwork == null || passenger == null || pickupStop == null || destinationStop == null
                || pickupStop == destinationStop
                || !pickupStop.Prepare(roadNetwork, vehicleLengthMeters, vehicleWidthMeters)
                || !destinationStop.Prepare(roadNetwork, vehicleLengthMeters, vehicleWidthMeters))
            {
                return false;
            }

            // Preserve the authored departure and approach direction even when a shorter graph
            // route exists through another side of either junction.
            var middle = roadNetwork.FindPath(pickupStop.DepartureNextNode, destinationStop.ApproachFromNode);
            if (middle == null || middle.Count == 0)
            {
                return false;
            }

            var path = new List<RoadNode>(middle.Count + 2) { pickupStop.DepartureNode };
            path.AddRange(middle);
            path.Add(destinationStop.ApproachNode);
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] == null || !path[i].isActiveAndEnabled || roadNetwork.GetNode(path[i].NodeId) != path[i])
                {
                    return false;
                }
            }
            float distance = pickupStop.PullOutLength
                + CalculateGroundDistance(roadNetwork.GroundProjection, path)
                + destinationStop.PullInLength;
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0.01f)
            {
                return false;
            }

            quote = new RideQuote(path, distance, CalculateFare(distance, passenger.FareMultiplier, district));
            return true;
        }

        public static float CalculateGroundDistance(IsometricGroundProjection projection, IReadOnlyList<RoadNode> path)
        {
            if (path == null || path.Count < 2)
            {
                return 0f;
            }

            float totalMeters = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (path[i] != null && path[i + 1] != null)
                {
                    totalMeters += projection.Distance(path[i].Position, path[i + 1].Position);
                }
            }

            return totalMeters;
        }

        public static int CalculateFare(float distanceMeters, float passengerMultiplier, DistrictConfigSO district)
        {
            if (district != null)
            {
                return district.CalculateEstimatedFare(distanceMeters, passengerMultiplier);
            }

            float raw = (DefaultBasePickupFare + distanceMeters * DefaultCreditsPerUnitDistance) * Mathf.Max(0.1f, passengerMultiplier);
            return Mathf.RoundToInt(raw);
        }
    }
}
