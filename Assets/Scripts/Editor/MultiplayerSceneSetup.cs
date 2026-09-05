using System;
using System.IO;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Object;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Managing.Predicting;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Transporting.Tugboat;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace PirateSlop.EditorTools
{
    public static class MultiplayerSceneSetup
    {
        const string MenuPath = "Assets/Scenes/NetworkMenu.unity", OceanPath = "Assets/Scenes/NetworkOcean.unity";
        [MenuItem("PirateSlop/Multiplayer/Configure Scenes")]
        public static void Configure()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your unsaved scene before configuring multiplayer.");
            Directory.CreateDirectory("Assets/Prefabs/Networking"); Directory.CreateDirectory("Assets/Settings/Networking");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                var originalShip = GameObject.Find("SM_PirateSloop"); var originalPlayer = GameObject.Find("PlayerCharacter");
                if (originalShip == null || originalPlayer == null) throw new InvalidOperationException("SampleScene ship/player missing.");
                var config = LoadOrCreate<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
                config.ShipOrigin = originalShip.transform.position;
                config.PlayerLocalSpawn = originalShip.transform.InverseTransformPoint(originalPlayer.transform.position);
                var bounds = new Bounds(originalShip.transform.position, Vector3.zero);
                foreach (var c in originalShip.GetComponentsInChildren<Collider>()) if (c.enabled) bounds.Encapsulate(c.bounds);
                config.SpawnSpacing = Mathf.Max(bounds.size.x, bounds.size.z) + 20;
                var observer = LoadOrCreate<ShipObserverCondition>("Assets/Settings/Networking/ShipObserver.asset");
                var ship = UnityEngine.Object.Instantiate(originalShip); ship.name = "NetworkShip"; Unpack(ship);
                ship.transform.position = Vector3.zero; ship.transform.rotation = Quaternion.identity;
                var sn = ship.AddComponent<NetworkObject>(); ship.AddComponent<NetworkShip>(); ConfigureObserver(ship, observer);
                var shipPrefab = PrefabUtility.SaveAsPrefabAsset(ship, "Assets/Prefabs/Networking/NetworkShip.prefab"); UnityEngine.Object.DestroyImmediate(ship);
                var player = UnityEngine.Object.Instantiate(originalPlayer); player.name = "NetworkPlayer"; Unpack(player); player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var pn = player.AddComponent<NetworkObject>(); player.AddComponent<NetworkPlayer>(); ConfigureObserver(player, observer);
                var graphics = new GameObject("PlayerGraphics").transform; graphics.SetParent(player.transform, false);
                var camera = player.GetComponentInChildren<Camera>(true); camera.transform.SetParent(graphics, true); camera.enabled = false;
                var listener = camera.GetComponent<AudioListener>(); if (listener != null) listener.enabled = false;
                var visual = player.transform.Find("PirateVisual"); if (visual != null) visual.SetParent(graphics, true);
                var pred = new SerializedObject(pn); pred.FindProperty("_enablePrediction").boolValue = true;
                pred.FindProperty("_graphicalObject").objectReferenceValue = graphics;
                // Observers use NetworkPlayer snapshots, including visual smoothing.
                pred.FindProperty("_enableStateForwarding").boolValue = false;
                pred.ApplyModifiedPropertiesWithoutUndo();
                var playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Networking/NetworkPlayer.prefab"); UnityEngine.Object.DestroyImmediate(player);
                EditorUtility.SetDirty(config);
                var prefabs = LoadOrCreate<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
                prefabs.Clear(); prefabs.AddObject(shipPrefab.GetComponent<NetworkObject>()); prefabs.AddObject(playerPrefab.GetComponent<NetworkObject>()); prefabs.InitializePrefabRange(0); EditorUtility.SetDirty(prefabs);
                // Copy the baseline environment once. Reconfiguration preserves authored network-environment changes.
                if (!File.Exists(OceanPath)) AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", OceanPath);
                var ocean = EditorSceneManager.OpenScene(OceanPath);
                var oldPlayer = GameObject.Find("PlayerCharacter"); if (oldPlayer != null) UnityEngine.Object.DestroyImmediate(oldPlayer);
                var oldShip = GameObject.Find("SM_PirateSloop"); if (oldShip != null) UnityEngine.Object.DestroyImmediate(oldShip);
                var water = GameObject.Find("OceanWater");
                if (water != null) water.transform.localScale = new Vector3(Mathf.Max(water.transform.localScale.x, 500), water.transform.localScale.y, Mathf.Max(water.transform.localScale.z, 500));
                EditorSceneManager.SaveScene(ocean);
                var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var managerObject = new GameObject("Session"); managerObject.SetActive(false);
                managerObject.AddComponent<SessionAuthenticator>(); managerObject.AddComponent<Tugboat>(); managerObject.AddComponent<TransportManager>();
                managerObject.AddComponent<TimeManager>(); managerObject.AddComponent<PredictionManager>(); managerObject.AddComponent<ServerManager>(); managerObject.AddComponent<ClientManager>();
                var nm = managerObject.AddComponent<NetworkManager>(); nm.SpawnablePrefabs = prefabs;
                var session = managerObject.AddComponent<SessionController>(); session.Config = config; session.ShipPrefab = shipPrefab.GetComponent<NetworkObject>(); session.PlayerPrefab = playerPrefab.GetComponent<NetworkObject>();
                managerObject.AddComponent<SessionMetrics>();
                var cam = new GameObject("MenuCamera").AddComponent<Camera>(); cam.gameObject.AddComponent<AudioListener>(); cam.backgroundColor = new Color(.025f,.065f,.10f); cam.clearFlags = CameraClearFlags.SolidColor; session.MenuCamera = cam;
                var light = new GameObject("MenuLight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = .5f;
                managerObject.SetActive(true);
                EditorSceneManager.SaveScene(menu, MenuPath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuPath, true), new EditorBuildSettingsScene(OceanPath, true) };
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                File.WriteAllText("Temp/multiplayer-setup.json", JsonUtility.ToJson(config, true));
                Debug.Log("MULTIPLAYER_SETUP_OK");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        static void Unpack(GameObject go) { if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); }
        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var result = AssetDatabase.LoadAssetAtPath<T>(path); if (result != null) return result;
            result = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(result, path); return result;
        }
        static void ConfigureObserver(GameObject go, ObserverCondition condition)
        {
            var no = go.AddComponent<NetworkObserver>(); var so = new SerializedObject(no); var list = so.FindProperty("_observerConditions"); list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = condition; so.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("PirateSlop/Multiplayer/Build Windows")]
        public static void Build()
        {
            if (!File.Exists(MenuPath)) throw new InvalidOperationException("Configure multiplayer scenes first.");
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { MenuPath, OceanPath }, locationPathName = "Builds/Windows/PirateSlop.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | BuildOptions.StrictMode });
            File.WriteAllText("Temp/multiplayer-build-result.txt", report.summary.result + " errors=" + report.summary.totalErrors + " size=" + report.summary.totalSize);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Multiplayer build failed.");
            Debug.Log("MULTIPLAYER_BUILD_OK");
        }
    }
}
