using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed class FogCloudVisual : MonoBehaviour
    {
        public const float Duration = 15f;
        public float Radius = 50f;
        public float Height = 44f;
        public float Density = .18f;
        public bool InsideBottle;
        static Material fogMaterial;
        static Material bottleMaterial;
        static readonly int CenterId = Shader.PropertyToID("_VolumeCenter");
        static readonly int RadiiId = Shader.PropertyToID("_VolumeRadii");
        static readonly int AgeId = Shader.PropertyToID("_CloudAge");
        static readonly int OpacityId = Shader.PropertyToID("_CloudOpacity");
        static readonly int DensityId = Shader.PropertyToID("_Density");
        static readonly int AxisXId = Shader.PropertyToID("_VolumeAxisX");
        static readonly int AxisYId = Shader.PropertyToID("_VolumeAxisY");
        static readonly int AxisZId = Shader.PropertyToID("_VolumeAxisZ");
        Mesh volumeMesh;
        MeshRenderer volumeRenderer;
        Transform volumeTransform;
        MaterialPropertyBlock properties;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMaterial()
        {
            if (fogMaterial != null) Destroy(fogMaterial);
            if (bottleMaterial != null) Destroy(bottleMaterial);
            fogMaterial = null;
            bottleMaterial = null;
        }

        void Awake()
        {
            if (fogMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("BottleFog");
                if (shader == null) shader = Shader.Find("PirateSlop/BottleFog");
                if (shader == null) return;
                fogMaterial = new Material(shader) { name = "BottleFogRuntime", hideFlags = HideFlags.HideAndDontSave };
            }
            if (InsideBottle && bottleMaterial == null)
            {
                bottleMaterial = new Material(fogMaterial) { name = "BottleMistRuntime", renderQueue = 3000, hideFlags = HideFlags.HideAndDontSave };
                bottleMaterial.SetFloat("_InsideBottle", 1f);
                bottleMaterial.SetColor("_FogColor", new Color(.68f, .77f, .81f, 1f));
            }
            volumeMesh = new Mesh
            {
                name = "BottleFogVolume",
                vertices = new[]
                {
                    new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, -.5f, -.5f),
                    new Vector3(.5f, .5f, -.5f), new Vector3(-.5f, .5f, -.5f),
                    new Vector3(-.5f, -.5f, .5f), new Vector3(.5f, -.5f, .5f),
                    new Vector3(.5f, .5f, .5f), new Vector3(-.5f, .5f, .5f)
                },
                triangles = new[]
                {
                    0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                    0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5,
                    0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2
                }
            };
            volumeMesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            var volumeObject = new GameObject("BottleFogVolume");
            volumeObject.layer = gameObject.layer;
            volumeTransform = volumeObject.transform;
            volumeTransform.SetParent(transform, false);
            volumeObject.AddComponent<MeshFilter>().sharedMesh = volumeMesh;
            volumeRenderer = volumeObject.AddComponent<MeshRenderer>();
            volumeRenderer.sharedMaterial = InsideBottle ? bottleMaterial : fogMaterial;
            volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            volumeRenderer.receiveShadows = false;
            volumeRenderer.lightProbeUsage = LightProbeUsage.Off;
            volumeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            volumeRenderer.enabled = false;
            properties = new MaterialPropertyBlock();
        }

        public void SetAge(float seconds)
        {
            if (volumeRenderer == null) return;
            float age = Mathf.Max(0f, seconds);
            float growth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .55f));
            float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .2f)) *
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Duration - age) / 1.1f));
            volumeRenderer.enabled = seconds >= 0f && age < Duration && opacity > .001f;
            if (!volumeRenderer.enabled) return;
            float radius = Mathf.Max(.005f, Radius);
            float height = Mathf.Max(.01f, Height);
            volumeTransform.localPosition = Vector3.up * height * .35f;
            volumeTransform.localRotation = Quaternion.identity;
            volumeTransform.localScale = new Vector3(radius * 2f, height, radius * 2f);
            Vector3 scale = volumeTransform.lossyScale;
            properties.SetVector(CenterId, volumeTransform.position);
            properties.SetVector(RadiiId, new Vector4(Mathf.Abs(scale.x) * .5f * Mathf.Lerp(.12f, 1f, growth),
                Mathf.Abs(scale.y) * .5f * Mathf.Lerp(.4f, 1f, growth), Mathf.Abs(scale.z) * .5f * Mathf.Lerp(.12f, 1f, growth), 0f));
            properties.SetVector(AxisXId, volumeTransform.right);
            properties.SetVector(AxisYId, volumeTransform.up);
            properties.SetVector(AxisZId, volumeTransform.forward);
            properties.SetFloat(DensityId, Mathf.Max(0f, Density));
            properties.SetFloat(AgeId, age);
            properties.SetFloat(OpacityId, opacity);
            volumeRenderer.SetPropertyBlock(properties);
        }

        public void SetPersistentAge(float seconds)
        {
            SetAge(1f);
            if (volumeRenderer == null) return;
            properties.SetFloat(AgeId, seconds);
            volumeRenderer.SetPropertyBlock(properties);
        }

        void OnDestroy()
        {
            if (volumeMesh != null) Destroy(volumeMesh);
        }
    }
}
