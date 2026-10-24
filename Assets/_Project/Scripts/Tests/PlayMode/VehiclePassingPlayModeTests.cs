using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.PlayMode
{
    public class VehiclePassingPlayModeTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }
            _spawned.Clear();
            VehicleController.ClearActiveVehicles();
        }

        [UnityTest]
        public IEnumerator OpposingVehicles_PassInSeparateLanesInRealTime()
        {
            Time.timeScale = 1f;
            Application.runInBackground = true;

            var gridGo = Track(new GameObject("PlayModeGrid"));
            var grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            var networkGo = Track(new GameObject("PlayModeNetwork"));
            var network = networkGo.AddComponent<RoadNetwork>();
            network.ConfigureGrid(grid);

            var nodeA = CreateNode(network.transform, grid, "node_play_a", 0, 0, LaneOffset);
            var nodeB = CreateNode(network.transform, grid, "node_play_b", 40, 0, LaneOffset, RoadPieceType.EndCap);
            nodeA.AddConnection(nodeB);
            nodeB.AddConnection(nodeA);
            network.RebuildNetwork();

            var outbound = CreateVehicle("OutboundPlay", nodeA.Position, network);
            var inbound = CreateVehicle("InboundPlay", nodeB.Position, network);
            outbound.MaxSpeed = 10f;
            inbound.MaxSpeed = 10f;
            outbound.Acceleration = 1000f;
            inbound.Acceleration = 1000f;
            outbound.WaypointArrivalRadius = 0.6f;
            inbound.WaypointArrivalRadius = 0.6f;

            Assert.IsTrue(outbound.NavigateTo(nodeB));
            Assert.IsTrue(inbound.NavigateTo(nodeA));

            var projection = network.GroundProjection;
            float previousGap = projection.WorldToGround(outbound.transform.position).x
                - projection.WorldToGround(inbound.transform.position).x;
            float lateralAtCrossing = float.MaxValue;
            bool crossed = false;
            bool yieldObserved = false;

            float startedAt = Time.realtimeSinceStartup;
            for (int frame = 0; frame < 9000 && (outbound.HasActiveRoute || inbound.HasActiveRoute) && Time.realtimeSinceStartup - startedAt < 15f; frame++)
            {
                yield return null;

                if (outbound.IsYielding || inbound.IsYielding)
                {
                    yieldObserved = true;
                }

                Vector2 outboundGround = projection.WorldToGround(outbound.transform.position);
                Vector2 inboundGround = projection.WorldToGround(inbound.transform.position);
                float gap = outboundGround.x - inboundGround.x;
                if (!crossed && previousGap < 0f && gap >= 0f)
                {
                    crossed = true;
                    lateralAtCrossing = Mathf.Abs(outboundGround.y - inboundGround.y);
                }
                previousGap = gap;
            }

            Assert.IsTrue(crossed, "Vehicles should meet and pass in real time.");
            Assert.GreaterOrEqual(lateralAtCrossing, 2.5f, "Real-time opposing vehicles must keep separate lane spacing.");
            Assert.IsFalse(yieldObserved, "Opposing vehicles in dedicated lanes should not yield to one another while passing.");
            Assert.IsFalse(outbound.HasActiveRoute, "Outbound vehicle should finish its route.");
            Assert.IsFalse(inbound.HasActiveRoute, "Inbound vehicle should finish its route.");
        }

        private const float LaneOffset = 1.5f;

        private T Track<T>(T obj) where T : Object
        {
            _spawned.Add(obj);
            return obj;
        }

        private RoadNode CreateNode(Transform parent, Grid grid, string id, int cellX, int cellY, float laneOffset, RoadPieceType pieceType = RoadPieceType.Straight)
        {
            var go = Track(new GameObject(id));
            go.transform.SetParent(parent, false);
            go.transform.position = grid.CellToWorld(new Vector3Int(cellX, cellY, 0));
            var node = go.AddComponent<RoadNode>();
            node.Configure(id, RoadNodePriority.MainArtery, pieceType, "district_01", 0, false, laneOffset);
            return node;
        }

        private VehicleController CreateVehicle(string name, Vector3 position, RoadNetwork network)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            var controller = go.AddComponent<VehicleController>();
            controller.Construct(network);
            VehicleController.RegisterActiveVehicle(controller);
            return controller;
        }
    }
}
