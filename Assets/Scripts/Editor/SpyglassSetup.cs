using System;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class SpyglassSetup
    {
        const string VisualPath = "Assets/Prefabs/Props/PirateEquipment/Spyglass.prefab";
        const string PickupPath = "Assets/Prefabs/Props/PirateEquipment/SpyglassPickup.prefab";

        [MenuItem("PirateSlop/Configure Portable Spyglass")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>(shipPath);
            var source = ship.GetComponentInChildren<ShipSpyglass>(true).transform.Find("Spyglass");
            var root = UnityEngine.Object.Instantiate(source.gameObject);
            try
            {
                root.name = "Spyglass";
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                foreach (var collider in root.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath) != null ? PickupPath : "Assets/Prefabs/Networking/DroppedPistol.prefab");
            try
            {
                root.name = "SpyglassPickup";
                root.transform.localScale = Vector3.one;
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
                var renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var shape = root.GetComponent<BoxCollider>();
                shape.center = root.transform.InverseTransformPoint(bounds.center);
                shape.size = Vector3.Max(bounds.size, Vector3.one * .12f);
                root.GetComponent<NetworkFish>().Item = InventoryItem.Spyglass;
                string hash = new string((PickupPath + root.name).ToLowerInvariant().Where(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9').ToArray());
                root.GetComponent<NetworkObject>().SetAssetPathHash(hash.GetStableHashU64());
                PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath).GetComponent<NetworkFish>();
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            registry.AddObject(pickup.GetComponent<NetworkObject>(), true, true);
            EditorUtility.SetDirty(registry);
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            root = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var equipment = root.GetComponent<NetworkEquipment>();
                var models = equipment.Models;
                Array.Resize(ref models, Mathf.Max(models.Length, (int)InventoryItem.Spyglass - 13 + 1));
                models[(int)InventoryItem.Spyglass - 13] = visual;
                equipment.Models = models;
                var weapon = root.GetComponent<NetworkWeapon>();
                var drops = weapon.DropPrefabs;
                Array.Resize(ref drops, Mathf.Max(drops.Length, (int)InventoryItem.Spyglass + 1));
                drops[(int)InventoryItem.Spyglass] = pickup;
                weapon.DropPrefabs = drops;
                PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            root = PrefabUtility.LoadPrefabContents(shipPath);
            try
            {
                var equipment = root.GetComponent<ExperimentalShipEquipment>();
                if (!equipment.Prefabs.Contains(pickup)) equipment.Prefabs = equipment.Prefabs.Concat(new[] { pickup }).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, shipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var loose = catalog.LoosePrefabs;
            Array.Resize(ref loose, Mathf.Max(loose.Length, (int)InventoryItem.Spyglass + 1));
            loose[(int)InventoryItem.Spyglass] = pickup;
            catalog.LoosePrefabs = loose;
            if (!catalog.Items.Any(e => e != null && e.Item == InventoryItem.Spyglass))
                catalog.Items = catalog.Items.Concat(new[] { new LootCatalog.Entry { Item = InventoryItem.Spyglass, Name = "Подзорная труба", Weight = 4f } }).ToArray();
            EditorUtility.SetDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 98);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
}
