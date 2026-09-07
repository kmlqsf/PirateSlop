using UnityEngine;

namespace PirateSlop
{
    public sealed class GameVersionOverlay : MonoBehaviour
    {
        string version;
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindFirstObjectByType<GameVersionOverlay>() != null) return;
            var overlay = new GameObject("GameVersionOverlay");
            DontDestroyOnLoad(overlay);
            overlay.AddComponent<GameVersionOverlay>();
        }

        void Awake()
        {
            var asset = Resources.Load<TextAsset>("GeneratedGameVersion");
            version = asset != null ? asset.text.Trim() : Application.version + " · unknown";
        }

        void OnGUI()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 13 };
            var color = GUI.color;
            var rect = new Rect(8, Screen.height - 28, Screen.width - 20, 22);
            GUI.color = new Color(0, 0, 0, .85f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), "v" + version, style);
            GUI.color = new Color(1, 1, 1, .8f);
            GUI.Label(rect, "v" + version, style);
            GUI.color = color;
        }
    }
}
