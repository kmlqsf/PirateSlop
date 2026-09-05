using UnityEditor;
using System.IO;
[InitializeOnLoad]
public static class MultiplayerWorkBridge
{
    static double next;
    static MultiplayerWorkBridge() { EditorApplication.update += Update; }
    static async void Update()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        next = EditorApplication.timeSinceStartup + 2;
        const string path = "Temp/multiplayer-editor-command.txt";
        if (!File.Exists(path)) return;
        string command = File.ReadAllText(path).Trim(); File.Delete(path);
        try {
            if (command == "connect") await MCPForUnity.Editor.Services.MCPServiceLocator.Bridge.StartAsync();
            else if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "setup") EditorApplication.ExecuteMenuItem("PirateSlop/Multiplayer/Configure Scenes");
            else if (command == "build") EditorApplication.ExecuteMenuItem("PirateSlop/Multiplayer/Build Windows");
            File.WriteAllText("Temp/multiplayer-editor-result.txt", command + " completed");
        } catch (System.Exception e) { File.WriteAllText("Temp/multiplayer-editor-result.txt", e.ToString()); UnityEngine.Debug.LogException(e); }
    }
}
