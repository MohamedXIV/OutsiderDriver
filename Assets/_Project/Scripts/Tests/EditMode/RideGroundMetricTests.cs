using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Rides;

namespace OutsiderDriver.Tests.EditMode
{
    public class RideGroundMetricTests
    {
        private readonly List<TaxiStopTestFixture> _worlds = new List<TaxiStopTestFixture>();
        [TearDown]
        public void TearDown()
        {
            for (int i = _worlds.Count - 1; i >= 0; i--) _worlds[i].Dispose();
            _worlds.Clear();
        }

        [Test]
        public void GroundMetreQuote_IsIndependentOfIsometricWorldProjection()
        {
            var wide = World(new Vector3(1f, 0.5f, 1f));
            var tall = World(new Vector3(2f, 1f, 1f));
            Assert.IsTrue(RideFareCalculator.TryQuote(wide.Network, wide.Passenger, wide.Pickup, wide.Destination, null, out var wideQuote));
            Assert.IsTrue(RideFareCalculator.TryQuote(tall.Network, tall.Passenger, tall.Pickup, tall.Destination, null, out var tallQuote));
            // Bay A exits at x=40, bay B enters at x=80: 40 m on road plus the two paid curb curves.
            float paidDistance = 40f + wide.Pickup.PullOutLength + wide.Destination.PullInLength;
            Assert.AreEqual(paidDistance, wideQuote.DistanceMeters, 0.001f);
            Assert.AreEqual(Mathf.RoundToInt(15f + 2f * paidDistance), wideQuote.QuotedFare);
            Assert.AreEqual(wideQuote.DistanceMeters, tallQuote.DistanceMeters, 0.001f);
            Assert.AreEqual(wideQuote.QuotedFare, tallQuote.QuotedFare);
            float wideWorld = Vector3.Distance(wide.Pickup.ApproachNode.Position, wide.Destination.ApproachNode.Position);
            float tallWorld = Vector3.Distance(tall.Pickup.ApproachNode.Position, tall.Destination.ApproachNode.Position);
            Assert.Greater(tallWorld, wideWorld + 1f, "The two grids must project to different world distances.");
        }

        [Test]
        public void GroundMetreQuote_UsesDistrictPolicy_AndExcludesDeadheadAndPickupPullIn()
        {
            var world = World();
            world.District.Initialize("district_01", "district_01", 4, 20, 3f);
            Assert.IsTrue(RideFareCalculator.TryQuote(world.Network, world.Passenger, world.Pickup, world.Destination, world.District, out var quote));
            float expected = world.Pickup.PullOutLength + 40f + world.Destination.PullInLength;
            Assert.AreEqual(expected, quote.DistanceMeters, 0.001f);
            Assert.AreEqual(Mathf.RoundToInt(20f + 3f * expected), quote.QuotedFare);
            Assert.Greater(expected, 40f);
            Assert.Less(expected, 40f + world.Pickup.PullInLength + world.Pickup.PullOutLength + world.Destination.PullInLength);
        }

        [Test]
        public void GroundMetreQuote_RejectsIdenticalInvalidAndDisconnectedStops()
        {
            var world = World();
            Assert.IsFalse(RideFareCalculator.TryQuote(world.Network, world.Passenger, world.Pickup, world.Pickup, null, out _));
            world.Alternate.enabled = false;
            Assert.IsFalse(RideFareCalculator.TryQuote(world.Network, world.Passenger, world.Pickup, world.Alternate, null, out _));
            world.Pickup.DepartureNextNode.RemoveConnection(world.Destination.ApproachNode);
            world.Network.RebuildNetwork();
            Assert.IsFalse(RideFareCalculator.TryQuote(world.Network, world.Passenger, world.Pickup, world.Destination, null, out _));
        }

        private TaxiStopTestFixture World(Vector3? size = null)
        {
            var world = new TaxiStopTestFixture(size);
            _worlds.Add(world);
            return world;
        }
    }
}
