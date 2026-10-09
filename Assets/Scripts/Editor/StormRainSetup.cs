using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.Editor
{
    public static class StormRainSetup
    {
        [MenuItem("PirateSlop/Prepare Test Storm Downpour")]
        public static void PrepareTestDownpour()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            foreach (var name in new[] { "RainStreak", "RainContact", "RainScreen", "RainWetWood", "TestLightning" })
                AssetDatabase.SaveAssetIfDirty(Material(name));
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            if (bank == null) throw new InvalidOperationException("Audio bank is missing.");
            bank.Rain = ImportRainAudio("RainDownpourLoop");
            bank.RainDeck = ImportRainAudio("RainDeckLoop");
            bank.TestStormThunder = new[] { ImportRainAudio("TestThunderA"), ImportRainAudio("TestThunderB"), ImportRainAudio("TestThunderC") };
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssetIfDirty(bank);
            const string warmupPath = "Assets/Resources/Storm/RainWarmup.shadervariants";
            var warmup = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(warmupPath);
            if (warmup == null)
            {
                warmup = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(warmup, warmupPath);
            }
            warmup.Clear();
            foreach (var name in new[] { "RainStreak", "RainContact", "RainScreen", "RainWetWood", "TestLightning" })
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Storm/"+name+".shader");
                var type = name == "RainScreen" ? UnityEngine.Rendering.PassType.Normal : UnityEngine.Rendering.PassType.ScriptableRenderPipeline;
                warmup.Add(new ShaderVariantCollection.ShaderVariant(shader, type, Array.Empty<string>()));
            }
            EditorUtility.SetDirty(warmup);
            AssetDatabase.SaveAssetIfDirty(warmup);
        }

        static AudioClip ImportRainAudio(string name)
        {
            string path = "Assets/Audio/Ambience/Storm/"+name+".wav";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Rain audio is missing: "+path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .85f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        [MenuItem("PirateSlop/Prepare Storm Rain")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var streak = Material("RainStreak");
            var contact = Material("RainContact");
            var screen = Material("RainScreen");
            var depth = Shader.Find("Hidden/Universal Render Pipeline/CopyDepth");
            if (depth == null) throw new InvalidOperationException("CopyDepth shader is missing.");
            foreach (var path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/TestSky/TestSkyRenderer.asset" })
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer == null) throw new InvalidOperationException("Missing renderer: " + path);
                var feature = renderer.rendererFeatures.OfType<StormRainRendererFeature>().FirstOrDefault();
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<StormRainRendererFeature>();
                    feature.name = "Storm Rain Lens and Veil";
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                    renderer.rendererFeatures.Add(feature);
                }
                feature.Template = screen; feature.DepthCopyShader = depth; feature.SetActive(true);
                EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
                renderer.SetDirty(); feature.Create();
            }
            const string prefabPath = "Assets/Game/BRZoneVolumetric/Resources/BRStormVolume.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var rain = root.GetComponent<StormRainController>();
                if (rain == null) rain = root.AddComponent<StormRainController>();
                rain.RainMaterial = streak; rain.ImpactMaterial = contact;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var clouds = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/TestSky/TestSkyClouds.mat");
            clouds.SetFloat("_ClearCloudCoverageStart", .52f);
            clouds.SetFloat("_ClearCloudCoverageEnd", .74f);
            EditorUtility.SetDirty(clouds);
            AssetDatabase.SaveAssets();
        }

        static Material Material(string name)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Storm/" + name + ".shader");
            if (shader == null) throw new InvalidOperationException("Missing rain shader: " + name);
            var path = "Assets/Resources/Storm/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
