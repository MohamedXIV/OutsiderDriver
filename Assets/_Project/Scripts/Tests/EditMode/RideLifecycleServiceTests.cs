using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Rides;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Tests.EditMode
{
    public class RideLifecycleServiceTests
    {
        private TaxiStopTestFixture _world;

        [SetUp] public void SetUp() => _world = new TaxiStopTestFixture();
        [TearDown] public void TearDown() => _world.Dispose();

        [Test]
        public void FullRideLifecycle_UsesAuthoredBays_AndSettlesExactlyOnce()
        {
            var taxi = _world.Taxi();
            _world.Time.SetTime(new WorldTime(day: 2, hour: 14, minute: 30));
            var player = new PlayerStateService(_world.Time);
            using var ride = Service(taxi, player);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination, _world.District));
            int quotedFare = ride.ActiveRequest.QuotedBaseFare;
            Assert.AreEqual(_world.District.CalculateEstimatedFare(ride.ActiveRequest.PlannedDistance, _world.Passenger.FareMultiplier), quotedFare);
            Assert.IsTrue(ride.AcceptHail());
            Assert.AreEqual(RidePhase.EnRouteToPickup, ride.CurrentPhase);
            Assert.IsFalse(ride.BoardPassenger());
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(RidePhase.AwaitingBoarding, ride.CurrentPhase);
            Assert.AreSame(_world.Pickup, taxi.ParkedStop);
            Assert.IsTrue(ride.BoardPassenger());
            Assert.AreEqual(RidePhase.InTransit, ride.CurrentPhase);
            Assert.IsFalse(ride.CompleteAndSettleRide());
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(RidePhase.ArrivedAtDestination, ride.CurrentPhase);
            Assert.AreSame(_world.Destination, taxi.ParkedStop);

            int creditsBefore = player.Credits;
            RideSettlementResult? result = null;
            int completionEvents = 0;
            ride.OnRideCompleted += value => { result = value; completionEvents++; };
            Assert.IsTrue(ride.CompleteAndSettleRide(bonusTipCredits: 4, affinityDelta: 12, trustDelta: 8, suspicionDelta: 3));
            int expectedTip = Mathf.RoundToInt(quotedFare * _world.Passenger.TipGenerosity) + 4;
            Assert.AreEqual(quotedFare, result.Value.BaseFarePaid);
            Assert.AreEqual(expectedTip, result.Value.TipPaid);
            Assert.AreEqual(creditsBefore + quotedFare + expectedTip, player.Credits);
            Assert.AreEqual(RidePhase.Idle, ride.CurrentPhase);
            Assert.IsNull(ride.ActiveRequest);
            Assert.IsFalse(ride.CompleteAndSettleRide());
            Assert.AreEqual(1, completionEvents);
            var relationship = player.GetOrAddResident(_world.Passenger.PassengerId);
            Assert.AreEqual(1, relationship.EncounterCount);
            Assert.AreEqual(12, relationship.Affinity);
            Assert.AreEqual(8, relationship.PersonalTrust);
            Assert.AreEqual(3, relationship.SuspicionOfHumanity);
            Assert.AreEqual(_world.Time.CurrentTime.TotalMinutes, relationship.LastEncounterTotalMinutes);
        }

        [Test]
        public void AcceptHail_WhenParkedAtPickupBay_AwaitsVoluntaryBoarding()
        {
            var taxi = _world.Taxi();
            Assert.IsTrue(taxi.NavigateToStop(_world.Pickup));
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            using var ride = Service(taxi);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            Assert.AreEqual(RidePhase.AwaitingBoarding, ride.CurrentPhase);
            Assert.IsFalse(taxi.HasActiveRoute);
        }

        [Test]
        public void AccessNodeProximity_DoesNotPermitBoardingBeforeBayArrival()
        {
            var taxi = _world.Taxi(new Vector2(20f, -1.5f));
            using var ride = Service(taxi);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            Assert.AreEqual(RidePhase.EnRouteToPickup, ride.CurrentPhase);
            Assert.IsFalse(ride.BoardPassenger(), "A taxi on the through lane has not reached the authored stop pose.");
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(RidePhase.AwaitingBoarding, ride.CurrentPhase);
        }

        [Test]
        public void TryChangeDestination_DuringTransit_UpdatesStopRouteAndFare()
        {
            var taxi = _world.Taxi();
            using var ride = StartBoardedRide(taxi);
            int originalFare = ride.ActiveRequest.QuotedBaseFare;
            Assert.IsTrue(ride.TryChangeDestination(_world.Alternate));
            Assert.AreSame(_world.Alternate, ride.ActiveRequest.DestinationStop);
            Assert.Greater(ride.ActiveRequest.QuotedBaseFare, originalFare);
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(RidePhase.ArrivedAtDestination, ride.CurrentPhase);
            Assert.AreSame(_world.Alternate, taxi.ParkedStop);
        }

        [Test]
        public void RetargetDuringDestinationPullIn_DrainsOriginalCurve_WithoutFalseRideArrival()
        {
            var taxi = _world.Taxi();
            using var ride = StartBoardedRide(taxi);
            for (int i = 0; i < 10000; i++)
            {
                taxi.TickMovement(0.02f);
                if (taxi.IsCurbManeuvering && _world.Ground(taxi).x > 80f) break;
            }
            Assert.IsTrue(taxi.IsCurbManeuvering);
            Assert.Greater(_world.Ground(taxi).x, 80f);
            Assert.IsTrue(ride.TryChangeDestination(_world.Alternate));
            var updatedRequest = ride.ActiveRequest;
            int arrivalEvents = 0;
            ride.OnArrivedAtDestination += request =>
            {
                Assert.AreSame(updatedRequest, request);
                Assert.AreSame(_world.Alternate, request.DestinationStop);
                arrivalEvents++;
            };
            bool drainedOriginalBay = false;
            for (int i = 0; i < 20000 && (taxi.HasActiveRoute || taxi.HasPendingStopMotion); i++)
            {
                taxi.TickMovement(0.02f);
                Assert.AreSame(updatedRequest, ride.ActiveRequest);
                if (Vector2.Distance(_world.Ground(taxi), _world.Destination.StopGroundPosition) < 0.001f)
                {
                    drainedOriginalBay = true;
                    Assert.AreEqual(RidePhase.InTransit, ride.CurrentPhase);
                    Assert.AreEqual(0, arrivalEvents);
                    Assert.IsFalse(ride.CompleteAndSettleRide());
                }
            }
            Assert.IsTrue(drainedOriginalBay, "Committed pull-in must finish physically before departure toward the new destination.");
            Assert.AreEqual(1, arrivalEvents);
            Assert.AreEqual(RidePhase.ArrivedAtDestination, ride.CurrentPhase);
            Assert.IsTrue(taxi.IsAtStop(_world.Alternate));
        }

        [Test]
        public void RejectedDestinationChange_PreservesRequestFareAndOriginalArrival()
        {
            var taxi = _world.Taxi();
            using var ride = StartBoardedRide(taxi);
            var request = ride.ActiveRequest;
            _world.Alternate.enabled = false;
            Assert.IsFalse(ride.TryChangeDestination(_world.Alternate));
            Assert.AreSame(request, ride.ActiveRequest);
            Assert.IsFalse(ride.TryChangeDestination((TaxiStopAnchor)null));
            Assert.AreSame(request, ride.ActiveRequest);
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(RidePhase.ArrivedAtDestination, ride.CurrentPhase);
            Assert.AreSame(_world.Destination, taxi.ParkedStop);
        }

        [Test]
        public void OccupiedDestinationBay_RejectsRetargetWithoutReplacingOriginalRequest()
        {
            var taxi = _world.Taxi();
            using var ride = StartBoardedRide(taxi);
            var request = ride.ActiveRequest;
            var otherTaxi = new object();
            Assert.IsTrue(_world.Alternate.TryReserve(otherTaxi));
            Assert.IsFalse(ride.TryChangeDestination(_world.Alternate));
            Assert.AreSame(request, ride.ActiveRequest);
            Assert.AreSame(_world.Destination, ride.ActiveRequest.DestinationStop);
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(RidePhase.ArrivedAtDestination, ride.CurrentPhase);
            Assert.AreSame(_world.Destination, taxi.ParkedStop);
            _world.Alternate.Release(otherTaxi);
        }

        [Test]
        public void DisabledHistoricalPickup_AfterPhysicalDeparture_DoesNotInvalidateFareOrSettlement()
        {
            var taxi = _world.Taxi();
            var player = new PlayerStateService();
            using var ride = StartBoardedRide(taxi, player);
            var request = ride.ActiveRequest;
            float quotedDistance = request.PlannedDistance;
            int quotedFare = request.QuotedBaseFare;
            int creditsBefore = player.Credits;
            for (int i = 0; i < 5000; i++)
            {
                taxi.TickMovement(0.02f);
                if (!taxi.IsCurbManeuvering && _world.Ground(taxi).x > 40.001f) break;
            }
            Assert.IsFalse(taxi.IsCurbManeuvering);
            Assert.Greater(_world.Ground(taxi).x, 40.001f);
            Assert.IsTrue(_world.Pickup.IsAvailableFor(new object()), "The taxi must physically leave the historical pickup first.");
            _world.Pickup.enabled = false;
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(RidePhase.ArrivedAtDestination, ride.CurrentPhase);
            Assert.IsTrue(taxi.IsAtStop(_world.Destination));
            Assert.AreSame(request, ride.ActiveRequest);
            Assert.AreEqual(quotedDistance, ride.ActiveRequest.PlannedDistance);
            Assert.AreEqual(quotedFare, ride.ActiveRequest.QuotedBaseFare);
            RideSettlementResult? settlement = null;
            ride.OnRideCompleted += result => settlement = result;
            Assert.IsTrue(ride.CompleteAndSettleRide());
            Assert.IsTrue(settlement.HasValue);
            Assert.AreEqual(quotedFare, settlement.Value.BaseFarePaid);
            int expectedTip = Mathf.RoundToInt(quotedFare * _world.Passenger.TipGenerosity);
            Assert.AreEqual(expectedTip, settlement.Value.TipPaid);
            Assert.AreEqual(creditsBefore + quotedFare + expectedTip, player.Credits);
            Assert.AreEqual(RidePhase.Idle, ride.CurrentPhase);
        }

        [Test]
        public void PickupDisabledWhileAwaitingBoarding_RejectsBoardingAndCancelsLogicalRide()
        {
            var taxi = _world.Taxi();
            var player = new PlayerStateService();
            using var ride = Service(taxi, player);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.AreEqual(RidePhase.AwaitingBoarding, ride.CurrentPhase);
            var request = ride.ActiveRequest;
            int cancellations = 0;
            int boardings = 0;
            ride.OnRideCancelled += cancelled => { Assert.AreSame(request, cancelled); cancellations++; };
            ride.OnPassengerBoarded += _ => boardings++;
            int creditsBefore = player.Credits;
            Vector2 parkedPosition = _world.Ground(taxi);
            _world.Pickup.enabled = false;

            Assert.IsFalse(ride.BoardPassenger());
            Assert.AreEqual(RidePhase.Idle, ride.CurrentPhase, "Invalid pickup authoring must release the logical ride for recovery.");
            Assert.IsNull(ride.ActiveRequest);
            Assert.AreEqual(1, cancellations);
            Assert.AreEqual(0, boardings);
            Assert.AreEqual(creditsBefore, player.Credits);
            Assert.Less(Vector2.Distance(parkedPosition, _world.Ground(taxi)), 0.001f);
        }

        [Test]
        public void NodeOverload_RequiresRegistryResolution_AndFailsClosedWithoutIt()
        {
            var taxi = _world.Taxi();
            using var noRegistry = new RideLifecycleService(_world.Network, taxi, new PlayerStateService());
            Assert.IsFalse(noRegistry.TryOfferHail(_world.Passenger, _world.Pickup.ApproachNode, _world.Destination.ApproachNode));
            Assert.AreEqual(RidePhase.Idle, noRegistry.CurrentPhase);
            using var resolved = Service(taxi);
            Assert.IsTrue(resolved.TryOfferHail(_world.Passenger, _world.Pickup.ApproachNode, _world.Destination.ApproachNode));
            Assert.AreSame(_world.Pickup, resolved.ActiveRequest.PickupStop);
            Assert.AreSame(_world.Destination, resolved.ActiveRequest.DestinationStop);
        }

        private RideLifecycleService StartBoardedRide(VehicleController taxi, PlayerStateService player = null)
        {
            var ride = Service(taxi, player);
            Assert.IsTrue(ride.TryOfferHail(_world.Passenger, _world.Pickup, _world.Destination));
            Assert.IsTrue(ride.AcceptHail());
            TaxiStopTestFixture.DriveUntilStopped(taxi);
            Assert.IsTrue(ride.BoardPassenger());
            return ride;
        }

        private RideLifecycleService Service(VehicleController taxi, PlayerStateService player = null) =>
            new RideLifecycleService(_world.Network, taxi, player ?? new PlayerStateService(), _world.Time, _world.Registry);
    }
}
