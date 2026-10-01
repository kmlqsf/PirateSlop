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
    public static class VortexBottleSetup
    {
        const string Folder = "Assets/Models/Loot/VortexBottle/";
        const string ModelPath = "Assets/Prefabs/Loot/VortexBottle.prefab";
        const string PickupPath = "Assets/Prefabs/Loot/VortexBottlePickup.prefab";
        public static void Configure()
        {
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Models/Loot", "VortexBottle");
            var glass = MakeMaterial("VortexGlass", "Universal Render Pipeline/Lit", new Color(.22f, .55f, .65f, .18f), true);
            glass.SetFloat("_Smoothness", .85f);
            glass.SetFloat("_Cull", 0f);
            glass.SetFloat("_QueueOffset", 10f);
            glass.renderQueue = 3010;
            EditorUtility.SetDirty(glass);
            var glow = MakeMaterial("VortexGlow", "Universal Render Pipeline/Unlit", new Color(.5f, 1.7f, 2f, .8f), true);
            glow.SetFloat("_Blend", 2f);
            glow.SetFloat("_DstBlend", (float)BlendMode.One);
            glow.SetFloat("_Cull", 0f);
            glow.renderQueue = 3000;
            EditorUtility.SetDirty(glow);
            var cork = MakeMaterial("VortexCork", "Universal Render Pipeline/Lit", new Color(.25f, .13f, .065f), false);
            var preview = EditorSceneManager.NewPreviewScene();
            var model = new GameObject("VortexBottle");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, preview);
            try
            {
                var bottleBase = new GameObject("Bottle");
                bottleBase.transform.SetParent(model.transform, false);
                bottleBase.transform.localPosition = new Vector3(0, -.28f, 0);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Loot/RumBottle.prefab");
                foreach (Transform child in source.transform)
                {
                    var shell = UnityEngine.Object.Instantiate(child.gameObject, bottleBase.transform);
                    shell.name = "Glass";
                    foreach (var shape in shell.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(shape);
                    foreach (var renderer in shell.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = glass;
                }
                var stopper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stopper.name = "Cork";
                stopper.transform.SetParent(bottleBase.transform, false);
                stopper.transform.localPosition = new Vector3(0, .535f, 0);
                stopper.transform.localScale = new Vector3(.078f, .022f, .078f);
                UnityEngine.Object.DestroyImmediate(stopper.GetComponent<Collider>());
                stopper.GetComponent<Renderer>().sharedMaterial = cork;
                var swirl = bottleBase.AddComponent<VortexBottleVisual>();
                swirl.SwirlMaterial = glow;
                PrefabUtility.SaveAsPrefabAsset(model, ModelPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(model);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath) != null ? PickupPath : "Assets/Prefabs/Loot/RumBottle.prefab");
            try
            {
                root.name = "VortexBottlePickup";
                root.transform.localScale = Vector3.one;
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                PrefabUtility.InstantiatePrefab(model, root.transform);
                var shape = root.GetComponent<BoxCollider>();
                shape.center = Vector3.zero;
                shape.size = new Vector3(.24f, .56f, .24f);
                root.GetComponent<NetworkFish>().Item = InventoryItem.VortexBottle;
                if (root.GetComponent<NetworkVortexBottle>() == null) root.AddComponent<NetworkVortexBottle>();
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
                Array.Resize(ref drops, Mathf.Max(drops.Length, 24));
                drops[23] = pickup;
                weapon.DropPrefabs = drops;
                var equipment = root.GetComponent<NetworkEquipment>();
                var models = equipment.Models;
                Array.Resize(ref models, Mathf.Max(models.Length, 11));
                models[10] = model;
                equipment.Models = models;
                var icons = root.GetComponent<PlayerInventory>().Icons;
                var images = icons.Icons;
                Array.Resize(ref images, Mathf.Max(images.Length, 24));
                images[23] = MakeIcon();
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
            Array.Resize(ref loose, Mathf.Max(loose.Length, 24));
            loose[23] = pickup;
            catalog.LoosePrefabs = loose;
            if (!catalog.Items.Any(e => e != null && e.Item == InventoryItem.VortexBottle))
                catalog.Items = catalog.Items.Concat(new[] { new LootCatalog.Entry { Item = InventoryItem.VortexBottle, Name = InventoryIcons.ItemName(InventoryItem.VortexBottle), Weight = 3f } }).ToArray();
            EditorUtility.SetDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(107, config.ProtocolVersion);
            EditorUtility.SetDirty(config);
            foreach (var asset in new UnityEngine.Object[] { glass, glow, cork, iconsAsset(), catalog, registry, networkObject, config }) AssetDatabase.SaveAssetIfDirty(asset);
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
            const string path = Folder + "VortexBottleIcon.asset";
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
                        float t = (h - .16f) / .46f;
                        for (int arm = 0; arm < 3; arm++)
                            if (Mathf.Abs(x / 127f - .5f - Mathf.Cos((t * 1.35f + arm / 3f) * Mathf.PI * 2f) * Mathf.Lerp(.035f, .18f, t)) < .014f)
                                color = new Color(.65f, 1f, 1f, 1f);
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
