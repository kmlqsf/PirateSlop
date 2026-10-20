using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.Editor
{
    public static class DistantSceneryBaker
    {
        public const string Revision = "horizon-3";
        public static Mesh Bake(GameObject prefab, float radius, string path)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var seen = new HashSet<MeshFilter>();
            foreach (var visual in prefab.GetComponentsInChildren<CoastalRockVisual>(true))
            {
                if (visual.Contours == null || visual.Contours.Length == 0) continue;
                var group = visual.GetComponent<LODGroup>();
                if (group == null) continue;
                var levels = group.GetLODs();
                if (levels.Length == 0) continue;
                foreach (var renderer in levels[levels.Length - 1].renderers)
                {
                    if (renderer == null) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || !seen.Add(filter)) continue;
                    using var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                    using var source = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                    data[0].GetVertices(source);
                    int offset = vertices.Count;
                    var matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    for (int i = 0; i < source.Length; i++) vertices.Add(matrix.MultiplyPoint3x4(source[i]));
                    for (int sub = 0; sub < data[0].subMeshCount; sub++)
                    {
                        using var indices = new NativeArray<int>(data[0].GetSubMesh(sub).indexCount, Allocator.Temp);
                        data[0].GetIndices(indices, sub, true);
                        foreach (int index in indices) triangles.Add(offset + index);
                    }
                }
            }
            foreach (var terrain in prefab.GetComponentsInChildren<Terrain>(true))
            {
                if (terrain.terrainData == null) continue;
                const int resolution = 64;
                var data = terrain.terrainData;
                int offset = vertices.Count;
                var matrix = prefab.transform.worldToLocalMatrix * terrain.transform.localToWorldMatrix;
                for (int z = 0; z <= resolution; z++) for (int x = 0; x <= resolution; x++)
                    vertices.Add(matrix.MultiplyPoint3x4(new Vector3(data.size.x * x / resolution, data.GetInterpolatedHeight(x / (float)resolution, z / (float)resolution), data.size.z * z / resolution)));
                for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
                {
                    int a = offset + z * (resolution + 1) + x, b = a + resolution + 1;
                    AddTriangle(vertices, triangles, a, b, a + 1);
                    AddTriangle(vertices, triangles, a + 1, b, b + 1);
                }
            }
            float cell = Mathf.Max(1.75f, radius * 2f / 64f);
            var lookup = new Dictionary<Vector3Int, int>();
            var sums = new List<Vector3>(); var counts = new List<int>();
            var remap = new int[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                var p = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x / cell), Mathf.RoundToInt(p.y / cell), Mathf.RoundToInt(p.z / cell));
                if (!lookup.TryGetValue(key, out int index))
                { index = sums.Count; lookup.Add(key, index); sums.Add(Vector3.zero); counts.Add(0); }
                sums[index] += p; counts[index]++; remap[i] = index;
            }
            for (int i = 0; i < sums.Count; i++) sums[i] /= counts[i];
            var reduced = new List<int>();
            var faces = new HashSet<(int, int, int)>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = remap[triangles[i]], b = remap[triangles[i + 1]], c = remap[triangles[i + 2]];
                if (a == b || b == c || a == c) continue;
                if (b < a && b < c) (a, b, c) = (b, c, a);
                else if (c < a && c < b) (a, b, c) = (c, a, b);
                if (!faces.Add((a, b, c)) || Vector3.Cross(sums[b] - sums[a], sums[c] - sums[a]).sqrMagnitude < .0001f) continue;
                reduced.Add(a); reduced.Add(b); reduced.Add(c);
            }
            var used = new Dictionary<int, int>();
            var compact = new List<Vector3>();
            for (int i = 0; i < reduced.Count; i++)
            {
                int source = reduced[i];
                if (!used.TryGetValue(source, out int target)) { target = compact.Count; used.Add(source, target); compact.Add(sums[source]); }
                reduced[i] = target;
            }
            var mesh = new Mesh { name = prefab.name + "_Horizon", indexFormat = compact.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(compact); mesh.SetTriangles(reduced, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); existing = mesh; }
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); }
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        static void AddTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
        {
            if (vertices[a].y < -.5f && vertices[b].y < -.5f && vertices[c].y < -.5f) return;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }
    }
}
