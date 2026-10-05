using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.World
{
    [DefaultExecutionOrder(1000)]
    public sealed class TestSkyDayNight : MonoBehaviour
    {
        public static TestSkyDayNight Active { get; private set; }
        public UniversalRenderPipelineAsset Pipeline;
        public Volume Environment;
        public Light Sun;
        public Light Moon;
        [Range(0f, 1f)] public float TargetBlend;
        public float CurrentBlend { get; private set; }
        float velocity;
        VolumeProfile runtimeProfile;
        PhysicallyBasedSky sky;
        VolumetricClouds clouds;
        Fog skyFog;
        RenderPipelineAsset previousPipeline;
        Material previousSkybox;
        Light previousSun;
        Texture previousReflection;
        DefaultReflectionMode previousReflectionMode;
        AmbientMode previousAmbientMode;
        SphericalHarmonicsL2 previousAmbientProbe;
        Color previousAmbientSky, previousAmbientEquator, previousAmbientGround, previousFogColor;
        float previousAmbientIntensity, previousReflectionIntensity, previousFogDensity;
        bool previousFog, initialized;
        readonly List<Light> disabledLights = new();
        Material waterMaterial;
        Color previousShallowColor, previousDeepColor, previousFoamColor;
        Texture previousWaterReflection;
        float previousUseWaterReflection;

        void OnEnable()
        {
            if (!Application.isPlaying || Pipeline == null || Environment == null || Sun == null || Moon == null) return;
            if (Active != null && Active != this) Active.enabled = false;
            previousPipeline = QualitySettings.renderPipeline;
            previousSkybox = RenderSettings.skybox;
            previousSun = RenderSettings.sun;
            previousReflection = RenderSettings.customReflectionTexture;
            previousReflectionMode = RenderSettings.defaultReflectionMode;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientProbe = RenderSettings.ambientProbe;
            previousAmbientSky = RenderSettings.ambientSkyColor;
            previousAmbientEquator = RenderSettings.ambientEquatorColor;
            previousAmbientGround = RenderSettings.ambientGroundColor;
            previousAmbientIntensity = RenderSettings.ambientIntensity;
            previousReflectionIntensity = RenderSettings.reflectionIntensity;
            previousFog = RenderSettings.fog;
            previousFogColor = RenderSettings.fogColor;
            previousFogDensity = RenderSettings.fogDensity;
            runtimeProfile = Environment.profile;
            runtimeProfile.TryGet(out sky);
            runtimeProfile.TryGet(out clouds);
            runtimeProfile.TryGet(out skyFog);
            if (sky == null || clouds == null) return;
            Environment.enabled = true;
            waterMaterial = OceanSurface.Instance != null ? OceanSurface.Instance.WaterMaterial : null;
            if (waterMaterial != null && waterMaterial.shader.name == "Custom/SimpleWaterURP")
            {
                previousShallowColor = waterMaterial.GetColor("_ShallowColor");
                previousDeepColor = waterMaterial.GetColor("_DeepColor");
                previousFoamColor = waterMaterial.GetColor("_FoamColor");
                previousWaterReflection = waterMaterial.GetTexture("_OceanReflection");
                previousUseWaterReflection = waterMaterial.GetFloat("_UseOceanReflection");
            }
            else waterMaterial = null;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light != Sun && light != Moon && light.enabled)
                {
                    disabledLights.Add(light);
                    light.enabled = false;
                }
            QualitySettings.renderPipeline = Pipeline;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            CurrentBlend = Mathf.Clamp01(TargetBlend);
            initialized = true;
            Active = this;
            ApplyLighting();
        }

        void LateUpdate()
        {
            if (!initialized) return;
            TargetBlend = Mathf.Clamp01(TargetBlend);
            CurrentBlend = Mathf.SmoothDamp(CurrentBlend, TargetBlend, ref velocity, .35f, Mathf.Infinity, Time.unscaledDeltaTime);
            ApplyLighting();
        }

        public void ApplyLighting()
        {
            if (sky == null || clouds == null) return;
            float t = CurrentBlend;
            float sunset = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.15f, .62f, t));
            float dusk = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.48f, .70f, t));
            float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.70f, 1f, t));
            bool moonMain = t >= .70f;
            Sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(55f, -12f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .70f))), -35f, 0f);
            Sun.color = Color.Lerp(new Color(1f, .96f, .87f), new Color(1f, .48f, .22f), sunset);
            Sun.intensity = Mathf.Lerp(3.03f, .8f, sunset) * (1f - dusk);
            Sun.enabled = !moonMain;
            Moon.transform.rotation = Quaternion.Euler(Mathf.Lerp(18f, 24f, night), -55f, 0f);
            Moon.color = new Color(.88f, .93f, 1f);
            Moon.intensity = .30f * night;
            Moon.enabled = moonMain;
            RenderSettings.sun = moonMain ? Moon : Sun;
            sky.moonBody.Override(moonMain);
            sky.exposure.Override(Mathf.Lerp(0f, -3.5f, night));
            sky.horizonTint.Override(Color.Lerp(Color.white, new Color(.68f, .76f, .92f), night));
            sky.zenithTint.Override(Color.Lerp(Color.white, new Color(.55f, .67f, .9f), night));
            float ambientRise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, .48f, t));
            float ambientNight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.58f, 1f, t));
            Color ambient = Color.Lerp(new Color(.30f, .29f, .30f).linear, new Color(.25f, .29f, .38f).linear * .5f, ambientNight);
            sky.ambientFloor.Override(Color.Lerp(Color.black, ambient, ambientRise).gamma);
            clouds.sunLightDimmer.Override(Mathf.Lerp(1f, .7f, night));
            clouds.temporalAccumulationFactor.Override(Mathf.Abs(velocity) > .005f ? .8f : .95f);
            RenderSettings.reflectionIntensity = Mathf.Lerp(1f, .6f, night);
            if (waterMaterial != null)
            {
                float waterBrightness = Mathf.Lerp(1f, .18f, night);
                waterMaterial.SetColor("_ShallowColor", DimWater(previousShallowColor, waterBrightness));
                waterMaterial.SetColor("_DeepColor", DimWater(previousDeepColor, waterBrightness));
                waterMaterial.SetColor("_FoamColor", DimWater(previousFoamColor, waterBrightness));
                var reflection = RenderSettings.customReflectionTexture;
                bool dynamicReflection = RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom && reflection != null && reflection.dimension == TextureDimension.Cube;
                if (dynamicReflection) waterMaterial.SetTexture("_OceanReflection", reflection);
                waterMaterial.SetFloat("_UseOceanReflection", dynamicReflection ? 1f : 0f);
            }
            RenderSettings.fog = false;
            if (skyFog != null)
            {
                float fogScale = SeaMistRendererFeature.DensityMultiplier / SeaMistRendererFeature.DefaultDensityMultiplier;
                skyFog.enabled.Override(fogScale > .001f);
                skyFog.meanFreePath.Override(Mathf.Lerp(4500f, 6000f, dusk) / Mathf.Max(.001f, fogScale));
            }
        }

        static Color DimWater(Color color, float brightness)
        {
            return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        }

        void OnDisable()
        {
            if (!initialized) return;
            initialized = false;
            if (Active == this) Active = null;
            Sun.enabled = false;
            Moon.enabled = false;
            Environment.enabled = false;
            if (waterMaterial != null)
            {
                waterMaterial.SetColor("_ShallowColor", previousShallowColor);
                waterMaterial.SetColor("_DeepColor", previousDeepColor);
                waterMaterial.SetColor("_FoamColor", previousFoamColor);
                waterMaterial.SetTexture("_OceanReflection", previousWaterReflection);
                waterMaterial.SetFloat("_UseOceanReflection", previousUseWaterReflection);
            }
            QualitySettings.renderPipeline = previousPipeline;
            foreach (var light in disabledLights) if (light != null) light.enabled = true;
            disabledLights.Clear();
            RenderSettings.skybox = previousSkybox;
            RenderSettings.sun = previousSun;
            RenderSettings.defaultReflectionMode = previousReflectionMode;
            RenderSettings.customReflectionTexture = previousReflection;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientProbe = previousAmbientProbe;
            RenderSettings.ambientSkyColor = previousAmbientSky;
            RenderSettings.ambientEquatorColor = previousAmbientEquator;
            RenderSettings.ambientGroundColor = previousAmbientGround;
            RenderSettings.ambientIntensity = previousAmbientIntensity;
            RenderSettings.reflectionIntensity = previousReflectionIntensity;
            RenderSettings.fog = previousFog;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogDensity = previousFogDensity;
            Shader.DisableKeyword("PHYSICALLY_BASED_SKY");
            Shader.DisableKeyword("VISUAL_ENVIRONMENT_DYNAMIC_SKY");
        }
    }
}
