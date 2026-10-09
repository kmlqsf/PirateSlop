using System;
using System.Collections.Generic;
using System.Linq;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static class CoastalSandSetup
    {
        const string ModelFolder = "Assets/Models/World/CoastalEnvironment";
        const string MaterialPath = "Assets/Materials/CoastalEnvironment/CoastalSand.mat";
        const float Step = .65f;
        const float Floor = -205f;
        static readonly string[] Names = { "Sea_Lagoon_Cave", "Reef_Moai_A", "Reef_Spires_A", "Reef_Spires_B", "Reef_Spires_C", "SeaArch_Huge_A", "Reef_ShallowField_A", "RockLarge", "RockMedium", "CliffWallA", "CliffWallB", "CliffWallC" };

        struct Edge
        {
            public Vector2Int A;
            public Vector2Int B;
        }

        [MenuItem("PirateSlop/Art/Build Coastal Sand Foundations")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before updating coastal foundations.");
            var material = PrepareMaterial();
            foreach (string name in Names)
            {
                string prefabPath = (name.StartsWith("Sea", StringComparison.Ordinal) || name.StartsWith("Reef", StringComparison.Ordinal) ? "Assets/Prefabs/Environment/" : "Assets/Prefabs/World/StarterIsland/") + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var source = root.transform.Find("CoastalCollision").GetComponent<MeshCollider>();
                    var mesh = Build(root.transform, source);
                    string meshPath = ModelFolder + "/" + name + "_Sand.asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (saved == null) AssetDatabase.CreateAsset(mesh, meshPath);
                    else
                    {
                        EditorUtility.CopySerialized(mesh, saved);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        mesh = saved;
                        EditorUtility.SetDirty(mesh);
                        AssetDatabase.SaveAssetIfDirty(mesh);
                    }
                    var previous = root.transform.Find("CoastalSand");
                    if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                    var foundation = new GameObject("CoastalSand") { layer = root.layer };
                    foundation.transform.SetParent(root.transform, false);
                    foundation.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = foundation.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    var collider = foundation.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = false;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            PirateSlop.Editor.CoastalEnvironmentSetup.RefreshCatalogVersions();
        }

        static Material PrepareMaterial()
        {
            const string folder = ModelFolder + "/Textures/Sand/";
            foreach (string file in new[] { "SandColour.jpg", "SandNormal.png", "SandRoughness.jpg" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(folder + file);
                importer.textureType = file == "SandNormal.png" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = file == "SandColour.jpg";
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.mipmapEnabled = importer.streamingMipmaps = true;
                importer.isReadable = file == "SandRoughness.jpg";
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = file == "SandColour.jpg" ? 2048 : 1024;
                importer.SaveAndReimport();
            }
            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "SandRoughness.jpg");
            var packed = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
            string packedPath = folder + "SandMetalSmooth.png";
            try
            {
                packed.SetPixels(roughness.GetPixels().Select(p => new Color(0, 0, 0, (1f - p.r) * .35f)).ToArray());
                packed.Apply();
                System.IO.File.WriteAllBytes(packedPath, packed.EncodeToPNG());
                AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(packedPath);
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = importer.streamingMipmaps = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(packed);
                var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "SandRoughness.jpg");
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "SandColour.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "SandNormal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
            material.SetColor("_BaseColor", new Color(.78f, .75f, .68f));
            material.SetFloat("_BumpScale", .45f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static Mesh Build(Transform anchor, MeshCollider source)
        {
            var matrix = anchor.worldToLocalMatrix * source.transform.localToWorldMatrix;
            var points = source.sharedMesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var indices = source.sharedMesh.triangles;
            var segments = new List<(Vector2 A, Vector2 B)>();
            const float section = .25f;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 first = default, second = default;
                int count = 0;
                for (int edge = 0; edge < 3; edge++)
                {
                    var a = points[indices[i + edge]];
                    var b = points[indices[i + (edge + 1) % 3]];
                    if ((a.y <= section && b.y > section) || (b.y <= section && a.y > section))
                    {
                        var crossing = Vector3.Lerp(a, b, (section - a.y) / (b.y - a.y));
                        if (count++ == 0) first = crossing; else second = crossing;
                    }
                }
                if (count != 2 || (first - second).sqrMagnitude < .000001f) continue;
                var normal = Vector3.Cross(points[indices[i + 1]] - points[indices[i]], points[indices[i + 2]] - points[indices[i]]);
                if (Vector3.Dot(second - first, Vector3.Cross(Vector3.up, normal)) < 0) (first, second) = (second, first);
                segments.Add((new Vector2(first.x, first.z), new Vector2(second.x, second.z)));
            }
            if (segments.Count == 0) throw new InvalidOperationException("No waterline rock supports: " + anchor.name);
            var min = new Vector2(segments.Min(s => Mathf.Min(s.A.x, s.B.x)), segments.Min(s => Mathf.Min(s.A.y, s.B.y))) - Vector2.one * Step;
            var max = new Vector2(segments.Max(s => Mathf.Max(s.A.x, s.B.x)), segments.Max(s => Mathf.Max(s.A.y, s.B.y))) + Vector2.one * Step;
            int width = Mathf.CeilToInt((max.x - min.x) / Step);
            int depth = Mathf.CeilToInt((max.y - min.y) / Step);
            var mask = new bool[width * depth];
            for (int z = 0; z < depth; z++)
            {
                float y = min.y + (z + .5f) * Step;
                var row = new List<(float X, int Sign)>();
                foreach (var s in segments)
                {
                    if (y < Mathf.Min(s.A.y, s.B.y) || y >= Mathf.Max(s.A.y, s.B.y)) continue;
                    row.Add((Mathf.Lerp(s.A.x, s.B.x, (y - s.A.y) / (s.B.y - s.A.y)), s.B.y > s.A.y ? 1 : -1));
                }
                row.Sort((a, b) => a.X.CompareTo(b.X));
                int winding = 0;
                float previous = min.x;
                foreach (var crossing in row)
                {
                    if (winding != 0)
                    {
                        int first = Mathf.Max(0, Mathf.CeilToInt((previous - min.x) / Step - .5f));
                        int last = Mathf.Min(width - 1, Mathf.CeilToInt((crossing.X - min.x) / Step - .5f) - 1);
                        for (int x = first; x <= last; x++) mask[z * width + x] = true;
                    }
                    winding += crossing.Sign;
                    previous = crossing.X;
                }
            }
            bool Filled(int x, int z) => x >= 0 && z >= 0 && x < width && z < depth && mask[z * width + x];
            Vector2 Position(Vector2Int p) => min + new Vector2(p.x, p.y) * Step;
            var edges = new List<Edge>();
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool cap)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                uv.AddRange(new[] { new Vector2(a.x, a.z) * .5f, new Vector2(b.x, b.z) * .5f, new Vector2(c.x, c.z) * .5f, new Vector2(d.x, d.z) * .5f });
                triangles.AddRange(cap ? new[] { start, start + 2, start + 1, start, start + 3, start + 2 } : new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    if (!Filled(x, z)) continue;
                    var a = new Vector2Int(x, z);
                    var b = new Vector2Int(x + 1, z);
                    var c = new Vector2Int(x + 1, z + 1);
                    var d = new Vector2Int(x, z + 1);
                    if (!Filled(x, z - 1)) edges.Add(new Edge { A = a, B = b });
                    if (!Filled(x + 1, z)) edges.Add(new Edge { A = b, B = c });
                    if (!Filled(x, z + 1)) edges.Add(new Edge { A = c, B = d });
                    if (!Filled(x - 1, z)) edges.Add(new Edge { A = d, B = a });
                }
            var covered = new bool[mask.Length];
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    if (!Filled(x, z) || covered[z * width + x]) continue;
                    int endX = x + 1;
                    while (endX < width && Filled(endX, z) && !covered[z * width + endX]) endX++;
                    int endZ = z + 1;
                    while (endZ < depth)
                    {
                        bool available = true;
                        for (int column = x; column < endX; column++)
                            if (!Filled(column, endZ) || covered[endZ * width + column]) { available = false; break; }
                        if (!available) break;
                        endZ++;
                    }
                    for (int row = z; row < endZ; row++)
                        for (int column = x; column < endX; column++) covered[row * width + column] = true;
                    Vector3 At(int column, int row, float height)
                    {
                        var p = Position(new Vector2Int(column, row));
                        return new Vector3(p.x, height, p.y);
                    }
                    Quad(At(x, z, .12f), At(endX, z, .12f), At(endX, endZ, .12f), At(x, endZ, .12f), true);
                    Quad(At(x, z, Floor), At(endX, z, Floor), At(endX, endZ, Floor), At(x, endZ, Floor), false);
                }
            var byStart = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < edges.Count; i++)
            {
                if (!byStart.TryGetValue(edges[i].A, out var list)) byStart[edges[i].A] = list = new List<int>();
                list.Add(i);
            }
            var used = new bool[edges.Count];
            float baseSpread = Mathf.Clamp(Mathf.Max(max.x - min.x, max.y - min.y) * .035f, 1.2f, 5.5f);
            for (int seed = 0; seed < edges.Count; seed++)
            {
                if (used[seed]) continue;
                var loop = new List<Vector2Int>();
                int current = seed;
                while (!used[current])
                {
                    var edge = edges[current];
                    used[current] = true;
                    loop.Add(edge.A);
                    if (edge.B == loop[0]) break;
                    if (!byStart.TryGetValue(edge.B, out var choices)) throw new InvalidOperationException("Open sand support contour.");
                    var direction = edge.B - edge.A;
                    current = choices.Where(i => !used[i]).OrderByDescending(i =>
                    {
                        var next = edges[i].B - edges[i].A;
                        return Mathf.Atan2(direction.x * next.y - direction.y * next.x, Vector2.Dot(direction, next));
                    }).First();
                }
                if (loop.Count < 3) continue;
                loop = loop.Where((point, i) =>
                {
                    var incoming = point - loop[(i + loop.Count - 1) % loop.Count];
                    var outgoing = loop[(i + 1) % loop.Count] - point;
                    return incoming.x * outgoing.y - incoming.y * outgoing.x != 0;
                }).ToList();
                int start = vertices.Count;
                float arc = 0;
                for (int k = 0; k <= loop.Count; k++)
                {
                    int i = k % loop.Count;
                    var position = Position(loop[i]);
                    var previous = Position(loop[(i + loop.Count - 1) % loop.Count]);
                    var next = Position(loop[(i + 1) % loop.Count]);
                    if (k > 0) arc += Vector2.Distance(Position(loop[(k - 1) % loop.Count]), position);
                    var before = (position - previous).normalized;
                    var after = (next - position).normalized;
                    var outward = new Vector2(before.y + after.y, -before.x - after.x).normalized;
                    float spread = baseSpread * Mathf.Lerp(.8f, 1f, Mathf.PerlinNoise(position.x * .11f + 31, position.y * .11f + 17));
                    foreach (var edge in edges)
                    {
                        var a = Position(edge.A);
                        var direction = Position(edge.B) - a;
                        float cross = outward.x * direction.y - outward.y * direction.x;
                        if (Mathf.Abs(cross) < .00001f) continue;
                        var delta = a - position;
                        float t = (delta.x * direction.y - delta.y * direction.x) / cross;
                        float u = (delta.x * outward.y - delta.y * outward.x) / cross;
                        if (t > .03f && u >= 0 && u <= 1) spread = Mathf.Min(spread, t * .28f);
                    }
                    var heights = new[] { .12f, -3f, -12f, -65f, Floor, Floor };
                    var expansion = new[] { 0f, .025f, .1f, .38f, 1f, 0f };
                    for (int ring = 0; ring < heights.Length; ring++)
                    {
                        var p = position + outward * (spread * expansion[ring]);
                        vertices.Add(new Vector3(p.x, heights[ring], p.y));
                        uv.Add(new Vector2(arc * .5f, -heights[ring] * .5f));
                    }
                }
                for (int i = 0; i < loop.Count; i++)
                    for (int ring = 0; ring < 5; ring++)
                    {
                        int a = start + i * 6 + ring;
                        int b = a + 6;
                        triangles.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                    }
            }
            var mesh = new Mesh { name = anchor.name + "_Sand", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
