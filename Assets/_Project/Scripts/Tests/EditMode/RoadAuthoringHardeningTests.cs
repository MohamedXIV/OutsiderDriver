using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    public class RoadAuthoringHardeningTests
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
        public void RoadPiecePrefabs_AreVisualOnlyAndRepeatableWithoutCloningNavigationIds()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Roads" });
            Assert.IsNotEmpty(guids, "Expected reusable road piece prefabs under Assets/_Project/Prefabs/Roads.");

            var root = CreateRoot("PrefabRepeatabilityRoot");
            var network = root.AddComponent<RoadNetwork>();

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert.IsNotNull(prefab, $"Could not load road prefab '{path}'.");

                    Assert.AreEqual(0, prefab.GetComponentsInChildren<RoadNode>(true).Length,
                        $"Visual road prefab '{path}' must not embed a RoadNode; navigation identity belongs to separately authored nodes.");
                    Assert.IsNotNull(prefab.GetComponentInChildren<SpriteRenderer>(true),
                        $"Visual road prefab '{path}' should provide a sprite visual.");

                    for (int copy = 0; copy < 3; copy++)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        instance.transform.SetParent(root.transform, false);
                        Assert.AreEqual(0, instance.GetComponentsInChildren<RoadNode>(true).Length,
                            $"Duplicating '{path}' must not create navigation nodes.");
                    }
                }

                var nodeA = CreateNode(root.transform, "node_authored_a", Vector3.zero);
                var nodeB = CreateNode(root.transform, "node_authored_b", new Vector3(4f, 2f, 0f));
                nodeA.AddConnection(nodeB);
                nodeB.AddConnection(nodeA);
                network.RebuildNetwork();

                Assert.AreEqual(2, network.AllNodes.Count, "Only the two separately authored navigation nodes should be registered.");
                Assert.IsTrue(network.ValidateNetwork(out var issues), string.Join("; ", issues));
                Assert.IsEmpty(network.DuplicateNodeIds);
            }
            finally
            {
                Object.DestroyImmediate(root);
                _spawnedObjects.Remove(root);
            }
        }

        [Test]
        public void RoadNetworkValidation_ReportsDuplicatesNullsSelfLinksOneWayLinksDisconnectionAndElevation()
        {
            var root = CreateRoot("ValidationRoot");

            var mainA = CreateNode(root.transform, "node_main_a", new Vector3(0f, 0f, 0f));
            var mainB = CreateNode(root.transform, "node_main_b", new Vector3(2f, 1f, 0f));
            var mainC = CreateNode(root.transform, "node_main_c", new Vector3(4f, 2f, 0f));
            ConnectBidirectional(mainA, mainB);
            ConnectBidirectional(mainB, mainC);

            var duplicateA = CreateNode(root.transform, "node_duplicate", new Vector3(10f, 5f, 0f));
            var duplicateB = CreateNode(root.transform, "node_duplicate", new Vector3(12f, 6f, 0f));
            ConnectBidirectional(duplicateA, duplicateB);

            var selfLinked = CreateNode(root.transform, "node_self", new Vector3(20f, 10f, 0f));
            AppendRawConnection(selfLinked, selfLinked);

            var nullLinked = CreateNode(root.transform, "node_null", new Vector3(24f, 12f, 0f));
            AppendRawConnection(nullLinked, null);

            var oneWayA = CreateNode(root.transform, "node_one_way_a", new Vector3(30f, 15f, 0f));
            var oneWayB = CreateNode(root.transform, "node_one_way_b", new Vector3(32f, 16f, 0f));
            oneWayA.AddConnection(oneWayB);
            oneWayB.AddConnection(mainC);

            var elevatedA = CreateNode(root.transform, "node_elevated_a", new Vector3(40f, 20f, 0f), elevation: 0);
            var elevatedB = CreateNode(root.transform, "node_elevated_b", new Vector3(42f, 21f, 0f), elevation: 1);
            ConnectBidirectional(elevatedA, elevatedB);

            var network = root.AddComponent<RoadNetwork>();
            network.RebuildNetwork();

            Assert.IsFalse(network.ValidateNetwork(out var issues));
            Assert.IsTrue(network.DuplicateNodeIds.Contains("node_duplicate"), "DuplicateNodeIds should expose the ambiguous ID.");
            AssertIssue(issues, "Duplicate RoadNode ID");
            AssertIssue(issues, "null connection reference");
            AssertIssue(issues, "connected to itself");
            AssertIssue(issues, "one-way link");
            AssertIssue(issues, "disconnected from the main road network component");
            AssertIssue(issues, "without an authored ramp");
        }

        [Test]
        public void BuildSettings_OutsiderCityIsTheFirstEnabledScene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.IsNotEmpty(scenes);
            Assert.AreEqual("Assets/_Project/Scenes/OutsiderCity.unity", scenes[0].path);
            Assert.IsTrue(scenes[0].enabled, "OutsiderCity must be enabled as the default build scene.");

            for (int i = 1; i < scenes.Length; i++)
            {
                if (scenes[i].path == "Assets/Scenes/SampleScene.unity")
                {
                    Assert.IsFalse(scenes[i].enabled, "SampleScene must not be a competing enabled production scene.");
                }
            }
        }

        private static void AssertIssue(List<string> issues, string fragment)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Contains(fragment)) return;
            }
            Assert.Fail($"Expected a validation issue containing '{fragment}'. Actual issues: {string.Join(" | ", issues)}");
        }

        private GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            _spawnedObjects.Add(root);
            return root;
        }

        private RoadNode CreateNode(Transform parent, string nodeId, Vector3 position, int elevation = 0, bool isRamp = false)
        {
            var go = new GameObject(nodeId);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            _spawnedObjects.Add(go);

            var node = go.AddComponent<RoadNode>();
            node.Configure(nodeId, RoadNodePriority.MainArtery, RoadPieceType.Straight, "district_01", elevation, isRamp);
            return node;
        }

        private static void ConnectBidirectional(RoadNode a, RoadNode b)
        {
            a.AddConnection(b);
            b.AddConnection(a);
        }

        private static void AppendRawConnection(RoadNode node, RoadNode connection)
        {
            var serialized = new SerializedObject(node);
            var list = serialized.FindProperty("_connectedNodes");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = connection;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public class PhysicalScaleCalibrationTests
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
        public void IsometricGroundProjection_TreatsOneAuthoredCellAsOneMetre()
        {
            var grid = CreateIsometricGrid();
            var projection = IsometricGroundProjection.FromGrid(grid);

            Vector2 origin = grid.CellToWorld(new Vector3Int(0, 0, 0));
            Vector2 cellX = grid.CellToWorld(new Vector3Int(1, 0, 0));
            Vector2 cellY = grid.CellToWorld(new Vector3Int(0, 1, 0));
            Vector2 fiveCellsX = grid.CellToWorld(new Vector3Int(5, 0, 0));

            Assert.AreEqual(1f, projection.Distance(origin, cellX), 0.0001f, "One +X cell must equal one ground metre.");
            Assert.AreEqual(1f, projection.Distance(origin, cellY), 0.0001f, "One +Y cell must equal one ground metre.");
            Assert.AreEqual(5f, projection.WorldToGround(fiveCellsX).x - projection.WorldToGround(origin).x, 0.0001f);
            Assert.AreEqual(0f, projection.WorldToGround(fiveCellsX).y - projection.WorldToGround(origin).y, 0.0001f);
        }

        [Test]
        public void VehicleController_TravelsAtAuthoredMetresPerSecond()
        {
            var grid = CreateIsometricGrid();
            var network = CreateNetwork(grid);
            var nodeA = CreateNode(network.transform, grid, "node_speed_a", 0, 0, laneOffset: 0f);
            var nodeB = CreateNode(network.transform, grid, "node_speed_b", 30, 0, laneOffset: 0f, pieceType: RoadPieceType.EndCap);
            ConnectBidirectional(nodeA, nodeB);
            network.RebuildNetwork();

            var vehicle = CreateVehicle("SpeedTaxi", nodeA.Position, network);
            vehicle.MaxSpeed = 10f;
            vehicle.Acceleration = 1000f;
            vehicle.WaypointArrivalRadius = 0.5f;

            Assert.IsTrue(vehicle.NavigateTo(nodeB));
            Vector2 startGround = network.GroundProjection.WorldToGround(vehicle.transform.position);
            vehicle.TickMovement(1f);

            float travelled = (network.GroundProjection.WorldToGround(vehicle.transform.position) - startGround).magnitude;
            Assert.AreEqual(10f, travelled, 0.25f, "A 10 m/s vehicle must cover ten ground metres in one second, independent of the isometric world projection.");

            for (int i = 0; i < 40 && vehicle.HasActiveRoute; i++)
            {
                vehicle.TickMovement(0.1f);
            }

            Assert.IsFalse(vehicle.HasActiveRoute, "Vehicle should finish its 30 m route.");
            Vector2 endGround = network.GroundProjection.WorldToGround(vehicle.transform.position);
            Vector2 targetGround = network.GroundProjection.WorldToGround(nodeB.Position);
            Assert.LessOrEqual(Vector2.Distance(endGround, targetGround), 1.0f);
        }

        [Test]
        public void OpposingVehicles_PassInSeparateLanesWithoutMutualYielding()
        {
            var grid = CreateIsometricGrid();
            var network = CreateNetwork(grid);
            var nodeA = CreateNode(network.transform, grid, "node_pass_a", 0, 0, laneOffset: 1.5f);
            var nodeB = CreateNode(network.transform, grid, "node_pass_b", 40, 0, laneOffset: 1.5f, pieceType: RoadPieceType.EndCap);
            ConnectBidirectional(nodeA, nodeB);
            network.RebuildNetwork();

            var outbound = CreateVehicle("Outbound", nodeA.Position, network);
            var inbound = CreateVehicle("Inbound", nodeB.Position, network);
            outbound.MaxSpeed = 10f;
            inbound.MaxSpeed = 10f;
            outbound.Acceleration = 1000f;
            inbound.Acceleration = 1000f;
            outbound.WaypointArrivalRadius = 2.0f;
            inbound.WaypointArrivalRadius = 2.0f;

            Assert.IsTrue(outbound.NavigateTo(nodeB));
            Assert.IsTrue(inbound.NavigateTo(nodeA));

            float closestLateralSeparation = float.MaxValue;
            bool crossed = false;
            bool yieldObserved = false;
            float previousGap = network.GroundProjection.WorldToGround(outbound.transform.position).x
                - network.GroundProjection.WorldToGround(inbound.transform.position).x;

            for (int step = 0; step < 200 && (outbound.HasActiveRoute || inbound.HasActiveRoute); step++)
            {
                outbound.TickMovement(0.05f);
                inbound.TickMovement(0.05f);

                if (outbound.IsYielding || inbound.IsYielding)
                {
                    yieldObserved = true;
                }

                Vector2 outboundGround = network.GroundProjection.WorldToGround(outbound.transform.position);
                Vector2 inboundGround = network.GroundProjection.WorldToGround(inbound.transform.position);
                float gap = outboundGround.x - inboundGround.x;
                if (!crossed && previousGap < 0f && gap >= 0f)
                {
                    crossed = true;
                    closestLateralSeparation = Mathf.Abs(outboundGround.y - inboundGround.y);
                }
                previousGap = gap;
            }

            Assert.IsTrue(crossed, "The two vehicles should meet and pass on the street.");
            Assert.GreaterOrEqual(closestLateralSeparation, 2.5f, "Opposing vehicles must pass in separate lanes with a believable gap.");
            Assert.IsFalse(yieldObserved, "Opposing vehicles in separate lanes must not yield to each other while passing.");
            Assert.IsFalse(outbound.HasActiveRoute, "Outbound vehicle should reach its destination.");
            Assert.IsFalse(inbound.HasActiveRoute, "Inbound vehicle should reach its destination.");
        }

        private Grid CreateIsometricGrid()
        {
            var go = new GameObject("TestIsometricGrid");
            _spawnedObjects.Add(go);
            var grid = go.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
            return grid;
        }

        private RoadNetwork CreateNetwork(Grid grid)
        {
            var go = new GameObject("TestRoadNetwork");
            _spawnedObjects.Add(go);
            var network = go.AddComponent<RoadNetwork>();
            network.ConfigureGrid(grid);
            return network;
        }

        private RoadNode CreateNode(
            Transform parent,
            Grid grid,
            string nodeId,
            int cellX,
            int cellY,
            float laneOffset,
            RoadPieceType pieceType = RoadPieceType.Straight)
        {
            var go = new GameObject(nodeId);
            go.transform.SetParent(parent, false);
            go.transform.position = grid.CellToWorld(new Vector3Int(cellX, cellY, 0));
            _spawnedObjects.Add(go);

            var node = go.AddComponent<RoadNode>();
            node.Configure(nodeId, RoadNodePriority.MainArtery, pieceType, "district_01", 0, false, laneOffset);
            return node;
        }

        private VehicleController CreateVehicle(string name, Vector3 position, RoadNetwork network)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawnedObjects.Add(go);

            var controller = go.AddComponent<VehicleController>();
            controller.Construct(network);
            VehicleController.RegisterActiveVehicle(controller);
            return controller;
        }

        private static void ConnectBidirectional(RoadNode a, RoadNode b)
        {
            a.AddConnection(b);
            b.AddConnection(a);
        }
    }
}
