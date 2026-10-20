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
            var axes = new List<Vector3>(); var thresholds = new List<float>();
            for (int axis = 0; axis < 3; axis++)
            {
                var direction = Vector3.zero; direction[axis] = 1f;
                axes.Add(direction); thresholds.Add(bounds.min[axis]);
                axes.Add(-direction); thresholds.Add(-bounds.max[axis]);
            }
            return Cut(source, toShip, axes.ToArray(), thresholds.ToArray());
        }

        public static Mesh Sector(Mesh source, int axis, int sector, int count = 3)
        {
            var original = source;
            source = UnityEngine.Object.Instantiate(source);
            var warped = source.vertices;
            Vector3 Twist(Vector3 point, float sign)
            {
                var center = original.bounds.center;
                var delta = point - center;
                var normalAxis = Vector3.zero; normalAxis[axis] = 1f;
                float radius = Vector3.ProjectOnPlane(delta, normalAxis).magnitude;
                float wave = Mathf.Sin(radius * 4100f) * 4f + Mathf.Sin(radius * 1750f + delta[axis] * 8200f) * 3f;
                return center + Quaternion.AngleAxis(sign * wave, normalAxis) * delta;
            }
            for (int i = 0; i < warped.Length; i++) warped[i] = Twist(warped[i], 1f);
            source.vertices = warped;
            var normal = Vector3.zero; normal[axis] = 1f;
            var radial = Vector3.zero; radial[(axis + 1) % 3] = 1f;
            var start = Quaternion.AngleAxis(sector * 360f / count, normal) * radial;
            var end = Quaternion.AngleAxis((sector + 1) * 360f / count, normal) * radial;
            var axes = new[] { Vector3.Cross(normal, start), Vector3.Cross(end, normal) };
            var mesh = Cut(source, Matrix4x4.Scale(Vector3.one * 100f), axes, axes.Select(a => Vector3.Dot(a, original.bounds.center * 100f)).ToArray());
            warped = mesh.vertices;
            for (int i = 0; i < warped.Length; i++) warped[i] = Twist(warped[i], -1f);
            mesh.vertices = warped; SmoothCaps(mesh, original.subMeshCount); mesh.RecalculateBounds();
            UnityEngine.Object.DestroyImmediate(source);
            return mesh;
        }

        public static Mesh Band(Mesh source, int axis, float min, float max, float roughness)
        {
            var warped = UnityEngine.Object.Instantiate(source);
            float Offset(Vector3 point) => roughness * (.65f * Mathf.Sin(point[(axis + 1) % 3] * 4700f + point[(axis + 2) % 3] * 1900f) + .35f * Mathf.Sin(point[(axis + 1) % 3] * 9900f - point[(axis + 2) % 3] * 5600f));
            var vertices = source.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i][axis] += Offset(vertices[i]);
            warped.vertices = vertices;
            var direction = Vector3.zero; direction[axis] = 1f;
            var axes = new List<Vector3>(); var thresholds = new List<float>();
            if (!float.IsNegativeInfinity(min)) { axes.Add(direction); thresholds.Add(min * 100f); }
            if (!float.IsPositiveInfinity(max)) { axes.Add(-direction); thresholds.Add(-max * 100f); }
            var mesh = Cut(warped, Matrix4x4.Scale(Vector3.one * 100f), axes.ToArray(), thresholds.ToArray());
            vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i][axis] -= Offset(vertices[i]);
            mesh.vertices = vertices; SmoothCaps(mesh, source.subMeshCount); mesh.RecalculateBounds();
            UnityEngine.Object.DestroyImmediate(warped);
            return mesh;
        }

        static void SmoothCaps(Mesh mesh, int firstCap)
        {
            var points = mesh.vertices; var normals = mesh.normals;
            var sums = new Dictionary<Vector3Int, Vector3>();
            var cap = new HashSet<int>();
            for (int sub = firstCap; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                for (int at = 0; at < triangles.Length; at += 3)
                {
                    int a = triangles[at], b = triangles[at + 1], c = triangles[at + 2];
                    var normal = Vector3.Cross((points[b] - points[a]) * 100f, (points[c] - points[a]) * 100f).normalized;
                    foreach (int vertex in new[] { a, b, c })
                    {
                        var key = Vector3Int.RoundToInt(points[vertex] * 10000000f);
                        sums.TryGetValue(key, out var sum); sums[key] = sum + normal; cap.Add(vertex);
                    }
                }
            }
            foreach (int vertex in cap) normals[vertex] = sums[Vector3Int.RoundToInt(points[vertex] * 10000000f)].normalized;
            mesh.normals = normals;
        }

        static Mesh Cut(Mesh source, Matrix4x4 toShip, Vector3[] axes, float[] thresholds)
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
            for (int plane = 0; plane < axes.Length; plane++) polygons = Clip(polygons, toShip, axes[plane], thresholds[plane], source.subMeshCount);
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
            var normal = -matrix.transpose.MultiplyVector(axis).normalized;
            var tangent = Vector3.Cross(-axis, Mathf.Abs(axis.y) < .8f ? Vector3.up : Vector3.right).normalized;
            var bitangent = Vector3.Cross(-axis, tangent);
            Vector2 Project(Vector3 p) { var point = matrix.MultiplyPoint3x4(p); return new(Vector3.Dot(point, tangent), Vector3.Dot(point, bitangent)); }
            var projected = positions.Select(Project).ToArray();
            var adjacent = Enumerable.Range(0, positions.Count).Select(_ => new List<int>()).ToArray();
            foreach (var edge in segments) { adjacent[edge.A].Add(edge.B); adjacent[edge.B].Add(edge.A); }
            var compact = new List<Polygon>();
            bool Compact()
            {
                if (adjacent.Any(a => a.Count != 2)) return false;
                var visited = new HashSet<int>(); var loops = new List<List<int>>();
                for (int start = 0; start < positions.Count; start++)
                {
                    if (visited.Contains(start)) continue;
                    var loop = new List<int>(); int previous = -1, current = start;
                    do
                    {
                        if (!visited.Add(current)) return false;
                        loop.Add(current); int next = adjacent[current][0] == previous ? adjacent[current][1] : adjacent[current][0];
                        previous = current; current = next;
                    } while (current != start);
                    loops.Add(loop);
                }
                bool Inside(Vector2 point, List<int> loop)
                {
                    bool inside = false;
                    for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
                    {
                        var a = projected[loop[i]]; var b = projected[loop[j]];
                        if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    }
                    return inside;
                }
                foreach (var loop in loops)
                    foreach (var other in loops) if (other != loop && Inside(projected[loop[0]], other)) return false;
                float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
                foreach (var original in loops)
                {
                    var loop = new List<int>(original); float area = 0;
                    for (int i = 0; i < loop.Count; i++) area += Cross2(projected[loop[i]], projected[loop[(i + 1) % loop.Count]]);
                    if (area < 0) loop.Reverse();
                    while (loop.Count > 2)
                    {
                        int ear = -1;
                        for (int i = 0; i < loop.Count; i++)
                        {
                            int ai = loop[(i + loop.Count - 1) % loop.Count], bi = loop[i], ci = loop[(i + 1) % loop.Count];
                            var a = projected[ai]; var b = projected[bi]; var c = projected[ci];
                            if (Cross2(b - a, c - b) <= 1e-9f) continue;
                            if (loop.Any(v => v != ai && v != bi && v != ci && Cross2(b - a, projected[v] - a) > 1e-9f && Cross2(c - b, projected[v] - b) > 1e-9f && Cross2(a - c, projected[v] - c) > 1e-9f)) continue;
                            var triangle = new[] { ai, bi, ci }.Select(v => new Vertex { Point = positions[v], Normal = normal, Uv = projected[v] * 2f, Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1f) }).ToList();
                            if (Vector3.Dot(Vector3.Cross(triangle[1].Point - triangle[0].Point, triangle[2].Point - triangle[0].Point), normal) < 0) triangle.Reverse();
                            compact.Add(new Polygon { Slot = cap, Vertices = triangle }); ear = i; break;
                        }
                        if (ear < 0)
                        {
                            int collinear = loop.FindIndex(v => { int at = loop.IndexOf(v); return Mathf.Abs(Cross2(projected[v] - projected[loop[(at + loop.Count - 1) % loop.Count]], projected[loop[(at + 1) % loop.Count]] - projected[v])) < 1e-9f; });
                            if (collinear < 0) return false;
                            loop.RemoveAt(collinear);
                        }
                        else loop.RemoveAt(ear);
                    }
                }
                return true;
            }
            if (Compact()) { result.AddRange(compact); return result; }
            var edges = segments.Select(s => (A: s.A, B: s.B)).ToArray();
            var cuts = projected.Select(p => p.x).ToList();
            float Cross(Vector2 p, Vector2 q) => p.x * q.y - p.y * q.x;
            for (int first = 0; first < edges.Length; first++)
                for (int second = first + 1; second < edges.Length; second++)
                {
                    var a = projected[edges[first].A]; var ab = projected[edges[first].B] - a;
                    var c = projected[edges[second].A]; var cd = projected[edges[second].B] - c;
                    float denominator = Cross(ab, cd);
                    if (Mathf.Abs(denominator) < 1e-10f) continue;
                    float t = Cross(c - a, cd) / denominator, u = Cross(c - a, ab) / denominator;
                    if (t > 0f && t < 1f && u > 0f && u < 1f) cuts.Add(a.x + ab.x * t);
                }
            cuts.Sort();
            Vertex At((int A, int B) edge, float x)
            {
                float t = Mathf.Clamp01((x - projected[edge.A].x) / (projected[edge.B].x - projected[edge.A].x));
                var point = Vector3.Lerp(positions[edge.A], positions[edge.B], t);
                return new Vertex { Point = point, Normal = normal, Uv = Project(point) * 2f, Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1f) };
            }
            for (int strip = 0; strip + 1 < cuts.Count; strip++)
            {
                float left = cuts[strip], right = cuts[strip + 1], middle = (left + right) * .5f;
                if (right - left < 1e-6f) continue;
                var crossings = edges.Where(e => Mathf.Min(projected[e.A].x, projected[e.B].x) < middle && Mathf.Max(projected[e.A].x, projected[e.B].x) > middle)
                    .OrderBy(e => Mathf.Lerp(projected[e.A].y, projected[e.B].y, (middle - projected[e.A].x) / (projected[e.B].x - projected[e.A].x))).ToArray();
                for (int at = 0; at + 1 < crossings.Length; at += 2)
                {
                    var quad = new List<Vertex> { At(crossings[at], left), At(crossings[at], right), At(crossings[at + 1], right), At(crossings[at + 1], left) };
                    if (Vector3.Dot(Vector3.Cross(quad[1].Point - quad[0].Point, quad[2].Point - quad[0].Point), normal) < 0f) quad.Reverse();
                    result.Add(new Polygon { Slot = cap, Vertices = quad });
                }
            }
            return result;
        }
    }
}
