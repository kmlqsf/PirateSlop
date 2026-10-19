using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    static class UpperMeshCut
    {
        struct Vertex
        {
            public Vector3 Point, Normal;
            public Vector2 Uv;
            public Vector4 Tangent;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new() { Point = Vector3.Lerp(a.Point, b.Point, t), Normal = Vector3.Lerp(a.Normal, b.Normal, t), Uv = Vector2.Lerp(a.Uv, b.Uv, t), Tangent = Vector4.Lerp(a.Tangent, b.Tangent, t) };
        }
        sealed class Polygon
        {
            public int Slot;
            public List<Vertex> Vertices;
        }

        public static Mesh Box(Mesh source, Matrix4x4 toShip, Bounds bounds)
        {
            var vertices = source.vertices; var normals = source.normals; var uv = source.uv; var tangents = source.tangents;
            var polygons = new List<Polygon>();
            for (int slot = 0; slot < source.subMeshCount; slot++)
            {
                var indices = source.GetTriangles(slot);
                for (int at = 0; at < indices.Length; at += 3)
                {
                    var triangle = new List<Vertex>(3);
                    for (int j = 0; j < 3; j++)
                    {
                        int i = indices[at + j];
                        triangle.Add(new Vertex { Point = vertices[i], Normal = normals.Length > i ? normals[i] : Vector3.up, Uv = uv.Length > i ? uv[i] : Vector2.zero, Tangent = tangents.Length > i ? tangents[i] : new Vector4(1, 0, 0, 1) });
                    }
                    polygons.Add(new Polygon { Slot = slot, Vertices = triangle });
                }
            }
            for (int axis = 0; axis < 3; axis++)
            {
                var direction = Vector3.zero; direction[axis] = 1f;
                polygons = Clip(polygons, toShip, direction, bounds.min[axis], source.subMeshCount);
                polygons = Clip(polygons, toShip, -direction, -bounds.max[axis], source.subMeshCount);
            }
            var points = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var ts = new List<Vector4>();
            var triangles = Enumerable.Range(0, source.subMeshCount + 1).Select(_ => new List<int>()).ToArray();
            foreach (var polygon in polygons)
            {
                int start = points.Count;
                foreach (var v in polygon.Vertices) { points.Add(v.Point); ns.Add(v.Normal.normalized); uvs.Add(v.Uv); ts.Add(v.Tangent); }
                for (int i = 1; i + 1 < polygon.Vertices.Count; i++) triangles[polygon.Slot].AddRange(new[] { start, start + i, start + i + 1 });
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(points); mesh.SetNormals(ns); mesh.SetUVs(0, uvs); mesh.SetTangents(ts); mesh.subMeshCount = triangles.Length;
            for (int slot = 0; slot < triangles.Length; slot++) mesh.SetTriangles(triangles[slot], slot);
            mesh.RecalculateBounds();
            return mesh;
        }

        static List<Polygon> Clip(List<Polygon> polygons, Matrix4x4 matrix, Vector3 axis, float threshold, int cap)
        {
            var result = new List<Polygon>();
            var positions = new List<Vector3>();
            var weld = new Dictionary<Vector3Int, int>();
            var segments = new HashSet<(int A, int B)>();
            int Index(Vector3 point)
            {
                var position = matrix.MultiplyPoint3x4(point);
                var key = Vector3Int.RoundToInt(position * 10000f);
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (weld.TryGetValue(key + new Vector3Int(x, y, z), out int found) && (matrix.MultiplyPoint3x4(positions[found]) - position).sqrMagnitude < 1e-8f) return found;
                weld[key] = positions.Count; positions.Add(point); return positions.Count - 1;
            }
            foreach (var polygon in polygons)
            {
                var clipped = new List<Vertex>(); var crossings = new List<Vector3>();
                for (int i = 0; i < polygon.Vertices.Count; i++)
                {
                    var a = polygon.Vertices[i]; var b = polygon.Vertices[(i + 1) % polygon.Vertices.Count];
                    float da = Vector3.Dot(axis, matrix.MultiplyPoint3x4(a.Point)) - threshold;
                    float db = Vector3.Dot(axis, matrix.MultiplyPoint3x4(b.Point)) - threshold;
                    bool keepA = da >= -1e-6f, keepB = db >= -1e-6f;
                    if (keepA) clipped.Add(a);
                    if (keepA == keepB) continue;
                    var crossing = Vertex.Lerp(a, b, da / (da - db));
                    clipped.Add(crossing); crossings.Add(crossing.Point);
                }
                if (clipped.Count >= 3) result.Add(new Polygon { Slot = polygon.Slot, Vertices = clipped });
                if (crossings.Count == 2)
                {
                    int a = Index(crossings[0]), b = Index(crossings[1]);
                    if (a != b) segments.Add(a < b ? (a, b) : (b, a));
                }
            }
            var adjacent = new List<int>[positions.Count];
            for (int i = 0; i < adjacent.Length; i++) adjacent[i] = new List<int>();
            foreach (var segment in segments) { adjacent[segment.A].Add(segment.B); adjacent[segment.B].Add(segment.A); }
            var normal = -matrix.transpose.MultiplyVector(axis).normalized;
            var tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < .8f ? Vector3.up : Vector3.right).normalized;
            var bitangent = Vector3.Cross(normal, tangent);
            while (segments.Count > 0)
            {
                var first = segments.First();
                var loop = new List<int> { first.A }; int previous = first.A, current = first.B;
                segments.Remove(first);
                for (int guard = 0; guard <= positions.Count; guard++)
                {
                    if (current == loop[0]) break;
                    int repeated = loop.IndexOf(current);
                    if (repeated >= 0)
                    {
                        for (int i = 0; i < repeated; i++) { int a = loop[i], b = loop[i + 1]; segments.Add(a < b ? (a, b) : (b, a)); }
                        loop = loop.Skip(repeated).ToList();
                        break;
                    }
                    loop.Add(current); int next = -1;
                    foreach (int candidate in adjacent[current])
                        if (candidate != previous && segments.Contains(current < candidate ? (current, candidate) : (candidate, current))) { next = candidate; break; }
                    if (next < 0) break;
                    segments.Remove(current < next ? (current, next) : (next, current)); previous = current; current = next;
                }
                if (loop.Count < 3) continue;
                var contour = loop.Select(i => positions[i]).ToList();
                float area = 0f;
                for (int i = 0; i < contour.Count; i++) area += Vector3.Dot(Vector3.Cross(contour[i], contour[(i + 1) % contour.Count]), normal);
                if (area < 0f) contour.Reverse();
                var remaining = Enumerable.Range(0, contour.Count).ToList();
                Vector2 Project(Vector3 p) => new(Vector3.Dot(p, tangent), Vector3.Dot(p, bitangent));
                float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
                while (remaining.Count > 2)
                {
                    if (remaining.Count == 3)
                    {
                        if (Vector3.Dot(Vector3.Cross(contour[remaining[1]] - contour[remaining[0]], contour[remaining[2]] - contour[remaining[0]]), normal) < 0f) remaining.Reverse();
                        result.Add(new Polygon { Slot = cap, Vertices = remaining.Select(j => new Vertex { Point = contour[j], Normal = normal, Uv = Project(contour[j]) * 2f, Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1) }).ToList() });
                        break;
                    }
                    bool found = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        int ai = remaining[(i + remaining.Count - 1) % remaining.Count], bi = remaining[i], ci = remaining[(i + 1) % remaining.Count];
                        var a = Project(contour[ai]); var b = Project(contour[bi]); var c = Project(contour[ci]);
                        if (Cross(b - a, c - b) < 1e-12f) continue;
                        bool occupied = remaining.Any(j => j != ai && j != bi && j != ci && Cross(b - a, Project(contour[j]) - a) > 1e-12f && Cross(c - b, Project(contour[j]) - b) > 1e-12f && Cross(a - c, Project(contour[j]) - c) > 1e-12f);
                        if (occupied) continue;
                        result.Add(new Polygon { Slot = cap, Vertices = new[] { ai, bi, ci }.Select(j => new Vertex { Point = contour[j], Normal = normal, Uv = Project(contour[j]) * 2f, Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1) }).ToList() });
                        remaining.RemoveAt(i); found = true; break;
                    }
                    if (!found)
                    {
                        int collinear = remaining.FindIndex(i => {
                            int at = remaining.IndexOf(i); var a = Project(contour[remaining[(at + remaining.Count - 1) % remaining.Count]]); var b = Project(contour[i]); var c = Project(contour[remaining[(at + 1) % remaining.Count]]);
                            return Mathf.Abs(Cross(b - a, c - b)) < 1e-12f;
                        });
                        if (collinear < 0) throw new InvalidOperationException("A mast cut contour could not be triangulated: " + string.Join(";", remaining.Select(i => Project(contour[i]).ToString("F6"))));
                        remaining.RemoveAt(collinear);
                    }
                }
            }
            return result;
        }
    }
}
