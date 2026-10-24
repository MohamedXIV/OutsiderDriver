using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    public class RoadYieldingTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private Grid _grid;

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
        public void RoadNode_IntersectionReservation_BlocksSecondVehicleUntilReleased()
        {
            var nodeGo = CreateTrackedObject("IntersectionNode", Vector3.zero);
            var node = nodeGo.AddComponent<RoadNode>();
            node.Priority = RoadNodePriority.YieldIntersection;

            var carA = new object();
            var carB = new object();

            Assert.IsTrue(node.CanVehicleEnter(carA));
            Assert.IsTrue(node.TryReserveIntersection(carA));
            Assert.AreEqual(carA, node.OccupyingVehicle);

            // Second vehicle cannot enter or reserve while carA occupies it
            Assert.IsFalse(node.CanVehicleEnter(carB));
            Assert.IsFalse(node.TryReserveIntersection(carB));

            // Releasing from carB does nothing; releasing from carA frees the node
            node.ReleaseIntersection(carB);
            Assert.IsFalse(node.CanVehicleEnter(carB));

            node.ReleaseIntersection(carA);
            Assert.IsTrue(node.CanVehicleEnter(carB));
            Assert.IsTrue(node.TryReserveIntersection(carB));
        }

        [Test]
        public void VehicleController_YieldsWhenLeadVehicleIsWithinForwardSensorCone()
        {
            var network = CreateMetricNetwork();
            var nodeStart = CreateNode(network.transform, "Start", 0, 0);
            var nodeEnd = CreateNode(network.transform, "End", 10, 0);
            nodeStart.AddConnection(nodeEnd);
            nodeEnd.AddConnection(nodeStart);
            network.RebuildNetwork();

            var leadCar = CreateVehicle("LeadCar", CellWorld(2, 0), network);
            var followerCar = CreateVehicle("FollowerCar", CellWorld(0, 0), network);
            followerCar.ForwardSensorDistance = 3.0f;

            // Start follower navigating to nodeEnd
            Assert.IsTrue(followerCar.NavigateTo(nodeEnd));
            followerCar.TickMovement(0.05f); // Pass the start node
            followerCar.TickMovement(0.1f);  // Target nodeEnd, leadCar 2 m ahead

            Assert.IsTrue(followerCar.IsYielding, "Follower vehicle should yield when lead vehicle is within forward sensor range.");

            // Move lead vehicle far ahead out of sensor range
            leadCar.transform.position = CellWorld(8, 0);
            followerCar.TickMovement(0.1f);

            Assert.IsFalse(followerCar.IsYielding, "Follower vehicle should resume moving once lead car clears sensor range.");
            Assert.Greater(followerCar.CurrentSpeed, 0f);
        }

        [Test]
        public void VehicleController_YieldsAtOccupiedYieldIntersectionUntilCleared()
        {
            var network = CreateMetricNetwork();
            var nodeStart = CreateNode(network.transform, "Start", 0, 0);
            var nodeCrossing = CreateNode(network.transform, "Crossing", 8, 0);
            nodeCrossing.Priority = RoadNodePriority.YieldIntersection;
            nodeCrossing.IntersectionClearanceRadius = 10f;

            var nodeEnd = CreateNode(network.transform, "End", 20, 0);
            nodeStart.AddConnection(nodeCrossing);
            nodeCrossing.AddConnection(nodeEnd);
            nodeCrossing.AddConnection(nodeStart);
            nodeEnd.AddConnection(nodeCrossing);
            network.RebuildNetwork();

            // Another vehicle already occupies the crossing
            var crossTrafficToken = new object();
            nodeCrossing.TryReserveIntersection(crossTrafficToken);

            var approachingCar = CreateVehicle("ApproachingCar", CellWorld(0, 0), network);
            Assert.IsTrue(approachingCar.NavigateTo(nodeEnd));

            // First tick passes nodeStart and targets nodeCrossing 8 m away
            approachingCar.TickMovement(0.05f);
            approachingCar.TickMovement(0.1f);

            Assert.IsTrue(approachingCar.IsYielding, "Vehicle approaching an occupied YieldIntersection must yield.");

            // Release the crossing and verify vehicle claims it and accelerates
            nodeCrossing.ReleaseIntersection(crossTrafficToken);
            approachingCar.TickMovement(0.1f);

            Assert.IsFalse(approachingCar.IsYielding, "Vehicle should proceed once YieldIntersection is released.");
            Assert.AreEqual(approachingCar, nodeCrossing.OccupyingVehicle);
        }

        private RoadNetwork CreateMetricNetwork()
        {
            var gridGo = CreateTrackedObject("IsometricGrid", Vector3.zero);
            _grid = gridGo.AddComponent<Grid>();
            _grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            _grid.cellSize = new Vector3(1f, 0.5f, 1f);
            _grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            var networkGo = CreateTrackedObject("RoadNetwork", Vector3.zero);
            var network = networkGo.AddComponent<RoadNetwork>();
            network.ConfigureGrid(_grid);
            return network;
        }

        private Vector3 CellWorld(int cellX, int cellY)
        {
            return _grid.CellToWorld(new Vector3Int(cellX, cellY, 0));
        }

        private GameObject CreateTrackedObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawnedObjects.Add(go);
            return go;
        }

        private RoadNode CreateNode(Transform parent, string name, int cellX, int cellY)
        {
            var go = CreateTrackedObject(name, CellWorld(cellX, cellY));
            go.transform.SetParent(parent);
            return go.AddComponent<RoadNode>();
        }

        private VehicleController CreateVehicle(string name, Vector3 position, RoadNetwork network)
        {
            var go = CreateTrackedObject(name, position);
            var controller = go.AddComponent<VehicleController>();
            controller.Construct(network);
            VehicleController.RegisterActiveVehicle(controller);
            return controller;
        }
    }
}
