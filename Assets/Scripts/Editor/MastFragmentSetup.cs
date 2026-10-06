using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static class MastFragmentSetup
    {
        struct Vertex
        {
            public Vector3 Point, Normal;
            public Vector2 Uv;
            public Vector4 Tangent;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new()
            {
                Point = Vector3.Lerp(a.Point, b.Point, t), Normal = Vector3.Lerp(a.Normal, b.Normal, t),
                Uv = Vector2.Lerp(a.Uv, b.Uv, t), Tangent = Vector4.Lerp(a.Tangent, b.Tangent, t)
            };
        }
        sealed class Polygon
        {
            public int Material;
            public List<Vertex> Vertices;
        }
        sealed class Replacement
        {
            public readonly Dictionary<int, List<int>> OldFragments = new();
        }

        public static string Configure()
        {
            const string prefabPath = "Assets/Resources/Ships/ShipV3Test.prefab";
            const string folder = "Assets/Models/ShipV3/MastChips";
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            if (!AssetDatabase.IsValidFolder("Assets/Models/ShipV3")) AssetDatabase.CreateFolder("Assets/Models", "ShipV3");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Models/ShipV3", "MastChips");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var owner = root.GetComponent<ShipDestruction>();
                var replacements = new Dictionary<int, Replacement>();
                int changed = 0, fragments = 0;
                foreach (var section in owner.Sections)
                {
                    var definition = Array.Find(owner.Profile.Sections, x => x.SectionId == section.SectionId);
                    if (definition.Type != ShipSectionType.Mast || !definition.SourceGroup.StartsWith("V3_Mast_") || section.Fragments.Length != 3) continue;
                    string path = folder + "/" + definition.SourceGroup + ".asset";
                    var container = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    var oldParts = section.Fragments;
                    var parts = new List<GameObject>();
                    var replacement = new Replacement();
                    for (int old = 0; old < oldParts.Length; old++)
                    {
                        var source = oldParts[old];
                        var filter = source.GetComponent<MeshFilter>();
                        var matrix = root.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                        var worldBounds = TransformBounds(filter.sharedMesh.bounds, matrix);
                        int bands = Mathf.CeilToInt(worldBounds.size.y / 1.1f);
                        replacement.OldFragments[old] = new List<int>();
                        for (int band = 0; band < bands; band++)
                            for (int side = 0; side < 2; side++)
                            {
                                var polygons = Read(filter.sharedMesh);
                                float low = Mathf.Lerp(worldBounds.min.y, worldBounds.max.y, band / (float)bands);
                                float high = Mathf.Lerp(worldBounds.min.y, worldBounds.max.y, (band + 1f) / bands);
                                polygons = Clip(polygons, matrix, Vector3.up, low, filter.sharedMesh.subMeshCount);
                                polygons = Clip(polygons, matrix, Vector3.down, -high, filter.sharedMesh.subMeshCount);
                                polygons = Clip(polygons, matrix, side == 0 ? Vector3.right : Vector3.left, side == 0 ? worldBounds.center.x : -worldBounds.center.x, filter.sharedMesh.subMeshCount);
                                string name = "MastChip_" + section.SectionId + "_" + parts.Count;
                                var mesh = Build(polygons, filter.sharedMesh.subMeshCount + 1, name);
                                if (mesh.vertexCount == 0) throw new InvalidOperationException("Empty mast chip " + name);
                                var saved = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(x => x.name == name);
                                if (saved != null) { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); mesh = saved; }
                                else if (container == null) { AssetDatabase.CreateAsset(mesh, path); container = mesh; }
                                else AssetDatabase.AddObjectToAsset(mesh, container);
                                var part = new GameObject(name);
                                part.transform.SetParent(source.transform.parent, false);
                                part.transform.localPosition = source.transform.localPosition;
                                part.transform.localRotation = source.transform.localRotation;
                                part.transform.localScale = source.transform.localScale;
                                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                                var renderer = part.AddComponent<MeshRenderer>();
                                var materials = source.GetComponent<MeshRenderer>().sharedMaterials.ToList();
                                while (materials.Count < filter.sharedMesh.subMeshCount) materials.Add(materials[0]);
                                materials.Add(owner.Profile.SplinterMaterial);
                                renderer.sharedMaterials = materials.ToArray();
                                part.SetActive(false);
                                replacement.OldFragments[old].Add(parts.Count);
                                parts.Add(part);
                            }
                    }
                    if (parts.Count > 64) throw new InvalidOperationException("Mast fragment mask overflow");
                    section.Fragments = parts.ToArray();
                    foreach (var old in oldParts) UnityEngine.Object.DestroyImmediate(old);
                    replacements[section.SectionId] = replacement;
                    fragments += parts.Count; changed++;
                    EditorUtility.SetDirty(container);
                    AssetDatabase.SaveAssetIfDirty(container);
                }
                if (changed == 0) return "Mast chips already configured";
                var oldGraph = owner.Profile.Structure;
                var graph = new List<ShipFragmentConnection>();
                var indices = new List<int>[oldGraph.Length];
                for (int i = 0; i < oldGraph.Length; i++)
                {
                    var node = oldGraph[i];
                    indices[i] = new List<int>();
                    var newFragments = replacements.TryGetValue(node.SectionId, out var replacement) ? replacement.OldFragments[node.Fragment] : new List<int> { node.Fragment };
                    foreach (int fragment in newFragments)
                    {
                        indices[i].Add(graph.Count);
                        graph.Add(new ShipFragmentConnection { SectionId = node.SectionId, Fragment = fragment, Anchor = node.Anchor, LoadBearing = node.LoadBearing });
                    }
                }
                for (int i = 0; i < oldGraph.Length; i++)
                    foreach (int index in indices[i])
                    {
                        var neighbours = new HashSet<int>(indices[i]);
                        neighbours.Remove(index);
                        foreach (int oldNeighbour in oldGraph[i].Neighbours)
                            foreach (int next in indices[oldNeighbour]) neighbours.Add(next);
                        graph[index].Neighbours = neighbours.OrderBy(x => x).ToArray();
                    }
                owner.Profile.Structure = graph.ToArray();
                EditorUtility.SetDirty(owner.Profile);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssetIfDirty(owner.Profile);
                return changed + " masts, " + fragments + " short half-section chips";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        static List<Polygon> Read(Mesh mesh)
        {
            var result = new List<Polygon>();
            var points = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var tangents = mesh.tangents;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var vertices = new List<Vertex>();
                    for (int j = 0; j < 3; j++)
                    {
                        int index = triangles[i + j];
                        vertices.Add(new Vertex { Point = points[index], Normal = normals[index], Uv = uv[index], Tangent = tangents.Length == points.Length ? tangents[index] : new Vector4(1, 0, 0, 1) });
                    }
                    result.Add(new Polygon { Material = sub, Vertices = vertices });
                }
            }
            return result;
        }

        static List<Polygon> Clip(List<Polygon> polygons, Matrix4x4 toShip, Vector3 axis, float threshold, int capMaterial)
        {
            var result = new List<Polygon>();
            var edge = new List<Vector3>();
            foreach (var polygon in polygons)
            {
                var vertices = new List<Vertex>();
                for (int i = 0; i < polygon.Vertices.Count; i++)
                {
                    var a = polygon.Vertices[i]; var b = polygon.Vertices[(i + 1) % polygon.Vertices.Count];
                    float da = Vector3.Dot(axis, toShip.MultiplyPoint3x4(a.Point)) - threshold;
                    float db = Vector3.Dot(axis, toShip.MultiplyPoint3x4(b.Point)) - threshold;
                    bool insideA = da >= -1e-5f, insideB = db >= -1e-5f;
                    if (insideA) vertices.Add(a);
                    if (insideA == insideB) continue;
                    var crossing = Vertex.Lerp(a, b, da / (da - db));
                    vertices.Add(crossing);
                    if (!edge.Any(p => (p - crossing.Point).sqrMagnitude < 1e-12f)) edge.Add(crossing.Point);
                }
                if (vertices.Count >= 3) result.Add(new Polygon { Material = polygon.Material, Vertices = vertices });
            }
            if (edge.Count < 3) return result;
            Vector3 center = edge.Aggregate(Vector3.zero, (a, b) => a + b) / edge.Count;
            Vector3 normal = -toShip.transpose.MultiplyVector(axis).normalized;
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < .8f ? Vector3.up : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            edge.Sort((a, b) => Mathf.Atan2(Vector3.Dot(a - center, bitangent), Vector3.Dot(a - center, tangent)).CompareTo(Mathf.Atan2(Vector3.Dot(b - center, bitangent), Vector3.Dot(b - center, tangent))));
            var cap = new List<Vertex>();
            foreach (var point in edge) cap.Add(new Vertex { Point = point, Normal = normal, Uv = new Vector2(Vector3.Dot(point, tangent), Vector3.Dot(point, bitangent)) * 100f, Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1) });
            result.Add(new Polygon { Material = capMaterial, Vertices = cap });
            return result;
        }

        static Mesh Build(List<Polygon> polygons, int materials, string name)
        {
            var points = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var tangents = new List<Vector4>();
            var indices = Enumerable.Range(0, materials).Select(_ => new List<int>()).ToArray();
            foreach (var polygon in polygons)
            {
                int start = points.Count;
                foreach (var vertex in polygon.Vertices) { points.Add(vertex.Point); normals.Add(vertex.Normal.normalized); uv.Add(vertex.Uv); tangents.Add(vertex.Tangent); }
                for (int i = 1; i + 1 < polygon.Vertices.Count; i++) indices[polygon.Material].AddRange(new[] { start, start + i, start + i + 1 });
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(points); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTangents(tangents);
            mesh.subMeshCount = materials;
            for (int i = 0; i < materials; i++) mesh.SetTriangles(indices[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
