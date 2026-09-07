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
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkShip.prefab");
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
                EditorUtility.SetDirty(config);
                EditorSceneManager.MarkSceneDirty(menu); EditorSceneManager.SaveScene(menu);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuPath, true), new EditorBuildSettingsScene(OceanPath, true) };
                AssetDatabase.SaveAssets();
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
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { MenuPath, OceanPath }, locationPathName = "Builds/Windows/PirateSlop.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | BuildOptions.StrictMode | BuildOptions.CleanBuildCache });
            File.WriteAllText("Temp/multiplayer-build-result.txt", report.summary.result + " errors=" + report.summary.totalErrors + " size=" + report.summary.totalSize);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Multiplayer build failed.");
            Debug.Log("MULTIPLAYER_BUILD_OK");
        }
    }
}
