using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    public class CityExtensionTests
    {
        [TearDown]
        public void TearDown()
        {
            VehicleController.ClearActiveVehicles();
        }

        [Test]
        public void ProductionScene_EastLoopIsAnAdditiveDrivableAlternativeRoute()
        {
            const string scenePath = "Assets/_Project/Scenes/OutsiderCity.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid());

            var network = Object.FindAnyObjectByType<RoadNetwork>();
            var taxi = Object.FindAnyObjectByType<VehicleController>();
            Assert.IsNotNull(network);
            Assert.IsNotNull(taxi);
            Assert.IsTrue(network.ValidateNetwork(out var issues), string.Join("; ", issues));

            var midEast = network.GetNode("node_mid_east");
            var link = network.GetNode("node_d02_east_link");
            var bend = network.GetNode("node_d02_east_bend");
            var outer = network.GetNode("node_d02_east_outer");
            var destination = network.GetNode("node_d02_east_corner");
            Assert.IsNotNull(midEast);
            Assert.IsNotNull(link);
            Assert.IsNotNull(bend);
            Assert.IsNotNull(outer);
            Assert.IsNotNull(destination);

            var loop = new List<RoadNode> { midEast, link, bend, outer, destination };
            for (int i = 0; i < loop.Count - 1; i++)
            {
                CollectionAssert.Contains(loop[i].ConnectedNodes, loop[i + 1],
                    $"Loop segment {loop[i].NodeId} -> {loop[i + 1].NodeId} must be authored and bidirectional.");
                CollectionAssert.Contains(loop[i + 1].ConnectedNodes, loop[i],
                    $"Loop segment {loop[i + 1].NodeId} -> {loop[i].NodeId} must be authored and bidirectional.");
            }

            var primary = network.FindPath(midEast, destination);
            Assert.IsNotEmpty(primary);
            Assert.Contains(link, primary, "The direct route must pass through the new T-junction.");

            VehicleController.ClearActiveVehicles();
            VehicleController.RegisterActiveVehicle(taxi);
            taxi.transform.position = midEast.Position;
            taxi.CancelRoute();
            foreach (var node in network.AllNodes)
            {
                node.ClearReservation();
            }

            Assert.IsTrue(taxi.NavigateAlongPath(loop), "The authored east loop must be accepted as a route.");
            for (int i = 0; i < 1600 && taxi.HasActiveRoute; i++)
            {
                taxi.TickMovement(0.05f);
            }

            Assert.IsFalse(taxi.HasActiveRoute, "Taxi should complete the east loop route.");
            float distanceToDestination = Vector2.Distance(
                network.GroundProjection.WorldToGround(taxi.transform.position),
                network.GroundProjection.WorldToGround(destination.Position));
            Assert.LessOrEqual(distanceToDestination, 2.0f, "Taxi should stop in the destination lane beside the corner node.");
        }
    }
}
