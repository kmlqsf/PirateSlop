using System;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static class FogBottleSetup
    {
        const string Folder = "Assets/Models/Loot/FogBottle/";
        const string ModelPath = "Assets/Prefabs/Loot/FogBottle.prefab";
        const string PickupPath = "Assets/Prefabs/Loot/FogBottlePickup.prefab";
        public static void Configure()
        {
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Models/Loot", "FogBottle");
            var glass = MakeMaterial("FogGlass", "Universal Render Pipeline/Lit", new Color(.4f, .5f, .48f, .16f), true);
            glass.SetFloat("_Smoothness", .85f);
            glass.SetFloat("_Cull", 0f);
            glass.SetFloat("_QueueOffset", 10f);
            glass.renderQueue = 3010;
            EditorUtility.SetDirty(glass);
            var model = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Loot/VortexBottle.prefab");
            try
            {
                model.name = "FogBottle";
                foreach (var swirl in model.GetComponentsInChildren<VortexBottleVisual>(true)) UnityEngine.Object.DestroyImmediate(swirl);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name == "Glass") renderer.sharedMaterial = glass;
                var bottleBase = model.transform.Find("Bottle");
                var mistObject = new GameObject("BottleMist");
                mistObject.transform.SetParent(bottleBase, false);
                mistObject.transform.localPosition = new Vector3(0, .085f, 0);
                var mist = mistObject.AddComponent<FogCloudVisual>();
                mist.Radius = .075f;
                mist.Height = .26f;
                mist.Density = 45f;
                mist.InsideBottle = true;
                var bottle = model.AddComponent<FogBottleVisual>();
                bottle.Mist = mist;
                PrefabUtility.SaveAsPrefabAsset(model, ModelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(model); }
            model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var cloud = CreateCloud();
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath) != null ? PickupPath : "Assets/Prefabs/Loot/RumBottle.prefab");
            try
            {
                root.name = "FogBottlePickup";
                root.transform.localScale = Vector3.one;
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                PrefabUtility.InstantiatePrefab(model, root.transform);
                var shape = root.GetComponent<BoxCollider>();
                shape.center = Vector3.zero;
                shape.size = new Vector3(.24f, .56f, .24f);
                root.GetComponent<NetworkFish>().Item = InventoryItem.FogBottle;
                var projectile = root.GetComponent<NetworkFogBottle>() ?? root.AddComponent<NetworkFogBottle>();
                projectile.CloudPrefab = cloud;
                PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath).GetComponent<NetworkFish>();
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            var networkObject = pickup.GetComponent<NetworkObject>();
            string hash = new string((PickupPath + pickup.name).ToLowerInvariant().Where(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9').ToArray());
            networkObject.SetAssetPathHash(hash.GetStableHashU64());
            EditorUtility.SetDirty(networkObject);
            registry.AddObject(networkObject, true, true);
            EditorUtility.SetDirty(registry);
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            root = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var weapon = root.GetComponent<NetworkWeapon>();
                var drops = weapon.DropPrefabs;
                Array.Resize(ref drops, Mathf.Max(drops.Length, 25));
                drops[24] = pickup;
                weapon.DropPrefabs = drops;
                var equipment = root.GetComponent<NetworkEquipment>();
                var models = equipment.Models;
                Array.Resize(ref models, Mathf.Max(models.Length, 12));
                models[11] = model;
                equipment.Models = models;
                var icons = root.GetComponent<PlayerInventory>().Icons;
                var images = icons.Icons;
                Array.Resize(ref images, Mathf.Max(images.Length, 25));
                images[24] = MakeIcon();
                icons.Icons = images;
                EditorUtility.SetDirty(icons);
                PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
            root = PrefabUtility.LoadPrefabContents(shipPath);
            try
            {
                var shelf = root.GetComponent<ExperimentalShipEquipment>();
                if (!shelf.Prefabs.Contains(pickup)) shelf.Prefabs = shelf.Prefabs.Concat(new[] { pickup }).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, shipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var loose = catalog.LoosePrefabs;
            Array.Resize(ref loose, Mathf.Max(loose.Length, 25));
            loose[24] = pickup;
            catalog.LoosePrefabs = loose;
            if (!catalog.Items.Any(e => e != null && e.Item == InventoryItem.FogBottle))
                catalog.Items = catalog.Items.Concat(new[] { new LootCatalog.Entry { Item = InventoryItem.FogBottle, Name = InventoryIcons.ItemName(InventoryItem.FogBottle), Weight = 3f } }).ToArray();
            EditorUtility.SetDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(108, config.ProtocolVersion);
            EditorUtility.SetDirty(config);
            foreach (var asset in new UnityEngine.Object[] { glass, iconsAsset(), catalog, registry, networkObject, config }) AssetDatabase.SaveAssetIfDirty(asset);
        }
        static NetworkFogCloud CreateCloud()
        {
            const string path = "Assets/Prefabs/Loot/FogCloud.prefab";
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null ? path : "Assets/Prefabs/Loot/RumBottle.prefab");
            try
            {
                root.name = "FogCloud";
                root.transform.localScale = Vector3.one;
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var pickup = root.GetComponent<NetworkFish>();
                if (pickup != null) UnityEngine.Object.DestroyImmediate(pickup);
                foreach (var collider in root.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                var body = root.GetComponent<Rigidbody>();
                if (body != null) UnityEngine.Object.DestroyImmediate(body);
                if (root.GetComponent<NetworkFogCloud>() == null) root.AddComponent<NetworkFogCloud>();
                var visual = root.GetComponent<FogCloudVisual>() ?? root.AddComponent<FogCloudVisual>();
                visual.Radius = 50f;
                visual.Height = 44f;
                visual.Density = .18f;
                visual.InsideBottle = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var networkObject = prefab.GetComponent<NetworkObject>();
            string hash = new string((path + prefab.name).ToLowerInvariant().Where(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9').ToArray());
            networkObject.SetAssetPathHash(hash.GetStableHashU64());
            EditorUtility.SetDirty(networkObject);
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            registry.AddObject(networkObject, true, true);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
            AssetDatabase.SaveAssetIfDirty(networkObject);
            return prefab.GetComponent<NetworkFogCloud>();
        }
        static InventoryIcons iconsAsset() => AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset");
        static Material MakeMaterial(string name, string shaderName, Color color, bool transparent)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = transparent ? 3000 : 2000;
            EditorUtility.SetDirty(material);
            return material;
        }
        static Texture2D MakeIcon()
        {
            const string path = Folder + "FogBottleIcon.asset";
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (image == null) { image = new Texture2D(128, 128, TextureFormat.RGBA32, false); AssetDatabase.CreateAsset(image, path); }
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float h = y / 127f, side = Mathf.Abs(x / 127f - .5f);
                    float radius = h < .65f ? .23f : h < .77f ? Mathf.Lerp(.23f, .08f, (h - .65f) / .12f) : .08f;
                    if (h < .08f || h > .94f || side > radius) continue;
                    var color = new Color(.18f, .48f, .57f, .7f);
                    if (side > radius - .022f || h < .1f) color = new Color(.65f, .88f, .96f, 1f);
                    if (h > .89f) color = new Color(.48f, .3f, .13f, 1f);
                    if (h > .16f && h < .62f)
                    {
                        float dx = (x / 127f - .5f) / .17f;
                        float dy = (h - .38f) / .22f;
                        float smoke = 1f - Mathf.Clamp01(dx * dx + dy * dy);
                        color = Color.Lerp(color, new Color(.66f, .72f, .69f, 1f), smoke);
                        float cloudEdge = Mathf.Sin(h * 65f + x * .15f) * .02f;
                        if (side < .13f + cloudEdge) color = Color.Lerp(color, new Color(.83f, .87f, .85f, 1f), smoke * .45f);
                    }
                    pixels[y * 128 + x] = color;
                }
            image.SetPixels(pixels);
            image.Apply();
            image.filterMode = FilterMode.Bilinear;
            image.wrapMode = TextureWrapMode.Clamp;
            EditorUtility.SetDirty(image);
            AssetDatabase.SaveAssetIfDirty(image);
            return image;
        }
    }
}
