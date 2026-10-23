using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Editor
{
    public static class TaxiSandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/TaxiSandbox.unity";

        [MenuItem("Outsider Driver/Build Taxi Sandbox Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f); // Soft alien dusk navy
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var camFollow = camGo.AddComponent<IsometricCameraFollow>();
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // 2. Global 2D Light
            var lightGo = new GameObject("Global Light 2D");
            var light2D = lightGo.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.color = new Color(0.95f, 0.92f, 0.88f); // Warm alien daylight
            light2D.intensity = 1.0f;

            // 3. Road Network
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

            // 4. Taxi
            var taxiGo = new GameObject("Taxi");
            taxiGo.transform.position = homeNode.transform.position;

            var vehicle = taxiGo.AddComponent<VehicleController>();
            var driver = taxiGo.AddComponent<TaxiTestDriver>();

            // Visual representation
            var visualsGo = new GameObject("Visuals");
            visualsGo.transform.SetParent(taxiGo.transform, false);
            var sr = visualsGo.AddComponent<SpriteRenderer>();

            // Use built-in Unity 2D sprite
            var defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (defaultSprite == null)
            {
                defaultSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
            sr.sprite = defaultSprite;
            sr.color = new Color(1f, 0.78f, 0.28f); // Warm retro-futurist taxi yellow/amber
            visualsGo.transform.localScale = new Vector3(0.7f, 0.5f, 1f); // Flattened 2:1 isometric vehicle footprint

            // Link camera
            camFollow.Target = taxiGo.transform;
            camFollow.SnapToTarget();

            // Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TaxiSandboxBuilder] Successfully generated and saved scene at {ScenePath}");
        }

        private static RoadNode CreateNode(string id, Vector3 position, Transform parent)
        {
            var go = new GameObject($"Node_{id}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var node = go.AddComponent<RoadNode>();

            var field = typeof(RoadNode).GetField("_nodeId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(node, id);
            }

            return node;
        }

        private static void ConnectBidirectional(RoadNode a, RoadNode b)
        {
            a.AddConnection(b);
            b.AddConnection(a);
            EditorUtility.SetDirty(a);
            EditorUtility.SetDirty(b);
        }
    }
}
