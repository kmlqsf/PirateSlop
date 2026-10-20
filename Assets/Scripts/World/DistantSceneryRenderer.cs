using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.World
{
    public sealed class DistantSceneryRenderer : MonoBehaviour
    {
        public const float MaximumDistance = 5000f;
        public const string Layer = "WorldHorizon";
        sealed class Batch
        {
            public Mesh Mesh;
            public readonly Matrix4x4[] Matrices = new Matrix4x4[1023];
            public int Count;
        }
        readonly List<Batch> batches = new();
        readonly Dictionary<Material, Material> nearMaterials = new();
        MaterialPropertyBlock properties;
        Material material;
        float seaLevel;
        int layer = -1;

        void OnEnable() { if (Application.isPlaying) RenderPipelineManager.beginCameraRendering += BeforeCamera; }
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        void OnDestroy()
        {
            if (material != null) Release(material);
            foreach (var entry in nearMaterials) Release(entry.Value);
        }
        static void Release(Material value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }

        public void UseNearFade(Renderer renderer)
        {
            if (renderer == null) return;
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || !source.HasProperty("_HorizonNearFade") || source.GetFloat("_HorizonNearFade") > .5f) continue;
                if (!nearMaterials.TryGetValue(source, out var variant))
                {
                    variant = new Material(source) { name = source.name + " Horizon Blend", hideFlags = HideFlags.HideAndDontSave };
                    variant.SetFloat("_HorizonNearFade", 1f); nearMaterials.Add(source, variant);
                }
                materials[i] = variant; changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }

        public void Add(Mesh mesh, Matrix4x4 matrix)
        {
            Batch target = null;
            foreach (var batch in batches) if (batch.Mesh == mesh && batch.Count < 1023) { target = batch; break; }
            if (target == null) { target = new Batch { Mesh = mesh }; batches.Add(target); }
            target.Matrices[target.Count++] = matrix;
            seaLevel = matrix.m13;
        }

        public static void ConfigureCamera(Camera camera)
        {
            int layer = LayerMask.NameToLayer(Layer);
            if (camera == null || layer < 0) return;
            var distances = camera.layerCullDistances;
            distances[layer] = MaximumDistance;
            camera.layerCullDistances = distances;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, MaximumDistance + 250f);
        }

        void BeforeCamera(ScriptableRenderContext context, Camera camera) => Draw(camera);

        public void Draw(Camera camera)
        {
            if (camera == null || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) || batches.Count == 0) return;
            if (layer < 0) layer = LayerMask.NameToLayer(Layer);
            if (layer < 0 || (camera.cullingMask & (1 << layer)) == 0) return;
            if (material == null)
            {
                var template = Resources.Load<Material>("DistantScenery");
                if (template == null) return;
                material = new Material(template) { name = "DistantScenery", enableInstancing = true, hideFlags = HideFlags.HideAndDontSave };
            }
            float night = TestSkyDayNight.Active != null ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.58f, 1f, TestSkyDayNight.Active.CurrentBlend)) : 0f;
            properties ??= new MaterialPropertyBlock();
            var stone = Color.Lerp(new Color(.24f, .31f, .33f), new Color(.018f, .026f, .04f), night);
            var haze = Color.Lerp(new Color(.58f, .7f, .76f), new Color(.045f, .065f, .10f), night);
            properties.SetColor("_Stone", stone);
            properties.SetColor("_Haze", haze);
            Shader.SetGlobalColor("_WorldHorizonStone", stone.linear);
            Shader.SetGlobalColor("_WorldHorizonHaze", haze.linear);
            properties.SetFloat("_SeaLevel", seaLevel);
            foreach (var batch in batches)
                Graphics.DrawMeshInstanced(batch.Mesh, 0, material, batch.Matrices, batch.Count, properties, ShadowCastingMode.Off, false, layer, camera, LightProbeUsage.Off);
        }
    }
}
