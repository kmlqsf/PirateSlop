using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    // Keep the local body visible to scene/other cameras, without clipping the FPS view.
    public sealed class FirstPersonModelVisibility : MonoBehaviour
    {
        [SerializeField] Camera ownerCamera;
        Renderer[] renderers;
        ShadowCastingMode[] savedModes;
        bool hidden;
        bool firstPerson = true;
        public void SetFirstPerson(bool value) { Restore(); firstPerson = value; }
        public void Configure(Camera camera) => ownerCamera = camera;
        void OnEnable()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            savedModes = new ShadowCastingMode[renderers.Length];
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            Restore();
            if (camera != ownerCamera || !firstPerson) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                savedModes[i] = renderers[i].shadowCastingMode;
                renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            hidden = true;
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera) => Restore();
        void Restore()
        {
            if (!hidden) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].shadowCastingMode = savedModes[i];
            hidden = false;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            Restore();
        }
    }
}
