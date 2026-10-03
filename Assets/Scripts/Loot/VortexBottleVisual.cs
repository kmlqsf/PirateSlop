using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed class VortexBottleVisual : MonoBehaviour
    {
        public Material SwirlMaterial;
        public float RotationSpeed = 125f;
        Transform swirl;
        Mesh ownedMesh;

        void Awake()
        {
            const int arms = 3;
            const int segments = 72;
            const float turns = 1.9f;
            var vertices = new Vector3[arms * (segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[arms * segments * 6];
            int triangleIndex = 0;
            for (int arm = 0; arm < arms; arm++)
            {
                int firstVertex = arm * (segments + 1) * 2;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float height = segment / (float)segments;
                    float radius = .014f + .065f * Mathf.SmoothStep(0, 1, height);
                    float radiusSlope = .065f * 6f * height * (1f - height);
                    float angle = (height * turns + arm / (float)arms) * Mathf.PI * 2f;
                    float sine = Mathf.Sin(angle);
                    float cosine = Mathf.Cos(angle);
                    var center = new Vector3(cosine * radius, .045f + height * .3f, sine * radius);
                    var surfaceNormal = new Vector3(cosine, -radiusSlope / .3f, sine).normalized;
                    float angleSlope = turns * Mathf.PI * 2f;
                    var tangent = new Vector3(radiusSlope * cosine - radius * angleSlope * sine, .3f, radiusSlope * sine + radius * angleSlope * cosine);
                    var across = Vector3.Cross(surfaceNormal, tangent).normalized;
                    float endFade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(height / .06f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((1f - height) / .06f));
                    float halfWidth = Mathf.Lerp(.01f, .022f, height) * .5f;
                    int vertexIndex = firstVertex + segment * 2;
                    vertices[vertexIndex] = center - across * halfWidth;
                    vertices[vertexIndex + 1] = center + across * halfWidth;
                    uv[vertexIndex] = new Vector2(0, height);
                    uv[vertexIndex + 1] = new Vector2(1, height);
                    Color tint = Color.Lerp(new Color(.24f, .88f, 1f, .9f), new Color(.85f, 1f, 1f, .8f), height);
                    tint.a *= endFade;
                    colors[vertexIndex] = tint;
                    colors[vertexIndex + 1] = tint;
                    if (segment == segments) continue;
                    triangles[triangleIndex++] = vertexIndex;
                    triangles[triangleIndex++] = vertexIndex + 2;
                    triangles[triangleIndex++] = vertexIndex + 1;
                    triangles[triangleIndex++] = vertexIndex + 1;
                    triangles[triangleIndex++] = vertexIndex + 2;
                    triangles[triangleIndex++] = vertexIndex + 3;
                }
            }
            ownedMesh = new Mesh
            {
                name = "VortexBottleSpirals",
                vertices = vertices,
                uv = uv,
                colors = colors,
                triangles = triangles
            };
            ownedMesh.RecalculateNormals();
            ownedMesh.bounds = new Bounds(new Vector3(0, .2f, 0), new Vector3(.18f, .34f, .18f));
            var spiralObject = new GameObject("BottleVortex");
            spiralObject.layer = gameObject.layer;
            swirl = spiralObject.transform;
            swirl.SetParent(transform, false);
            spiralObject.AddComponent<MeshFilter>().sharedMesh = ownedMesh;
            var renderer = spiralObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SwirlMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void Update()
        {
            if (swirl != null) swirl.localRotation = Quaternion.Euler(0, Time.time * RotationSpeed, 0);
        }

        void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }
    }
}
