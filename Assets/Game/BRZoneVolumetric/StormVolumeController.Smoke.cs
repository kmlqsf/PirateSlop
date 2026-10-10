using UnityEngine;
using UnityEngine.Rendering;
using WaterSystem;

namespace PirateSlop
{
    public sealed partial class StormVolumeController
    {
        ComputeShader smokeFields;
        RenderTexture smokeNoise;
        RenderTexture smokeWater;
        BoatAttackOcean smokeOcean;
        readonly Vector4[] smokeWaveA = new Vector4[16];
        readonly Vector4[] smokeWaveB = new Vector4[16];
        int smokeWaterKernel;
        int smokeWaveCount = -1;
        static readonly int SmokeNoiseId = Shader.PropertyToID("_StormSmokeNoise");
        static readonly int SmokeWaterId = Shader.PropertyToID("_StormSmokeWaterHeight");
        static readonly int SmokeWaterPatchId = Shader.PropertyToID("_StormSmokeWaterPatch");

        void UpdateSmokeFields()
        {
            if (smokeFields == null)
            {
                var source = Resources.Load<ComputeShader>("StormSmokeFields");
                if (source != null) smokeFields = Instantiate(source);
            }
            if (smokeFields == null || !SystemInfo.supportsComputeShaders) return;
            if (smokeNoise == null)
            {
                smokeNoise = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBHalf)
                {
                    name = "StormSmokeNoise128",
                    dimension = TextureDimension.Tex3D,
                    volumeDepth = 128,
                    enableRandomWrite = true,
                    useMipMap = true,
                    autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                smokeNoise.Create();
                int kernel = smokeFields.FindKernel("BakeNoise");
                smokeFields.SetTexture(kernel, "_NoiseOutput", smokeNoise);
                smokeFields.Dispatch(kernel, 32, 32, 32);
                smokeNoise.GenerateMips();
                smokeWater = new RenderTexture(128, 128, 0, RenderTextureFormat.RFloat)
                {
                    name = "StormSmokeWaterHeight",
                    enableRandomWrite = true,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                smokeWater.Create();
                smokeWaterKernel = smokeFields.FindKernel("WaterHeight");
                smokeFields.SetTexture(smokeWaterKernel, "_WaterOutput", smokeWater);
            }
            Shader.SetGlobalTexture(SmokeNoiseId, smokeNoise);
            Shader.SetGlobalTexture(SmokeWaterId, smokeWater);
            var surface = OceanSurface.Instance;
            var ocean = surface != null ? surface.HeightSource as BoatAttackOcean : null;
            if (ocean != smokeOcean || smokeWaveCount < 0)
            {
                smokeOcean = ocean;
                smokeWaveCount = 0;
                if (ocean != null && ocean.Water != null)
                {
                    var waves = GerstnerWaves.GetWaveArray(ocean.Water.gerstnerData);
                    smokeWaveCount = Mathf.Min(16, waves.Length);
                    for (int i = 0; i < smokeWaveCount; i++)
                    {
                        float frequency = 2 * Mathf.PI / waves[i].wavelength;
                        float angle = waves[i].direction * Mathf.Deg2Rad;
                        smokeWaveA[i] = new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), frequency, Mathf.Sqrt(9.8f * frequency));
                        smokeWaveB[i] = new Vector4(waves[i].amplitude / waves.Length, waves[i].amplitude > 0 ? .85f / (frequency * waves.Length) : 0, 0, 0);
                    }
                }
                smokeFields.SetInt("_WaveCount", smokeWaveCount);
                smokeFields.SetVectorArray("_WaveA", smokeWaveA);
                smokeFields.SetVectorArray("_WaveB", smokeWaveB);
            }
            var camera = Camera.main;
            Vector2 center = camera != null ? new Vector2(camera.transform.position.x, camera.transform.position.z) : new Vector2(CurrentCenter.x, CurrentCenter.z);
            if (camera != null)
            {
                var fromCenter = center - new Vector2(CurrentCenter.x, CurrentCenter.z);
                if (fromCenter.magnitude < CurrentRadius - 120)
                {
                    var forward = new Vector2(camera.transform.forward.x, camera.transform.forward.z).normalized;
                    float b = Vector2.Dot(fromCenter, forward);
                    float distance = -b + Mathf.Sqrt(Mathf.Max(0, b * b + CurrentRadius * CurrentRadius - fromCenter.sqrMagnitude));
                    center += forward * distance;
                }
            }
            var patch = new Vector4(Mathf.Floor(center.x / 4) * 4 - 256, Mathf.Floor(center.y / 4) * 4 - 256, 1f / 512, 1);
            float waveTime = surface != null ? surface.WaveTime : Time.time;
            var settings = ocean != null
                ? new Vector4(ocean.WaveStrength, ocean.WaveSteepness, ocean.WaveClockValue + (waveTime - ocean.WaveClockAnchor) * ocean.WaveSpeed, WaterLevel)
                : new Vector4(0, 0, waveTime, WaterLevel);
            smokeFields.SetVector("_WaterPatch", patch);
            smokeFields.SetVector("_WaveSettings", settings);
            smokeFields.SetVector("_Whirlpool", surface != null ? new Vector4(surface.WhirlpoolCenter.x, surface.WhirlpoolCenter.z, surface.WhirlpoolRadius, surface.WhirlpoolDepth) : Vector4.zero);
            smokeFields.Dispatch(smokeWaterKernel, 16, 16, 1);
            Shader.SetGlobalVector(SmokeWaterPatchId, patch);
        }

        void ReleaseSmokeFields()
        {
            if (smokeNoise != null) { smokeNoise.Release(); Destroy(smokeNoise); smokeNoise = null; }
            if (smokeWater != null) { smokeWater.Release(); Destroy(smokeWater); smokeWater = null; }
            smokeOcean = null;
            smokeWaveCount = -1;
            if (smokeFields != null) { Destroy(smokeFields); smokeFields = null; }
            if (Instance == this) Shader.SetGlobalVector(SmokeWaterPatchId, Vector4.zero);
        }
    }
}
