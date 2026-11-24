using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PirateSlop.EditorTools
{
    public sealed class SteamTestBuild : IPostprocessBuildWithReport
    {
        internal const string WindowsBuildDirectory = "Builds/Windows";
        public int callbackOrder => 0;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != UnityEditor.BuildTarget.StandaloneWindows64) return;
            string directory = Path.GetDirectoryName(Path.GetFullPath(report.summary.outputPath));
            if (string.Equals(directory, Path.GetFullPath(WindowsBuildDirectory), StringComparison.OrdinalIgnoreCase))
                File.Delete(Path.Combine(directory, "steam_appid.txt"));
        }
    }
}
