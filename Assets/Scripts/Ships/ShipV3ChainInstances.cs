using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Ships
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class ShipV3ChainInstances : MonoBehaviour
    {
        public Transform[] Links = Array.Empty<Transform>();
        public Mesh SharedMesh;
        public Material SharedMaterial;

        Transform[] trackedLinks;
        MeshRenderer[] renderers;
        bool[] originalEnabled;
        Matrix4x4[] matrices;
        bool instanced;
        bool drawFailed;

        void OnEnable()
        {
            Links ??= Array.Empty<Transform>();
            trackedLinks = Links;
            renderers = new MeshRenderer[trackedLinks.Length];
            originalEnabled = new bool[trackedLinks.Length];
            matrices = trackedLinks.Length <= 1023 ? new Matrix4x4[trackedLinks.Length] : Array.Empty<Matrix4x4>();
            drawFailed = false;
            instanced = false;
            for (int i = 0; i < trackedLinks.Length; i++)
            {
                if (trackedLinks[i] == null) continue;
                renderers[i] = trackedLinks[i].GetComponent<MeshRenderer>();
                originalEnabled[i] = renderers[i] != null && renderers[i].enabled;
            }
            if (trackedLinks.Length > 1023)
                Debug.LogWarning("Ship V3 chain instancing supports at most 1023 links; using the original renderers.", this);
            SetInstanced(CanDraw());
        }

        bool CanDraw()
        {
            return !drawFailed && trackedLinks.Length > 0 && trackedLinks.Length <= 1023 &&
                SystemInfo.supportsInstancing && SharedMesh != null && SharedMesh.subMeshCount == 1 &&
                SharedMaterial != null && SharedMaterial.enableInstancing &&
                SharedMaterial.shader != null && SharedMaterial.shader.isSupported;
        }

        void SetInstanced(bool value)
        {
            if (instanced == value) return;
            instanced = value;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = value ? false : originalEnabled[i];
        }

        void LateUpdate()
        {
            if (!ReferenceEquals(trackedLinks, Links))
            {
                OnDisable();
                OnEnable();
            }
            SetInstanced(CanDraw());
            if (!instanced) return;
            int count = 0;
            for (int i = 0; i < trackedLinks.Length; i++)
            {
                var link = trackedLinks[i];
                var renderer = renderers[i];
                if (renderer != null && renderer.enabled) renderer.enabled = false;
                if (link == null || renderer == null || !originalEnabled[i] ||
                    !link.gameObject.activeInHierarchy || renderer.forceRenderingOff) continue;
                matrices[count++] = link.localToWorldMatrix;
            }
            if (count == 0) return;
            try
            {
                Graphics.DrawMeshInstanced(SharedMesh, 0, SharedMaterial, matrices, count, null,
                    ShadowCastingMode.On, true, gameObject.layer);
            }
            catch (InvalidOperationException exception)
            {
                drawFailed = true;
                SetInstanced(false);
                Debug.LogWarning("Ship V3 chain instancing is unavailable; using the original renderers. " + exception.Message, this);
            }
        }

        void OnDisable()
        {
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].enabled = originalEnabled[i];
            instanced = false;
        }

        void OnDestroy() => OnDisable();
    }
}
