using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop
{
    public sealed class StormVolumeRendererFeature : ScriptableRendererFeature
    {
        public Material Template;
        [Range(.25f, 1f)] public float ResolutionScale = .5f;
        Material material;
        VolumetricCloudsURP.VolumetricCloudsPass pass;
        public override void Create()
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            pass = null;
            if (Template == null) return;
            material = new Material(Template) { hideFlags = HideFlags.HideAndDontSave };
            pass = new VolumetricCloudsURP.VolumetricCloudsPass(material, ResolutionScale)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
                dynamicAmbientProbe = false,
                outputDepth = false,
                outputToSceneDepth = false,
                sunAttenuation = false,
                hasAtmosphericScattering = false,
                resetWindOnStart = true,
                renderMode = VolumetricCloudsURP.CloudsRenderMode.BlitTexture,
                upscaleMode = VolumetricCloudsURP.CloudsUpscaleMode.Bilinear
            };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var storm = StormVolumeController.Instance;
            if (pass == null || storm == null || !storm.Ready || renderingData.cameraData.cameraType != CameraType.Game) return;
            storm.Apply(material);
            pass.cloudsVolume = storm.CloudSettings;
            pass.colorAdjustments = null;
            pass.resolutionScale = ShipSpyglassView.ClearsFog(renderingData.cameraData.camera) ? 1f : ResolutionScale;
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            pass = null;
            CoreUtils.Destroy(material);
        }
    }
}

