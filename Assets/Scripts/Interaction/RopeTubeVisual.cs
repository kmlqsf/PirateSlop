using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(70)]
    public sealed class RopeTubeVisual : MonoBehaviour
    {
        public LineRenderer Line;
        public SkinnedMeshRenderer Skin;
        public float Radius = .022f;
        public float TilesPerMeter = 6f;
        Mesh mesh, baked;
        MeshRenderer output;
        int[][] rings;
        Vector3[] centers, vertices, normals, sourceVertices;
        Vector2[] uv;
        int[] triangles;
        float[] weights;

        void Awake()
        {
            var child = new GameObject("TexturedRopeTube");
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            mesh = new Mesh { name = "RopeTube" };
            mesh.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            output = child.AddComponent<MeshRenderer>();
            output.shadowCastingMode = ShadowCastingMode.Off;
            output.receiveShadows = true;
        }

        void ReadRings()
        {
            var coordinates = Skin.sharedMesh.uv;
            var groups = new SortedDictionary<int, List<int>>();
            for (int i = 0; i < coordinates.Length; i++)
            {
                int key = Mathf.RoundToInt(coordinates[i].x * 10000f);
                if (!groups.TryGetValue(key, out var group)) groups[key] = group = new List<int>();
                group.Add(i);
            }
            rings = new int[groups.Count][];
            int index = 0;
            foreach (var group in groups.Values) rings[index++] = group.ToArray();
            baked = new Mesh { name = "RopeCenterline" };
            weights = new float[Skin.sharedMesh.blendShapeCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = float.NaN;
        }

        void LateUpdate()
        {
            if (Line == null && Skin == null) { output.enabled = false; return; }
            bool visible = Line != null ? Line.enabled : Skin.enabled;
            output.enabled = visible;
            if (!visible) return;
            if (Line != null)
            {
                Line.forceRenderingOff = true;
                output.sharedMaterial = Line.sharedMaterial;
                if (centers == null || centers.Length != Line.positionCount) centers = new Vector3[Line.positionCount];
                for (int i = 0; i < centers.Length; i++) centers[i] = Line.useWorldSpace ? Line.GetPosition(i) : Line.transform.TransformPoint(Line.GetPosition(i));
                Radius = Line.widthMultiplier * .5f;
            }
            else
            {
                Skin.forceRenderingOff = true;
                output.sharedMaterial = Skin.sharedMaterial;
                if (rings == null) ReadRings();
                bool changed = centers == null;
                for (int i = 0; i < weights.Length; i++)
                {
                    float weight = Skin.GetBlendShapeWeight(i);
                    changed |= !Mathf.Approximately(weights[i], weight);
                    weights[i] = weight;
                }
                if (!changed) return;
                Skin.BakeMesh(baked, false);
                sourceVertices = baked.vertices;
                if (centers == null) centers = new Vector3[rings.Length];
                for (int i = 0; i < rings.Length; i++)
                {
                    Vector3 center = Vector3.zero;
                    foreach (int vertex in rings[i]) center += sourceVertices[vertex];
                    centers[i] = Skin.transform.TransformPoint(center / rings[i].Length);
                }
            }
            if (centers.Length < 2) { output.enabled = false; return; }
            const int sides = 8;
            int count = centers.Length * (sides + 1);
            if (vertices == null || vertices.Length != count)
            {
                vertices = new Vector3[count]; normals = new Vector3[count]; uv = new Vector2[count];
                triangles = new int[(centers.Length - 1) * sides * 6];
                int at = 0;
                for (int i = 0; i < centers.Length - 1; i++)
                    for (int side = 0; side < sides; side++)
                    {
                        int a = i * (sides + 1) + side, b = a + sides + 1;
                        triangles[at++] = a; triangles[at++] = a + 1; triangles[at++] = b;
                        triangles[at++] = a + 1; triangles[at++] = b + 1; triangles[at++] = b;
                    }
            }
            float distance = 0f;
            Vector3 across = Vector3.right;
            for (int i = 0; i < centers.Length; i++)
            {
                if (i > 0) distance += Vector3.Distance(centers[i - 1], centers[i]);
                Vector3 tangent = (centers[Mathf.Min(i + 1, centers.Length - 1)] - centers[Mathf.Max(0, i - 1)]).normalized;
                across = Vector3.ProjectOnPlane(across, tangent).normalized;
                if (across.sqrMagnitude < .01f) across = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < .9f ? Vector3.up : Vector3.forward).normalized;
                Vector3 other = Vector3.Cross(tangent, across).normalized;
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    Vector3 radial = across * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                    int index = i * (sides + 1) + side;
                    vertices[index] = transform.InverseTransformPoint(centers[i] + radial * Radius);
                    normals[index] = transform.InverseTransformDirection(radial);
                    uv[index] = new Vector2(distance * TilesPerMeter, (float)side / sides);
                }
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (Line != null) Line.forceRenderingOff = false;
            if (Skin != null) Skin.forceRenderingOff = false;
            if (mesh != null) Destroy(mesh);
            if (baked != null) Destroy(baked);
        }
    }
}
