using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static partial class UpperDestructionAssetSetup
    {
        static Mesh UpperWheelMesh(Bounds bounds)
        {
            const int sides = 16;
            var vertices = new Vector3[sides * 2 + 2];
            var triangles = new int[sides * 12];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? bounds.min.x : bounds.max.x;
                vertices[sides * 2 + side] = new Vector3(x, bounds.center.y, bounds.center.z);
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    vertices[side * sides + i] = new Vector3(x, bounds.center.y + Mathf.Cos(angle) * bounds.extents.y, bounds.center.z + Mathf.Sin(angle) * bounds.extents.z);
                }
            }
            int at = 0;
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                foreach (int vertex in new[] { i, next, sides + i, next, sides + next, sides + i, sides * 2, next, i, sides * 2 + 1, sides + i, sides + next }) triangles[at++] = vertex;
            }
            var mesh = new Mesh { name = "FallenWheelConvex" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
