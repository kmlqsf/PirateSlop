using System;
using System.IO;
using UnityEngine;

namespace PirateSlop
{
    public static class ErrorLog
    {
        const int MaxEntries = 300;
        const long MaxBytes = 2 * 1024 * 1024;
        static readonly object gate = new();
        static string path;
        static int written;
        static string lastCondition;
        static DateTime lastAt;

        public static string Folder => Application.persistentDataPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (Application.isEditor) return;
            try
            {
                path = Path.Combine(Folder, "errors.log");
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    string previous = Path.Combine(Folder, "errors.prev.log");
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(path, previous);
                }
                File.AppendAllText(path, $"{Environment.NewLine}=== {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC · v{Application.version} · Unity {Application.unityVersion} · {SystemInfo.operatingSystem} · {SystemInfo.graphicsDeviceName} · RAM {SystemInfo.systemMemorySize} MB · log {Application.consoleLogPath}{Environment.NewLine}");
                Application.logMessageReceivedThreaded += Record;
            }
            catch (Exception) { path = null; }
        }

        static void Record(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            lock (gate)
            {
                if (path == null || written >= MaxEntries) return;
                var now = DateTime.UtcNow;
                if (condition == lastCondition && (now - lastAt).TotalSeconds < 5) return;
                lastCondition = condition; lastAt = now;
                written++;
                try
                {
                    string entry = $"[{now:HH:mm:ss}] {type}: {condition}{Environment.NewLine}{stackTrace}";
                    if (written == MaxEntries) entry += "… дальнейшие ошибки этой сессии не записываются" + Environment.NewLine;
                    File.AppendAllText(path, entry);
                }
                catch (Exception) { path = null; }
            }
        }

        public static void OpenFolder() => Application.OpenURL("file:///" + Folder.Replace('\\', '/'));
    }
}
