using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(-100)]
    public sealed class DeveloperMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static bool Available => Application.isEditor || Debug.isDebugBuild;
        public static bool AllowRemote;
        NetworkWeapon network;
        string message = "";
        Vector2 scroll;
        void Awake() => network = GetComponent<NetworkWeapon>();
        void Update()
        {
            if (!Available || network == null || !network.IsOwner) return;
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            { IsOpen = !IsOpen; AdvancedPlayerController.SetCursor(!IsOpen); }
        }
        void OnDisable() { if (network != null && network.IsOwner) IsOpen = false; }
        void OnGUI()
        {
            if (!Available || !IsOpen || network == null || !network.IsOwner) return;
            GUILayout.BeginArea(new Rect(20, 30, 300, Mathf.Min(620, Screen.height - 60)), "Developer tools · F8", GUI.skin.window);
            GUILayout.Space(25);
            scroll = GUILayout.BeginScrollView(scroll);
            if (network.IsServerInitialized) AllowRemote = GUILayout.Toggle(AllowRemote, "Allow client developer commands");
            GUILayout.Label("Spawning is performed by the server.");
            Button("Spawn ship nearby", 0);
            Button("Spawn target dummy", 1);
            Button("Spawn disassembled cannon", 2);
            Button("Spawn pistol", 3);
            Button("Spawn sabre", 4);
            Button("Spawn cannonball", 5);
            Button("Spawn mallet", 10);
            Button("Spawn plank", 11);
            Button("Heal player", 6);
            Button("Heal current ship", 7);
            Button("Give cannon kit", 8);
            Button("Remove spawned test objects", 9);
            GUILayout.Label(message);
            if (GUILayout.Button("Close")) { IsOpen = false; AdvancedPlayerController.SetCursor(true); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        void Button(string label, byte command)
        {
            if (GUILayout.Button(label, GUILayout.Height(29))) network.DeveloperCommand(command);
        }
        public void Report(string value) => message = value;
    }
}
