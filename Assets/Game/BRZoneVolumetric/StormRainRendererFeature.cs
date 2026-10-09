using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace PirateSlop
{
    public sealed class StormRainRendererFeature : ScriptableRendererFeature
    {
        public Material Template;
        public Shader DepthCopyShader;
        Material material;
        RainPass pass;

        public override void Create()
        {
            pass?.Dispose(); CoreUtils.Destroy(material); pass = null;
            if (Template == null || DepthCopyShader == null) return;
            material = new Material(Template) { hideFlags = HideFlags.HideAndDontSave };
            pass = new RainPass(material, DepthCopyShader) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing, requiresIntermediateTexture = true };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var controller = StormRainController.Instance;
            if (pass == null || controller == null || renderingData.cameraData.cameraType != CameraType.Game ||
                renderingData.cameraData.renderType != CameraRenderType.Base || !controller.RendersCamera(renderingData.cameraData.camera)) return;
            var medium = Shader.GetGlobalVector("_BoatAttack_CameraWater");
            if (medium.w > .5f && medium.x > .5f) return;
            pass.DrawDownpour = StormVolumeController.Instance != null && StormVolumeController.Instance.TestCloudWall;
            pass.DrawScreen = controller.ApplyScreen(material, renderingData.cameraData.camera) || pass.DrawDownpour;
            if (!pass.DrawScreen && !pass.DrawDownpour) return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose(); pass = null; CoreUtils.Destroy(material);
        }

        sealed class RainPass : ScriptableRenderPass
        {
            public bool DrawDownpour;
            public bool DrawScreen;
            readonly Material material;
            readonly CopyDepthPass copyDepth;
            readonly MaterialPropertyBlock properties = new();
            static readonly int DepthId = Shader.PropertyToID("_RainSceneDepth");
            sealed class GeometryData
            {
                public RendererListHandle Renderers;
            }
            sealed class Data
            {
                public TextureHandle Color, Destination, Depth;
                public Material Material;
                public MaterialPropertyBlock Properties;
            }

            public RainPass(Material material, Shader depthShader)
            {
                this.material = material;
                profilingSampler = new ProfilingSampler("Storm rain lens and veil");
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                copyDepth = new CopyDepthPass(RenderPassEvent.BeforeRenderingPostProcessing, depthShader, customPassName: "Rain surface depth");
            }

            public void Dispose() => copyDepth.Dispose();

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return;
                if (DrawScreen)
                {
                var descriptor = camera.cameraTargetDescriptor;
                descriptor.graphicsFormat = GraphicsFormat.R32_SFloat; descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.msaaSamples = 1; descriptor.bindMS = false;
                var depth = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "Rain surface depth", false, FilterMode.Point);
                copyDepth.Render(graph, frameData, depth, resources.activeDepthTexture, false, "Rain surface depth");
                var colorDescriptor = graph.GetTextureDesc(resources.activeColorTexture);
                colorDescriptor.name = "Rain scene color"; colorDescriptor.clearBuffer = false;
                var color = graph.CreateTexture(colorDescriptor);
                graph.AddBlitPass(resources.activeColorTexture, color, Vector2.one, Vector2.zero, passName: "Rain color copy");
                using (var builder = graph.AddRasterRenderPass<Data>("Storm rain lens and veil", out var data, profilingSampler))
                {
                data.Color = color; data.Destination = resources.activeColorTexture; data.Depth = depth;
                data.Material = material; data.Properties = properties;
                builder.UseTexture(color, AccessFlags.Read); builder.UseTexture(depth, AccessFlags.Read);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (Data data, RasterGraphContext context) =>
                {
                    RTHandle source = data.Color;
                    var scale = source.useScaling ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y) : Vector2.one;
                    bool flip = context.GetTextureUVOrigin(in data.Color) != context.GetTextureUVOrigin(in data.Destination);
                    data.Properties.Clear();
                    data.Properties.SetTexture("_BlitTexture", source);
                    data.Properties.SetTexture(DepthId, (RTHandle)data.Depth);
                    data.Properties.SetVector("_BlitScaleBias", flip ? new Vector4(scale.x, -scale.y, 0, scale.y) : new Vector4(scale.x, scale.y, 0, 0));
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1, data.Properties);
                });
                }
                }
                if (DrawDownpour)
                {
                    var rendering = frameData.Get<UniversalRenderingData>();
                    var drawing = RenderingUtils.CreateDrawingSettings(new ShaderTagId("StormRainHeavy"), rendering,
                        camera, frameData.Get<UniversalLightData>(), SortingCriteria.CommonTransparent);
                    var filtering = new FilteringSettings(RenderQueueRange.transparent, camera.camera.cullingMask);
                    var renderers = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                    using (var geometry = graph.AddRasterRenderPass<GeometryData>("Storm downpour and contacts", out var geometryData))
                    {
                        geometryData.Renderers = renderers;
                        geometry.UseRendererList(renderers);
                        geometry.UseAllGlobalTextures(true);
                        geometry.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                        geometry.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                        geometry.SetRenderFunc(static (GeometryData data, RasterGraphContext context) => context.cmd.DrawRendererList(data.Renderers));
                    }
                }
            }
        }
    }
}
