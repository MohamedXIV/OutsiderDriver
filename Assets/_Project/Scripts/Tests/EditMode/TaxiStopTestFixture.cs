using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Tests.EditMode
{
    /// <summary>
    /// Test-only, explicitly authored metre geometry. This is not a world generator or scene authoring tool.
    /// Three eastbound bays sit outside a directed through lane; the upper return road closes the loop.
    /// </summary>
    internal sealed class TaxiStopTestFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        public Grid Grid { get; }
        public RoadNetwork Network { get; }
        public CityWorldRegistry Registry { get; }
        public TimeService Time { get; }
        public RideDemandConfigSO Config { get; }
        public TaxiStopAnchor Pickup { get; }
        public TaxiStopAnchor Destination { get; }
        public TaxiStopAnchor Alternate { get; }
        public PassengerDefinitionSO Passenger { get; }
        public DistrictConfigSO District { get; }
        public RoadNode Start { get; }
        public RoadNode ReturnJunction { get; }

        public TaxiStopTestFixture(Vector3? cellSize = null, bool authorStops = true)
        {
            Grid = Track(new GameObject("AuthoredTestGrid")).AddComponent<Grid>();
            Grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            Grid.cellSize = cellSize ?? new Vector3(1f, 0.5f, 1f);
            Grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
            Network = Track(new GameObject("AuthoredTestNetwork")).AddComponent<RoadNetwork>();
            Network.ConfigureGrid(Grid);

            Start = Node("start", 0, 0);
            var aIn = Node("pickup_access", 20, 0);
            var aOut = Node("pickup_departure", 40, 0);
            var bFrom = Node("destination_from", 60, 0);
            var bIn = Node("destination_access", 80, 0);
            var bOut = Node("destination_departure", 100, 0);
            var cFrom = Node("alternate_from", 120, 0);
            var cIn = Node("alternate_access", 140, 0);
            var cOut = Node("alternate_departure", 160, 0);
            var cNext = Node("alternate_next", 180, 0);
            var east = Node("east_corner", 200, 0);
            ReturnJunction = Node("return_junction", 200, 60);
            ReturnJunction.PieceType = RoadPieceType.TJunction;
            var west = Node("west_corner", 0, 60);
            Connect(Start, aIn); Connect(aIn, aOut); Connect(aOut, bFrom); Connect(bFrom, bIn);
            Connect(bIn, bOut); Connect(bOut, cFrom); Connect(cFrom, cIn); Connect(cIn, cOut);
            Connect(cOut, cNext); Connect(cNext, east); Connect(east, ReturnJunction);
            Connect(ReturnJunction, west); Connect(west, Start);
            Network.RebuildNetwork();

            District = Track(ScriptableObject.CreateInstance<DistrictConfigSO>());
            District.Initialize("district_01", "district_01", 4, 15, 2f);
            Registry = Track(new GameObject("AuthoredTestRegistry")).AddComponent<CityWorldRegistry>();
            var pickupLocation = Location("location_a", aIn);
            var destinationLocation = Location("location_b", bIn);
            var alternateLocation = Location("location_c", cIn);
            if (authorStops)
            {
                Pickup = Bay("stop_a", pickupLocation, Start, aIn, aOut, bFrom,
                    new Vector2(20, -1.5f), new Vector2(24, -1.5f), new Vector2(26, -5), new Vector2(30, -5),
                    new Vector2(34, -5), new Vector2(36, -1.5f), new Vector2(40, -1.5f), new Vector2(31, -5));
                Destination = Bay("stop_b", destinationLocation, bFrom, bIn, bOut, cFrom,
                    new Vector2(80, -1.5f), new Vector2(84, -1.5f), new Vector2(86, -5), new Vector2(90, -5),
                    new Vector2(94, -5), new Vector2(96, -1.5f), new Vector2(100, -1.5f), new Vector2(91, -5));
                Alternate = Bay("stop_c", alternateLocation, cFrom, cIn, cOut, cNext,
                    new Vector2(140, -1.5f), new Vector2(144, -1.5f), new Vector2(146, -5), new Vector2(150, -5),
                    new Vector2(154, -5), new Vector2(156, -1.5f), new Vector2(160, -1.5f), new Vector2(151, -5));
            }
            Registry.Configure(Network, null, new[] { District });
            if (authorStops)
            {
                AssertPrepared(Pickup); AssertPrepared(Destination); AssertPrepared(Alternate);
            }
            Passenger = Resident("resident_001", 6, 14, aIn.NodeId);
            var second = Resident("resident_002", 12, 22, bIn.NodeId);
            var third = Resident("resident_003", 18, 24, cIn.NodeId);
            Config = Track(ScriptableObject.CreateInstance<RideDemandConfigSO>());
            Config.Configure(new[] { Passenger, second, third }, minimumRideDistanceMeters: 25f,
                offerLifetimeMinutes: 12, declineCooldownMinutes: 20, randomSeed: 0, maxVisibleOffers: 2);
            Time = new TimeService(null);
        }

        public Vector3 World(Vector2 ground)
        {
            Vector2 point = Network.GroundProjection.GroundToWorld(ground);
            return new Vector3(point.x, point.y, 0f);
        }

        public Vector2 Ground(VehicleController taxi) => Network.GroundProjection.WorldToGround(taxi.transform.position);

        public VehicleController Taxi(Vector2? ground = null)
        {
            var go = Track(new GameObject("TestTaxi"));
            go.transform.position = World(ground ?? new Vector2(0f, -1.5f));
            var taxi = go.AddComponent<VehicleController>();
            taxi.Construct(Network);
            VehicleController.RegisterActiveVehicle(taxi);
            return taxi;
        }

        public RoadNode Node(string id, int x, int y)
        {
            var go = Track(new GameObject(id));
            go.transform.SetParent(Network.transform, false);
            go.transform.position = Grid.CellToWorld(new Vector3Int(x, y, 0));
            var node = go.AddComponent<RoadNode>();
            node.Configure(id, RoadNodePriority.MainArtery, RoadPieceType.Straight, "district_01", laneOffsetMeters: 1.5f);
            return node;
        }

        public Transform Point(string id, Vector2 ground, Transform parent = null)
        {
            var go = Track(new GameObject(id));
            go.transform.SetParent(parent ?? Registry.transform, false);
            go.transform.position = World(ground);
            return go.transform;
        }

        public static void DriveUntilStopped(VehicleController taxi, int maxSteps = 20000)
        {
            for (int i = 0; i < maxSteps && (taxi.HasActiveRoute || taxi.HasPendingStopMotion); i++)
                taxi.TickMovement(0.05f);
            Assert.IsFalse(taxi.HasActiveRoute || taxi.HasPendingStopMotion, "Taxi must finish the authored route and curb manoeuvre.");
        }

        public static void AssertPrepared(TaxiStopAnchor stop)
        {
            var registry = stop.GetComponentInParent<CityWorldRegistry>();
            Assert.IsTrue(stop.Validate(registry.RoadNetwork, 4.5f, 2f, out var diagnostic), diagnostic);
        }

        public T Track<T>(T value) where T : UnityEngine.Object { _objects.Add(value); return value; }

        public void Dispose()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            VehicleController.ClearActiveVehicles();
        }

        private CityLocationAnchor Location(string id, RoadNode access)
        {
            var data = Track(ScriptableObject.CreateInstance<WorldLocationSO>());
            data.Configure(id, "district_01", WorldLocationCategory.CommercialMarket, id, id, id, access.NodeId, floorCount: 1);
            var anchor = Point(id, Network.GroundProjection.WorldToGround(access.Position)).gameObject.AddComponent<CityLocationAnchor>();
            anchor.Configure(data, access, Vector2Int.zero, Vector2Int.one, id);
            return anchor;
        }

        private TaxiStopAnchor Bay(string id, CityLocationAnchor location, RoadNode from, RoadNode approach,
            RoadNode departure, RoadNode next, Vector2 entry, Vector2 h1, Vector2 h2, Vector2 pose,
            Vector2 outH1, Vector2 outH2, Vector2 exit, Vector2 heading)
        {
            var stop = Point(id, pose).gameObject.AddComponent<TaxiStopAnchor>();
            stop.Configure(id, location, from, approach, departure, next,
                Point("entry", entry, stop.transform), Point("in_h1", h1, stop.transform), Point("in_h2", h2, stop.transform),
                Point("stop_pose", pose, stop.transform), Point("out_h1", outH1, stop.transform),
                Point("out_h2", outH2, stop.transform), Point("exit", exit, stop.transform), Point("heading", heading, stop.transform));
            return stop;
        }

        private PassengerDefinitionSO Resident(string id, int start, int end, string nodeId)
        {
            var resident = Track(ScriptableObject.CreateInstance<PassengerDefinitionSO>());
            resident.Initialize(id, id, "species_tbd", "Common", 1f, 0.2f);
            resident.SetSchedule(new[] { new PassengerScheduleEntry(start, end, "district_01", nodeId) });
            return resident;
        }
        private static void Connect(RoadNode from, RoadNode to) => from.AddConnection(to);
    }
}
