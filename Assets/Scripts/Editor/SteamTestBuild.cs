using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PirateSlop.EditorTools
{
    public sealed class SteamTestBuild : BuildPlayerProcessor, IPostprocessBuildWithReport
    {
        public override int callbackOrder => 0;
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            var scenes = context.BuildPlayerOptions.scenes;
            if (scenes == null || Array.IndexOf(scenes, "Assets/Scenes/NetworkMenu.unity") < 0) return;
            foreach (var scene in new[] { "Assets/Scenes/BoatAttackWaterTest.unity", "Assets/Scenes/OceanaWaterTest.unity" })
                if (Array.IndexOf(scenes, scene) < 0)
                    throw new BuildFailedException("В списке этой сборки отсутствует " + scene + ". Используйте все включённые сцены из Build Profiles; собственный список BuildPlayerOptions тоже должен содержать обе водные тестовые сцены.");
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == UnityEditor.BuildTarget.StandaloneWindows64)
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "steam_appid.txt"), "480");
        }
    }
}
