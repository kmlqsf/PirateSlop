using System;
using System.Linq;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace PirateSlop.EditorTools
{
    public static class NetworkLoadTestSetup
    {
        public const string ScenePath = "Assets/Scenes/NetworkLoadTest.unity";

        [MenuItem("PirateSlop/Diagnostics/Prepare Network Load Test")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before preparing the scene.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset("Assets/Scenes/NetworkMenu.unity", ScenePath))
                throw new InvalidOperationException("Could not copy the network entry scene.");
            var original = SceneManager.GetActiveScene();
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.isLoaded && loaded.isDirty) throw new InvalidOperationException("Save the load test scene before preparing it again.");
            var scene = loaded.isLoaded ? loaded : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var session = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var data = new SerializedObject(session);
                data.FindProperty("LoadTestOnStart").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save the load test scene.");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (!loaded.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(entry => entry.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
