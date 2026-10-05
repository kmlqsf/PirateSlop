#region Modified for PirateSlop Unity 6 integration
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
namespace Oceana
{
    public class ScreenCopyPassData : ContextItem
    {
        public TextureHandle sceneColor;
        public TextureHandle sceneDepth;
        public override void Reset() { sceneColor = TextureHandle.nullHandle; sceneDepth = TextureHandle.nullHandle; }
    }
    public sealed class OceanaSurfacePass : ScriptableRenderPass
    {
        OceanaSettings settings;
        public OceanaSurfacePass(RenderPassEvent injection) { renderPassEvent = injection; ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth); }
        public void FetchSettings(OceanaSettings value) => settings = value;
        sealed class PassData
        {
            public Material Material;
            public Mesh Mesh;
            public MaterialPropertyBlock Properties;
            public TextureHandle Scroll, Color, Depth;
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var scroll = frameData.Get<OceanaScrollPass.ScrollGlobalData>();
            if (!scroll.scrollMap.IsValid() || !resources.cameraDepthTexture.IsValid()) return;
            var desc = graph.GetTextureDesc(resources.activeColorTexture);
            desc.name = "Oceana Refraction";
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            var color = graph.CreateTexture(desc);
            graph.AddBlitPass(resources.activeColorTexture, color, Vector2.one, Vector2.zero);
            using var builder = graph.AddRasterRenderPass<PassData>("Oceana Ocean Surface", out var data);
            data.Material = settings.SurfaceMaterial;
            data.Properties = new MaterialPropertyBlock();
            var ocean = PirateSlop.OceanaOcean.Instance;
            var surface = ocean.Surface;
            var cameraPosition = frameData.Get<UniversalCameraData>().camera.transform.position;
            data.Mesh = ocean.GetSurfaceMesh(frameData.Get<UniversalCameraData>().camera);
            PirateSlop.WaterShipFoam.Apply(data.Properties);
            data.Properties.SetFloat("_CameraAboveWater", cameraPosition.y >= surface.Height(cameraPosition) ? 1f : 0f);
            data.Properties.SetFloat("_SeaLevel", surface.SeaLevel);
            data.Properties.SetFloat("_DisplaceHeight", settings.DisplaceHeight);
            data.Properties.SetVector("_ScrollMap_ST", settings.ScrollST);
            data.Properties.SetFloat("_OceanWaveTime", ocean.WaveTime);
            data.Properties.SetVector("_WhirlpoolCenter", surface.WhirlpoolCenter);
            data.Properties.SetVector("_Whirlpool", new Vector4(surface.WhirlpoolRadius, surface.WhirlpoolDepth, surface.WhirlpoolTwist, 0f));
            data.Scroll = scroll.scrollMap; data.Color = color; data.Depth = resources.cameraDepthTexture;
            builder.UseTexture(scroll.scrollMap, AccessFlags.Read);
            builder.UseTexture(color, AccessFlags.Read);
            builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
            builder.UseAllGlobalTextures(true);
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                pass.Properties.SetTexture("_ScrollMap", pass.Scroll);
                pass.Properties.SetTexture("_SourceColor", pass.Color);
                pass.Properties.SetTexture("_SourceDepth", pass.Depth);
                context.cmd.DrawMesh(pass.Mesh, Matrix4x4.identity, pass.Material, 0, 0, pass.Properties);
            });
        }
        public void Dispose() { }
    }
}
#endregion
