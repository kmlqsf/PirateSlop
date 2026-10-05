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
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkPlayer.prefab");
            if (config == null || ship == null || player == null || !File.Exists(MenuPath) || !File.Exists(OceanPath))
                throw new InvalidOperationException("Multiplayer scenes, config and prefabs must exist.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            try
            {
                var menu = EditorSceneManager.OpenScene(MenuPath);
                var session = UnityEngine.Object.FindFirstObjectByType<SessionController>();
                if (session == null) throw new InvalidOperationException("SessionController missing in NetworkMenu.");
                session.Config = config; session.ShipPrefab = ship.GetComponent<NetworkObject>(); session.PlayerPrefab = player.GetComponent<NetworkObject>();
                config.GameScene = "NetworkOcean";
                config.PlayerLocalSpawn = ship.transform.InverseTransformPoint(ship.GetComponent<PirateSlop.Ships.ShipV3Features>().RespawnPoint.position);
                config.ShipComparisonEnabled = false;
                EditorUtility.SetDirty(config);
                EditorSceneManager.MarkSceneDirty(menu); EditorSceneManager.SaveScene(menu);
                EditorBuildSettings.scenes = System.Array.ConvertAll(GetBuildScenePaths(), path => new EditorBuildSettingsScene(path, true));
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        [MenuItem("PirateSlop/Multiplayer/Use Ship V3")]
        public static void PromoteShipV3()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MenuPath || scene.isDirty)
                throw new InvalidOperationException("Open the saved NetworkMenu scene outside Play Mode first.");
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
            var session = UnityEngine.Object.FindFirstObjectByType<SessionController>();
            var backdrop = UnityEngine.Object.FindFirstObjectByType<PirateSlop.MenuBackdrop>();
            if (ship == null || session == null || session.Config == null || backdrop == null)
                throw new InvalidOperationException("Ship V3, session config or menu backdrop is missing.");
            var features = ship.GetComponent<PirateSlop.Ships.ShipV3Features>();
            var networkObject = ship.GetComponent<NetworkObject>();
            if (features == null || features.RespawnPoint == null || networkObject == null)
                throw new InvalidOperationException("Ship V3 gameplay bindings are incomplete.");
            MenuPresentationSetup.ReplaceShip(backdrop, ship);
            session.ShipPrefab = networkObject;
            session.Config.PlayerLocalSpawn = ship.transform.InverseTransformPoint(features.RespawnPoint.position);
            session.Config.ShipComparisonEnabled = false;
            session.Config.ComparisonShips = Array.Empty<PirateSlop.World.ShipComparison>();
            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(session.Config);
            AssetDatabase.SaveAssetIfDirty(session.Config);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("NetworkMenu could not be saved.");
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

        static string[] GetBuildScenePaths()
        {
            var paths = new System.Collections.Generic.List<string>
            {
                MenuPath,
                OceanPath,
                "Assets/Scenes/NetworkLoadTest.unity",
                "Assets/Scenes/BoatAttackWaterTest.unity",
                "Assets/Scenes/OceanaWaterTest.unity"
            };
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && !paths.Contains(scene.path)) paths.Add(scene.path);
            foreach (var path in paths)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new InvalidOperationException("Build scene is missing: " + path);
            return paths.ToArray();
        }
        [MenuItem("PirateSlop/Multiplayer/Build Windows")]
        public static void Build()
        {
            if (!File.Exists(MenuPath)) throw new InvalidOperationException("Configure multiplayer scenes first.");
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = GetBuildScenePaths(), locationPathName = "Builds/Windows/PirateSlop.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | BuildOptions.StrictMode | BuildOptions.CleanBuildCache | BuildOptions.DetailedBuildReport });
            File.WriteAllText("Temp/multiplayer-build-result.txt", report.summary.result + " errors=" + report.summary.totalErrors + " size=" + report.summary.totalSize);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Multiplayer build failed.");
            Debug.Log("MULTIPLAYER_BUILD_OK");
        }
    }
}
