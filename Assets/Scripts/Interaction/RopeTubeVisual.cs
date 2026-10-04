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
        Vector3[] centers, vertices, normals;
        readonly List<Vector3> sourceVertices = new();
        sealed class Centerline
        {
            public Vector3[] Rest;
            public Vector3[][] Deltas;
            public float[] FrameWeights;
        }
        static readonly Dictionary<Mesh, Centerline> centerlines = new();
        Centerline centerline;
        bool topologyReady;
        static readonly Unity.Profiling.ProfilerMarker marker = new("Ships.RopeTubes");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => centerlines.Clear();
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
            weights = new float[Skin.sharedMesh.blendShapeCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = float.NaN;
            var source = Skin.sharedMesh;
            bool simple = Skin.bones.Length == 0 && source.bindposes.Length == 0;
            for (int i = 0; i < weights.Length; i++)
                simple &= source.GetBlendShapeFrameCount(i) == 1 && source.GetBlendShapeFrameWeight(i, 0) > 0f;
            if (!simple) { baked = new Mesh { name = "RopeCenterline" }; return; }
            if (centerlines.TryGetValue(source, out centerline)) return;
            centerline = new Centerline { Rest = AverageRings(source.vertices), Deltas = new Vector3[weights.Length][], FrameWeights = new float[weights.Length] };
            var delta = new Vector3[source.vertexCount];
            for (int i = 0; i < weights.Length; i++)
            {
                source.GetBlendShapeFrameVertices(i, 0, delta, null, null);
                centerline.Deltas[i] = AverageRings(delta);
                centerline.FrameWeights[i] = source.GetBlendShapeFrameWeight(i, 0);
            }
            centerlines.Add(source, centerline);
        }

        Vector3[] AverageRings(IReadOnlyList<Vector3> values)
        {
            var result = new Vector3[rings.Length];
            for (int i = 0; i < rings.Length; i++)
            {
                foreach (int vertex in rings[i]) result[i] += values[vertex];
                result[i] /= rings[i].Length;
            }
            return result;
        }

        void LateUpdate()
        {
            using var sample = marker.Auto();
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
                if (centers == null) centers = new Vector3[rings.Length];
                if (centerline == null) { Skin.BakeMesh(baked, false); baked.GetVertices(sourceVertices); }
                var world = Skin.transform.localToWorldMatrix;
                for (int i = 0; i < rings.Length; i++)
                {
                    Vector3 center = Vector3.zero;
                    if (centerline != null)
                    {
                        center = centerline.Rest[i];
                        for (int shape = 0; shape < weights.Length; shape++) center += centerline.Deltas[shape][i] * (weights[shape] / centerline.FrameWeights[shape]);
                    }
                    else
                    {
                        foreach (int vertex in rings[i]) center += sourceVertices[vertex];
                        center /= rings[i].Length;
                    }
                    centers[i] = world.MultiplyPoint3x4(center);
                }
            }
            if (centers.Length < 2) { output.enabled = false; return; }
            const int sides = 8;
            int count = centers.Length * (sides + 1);
            if (vertices == null || vertices.Length != count)
            {
                vertices = new Vector3[count]; normals = new Vector3[count]; uv = new Vector2[count];
                triangles = new int[(centers.Length - 1) * sides * 6];
                topologyReady = false;
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
            var local = transform.worldToLocalMatrix;
            var rotation = Quaternion.Inverse(transform.rotation);
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
                    vertices[index] = local.MultiplyPoint3x4(centers[i] + radial * Radius);
                    normals[index] = rotation * radial;
                    uv[index] = new Vector2(distance * TilesPerMeter, (float)side / sides);
                }
            }
            if (!topologyReady) mesh.Clear();
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv;
            if (!topologyReady) { mesh.triangles = triangles; topologyReady = true; }
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
