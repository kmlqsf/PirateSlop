using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PirateSlop.EditorTools
{
    public sealed class SteamTestBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == UnityEditor.BuildTarget.StandaloneWindows64)
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "steam_appid.txt"), "480");
        }
    }
}
