using UnityEditor;
using UnityEditor.SceneManagement;

namespace PirateSlop.EditorTools
{
    [InitializeOnLoad]
    public static class MultiplayerStartup
    {
        static MultiplayerStartup()
        {
            EditorApplication.delayCall += () =>
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/NetworkMenu.unity");
                if (scene != null) EditorSceneManager.playModeStartScene = scene;
            };
        }
    }
}
