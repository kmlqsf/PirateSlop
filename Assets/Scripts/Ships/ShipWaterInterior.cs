using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Ships
{
    public sealed class ShipWaterInterior : MonoBehaviour
    {
        public Texture2D WidthProfile;
        public Vector4 ProfileBounds;
        static readonly List<ShipWaterInterior> active = new();
        static readonly Matrix4x4[] matrices = new Matrix4x4[16];
        static readonly int CountId = Shader.PropertyToID("_ShipWaterInteriorCount");
        static readonly int MatricesId = Shader.PropertyToID("_ShipWaterInteriorMatrices");
        static readonly int ProfileId = Shader.PropertyToID("_ShipWaterInteriorProfile");
        static readonly int MappingId = Shader.PropertyToID("_ShipWaterInteriorMapping");
        static readonly int SizeId = Shader.PropertyToID("_ShipWaterInteriorSize");
        static readonly int CameraInsideId = Shader.PropertyToID("_ShipWaterInteriorCameraInside");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitializeRegistry()
        {
            RenderPipelineManager.beginCameraRendering -= Publish;
            active.Clear();
            Shader.SetGlobalInteger(CountId, 0);
            Shader.SetGlobalInteger(CameraInsideId, 0);
        }

        void OnEnable()
        {
            if (!Application.isPlaying || WidthProfile == null) return;
            if (active.Count == 0) RenderPipelineManager.beginCameraRendering += Publish;
            if (!active.Contains(this)) active.Add(this);
        }

        void OnDisable()
        {
            active.Remove(this);
            if (active.Count != 0) return;
            RenderPipelineManager.beginCameraRendering -= Publish;
            Shader.SetGlobalInteger(CountId, 0);
            Shader.SetGlobalInteger(CameraInsideId, 0);
        }

        static void Publish(ScriptableRenderContext context, Camera camera)
        {
            int count = 0;
            ShipWaterInterior profile = null;
            foreach (var interior in active)
            {
                if (interior == null || interior.WidthProfile == null || count == matrices.Length) continue;
                profile ??= interior;
                matrices[count++] = interior.transform.worldToLocalMatrix;
            }
            Shader.SetGlobalInteger(CountId, count);
            Shader.SetGlobalInteger(CameraInsideId, Contains(camera.transform.position) ? 1 : 0);
            if (profile == null) return;
            var bounds = profile.ProfileBounds;
            var texture = profile.WidthProfile;
            Shader.SetGlobalMatrixArray(MatricesId, matrices);
            Shader.SetGlobalTexture(ProfileId, texture);
            Shader.SetGlobalVector(MappingId, new Vector4(bounds.x, bounds.z, 1f / (bounds.y - bounds.x), 1f / (bounds.w - bounds.z)));
            Shader.SetGlobalVector(SizeId, new Vector4(1f / texture.width, 1f / texture.height, texture.width, texture.height));
        }

        public static bool Contains(Vector3 position)
        {
            foreach (var interior in active)
                if (interior != null && interior.ContainsPoint(position)) return true;
            return false;
        }

        public static bool TryWaterHeight(Vector3 position, out float height)
        {
            foreach (var interior in active)
            {
                if (interior == null || !interior.ContainsPoint(position)) continue;
                var flooding = interior.GetComponent<ShipFlooding>();
                height = float.NegativeInfinity;
                if (flooding == null || !flooding.UseBilgeFlooding || flooding.Level <= .0001f) return true;
                var local = interior.transform.InverseTransformPoint(position);
                var water = interior.GetComponent<ShipBilgeWater>();
                local.y = water != null && water.isActiveAndEnabled ? water.SurfaceHeight(local) : flooding.BilgeSurfaceHeight;
                height = interior.transform.TransformPoint(local).y;
                return true;
            }
            height = 0f;
            return false;
        }

        public static bool IsSubmerged(Vector3 position, float margin = .1f)
        {
            if (TryWaterHeight(position, out float height)) return position.y < height - margin;
            var ocean = OceanSurface.Instance;
            return ocean != null && position.y < ocean.Height(position) - margin;
        }

        public bool ContainsPoint(Vector3 position)
        {
            if (WidthProfile == null) return false;
            var local = transform.InverseTransformPoint(position);
            var bounds = ProfileBounds;
            if (local.z < bounds.x || local.z > bounds.y || local.y < bounds.z || local.y > bounds.w || Mathf.Abs(local.x) > 7f) return false;
            float u = (local.z - bounds.x) / (bounds.y - bounds.x);
            float v = (local.y - bounds.z) / (bounds.w - bounds.z);
            float width = WidthProfile.GetPixelBilinear((u * (WidthProfile.width - 1) + .5f) / WidthProfile.width, (v * (WidthProfile.height - 1) + .5f) / WidthProfile.height).r;
            return width > 0f && Mathf.Abs(local.x) <= width;
        }
        public static bool KeepFishOutside(ref Vector3 position, float clearance, out Vector3 away)
        {
            away = Vector3.zero;
            bool corrected = false;
            foreach (var interior in active)
            {
                if (interior == null || interior.WidthProfile == null) continue;
                var local = interior.transform.InverseTransformPoint(position);
                var bounds = interior.ProfileBounds;
                if (local.z < bounds.x - clearance || local.z > bounds.y + clearance || local.y < bounds.z - clearance || local.y > bounds.w + clearance || Mathf.Abs(local.x) > 7f + clearance) continue;
                float width = 0f;
                for (int sample = -1; sample <= 1; sample++)
                {
                    float u = Mathf.Clamp01((local.z + sample * clearance - bounds.x) / (bounds.y - bounds.x));
                    float v = Mathf.Clamp01((local.y - bounds.z) / (bounds.w - bounds.z));
                    width = Mathf.Max(width, interior.WidthProfile.GetPixelBilinear((u * (interior.WidthProfile.width - 1) + .5f) / interior.WidthProfile.width, (v * (interior.WidthProfile.height - 1) + .5f) / interior.WidthProfile.height).r);
                }
                if (width <= .01f || Mathf.Abs(local.x) >= width + clearance) continue;
                float sign = local.x < 0f ? -1f : 1f;
                local.x = sign * (width + clearance + .05f);
                var safe = interior.transform.TransformPoint(local);
                away += safe - position;
                position = safe;
                corrected = true;
            }
            return corrected;
        }
    }
}
