using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class HandMortarSetup
    {
        const string Folder = "Assets/Models/HandMortar/";
        const string PickupPath = "Assets/Prefabs/Networking/HandMortarPickup.prefab";
        const string BallPath = "Assets/Prefabs/Networking/HandMortarBall.prefab";
        const string SettingsPath = "Assets/Settings/Weapons/HandMortar.asset";

        [MenuItem("PirateSlop/Configure Hand Mortar")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder + "Meshes");
            Directory.CreateDirectory("Assets/Settings/Weapons");
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<HandMortarSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<HandMortarSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            PrepareMaterials();
            var visual = CreateVisual();
            var pickup = CreatePickup(visual);
            var ball = CreateBall(settings);
            Edit("Assets/Prefabs/Networking/NetworkPlayer.prefab", root =>
            {
                var weapon = root.GetComponent<NetworkWeapon>(); var drops = weapon.DropPrefabs;
                Array.Resize(ref drops, Math.Max(drops.Length, 28)); drops[27] = pickup; weapon.DropPrefabs = drops;
                var equipment = root.GetComponent<NetworkEquipment>(); var models = equipment.Models;
                Array.Resize(ref models, Math.Max(models.Length, 15)); models[14] = visual; equipment.Models = models;
                equipment.MortarSettings = settings; equipment.MortarBallPrefab = ball;
            });
            Edit("Assets/Resources/Ships/ShipV3Test.prefab", root => ConfigureImportedShip(root, pickup));
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            Register(pickup.GetComponent<NetworkObject>(), PickupPath, registry); Register(ball.GetComponent<NetworkObject>(), BallPath, registry);
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var loose = catalog.LoosePrefabs; Array.Resize(ref loose, Math.Max(loose.Length, 28)); loose[27] = pickup; catalog.LoosePrefabs = loose;
            if (!catalog.Items.Any(e => e != null && e.Item == InventoryItem.HandMortar))
                catalog.Items = catalog.Items.Concat(new[] { new LootCatalog.Entry { Item = InventoryItem.HandMortar, Name = "Ручная мортира", Weight = 5f } }).ToArray();
            const string iconPath = "Assets/UI/Inventory/HandMortar.png";
            InventorySlotSetup.RenderIcon(visual, iconPath); AssetDatabase.ImportAsset(iconPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath); importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset"); var images = icons.Icons;
            Array.Resize(ref images, Math.Max(images.Length, 28)); images[27] = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath); icons.Icons = images;
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Math.Max(config.ProtocolVersion, 137);
            foreach (var asset in new UnityEngine.Object[] { registry, catalog, icons, config }) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
            AssetDatabase.SaveAssets();
        }

        static void PrepareMaterials()
        {
            foreach (string key in new[] { "Body", "Trigger", "FlintHammer", "Frizzen" })
            {
                foreach (string suffix in new[] { "basecolor.JPEG", "normal.PNG", "MetalSmooth.png" })
                {
                    string path = Folder + key + "_" + suffix;
                    var texture = (TextureImporter)AssetImporter.GetAtPath(path);
                    texture.textureType = suffix == "normal.PNG" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    texture.sRGBTexture = suffix == "basecolor.JPEG";
                    texture.maxTextureSize = suffix == "basecolor.JPEG" ? 2048 : 1024;
                    texture.mipmapEnabled = true; texture.streamingMipmaps = true; texture.textureCompression = TextureImporterCompression.CompressedHQ;
                    texture.SaveAndReimport();
                }
                var material = Material(key, "Universal Render Pipeline/Lit");
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + key + "_basecolor.JPEG"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + key + "_normal.PNG"));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + key + "_MetalSmooth.png"));
                material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Smoothness", .8f); material.SetFloat("_BumpScale", .6f); material.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        static Material Material(string key, string shader)
        {
            string path = Folder + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
            return material;
        }

        static GameObject CreateVisual()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "HandMortarAssembly.fbx"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source, scene);
            var root = new GameObject("HandMortarVisual"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                Vector3 grip = source.GetComponentsInChildren<Transform>().Single(t => t.name == "Grip").position;
                var nodes = new Dictionary<Transform, Transform> { [source.transform] = root.transform };
                foreach (var t in source.GetComponentsInChildren<Transform>(true))
                {
                    if (t == source.transform) continue;
                    var target = new GameObject(t.name).transform; target.SetParent(nodes[t.parent], false); target.position = t.position - grip; nodes[t] = target;
                    var filter = t.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) continue;
                    var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name = t.name;
                    var matrix = t.localToWorldMatrix; Vector3 origin = t.position;
                    mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v) - origin).ToArray();
                    var normals = matrix.inverse.transpose; mesh.normals = mesh.normals.Select(n => normals.MultiplyVector(n).normalized).ToArray();
                    mesh.tangents = mesh.tangents.Select(v => { var tangent = matrix.MultiplyVector(new Vector3(v.x, v.y, v.z)).normalized; return new Vector4(tangent.x, tangent.y, tangent.z, v.w); }).ToArray();
                    mesh.RecalculateBounds();
                    string path = Folder + "Meshes/" + t.name + ".asset"; var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) AssetDatabase.CreateAsset(mesh, path);
                    else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); mesh = saved; EditorUtility.SetDirty(mesh); }
                    target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    target.gameObject.AddComponent<MeshRenderer>().sharedMaterial = t.name == "Wick" ? Material("Wick", "Universal Render Pipeline/Lit") : Material(t.name, "Universal Render Pipeline/Lit");
                }
                var all = root.GetComponentsInChildren<Transform>();
                Transform Find(string name) => all.Single(t => t.name == name);
                var wick = Material("Wick", "Universal Render Pipeline/Lit"); wick.SetColor("_BaseColor", new Color(.34f, .25f, .12f)); EditorUtility.SetDirty(wick);
                if (Find("Wick").GetComponent<Renderer>() == null) throw new InvalidOperationException("Wick was not exported as geometry.");
                var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere); ember.name = "Ember"; UnityEngine.Object.DestroyImmediate(ember.GetComponent<Collider>());
                ember.transform.SetParent(root.transform, false); ember.transform.localPosition = new Vector3(.1332f, .21456f, .20808f); ember.transform.localScale = Vector3.one * .009f;
                var emberMaterial = Material("Ember", "Universal Render Pipeline/Unlit"); emberMaterial.SetColor("_BaseColor", new Color(2f, 1.1f, .25f)); EditorUtility.SetDirty(emberMaterial);
                ember.GetComponent<Renderer>().sharedMaterial = emberMaterial; ember.SetActive(false);
                var visual = root.AddComponent<HandMortarVisual>();
                visual.Trigger = Find("TriggerPivot"); visual.Hammer = Find("FlintHammerPivot"); visual.Frizzen = Find("FrizzenPivot");
                visual.Wick = Find("Wick"); visual.Ember = ember.transform; visual.StrikePoint = Find("StrikePoint"); visual.Muzzle = Find("Muzzle");
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "HandMortarVisual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(source); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static NetworkFish CreatePickup(GameObject visual)
        {
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("HandMortarPickup"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkFish>().Item = InventoryItem.HandMortar;
                var model = UnityEngine.Object.Instantiate(visual, root.transform);
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var bounds = new Bounds(); bool first = true;
                foreach (var r in model.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
                var shape = root.AddComponent<BoxCollider>(); shape.center = bounds.center; shape.size = bounds.size;
                root.AddComponent<BulletSurface>().Kind = BulletSurfaceKind.Metal;
                return PrefabUtility.SaveAsPrefabAsset(root, PickupPath).GetComponent<NetworkFish>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static NetworkHandMortarBall CreateBall(HandMortarSettings settings)
        {
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("HandMortarBall"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkHandMortarBall>();
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name = "Ball"; ball.transform.SetParent(root.transform, false); ball.transform.localScale = Vector3.one * settings.BallRadius * 2f;
                UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
                var material = Material("Ball", "Universal Render Pipeline/Lit"); material.SetColor("_BaseColor", new Color(.07f, .075f, .08f)); material.SetFloat("_Metallic", .85f); material.SetFloat("_Smoothness", .25f); EditorUtility.SetDirty(material);
                ball.GetComponent<Renderer>().sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(root, BallPath).GetComponent<NetworkHandMortarBall>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void ConfigureImportedShip(GameObject root, NetworkFish pickup = null)
        {
            if (pickup == null) pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath)?.GetComponent<NetworkFish>();
            var stock = root.GetComponent<ExperimentalShipEquipment>(); if (stock == null || pickup == null) return;
            var prefabs = stock.Prefabs.ToList(); var items = stock.Items.ToList(); var points = stock.SpawnPoints.ToList();
            int index = prefabs.FindIndex(p => p != null && p.Item == InventoryItem.HandMortar);
            if (index < 0)
            {
                var point = root.transform.Find("Loot_HandMortar"); if (point == null) { point = new GameObject("Loot_HandMortar").transform; point.SetParent(root.transform, false); }
                point.localPosition = new Vector3(1.75f, 4.11f, -1.3f);
                prefabs.Add(pickup); items.Add(InventoryItem.HandMortar); points.Add(point);
            }
            else prefabs[index] = pickup;
            stock.Prefabs = prefabs.ToArray(); stock.Items = items.ToArray(); stock.SpawnPoints = points.ToArray();
        }

        internal static void Register(NetworkObject networkObject, string path, SinglePrefabObjects registry)
        {
            string hash = new string((path + networkObject.name).ToLowerInvariant().Where(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9').ToArray());
            networkObject.SetAssetPathHash(hash.GetStableHashU64()); EditorUtility.SetDirty(networkObject); registry.AddObject(networkObject, true, true);
        }

        internal static void Edit(string path, Action<GameObject> action)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
