using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class BakeoffLauncher
{
    const string Key = "StormBakeoff.PreviousStartScene";
    static BakeoffLauncher()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            string previous = SessionState.GetString(Key, "__none__");
            if (previous == "__none__") return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.EraseString(Key);
        };
    }
    [MenuItem("PirateSlop/Tests/Run Cloud Bakeoff")]
    static void Run()
    {
        SessionState.SetString(Key, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Tests/StormCloudBakeoff/StormCloudBakeoff.unity");
        EditorApplication.isPlaying = true;
    }
}
