using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class BarricadeAtlasSetup
    {
        static string Key(Vector3 p, Vector2 uv) => Mathf.RoundToInt(p.x * 10000f) + ":" + Mathf.RoundToInt(p.y * 10000f) + ":" + Mathf.RoundToInt(p.z * 10000f) + ":" + Mathf.RoundToInt(uv.x * 100000f) + ":" + Mathf.RoundToInt(uv.y * 100000f);

        public static string Configure()
        {
            const string folder = "Assets/Models/Barricade/";
            const string meshPath = folder + "BarricadeAtlas.asset";
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            foreach (string suffix in new[] { "BaseColor.jpg", "Normal.png", "MetalSmooth.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "Textures/Barricade" + suffix);
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "Barricade.mat");
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            var preview = AssetDatabase.LoadAssetAtPath<Material>(folder + "BarricadeConstruction.mat");
            preview.CopyPropertiesFromMaterial(material);
            preview.EnableKeyword("_NORMALMAP"); preview.EnableKeyword("_METALLICSPECGLOSSMAP");
            preview.renderQueue = 3000;
            EditorUtility.SetDirty(preview);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Barricade.fbx");
            var source = model.GetComponentInChildren<MeshFilter>();
            var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            mesh.name = "BarricadeAtlas";
            var oldUv = mesh.uv;
            var packed = material.GetTexture("_MetallicGlossMap");
            var target = RenderTexture.GetTemporary(packed.width, packed.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var metals = new Texture2D(packed.width, packed.height, TextureFormat.RGBA32, false, true);
            Graphics.Blit(packed, target);
            RenderTexture.active = target;
            metals.ReadPixels(new Rect(0, 0, packed.width, packed.height), 0, 0); metals.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            var corrected = Relax(mesh, metals);
            UnityEngine.Object.DestroyImmediate(metals);
            mesh.uv = corrected;
            mesh.RecalculateTangents();
            var mapping = new Dictionary<string, Vector2>();
            var matrix = source.transform.localToWorldMatrix;
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) mapping[Key(matrix.MultiplyPoint3x4(vertices[i]), oldUv[i])] = corrected[i];
            var container = SaveMesh(mesh, meshPath);
            var fragmentSource = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "BarricadeFragments.fbx").GetComponentsInChildren<MeshFilter>().ToDictionary(x => x.gameObject.name);
            int mapped = 0;
            foreach (string name in new[] { "BarricadeVisual", "BarricadeFragmentsVisual" })
            {
                string path = folder + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (name == "BarricadeVisual") { filter.sharedMesh = container; continue; }
                        var fragment = UnityEngine.Object.Instantiate(fragmentSource[filter.gameObject.name].sharedMesh);
                        fragment.name = filter.gameObject.name + "Atlas";
                        var uv = fragment.uv; var points = fragment.vertices;
                        var toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        for (int i = 0; i < points.Length; i++)
                            if (mapping.TryGetValue(Key(toRoot.MultiplyPoint3x4(points[i]), uv[i]), out var updated)) { uv[i] = updated; mapped++; }
                        fragment.uv = uv;
                        fragment.RecalculateTangents();
                        filter.sharedMesh = SaveMesh(fragment, meshPath);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorUtility.SetDirty(container);
            AssetDatabase.SaveAssetIfDirty(container);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(preview);
            int moved = 0;
            for (int i = 0; i < oldUv.Length; i++) if ((oldUv[i] - corrected[i]).sqrMagnitude > 1e-10f) moved++;
            return "Original PBR atlas 4096; " + moved + " interior UV vertices adjusted; " + mapped + " fragment vertices matched";
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var saved = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(x => x.name == mesh.name);
            if (saved != null) { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); return saved; }
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CreateAsset(mesh, path);
            else AssetDatabase.AddObjectToAsset(mesh, path);
            return mesh;
        }

        static Vector2[] Relax(Mesh mesh, Texture2D metals)
        {
            var points = mesh.vertices; var original = mesh.uv; var triangles = mesh.triangles;
            var keys = new Dictionary<string, int>();
            var groups = new int[points.Length];
            var positions = new List<Vector3>(); var uv = new List<Vector2>();
            for (int i = 0; i < points.Length; i++)
            {
                string key = Key(points[i], original[i]);
                if (!keys.TryGetValue(key, out int group)) { group = positions.Count; keys[key] = group; positions.Add(points[i]); uv.Add(original[i]); }
                groups[i] = group;
            }
            var weights = Enumerable.Range(0, positions.Count).Select(_ => new Dictionary<int, float>()).ToArray();
            var edges = new Dictionary<(int A, int B), int>();
            for (int i = 0; i < triangles.Length; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    int a = groups[triangles[i + j]], b = groups[triangles[i + (j + 1) % 3]], c = groups[triangles[i + (j + 2) % 3]];
                    if (a == b || a == c || b == c) continue;
                    var edge = a < b ? (a, b) : (b, a);
                    edges.TryGetValue(edge, out int count); edges[edge] = count + 1;
                    Vector3 ca = positions[a] - positions[c], cb = positions[b] - positions[c];
                    float weight = Mathf.Max(.02f, Vector3.Dot(ca, cb) / Mathf.Max(1e-12f, Vector3.Cross(ca, cb).magnitude));
                    weights[a].TryGetValue(b, out float ab); weights[a][b] = ab + weight;
                    weights[b].TryGetValue(a, out float ba); weights[b][a] = ba + weight;
                }
            var pinned = new bool[positions.Count];
            foreach (var edge in edges) if (edge.Value != 2) { pinned[edge.Key.A] = true; pinned[edge.Key.B] = true; }
            for (int i = 0; i < pinned.Length; i++) if (metals.GetPixelBilinear(uv[i].x, uv[i].y).r > .25f) pinned[i] = true;
            var metalCells = new bool[256 * 256];
            var pixels = metals.GetPixels32();
            for (int y = 0; y < metals.height; y++)
                for (int x = 0; x < metals.width; x++)
                    if (pixels[y * metals.width + x].r > 63) metalCells[(y * 256 / metals.height) * 256 + x * 256 / metals.width] = true;
            var prefix = new int[257 * 257];
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++) prefix[(y + 1) * 257 + x + 1] = (metalCells[y * 256 + x] ? 1 : 0) + prefix[y * 257 + x + 1] + prefix[(y + 1) * 257 + x] - prefix[y * 257 + x];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = groups[triangles[i]], b = groups[triangles[i + 1]], c = groups[triangles[i + 2]];
                Vector2 min = Vector2.Min(uv[a], Vector2.Min(uv[b], uv[c])), max = Vector2.Max(uv[a], Vector2.Max(uv[b], uv[c]));
                int x0 = Mathf.Clamp(Mathf.FloorToInt(min.x * 256), 0, 255), y0 = Mathf.Clamp(Mathf.FloorToInt(min.y * 256), 0, 255);
                int x1 = Mathf.Clamp(Mathf.FloorToInt(max.x * 256), 0, 255) + 1, y1 = Mathf.Clamp(Mathf.FloorToInt(max.y * 256), 0, 255) + 1;
                if (prefix[y1 * 257 + x1] - prefix[y0 * 257 + x1] - prefix[y1 * 257 + x0] + prefix[y0 * 257 + x0] > 0) pinned[a] = pinned[b] = pinned[c] = true;
            }
            var current = uv.ToArray(); var next = new Vector2[current.Length];
            for (int iteration = 0; iteration < 36; iteration++)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    next[i] = current[i];
                    if (pinned[i] || weights[i].Count == 0) continue;
                    Vector2 sum = Vector2.zero; float total = 0f;
                    foreach (var neighbour in weights[i]) { sum += current[neighbour.Key] * neighbour.Value; total += neighbour.Value; }
                    next[i] = Vector2.Lerp(current[i], sum / total, .5f);
                }
                (current, next) = (next, current);
            }
            var bad = new HashSet<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = groups[triangles[i]], b = groups[triangles[i + 1]], c = groups[triangles[i + 2]];
                if (Cross(uv[b] - uv[a], uv[c] - uv[a]) * Cross(current[b] - current[a], current[c] - current[a]) < 0f) { bad.Add(a); bad.Add(b); bad.Add(c); }
            }
            var queue = new Queue<int>(bad);
            while (queue.Count > 0)
            {
                int id = queue.Dequeue(); current[id] = uv[id];
                foreach (int neighbour in weights[id].Keys) if (bad.Add(neighbour)) queue.Enqueue(neighbour);
            }
            var result = new Vector2[original.Length];
            for (int i = 0; i < result.Length; i++) result[i] = current[groups[i]];
            return result;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
