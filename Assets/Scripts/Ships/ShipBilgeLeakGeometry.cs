using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Ships
{
    public static class ShipBilgeLeakGeometry
    {
        public struct Surface
        {
            public Mesh Mesh;
            public float Seed;
            public ulong Key;
            public Vector3 LowerPoint;
            public bool Strong;
            public Vector3[] Outline;
        }

        public sealed class Aperture
        {
            public Vector3 Center;
            public Vector3[] Triangles;
        }

        sealed class Patch
        {
            public ShipBreach Breach;
            public readonly List<Vector3> Vertices = new();
            public Vector3 Normal;
            public Bounds Bounds;
            public readonly Dictionary<Vector3Int, List<int>> Triangles = new();
            public readonly HashSet<Vector3> Points = new();
        }

        static Patch Read(ShipBreach breach)
        {
            var mesh = breach.SurfaceMesh;
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0) return null;
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            var matrix = breach.SurfaceTransform;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            var toward = new Vector3(-breach.LocalPoint.x, 0f, -breach.LocalPoint.z * .04f).normalized;
            var normal = toward;
            float best = 0f;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var cross = Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]);
                float area = cross.magnitude;
                if (area <= best || Vector3.Dot(cross.normalized, toward) < .35f) continue;
                best = area;
                normal = cross.normalized;
            }
            var patch = new Patch { Breach = breach };
            var sum = Vector3.zero;
            var polygon = new List<Vector3>(6);
            List<Vector3> Clip(List<Vector3> input, float height, bool above)
            {
                var output = new List<Vector3>(6);
                if (input.Count == 0) return output;
                var previous = input[^1];
                bool previousInside = above ? previous.y >= height : previous.y <= height;
                foreach (var point in input)
                {
                    bool inside = above ? point.y >= height : point.y <= height;
                    if (inside != previousInside) output.Add(Vector3.LerpUnclamped(previous, point, (height - previous.y) / (point.y - previous.y)));
                    if (inside) output.Add(point);
                    previous = point; previousInside = inside;
                }
                return output;
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];
                var cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < .00000001f || Vector3.Dot(cross.normalized, normal) < .7f) continue;
                polygon.Clear(); polygon.Add(a); polygon.Add(b); polygon.Add(c);
                if (breach.MaximumHeight > breach.MinimumHeight) polygon = Clip(Clip(polygon, breach.MinimumHeight, true), breach.MaximumHeight, false);
                for (int vertex = 2; vertex < polygon.Count; vertex++)
                {
                    patch.Vertices.Add(polygon[0]); patch.Vertices.Add(polygon[vertex - 1]); patch.Vertices.Add(polygon[vertex]);
                }
                sum += cross;
            }
            if (patch.Vertices.Count == 0) return null;
            patch.Normal = sum.normalized;
            patch.Bounds = new Bounds(patch.Vertices[0], Vector3.zero);
            foreach (var vertex in patch.Vertices) patch.Bounds.Encapsulate(vertex);
            foreach (var vertex in patch.Vertices) patch.Points.Add(vertex);
            for (int i = 0; i < patch.Vertices.Count; i += 3)
            {
                var low = Vector3.Min(patch.Vertices[i], Vector3.Min(patch.Vertices[i + 1], patch.Vertices[i + 2])) - Vector3.one * .10f;
                var high = Vector3.Max(patch.Vertices[i], Vector3.Max(patch.Vertices[i + 1], patch.Vertices[i + 2])) + Vector3.one * .10f;
                var from = Vector3Int.FloorToInt(low * 4f);
                var to = Vector3Int.FloorToInt(high * 4f);
                for (int x = from.x; x <= to.x; x++)
                    for (int y = from.y; y <= to.y; y++)
                        for (int z = from.z; z <= to.z; z++)
                        {
                            var cell = new Vector3Int(x, y, z);
                            if (!patch.Triangles.TryGetValue(cell, out var bucket)) patch.Triangles[cell] = bucket = new List<int>(4);
                            bucket.Add(i);
                        }
            }
            return patch;
        }

        public static List<Surface> Build(IReadOnlyList<ShipBreach> breaches) => new Builder().Build(breaches, null);

        static bool NearFace(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            const float tolerance = .10f;
            var normal = Vector3.Cross(b - a, c - a).normalized;
            float depth = Vector3.Dot(point - a, normal);
            if (Mathf.Abs(depth) <= tolerance)
            {
                var projected = point - normal * depth;
                if (Vector3.Dot(Vector3.Cross(b - a, projected - a), normal) >= -.0001f &&
                    Vector3.Dot(Vector3.Cross(c - b, projected - b), normal) >= -.0001f &&
                    Vector3.Dot(Vector3.Cross(a - c, projected - c), normal) >= -.0001f) return true;
            }
            bool NearEdge(Vector3 start, Vector3 end)
            {
                var edge = end - start;
                var closest = start + edge * Mathf.Clamp01(Vector3.Dot(point - start, edge) / Mathf.Max(.000001f, edge.sqrMagnitude));
                return (point - closest).sqrMagnitude <= tolerance * tolerance;
            }
            return NearEdge(a, b) || NearEdge(b, c) || NearEdge(c, a);
        }

        static bool Connected(Patch a, Patch b)
        {
            var bounds = a.Bounds;
            bounds.Expand(.20f);
            if (Vector3.Dot(a.Normal, b.Normal) < .55f || !bounds.Intersects(b.Bounds)) return false;
            bool Touches(HashSet<Vector3> points, Patch other)
            {
                foreach (var point in points)
                {
                    if (!other.Triangles.TryGetValue(Vector3Int.FloorToInt(point * 4f), out var bucket)) continue;
                    foreach (int i in bucket)
                        if (NearFace(point, other.Vertices[i], other.Vertices[i + 1], other.Vertices[i + 2])) return true;
                }
                return false;
            }
            if (Touches(a.Points, b) || Touches(b.Points, a)) return true;
            return false;
        }

        static List<List<Patch>> Groups(List<Patch> patches)
        {
            var groups = new List<List<Patch>>();
            var used = new bool[patches.Count];
            for (int start = 0; start < patches.Count; start++)
            {
                if (used[start]) continue;
                var group = new List<Patch> { patches[start] };
                used[start] = true;
                for (int at = 0; at < group.Count; at++)
                    for (int i = 0; i < patches.Count; i++)
                        if (!used[i] && Connected(group[at], patches[i])) { used[i] = true; group.Add(patches[i]); }
                groups.Add(group);
            }
            return groups;
        }

        public sealed class Builder
        {
            readonly Dictionary<(int, int, Mesh), Patch> cache = new();

            List<Patch> ReadAll(IEnumerable<ShipBreach> breaches)
            {
                var patches = new List<Patch>();
                foreach (var breach in breaches)
                {
                    var identity = (breach.SectionId, breach.FragmentIndex, breach.SurfaceMesh);
                    if (!cache.TryGetValue(identity, out var patch)) cache[identity] = patch = Read(breach);
                    if (patch != null) patches.Add(patch);
                }
                return patches;
            }

            public bool FullyCovered(ShipBreach breach, Vector4 plane)
            {
                var identity = (breach.SectionId, breach.FragmentIndex, breach.SurfaceMesh);
                if (!cache.TryGetValue(identity, out var patch)) cache[identity] = patch = Read(breach);
                if (patch == null) return false;
                foreach (var point in patch.Vertices) if (ShipFlooding.OceanHead(plane, point) < .005f) return false;
                return true;
            }

            public List<Aperture> Apertures(IEnumerable<ShipBreach> breaches)
            {
                var result = new List<Aperture>();
                foreach (var patch in ReadAll(breaches))
                    result.Add(new Aperture { Center = patch.Bounds.center, Triangles = patch.Vertices.ToArray() });
                return result;
            }

            public List<Surface> Build(IReadOnlyList<ShipBreach> breaches, IReadOnlyList<Surface> previous)
            {
                var surfaces = new List<Surface>();
                foreach (var group in Groups(ReadAll(breaches)))
                {
                    ulong key = 0;
                    foreach (var patch in group)
                    {
                        ulong id = (uint)patch.Breach.SectionId * 64UL + (uint)patch.Breach.FragmentIndex;
                        id ^= (ulong)(uint)patch.Breach.SurfaceMesh.GetEntityId().GetHashCode() << 32;
                        id ^= id >> 30; id *= 0xbf58476d1ce4e5b9UL; id ^= id >> 27; id *= 0x94d049bb133111ebUL;
                        key ^= id ^ (id >> 31);
                    }
                    var pair = new Surface[2];
                    bool cached = true;
                    for (int mode = 0; mode < 2; mode++)
                    {
                        bool strong = mode == 0;
                        ulong variant = key ^ (strong ? 0x9e3779b97f4a7c15UL : 0);
                        Surface surface = default;
                        if (previous != null)
                            foreach (var candidate in previous) if (candidate.Key == variant && candidate.Mesh != null) { surface = candidate; break; }
                        if (surface.Mesh == null) cached = false;
                        surface.Key = variant;
                        pair[mode] = surface;
                    }
                    if (!cached)
                    {
                        var generated = BuildSurfaces(group);
                        for (int mode = 0; mode < 2; mode++) { generated[mode].Key = pair[mode].Key; pair[mode] = generated[mode]; }
                    }
                    surfaces.AddRange(pair);
                }
                return surfaces;
            }
        }

        static Surface[] BuildSurfaces(List<Patch> group)
        {
            var normal = Vector3.zero;
            var center = Vector3.zero;
            int count = 0;
            foreach (var patch in group)
            {
                normal += patch.Normal * patch.Breach.Area;
                foreach (var point in patch.Vertices) { center += point; count++; }
            }
            center /= Mathf.Max(1, count);
            var flowAxis = normal.normalized;
            normal.y = 0f; normal.Normalize();
            var across = Vector3.Cross(Vector3.up, normal).normalized;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var patch in group)
                foreach (var point in patch.Vertices)
                {
                    float x = Vector3.Dot(point, across);
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
                }
            float cell = Mathf.Max(.025f, Mathf.Max(maxX - minX, maxY - minY) / 224f);
            const int padding = 5;
            float originX = minX - cell * padding, originY = minY - cell * padding;
            int width = Mathf.CeilToInt((maxX - minX) / cell) + padding * 2, height = Mathf.CeilToInt((maxY - minY) / cell) + padding * 2;
            var filled = new bool[width * height];
            var depths = new float[filled.Length];
            float centerDepth = Vector3.Dot(center, normal);
            for (int i = 0; i < depths.Length; i++) depths[i] = centerDepth;
            foreach (var patch in group)
                for (int i = 0; i < patch.Vertices.Count; i += 3)
                {
                    var a = patch.Vertices[i]; var b = patch.Vertices[i + 1]; var c = patch.Vertices[i + 2];
                    var pa = new Vector2(Vector3.Dot(a, across), a.y); var pb = new Vector2(Vector3.Dot(b, across), b.y); var pc = new Vector2(Vector3.Dot(c, across), c.y);
                    float determinant = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
                    if (Mathf.Abs(determinant) < .000001f) continue;
                    int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)) - originX) / cell));
                    int x1 = Mathf.Min(width - 1, Mathf.CeilToInt((Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x)) - originX) / cell));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y)) - originY) / cell));
                    int y1 = Mathf.Min(height - 1, Mathf.CeilToInt((Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y)) - originY) / cell));
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            var p = new Vector2(originX + (x + .5f) * cell, originY + (y + .5f) * cell);
                            float u = ((pb.y - pc.y) * (p.x - pc.x) + (pc.x - pb.x) * (p.y - pc.y)) / determinant;
                            float v = ((pc.y - pa.y) * (p.x - pc.x) + (pa.x - pc.x) * (p.y - pc.y)) / determinant;
                            if (u < -.0001f || v < -.0001f || u + v > 1.0001f) continue;
                            int at = y * width + x;
                            filled[at] = true; depths[at] = Vector3.Dot(a * u + b * v + c * (1f - u - v), normal);
                        }
                }
            bool[] Filter(bool[] input, bool horizontal, bool dilate, int radius)
            {
                var output = new bool[input.Length];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        bool value = !dilate;
                        for (int offset = -radius; offset <= radius; offset++)
                        {
                            int px = horizontal ? x + offset : x, py = horizontal ? y : y + offset;
                            bool sample = px >= 0 && px < width && py >= 0 && py < height && input[py * width + px];
                            if (dilate ? sample : !sample) { value = dilate; break; }
                        }
                        output[y * width + x] = value;
                    }
                return output;
            }
            int radius = Mathf.Clamp(Mathf.CeilToInt(.05f / cell), 1, 3);
            var closed = Filter(Filter(Filter(Filter(filled, true, true, radius), false, true, radius), false, false, radius), true, false, radius);
            for (int i = 0; i < filled.Length; i++)
                if (!filled[i] && closed[i]) { filled[i] = true; depths[i] = centerDepth; }
            var points = new List<Vector3>();
            var pointIds = new Dictionary<int, int>();
            int Point(int x, int y)
            {
                int key = y * (width + 1) + x;
                if (pointIds.TryGetValue(key, out int existing)) return existing;
                float depth = 0f; int samples = 0;
                for (int dy = -1; dy <= 0; dy++)
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        int px = x + dx, py = y + dy;
                        if (px < 0 || px >= width || py < 0 || py >= height || !filled[py * width + px]) continue;
                        depth += depths[py * width + px]; samples++;
                    }
                var point = across * (originX + x * cell) + Vector3.up * Mathf.Clamp(originY + y * cell, minY, maxY) + normal * (samples == 0 ? centerDepth : depth / samples);
                int index = points.Count; points.Add(point); pointIds.Add(key, index); return index;
            }
            var edges = new List<(int, int)>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (!filled[y * width + x]) continue;
                    if (x == 0 || !filled[y * width + x - 1]) edges.Add((Point(x, y + 1), Point(x, y)));
                    if (x == width - 1 || !filled[y * width + x + 1]) edges.Add((Point(x + 1, y), Point(x + 1, y + 1)));
                    if (y == 0 || !filled[(y - 1) * width + x]) edges.Add((Point(x, y), Point(x + 1, y)));
                    if (y == height - 1 || !filled[(y + 1) * width + x]) edges.Add((Point(x + 1, y + 1), Point(x, y + 1)));
                }
            var neighbours = new List<int>[points.Count];
            for (int i = 0; i < neighbours.Length; i++) neighbours[i] = new List<int>(2);
            foreach (var edge in edges) { neighbours[edge.Item1].Add(edge.Item2); neighbours[edge.Item2].Add(edge.Item1); }
            var rounded = points.ToArray();
            for (int pass = 0; pass < 10; pass++)
            {
                var next = (Vector3[])rounded.Clone();
                for (int i = 0; i < rounded.Length; i++)
                    if (neighbours[i].Count == 2) next[i] = Vector3.Lerp(rounded[i], (rounded[neighbours[i][0]] + rounded[neighbours[i][1]]) * .5f, .5f);
                rounded = next;
            }
            var arc = new float[points.Count];
            var visited = new HashSet<int>();
            foreach (var edge in edges)
            {
                if (visited.Contains(edge.Item1)) continue;
                int current = edge.Item1, previous = -1;
                float length = 0f;
                while (visited.Add(current))
                {
                    arc[current] = length;
                    int next = -1;
                    foreach (int candidate in neighbours[current])
                        if (candidate != previous) { next = candidate; break; }
                    if (next < 0) break;
                    length += Vector3.Distance(points[current], points[next]);
                    previous = current; current = next;
                }
            }
            Surface EmitSurface(bool strong)
            {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var origins = new List<Vector3>();
            var flow = new List<Vector4>();
            var centers = new List<Vector4>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            float lifetime = strong ? .68f : .85f;
            float thickness = Mathf.Clamp(maxY - minY, .025f, .8f);
            int Emit(int source, float age, int skin)
            {
                var point = points[source];
                var relative = rounded[source] - center;
                var smooth = Vector3.Lerp(point, rounded[source], Mathf.SmoothStep(0f, 1f, age / .12f));
                var planar = relative - normal * Vector3.Dot(relative, normal);
                var p = smooth - planar * (.16f * Mathf.SmoothStep(0f, 1f, age / .12f)) + normal * (.015f + age * (strong ? 3.8f : .7f)) + Vector3.down * (4.905f * age * age);
                if (!strong) p.y += skin * .035f;
                int index = vertices.Count;
                vertices.Add(p); uv.Add(new Vector2(arc[source], age)); origins.Add(smooth);
                int column = Mathf.Clamp(Mathf.FloorToInt((Vector3.Dot(point, across) - originX) / cell), 0, width - 1);
                int row = Mathf.Clamp(Mathf.FloorToInt((point.y - originY) / cell), 0, height - 1);
                while (row < height - 1 && filled[(row + 1) * width + column]) row++;
                float ceiling = originY + (row + 1) * cell;
                flow.Add(new Vector4(flowAxis.x, flowAxis.y, flowAxis.z, strong ? source * .6180339f : ceiling));
                centers.Add(new Vector4(center.x, center.y, center.z, strong ? thickness : skin));
                normals.Add(normal); colors.Add(new Color(.25f + age, strong ? 1f : 0f, 0f, 1f));
                return index;
            }
            var ringVertices = new Dictionary<(int, int, int), int>();
            int Ring(int source, int ring, int skin)
            {
                var key = (source, ring, skin);
                if (!ringVertices.TryGetValue(key, out int at)) ringVertices[key] = at = Emit(source, lifetime * ring / 28f, skin);
                return at;
            }
            var lowerPoint = center; lowerPoint.y = minY;
            var spillEdges = new List<(int, int)>();
            foreach (var edge in edges)
            {
                var a = points[edge.Item1]; var b = points[edge.Item2];
                if (!strong)
                {
                    if (Vector3.Dot(b - a, across) < cell * .5f) continue;
                    float x = Vector3.Dot((a + b) * .5f, across), y = (a.y + b.y) * .5f;
                    if (edges.Exists(e => Vector3.Dot(points[e.Item2] - points[e.Item1], across) > cell * .5f &&
                        Vector3.Dot(points[e.Item1], across) <= x && Vector3.Dot(points[e.Item2], across) >= x && (points[e.Item1].y + points[e.Item2].y) * .5f < y - .01f)) continue;
                    spillEdges.Add(edge);
                }
                for (int skin = 0; skin < (strong ? 1 : 2); skin++)
                {
                    int previousA = Ring(edge.Item1, 0, skin), previousB = Ring(edge.Item2, 0, skin);
                    for (int ring = 1; ring <= 28; ring++)
                    {
                        int nextA = Ring(edge.Item1, ring, skin), nextB = Ring(edge.Item2, ring, skin);
                        indices.Add(previousA); indices.Add(nextA); indices.Add(previousB); indices.Add(previousB); indices.Add(nextA); indices.Add(nextB);
                        previousA = nextA; previousB = nextB;
                    }
                }
            }
            var random = new System.Random(Mathf.RoundToInt(center.x * 173f + center.z * 337f));
            void Sprite(Vector3 source, int mode, float size, float phase)
            {
                int first = vertices.Count;
                foreach (var corner in new[] { new Vector2(-1,-1), new Vector2(-1,1), new Vector2(1,-1), new Vector2(1,1) })
                {
                    vertices.Add(source); uv.Add(corner); origins.Add(source); normals.Add(normal);
                    flow.Add(new Vector4(flowAxis.x,flowAxis.y,flowAxis.z,phase));
                    centers.Add(new Vector4(center.x,center.y,center.z,size));
                    colors.Add(new Color(.9f,strong?1f:0f,mode,phase));
                }
                indices.Add(first);indices.Add(first+1);indices.Add(first+2);indices.Add(first+2);indices.Add(first+1);indices.Add(first+3);
            }
            var occupied = new List<int>();
            for (int i = 0; i < filled.Length; i++) if (filled[i]) occupied.Add(i);
            int droplets = strong ? 640 : 128;
            for (int i = 0; i < droplets; i++)
            {
                int at = occupied[random.Next(occupied.Count)], x = at % width, y = at / width;
                var source = across * (originX + (x + .5f) * cell) + Vector3.up * (strong ? originY + (y + .5f) * cell : minY) + normal * depths[at];
                if (!strong && spillEdges.Count > 0)
                {
                    var edge = spillEdges[random.Next(spillEdges.Count)];
                    source = Vector3.Lerp(points[edge.Item1], points[edge.Item2], (float)random.NextDouble());
                }
                float phase = (float)random.NextDouble();
                Sprite(source, 1, Mathf.Lerp(.003f, .014f, phase * phase), phase);
                if (i % 10 == 0) Sprite(source, 2, Mathf.Lerp(.18f, .55f, phase), phase);
                if (i % 3 == 0) Sprite(source, 3, Mathf.Lerp(.003f, .012f, phase), phase);
                if (i % 5 == 0) Sprite(source, 4, Mathf.Lerp(.006f, .022f, phase), phase);
                if (i % 8 == 0) Sprite(source, 5, Mathf.Lerp(.065f, .16f, phase), phase);
            }
            var mesh = new Mesh { name = "BilgeBreachFlow", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, flow); mesh.SetUVs(2, origins); mesh.SetUVs(3, centers); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(12f); mesh.bounds = bounds;
            return new Surface { Mesh = mesh, Seed = center.z * .137f, LowerPoint = lowerPoint, Strong = strong, Outline = points.ToArray() };
            }
            return new[] { EmitSurface(true), EmitSurface(false) };
        }
    }
}
