using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using VContainer.Unity;
using OutsiderDriver.Core;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Editor
{
    public static class TaxiSandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/TaxiSandbox.unity";
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string TimeConfigPath = "Assets/_Project/Settings/TimeConfig.asset";

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

            // 4. Road Network
            var networkGo = new GameObject("RoadNetwork");
            var roadNetwork = networkGo.AddComponent<RoadNetwork>();

            // Create nodes in 2:1 isometric arrangement
            var homeNode = CreateNode("HomeGarage", new Vector3(0f, 0f, 0f), networkGo.transform);
            var centralNode = CreateNode("CentralJunction", new Vector3(6f, 3f, 0f), networkGo.transform);
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

            // 5. Taxi
            var taxiGo = new GameObject("Taxi");
            taxiGo.transform.position = homeNode.transform.position;

            taxiGo.AddComponent<VehicleController>();
            taxiGo.AddComponent<TaxiTestDriver>();

            // Visual representation
            var visualsGo = new GameObject("Visuals");
            visualsGo.transform.SetParent(taxiGo.transform, false);
            var sr = visualsGo.AddComponent<SpriteRenderer>();

            var defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (defaultSprite == null)
            {
                defaultSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
            sr.sprite = defaultSprite;
            sr.color = new Color(1f, 0.78f, 0.28f); // Warm retro-futurist taxi yellow/amber
            visualsGo.transform.localScale = new Vector3(0.7f, 0.5f, 1f);

            // 6. Cinemachine 3 Virtual Camera
            var vcamGo = new GameObject("Cinemachine Camera (Taxi)");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Target.TrackingTarget = taxiGo.transform;
            vcam.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            vcam.Lens.OrthographicSize = 5f;
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 0f, -10f);

            // 7. Scene Lifetime Scope (CitySceneLifetimeScope)
            var sceneScopeGo = new GameObject("CitySceneLifetimeScope");
            var cityScope = sceneScopeGo.AddComponent<CitySceneLifetimeScope>();
            cityScope.parentReference = ParentReference.Create<GameLifetimeScope>();
            SetPrivateField(cityScope, "autoInjectGameObjects", new List<GameObject> { networkGo, taxiGo });

            // Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TaxiSandboxBuilder] Successfully generated and saved scene at {ScenePath}");
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
