using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.EditorTools
{
    public static class SabreModelReplacementSetup
    {
        const string Folder = "Assets/Models/Sabre/";
        [MenuItem("PirateSlop/Replace Sabre Model")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            ImportModel();
            Edit("Assets/Prefabs/Networking/NetworkPlayer.prefab", root =>
            {
                var weapon = root.GetComponent<PirateWeapon>();
                root.GetComponent<SabreAnimation>().MixamoViewOffset = new Vector3(-.04f, .10f, .28f);
                foreach (var pivot in new[] { weapon.SabreWorldPivot, weapon.SabreViewPivot })
                {
                    Clear(pivot);
                    var model = UnityEngine.Object.Instantiate(Visual(), pivot);
                    model.name = "SabreAssembly";
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    if (pivot == weapon.SabreViewPivot)
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            });
            Edit("Assets/Prefabs/Networking/DroppedSabre.prefab", root =>
            {
                Clear(root.transform);
                root.transform.localScale = Vector3.one;
                var model = UnityEngine.Object.Instantiate(Visual(), root.transform).transform;
                model.localRotation = Quaternion.Euler(90, 0, 0) * model.localRotation;
                var bounds = Bounds(root);
                model.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                bounds = Bounds(root);
                var box = root.GetComponent<BoxCollider>();
                if (box != null) { box.center = bounds.center; box.size = bounds.size + Vector3.one * .015f; }
            });
            InstallEffects();
            AssetDatabase.SaveAssets();
        }
        static GameObject Visual() => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SabreVisual.prefab");
        static void ImportModel()
        {
            Directory.CreateDirectory(Folder + "Meshes");
            AssetDatabase.Refresh();
            foreach (string suffix in new[] { "BaseColor.jpg", "Normal.png", "MetalSmooth.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "Textures/Sabre" + suffix);
                importer.textureType = suffix == "Normal.png" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = suffix == "BaseColor.jpg";
                importer.maxTextureSize = suffix == "BaseColor.jpg" ? 2048 : 1024;
                importer.isReadable = false; importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Sabre.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Folder + "Sabre.mat"); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Textures/SabreBaseColor.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Textures/SabreNormal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Textures/SabreMetalSmooth.png"));
            material.SetFloat("_Smoothness", 1); material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            string path = Folder + "SabreAssembly.fbx";
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(path);
            modelImporter.isReadable = true; modelImporter.importAnimation = false;
            modelImporter.importCameras = false; modelImporter.importLights = false;
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            modelImporter.SaveAndReimport();
            var preview = EditorSceneManager.NewPreviewScene();
            var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            SceneManager.MoveGameObjectToScene(source, preview);
            var root = new GameObject("SabreVisual"); SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var mirror = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                foreach (var t in source.GetComponentsInChildren<Transform>())
                {
                    if (t == source.transform) continue;
                    var node = new GameObject(t.name).transform; node.SetParent(root.transform, false);
                    node.position = mirror.MultiplyPoint3x4(t.position);
                    var filter = t.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name = "SabreBody";
                    var matrix = mirror * t.localToWorldMatrix;
                    mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v) - node.position).ToArray();
                    var normalMatrix = matrix.inverse.transpose;
                    mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
                    mesh.tangents = mesh.tangents.Select(tangent =>
                    {
                        var v = matrix.MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z)).normalized;
                        return new Vector4(v.x, v.y, v.z, -tangent.w);
                    }).ToArray();
                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        var indices = mesh.GetTriangles(i);
                        for (int j = 0; j < indices.Length; j += 3) { int first = indices[j]; indices[j] = indices[j + 2]; indices[j + 2] = first; }
                        mesh.SetTriangles(indices, i);
                    }
                    mesh.RecalculateBounds();
                    string meshPath = Folder + "Meshes/SabreBody.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                    else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                    node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                root.transform.localRotation = Quaternion.Euler(0, 180, 0);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "SabreVisual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(source); EditorSceneManager.ClosePreviewScene(preview); }
        }
        static void InstallEffects()
        {
            foreach (string name in new[] { "SabreCut", "SabreWoodChip" })
            {
                string path = "Assets/Resources/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("PirateSlop/" + name)); AssetDatabase.CreateAsset(material, path); }
                material.enableInstancing = true; EditorUtility.SetDirty(material);
            }
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            var entries = bank.Entries.ToList();
            var entry = entries.FirstOrDefault(e => e.Cue == SoundCue.SabreWood);
            if (entry == null) { entry = new GameAudioBank.Entry { Cue = SoundCue.SabreWood }; entries.Add(entry); }
            entry.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Foley/chop.ogg") };
            entry.Volume = .6f; entry.Distance = 22;
            bank.Entries = entries.ToArray(); EditorUtility.SetDirty(bank);
        }
        static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return new Bounds(root.transform.InverseTransformPoint(bounds.center), bounds.size);
        }
        static void Clear(Transform root)
        {
            foreach (var child in root.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        static void Edit(string path, Action<GameObject> change)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { change(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                typeof(EditorSceneManager).GetMethod("ClearTargetSceneForNewGameObjects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
            }
        }
    }
}
