using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Rides;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    public class RideLifecycleServiceTests
    {
        private readonly List<Object> _spawnedObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                if (_spawnedObjects[i] != null)
                {
                    Object.DestroyImmediate(_spawnedObjects[i]);
                }
            }
            _spawnedObjects.Clear();
            VehicleController.ClearActiveVehicles();
        }

        [Test]
        public void FullRideLifecycle_HailPickupBoardTransitArriveAndSettle_CreditsPlayerAndRecordsEncounter()
        {
            var network = CreateLinearRoadNetwork(out RoadNode nodeStart, out RoadNode nodePickup, out RoadNode nodeDropoff);
            var taxi = CreateTaxi(nodeStart.Position, network);
            var timeService = new TimeService(null);
            timeService.SetTime(new WorldTime(day: 2, hour: 14, minute: 30));
            var playerState = new PlayerStateService(timeService);

            var passenger = CreatePassenger("res_vori", "Vori", fareMultiplier: 1.0f, tipGenerosity: 0.2f);
            var district = CreateDistrict("district_central", baseFare: 20, perUnit: 2.0f);

            using var rideService = new RideLifecycleService(network, taxi, playerState, timeService);

            // 1. Offer Hail (Pickup at (4,0,0) -> Dropoff at (10,0,0), distance = 6m)
            // Expected Quoted Fare: 20 + (6 * 2.0) = 32
            Assert.AreEqual(RidePhase.Idle, rideService.CurrentPhase);
            bool offered = rideService.TryOfferHail(passenger, nodePickup, nodeDropoff, district);
            Assert.IsTrue(offered);
            Assert.AreEqual(RidePhase.HailOffered, rideService.CurrentPhase);
            Assert.AreEqual(32, rideService.ActiveRequest.QuotedBaseFare);

            // 2. Accept Hail -> Taxi starts at (0,0,0), so transitions to EnRouteToPickup
            Assert.IsTrue(rideService.AcceptHail());
            Assert.AreEqual(RidePhase.EnRouteToPickup, rideService.CurrentPhase);
            Assert.AreEqual(nodePickup, taxi.DestinationNode);

            // Simulate taxi driving to nodePickup
            SimulateUntilStopped(taxi);
            Assert.AreEqual(RidePhase.AwaitingBoarding, rideService.CurrentPhase);

            // 3. Board Passenger -> Transitions to InTransit toward nodeDropoff
            Assert.IsTrue(rideService.BoardPassenger());
            Assert.AreEqual(RidePhase.InTransit, rideService.CurrentPhase);
            Assert.AreEqual(nodeDropoff, taxi.DestinationNode);

            // Simulate taxi driving to nodeDropoff
            SimulateUntilStopped(taxi);
            Assert.AreEqual(RidePhase.ArrivedAtDestination, rideService.CurrentPhase);

            // 4. Complete and Settle Ride
            // Base fare = 32, default tip (20% of 32 = 6.4 -> 6) + bonus tip 4 = 10 -> Total = 42
            int initialCredits = playerState.Credits;
            RideSettlementResult? completedResult = null;
            rideService.OnRideCompleted += res => completedResult = res;

            bool settled = rideService.CompleteAndSettleRide(bonusTipCredits: 4, affinityDelta: 12, trustDelta: 8, suspicionDelta: 3);
            Assert.IsTrue(settled);
            Assert.AreEqual(RidePhase.Idle, rideService.CurrentPhase);
            Assert.IsNull(rideService.ActiveRequest);

            Assert.IsTrue(completedResult.HasValue);
            Assert.AreEqual(32, completedResult.Value.BaseFarePaid);
            Assert.AreEqual(10, completedResult.Value.TipPaid);
            Assert.AreEqual(42, completedResult.Value.TotalCreditsEarned);
            Assert.AreEqual(initialCredits + 42, playerState.Credits);

            var relationship = playerState.GetOrAddResident("res_vori");
            Assert.AreEqual(1, relationship.EncounterCount);
            Assert.AreEqual(12, relationship.Affinity);
            Assert.AreEqual(8, relationship.PersonalTrust);
            Assert.AreEqual(3, relationship.SuspicionOfHumanity);
            Assert.AreEqual(timeService.CurrentTime.TotalMinutes, relationship.LastEncounterTotalMinutes);
        }

        [Test]
        public void AcceptHail_WhenAlreadyAtPickupNode_TransitionsImmediatelyToAwaitingBoarding()
        {
            var network = CreateLinearRoadNetwork(out RoadNode nodeStart, out RoadNode nodePickup, out RoadNode nodeDropoff);
            var taxi = CreateTaxi(nodePickup.Position, network);
            var playerState = new PlayerStateService();
            var passenger = CreatePassenger("res_kael", "Kael", fareMultiplier: 1.0f, tipGenerosity: 0f);

            using var rideService = new RideLifecycleService(network, taxi, playerState);

            Assert.IsTrue(rideService.TryOfferHail(passenger, nodePickup, nodeDropoff));
            Assert.IsTrue(rideService.AcceptHail());

            Assert.AreEqual(RidePhase.AwaitingBoarding, rideService.CurrentPhase, "Taxi already at pickup node should skip EnRouteToPickup.");
        }

        [Test]
        public void TryChangeDestination_DuringTransit_UpdatesRouteAndFare()
        {
            var network = CreateLinearRoadNetwork(out RoadNode nodeStart, out RoadNode nodePickup, out RoadNode nodeDropoff);
            var taxi = CreateTaxi(nodePickup.Position, network);
            var playerState = new PlayerStateService();
            var passenger = CreatePassenger("res_zora", "Zora", fareMultiplier: 1.0f, tipGenerosity: 0f);

            using var rideService = new RideLifecycleService(network, taxi, playerState);

            Assert.IsTrue(rideService.TryOfferHail(passenger, nodePickup, nodeStart));
            Assert.IsTrue(rideService.AcceptHail());
            Assert.IsTrue(rideService.BoardPassenger());
            Assert.AreEqual(RidePhase.InTransit, rideService.CurrentPhase);

            int originalFare = rideService.ActiveRequest.QuotedBaseFare;

            // Change destination mid-ride to nodeDropoff
            bool changed = rideService.TryChangeDestination(nodeDropoff);
            Assert.IsTrue(changed);
            Assert.AreEqual(nodeDropoff, rideService.ActiveRequest.DestinationNode);
            Assert.AreEqual(nodeDropoff, taxi.DestinationNode);
            Assert.Greater(rideService.ActiveRequest.QuotedBaseFare, originalFare);
        }

        private static void SimulateUntilStopped(VehicleController taxi, int maxSteps = 200)
        {
            for (int i = 0; i < maxSteps && taxi.HasActiveRoute; i++)
            {
                taxi.TickMovement(0.05f);
            }
        }

        private RoadNetwork CreateLinearRoadNetwork(out RoadNode nodeA, out RoadNode nodeB, out RoadNode nodeC)
        {
            var networkGo = CreateTrackedObject("RoadNetwork", Vector3.zero);
            var network = networkGo.AddComponent<RoadNetwork>();

            nodeA = CreateNode(networkGo.transform, "NodeA", new Vector3(0f, 0f, 0f));
            nodeB = CreateNode(networkGo.transform, "NodeB", new Vector3(4f, 0f, 0f));
            nodeC = CreateNode(networkGo.transform, "NodeC", new Vector3(10f, 0f, 0f));

            nodeA.AddConnection(nodeB);
            nodeB.AddConnection(nodeA);
            nodeB.AddConnection(nodeC);
            nodeC.AddConnection(nodeB);

            network.RebuildNetwork();
            return network;
        }

        private GameObject CreateTrackedObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawnedObjects.Add(go);
            return go;
        }

        private RoadNode CreateNode(Transform parent, string name, Vector3 position)
        {
            var go = CreateTrackedObject(name, position);
            go.transform.SetParent(parent);
            return go.AddComponent<RoadNode>();
        }

        private VehicleController CreateTaxi(Vector3 position, RoadNetwork network)
        {
            var go = CreateTrackedObject("PlayerTaxi", position);
            var controller = go.AddComponent<VehicleController>();
            controller.Construct(network);
            VehicleController.RegisterActiveVehicle(controller);
            return controller;
        }

        private PassengerDefinitionSO CreatePassenger(string id, string name, float fareMultiplier, float tipGenerosity)
        {
            var so = ScriptableObject.CreateInstance<PassengerDefinitionSO>();
            so.Initialize(id, name, "Vesperian", "Common", fareMultiplier, tipGenerosity);
            _spawnedObjects.Add(so);
            return so;
        }

        private DistrictConfigSO CreateDistrict(string id, int baseFare, float perUnit)
        {
            var so = ScriptableObject.CreateInstance<DistrictConfigSO>();
            so.Initialize(id, id, targetAmbientVehicles: 4, basePickupFare: baseFare, creditsPerUnitDistance: perUnit);
            _spawnedObjects.Add(so);
            return so;
        }
    }
}
