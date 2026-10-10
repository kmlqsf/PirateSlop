using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Editor
{
    public static class StormSmokePreview
    {
        public static string Render(string name, Vector3 eye, Vector3 target, float time, float advance, float fov = 65, bool lightning = false)
        {
            var compute = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Game/BRZoneVolumetric/Resources/StormSmokeFields.compute"));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Editor/StormSmokePreview.shader");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var noise = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBHalf)
            {
                dimension = TextureDimension.Tex3D, volumeDepth = 128, enableRandomWrite = true,
                useMipMap = true, autoGenerateMips = false, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat
            };
            var water = new RenderTexture(128, 128, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var output = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false, true);
            var previous = RenderTexture.active;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                noise.Create(); water.Create(); output.Create();
                int bake = compute.FindKernel("BakeNoise");
                compute.SetTexture(bake, "_NoiseOutput", noise);
                compute.Dispatch(bake, 32, 32, 32);
                noise.GenerateMips();
                int height = compute.FindKernel("WaterHeight");
                var patch = new Vector4(-256, 1000 - 256, 1f / 512, 1);
                compute.SetTexture(height, "_WaterOutput", water);
                compute.SetVector("_WaterPatch", patch);
                compute.SetVector("_WaveSettings", new Vector4(1, 1, time, 0));
                compute.SetVector("_Whirlpool", Vector4.zero);
                compute.SetInt("_WaveCount", 3);
                var a = new Vector4[16]; var b = new Vector4[16];
                for (int i = 0; i < 3; i++)
                {
                    float angle = (i * 127 + 23) * Mathf.Deg2Rad;
                    float frequency = 2 * Mathf.PI / (30 + i * 21);
                    a[i] = new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), frequency, Mathf.Sqrt(9.8f * frequency));
                    b[i] = new Vector4(1.1f - i * .25f, .85f / (frequency * 3), 0, 0);
                }
                compute.SetVectorArray("_WaveA", a); compute.SetVectorArray("_WaveB", b);
                compute.Dispatch(height, 16, 16, 1);
                material.SetTexture("_StormSmokeNoise", noise);
                var visibilityRange = StormVolumeRendererFeature.TestSmokeVisibilityRange(fov);
                visibilityRange.z = 1f;
                material.SetVector("_StormSmokeVisibilityRange", visibilityRange);
                material.SetTexture("_StormSmokeWaterHeight", water);
                material.SetVector("_StormSmokeWaterPatch", patch);
                material.SetVector("_StormTestSmokeMap", new Vector4(0, 0, 4000, time));
                material.SetVector("_StormCenterWater", new Vector4(0, 0, 0, 1000));
                material.SetVector("_StormBand", new Vector4(1000, 2, 18, .6f));
                material.SetVector("_StormShape", new Vector4(65, 0, 0, 1));
                material.SetVector("_PirateStormTestClouds", new Vector4(1, -250, 65, 0));
                material.SetVector("_StormTestSmokeMotion", new Vector4(advance, time * advance * 8, time * Mathf.Lerp(3.5f, 9, advance), 0));
                material.SetTexture("_StormTestLightningEmission", Texture2D.blackTexture);
                material.SetTexture("_StormTestLightningDistance", Texture2D.blackTexture);
                var flashes = new Vector4[12]; var starts = new Vector4[72]; var ends = new Vector4[72];
                if (lightning)
                {
                    for (int group = 6; group < 9; group++)
                    {
                        float x = (group - 7) * 115;
                        float z = Mathf.Sqrt(1000 * 1000 - x * x);
                        flashes[group] = new Vector4(x, 32, z, 1);
                        for (int channel = 0; channel < 6; channel++)
                        {
                            var root = new Vector3(x, 48 - channel * 6, z);
                            var end = root + new Vector3(channel % 2 == 0 ? 7 : -8, -12, 1);
                            starts[group * 6 + channel] = new Vector4(root.x, root.y, root.z, .65f);
                            ends[group * 6 + channel] = end;
                        }
                    }
                }
                material.SetVectorArray("_StormSmokeFlashVolumes", flashes);
                material.SetVectorArray("_StormSmokeFlashChannelStarts", starts);
                material.SetVectorArray("_StormSmokeFlashChannelEnds", ends);
                material.SetColor("_StormLightningColor", new Color(.42f, .12f, .8f, 1));
                material.SetVector("_MainLightPosition", new Vector4(.4f, 1, -.3f, 0));
                material.SetFloat("_DensityMultiplier", .95f);
                var forward = (target - eye).normalized;
                var right = Vector3.Cross(Vector3.up, forward).normalized;
                var up = Vector3.Cross(forward, right);
                float lens = Mathf.Tan(fov * Mathf.Deg2Rad * .5f);
                material.SetVector("_PreviewEye", eye);
                material.SetVector("_PreviewForward", forward);
                material.SetVector("_PreviewRight", right);
                material.SetVector("_PreviewUp", up);
                material.SetVector("_PreviewLens", new Vector4(lens * 960 / 540, lens, 0, 0));
                material.SetFloat("_StormSmokePreviewProjectionScale", lens / 540);
                Graphics.Blit(Texture2D.blackTexture, output, material);
                RenderTexture.active = output;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                pixels.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/StormSmokePreview"));
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, name + ".png");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return path;
            }
            finally
            {
                RenderTexture.active = previous;
                ShaderUtil.allowAsyncCompilation = previousAsync;
                noise.Release(); water.Release(); output.Release();
                Object.DestroyImmediate(material); Object.DestroyImmediate(noise); Object.DestroyImmediate(water);
                Object.DestroyImmediate(output); Object.DestroyImmediate(pixels); Object.DestroyImmediate(compute);
            }
        }
    }
}
