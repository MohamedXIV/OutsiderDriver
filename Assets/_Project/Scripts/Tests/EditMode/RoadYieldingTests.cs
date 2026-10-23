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
            var networkGo = CreateTrackedObject("RoadNetwork", Vector3.zero);
            var network = networkGo.AddComponent<RoadNetwork>();

            var nodeStart = CreateNode(networkGo.transform, "Start", new Vector3(0f, 0f, 0f));
            var nodeEnd = CreateNode(networkGo.transform, "End", new Vector3(10f, 0f, 0f));
            nodeStart.AddConnection(nodeEnd);
            network.RebuildNetwork();

            var leadCar = CreateVehicle("LeadCar", new Vector3(2.5f, 0f, 0f), network);
            var followerCar = CreateVehicle("FollowerCar", new Vector3(0f, 0f, 0f), network);
            followerCar.ForwardSensorDistance = 3.0f;

            // Start follower navigating to nodeEnd
            Assert.IsTrue(followerCar.NavigateTo(nodeEnd));
            followerCar.TickMovement(0.05f); // Pass nodeStart
            followerCar.TickMovement(0.1f);  // Targeting nodeEnd (10,0,0), facing (+1,0,0), leadCar at (2.5,0,0) is in front!

            Assert.IsTrue(followerCar.IsYielding, "Follower vehicle should yield when lead vehicle is within forward sensor range.");

            // Move lead vehicle far ahead out of sensor range
            leadCar.transform.position = new Vector3(8.0f, 0f, 0f);
            followerCar.TickMovement(0.1f);

            Assert.IsFalse(followerCar.IsYielding, "Follower vehicle should resume moving once lead vehicle clears sensor range.");
            Assert.Greater(followerCar.CurrentSpeed, 0f);
        }

        [Test]
        public void VehicleController_YieldsAtOccupiedYieldIntersectionUntilCleared()
        {
            var networkGo = CreateTrackedObject("RoadNetwork", Vector3.zero);
            var network = networkGo.AddComponent<RoadNetwork>();

            var nodeStart = CreateNode(networkGo.transform, "Start", new Vector3(0f, 0f, 0f));
            var nodeCrossing = CreateNode(networkGo.transform, "Crossing", new Vector3(1.0f, 0f, 0f));
            nodeCrossing.Priority = RoadNodePriority.YieldIntersection;
            nodeCrossing.IntersectionClearanceRadius = 1.2f;

            var nodeEnd = CreateNode(networkGo.transform, "End", new Vector3(5.0f, 0f, 0f));
            nodeStart.AddConnection(nodeCrossing);
            nodeCrossing.AddConnection(nodeEnd);
            network.RebuildNetwork();

            // Another vehicle already occupies the crossing
            var crossTrafficToken = new object();
            nodeCrossing.TryReserveIntersection(crossTrafficToken);

            var approachingCar = CreateVehicle("ApproachingCar", new Vector3(0f, 0f, 0f), network);
            Assert.IsTrue(approachingCar.NavigateTo(nodeEnd));

            // First tick passes nodeStart at (0,0,0) and targets nodeCrossing at (1,0,0)
            approachingCar.TickMovement(0.05f);
            approachingCar.TickMovement(0.1f);

            Assert.IsTrue(approachingCar.IsYielding, "Vehicle approaching an occupied YieldIntersection must yield.");

            // Release the crossing and verify vehicle claims it and accelerates
            nodeCrossing.ReleaseIntersection(crossTrafficToken);
            approachingCar.TickMovement(0.1f);

            Assert.IsFalse(approachingCar.IsYielding, "Vehicle should proceed once YieldIntersection is released.");
            Assert.AreEqual(approachingCar, nodeCrossing.OccupyingVehicle);
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
