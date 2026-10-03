using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed class VortexBottleVisual : MonoBehaviour
    {
        public Material SwirlMaterial;
        public float RotationSpeed = 90f;
        static readonly int AgeId = Shader.PropertyToID("_SwirlAge");
        MeshRenderer swirlRenderer;
        MaterialPropertyBlock properties;
        Mesh ownedMesh;
        float animationAge;

        void Awake()
        {
            if (SwirlMaterial == null) return;
            ownedMesh = new Mesh
            {
                name = "VortexBottleVolume",
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
            ownedMesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            var volumeObject = new GameObject("BottleVortex");
            volumeObject.layer = gameObject.layer;
            volumeObject.transform.SetParent(transform, false);
            volumeObject.transform.localPosition = new Vector3(0, .195f, 0);
            volumeObject.transform.localScale = new Vector3(.21f, .32f, .21f);
            volumeObject.AddComponent<MeshFilter>().sharedMesh = ownedMesh;
            swirlRenderer = volumeObject.AddComponent<MeshRenderer>();
            swirlRenderer.sharedMaterial = SwirlMaterial;
            swirlRenderer.shadowCastingMode = ShadowCastingMode.Off;
            swirlRenderer.receiveShadows = false;
            swirlRenderer.lightProbeUsage = LightProbeUsage.Off;
            swirlRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            properties = new MaterialPropertyBlock();
            animationAge = Mathf.Repeat(GetEntityId().GetHashCode() * .618f, 20f);
            UpdateVisual();
        }

        void LateUpdate()
        {
            animationAge += Mathf.Min(Time.deltaTime, .05f) * RotationSpeed / 125f;
            UpdateVisual();
        }

        void UpdateVisual()
        {
            if (swirlRenderer == null) return;
            properties.SetFloat(AgeId, animationAge);
            swirlRenderer.SetPropertyBlock(properties);
        }

        void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }
    }
}
