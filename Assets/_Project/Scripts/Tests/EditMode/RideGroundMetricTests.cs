using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Rides;

namespace OutsiderDriver.Tests.EditMode
{
    public class RideGroundMetricTests
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
        public void GroundMetreQuote_IsIndependentOfIsometricWorldProjection()
        {
            var passenger = CreatePassenger();
            var wide = CreateNetwork(new Vector3(1f, 0.5f, 1f), "a");
            var tall = CreateNetwork(new Vector3(2f, 1f, 1f), "b");

            Assert.IsTrue(RideFareCalculator.TryQuote(wide.Network, passenger, wide.Pickup, wide.Dropoff, null, out var wideQuote));
            Assert.IsTrue(RideFareCalculator.TryQuote(tall.Network, passenger, tall.Pickup, tall.Dropoff, null, out var tallQuote));

            Assert.AreEqual(15f, wideQuote.DistanceMeters, 0.001f, "Cells 10 -> 25 are 15 logical ground metres.");
            Assert.AreEqual(45, wideQuote.QuotedFare, "Default fare is 15 + 15 m * 2 credits.");
            Assert.AreEqual(wideQuote.DistanceMeters, tallQuote.DistanceMeters, 0.001f, "Distance must not change with projection.");
            Assert.AreEqual(wideQuote.QuotedFare, tallQuote.QuotedFare, "Fare must not change with projection.");

            float wideWorldDistance = Vector3.Distance(wide.Pickup.Position, wide.Dropoff.Position);
            float tallWorldDistance = Vector3.Distance(tall.Pickup.Position, tall.Dropoff.Position);
            Assert.Greater(tallWorldDistance, wideWorldDistance + 1f, "The test grids must actually project to different world distances.");
        }

        [Test]
        public void GroundMetreQuote_UsesDistrictFarePolicyOnTheSameDistance()
        {
            var passenger = CreatePassenger();
            var network = CreateNetwork(new Vector3(1f, 0.5f, 1f), "district");
            var district = CreateDistrict(baseFare: 20, perUnit: 2.0f);

            Assert.IsTrue(RideFareCalculator.TryQuote(network.Network, passenger, network.Pickup, network.Dropoff, district, out var quote));

            Assert.AreEqual(15f, quote.DistanceMeters, 0.001f);
            Assert.AreEqual(50, quote.QuotedFare, "District fare is 20 + 15 m * 2 credits.");
            Assert.AreEqual(district.CalculateEstimatedFare(quote.DistanceMeters, passenger.FareMultiplier), quote.QuotedFare);
        }

        [Test]
        public void GroundMetreQuote_RejectsIdenticalAndDisconnectedNodes()
        {
            var passenger = CreatePassenger();
            var network = CreateNetwork(new Vector3(1f, 0.5f, 1f), "invalid");
            var isolated = CreateNode(network.Network.transform, network.Grid, "node_isolated", 20, 10);
            network.Network.RebuildNetwork();

            Assert.IsFalse(RideFareCalculator.TryQuote(network.Network, passenger, network.Pickup, network.Pickup, null, out _));
            Assert.IsFalse(RideFareCalculator.TryQuote(network.Network, passenger, network.Pickup, isolated, null, out _));
        }

        private MetricNetwork CreateNetwork(Vector3 cellSize, string suffix)
        {
            var gridGo = Track(new GameObject("Grid_" + suffix));
            var grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            grid.cellSize = cellSize;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            var networkGo = Track(new GameObject("Network_" + suffix));
            var network = networkGo.AddComponent<RoadNetwork>();
            network.ConfigureGrid(grid);

            var pickup = CreateNode(networkGo.transform, grid, "node_pickup_" + suffix, 10, 0);
            var dropoff = CreateNode(networkGo.transform, grid, "node_dropoff_" + suffix, 25, 0);
            var depot = CreateNode(networkGo.transform, grid, "node_depot_" + suffix, 0, 0);
            pickup.AddConnection(dropoff);
            dropoff.AddConnection(pickup);
            pickup.AddConnection(depot);
            depot.AddConnection(pickup);
            network.RebuildNetwork();

            return new MetricNetwork(grid, network, pickup, dropoff);
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

        private PassengerDefinitionSO CreatePassenger()
        {
            var passenger = ScriptableObject.CreateInstance<PassengerDefinitionSO>();
            passenger.Initialize("metric_resident", "metric_resident", "species_tbd", "Common", 1f, 0f);
            _spawnedObjects.Add(passenger);
            return passenger;
        }

        private DistrictConfigSO CreateDistrict(int baseFare, float perUnit)
        {
            var district = ScriptableObject.CreateInstance<DistrictConfigSO>();
            district.Initialize("district_metric", "district_metric", 4, baseFare, perUnit);
            _spawnedObjects.Add(district);
            return district;
        }

        private T Track<T>(T obj) where T : Object
        {
            _spawnedObjects.Add(obj);
            return obj;
        }

        private sealed class MetricNetwork
        {
            public readonly Grid Grid;
            public readonly RoadNetwork Network;
            public readonly RoadNode Pickup;
            public readonly RoadNode Dropoff;

            public MetricNetwork(Grid grid, RoadNetwork network, RoadNode pickup, RoadNode dropoff)
            {
                Grid = grid;
                Network = network;
                Pickup = pickup;
                Dropoff = dropoff;
            }
        }
    }
}
