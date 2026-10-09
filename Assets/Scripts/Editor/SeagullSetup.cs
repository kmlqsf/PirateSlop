using System;
using System.Collections.Generic;
using System.Linq;
using PirateSlop.World;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Editor
{
    public static class SeagullSetup
    {
        struct Triangle
        {
            public Vector3 A, B, C, Normal;
            public float Area;
        }

        [MenuItem("PirateSlop/Prepare Ambient Seagulls")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/World")) AssetDatabase.CreateFolder("Assets/Resources", "World");
            Material("SeagullWhite", new Color(.92f, .94f, .93f));
            Material("SeagullDark", new Color(.18f, .21f, .23f));
            Material("SeagullFeet", new Color(.55f, .39f, .18f));
            PrepareAudio();
            string[] environment = { "Sea_Lagoon_Cave", "Reef_Moai_A", "Reef_Spires_A", "Reef_Spires_B", "Reef_Spires_C", "SeaArch_Huge_A", "Reef_ShallowField_A" };
            string[] starter = { "RockLarge", "RockMedium", "CliffWallA", "CliffWallB", "CliffWallC" };
            foreach (var path in environment.Select(n => "Assets/Prefabs/Environment/" + n + ".prefab")
                .Concat(starter.Select(n => "Assets/Prefabs/World/StarterIsland/" + n + ".prefab")))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var visual = root.GetComponentInChildren<CoastalRockVisual>(true);
                    if (visual == null) throw new InvalidOperationException("Missing coastal visual: " + path);
                    BuildPerches(visual.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void PrepareAudio()
        {
            const string path = "Assets/Audio/Ambience/Seagulls/SeagullCall.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing seagull call: " + path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            if (bank == null) throw new InvalidOperationException("Missing GameAudioBank.");
            var entry = bank.Entries.FirstOrDefault(e => e != null && e.Cue == SoundCue.Seagull);
            if (entry == null)
            {
                entry = new GameAudioBank.Entry { Cue = SoundCue.Seagull };
                bank.Entries = bank.Entries.Append(entry).ToArray();
            }
            entry.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(path) };
            entry.Volume = .55f;
            entry.Distance = 170;
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
        }

        static void Material(string name, Color color)
        {
            var path = "Assets/Resources/World/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .18f);
            material.SetFloat("_Metallic", 0);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        public static void BuildPerches(GameObject visual)
        {
            var triangles = new List<Triangle>();
            var lod = visual.transform.Find("LOD0");
            if (lod == null) return;
            foreach (var filter in lod.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.name.Contains("_Rock_")) continue;
                var matrix = visual.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                using var meshData = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                var data = meshData[0];
                using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                data.GetVertices(vertices);
                for (int sub = 0; sub < data.subMeshCount; sub++)
                {
                    var descriptor = data.GetSubMesh(sub);
                    if (descriptor.topology != MeshTopology.Triangles) continue;
                    var shortIndices = data.indexFormat == IndexFormat.UInt16 ? data.GetIndexData<ushort>() : default;
                    var longIndices = data.indexFormat == IndexFormat.UInt32 ? data.GetIndexData<int>() : default;
                    for (int i = descriptor.indexStart; i < descriptor.indexStart + descriptor.indexCount; i += 3)
                    {
                        int Index(int j) => (data.indexFormat == IndexFormat.UInt16 ? shortIndices[j] : longIndices[j]) + descriptor.baseVertex;
                        var a = matrix.MultiplyPoint3x4(vertices[Index(i)]);
                        var b = matrix.MultiplyPoint3x4(vertices[Index(i + 1)]);
                        var c = matrix.MultiplyPoint3x4(vertices[Index(i + 2)]);
                        var cross = Vector3.Cross(b - a, c - a);
                        triangles.Add(new Triangle { A = a, B = b, C = c, Normal = cross.normalized * (matrix.determinant < 0 ? -1 : 1), Area = cross.magnitude * .5f });
                    }
                }
            }
            if (triangles.Count == 0) return;
            float top = triangles.Max(t => Mathf.Max(t.A.y, Mathf.Max(t.B.y, t.C.y))) + 5;
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            int attempts = 0;
            foreach (var triangle in triangles.Where(t => t.Normal.y >= .8f && t.Area >= .04f).OrderByDescending(t => t.Area))
            {
                if (points.Count >= 24 || attempts >= 220) break;
                var point = (triangle.A + triangle.B + triangle.C) / 3;
                if (points.Any(p => Vector3.Distance(p, point) < 2)) continue;
                attempts++;
                if (!Surface(triangles, new Vector3(point.x, top, point.z), out var hit, out var normal) || hit.y - point.y > .08f || normal.y < .8f) continue;
                bool supported = true;
                foreach (var offset in new[] { Vector3.right * .22f, Vector3.left * .22f, Vector3.forward * .22f, Vector3.back * .22f })
                {
                    var origin = hit + offset; origin.y = top;
                    float planeHeight = hit.y - (normal.x * offset.x + normal.z * offset.z) / normal.y;
                    if (!Surface(triangles, origin, out var support, out var supportNormal) || Mathf.Abs(support.y - planeHeight) > .06f || supportNormal.y < .65f) { supported = false; break; }
                }
                if (!supported) continue;
                points.Add(hit); normals.Add(normal);
            }
            var perches = visual.GetComponent<CoastalSeagullPerches>();
            if (perches == null) perches = visual.AddComponent<CoastalSeagullPerches>();
            perches.Points = points.ToArray(); perches.Normals = normals.ToArray();
            var bounds = new Bounds(triangles[0].A, Vector3.zero);
            foreach (var triangle in triangles) { bounds.Encapsulate(triangle.A); bounds.Encapsulate(triangle.B); bounds.Encapsulate(triangle.C); }
            perches.RockBounds = bounds;
            EditorUtility.SetDirty(perches);
        }

        static bool Surface(List<Triangle> triangles, Vector3 origin, out Vector3 point, out Vector3 normal)
        {
            float nearest = float.PositiveInfinity;
            normal = Vector3.up;
            foreach (var triangle in triangles)
            {
                var edge1 = triangle.B - triangle.A;
                var edge2 = triangle.C - triangle.A;
                var p = Vector3.Cross(Vector3.down, edge2);
                float determinant = Vector3.Dot(edge1, p);
                if (Mathf.Abs(determinant) < .000001f) continue;
                float inverse = 1 / determinant;
                var t = origin - triangle.A;
                float u = Vector3.Dot(t, p) * inverse;
                if (u < 0 || u > 1) continue;
                var q = Vector3.Cross(t, edge1);
                float v = Vector3.Dot(Vector3.down, q) * inverse;
                if (v < 0 || u + v > 1) continue;
                float distance = Vector3.Dot(edge2, q) * inverse;
                if (distance < 0 || distance >= nearest) continue;
                nearest = distance; normal = triangle.Normal;
            }
            point = origin + Vector3.down * nearest;
            return !float.IsInfinity(nearest);
        }
    }
}
