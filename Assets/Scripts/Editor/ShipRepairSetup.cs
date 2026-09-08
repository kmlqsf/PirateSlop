using System;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipRepairSetup
    {
        const string Folder = "Assets/Models/Repair/";
        [MenuItem("PirateSlop/Configure Ship Repair")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory("Assets/Materials/Repair");
            Directory.CreateDirectory("Assets/Prefabs/Repair");
            AssetDatabase.Refresh();
            var names = new[] { "SM_RepairMallet", "SM_RepairPlank_1", "SM_RepairPlank_2", "SM_RepairPlank_3", "SM_HullBreach" };
            foreach (string name in names)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + name + ".fbx");
                if (importer == null) throw new InvalidOperationException("Repair model missing: " + name);
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            var mallet = Model("SM_RepairMallet");
            var planks = new[] { Model("SM_RepairPlank_1"), Model("SM_RepairPlank_2"), Model("SM_RepairPlank_3") };
            var breach = Model("SM_HullBreach");
            var droppedMallet = Dropped(mallet, "DroppedMallet", InventoryItem.Mallet);
            var droppedPlanks = planks.Select((p, i) => Dropped(p, "DroppedPlank" + (i + 1), InventoryItem.Plank)).ToArray();
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var equipment = player.GetComponent<NetworkWeapon>();
                var drops = equipment.DropPrefabs; Array.Resize(ref drops, 7);
                drops[5] = droppedMallet; drops[6] = droppedPlanks[0]; equipment.DropPrefabs = drops;
                var hands = player.GetComponent<MalletHands>(); if (hands == null) hands = player.AddComponent<MalletHands>();
                var camera = player.GetComponentInChildren<Camera>(true);
                var hand = player.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hand.R");
                hands.ViewPivot = Equip(player.transform, camera.transform, "MalletViewPivot", mallet, new Vector3(.28f, -.34f, .5f));
                hands.WorldPivot = Equip(player.transform, hand, "MalletWorldPivot", mallet, new Vector3(0, -.12f, 0));
                hands.WorldPivot.rotation = player.transform.rotation;
                foreach (var renderer in hands.ViewPivot.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var icons = player.GetComponent<PlayerInventory>().Icons;
                var textures = icons.Icons; Array.Resize(ref textures, 7);
                foreach (int index in new[] { 5, 6 })
                {
                    string path = "Assets/UI/Inventory/" + (InventoryItem)index + ".png";
                    InventorySlotSetup.RenderIcon(drops[index].gameObject, path);
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                    textures[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                icons.Icons = textures; EditorUtility.SetDirty(icons);
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var ship = PrefabUtility.LoadPrefabContents(shipPath);
            try
            {
                var repair = ship.GetComponent<ShipRepair>(); if (repair == null) repair = ship.AddComponent<ShipRepair>();
                repair.BreachPrefab = breach; repair.PatchPrefabs = planks;
                repair.StartingMallet = droppedMallet; repair.StartingPlank = droppedPlanks[0];
                PrefabUtility.SaveAsPrefabAsset(ship, shipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(ship); }
            var prefabs = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            foreach (var drop in droppedPlanks.Append(droppedMallet)) prefabs.AddObject(drop.GetComponent<NetworkObject>(), true, true);
            EditorUtility.SetDirty(prefabs);
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var entries = catalog.Items.ToList();
            if (!entries.Any(e => e.Item == InventoryItem.Plank)) entries.Add(new LootCatalog.Entry { Item = InventoryItem.Plank, Name = "Доска", Weight = 3 });
            if (!entries.Any(e => e.Item == InventoryItem.Mallet)) entries.Add(new LootCatalog.Entry { Item = InventoryItem.Mallet, Name = "Деревянная киянка", Weight = 1 });
            catalog.Items = entries.ToArray(); EditorUtility.SetDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 26); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static Transform Equip(Transform player, Transform parent, string name, GameObject model, Vector3 position)
        {
            foreach (var old in player.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false); pivot.localPosition = position;
            PrefabUtility.InstantiatePrefab(model, pivot);
            return pivot;
        }
        static GameObject Model(string name)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".fbx");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    {
                        string path = "Assets/Materials/Repair/" + original.name + ".mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null)
                        {
                            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            material.SetColor("_BaseColor", original.color);
                            material.SetFloat("_Smoothness", .16f);
                            material.SetFloat("_Metallic", original.name.Contains("Iron") ? .5f : 0f);
                            AssetDatabase.CreateAsset(material, path);
                        }
                        return material;
                    }).ToArray();
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Repair/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static NetworkFish Dropped(GameObject model, string name, InventoryItem item)
        {
            var root = new GameObject(name);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                var renderers = visual.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var box = root.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkFish>().Item = item;
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Repair/" + name + ".prefab").GetComponent<NetworkFish>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
