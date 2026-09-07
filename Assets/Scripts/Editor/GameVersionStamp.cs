using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PirateSlop.Editor
{
    [InitializeOnLoad]
    public sealed class GameVersionStamp : IPreprocessBuildWithReport
    {
        const string Output = "Assets/Resources/GeneratedGameVersion.txt";
        public int callbackOrder => -1000;

        static GameVersionStamp()
        {
            EditorApplication.delayCall += Refresh;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Refresh();
            };
        }

        public void OnPreprocessBuild(BuildReport report) => Refresh();

        [MenuItem("PirateSlop/Refresh Game Version")]
        public static void Refresh()
        {
            string version;
            try
            {
                string count = Git("rev-list --count HEAD");
                string hash = Git("rev-parse --short=8 HEAD");
                if (!long.TryParse(count, out _) || hash.Length < 8) throw new InvalidOperationException("Invalid Git revision.");
                var release = PlayerSettings.bundleVersion.Split('.');
                var prefix = release.Length >= 2 ? release[0] + "." + release[1] : PlayerSettings.bundleVersion;
                version = prefix + "." + count + " · " + hash;
                if (Git("status --porcelain --untracked-files=normal").Length != 0) version += " +dev";
                if (Git("rev-parse --is-shallow-repository") == "true") version += " (shallow)";
            }
            catch (Exception error)
            {
                version = PlayerSettings.bundleVersion + " · unknown";
                UnityEngine.Debug.LogWarning("Game version cannot read Git: " + error.Message);
            }
            if (File.Exists(Output) && File.ReadAllText(Output) == version) return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            File.WriteAllText(Output, version, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceSynchronousImport);
        }

        static string Git(string arguments)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var start = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(start);
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                process.Kill();
                throw new TimeoutException("Git version lookup timed out.");
            }
            if (process.ExitCode != 0) throw new InvalidOperationException(error.GetAwaiter().GetResult().Trim());
            return output.GetAwaiter().GetResult().Trim();
        }
    }
}
