using System;
using System.Globalization;
using UnityEngine;

namespace PirateSlop.Performance
{
    public static class PerformanceRenderOverrides
    {
        static float stormScale = -1, skyScale = -1;
        static bool optimizedClouds = true;
        static string signature = "";
        public static string Signature => signature;
        public static bool OptimizedClouds => optimizedClouds;
        public static string QualitySignature => optimizedClouds ? "clouds-balanced-v1(0.50/0.30)" : "clouds-original-v1";
        public static float StormResolution(float original) => PerformanceBenchmarkController.Running && stormScale > 0 ? stormScale : optimizedClouds ? Mathf.Min(original, .5f) : original;
        public static float SkyResolution(float original)
        {
            if (PerformanceBenchmarkController.Running && skyScale > 0) return skyScale;
            return optimizedClouds && Shader.GetGlobalVector("_PirateStormBackdrop").x < .5f ? Mathf.Min(original, .3f) : original;
        }

        public static void SetOptimizedClouds(bool value)
        {
            optimizedClouds = value;
            PlayerPrefs.SetInt("OptimizedClouds", value ? 1 : 0);
            PlayerPrefs.Save();
            PirateSlop.Performance.PerformanceCapture.Event("GraphicsCloudQuality:" + QualitySignature);
        }

        public static void Configure(string storm, string sky) => Set(Parse(storm), Parse(sky));

        public static void Set(float storm, float sky)
        {
            if (!Valid(storm) || !Valid(sky)) throw new ArgumentOutOfRangeException("Cloud render scales must be -1 or between 0.25 and 1.");
            stormScale = storm; skyScale = sky;
            VolumetricCloudsURP.ResolutionScaleFilter = SkyResolution;
            signature = storm > 0 || sky > 0 ? FormattableString.Invariant($"stormScale={storm:F3};skyScale={sky:F3}") : "";
        }

        static bool Valid(float value) => value == -1 || float.IsFinite(value) && value >= .25f && value <= 1;
        static float Parse(string value) => string.IsNullOrWhiteSpace(value) ? -1 : float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            optimizedClouds = PlayerPrefs.GetInt("OptimizedClouds", 1) != 0;
            Set(-1, -1);
        }
    }
}
