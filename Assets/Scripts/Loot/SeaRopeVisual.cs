using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SeaRopeVisual : MonoBehaviour
    {
        const int Sides = 8;
        readonly List<Vector3> vertices = new(2400);
        readonly List<Vector3> normals = new(2400);
        readonly List<Vector4> tangents = new(2400);
        readonly List<Vector2> uv = new(2400);
        readonly List<int> triangles = new(14000);
        Mesh mesh;
        int previousCount;

        public void SetPath(Vector3[] points, int count, float radius)
        {
            if (count < 2) return;
            if (mesh == null)
            {
                mesh = new Mesh { name = "SeaEventRope" };
                mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            vertices.Clear(); normals.Clear(); tangents.Clear(); uv.Clear();
            Vector3 normal = Vector3.up;
            float distance = 0f;
            for (int i = 0; i < count; i++)
            {
                var tangent = (points[Mathf.Min(count - 1, i + 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                if (tangent.sqrMagnitude < .1f) tangent = Vector3.forward;
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                if (normal.sqrMagnitude < .1f) normal = Vector3.Cross(tangent, Vector3.right).normalized;
                var binormal = Vector3.Cross(tangent, normal).normalized;
                if (i > 0) distance += Vector3.Distance(points[i - 1], points[i]);
                for (int side = 0; side <= Sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / Sides;
                    var outward = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                    float strand = 1f + .055f * Mathf.Sin(angle * 3f - distance * 90f);
                    vertices.Add(points[i] + outward * (radius * strand));
                    normals.Add(outward);
                    var around = -normal * Mathf.Sin(angle) + binormal * Mathf.Cos(angle);
                    tangents.Add(new Vector4(around.x, around.y, around.z, 1f));
                    uv.Add(new Vector2(side / (float)Sides, distance / .24f));
                }
            }
            if (previousCount != count)
            {
                mesh.Clear();
                triangles.Clear();
                for (int i = 0; i < count - 1; i++)
                    for (int side = 0; side < Sides; side++)
                    {
                        int a = i * (Sides + 1) + side, b = a + Sides + 1;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                        triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                    }
            }
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uv);
            if (previousCount != count) mesh.SetTriangles(triangles, 0);
            previousCount = count;
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
