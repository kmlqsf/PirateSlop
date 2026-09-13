using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipConnectedFractureSetup
    {
        const string ShipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
        const string Folder = "Assets/Models/Ships/MainShip/Destruction/ConnectedFragments";
        const float Contact = .22f;
        struct Vertex
        {
            public Vector3 Position, Normal;
            public Vector2 UV;
            public Vector4 Tangent;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
            {
                Position = Vector3.LerpUnclamped(a.Position, b.Position, t),
                Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized,
                UV = Vector2.LerpUnclamped(a.UV, b.UV, t),
                Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t)
            };
        }
        sealed class Surface
        {
            public readonly List<Vertex> Vertices = new();
            public readonly List<int> Materials = new();
            public void Triangle(Vertex a, Vertex b, Vertex c, int material)
            {
                if (Vector3.Cross(b.Position - a.Position, c.Position - a.Position).sqrMagnitude < 1e-12f) return;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Materials.Add(material);
            }
            public Mesh Mesh(int materials)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(Vertices.Select(v => v.Position).ToList());
                mesh.SetNormals(Vertices.Select(v => v.Normal).ToList());
                mesh.SetUVs(0, Vertices.Select(v => v.UV).ToList());
                mesh.SetTangents(Vertices.Select(v => v.Tangent).ToList());
                mesh.subMeshCount = materials;
                for (int s = 0; s < materials; s++)
                {
                    var indices = new List<int>();
                    for (int i = 0; i < Materials.Count; i++)
                        if (Materials[i] == s) { indices.Add(i * 3); indices.Add(i * 3 + 1); indices.Add(i * 3 + 2); }
                    mesh.SetTriangles(indices, s);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }
        sealed class Node
        {
            public ShipFragmentConnection Connection;
            public Surface Surface;
            public Bounds Bounds;
            public string Source;
            public readonly HashSet<int> Links = new();
        }
        struct Sample { public Vector3 Point; public int Node; }

        [MenuItem("PirateSlop/Prepare Connected Ship Destruction")]
        public static string Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                var destruction = root.GetComponent<ShipDestruction>();
                var old = root.transform.Find("ShipDestructionSections");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var container = Child(root.transform, "ShipDestructionSections");
                var sections = new List<ShipDamageSection>();
                var definitions = new List<ShipSectionDefinition>();
                var nodes = new List<Node>();
                int id = 1000;
                var sources = root.transform.Find("MainShipVisual").GetComponentsInChildren<MeshFilter>(true).OrderBy(f => f.name).ToArray();
                AssetDatabase.StartAssetEditing();
                try
                {
                foreach (var source in sources)
                {
                    var renderer = source.GetComponent<MeshRenderer>();
                    if (renderer == null || source.sharedMesh == null) continue;
                    source.gameObject.SetActive(true);
                    var materials = renderer.sharedMaterials;
                    bool animated = source.name.StartsWith("F2_Sail") || source.name == "F2_Wheel";
                    bool bearing = !(source.name.Contains("Sail") || source.name.Contains("Flag") || source.name.Contains("Wire") || source.name.Contains("Ropes") || source.name.Contains("Anchor"));
                    var type = Type(source.name);
                    var surface = Read(source, root.transform);
                    var prepared = Prepared(source.name, root.transform);
                    var pieces = prepared ?? (animated ? new List<Surface> { surface } : Cut(surface, type));
                    if (prepared != null) materials = new[]{materials[0], destruction.Profile.SplinterMaterial};
                    float area = Area(surface);
                    if (prepared == null && Mathf.Abs(pieces.Sum(Area) - area) > Mathf.Max(.001f, area * .0001f)) throw new InvalidOperationException("Surface coverage changed: " + source.name);
                    foreach (var collider in source.GetComponents<Collider>()) collider.enabled = false;
                    renderer.enabled = animated;
                    for (int offset = 0; offset < pieces.Count; offset += 64)
                    {
                        var batch = pieces.Skip(offset).Take(64).ToArray();
                        var section = Child(container, source.name + "_" + offset / 64).gameObject.AddComponent<ShipDamageSection>();
                        section.SectionId = id++;
                        section.SafeColliderReplacement = true;
                        var definition = new ShipSectionDefinition
                        {
                            SectionId = section.SectionId, Name = section.name, SourceGroup = source.name, Type = type,
                            MaxHealth = type == ShipSectionType.Hull ? 240f : 150f,
                            MaxDebris = 64, DebrisMass = type == ShipSectionType.Mast ? 240f : 80f,
                            SailNames = source.name.StartsWith("F2_Sail") ? new[] { source.name } : Array.Empty<string>(),
                            Ammo = new[] { new ShipAmmoMultiplier { Ammo = InventoryItem.BoardingHook, Multiplier = 0f }, new ShipAmmoMultiplier { Ammo = InventoryItem.IceCannonball, Multiplier = .6f }, new ShipAmmoMultiplier { Ammo = InventoryItem.PushCannonball, Multiplier = .5f } }
                        };
                        definitions.Add(definition);
                        var intactSurface = new Surface();
                        foreach (var piece in batch) { intactSurface.Vertices.AddRange(piece.Vertices); intactSurface.Materials.AddRange(piece.Materials); }
                        var intactMesh = Store(intactSurface.Mesh(materials.Length), section.SectionId + "_Intact");
                        if (animated)
                        {
                            section.Intact = source.gameObject;
                            var collider = source.GetComponent<MeshCollider>();
                            if (collider == null) collider = source.gameObject.AddComponent<MeshCollider>();
                            collider.sharedMesh = source.sharedMesh; collider.enabled = true;
                            section.GameplayColliders = new Collider[] { collider };
                        }
                        else
                        {
                            section.Intact = Visual(section.transform, "Intact", intactMesh, materials);
                            section.GameplayColliders = new Collider[] { section.Intact.AddComponent<MeshCollider>() };
                            ((MeshCollider)section.GameplayColliders[0]).sharedMesh = intactMesh;
                        }
                        section.Fragments = new GameObject[batch.Length];
                        var damage = section.GameplayColliders.ToList();
                        for (int i = 0; i < batch.Length; i++)
                        {
                            var mesh = Store(batch[i].Mesh(materials.Length), section.SectionId + "_" + i);
                            var fragment = Visual(section.transform, "Fragment" + i, mesh, materials);
                            var collider = fragment.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                            damage.Add(collider); fragment.SetActive(false); section.Fragments[i] = fragment;
                            nodes.Add(new Node { Connection = new ShipFragmentConnection { SectionId = section.SectionId, Fragment = i, LoadBearing = bearing }, Surface = batch[i], Bounds = mesh.bounds, Source = source.name });
                        }
                        section.DamageColliders = damage.ToArray();
                        sections.Add(section);
                    }
                }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                foreach (var collider in root.transform.Find("MainShipCollision").GetComponentsInChildren<Collider>(true))
                {
                    if (collider.name.Contains("Stairs")) collider.enabled = false;
                    else if (collider.name.EndsWith("Control"))
                    {
                        string sourceName = collider.name.Substring(0, collider.name.Length - "Control".Length);
                        var nearest = nodes.Where(n => n.Source == sourceName).OrderBy(n => (n.Bounds.center - root.transform.InverseTransformPoint(collider.transform.position)).sqrMagnitude).First();
                        var section = sections.First(s => s.SectionId == nearest.Connection.SectionId);
                        section.DisabledControls = section.DisabledControls.Append(collider).ToArray();
                        section.ControlFragments = section.ControlFragments.Append(nearest.Connection.Fragment).ToArray();
                        section.DamageColliders = section.DamageColliders.Append(collider).ToArray();
                    }
                }
                string structure = Connect(nodes);
                destruction.Sections = sections.ToArray();
                destruction.Profile.Sections = definitions.ToArray();
                destruction.Profile.Structure = nodes.Select(n => n.Connection).ToArray();
                destruction.Profile.FallbackSectionId = definitions.First(d => d.Type == ShipSectionType.Hull).SectionId;
                destruction.Profile.SectionsPrefab = SaveSections(container);
                EditorUtility.SetDirty(destruction.Profile);
                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
                var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
                config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 73); EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                return sections.Count + " sections, " + nodes.Count + " fragments. " + structure;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static ShipSectionType Type(string name)
        {
            if (name == "F2_Body") return ShipSectionType.Hull;
            if (name.Contains("Floor") || name.Contains("Hatch") || name.Contains("ClosedHold")) return ShipSectionType.Deck;
            if (name.Contains("Mast")) return ShipSectionType.Mast;
            if (name.Contains("Stairs")) return ShipSectionType.Stairs;
            if (name.Contains("Wheel")) return ShipSectionType.Helm;
            if (name.Contains("Fencing")) return ShipSectionType.Railing;
            if (name.Contains("Rudder")) return ShipSectionType.Rudder;
            if (name.Contains("Prow")) return ShipSectionType.Bowsprit;
            return ShipSectionType.Fitting;
        }
        static List<Surface> Prepared(string source, Transform root)
        {
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Models/Ships/MainShip/Destruction/sections.json"));
            var records = manifest["sections"].Where(r => (string)r["source"] == source).ToArray();
            if (source != "F2_Body" && !source.StartsWith("F2_Floor") && !source.StartsWith("F2_Fencing") && source != "F2_Prow" && source != "F2_Rudder" && source != "F2_MastFront" && source != "F2_MastBack") return null;
            var result = new List<Surface>();
            var staging = new GameObject("PreparedSurface");
            staging.transform.SetParent(root, false);
            var filter = staging.AddComponent<MeshFilter>();
            try
            {
                foreach (var record in records)
                {
                    int id = (int)record["id"];
                    string folder = source.StartsWith("F2_Floor") ? "DeckFragments" : "WoodFragments";
                    var meshes = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Ships/MainShip/Destruction/" + folder + "/SD" + id + ".asset").OfType<Mesh>().Where(m => m.name.StartsWith("Fragment") || folder == "DeckFragments" && m.name == "SD" + id).OrderBy(m => m.name.StartsWith("SD") ? "Fragment00" : m.name, StringComparer.Ordinal).ToArray();
                    if (meshes.Length == 0) throw new InvalidOperationException("Missing prepared fragments: " + id);
                    var center = record["center"];
                    staging.transform.localPosition = new Vector3((float)center[0], (float)center[1], (float)center[2]);
                    foreach (var mesh in meshes) { filter.sharedMesh = mesh; result.Add(Read(filter, root)); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(staging); }
            return result;
        }
        static GameObject SaveSections(Transform container)
        {
            var copy = UnityEngine.Object.Instantiate(container.gameObject);
            copy.name = container.name;
            try
            {
                foreach (var section in copy.GetComponentsInChildren<ShipDamageSection>(true))
                {
                    section.DisabledControls = Array.Empty<Collider>();
                    section.ControlFragments = Array.Empty<int>();
                    section.DamageColliders = section.DamageColliders.Where(c => c != null && c.transform.IsChildOf(copy.transform)).ToArray();
                    if (section.Intact.transform.IsChildOf(copy.transform)) continue;
                    var fragment = section.Fragments[0];
                    var mesh = fragment.GetComponent<MeshFilter>().sharedMesh;
                    section.Intact = Visual(section.transform, "Intact", mesh, fragment.GetComponent<MeshRenderer>().sharedMaterials);
                    var collider = section.Intact.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                    section.GameplayColliders = new Collider[] { collider };
                    section.DamageColliders = section.DamageColliders.Append(collider).ToArray();
                }
                return PrefabUtility.SaveAsPrefabAsset(copy, "Assets/Prefabs/ShipDestruction/MainShipSections.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        static float Area(Surface surface)
        {
            float area = 0f;
            for (int i = 0; i < surface.Vertices.Count; i += 3) area += Vector3.Cross(surface.Vertices[i + 1].Position - surface.Vertices[i].Position, surface.Vertices[i + 2].Position - surface.Vertices[i].Position).magnitude * .5f;
            return area;
        }
        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer; return go.transform;
        }
        static GameObject Visual(Transform parent, string name, Mesh mesh, Material[] materials)
        {
            var go = Child(parent, name).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }
        static Mesh Store(Mesh mesh, string name)
        {
            return ShipFragmentPacking.Store(mesh, Folder + "/SD" + name.Split('_')[0] + ".asset", name);
        }
        static Surface Read(MeshFilter filter, Transform root)
        {
            var mesh = filter.sharedMesh;
            var positions = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var tangents = mesh.tangents;
            var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var normalMatrix = matrix.inverse.transpose;
            var vertices = new Vertex[positions.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 tangent = tangents.Length == vertices.Length ? matrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized : Vector3.right;
                vertices[i] = new Vertex { Position = matrix.MultiplyPoint3x4(positions[i]), Normal = normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.up, UV = uv.Length == vertices.Length ? uv[i] : Vector2.zero, Tangent = new Vector4(tangent.x, tangent.y, tangent.z, tangents.Length == vertices.Length ? tangents[i].w * Mathf.Sign(matrix.determinant) : 1f) };
            }
            var result = new Surface();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var triangles = mesh.GetTriangles(s);
                for (int i = 0; i < triangles.Length; i += 3)
                    result.Triangle(vertices[triangles[i]], vertices[triangles[i + (matrix.determinant < 0 ? 2 : 1)]], vertices[triangles[i + (matrix.determinant < 0 ? 1 : 2)]], s);
            }
            return result;
        }
        static List<Surface> Cut(Surface input, ShipSectionType type)
        {
            Vector3 size = type == ShipSectionType.Mast ? new Vector3(2f, 1.2f, 2f) : type == ShipSectionType.Stairs ? new Vector3(1.5f, 3f, .65f) : new Vector3(2f, 1.5f, 2f);
            if (type == ShipSectionType.Deck) size.y = 20f;
            var cells = new SortedDictionary<(int, int, int), Surface>();
            for (int t = 0; t < input.Materials.Count; t++)
            {
                var a = input.Vertices[t * 3]; var b = input.Vertices[t * 3 + 1]; var c = input.Vertices[t * 3 + 2];
                Vector3 min = Vector3.Min(a.Position, Vector3.Min(b.Position, c.Position));
                Vector3 max = Vector3.Max(a.Position, Vector3.Max(b.Position, c.Position));
                var low = Cell(min, size); var high = Cell(max - Vector3.one * .00001f, size);
                high = Vector3Int.Max(low, high);
                for (int x = low.x; x <= high.x; x++) for (int y = low.y; y <= high.y; y++) for (int z = low.z; z <= high.z; z++)
                {
                    var polygon = new List<Vertex> { a, b, c };
                    Vector3 start = Vector3.Scale(new Vector3(x, y, z), size);
                    for (int axis = 0; axis < 3 && polygon.Count >= 3; axis++)
                    {
                        polygon = Clip(polygon, axis, start[axis], true);
                        polygon = Clip(polygon, axis, start[axis] + size[axis], false);
                    }
                    if (polygon.Count < 3) continue;
                    if (!cells.TryGetValue((x, y, z), out var surface)) cells[(x, y, z)] = surface = new Surface();
                    for (int i = 1; i + 1 < polygon.Count; i++) surface.Triangle(polygon[0], polygon[i], polygon[i + 1], input.Materials[t]);
                }
            }
            return cells.Values.Where(s => s.Materials.Count > 0).SelectMany(Islands).ToList();
        }
        static Vector3Int Cell(Vector3 p, Vector3 size) => new(Mathf.FloorToInt(p.x / size.x), Mathf.FloorToInt(p.y / size.y), Mathf.FloorToInt(p.z / size.z));
        static List<Vertex> Clip(List<Vertex> input, int axis, float plane, bool greater)
        {
            var output = new List<Vertex>();
            if (input.Count == 0) return output;
            var previous = input[input.Count - 1];
            float before = (previous.Position[axis] - plane) * (greater ? 1f : -1f);
            foreach (var current in input)
            {
                float after = (current.Position[axis] - plane) * (greater ? 1f : -1f);
                if ((before >= 0f) != (after >= 0f)) output.Add(Vertex.Lerp(previous, current, before / (before - after)));
                if (after >= 0f) output.Add(current);
                previous = current; before = after;
            }
            return output;
        }
        static IEnumerable<Surface> Islands(Surface input)
        {
            var parent = Enumerable.Range(0, input.Materials.Count).ToArray();
            int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            var positions = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < input.Vertices.Count; i++)
            {
                var key = Vector3Int.RoundToInt(input.Vertices[i].Position * 1000f);
                if (positions.TryGetValue(key, out int other)) parent[Root(i / 3)] = Root(other);
                else positions[key] = i / 3;
            }
            var groups = new SortedDictionary<int, Surface>();
            for (int i = 0; i < parent.Length; i++)
            {
                int key = Root(i);
                if (!groups.TryGetValue(key, out var surface)) groups[key] = surface = new Surface();
                surface.Triangle(input.Vertices[i * 3], input.Vertices[i * 3 + 1], input.Vertices[i * 3 + 2], input.Materials[i]);
            }
            return groups.Values;
        }
        static string Connect(List<Node> nodes)
        {
            var samples = new Dictionary<Vector3Int, List<Sample>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var unique = new HashSet<Vector3Int>();
                foreach (var point in Samples(node.Surface))
                {
                    if (!unique.Add(Vector3Int.RoundToInt(point * 50f))) continue;
                    var cell = Cell(point, Vector3.one * Contact);
                    for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    {
                        if (!samples.TryGetValue(cell + new Vector3Int(x, y, z), out var neighbours)) continue;
                        foreach (var sample in neighbours)
                        {
                            if (sample.Node == i || node.Links.Contains(sample.Node)) continue;
                            if ((point - sample.Point).sqrMagnitude > Contact * Contact) continue;
                            node.Links.Add(sample.Node); nodes[sample.Node].Links.Add(i);
                        }
                    }
                    if (!samples.TryGetValue(cell, out var bucket)) samples[cell] = bucket = new List<Sample>();
                    bucket.Add(new Sample { Point = point, Node = i });
                }
            }
            var keel = nodes.Where(n => n.Source == "F2_Body").OrderBy(n => n.Bounds.center.y + .05f * n.Bounds.center.sqrMagnitude).First();
            keel.Connection.Anchor = true;
            var reached = new HashSet<int>();
            void Flood(int start)
            {
                var queue = new Queue<int>(); queue.Enqueue(start); reached.Add(start);
                while (queue.Count > 0)
                {
                    int next = queue.Dequeue();
                    foreach (int neighbour in nodes[next].Links)
                        if ((nodes[next].Connection.LoadBearing || !nodes[neighbour].Connection.LoadBearing) && reached.Add(neighbour)) queue.Enqueue(neighbour);
                }
            }
            Flood(nodes.IndexOf(keel));
            int fasteners = 0;
            float longest = 0f;
            while (reached.Count < nodes.Count)
            {
                int from = -1, to = -1; float best = float.MaxValue;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (reached.Contains(i)) continue;
                    for (int j = 0; j < nodes.Count; j++)
                    {
                        if (!reached.Contains(j) || nodes[i].Connection.LoadBearing && !nodes[j].Connection.LoadBearing) continue;
                        Vector3 gap = Vector3.Max(Vector3.zero, Vector3.Max(nodes[i].Bounds.min - nodes[j].Bounds.max, nodes[j].Bounds.min - nodes[i].Bounds.max));
                        float lower = gap.sqrMagnitude;
                        if (lower > best) continue;
                        float distance = Distance(nodes[i].Surface, nodes[j].Surface, best);
                        if (distance < best) { best = distance; from = i; to = j; }
                    }
                }
                if (from < 0 || best > 2.25f) throw new InvalidOperationException("Unattached model part: " + (from < 0 ? "unknown" : nodes[from].Source) + ", gap " + Mathf.Sqrt(best));
                nodes[from].Links.Add(to); nodes[to].Links.Add(from);
                longest = Mathf.Max(longest, Mathf.Sqrt(best)); fasteners++; Flood(from);
            }
            for (int i = 0; i < nodes.Count; i++) nodes[i].Connection.Neighbours = nodes[i].Links.OrderBy(n => n).ToArray();
            return fasteners + " model fasteners, maximum gap " + longest.ToString("F3") + " m";
        }
        static IEnumerable<Vector3> Samples(Surface surface)
        {
            for (int t = 0; t < surface.Vertices.Count; t += 3)
            {
                Vector3 a = surface.Vertices[t].Position, b = surface.Vertices[t + 1].Position, c = surface.Vertices[t + 2].Position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max((b - a).magnitude, Mathf.Max((c - a).magnitude, (c - b).magnitude)) / .3f));
                for (int x = 0; x <= steps; x++) for (int y = 0; y <= steps - x; y++) yield return a + (b - a) * (x / (float)steps) + (c - a) * (y / (float)steps);
            }
        }
        static float Distance(Surface a, Surface b, float best)
        {
            foreach (var vertex in a.Vertices)
                for (int i = 0; i < b.Vertices.Count; i += 3)
                    best = Mathf.Min(best, PointTriangle(vertex.Position, b.Vertices[i].Position, b.Vertices[i + 1].Position, b.Vertices[i + 2].Position));
            return best;
        }
        static float PointTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, n = Vector3.Cross(ab, ac);
            float length = n.sqrMagnitude;
            if (length > 1e-12f)
            {
                Vector3 q = p - n * (Vector3.Dot(p - a, n) / length);
                if (Vector3.Dot(Vector3.Cross(ab, q - a), n) >= 0f && Vector3.Dot(Vector3.Cross(c - b, q - b), n) >= 0f && Vector3.Dot(Vector3.Cross(a - c, q - c), n) >= 0f) return (p - q).sqrMagnitude;
            }
            float Edge(Vector3 x, Vector3 y) { var d = y - x; return (p - x - d * Mathf.Clamp01(Vector3.Dot(p - x, d) / Mathf.Max(d.sqrMagnitude, 1e-12f))).sqrMagnitude; }
            return Mathf.Min(Edge(a, b), Mathf.Min(Edge(b, c), Edge(c, a)));
        }
    }
}
