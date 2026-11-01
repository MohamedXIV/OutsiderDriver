using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Core.State;
using OutsiderDriver.Navigation;
using OutsiderDriver.Rides;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    // Explicit metre-space authoring, simulated through the real controller and ride service.
    public class CurbTaxiIntegrationTests
    {
        private TaxiStopTestFixture _world;
        [SetUp] public void SetUp() => _world = new TaxiStopTestFixture();
        [TearDown] public void TearDown() => _world.Dispose();

        [TestCase(0.02f)]
        [TestCase(0.1f)]
        public void Arrival_FollowsAuthoredPullIn_AndWaitsAtExactPoseAndHeading(float dt)
        {
            var taxi = _world.Taxi();
            int arrivals = 0;
            taxi.OnStopReached += stop => { Assert.AreSame(_world.Pickup, stop); arrivals++; };
            Assert.IsTrue(taxi.NavigateToStop(_world.Pickup));
            int samples = 0;
            for (int i = 0; i < 20000 && (taxi.HasActiveRoute || taxi.HasPendingStopMotion); i++)
            {
                Vector2 before = _world.Ground(taxi);
                taxi.TickMovement(dt);
                Vector2 position = _world.Ground(taxi);
                Assert.LessOrEqual(Vector2.Distance(before, position), taxi.MaxSpeed * dt + 0.002f,
                    "Road-to-curb entry and final arrival must consume physical travel rather than snap.");
                if (!taxi.IsCurbManeuvering) continue;
                Assert.IsTrue(_world.Pickup.TryProject(false, position, out float distance, out float lateral));
                Assert.Less(Mathf.Abs(lateral), 0.02f);
                Assert.Greater(Vector2.Dot(taxi.ForwardGroundDirection, _world.Pickup.TangentAtDistance(false, distance)), 0.999f);
                Assert.AreEqual(0, arrivals, "Crossing the access node is not stop arrival.");
                samples++;
            }
            Assert.Greater(samples, 20);
            Assert.AreEqual(1, arrivals);
            Assert.IsTrue(taxi.IsAtStop(_world.Pickup));
            Assert.Less(Vector2.Distance(_world.Ground(taxi), _world.Pickup.StopGroundPosition), 0.001f);
            Assert.Greater(Vector2.Dot(taxi.ForwardGroundDirection, _world.Pickup.StopGroundDirection), 0.9999f);
            Assert.AreEqual(0f, taxi.CurrentSpeed, 0.001f);
            Vector2 parked = _world.Ground(taxi);
            for (int i = 0; i < 100; i++) taxi.TickMovement(dt);
            Assert.Less(Vector2.Distance(parked, _world.Ground(taxi)), 0.001f);
            Assert.AreEqual(1, arrivals);
            foreach (RoadNode node in _world.Network.AllNodes) Assert.AreNotSame(taxi, node.OccupyingVehicle);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WaitingForBoarding_LeavesThroughLaneUsableByAmbientTraffic(bool reverseTickOrder)
        {
            var taxi = _world.Taxi();
            using var ride = Service(taxi);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            var ambient = _world.Taxi();
            Assert.IsTrue(ambient.NavigateAlongPath(new[] { _world.Start, _world.Pickup.ApproachNode,
                _world.Pickup.DepartureNode, _world.Pickup.DepartureNextNode, _world.Destination.ApproachNode }));
            bool passedBay = false;
            for (int i = 0; i < 5000 && ambient.HasActiveRoute; i++)
            {
                if (reverseTickOrder) taxi.TickMovement(0.02f);
                ambient.TickMovement(0.02f);
                if (!reverseTickOrder) taxi.TickMovement(0.02f);
                Vector2 moving = _world.Ground(ambient);
                if (moving.x > 35f) passedBay = true;
                Assert.AreEqual(-1.5f, moving.y, 0.02f);
                Assert.IsTrue(taxi.IsAtStop(_world.Pickup));
                Assert.AreEqual(RidePhase.AwaitingBoarding, ride.CurrentPhase);
            }
            Assert.IsTrue(passedBay, "An unboarded taxi in its bay must not stop ordinary through traffic.");
            Assert.IsFalse(ambient.HasActiveRoute);
            foreach (RoadNode node in _world.Network.AllNodes) Assert.AreNotSame(taxi, node.OccupyingVehicle);
        }

        [Test]
        public void Departure_YieldsToOccupiedMerge_ThenFollowsPullOutWithoutTeleporting()
        {
            var taxi = _world.Taxi();
            Assert.IsTrue(taxi.NavigateToStop(_world.Pickup));
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            var obstacle = _world.Taxi(new Vector2(40f, -1.5f));
            Vector2 parked = _world.Ground(taxi);
            Assert.IsTrue(taxi.NavigateToStop(_world.Destination));
            Assert.Less(Vector2.Distance(parked, _world.Ground(taxi)), 0.001f);
            for (int i = 0; i < 100; i++) taxi.TickMovement(0.02f);
            Assert.Less(Vector2.Distance(parked, _world.Ground(taxi)), 0.001f,
                "An occupied through-lane merge must keep the taxi in its bay.");
            Assert.IsTrue(taxi.HasPendingStopMotion);
            obstacle.gameObject.SetActive(false);
            int curveSamples = 0;
            for (int i = 0; i < 2000; i++)
            {
                Vector2 before = _world.Ground(taxi);
                taxi.TickMovement(0.02f);
                Assert.LessOrEqual(Vector2.Distance(before, _world.Ground(taxi)), taxi.MaxSpeed * 0.02f + 0.002f);
                if (!taxi.IsCurbManeuvering)
                {
                    if (curveSamples > 0) break;
                    continue;
                }
                Assert.IsTrue(_world.Pickup.TryProject(true, _world.Ground(taxi), out _, out float lateral));
                Assert.Less(Mathf.Abs(lateral), 0.02f);
                curveSamples++;
            }
            Assert.Greater(curveSamples, 20);
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.IsTrue(taxi.IsAtStop(_world.Destination));
            Assert.IsTrue(_world.Pickup.IsAvailableFor(new object()), "The old bay lease is released after physical departure.");
        }

        [Test]
        public void RetargetBackToDepartingBay_RetainsItsLeaseThroughoutTheReturnRoute()
        {
            var taxi = _world.Taxi();
            var otherOwner = new object();
            Assert.IsTrue(taxi.NavigateToStop(_world.Pickup));
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.IsTrue(taxi.NavigateToStop(_world.Destination));
            for (int i = 0; i < 25; i++) taxi.TickMovement(0.02f);
            Assert.IsTrue(taxi.IsCurbManeuvering);
            Assert.Greater(_world.Ground(taxi).x, 30f);
            Assert.Less(_world.Ground(taxi).x, 40f);

            Vector2 beforeRetarget = _world.Ground(taxi);
            Assert.IsTrue(taxi.NavigateToStop(_world.Pickup));
            Assert.Less(Vector2.Distance(beforeRetarget, _world.Ground(taxi)), 0.001f);
            for (int i = 0; i < 2000 && taxi.IsCurbManeuvering; i++)
            {
                taxi.TickMovement(0.02f);
                Assert.IsFalse(_world.Pickup.IsAvailableFor(otherOwner));
            }
            Assert.IsFalse(taxi.IsCurbManeuvering, "The already committed pull-out must finish before the return route.");
            Assert.IsTrue(taxi.HasActiveRoute || taxi.HasPendingStopMotion);
            Assert.IsFalse(taxi.IsAtStop(_world.Pickup));
            Assert.IsFalse(_world.Pickup.IsAvailableFor(otherOwner),
                "Finishing departure must retain the old bay lease when that bay is also the new goal.");
            for (int i = 0; i < 20000 && (taxi.HasActiveRoute || taxi.HasPendingStopMotion); i++)
            {
                taxi.TickMovement(0.05f);
                Assert.IsFalse(_world.Pickup.IsAvailableFor(otherOwner), "Another taxi must not steal the reserved return bay.");
            }
            Assert.IsTrue(taxi.IsAtStop(_world.Pickup));
            Assert.IsFalse(_world.Pickup.IsAvailableFor(otherOwner));
        }

        [Test]
        public void CancellationDuringPullIn_DrainsToBay_WithoutRestoringCancelledRide()
        {
            var taxi = _world.Taxi();
            using var ride = Service(taxi);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            for (int i = 0; i < 5000 && !taxi.IsCurbManeuvering; i++) taxi.TickMovement(0.02f);
            Assert.IsTrue(taxi.IsCurbManeuvering);
            for (int i = 0; i < 50; i++) taxi.TickMovement(0.02f);
            Assert.IsTrue(taxi.IsCurbManeuvering);
            Vector2 beforeCancel = _world.Ground(taxi);
            ride.CancelActiveRide();
            Assert.AreEqual(RidePhase.Idle, ride.CurrentPhase);
            Assert.IsNull(ride.ActiveRequest);
            Assert.Less(Vector2.Distance(beforeCancel, _world.Ground(taxi)), 0.001f);
            Assert.IsTrue(taxi.HasPendingStopMotion);
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.IsTrue(taxi.IsAtStop(_world.Pickup));
            Assert.AreEqual(RidePhase.Idle, ride.CurrentPhase, "Late physical arrival must not revive the cancelled logical ride.");
            Assert.IsFalse(ride.BoardPassenger());
        }

        [Test]
        public void CancellationDuringCommittedTurn_RetainsOwnershipUntilClear_ThenParksSafely()
        {
            CreateRemoteTurn();
            var taxi = TaxiOnRemoteApproach();
            using var ride = Service(taxi);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            ReachRemoteTurn(taxi);
            Vector2 beforeCancel = _world.Ground(taxi);
            Assert.AreSame(taxi, _world.ReturnJunction.OccupyingVehicle);
            ride.CancelActiveRide();
            Assert.AreEqual(RidePhase.Idle, ride.CurrentPhase);
            Assert.Less(Vector2.Distance(beforeCancel, _world.Ground(taxi)), 0.001f);
            Assert.AreSame(taxi, _world.ReturnJunction.OccupyingVehicle);
            for (int i = 0; i < 5000 && _world.ReturnJunction.OccupyingVehicle == taxi; i++)
            {
                Vector2 before = _world.Ground(taxi);
                taxi.TickMovement(0.02f);
                Assert.LessOrEqual(Vector2.Distance(before, _world.Ground(taxi)), taxi.MaxSpeed * 0.02f + 0.002f);
            }
            Assert.IsNull(_world.ReturnJunction.OccupyingVehicle);
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.IsTrue(taxi.IsAtStop(_world.Pickup));
            Assert.AreEqual(RidePhase.Idle, ride.CurrentPhase);
        }

        [Test]
        public void RouteToBay_ObeysRedStopLine_AndReleasesJunctionBeforeWaitingAtBay()
        {
            CreateRemoteTurn();
            var junction = _world.ReturnJunction;
            var incoming = _world.Network.GetNode("east_corner");
            var crossIncoming = _world.Node("cross_incoming", 240, 60);
            crossIncoming.AddConnection(junction);
            _world.Network.RebuildNetwork();
            var controller = _world.Point("signals", new Vector2(200, 60)).gameObject.AddComponent<TrafficSignalController>();
            var cross = _world.Point("cross_approach", Vector2.zero).gameObject.AddComponent<TrafficSignalApproach>();
            cross.Configure(controller, junction, crossIncoming, _world.Point("cross_stop", new Vector2(216, 61.5f)), 0);
            var approach = _world.Point("bay_route_approach", Vector2.zero).gameObject.AddComponent<TrafficSignalApproach>();
            approach.Configure(controller, junction, incoming, _world.Point("taxi_stop_line", new Vector2(201.5f, 44)), 1);
            controller.Initialize(junction, new[] { cross, approach }, 120f, 2f, 1f);
            controller.Tick(1f);
            Assert.AreEqual(SignalState.Red, approach.State);
            var taxi = TaxiOnRemoteApproach();
            Assert.IsTrue(taxi.NavigateToStop(_world.Pickup));
            for (int i = 0; i < 1000; i++)
            {
                taxi.TickMovement(0.02f);
                Assert.LessOrEqual(_world.Ground(taxi).y + taxi.LengthMeters * 0.5f, 44.001f);
                Assert.IsNull(junction.OccupyingVehicle);
                Assert.IsFalse(taxi.IsAtStop(_world.Pickup));
            }
            Assert.Less(taxi.CurrentSpeed, 0.05f);
            controller.Tick(123f);
            Assert.IsTrue(approach.AllowsEntry);
            bool ownedJunction = false;
            for (int i = 0; i < 20000 && (taxi.HasActiveRoute || taxi.HasPendingStopMotion); i++)
            {
                taxi.TickMovement(0.02f);
                if (junction.OccupyingVehicle == taxi) ownedJunction = true;
            }
            Assert.IsTrue(ownedJunction);
            Assert.IsTrue(taxi.IsAtStop(_world.Pickup));
            Assert.IsNull(junction.OccupyingVehicle);
        }

        [Test]
        public void NewCommandForCurrentBay_RevokesDepartureBeforeAnyMotion()
        {
            var taxi = _world.Taxi();
            Assert.IsTrue(taxi.NavigateToStop(_world.Pickup));
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Vector2 parked = _world.Ground(taxi);
            Assert.IsTrue(taxi.NavigateToStop(_world.Destination));
            Assert.IsTrue(taxi.HasPendingStopMotion);
            taxi.CancelRoute(); // logical cancellation may still be draining a pending stop goal
            Assert.IsTrue(taxi.NavigateToStop(_world.Pickup));
            Assert.IsTrue(taxi.IsAtStop(_world.Pickup));
            Assert.IsFalse(taxi.HasPendingStopMotion);
            for (int i = 0; i < 100; i++) taxi.TickMovement(0.02f);
            Assert.Less(Vector2.Distance(parked, _world.Ground(taxi)), 0.001f);
            Assert.IsTrue(_world.Destination.IsAvailableFor(new object()));
        }

        [Test]
        public void ReplacementRideDuringCancelledPullIn_UsesOnePhysicalArrivalAtSamePickup()
        {
            var taxi = _world.Taxi();
            using var ride = Service(taxi);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            for (int i = 0; i < 5000 && !taxi.IsCurbManeuvering; i++) taxi.TickMovement(0.02f);
            Assert.IsTrue(taxi.IsCurbManeuvering);
            ride.CancelActiveRide();
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            Assert.AreEqual(RidePhase.EnRouteToPickup, ride.CurrentPhase);
            int arrivals = 0;
            taxi.OnStopReached += stop => arrivals++;
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(1, arrivals);
            Assert.IsTrue(taxi.IsAtStop(_world.Pickup));
            Assert.AreEqual(RidePhase.AwaitingBoarding, ride.CurrentPhase);
        }

        private void CreateRemoteTurn()
        {
            var turn = _world.Point("remote_authored_turn", new Vector2(200, 60)).gameObject.AddComponent<RoadTurnPath>();
            turn.Configure(_world.Network.GetNode("east_corner"), _world.ReturnJunction, _world.Network.GetNode("west_corner"),
                _world.Point("turn_start", new Vector2(201.5f, 48)), _world.Point("turn_h1", new Vector2(201.5f, 56)),
                _world.Point("turn_h2", new Vector2(196, 61.5f)), _world.Point("turn_exit", new Vector2(188, 61.5f)));
            Assert.IsTrue(turn.Prepare(_world.Network.GroundProjection));
        }

        private VehicleController TaxiOnRemoteApproach() => _world.Taxi(new Vector2(201.5f, 20));

        private void ReachRemoteTurn(VehicleController taxi)
        {
            for (int i = 0; i < 5000; i++)
            {
                taxi.TickMovement(0.02f);
                Vector2 position = _world.Ground(taxi);
                if (position.x < 201f && position.x > 195f && position.y > 49f && position.y < 61.4f) return;
            }
            Assert.Fail("The fixture must reach the committed interior of its authored junction turn.");
        }

        private RideLifecycleService Service(VehicleController taxi) =>
            new RideLifecycleService(_world.Network, taxi, new PlayerStateService(), _world.Time, _world.Registry);
    }
}
