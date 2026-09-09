using System;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class EquipmentSetup
    {
        const string Models = "Assets/Models/PirateEquipment/";
        static readonly string[] Names = { "JesusWhine", "SniperMusket", "PirateDoubleBarrel", "BombParrot" };
        public static void Configure()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Models + "PirateEquipment.mat");
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            var models = new GameObject[4];
            var pickups = new NetworkFish[4];
            for (int i = 0; i < Names.Length; i++)
            {
                string name = Names[i];
                string modelPath = "Assets/Prefabs/Props/PirateEquipment/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(modelPath);
                try
                {
                    foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx"), root.transform);
                    visual.name = "Visual";
                    var renderers = visual.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) { renderer.sharedMaterial = material; bounds.Encapsulate(renderer.bounds); }
                    var shape = root.GetComponent<BoxCollider>(); shape.center = bounds.center; shape.size = bounds.size;
                    PrefabUtility.SaveAsPrefabAsset(root, modelPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                string path = "Assets/Prefabs/Props/PirateEquipment/" + name + "Pickup.prefab";
                root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null ? path : "Assets/Prefabs/Networking/DroppedPistol.prefab");
                try
                {
                    foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    root.name = name + "Pickup";
                    var visual = UnityEngine.Object.Instantiate(models[i], root.transform);
                    var modelShape = visual.GetComponent<BoxCollider>();
                    var shape = root.GetComponent<BoxCollider>(); shape.center = modelShape.center; shape.size = modelShape.size;
                    UnityEngine.Object.DestroyImmediate(modelShape);
                    root.GetComponent<NetworkFish>().Item = (InventoryItem)(i + 13);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                pickups[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NetworkFish>();
                string hash = new string((path + pickups[i].name).ToLowerInvariant().Where(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9').ToArray());
                var networkObject = pickups[i].GetComponent<NetworkObject>();
                networkObject.SetAssetPathHash(hash.GetStableHashU64());
                EditorUtility.SetDirty(networkObject);
                registry.AddObject(networkObject, true, true);
                var importer = (TextureImporter)AssetImporter.GetAtPath(Models + name + "Icon.png");
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            }
            EditorUtility.SetDirty(registry);
            var drone = CreateDrone(registry);
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            NetworkFish[] drops;
            try
            {
                var equipment = player.GetComponent<NetworkEquipment>() ?? player.AddComponent<NetworkEquipment>();
                equipment.Models = models;
                equipment.ParrotPrefab = drone;
                var weapon = player.GetComponent<NetworkWeapon>(); drops = weapon.DropPrefabs;
                Array.Resize(ref drops, Mathf.Max(drops.Length, 17));
                for (int i = 0; i < 4; i++) drops[13 + i] = pickups[i];
                weapon.DropPrefabs = drops;
                var icons = player.GetComponent<PlayerInventory>().Icons;
                var images = icons.Icons; Array.Resize(ref images, Mathf.Max(images.Length, 17));
                for (int i = 0; i < 4; i++) images[13 + i] = AssetDatabase.LoadAssetAtPath<Texture2D>(Models + Names[i] + "Icon.png");
                icons.Icons = images; EditorUtility.SetDirty(icons);
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var ship = PrefabUtility.LoadPrefabContents(shipPath);
            try
            {
                var equipment = ship.GetComponent<ExperimentalShipEquipment>() ?? ship.AddComponent<ExperimentalShipEquipment>();
                equipment.Prefabs = pickups; equipment.Enabled = true;
                PrefabUtility.SaveAsPrefabAsset(ship, shipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(ship); }
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var entries = catalog.Items.ToList();
            for (int i = 0; i < 4; i++)
            {
                var item = (InventoryItem)(13 + i);
                if (!entries.Any(e => e.Item == item)) entries.Add(new LootCatalog.Entry { Item = item, Name = InventoryIcons.ItemName(item), Weight = 3 });
            }
            catalog.Items = entries.ToArray();
            var loose = catalog.LoosePrefabs; Array.Resize(ref loose, Mathf.Max(loose.Length, 17));
            for (int i = 0; i < 4; i++) loose[13 + i] = pickups[i];
            catalog.LoosePrefabs = loose; EditorUtility.SetDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 47); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static NetworkParrotDrone CreateDrone(SinglePrefabObjects registry)
        {
            const string path="Assets/Prefabs/Props/PirateEquipment/ParrotDrone.prefab";
            var root=PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null ? path : "Assets/Prefabs/Props/PirateEquipment/BombParrotPickup.prefab");
            try
            {
                root.name="ParrotDrone";
                var pickup=root.GetComponent<NetworkFish>();
                if(pickup!=null) UnityEngine.Object.DestroyImmediate(pickup);
                foreach(var collider in root.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                if(root.GetComponent<NetworkParrotDrone>()==null) root.AddComponent<NetworkParrotDrone>();
                root.transform.localScale=Vector3.one*.65f;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var networkObject=prefab.GetComponent<NetworkObject>();
            string hash=new string((path+prefab.name).ToLowerInvariant().Where(c=>c>='a' && c<='z' || c>='0' && c<='9').ToArray());
            networkObject.SetAssetPathHash(hash.GetStableHashU64()); EditorUtility.SetDirty(networkObject);
            registry.AddObject(networkObject,true,true); EditorUtility.SetDirty(registry);
            return prefab.GetComponent<NetworkParrotDrone>();
        }
    }
}
