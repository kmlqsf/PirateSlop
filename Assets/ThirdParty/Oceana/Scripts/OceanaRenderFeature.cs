#region Modified for PirateSlop Unity 6 integration
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Oceana
{
    public sealed class OceanaRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] OceanaSettings m_Settings;
        OceanaScrollPass scroll;
        OceanaSurfacePass surface;
        public override void Create()
        {
            scroll?.Dispose();
            surface?.Dispose();
            if (m_Settings == null) return;
            scroll = new OceanaScrollPass(RenderPassEvent.BeforeRenderingOpaques);
            scroll.FetchSettings(m_Settings);
            surface = new OceanaSurfacePass(RenderPassEvent.AfterRenderingTransparents);
            surface.FetchSettings(m_Settings);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var ocean = PirateSlop.OceanaOcean.Instance;
            if (ocean == null || ocean.Settings != m_Settings || scroll == null || surface == null || data.cameraData.cameraType == CameraType.Preview) return;
            renderer.EnqueuePass(scroll);
            renderer.EnqueuePass(surface);
        }
        protected override void Dispose(bool disposing) { scroll?.Dispose(); surface?.Dispose(); }
    }
}
#endregion
