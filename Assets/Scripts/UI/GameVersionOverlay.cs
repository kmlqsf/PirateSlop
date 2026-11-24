using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    public sealed class GameVersionOverlay : MonoBehaviour
    {
        public static bool InterfaceHidden { get; private set; }
        public static Key CaptureKey
        {
            get
            {
                var session = Networking.SessionController.Instance;
                return DeveloperMenu.Available && session != null && session.Config != null && session.Config.ShipComparisonEnabled && !session.ObserverActive ? Key.F6 : Key.F4;
            }
        }

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
            InterfaceHidden = false;
            var asset = Resources.Load<TextAsset>("GeneratedGameVersion");
            version = asset != null ? asset.text.Trim() : Application.version + " · unknown";
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || keyboard.altKey.isPressed) return;
            if (keyboard[CaptureKey].wasPressedThisFrame) InterfaceHidden = !InterfaceHidden;
        }

        void OnGUI()
        {
            if (PirateSlop.GameVersionOverlay.InterfaceHidden) return;
            if (Event.current.type != EventType.Repaint) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 13 };
            var color = GUI.color;
            var rect = new Rect(8, Screen.height - 28, Screen.width - 20, 22);
            bool menu=Networking.SessionController.MenuOpen;
            GUI.color = menu?new Color(.012f,.035f,.044f,.55f):new Color(0,0,0,.85f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), "v" + version, style);
            GUI.color = menu?new Color(.68f,.74f,.72f,.55f):new Color(1,1,1,.8f);
            GUI.Label(rect, "v" + version, style);
            GUI.color = color;
        }
    }
}
