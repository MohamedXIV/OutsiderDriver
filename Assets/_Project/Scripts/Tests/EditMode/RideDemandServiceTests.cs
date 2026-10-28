using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Rides;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Tests.EditMode
{
    public class RideDemandServiceTests
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
        }

        [Test]
        public void EligibleOffers_VaryByWorldHour_AndUseReachableDistinctLocations()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 0));

            var service = CreateService(world, seedOverride: 7);
            var morningOffers = service.GetEligibleOffers();
            Assert.GreaterOrEqual(morningOffers.Count, 1);
            AssertValidOffers(world, morningOffers);

            var morningPassengers = CollectPassengerIds(morningOffers);
            Assert.AreEqual(1, morningPassengers.Count, "At 09:00 only resident_001's authored window is active.");

            world.Time.SetTime(new WorldTime(day: 1, hour: 19, minute: 0));
            var eveningOffers = service.GetEligibleOffers();
            Assert.GreaterOrEqual(eveningOffers.Count, 1);
            AssertValidOffers(world, eveningOffers);

            var eveningPassengers = CollectPassengerIds(eveningOffers);
            Assert.GreaterOrEqual(eveningPassengers.Count, 2, "At 19:00 two different authored residents are available.");
            Assert.IsFalse(morningPassengers.SetEquals(eveningPassengers), "Available work must change with world time.");

            world.Time.SetTime(new WorldTime(day: 2, hour: 3, minute: 0));
            Assert.IsEmpty(service.GetEligibleOffers(), "No eligible authored work at 03:00.");
            Assert.IsFalse(service.TryEnsureOffer(out _));
            Assert.IsNull(service.CurrentOffer);
        }

        [Test]
        public void OfferBoard_KeepsStableIdentitiesAcrossEnumerationAndRefresh()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 19, minute: 0));
            var service = CreateService(world, seedOverride: 7);

            var first = service.GetEligibleOffers();
            Assert.AreEqual(2, first.Count, "The evening board exposes two distinct residents.");
            var firstIds = CollectOfferIds(first);

            var second = service.GetEligibleOffers();
            CollectionAssert.AreEqual(firstIds, CollectOfferIds(second), "Repeated enumeration must not mint new offer identities.");

            service.RefreshOffers();
            CollectionAssert.AreEqual(firstIds, CollectOfferIds(service.GetEligibleOffers()), "Refreshing a populated board must not replace displayed offers.");
        }

        [Test]
        public void ReserveRelease_RevivesTheExactOffer_AndOnlyOneReservationIsAllowed()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 19, minute: 0));
            var service = CreateService(world, seedOverride: 7);

            var board = service.GetEligibleOffers();
            Assert.AreEqual(2, board.Count);
            var chosen = board[0];
            var other = board[1];

            Assert.IsTrue(service.TryReserveOffer(chosen));
            Assert.IsFalse(service.TryReserveOffer(chosen), "The same displayed offer must not be reserved twice.");
            Assert.IsFalse(service.TryReserveOffer(other), "Only one offer may be reserved at a time.");
            Assert.IsFalse(ContainsOfferId(service.GetEligibleOffers(), chosen.OfferId));
            Assert.IsFalse(service.ReleaseReservedOffer(other), "A different offer must not release the reservation.");

            Assert.IsTrue(service.ReleaseReservedOffer(chosen));
            var restored = service.GetEligibleOffers();
            Assert.IsTrue(ContainsOfferId(restored, chosen.OfferId), "A failed activation must restore the exact presented offer.");
            Assert.IsTrue(ContainsOfferId(restored, other.OfferId));
        }

        [Test]
        public void ConsumedOffer_CannotBeAcceptedOrReservedTwice()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 19, minute: 0));
            var service = CreateService(world, seedOverride: 7);

            var board = service.GetEligibleOffers();
            var chosen = board[0];
            var other = board[1];

            Assert.IsTrue(service.TryReserveOffer(chosen));
            Assert.IsTrue(service.ConsumeReservedOffer(chosen));
            Assert.IsFalse(service.ConsumeReservedOffer(chosen));
            Assert.IsFalse(service.TryReserveOffer(chosen));
            Assert.IsFalse(service.AcceptOffer(chosen), "A consumed offer must never start a second ride.");
            Assert.IsTrue(ContainsOfferId(service.GetEligibleOffers(), other.OfferId));
        }

        [Test]
        public void ExpiredOffer_IsNotSelectable_AndTheBoardReplenishesOnlyAfterCooldown()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 0));
            var service = CreateService(world, seedOverride: 7);

            Assert.IsTrue(service.TryEnsureOffer(out var ignored));
            string ignoredId = ignored.OfferId;

            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 13));
            Assert.IsTrue(service.ExpireIfNeeded());
            Assert.IsNull(service.CurrentOffer);
            Assert.IsFalse(service.ExpireIfNeeded());
            Assert.IsEmpty(service.GetEligibleOffers());
            Assert.IsFalse(service.TryReserveOffer(ignored), "An expired displayed offer must not be selectable.");
            Assert.IsFalse(service.AcceptOffer(ignored));

            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 34));
            Assert.IsTrue(service.TryEnsureOffer(out var replacement), "After expiry cooldown, ordinary work replenishes.");
            Assert.AreNotEqual(ignoredId, replacement.OfferId);
            Assert.AreEqual(ignored.Passenger.PassengerId, replacement.Passenger.PassengerId);
        }

        [Test]
        public void Decline_SuppressesThePassengerAndReplenishesAfterCooldown()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 0));
            var service = CreateService(world, seedOverride: 7);

            Assert.IsTrue(service.TryEnsureOffer(out var declined));
            Assert.IsTrue(service.DeclineOffer(declined));
            Assert.IsFalse(service.DeclineOffer(declined), "A declined offer must leave the board exactly once.");
            Assert.IsNull(service.CurrentOffer);
            Assert.IsFalse(service.TryEnsureOffer(out _), "A declined passenger must not immediately reappear.");
            Assert.IsFalse(service.TryGenerateOffer(out _), "Even a forced refresh must respect the decline cooldown.");

            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 21));
            Assert.IsTrue(service.TryEnsureOffer(out var replenished), "After decline cooldown, work replenishes.");
            Assert.AreEqual(declined.Passenger.PassengerId, replenished.Passenger.PassengerId);
        }

        [Test]
        public void DecliningOneOffer_KeepsTheRestOfTheBoardSelectable()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 19, minute: 0));
            var service = CreateService(world, seedOverride: 7);

            var board = service.GetEligibleOffers();
            var declined = board[0];
            var remaining = board[1];

            Assert.IsTrue(service.DeclineOffer(declined));
            var afterDecline = service.GetEligibleOffers();
            Assert.AreEqual(1, afterDecline.Count);
            Assert.IsTrue(ContainsOfferId(afterDecline, remaining.OfferId));

            for (int i = 0; i < afterDecline.Count; i++)
            {
                Assert.AreNotEqual(declined.Passenger.PassengerId, afterDecline[i].Passenger.PassengerId);
            }

            Assert.IsTrue(service.TryReserveOffer(remaining));
            Assert.IsTrue(service.ConsumeReservedOffer(remaining));
        }

        [Test]
        public void AcceptOffer_AllowsTheNextOfferToBeGenerated()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 0));
            var service = CreateService(world, seedOverride: 7);

            Assert.IsTrue(service.TryEnsureOffer(out var first));
            Assert.IsTrue(service.AcceptOffer(first));
            Assert.IsNull(service.CurrentOffer);

            Assert.IsTrue(service.TryEnsureOffer(out var next));
            Assert.IsNotNull(next);
            Assert.IsTrue(service.DeclineOffer(next) || service.AcceptOffer(next));
        }

        [Test]
        public void SeededSelection_IsReproducible_ForTheSameWorldState()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 0));

            var serviceA = CreateService(world, seedOverride: 1234);
            var serviceB = CreateService(world, seedOverride: 1234);

            Assert.IsTrue(serviceA.TryEnsureOffer(out var offerA));
            Assert.IsTrue(serviceB.TryEnsureOffer(out var offerB));
            Assert.AreEqual(offerA.Key, offerB.Key);
            Assert.AreEqual(offerA.OfferId, offerB.OfferId);
        }

        [Test]
        public void ProductionScene_ProducesMultipleReachableOffersFromRealCityLocations()
        {
            const string scenePath = "Assets/_Project/Scenes/OutsiderCity.unity";
            const string configPath = "Assets/_Project/Data/Rides/RideDemandConfig_Default.asset";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid());

            var network = Object.FindAnyObjectByType<RoadNetwork>();
            var registry = Object.FindAnyObjectByType<CityWorldRegistry>();
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<RideDemandConfigSO>(configPath);
            Assert.IsNotNull(network);
            Assert.IsNotNull(registry);
            Assert.IsNotNull(config);
            Assert.GreaterOrEqual(config.Passengers.Count, 2);

            var time = new TimeService(null);
            time.SetTime(new WorldTime(day: 1, hour: 12, minute: 0));
            var service = new RideDemandService(network, registry, time, config, seedOverride: 7);

            var offers = service.GetEligibleOffers();
            Assert.GreaterOrEqual(offers.Count, 2, "The production scene must expose more than one possible opportunity.");
            AssertValidProductionOffers(network, offers);

            var passengerIds = CollectPassengerIds(offers);
            Assert.GreaterOrEqual(passengerIds.Count, 2, "More than one authored resident must be available at noon.");

            Assert.IsTrue(service.TryEnsureOffer(out var current));
            Assert.IsTrue(service.AcceptOffer(current));
            Assert.IsTrue(service.TryEnsureOffer(out var next));
            Assert.IsNotNull(next);
        }

        [Test]
        public void DifferentSeeds_CanSelectDifferentOrdinaryWork()
        {
            var world = CreateDemandWorld();
            world.Time.SetTime(new WorldTime(day: 1, hour: 9, minute: 0));

            var firstKeys = new HashSet<string>();
            for (int seed = 1; seed <= 8; seed++)
            {
                var service = CreateService(world, seedOverride: seed);
                Assert.IsTrue(service.TryEnsureOffer(out var offer));
                firstKeys.Add(offer.Key);
            }

            Assert.Greater(firstKeys.Count, 1, "Seeded selection must not collapse to one fixed job.");
        }

        [Test]
        public void ProductionScene_DemandContinuesAfterARealSettledRide()
        {
            const string scenePath = "Assets/_Project/Scenes/OutsiderCity.unity";
            const string configPath = "Assets/_Project/Data/Rides/RideDemandConfig_Default.asset";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid());

            var network = Object.FindAnyObjectByType<RoadNetwork>();
            var registry = Object.FindAnyObjectByType<CityWorldRegistry>();
            var taxi = Object.FindAnyObjectByType<VehicleController>();
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<RideDemandConfigSO>(configPath);
            Assert.IsNotNull(network);
            Assert.IsNotNull(registry);
            Assert.IsNotNull(taxi);
            Assert.IsNotNull(config);

            var time = new TimeService(null);
            time.SetTime(new WorldTime(day: 1, hour: 12, minute: 0));
            var demand = new RideDemandService(network, registry, time, config, seedOverride: 7);
            Assert.IsTrue(demand.TryEnsureOffer(out var offer));
            Assert.IsTrue(demand.AcceptOffer(offer));

            var playerState = new PlayerStateService();
            using var rideService = new RideLifecycleService(network, taxi, playerState);
            taxi.transform.position = offer.PickupNode.Position;
            Assert.IsTrue(rideService.TryOfferHail(offer.Passenger, offer.PickupNode, offer.DestinationNode, offer.District));
            Assert.AreEqual(offer.QuotedFare, rideService.ActiveRequest.QuotedBaseFare);
            Assert.IsTrue(rideService.AcceptHail());
            Assert.AreEqual(RidePhase.AwaitingBoarding, rideService.CurrentPhase);
            Assert.IsTrue(rideService.BoardPassenger());

            for (int i = 0; i < 5000 && taxi.HasActiveRoute; i++)
            {
                taxi.TickMovement(0.05f);
            }

            Assert.IsFalse(taxi.HasActiveRoute, "The settled production ride must complete on the real city graph.");
            Assert.AreEqual(RidePhase.ArrivedAtDestination, rideService.CurrentPhase);

            int creditsBefore = playerState.Credits;
            Assert.IsTrue(rideService.CompleteAndSettleRide());
            Assert.Greater(playerState.Credits, creditsBefore);

            Assert.IsTrue(demand.TryEnsureOffer(out var nextOffer));
            Assert.IsNotNull(nextOffer);
        }

        private static void AssertValidOffers(DemandWorld world, IReadOnlyList<RideOffer> offers)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                Assert.IsNotNull(offer.Passenger);
                Assert.IsNotNull(offer.PickupNode);
                Assert.IsNotNull(offer.DestinationNode);
                Assert.AreNotEqual(offer.PickupNode.NodeId, offer.DestinationNode.NodeId);
                Assert.IsFalse(string.IsNullOrEmpty(offer.DestinationLocationId));
                Assert.Greater(offer.PlannedDistanceMeters, 0f);
                Assert.Greater(offer.QuotedFare, 0);
                Assert.GreaterOrEqual(offer.PlannedDistanceMeters, world.Config.MinimumRideDistanceMeters);

                var path = world.Network.FindPath(offer.PickupNode, offer.DestinationNode);
                Assert.GreaterOrEqual(path.Count, 2, "Every generated offer must have a real connected route.");
            }
        }

        private static void AssertValidProductionOffers(RoadNetwork network, IReadOnlyList<RideOffer> offers)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                Assert.IsNotNull(offer.Passenger);
                Assert.IsNotNull(offer.PickupNode);
                Assert.IsNotNull(offer.DestinationNode);
                Assert.AreNotEqual(offer.PickupNode.NodeId, offer.DestinationNode.NodeId);
                Assert.IsFalse(string.IsNullOrEmpty(offer.DestinationLocationId));
                Assert.Greater(offer.PlannedDistanceMeters, 0f);
                Assert.Greater(offer.QuotedFare, 0);
                var path = network.FindPath(offer.PickupNode, offer.DestinationNode);
                Assert.GreaterOrEqual(path.Count, 2);
            }
        }

        private static HashSet<string> CollectPassengerIds(IReadOnlyList<RideOffer> offers)
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < offers.Count; i++)
            {
                ids.Add(offers[i].Passenger.PassengerId);
            }
            return ids;
        }

        private static List<string> CollectOfferIds(IReadOnlyList<RideOffer> offers)
        {
            var ids = new List<string>();
            for (int i = 0; i < offers.Count; i++)
            {
                ids.Add(offers[i].OfferId);
            }
            return ids;
        }

        private static bool ContainsOfferId(IReadOnlyList<RideOffer> offers, string offerId)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (string.Equals(offers[i].OfferId, offerId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private RideDemandService CreateService(DemandWorld world, int seedOverride)
        {
            return new RideDemandService(world.Network, world.Registry, world.Time, world.Config, seedOverride);
        }

        private DemandWorld CreateDemandWorld()
        {
            var gridGo = Track(new GameObject("DemandGrid"));
            var grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            var networkGo = Track(new GameObject("DemandNetwork"));
            var network = networkGo.AddComponent<RoadNetwork>();
            network.ConfigureGrid(grid);

            var nodeA = CreateNode(networkGo.transform, grid, "node_a", 0, 0);
            var nodeB = CreateNode(networkGo.transform, grid, "node_b", 40, 0);
            var nodeC = CreateNode(networkGo.transform, grid, "node_c", 0, 40);
            var nodeD = CreateNode(networkGo.transform, grid, "node_d", 40, 40);
            var nodeE = CreateNode(networkGo.transform, grid, "node_e", 80, 0);
            var nodeF = CreateNode(networkGo.transform, grid, "node_f", 80, 40);
            Connect(nodeA, nodeB);
            Connect(nodeA, nodeC);
            Connect(nodeB, nodeD);
            Connect(nodeC, nodeD);
            Connect(nodeB, nodeE);
            Connect(nodeD, nodeF);
            Connect(nodeE, nodeF);
            network.RebuildNetwork();

            var districtOne = CreateDistrict("district_01", 15, 2.0f);
            var districtTwo = CreateDistrict("district_02", 20, 2.5f);

            var registryGo = Track(new GameObject("DemandRegistry"));
            var registry = registryGo.AddComponent<CityWorldRegistry>();
            AddLocation(registryGo.transform, "location_d", "district_01", nodeD);
            AddLocation(registryGo.transform, "location_e", "district_02", nodeE);
            AddLocation(registryGo.transform, "location_f", "district_02", nodeF);
            registry.Configure(network, null, new[] { districtOne, districtTwo });

            var residentOne = CreatePassenger("resident_001", 1.0f, 0.2f, 6, 14, "district_01", nodeA.NodeId);
            var residentTwo = CreatePassenger("resident_002", 1.1f, 0.15f, 12, 22, "district_02", nodeB.NodeId);
            var residentThree = CreatePassenger("resident_003", 0.9f, 0.25f, 18, 24, "district_01", nodeC.NodeId);

            var config = Track(ScriptableObject.CreateInstance<RideDemandConfigSO>());
            config.Configure(
                new[] { residentOne, residentTwo, residentThree },
                minimumRideDistanceMeters: 25f,
                offerLifetimeMinutes: 12,
                declineCooldownMinutes: 20,
                randomSeed: 0,
                maxVisibleOffers: 2);

            var time = new TimeService(null);
            return new DemandWorld(network, registry, time, config);
        }

        private void AddLocation(Transform parent, string locationId, string districtId, RoadNode accessNode)
        {
            var location = Track(ScriptableObject.CreateInstance<WorldLocationSO>());
            location.Configure(
                locationId,
                districtId,
                WorldLocationCategory.CommercialMarket,
                locationId,
                locationId,
                locationId,
                accessNode.NodeId,
                floorCount: 1);

            var go = Track(new GameObject("Anchor_" + locationId));
            go.transform.SetParent(parent, false);
            var anchor = go.AddComponent<CityLocationAnchor>();
            anchor.Configure(location, accessNode, Vector2Int.zero, Vector2Int.one, locationId);
        }

        private PassengerDefinitionSO CreatePassenger(
            string id,
            float fareMultiplier,
            float tipGenerosity,
            int startHour,
            int endHour,
            string districtId,
            string preferredNodeId)
        {
            var passenger = Track(ScriptableObject.CreateInstance<PassengerDefinitionSO>());
            passenger.Initialize(id, id, "species_tbd", "Common", fareMultiplier, tipGenerosity);
            passenger.SetSchedule(new[]
            {
                new PassengerScheduleEntry(startHour, endHour, districtId, preferredNodeId)
            });
            return passenger;
        }

        private DistrictConfigSO CreateDistrict(string id, int baseFare, float perUnit)
        {
            var district = Track(ScriptableObject.CreateInstance<DistrictConfigSO>());
            district.Initialize(id, id, 4, baseFare, perUnit);
            return district;
        }

        private RoadNode CreateNode(Transform parent, Grid grid, string id, int cellX, int cellY)
        {
            var go = Track(new GameObject(id));
            go.transform.SetParent(parent, false);
            go.transform.position = grid.CellToWorld(new Vector3Int(cellX, cellY, 0));
            var node = go.AddComponent<RoadNode>();
            node.Configure(id, RoadNodePriority.MainArtery, RoadPieceType.Straight, "district_01");
            return node;
        }

        private static void Connect(RoadNode a, RoadNode b)
        {
            a.AddConnection(b);
            b.AddConnection(a);
        }

        private T Track<T>(T obj) where T : Object
        {
            _spawnedObjects.Add(obj);
            return obj;
        }

        private sealed class DemandWorld
        {
            public readonly RoadNetwork Network;
            public readonly CityWorldRegistry Registry;
            public readonly TimeService Time;
            public readonly RideDemandConfigSO Config;

            public DemandWorld(RoadNetwork network, CityWorldRegistry registry, TimeService time, RideDemandConfigSO config)
            {
                Network = network;
                Registry = registry;
                Time = time;
                Config = config;
            }
        }
    }
}
