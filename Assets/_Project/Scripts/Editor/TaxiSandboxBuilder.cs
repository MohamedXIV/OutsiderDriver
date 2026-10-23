using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Unity.Cinemachine;
using VContainer.Unity;
using OutsiderDriver.Audio;
using OutsiderDriver.Core;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Navigation;
using OutsiderDriver.Player;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Editor
{
    public static class TaxiSandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/TaxiSandbox.unity";
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string TimeConfigPath = "Assets/_Project/Settings/TimeConfig.asset";
        private const string AmberWaveStationPath = "Assets/_Project/Settings/RadioStation_AmberWave.asset";
        private const string VesperChimesStationPath = "Assets/_Project/Settings/RadioStation_VesperChimes.asset";

        [MenuItem("Outsider Driver/Build Taxi Sandbox Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ensure TimeConfigSO exists
            var timeConfig = EnsureTimeConfigAsset();

            // 1. Root Kernel Scope (GameLifetimeScope)
            var rootScopeGo = new GameObject("GameLifetimeScope");
            var gameScope = rootScopeGo.AddComponent<GameLifetimeScope>();
            SetPrivateField(gameScope, "_timeConfig", timeConfig);

            // 2. Camera & Cinemachine 3 Brain
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f); // Soft alien dusk navy
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<CinemachineBrain>();
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // 3. Global 2D Light
            var lightGo = new GameObject("Global Light 2D");
            var light2D = lightGo.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.color = new Color(0.95f, 0.92f, 0.88f); // Warm alien daylight
            light2D.intensity = 1.0f;

            // 4. Authored Walkable Home & Starter Street Layout (Native 2D Isometric Grid & Tilemaps)
            var neighborhoodGo = BuildHomeNeighborhoodLayout();

            // 5. Road Network
            var networkGo = new GameObject("RoadNetwork");
            var roadNetwork = networkGo.AddComponent<RoadNetwork>();

            // Create nodes in 2:1 isometric arrangement aligned with the starter neighborhood street
            var homeNode = CreateNode("HomeGarage", new Vector3(0f, 0f, 0f), networkGo.transform);
            var centralNode = CreateNode("CentralJunction", new Vector3(6f, 3f, 0f), networkGo.transform);
            centralNode.Priority = RoadNodePriority.YieldIntersection;
            var marketNode = CreateNode("MarketSquare", new Vector3(12f, 0f, 0f), networkGo.transform);
            var transitNode = CreateNode("TransitTerminal", new Vector3(6f, -3f, 0f), networkGo.transform);
            var vistaNode = CreateNode("RiversideVista", new Vector3(0f, 6f, 0f), networkGo.transform);

            // Connect nodes bidirectionally
            ConnectBidirectional(homeNode, centralNode);
            ConnectBidirectional(centralNode, marketNode);
            ConnectBidirectional(marketNode, transitNode);
            ConnectBidirectional(transitNode, homeNode);
            ConnectBidirectional(centralNode, vistaNode);

            roadNetwork.RebuildNetwork();

            // 6. Taxi
            var taxiGo = new GameObject("Taxi");
            taxiGo.transform.position = homeNode.transform.position;

            var taxiVehicle = taxiGo.AddComponent<VehicleController>();
            taxiGo.AddComponent<TaxiTestDriver>();

            // Visual representation
            var visualsGo = new GameObject("Visuals");
            visualsGo.transform.SetParent(taxiGo.transform, false);
            var sr = visualsGo.AddComponent<SpriteRenderer>();

            var defaultSprite = GetDefaultSprite();
            sr.sprite = defaultSprite;
            sr.color = new Color(1f, 0.78f, 0.28f); // Warm retro-futurist taxi yellow/amber
            visualsGo.transform.localScale = new Vector3(0.7f, 0.5f, 1f);

            // 7. Traffic Director (GTA-style camera bubble)
            var trafficGo = new GameObject("TrafficDirector");
            var trafficDirector = trafficGo.AddComponent<TrafficDirector>();
            trafficDirector.BubbleCenter = taxiGo.transform;

            // 8. Driver On-Foot Actor (Bounded Home & Starter Street Scope - Decision P-009)
            var neighborhoodLayout = neighborhoodGo.GetComponent<HomeNeighborhoodLayout>();
            var driverGo = new GameObject("Driver_OnFoot");
            driverGo.transform.position = neighborhoodLayout.StreetSidewalkSpawnPoint != null
                ? neighborhoodLayout.StreetSidewalkSpawnPoint.position
                : new Vector3(-2.5f, 1.5f, 0f);

            var playerController = driverGo.AddComponent<PlayerController>();

            var driverVisualsGo = new GameObject("DriverVisuals");
            driverVisualsGo.transform.SetParent(driverGo.transform, false);
            var driverSr = driverVisualsGo.AddComponent<SpriteRenderer>();
            driverSr.sprite = defaultSprite;
            driverSr.color = new Color(0.95f, 0.48f, 0.36f); // Warm coral coat
            driverVisualsGo.transform.localScale = new Vector3(0.42f, 0.62f, 1f);

            // 9. Cinemachine 3 Virtual Cameras (Taxi & On-Foot with priority blending)
            var taxiVcamGo = new GameObject("Cinemachine Camera (Taxi)");
            var taxiVcam = taxiVcamGo.AddComponent<CinemachineCamera>();
            taxiVcam.Target.TrackingTarget = taxiGo.transform;
            taxiVcam.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            taxiVcam.Lens.OrthographicSize = 5f;
            taxiVcam.Priority.Value = PlayerController.ActiveCameraPriority;
            var taxiFollow = taxiVcamGo.AddComponent<CinemachineFollow>();
            taxiFollow.FollowOffset = new Vector3(0f, 0f, -10f);

            var onFootVcamGo = new GameObject("Cinemachine Camera (OnFoot)");
            var onFootVcam = onFootVcamGo.AddComponent<CinemachineCamera>();
            onFootVcam.Target.TrackingTarget = driverGo.transform;
            onFootVcam.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            onFootVcam.Lens.OrthographicSize = 3.75f;
            onFootVcam.Priority.Value = PlayerController.InactiveCameraPriority;
            var onFootFollow = onFootVcamGo.AddComponent<CinemachineFollow>();
            onFootFollow.FollowOffset = new Vector3(0f, 0f, -10f);

            playerController.Construct(neighborhoodLayout, taxiVehicle, trafficDirector);
            playerController.AssignVisualsRoot(driverVisualsGo);
            playerController.AssignCameras(taxiVcam, onFootVcam);

            // 10. Alien Radio Station & Ambient City Soundscape Controller
            var radioStations = EnsureDefaultRadioStations();
            var radioGo = new GameObject("RadioAudioController");
            var radioController = radioGo.AddComponent<RadioAudioController>();
            radioController.ConfigureStations(radioStations, startOn: true);

            // 11. Scene Lifetime Scope (CitySceneLifetimeScope)
            var sceneScopeGo = new GameObject("CitySceneLifetimeScope");
            var cityScope = sceneScopeGo.AddComponent<CitySceneLifetimeScope>();
            cityScope.parentReference = ParentReference.Create<GameLifetimeScope>();
            SetPrivateField(cityScope, "autoInjectGameObjects", new List<GameObject>
            {
                neighborhoodGo,
                networkGo,
                taxiGo,
                trafficGo,
                driverGo,
                radioGo
            });

            // Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TaxiSandboxBuilder] Successfully generated and saved scene at {ScenePath}");
        }

        private static GameObject BuildHomeNeighborhoodLayout()
        {
            var layoutRoot = new GameObject("HomeNeighborhoodLayout");
            var layout = layoutRoot.AddComponent<HomeNeighborhoodLayout>();
            layout.EnsureIsometricGridConfigured();

            Grid isoGrid = layout.IsometricGrid;

            // Create Isometric Tilemaps (Street Surface, Home Floor, Obstacles/Walls)
            var streetTilemap = CreateIsometricTilemap(isoGrid.transform, "StreetSurface_Tilemap", sortingOrder: -20);
            var homeFloorTilemap = CreateIsometricTilemap(isoGrid.transform, "HomeInterior_Tilemap", sortingOrder: -15);
            var obstaclesTilemap = CreateIsometricTilemap(isoGrid.transform, "WallsAndCurbs_Tilemap", sortingOrder: 0);
            obstaclesTilemap.gameObject.AddComponent<TilemapCollider2D>();

            layout.AssignTilemaps(streetTilemap, homeFloorTilemap, obstaclesTilemap);

            // Bounded Walkable Zones (Decision P-009)
            var homeZoneGo = new GameObject("Zone_HomeInterior");
            homeZoneGo.transform.SetParent(layoutRoot.transform, false);
            homeZoneGo.transform.position = new Vector3(-4.5f, 2.5f, 0f);
            var homeZone = homeZoneGo.AddComponent<WalkableZone>();
            homeZone.Configure("zone_home_interior", "Driver's Apartment", WalkableZoneType.HomeInterior, new Rect(-3f, -2f, 6f, 4f));

            var streetZoneGo = new GameObject("Zone_StarterStreetSidewalk");
            streetZoneGo.transform.SetParent(layoutRoot.transform, false);
            streetZoneGo.transform.position = new Vector3(-1f, 0.5f, 0f);
            var streetZone = streetZoneGo.AddComponent<WalkableZone>();
            streetZone.Configure("zone_starter_sidewalk", "Amber Terrace Curbside", WalkableZoneType.NeighborhoodSidewalk, new Rect(-4f, -2.5f, 8f, 5f));

            // Key Spawn Anchors & Domestic Interactables
            Sprite propSprite = GetDefaultSprite();

            var homeSpawnGo = CreatePropWithInteractable(
                layoutRoot.transform,
                "HomeBed_Interactable",
                new Vector3(-5.5f, 3.2f, 0f),
                new Color(0.88f, 0.52f, 0.38f), // Warm terracotta bedding
                propSprite,
                "interact_home_bed",
                "Rest / Pass Time",
                InteractableKind.HomeBed);

            CreatePropWithInteractable(
                layoutRoot.transform,
                "TranslationBench_Interactable",
                new Vector3(-3.5f, 3.0f, 0f),
                new Color(0.42f, 0.78f, 0.72f), // Retro-futurist teal console
                propSprite,
                "interact_translation_bench",
                "Translator & Radio Console",
                InteractableKind.TranslationWorkbench);

            var sidewalkSpawnGo = CreatePropWithInteractable(
                layoutRoot.transform,
                "ApartmentDoor_Interactable",
                new Vector3(-2.5f, 1.5f, 0f),
                new Color(0.92f, 0.76f, 0.45f), // Ochre doorway threshold
                propSprite,
                "interact_apartment_door",
                "Enter / Exit Apartment",
                InteractableKind.DoorwayThreshold);

            var taxiBayGo = CreatePropWithInteractable(
                layoutRoot.transform,
                "ParkedTaxiBay_Interactable",
                new Vector3(0f, 0f, 0f),
                new Color(0.95f, 0.82f, 0.32f), // Amber garage bay marker
                propSprite,
                "interact_taxi_bay",
                "Board / Exit Taxi",
                InteractableKind.ParkedTaxiBay);

            layout.AssignAnchors(homeSpawnGo.transform, sidewalkSpawnGo.transform, taxiBayGo.transform);
            layout.RebuildLayoutRegistry();

            return layoutRoot;
        }

        private static Tilemap CreateIsometricTilemap(Transform gridParent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(gridParent, false);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
            renderer.mode = TilemapRenderer.Mode.Individual;
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static GameObject CreatePropWithInteractable(
            Transform parent,
            string objectName,
            Vector3 position,
            Color tint,
            Sprite sprite,
            string interactableId,
            string promptLabel,
            InteractableKind kind)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;

            var interactable = go.AddComponent<NeighborhoodInteractable>();
            interactable.Configure(interactableId, promptLabel, kind, interactionRadius: 1.35f);
            return go;
        }

        private static Sprite GetDefaultSprite()
        {
            var defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (defaultSprite == null)
            {
                defaultSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
            return defaultSprite;
        }

        private static TimeConfigSO EnsureTimeConfigAsset()
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
            }

            var existing = AssetDatabase.LoadAssetAtPath<TimeConfigSO>(TimeConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<TimeConfigSO>();
            AssetDatabase.CreateAsset(config, TimeConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static List<RadioStationSO> EnsureDefaultRadioStations()
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
            }

            var amberWave = AssetDatabase.LoadAssetAtPath<RadioStationSO>(AmberWaveStationPath);
            if (amberWave == null)
            {
                amberWave = ScriptableObject.CreateInstance<RadioStationSO>();
                amberWave.Configure(
                    "station_amber_wave",
                    "Amber Wave Transit Radio",
                    "94.6 MHz",
                    "Common",
                    new[]
                    {
                        new RadioProgramEntry(
                            "prog_day_commute",
                            "Daylight Bridge & Canal Bulletin",
                            "Traffic flowing smoothly along Central Junction and Market Square.",
                            "Common",
                            6,
                            17),
                        new RadioProgramEntry(
                            "prog_dusk_synth",
                            "Warm Dusk Brass & Glass",
                            "Evening melodies drifting over Riverside Vista.",
                            "Common",
                            18,
                            5)
                    });
                AssetDatabase.CreateAsset(amberWave, AmberWaveStationPath);
            }

            var vesperChimes = AssetDatabase.LoadAssetAtPath<RadioStationSO>(VesperChimesStationPath);
            if (vesperChimes == null)
            {
                vesperChimes = ScriptableObject.CreateInstance<RadioStationSO>();
                vesperChimes.Configure(
                    "station_vesper_chimes",
                    "Vesperian Cultural Frequency",
                    "108.2 MHz",
                    "Local",
                    new[]
                    {
                        new RadioProgramEntry(
                            "prog_vesper_poetry",
                            "Old Terrace Recitations",
                            "Kaelor vesh tarnak solen mira.",
                            "Local",
                            0,
                            23)
                    });
                AssetDatabase.CreateAsset(vesperChimes, VesperChimesStationPath);
            }

            AssetDatabase.SaveAssets();
            return new List<RadioStationSO> { amberWave, vesperChimes };
        }

        private static RoadNode CreateNode(string id, Vector3 position, Transform parent)
        {
            var go = new GameObject($"Node_{id}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var node = go.AddComponent<RoadNode>();
            SetPrivateField(node, "_nodeId", id);
            return node;
        }

        private static void ConnectBidirectional(RoadNode a, RoadNode b)
        {
            a.AddConnection(b);
            b.AddConnection(a);
            EditorUtility.SetDirty(a);
            EditorUtility.SetDirty(b);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
        }
    }
}
