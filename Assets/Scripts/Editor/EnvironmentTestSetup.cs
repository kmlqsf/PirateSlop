using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class EnvironmentTestSetup
    {
        const string Source = "Assets/Game/Environment";
        const string Prefabs = "Assets/Prefabs/Environment";
        const string Materials = "Assets/Materials/Environment";
        const string GalleryPath = "Assets/Resources/EnvironmentTest/Gallery.prefab";

        [MenuItem("PirateSlop/Prepare Environment Test")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before preparing environment assets.");
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Materials);
            Directory.CreateDirectory(Path.GetDirectoryName(GalleryPath));
            AssetDatabase.Refresh();
            int layer = LayerMask.NameToLayer("WorldStatic");
            if (layer < 0) throw new InvalidOperationException("WorldStatic layer is missing.");
            var paths = AssetDatabase.FindAssets("t:Model", new[] { Source }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var gallery = new GameObject("EnvironmentTestGallery");
            try
            {
                var labels = new List<Transform>();
                var entries = new List<GameObject>();
                var decorations = new List<WorldDecoration>();
                float cursor = 0f, depth = 0f;
                foreach (var path in paths)
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.importAnimation = false;
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.addCollider = false;
                    importer.meshCompression = ModelImporterMeshCompression.Off;
                    importer.SaveAndReimport();
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model.name == "Reef_Spires_B")
                    {
                        var size = BoundsOf(model, false).size;
                        importer.globalScale *= 100f / Mathf.Max(size.x, size.z);
                        importer.SaveAndReimport();
                        model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    }
                    var root = new GameObject(model.name);
                    try
                    {
                        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                        var meshes = visual.GetComponentsInChildren<MeshFilter>(true);
                        bool authoredCollision = meshes.Any(m => m.name.EndsWith("_COL", StringComparison.OrdinalIgnoreCase));
                        var palettePath = Directory.GetFiles(Path.GetDirectoryName(path), "*.png").SingleOrDefault();
                        Texture2D palette = null;
                        if (palettePath != null)
                        {
                            palettePath = palettePath.Replace('\\', '/');
                            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(palettePath);
                            textureImporter.textureType = TextureImporterType.Default;
                            textureImporter.sRGBTexture = true;
                            textureImporter.filterMode = FilterMode.Point;
                            textureImporter.wrapMode = TextureWrapMode.Clamp;
                            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                            textureImporter.mipmapEnabled = false;
                            textureImporter.SaveAndReimport();
                            palette = AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);
                        }
                        foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = layer;
                        foreach (var mesh in meshes)
                        {
                            bool collision = mesh.name.EndsWith("_COL", StringComparison.OrdinalIgnoreCase);
                            var renderer = mesh.GetComponent<MeshRenderer>();
                            if (collision || !authoredCollision)
                            {
                                var collider = mesh.gameObject.AddComponent<MeshCollider>();
                                collider.sharedMesh = mesh.sharedMesh;
                                collider.convex = false;
                            }
                            if (renderer == null) continue;
                            renderer.enabled = !collision;
                            if (collision) continue;
                            renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                            {
                                if (source == null) throw new InvalidOperationException(path + " has an unassigned material.");
                                string matPath = Materials + "/" + model.name + "_" + source.name + ".mat";
                                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                                if (mat == null)
                                {
                                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                    AssetDatabase.CreateAsset(mat, matPath);
                                }
                                mat.color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                                mat.SetFloat("_Smoothness", .15f);
                                mat.SetFloat("_Metallic", 0f);
                                if (palette != null && mesh.sharedMesh.uv.Length > 0)
                                {
                                    mat.SetTexture("_BaseMap", palette);
                                    mat.SetColor("_BaseColor", Color.white);
                                }
                                mat.enableInstancing = true;
                                EditorUtility.SetDirty(mat);
                                return mat;
                            }).ToArray();
                        }
                        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + model.name + ".prefab");
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gallery.transform);
                        var bounds = BoundsOf(instance, true);
                        bool small = Mathf.Max(bounds.size.x, bounds.size.z) <= 110f;
                        decorations.Add(new WorldDecoration
                        {
                            Prefab = prefab,
                            ClearanceRadius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude,
                            MinCount = small ? 15 : 5,
                            MaxCount = small ? 20 : 10,
                            PrefabVersion = AssetDatabase.GetAssetDependencyHash(path).ToString()
                        });
                        instance.transform.localPosition = new Vector3(cursor + bounds.extents.x - bounds.center.x, 0, -bounds.center.z);
                        entries.Add(instance);
                        var label = new GameObject(model.name).transform;
                        label.SetParent(gallery.transform, false);
                        label.localPosition = new Vector3(cursor + bounds.extents.x, Mathf.Max(12f, bounds.max.y + 18f), 0);
                        labels.Add(label);
                        cursor += bounds.size.x + 110f;
                        depth = Mathf.Max(depth, bounds.extents.z);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                if (entries.Count == 0) throw new InvalidOperationException("No environment models found.");
                float half = (cursor - 110f) * .5f;
                foreach (var entry in entries) entry.transform.localPosition -= Vector3.right * half;
                foreach (var label in labels) label.localPosition -= Vector3.right * half;
                var component = gallery.AddComponent<EnvironmentTestGallery>();
                component.Labels = labels.ToArray();
                component.Spawn = new Vector3(-half, 0, -depth - 120f);
                component.Radius = Mathf.Max(1000f, half + depth + 500f);
                component.Revision = WorldLayout.Hash(string.Join("|", paths.Select(p => p + AssetDatabase.GetAssetDependencyHash(p))) + string.Join("|", entries.Select(e => AssetDatabase.GetAssetDependencyHash(Prefabs + "/" + e.name + ".prefab").ToString())));
                PrefabUtility.SaveAsPrefabAsset(gallery, GalleryPath);
                var profile = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
                if (profile == null) throw new InvalidOperationException("DefaultWorld profile is missing.");
                profile.Decorations = decorations.ToArray();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log("Prepared " + entries.Count + " environment models and Test Environments gallery.");
            }
            finally { UnityEngine.Object.DestroyImmediate(gallery); }
        }

        static Bounds BoundsOf(GameObject root, bool visibleOnly)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => !visibleOnly || r.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException(root.name + " has no rendered geometry.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
