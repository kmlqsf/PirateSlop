using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.World
{
    [DefaultExecutionOrder(-95)]
    public sealed class TestOceanController : MonoBehaviour
    {
        OceanSurface surface;
        OceanHeightSource previousSource;
        Material previousMaterial;
        MeshRenderer oldRenderer;
        bool previousRendererEnabled;
        Vector3 previousWhirlpoolCenter;
        float previousWhirlpoolRadius, previousWhirlpoolDepth, previousWhirlpoolTwist;
        BoatAttackOcean ocean;

        void OnEnable()
        {
            surface = OceanSurface.Instance;
            if (surface == null) throw new System.InvalidOperationException("The test ocean requires the gameplay OceanSurface.");
            ocean = GetComponent<BoatAttackOcean>();
            ocean.Surface = surface;
            previousSource = surface.HeightSource;
            previousWhirlpoolCenter = surface.WhirlpoolCenter;
            previousWhirlpoolRadius = surface.WhirlpoolRadius;
            previousWhirlpoolDepth = surface.WhirlpoolDepth;
            previousWhirlpoolTwist = surface.WhirlpoolTwist;
            previousMaterial = surface.WaterMaterial;
            oldRenderer = surface.GetComponent<MeshRenderer>();
            if (oldRenderer != null)
            {
                previousRendererEnabled = oldRenderer.enabled;
                oldRenderer.enabled = false;
            }
            surface.HeightSource = ocean;
            surface.WaterMaterial = null;
            ocean.Water.dynamicSkyReflection = true;
        }

        void LateUpdate()
        {
            var sky = TestSkyDayNight.Active;
            float night = sky != null ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.7f, 1f, sky.CurrentBlend)) : 0f;
            ApplySeaLighting(ocean.Water, night);
            var camera = Camera.main;
            if (camera == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.requiresColorTexture = true;
            data.requiresDepthTexture = true;
            data.volumeLayerMask |= 1;
        }

        public static void ApplySeaLighting(WaterSystem.Water water, float night)
        {
            float strength = GameSettings.Look.seaDetail * .01f * (1f - night);
            water.reflectionLightingMultiplier = 1f + .24f * strength;
            water.foamLightingMultiplier = Mathf.Lerp(1f + .28f * strength, .45f, night);
            water.scatteringLightingMultiplier = Mathf.Lerp(1f - .14f * strength, .65f, night);
        }

        void OnDisable()
        {
            if (surface == null || surface.HeightSource != ocean) return;
            surface.HeightSource = previousSource;
            surface.WhirlpoolCenter = previousWhirlpoolCenter;
            surface.WhirlpoolRadius = previousWhirlpoolRadius;
            surface.WhirlpoolDepth = previousWhirlpoolDepth;
            surface.WhirlpoolTwist = previousWhirlpoolTwist;
            surface.WaterMaterial = previousMaterial;
            if (oldRenderer != null) oldRenderer.enabled = previousRendererEnabled;
        }
    }
}
