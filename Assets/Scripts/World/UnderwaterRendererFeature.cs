using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace PirateSlop
{
    public sealed class UnderwaterRendererFeature : ScriptableRendererFeature
    {
        public Material Template;
        public Shader DepthCopyShader;
        public Vector3 Extinction = new(.028f, .014f, .017f);
        public Color FogTint = new(.007f, .055f, .065f, 1);
        Material material;
        ImmersionPass pass;

        public override void Create()
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            pass = null;
            if (Template == null || DepthCopyShader == null) return;
            material = new Material(Template) { hideFlags = HideFlags.HideAndDontSave };
            pass = new ImmersionPass(material, DepthCopyShader)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var medium = Shader.GetGlobalVector("_BoatAttack_CameraWater");
            if (pass == null || medium.w < .5f || medium.x < .5f ||
                renderingData.cameraData.cameraType != CameraType.Game ||
                renderingData.cameraData.renderType != CameraRenderType.Base) return;
            var surface = OceanSurface.Instance;
            if (surface == null || !(surface.HeightSource is BoatAttackOcean)) return;
            pass.Medium = medium;
            pass.Extinction = Extinction;
            pass.FogTint = FogTint;
            pass.Whirlpool = new Vector4(surface.WhirlpoolRadius, surface.WhirlpoolDepth, surface.SeaLevel, 0);
            pass.Center = surface.WhirlpoolCenter;
            Shader.SetGlobalFloat("_BoatAttack_UnderwaterPass", 1);
            Shader.SetGlobalVector("_UnderwaterExtinction", Extinction);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            pass = null;
            CoreUtils.Destroy(material);
        }

        sealed class ImmersionPass : ScriptableRenderPass
        {
            public Vector4 Medium, Whirlpool;
            public Vector3 Extinction, Center;
            public Color FogTint;
            readonly Material material;
            readonly CopyDepthPass copyDepth;
            static readonly ShaderTagId SuspensionTag = new("UnderwaterSuspension");
            static readonly int DepthId = Shader.PropertyToID("_UnderwaterSceneDepth");
            readonly MaterialPropertyBlock properties = new();

            sealed class FogData
            {
                public TextureHandle Source, Destination, Depth;
                public Material Material;
                public MaterialPropertyBlock Properties;
                public Vector4 Medium, Whirlpool;
                public Vector3 Extinction, Center;
                public Color FogTint;
            }

            sealed class SuspensionData { public RendererListHandle List; }

            public ImmersionPass(Material material, Shader depthShader)
            {
                this.material = material;
                profilingSampler = new ProfilingSampler("Underwater immersion");
                copyDepth = new CopyDepthPass(RenderPassEvent.BeforeRenderingPostProcessing, depthShader, customPassName: "Underwater surface depth");
            }

            public void Dispose() => copyDepth.Dispose();

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                if (resources.isActiveTargetBackBuffer) return;
                var descriptor = camera.cameraTargetDescriptor;
                descriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                var depth = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "Underwater scene depth", false, FilterMode.Point);
                copyDepth.Render(graph, frameData, depth, resources.activeDepthTexture, false, "Underwater surface depth");
                var colorDescriptor = graph.GetTextureDesc(resources.activeColorTexture);
                colorDescriptor.name = "Underwater scene color";
                colorDescriptor.clearBuffer = false;
                var color = graph.CreateTexture(colorDescriptor);
                graph.AddBlitPass(resources.activeColorTexture, color, Vector2.one, Vector2.zero, passName: "Underwater color copy");
                using (var builder = graph.AddRasterRenderPass<FogData>("Underwater immersion", out var data, profilingSampler))
                {
                    data.Source = color;
                    data.Destination = resources.activeColorTexture;
                    data.Depth = depth;
                    data.Material = material;
                    data.Properties = properties;
                    data.Medium = Medium;
                    data.Extinction = Extinction;
                    data.FogTint = FogTint;
                    data.Center = Center;
                    data.Whirlpool = Whirlpool;
                    builder.UseTexture(color, AccessFlags.Read);
                    builder.UseTexture(depth, AccessFlags.Read);
                    builder.SetRenderAttachment(data.Destination, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(depth, DepthId);
                    builder.SetRenderFunc(static (FogData data, RasterGraphContext context) =>
                    {
                        RTHandle source = data.Source;
                        var scale = source.useScaling ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y) : Vector2.one;
                        bool flip = context.GetTextureUVOrigin(in data.Source) != context.GetTextureUVOrigin(in data.Destination);
                        var bias = flip ? new Vector4(scale.x, -scale.y, 0, scale.y) : new Vector4(scale.x, scale.y, 0, 0);
                        var block = data.Properties;
                        block.Clear();
                        block.SetTexture("_BlitTexture", source);
                        block.SetTexture(DepthId, (RTHandle)data.Depth);
                        block.SetVector("_BlitScaleBias", bias);
                        block.SetVector("_WaterMedium", data.Medium);
                        block.SetVector("_WaterExtinction", data.Extinction);
                        block.SetVector("_WaterFogTint", new Vector4(data.FogTint.r, data.FogTint.g, data.FogTint.b, data.FogTint.a));
                        block.SetVector("_WaterWhirlpool", data.Whirlpool);
                        block.SetVector("_WaterWhirlpoolCenter", data.Center);
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1, block);
                    });
                }
                var drawing = new DrawingSettings(SuspensionTag, new SortingSettings(camera.camera) { criteria = SortingCriteria.CommonTransparent })
                {
                    enableInstancing = true,
                    perObjectData = PerObjectData.None
                };
                var filtering = new FilteringSettings(RenderQueueRange.transparent);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                using (var builder = graph.AddRasterRenderPass<SuspensionData>("Underwater suspension", out var data))
                {
                    data.List = list;
                    builder.UseRendererList(list);
                    builder.UseTexture(depth, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc(static (SuspensionData data, RasterGraphContext context) => context.cmd.DrawRendererList(data.List));
                }
            }
        }
    }
}
