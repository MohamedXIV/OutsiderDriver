using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Tests.EditMode
{
    [TestFixture]
    public class CityFoundationTests
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
        public void RoadNetwork_ElevationSeparation_BlocksUnrampedCrossingAndAllowsRampedConnection()
        {
            var networkGo = CreateTrackedObject("RoadNetwork", Vector3.zero);
            var network = networkGo.AddComponent<RoadNetwork>();

            var groundStart = CreateNode(networkGo.transform, "node_ground_start", new Vector3(0f, 0f, 0f), RoadPieceType.Straight, elevation: 0);
            var elevatedOverpass = CreateNode(networkGo.transform, "node_elevated_overpass", new Vector3(4f, 2f, 0f), RoadPieceType.Straight, elevation: 1, isRamp: false);
            var elevatedDest = CreateNode(networkGo.transform, "node_elevated_dest", new Vector3(8f, 4f, 0f), RoadPieceType.EndCap, elevation: 1);

            // Visually overlapping connection without a ramp must be rejected by pathfinding and validation
            groundStart.AddConnection(elevatedOverpass);
            elevatedOverpass.AddConnection(elevatedDest);
            network.RebuildNetwork();

            Assert.IsFalse(groundStart.CanTraverseElevationTo(elevatedOverpass));
            var illegalPath = network.FindPath(groundStart, elevatedDest);
            Assert.AreEqual(0, illegalPath.Count, "Pathfinding must never traverse across different elevations without an authored ramp.");
            Assert.IsFalse(network.ValidateNetwork(out var issues));
            Assert.IsNotEmpty(issues);

            // Now add an authored ramp node between level 0 and level 1
            groundStart.RemoveConnection(elevatedOverpass);
            var rampNode = CreateNode(networkGo.transform, "node_ramp_01", new Vector3(2f, 1f, 0f), RoadPieceType.Ramp, elevation: 0, isRamp: true);
            groundStart.AddConnection(rampNode);
            rampNode.AddConnection(elevatedOverpass);
            network.RebuildNetwork();

            Assert.IsTrue(groundStart.CanTraverseElevationTo(rampNode));
            Assert.IsTrue(rampNode.CanTraverseElevationTo(elevatedOverpass));

            var validPath = network.FindPath(groundStart, elevatedDest);
            Assert.AreEqual(4, validPath.Count);
            Assert.AreEqual(groundStart, validPath[0]);
            Assert.AreEqual(rampNode, validPath[1]);
            Assert.AreEqual(elevatedOverpass, validPath[2]);
            Assert.AreEqual(elevatedDest, validPath[3]);
        }

        [Test]
        public void RoadNetwork_FindsPrimaryReturnAndAlternativeRoutesAcrossIntersections()
        {
            var networkGo = CreateTrackedObject("RoadNetwork", Vector3.zero);
            var network = networkGo.AddComponent<RoadNetwork>();

            // Build a multi-block isometric network with T-junctions, corners, and a 4-way intersection
            var homeBay = CreateNode(networkGo.transform, "node_d01_home_bay", new Vector3(-2f, 0.25f, 0f), RoadPieceType.EndCap);
            var swJunction = CreateNode(networkGo.transform, "node_d01_sw_junction", new Vector3(0f, 0.25f, 0f), RoadPieceType.TJunction);
            var seCorner = CreateNode(networkGo.transform, "node_d01_se_corner", new Vector3(5f, 2.75f, 0f), RoadPieceType.Corner);
            var midWest = CreateNode(networkGo.transform, "node_mid_west", new Vector3(-5f, 2.75f, 0f), RoadPieceType.TJunction);
            var midCenter = CreateNode(networkGo.transform, "node_mid_center", new Vector3(0f, 5.25f, 0f), RoadPieceType.FourWayIntersection);
            var d02West = CreateNode(networkGo.transform, "node_d02_west", new Vector3(-8f, 4.25f, 0f), RoadPieceType.Corner);
            var marketJunction = CreateNode(networkGo.transform, "node_d02_market_junction", new Vector3(-5f, 7.75f, 0f), RoadPieceType.FourWayIntersection);
            var marketPlaza = CreateNode(networkGo.transform, "node_d02_market_plaza", new Vector3(-8f, 9.25f, 0f), RoadPieceType.EndCap);

            ConnectBidirectional(homeBay, swJunction);
            ConnectBidirectional(swJunction, midWest);
            ConnectBidirectional(swJunction, seCorner);
            ConnectBidirectional(midWest, d02West);
            ConnectBidirectional(midWest, midCenter);
            ConnectBidirectional(seCorner, midCenter);
            ConnectBidirectional(d02West, marketJunction);
            ConnectBidirectional(midCenter, marketJunction);
            ConnectBidirectional(marketJunction, marketPlaza);

            network.RebuildNetwork();

            var primaryOutbound = network.FindPath(homeBay, marketPlaza);
            Assert.GreaterOrEqual(primaryOutbound.Count, 5);
            Assert.AreEqual(homeBay, primaryOutbound[0]);
            Assert.AreEqual(marketPlaza, primaryOutbound[primaryOutbound.Count - 1]);

            var altOutbound = network.FindAlternativePath(homeBay, marketPlaza, primaryOutbound);
            Assert.GreaterOrEqual(altOutbound.Count, 5);
            Assert.AreEqual(homeBay, altOutbound[0]);
            Assert.AreEqual(marketPlaza, altOutbound[altOutbound.Count - 1]);
            CollectionAssert.AreNotEqual(primaryOutbound, altOutbound, "Alternative route must follow a distinct street sequence.");

            var returnPath = network.FindPath(marketPlaza, homeBay);
            Assert.GreaterOrEqual(returnPath.Count, 5);
            Assert.AreEqual(marketPlaza, returnPath[0]);
            Assert.AreEqual(homeBay, returnPath[returnPath.Count - 1]);
        }

        [Test]
        public void CityWorldRegistry_ValidatesDataDrivenIdsAlienNamingMetadataAndTwoStoryHomeConstraint()
        {
            var rootGo = CreateTrackedObject("CityRoot", Vector3.zero);
            var networkGo = CreateTrackedObject("RoadNetwork", Vector3.zero);
            networkGo.transform.SetParent(rootGo.transform, false);
            var network = networkGo.AddComponent<RoadNetwork>();

            var nodeHome = CreateNode(networkGo.transform, "node_d01_home_bay", new Vector3(-2f, 0.25f, 0f), RoadPieceType.EndCap);
            var nodeMarket = CreateNode(networkGo.transform, "node_d02_market_plaza", new Vector3(-8f, 9.25f, 0f), RoadPieceType.EndCap);
            ConnectBidirectional(nodeHome, nodeMarket);
            network.RebuildNetwork();

            var district01 = CreateAsset<DistrictConfigSO>();
            district01.Initialize("district_01", "Vesh-Kora", 4, 15, 2.0f, new[] { "node_d01_home_bay" });
            district01.ConfigureAuthoringMetadata(
                workingName: "Outer Home & Workshop Quarter",
                displayNameKey: "world.district_01.name",
                summary: "Lived-in residential and small-workshop neighborhood.",
                backgroundLore: "Draft lore pending owner canon review.",
                districtBounds: new Rect(-6f, -2f, 20f, 10f),
                ambientTintColor: new Color(0.95f, 0.85f, 0.72f),
                dominantLanguageId: "Common",
                defaultRadioStationId: "station_amber_wave",
                alienNameCandidates: new[] { "Vesh-Kora", "Tarn-Sul" },
                locationIds: new[] { "location_001" },
                isNameApproved: false);

            var district02 = CreateAsset<DistrictConfigSO>();
            district02.Initialize("district_02", "Solen-Mar", 6, 20, 2.5f, new[] { "node_d02_market_plaza" });
            district02.ConfigureAuthoringMetadata(
                workingName: "Central Market Core",
                displayNameKey: "world.district_02.name",
                summary: "Central commercial and everyday market streets.",
                backgroundLore: "Draft lore pending owner canon review.",
                districtBounds: new Rect(-14f, 4f, 20f, 10f),
                ambientTintColor: new Color(0.75f, 0.92f, 0.88f),
                dominantLanguageId: "Common",
                defaultRadioStationId: "station_amber_wave",
                alienNameCandidates: new[] { "Solen-Mar", "Kael-Vora" },
                locationIds: new[] { "location_003" },
                isNameApproved: false);

            var homeLocSO = CreateAsset<WorldLocationSO>();
            homeLocSO.Configure(
                locationId: "location_001",
                districtId: "district_01",
                category: WorldLocationCategory.PlayerHomeGarage,
                workingName: "Driver's Two-Story Home & Garage",
                displayNameKey: "world.location_001.name",
                displayName: "Tarn-Vesh 4",
                connectedRoadNodeId: "node_d01_home_bay",
                floorCount: 2,
                summary: "Two-story building housing the ground-level taxi garage and upstairs apartment.",
                alienNameCandidates: new[] { "Tarn-Vesh 4", "Kora-Dar 2" },
                isNameApproved: false);

            var marketLocSO = CreateAsset<WorldLocationSO>();
            marketLocSO.Configure(
                locationId: "location_003",
                districtId: "district_02",
                category: WorldLocationCategory.CommercialMarket,
                workingName: "Central Market Hall",
                displayNameKey: "world.location_003.name",
                displayName: "Solen-Vara Hall",
                connectedRoadNodeId: "node_d02_market_plaza",
                floorCount: 2,
                summary: "Recognizable commercial market exchange.",
                alienNameCandidates: new[] { "Solen-Vara", "Miren-Kael" },
                isNameApproved: false);

            var homeAnchorGo = CreateTrackedObject("Building_HomeGarage", new Vector3(-2.5f, 1.0f, 0f));
            homeAnchorGo.transform.SetParent(rootGo.transform, false);
            var homeAnchor = homeAnchorGo.AddComponent<CityLocationAnchor>();
            homeAnchor.Configure(homeLocSO, nodeHome, new Vector2Int(-4, 1), new Vector2Int(4, 4));

            var marketAnchorGo = CreateTrackedObject("Building_MarketHall", new Vector3(-8.5f, 10.0f, 0f));
            marketAnchorGo.transform.SetParent(rootGo.transform, false);
            var marketAnchor = marketAnchorGo.AddComponent<CityLocationAnchor>();
            marketAnchor.Configure(marketLocSO, nodeMarket, new Vector2Int(8, 26), new Vector2Int(6, 5));

            var registry = rootGo.AddComponent<CityWorldRegistry>();
            registry.Configure(network, null, new[] { district01, district02 }, homeAnchor, marketAnchor);

            Assert.IsTrue(registry.ValidateWorldData(out var issues), $"Expected valid world data, but got: {string.Join("; ", issues)}");
            Assert.IsFalse(district01.IsNameApproved);
            Assert.IsFalse(homeLocSO.IsNameApproved);
            Assert.AreEqual(2, homeLocSO.FloorCount);
            Assert.AreSame(nodeHome, registry.GetAccessNodeForLocation("location_001"));
            Assert.AreSame(nodeMarket, registry.GetAccessNodeForLocation("location_003"));
        }

        [Test]
        public void ProductionScene_OutsiderCity_LoadsAndSupportsOutboundReturnAndAlternativeTaxiJourneys()
        {
            const string scenePath = "Assets/_Project/Scenes/OutsiderCity.unity";
            if (!System.IO.File.Exists(scenePath))
            {
                Assert.Ignore($"Scene {scenePath} not yet saved on disk.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid());

            var registry = Object.FindAnyObjectByType<CityWorldRegistry>();
            var roadNetwork = Object.FindAnyObjectByType<RoadNetwork>();
            var neighborhoodLayout = Object.FindAnyObjectByType<HomeNeighborhoodLayout>();
            var taxi = Object.FindAnyObjectByType<VehicleController>();

            Assert.IsNotNull(registry, "OutsiderCity scene must contain a CityWorldRegistry.");
            Assert.IsNotNull(roadNetwork, "OutsiderCity scene must contain a RoadNetwork.");
            Assert.IsNotNull(neighborhoodLayout, "OutsiderCity scene must contain a HomeNeighborhoodLayout.");
            Assert.IsNotNull(taxi, "OutsiderCity scene must contain the player Taxi VehicleController.");
            Assert.IsNull(taxi.GetComponent<TaxiTestDriver>(), "Production OutsiderCity scene must NOT attach the legacy TaxiTestDriver harness.");

            // Validate data contract and road topology
            Assert.IsTrue(registry.ValidateWorldData(out var validationIssues),
                $"OutsiderCity world validation failed: {string.Join("; ", validationIssues)}");

            // Verify isometric tilemaps are populated with authored tiles
            Assert.IsNotNull(neighborhoodLayout.IsometricGrid);
            Assert.AreEqual(GridLayout.CellLayout.IsometricZAsY, neighborhoodLayout.IsometricGrid.cellLayout);
            Assert.IsNotNull(neighborhoodLayout.StreetSurfaceTilemap);
            Assert.Greater(neighborhoodLayout.StreetSurfaceTilemap.GetUsedTilesCount(), 0, "StreetSurfaceTilemap must contain authored isometric road tiles.");

            // Verify key locations: location_001 (Home/Garage in district_01) and location_003 (Commercial Market in district_02)
            RoadNode homeNode = registry.GetAccessNodeForLocation("location_001");
            RoadNode commercialNode = registry.GetAccessNodeForLocation("location_003");
            Assert.IsNotNull(homeNode, "Home/Garage location_001 must resolve to an authored RoadNode.");
            Assert.IsNotNull(commercialNode, "Commercial destination location_003 must resolve to an authored RoadNode.");
            Assert.AreNotEqual(homeNode, commercialNode);

            // 1. Outbound journey: Home/Garage -> Commercial Destination (Primary Route)
            taxi.transform.position = homeNode.Position;
            var primaryRoute = roadNetwork.FindPath(homeNode, commercialNode);
            Assert.GreaterOrEqual(primaryRoute.Count, 4, "Primary route should traverse multiple connected street segments.");
            Assert.IsTrue(registry.NavigateVehicleToLocation(taxi, "location_003", useAlternativeRoute: false));
            SimulateVehicleUntilArrived(taxi, maxSteps: 1200);
            Assert.LessOrEqual(Vector2.Distance(taxi.transform.position, commercialNode.Position), 0.35f, "Taxi should arrive at commercial destination.");

            // 2. Return journey: Commercial Destination -> Home/Garage
            Assert.IsTrue(registry.NavigateVehicleToLocation(taxi, "location_001", useAlternativeRoute: false));
            SimulateVehicleUntilArrived(taxi, maxSteps: 1200);
            Assert.LessOrEqual(Vector2.Distance(taxi.transform.position, homeNode.Position), 0.35f, "Taxi should complete return journey to Home/Garage.");

            // 3. Alternative route journey: Home/Garage -> Commercial Destination via Alternative Route
            var altRoute = roadNetwork.FindAlternativePath(homeNode, commercialNode, primaryRoute);
            Assert.GreaterOrEqual(altRoute.Count, 4, "At least one meaningful alternative route must exist between Home/Garage and Commercial Destination.");
            CollectionAssert.AreNotEqual(primaryRoute, altRoute);

            Assert.IsTrue(registry.NavigateVehicleToLocation(taxi, "location_003", useAlternativeRoute: true));
            SimulateVehicleUntilArrived(taxi, maxSteps: 1500);
            Assert.LessOrEqual(Vector2.Distance(taxi.transform.position, commercialNode.Position), 0.35f, "Taxi should arrive at commercial destination via the alternative route.");
        }

        private static void SimulateVehicleUntilArrived(VehicleController vehicle, int maxSteps)
        {
            for (int i = 0; i < maxSteps && vehicle.HasActiveRoute; i++)
            {
                vehicle.TickMovement(0.05f);
            }
        }

        private GameObject CreateTrackedObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawnedObjects.Add(go);
            return go;
        }

        private RoadNode CreateNode(
            Transform parent,
            string nodeId,
            Vector3 position,
            RoadPieceType pieceType,
            int elevation = 0,
            bool isRamp = false)
        {
            var go = CreateTrackedObject(nodeId, position);
            go.transform.SetParent(parent, false);
            var node = go.AddComponent<RoadNode>();
            node.Configure(nodeId, RoadNodePriority.MainArtery, pieceType, "district_01", elevation, isRamp);
            return node;
        }

        private static void ConnectBidirectional(RoadNode a, RoadNode b)
        {
            a.AddConnection(b);
            b.AddConnection(a);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            _spawnedObjects.Add(instance);
            return instance;
        }
    }
}
