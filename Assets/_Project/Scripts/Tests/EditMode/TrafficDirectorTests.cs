using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    public class TrafficDirectorTests
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
        public void EvaluateBubble_SpawnsVehiclesWithinRingUpToMaxCap()
        {
            var network = CreateTestRingRoadNetwork(out Transform center);
            var directorGo = CreateTrackedObject("TrafficDirector", Vector3.zero);
            var director = directorGo.AddComponent<TrafficDirector>();
            director.Construct(network);
            director.BubbleCenter = center;
            director.MinSpawnRadius = 5f;
            director.MaxSpawnRadius = 15f;
            director.DespawnRadius = 22f;
            director.MaxActiveVehicles = 2;

            director.EvaluateBubble();

            Assert.AreEqual(2, director.ActiveCount, "Director should spawn ambient vehicles up to MaxActiveVehicles.");
            for (int i = 0; i < director.ActiveAmbientVehicles.Count; i++)
            {
                var vehicle = director.ActiveAmbientVehicles[i];
                Assert.IsNotNull(vehicle);
                Assert.IsTrue(vehicle.gameObject.activeSelf);
                Assert.IsTrue(vehicle.HasActiveRoute, "Spawned ambient vehicle should have an active route assigned.");
            }
        }

        [Test]
        public void EvaluateBubble_RecyclesVehiclesOutsideDespawnRadiusAndReusesPool()
        {
            var network = CreateTestRingRoadNetwork(out Transform center);
            var directorGo = CreateTrackedObject("TrafficDirector", Vector3.zero);
            var director = directorGo.AddComponent<TrafficDirector>();
            director.Construct(network);
            director.BubbleCenter = center;
            director.MinSpawnRadius = 5f;
            director.MaxSpawnRadius = 15f;
            director.DespawnRadius = 20f;
            director.MaxActiveVehicles = 1;

            director.EvaluateBubble();
            Assert.AreEqual(1, director.ActiveCount);

            VehicleController firstSpawned = director.ActiveAmbientVehicles[0];

            // Move the vehicle outside the despawn bubble (25m > 20m despawn radius)
            // Temporarily set MaxActiveVehicles = 0 so it recycles without immediately re-spawning
            firstSpawned.transform.position = new Vector3(25f, 0f, 0f);
            director.MaxActiveVehicles = 0;
            director.EvaluateBubble();

            Assert.AreEqual(0, director.ActiveCount, "Vehicle outside despawn radius should be recycled.");
            Assert.AreEqual(1, director.InactivePoolCount, "Recycled vehicle should sit in the inactive ObjectPool.");
            Assert.IsFalse(firstSpawned.gameObject.activeSelf);

            // Restore capacity to 1 and verify the exact same pooled instance is reused
            director.MaxActiveVehicles = 1;
            director.EvaluateBubble();

            Assert.AreEqual(1, director.ActiveCount);
            Assert.AreSame(firstSpawned, director.ActiveAmbientVehicles[0], "ObjectPool should reuse the recycled VehicleController instance.");
            Assert.IsTrue(firstSpawned.gameObject.activeSelf);
        }

        private RoadNetwork CreateTestRingRoadNetwork(out Transform centerTransform)
        {
            var centerGo = CreateTrackedObject("BubbleCenter", Vector3.zero);
            centerTransform = centerGo.transform;

            var networkGo = CreateTrackedObject("RoadNetwork", Vector3.zero);
            var network = networkGo.AddComponent<RoadNetwork>();

            // Node too close (< 5m minSpawnRadius) - should not be used for spawning
            var nodeInner = CreateNode(networkGo.transform, "NodeInner", new Vector3(2f, 0f, 0f));

            // Nodes inside the spawn ring (between 5m and 15m)
            var nodeRingA = CreateNode(networkGo.transform, "NodeRingA", new Vector3(8f, 0f, 0f));
            var nodeRingB = CreateNode(networkGo.transform, "NodeRingB", new Vector3(0f, 9f, 0f));
            var nodeRingC = CreateNode(networkGo.transform, "NodeRingC", new Vector3(-10f, 0f, 0f));

            nodeInner.AddConnection(nodeRingA);
            nodeRingA.AddConnection(nodeRingB);
            nodeRingB.AddConnection(nodeRingC);
            nodeRingC.AddConnection(nodeRingA);

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
    }
}
