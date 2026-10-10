using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PirateSlop
{
    public sealed class StormVolumeRendererFeature : ScriptableRendererFeature
    {
        public Material Template;
        [Range(.25f, 1f)] public float ResolutionScale = .5f;
        Material material;
        VolumetricCloudsURP.VolumetricCloudsPass pass;
        LightningCapturePass lightning;
        public override void Create()
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            pass = null;
            lightning = null;
            if (Template == null) return;
            lightning = new LightningCapturePass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
                requiresIntermediateTexture = true
            };
            material = new Material(Template) { hideFlags = HideFlags.HideAndDontSave };
            pass = new VolumetricCloudsURP.VolumetricCloudsPass(material, ResolutionScale,
                Shader.Find("Hidden/Universal Render Pipeline/CopyDepth"))
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
            if (pass == null || storm == null || !storm.Ready || !storm.RendersCamera(renderingData.cameraData.camera) || renderingData.cameraData.cameraType != CameraType.Game || renderingData.cameraData.renderType != CameraRenderType.Base) return;
            if (!storm.IsMenuPreview && renderingData.cameraData.camera != Camera.main) return;
            
            if (!storm.IsMenuPreview && !storm.TestCloudWall && Shader.GetGlobalFloat("_PirateStormBillows") > .5f && Shader.GetGlobalFloat("_PirateStormVolume3D") < .5f)
            {
                var eye = renderingData.cameraData.camera.transform.position;
                float radialDistance = Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(storm.CurrentCenter.x, storm.CurrentCenter.z));
                float inner = storm.EffectiveInnerThickness + 100 + storm.EffectiveInwardOffset + storm.CloudEdgeBreakup * 1.65f;
                float outer = storm.OuterThickness + 640 + storm.CloudEdgeBreakup * 1.65f;
                float nearestDistance = Mathf.Max(0, Mathf.Max(storm.CurrentRadius - inner - radialDistance, radialDistance - storm.CurrentRadius - outer));
                if (nearestDistance > 420) return;
            }
            storm.Apply(material);
            if (storm.TestCloudWall) material.EnableKeyword("_STORM_TEST_CLOUD_WALL");
            else material.DisableKeyword("_STORM_TEST_CLOUD_WALL");
            if (storm.TestCloudWall && !material.shader.isSupported) return;
#if UNITY_EDITOR
            if (storm.TestCloudWall)
            {
                material.EnableKeyword("_LOCAL_VOLUMETRIC_CLOUDS");
                material.DisableKeyword("_CLOUDS_MICRO_EROSION");
                material.DisableKeyword("_CLOUDS_AMBIENT_PROBE");
                material.DisableKeyword("_LOW_RESOLUTION_CLOUDS");
                material.DisableKeyword("_OUTPUT_CLOUDS_DEPTH");
                material.DisableKeyword("_PHYSICALLY_BASED_SUN");
                material.DisableKeyword("_PERCEPTUAL_BLENDING");
                bool ready = true;
                bool previousAsync = UnityEditor.ShaderUtil.allowAsyncCompilation;
                try
                {
                    UnityEditor.ShaderUtil.allowAsyncCompilation = true;
                    for (int index = 0; index < 4; index++)
                    {
                        if (UnityEditor.ShaderUtil.IsPassCompiled(material, index)) continue;
                        UnityEditor.ShaderUtil.CompilePass(material, index, false);
                        ready = false;
                    }
                }
                finally { UnityEditor.ShaderUtil.allowAsyncCompilation = previousAsync; }
                if (!ready) return;
            }
#endif
            pass.cloudsVolume = storm.CloudSettings;
            pass.afterTransparentDepth = storm.TestCloudWall;
            pass.colorAdjustments = null;
            pass.resolutionScale = storm.TestCloudWall ? 1f : Mathf.Clamp(ResolutionScale, .25f, storm.IsMenuPreview ? .67f : .4f);
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            if (storm.TestCloudWall) renderer.EnqueuePass(lightning);
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            pass = null;
            CoreUtils.Destroy(material);
            lightning = null;
        }

        sealed class LightningCapturePass : ScriptableRenderPass
        {
            sealed class Data
            {
                public RendererListHandle Renderers;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var descriptor = camera.cameraTargetDescriptor;
                descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.msaaSamples = (int)graph.GetTextureDesc(resources.activeDepthTexture).msaaSamples;
                descriptor.bindMS = false; descriptor.useMipMap = false; descriptor.autoGenerateMips = false;
                var emissionDesc = new TextureDesc(descriptor)
                {
                    name = "Storm lightning emission", clearBuffer = true, clearColor = Color.clear, filterMode = FilterMode.Bilinear
                };
                var distanceDesc = emissionDesc;
                distanceDesc.name = "Storm lightning distance"; distanceDesc.format = GraphicsFormat.R32_SFloat;
                var buffers = frameData.Create<VolumetricCloudsURP.VolumetricCloudsPass.StormLightningFrameData>();
                buffers.Emission = graph.CreateTexture(emissionDesc);
                buffers.Distance = graph.CreateTexture(distanceDesc);
                var drawing = RenderingUtils.CreateDrawingSettings(new ShaderTagId("StormTestLightning"), rendering,
                    camera, frameData.Get<UniversalLightData>(), SortingCriteria.CommonTransparent);
                var filtering = new FilteringSettings(RenderQueueRange.transparent, camera.camera.cullingMask);
                var renderers = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                using var builder = graph.AddRasterRenderPass<Data>("Storm lightning inside cloud volume", out var data);
                data.Renderers = renderers;
                builder.UseRendererList(renderers);
                builder.AllowPassCulling(false);
                builder.SetRenderAttachment(buffers.Emission, 0, AccessFlags.Write);
                builder.SetRenderAttachment(buffers.Distance, 1, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.SetRenderFunc(static (Data data, RasterGraphContext context) => context.cmd.DrawRendererList(data.Renderers));
            }
        }
    }
}
