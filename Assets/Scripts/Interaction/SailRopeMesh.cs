using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SailRopeMesh : MonoBehaviour
    {
        public SailRopeVisual Rope;
        public float Radius = .023f;
        const int Sides = 12;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Vector3[] vertices, normals, points, lastPoints;
        Vector2[] uv;
        float[] distances;
        int[] triangles;
        int ringCount;
        void Awake()
        {
            mesh = new Mesh { name = "AnimatedLaidHemp" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
        }
        void LateUpdate()
        {
            if (Rope == null || Rope.Line == null || Rope.Sails == null) return;
            Rope.Line.enabled = false;
            meshRenderer.enabled = Rope.Sails.Efficiency(Rope.Index) > 0f;
            if (!meshRenderer.enabled) return;
            int count = Rope.Line.positionCount;
            if (count < 2) return;
            if (points == null || points.Length != count)
            { points = new Vector3[count]; lastPoints = new Vector3[count]; distances = new float[count]; ringCount = 0; }
            Rope.Line.GetPositions(points);
            bool changed = ringCount == 0;
            for (int i = 0; i < count; i++) if ((points[i] - lastPoints[i]).sqrMagnitude > .000001f) changed = true;
            if (!changed) return;
            System.Array.Copy(points, lastPoints, count);
            distances[0] = 0f;
            for (int i = 1; i < count; i++) distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            float length = distances[count - 1];
            int rings = ringCount > 0 ? ringCount : Mathf.Clamp(Mathf.CeilToInt(length * 26f), 24, 1200) + 1;
            if (rings != ringCount)
            {
                ringCount = rings;
                vertices = new Vector3[rings * (Sides + 1)]; normals = new Vector3[vertices.Length]; uv = new Vector2[vertices.Length];
                triangles = new int[(rings - 1) * Sides * 6];
                int k = 0;
                for (int j = 0; j < rings - 1; j++) for (int i = 0; i < Sides; i++)
                {
                    int a = j * (Sides + 1) + i, b = a + Sides + 1;
                    triangles[k++] = a; triangles[k++] = a + 1; triangles[k++] = b;
                    triangles[k++] = a + 1; triangles[k++] = b + 1; triangles[k++] = b;
                }
                mesh.Clear();
            }
            int segment = 1;
            Vector3 previousNormal = Vector3.right;
            for (int j = 0; j < rings; j++)
            {
                float distance = length * j / (rings - 1f);
                while (segment < count - 1 && distances[segment] < distance) segment++;
                float fraction = Mathf.InverseLerp(distances[segment - 1], distances[segment], distance);
                Vector3 center = Vector3.Lerp(points[segment - 1], points[segment], fraction);
                Vector3 tangent = (points[segment] - points[segment - 1]).normalized;
                Vector3 n = Vector3.ProjectOnPlane(previousNormal, tangent).normalized;
                if (n.sqrMagnitude < .1f) n = Vector3.Cross(tangent, Vector3.forward).normalized;
                if (n.sqrMagnitude < .1f) n = Vector3.Cross(tangent, Vector3.up).normalized;
                Vector3 b = Vector3.Cross(tangent, n).normalized;
                previousNormal = n;
                for (int i = 0; i <= Sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / Sides;
                    float twist = angle * 3f - distance * Mathf.PI * 2f / .115f;
                    float radius = Radius * (.84f + .16f * Mathf.Cos(twist));
                    Vector3 radial = n * Mathf.Cos(angle) + b * Mathf.Sin(angle);
                    int index = j * (Sides + 1) + i;
                    vertices[index] = center + radial * radius;
                    normals[index] = radial;
                    uv[index] = new Vector2(i / (float)Sides, distance / .115f);
                }
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
