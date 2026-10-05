using System;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.Editor
{
    public static class TestSkySetup
    {
        const string SettingsPath = "Assets/Settings/TestSky";
        const string PrefabPath = "Assets/Resources/EnvironmentTest/SkyDayNight.prefab";

        [MenuItem("PirateSlop/Prepare Test Sky")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before preparing the sky.");
            if (!AssetDatabase.IsValidFolder(SettingsPath)) AssetDatabase.CreateFolder("Assets/Settings", "TestSky");
            var rendererPath = SettingsPath + "/TestSkyRenderer.asset";
            var pipelinePath = SettingsPath + "/TestSkyPipeline.asset";
            var materialPath = SettingsPath + "/TestSkyClouds.mat";
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath) == null)
                AssetDatabase.CopyAsset("Assets/Settings/PC_Renderer.asset", rendererPath);
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath) == null)
                AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset", pipelinePath);
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
                AssetDatabase.CopyAsset("Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricClouds.mat", materialPath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            var cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var skyShader = Shader.Find("Hidden/Skybox/PhysicallyBasedSky");
            var lutShader = Shader.Find("Hidden/Sky/PhysicallyBasedSkyPrecomputation");
            if (renderer == null || pipeline == null || cloudMaterial == null || skyShader == null || lutShader == null)
                throw new InvalidOperationException("Sky renderer, pipeline, material or shaders are missing.");
            foreach (var feature in renderer.rendererFeatures)
                if (feature.name == "Sea Mist" || feature.name == "Sea Atmospheric Fog") feature.SetActive(false);
            var skyFeature = Feature<PhysicallyBasedSkyURP>(renderer);
            skyFeature.PBSkyShader = skyShader;
            skyFeature.PBSkyLutShader = lutShader;
            skyFeature.PrecomputationQuality = PhysicallyBasedSkyURP.PrecomputationQualityMode.Low;
            skyFeature.CloudsMaterial = cloudMaterial;
            skyFeature.FallbackSkyMaterial = RenderSettings.skybox;
            var cloudFeature = Feature<VolumetricCloudsURP>(renderer);
            cloudFeature.CloudsMaterial = cloudMaterial;
            cloudFeature.ResolutionScale = .5f;
            cloudFeature.SunAttenuation = false;
            cloudFeature.AmbientUpdateMode = VolumetricCloudsURP.CloudsAmbientMode.Dynamic;
            var pipelineSettings = new SerializedObject(pipeline);
            var renderers = pipelineSettings.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            pipelineSettings.FindProperty("m_DefaultRendererIndex").intValue = 0;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            var profilePath = SettingsPath + "/TestSkyVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            var environment = Component<VisualEnvironment>(profile);
            environment.skyType.Override((int)VisualEnvironment.SkyType.PhysicallyBased);
            environment.skyAmbientMode.Override(VisualEnvironment.SkyAmbientMode.Dynamic);
            environment.renderingSpace.Override(VisualEnvironment.RenderingSpace.Camera);
            var sky = Component<PhysicallyBasedSky>(profile);
            sky.atmosphericScattering.Override(true);
            sky.exposure.Override(0f);
            sky.updateMode.Override(PhysicallyBasedSky.EnvironmentUpdateMode.Realtime);
            sky.updatePeriod.Override(.25f);
            var fog = Component<Fog>(profile);
            fog.enabled.Override(true);
            fog.meanFreePath.Override(4500f);
            fog.maximumHeight.Override(120f);
            var clouds = Component<VolumetricClouds>(profile);
            clouds.cloudPreset = VolumetricClouds.CloudPresets.Sparse;
            clouds.cloudPreset = VolumetricClouds.CloudPresets.Custom;
            clouds.state.Override(true);
            clouds.localClouds.Override(false);
            clouds.densityMultiplier.Override(.38f);
            clouds.bottomAltitude.Override(2800f);
            clouds.altitudeRange.Override(1200f);
            clouds.globalSpeed.Override(10f);
            clouds.globalOrientation.Override(35f);
            clouds.numPrimarySteps.Override(48);
            clouds.numLightSteps.Override(4);
            clouds.shadows.Override(false);
            ConfigureLook(profile, pipeline, renderer);
            EditorUtility.SetDirty(profile);
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(skyFeature);
            EditorUtility.SetDirty(cloudFeature);
            EditorUtility.SetDirty(renderer);
            renderer.SetDirty();
            skyFeature.Create();
            cloudFeature.Create();
            var root = new GameObject("TestSkyDayNight");
            try
            {
                var controller = root.AddComponent<TestSkyDayNight>();
                controller.Pipeline = pipeline;
                controller.Sun = Directional(root.transform, "TestSun", 55f, -35f, 3.03f, new Color(1f, .96f, .87f), 1f);
                controller.Moon = Directional(root.transform, "TestMoon", 24f, -55f, 0f, new Color(.88f, .93f, 1f), .55f);
                controller.Moon.enabled = false;
                var volumeObject = new GameObject("TestSkyVolume");
                volumeObject.transform.SetParent(root.transform, false);
                controller.Environment = volumeObject.AddComponent<Volume>();
                controller.Environment.isGlobal = true;
                controller.Environment.priority = 100f;
                controller.Environment.sharedProfile = profile;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Test sky prepared for the ordinary environment test: F8 day / sunset / moonlit night.");
        }

        public static void ConfigureLook(VolumeProfile profile, UniversalRenderPipelineAsset pipeline, UniversalRendererData renderer)
        {
            var globalSettings = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            var globalProperties = new SerializedObject(globalSettings);
            var iterator = globalProperties.GetIterator();
            string includePath = null, labelPath = null;
            while (iterator.Next(true))
            {
                if (iterator.name == "m_IncludeAssetsByLabel") includePath = iterator.propertyPath;
                if (iterator.name == "m_LabelToInclude") labelPath = iterator.propertyPath;
            }
            if (includePath == null || labelPath == null) throw new InvalidOperationException("Runtime render pipeline inclusion settings are missing.");
            globalProperties.FindProperty(includePath).boolValue = true;
            globalProperties.FindProperty(labelPath).stringValue = "PirateSlopRuntimePipeline";
            globalProperties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(globalSettings);
            var labels = new System.Collections.Generic.List<string>(AssetDatabase.GetLabels(pipeline));
            if (!labels.Contains("PirateSlopRuntimePipeline")) labels.Add("PirateSlopRuntimePipeline");
            AssetDatabase.SetLabels(pipeline, labels.ToArray());
            pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
            pipeline.colorGradingLutSize = 32;
            var tone = Component<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = Component<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(6f);
            color.saturation.Override(-4f);
            color.hueShift.Override(0f);
            color.colorFilter.Override(Color.white);
            var balance = Component<WhiteBalance>(profile);
            balance.temperature.Override(0f);
            balance.tint.Override(0f);
            var bloom = Component<Bloom>(profile);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(.15f);
            bloom.scatter.Override(.55f);
            bloom.clamp.Override(8f);
            bloom.tint.Override(Color.white);
            bloom.highQualityFiltering.Override(true);
            bloom.downscale.Override(BloomDownscaleMode.Half);
            bloom.maxIterations.Override(5);
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                var settings = new SerializedObject(feature);
                settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = .15f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(renderer);
            renderer.SetDirty();
        }

        static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static T Feature<T>(UniversalRendererData renderer) where T : ScriptableRendererFeature
        {
            foreach (var feature in renderer.rendererFeatures) if (feature is T existing) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            created.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(created, renderer);
            renderer.rendererFeatures.Add(created);
            return created;
        }

        static Light Directional(Transform parent, string name, float elevation, float azimuth, float intensity, Color color, float shadowStrength)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = shadowStrength;
            return light;
        }
    }
}
