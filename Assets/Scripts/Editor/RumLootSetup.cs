using System;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class RumLootSetup
    {
        const string Models = "Assets/Models/Loot/Kenney/";
        const string Prefabs = "Assets/Prefabs/Loot/";
        static Material palette;
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh();
            palette = AssetDatabase.LoadAssetAtPath<Material>(Models + "PiratePalette.mat");
            if (palette == null)
            {
                palette = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(palette, Models + "PiratePalette.mat");
            }
            palette.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/colormap.png");
            palette.SetFloat("_Smoothness", .28f);
            EditorUtility.SetDirty(palette);
            var rum = CreatePickup("RumBottle", "bottle", InventoryItem.Rum, new Vector3(.23f,.55f,.23f));
            var ball = CreatePickup("IslandCannonball", "cannon-ball", InventoryItem.Cannonball, new Vector3(.34f,.34f,.34f));
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            NetworkFish[] drops;
            try
            {
                var weapon = player.GetComponent<NetworkWeapon>();
                drops = weapon.DropPrefabs;
                Array.Resize(ref drops, 13);
                drops[12] = rum;
                weapon.DropPrefabs = drops;
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            catalog.LoosePrefabs = drops.ToArray();
            catalog.LoosePrefabs[4] = ball;
            catalog.LooseItemsPerIsland = new Vector2Int(2,4);
            catalog.Items = new[]
            {
                Entry(InventoryItem.Fish,44), Entry(InventoryItem.Cannonball,28),
                Entry(InventoryItem.Rum,12), Entry(InventoryItem.FireCannonball,4), Entry(InventoryItem.IceCannonball,3),
                Entry(InventoryItem.PushCannonball,3), Entry(InventoryItem.BoomerangCannonball,2),
                Entry(InventoryItem.Sabre,2), Entry(InventoryItem.Pistol,1), Entry(InventoryItem.Cannon,1)
            };
            EditorUtility.SetDirty(catalog);
            ConfigureShelf();
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 44);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static LootCatalog.Entry Entry(InventoryItem item, float weight) => new LootCatalog.Entry { Item=item, Name=InventoryIcons.ItemName(item), Weight=weight };
        static GameObject Model(string name, Transform parent, Vector3 size)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");
            var model = UnityEngine.Object.Instantiate(source, parent);
            model.name = name;
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var scale = new Vector3(size.x/bounds.size.x, size.y/bounds.size.y, size.z/bounds.size.z);
            model.transform.localScale = Vector3.Scale(model.transform.localScale,scale);
            model.transform.localPosition = -Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-parent.position, scale);
            foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(m=>palette).ToArray();
            return model;
        }
        static NetworkFish CreatePickup(string name, string source, InventoryItem item, Vector3 size)
        {
            string path = Prefabs + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null ? path : "Assets/Prefabs/Networking/DroppedPistol.prefab");
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                root.name = name;
                root.GetComponent<NetworkFish>().Item = item;
                Model(source, root.transform, size);
                var box = root.GetComponent<BoxCollider>();
                box.center = Vector3.up * size.y * .5f; box.size = size;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NetworkFish>();
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            var networkObject = prefab.GetComponent<NetworkObject>();
            string hashSource = (path + prefab.gameObject.name).Trim().ToLowerInvariant();
            hashSource = new string(hashSource.Where(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')).ToArray());
            networkObject.SetAssetPathHash(hashSource.GetStableHashU64());
            EditorUtility.SetDirty(networkObject);
            registry.AddObject(networkObject, true, true);
            EditorUtility.SetDirty(registry);
            return prefab;
        }
        static void ConfigureShelf()
        {
            const string path = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old = root.transform.Find("RumShelf");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var shelf = new GameObject("RumShelf");
                shelf.transform.SetParent(root.transform, false);
                var component = shelf.AddComponent<RumShelf>();
                component.Bottles = new GameObject[NetworkShip.RumCapacity];
                for (int row = 0; row < 4; row++)
                {
                    var plank = Model("platform-planks",shelf.transform,new Vector3(1.9f,.1f,.55f));
                    plank.transform.localPosition += Vector3.up * (row * .62f - .85f);
                    var collider = plank.AddComponent<BoxCollider>();
                    var mesh = plank.GetComponentInChildren<MeshFilter>();
                    collider.center = mesh.sharedMesh.bounds.center;
                    collider.size = mesh.sharedMesh.bounds.size;
                    for (int column = 0; column < 3; column++)
                    {
                        int index = row * 3 + column;
                        var bottle = Model("bottle",shelf.transform,new Vector3(.23f,.5f,.23f));
                        bottle.transform.localPosition += new Vector3((column-1)*.58f,row*.62f-.75f,0);
                        bottle.name = "RespawnBottle" + (index + 1);
                        bottle.SetActive(index < 3);
                        component.Bottles[index] = bottle;
                    }
                }
                var hitbox = shelf.AddComponent<BoxCollider>();
                hitbox.center = new Vector3(0,.35f,0); hitbox.size = new Vector3(1.95f,2.4f,.6f);
                shelf.transform.localPosition = new Vector3(4.4f,2.05f,1.2f);
                shelf.transform.localRotation = Quaternion.Euler(0,90,0);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
