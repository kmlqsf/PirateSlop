using System;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class StaffSetup
    {
        const string Folder = "Assets/Prefabs/Weapons/Staves/";
        static readonly InventoryItem[] Items = { InventoryItem.FireStaff, InventoryItem.LightningStaff, InventoryItem.IceStaff };

        [MenuItem("PirateSlop/Configure Staves")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory("Assets/Settings/Weapons");
            AssetDatabase.Refresh();
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset");
            var settings = new StaffSettings[3];
            var projectiles = new NetworkStaffProjectile[3];
            var pickups = new NetworkFish[3];
            var visuals = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var item = Items[i];
                string key = item.ToString();
                string path = "Assets/Settings/Weapons/" + key + ".asset";
                settings[i] = AssetDatabase.LoadAssetAtPath<StaffSettings>(path);
                if (settings[i] == null)
                {
                    settings[i] = ScriptableObject.CreateInstance<StaffSettings>();
                    settings[i].Item = item;
                    if (item == InventoryItem.LightningStaff) { settings[i].Color = new Color(.65f, .4f, 1f); settings[i].Cooldown = 3f; settings[i].Damage = 45f; settings[i].Range = 60f; settings[i].BlastRadius = 2.5f; }
                    if (item == InventoryItem.IceStaff) { settings[i].Color = new Color(.15f, .75f, 1f); settings[i].Damage = 2f; settings[i].BeamDamagePerSecond = 2f; settings[i].Range = 30f; settings[i].SlowMultiplier = .2f; settings[i].SlowSeconds = 1.25f; }
                    AssetDatabase.CreateAsset(settings[i], path);
                }
                visuals[i] = CreateVisual(settings[i]);
                pickups[i] = CreatePickup(item, visuals[i]);
                string pickupPath = Folder + key + "Pickup.prefab";
                HandMortarSetup.Register(pickups[i].GetComponent<NetworkObject>(), pickupPath, registry);
                if (item != InventoryItem.LightningStaff)
                {
                    projectiles[i] = CreateProjectile(settings[i]);
                    HandMortarSetup.Register(projectiles[i].GetComponent<NetworkObject>(), Folder + key + "Projectile.prefab", registry);
                }
                var loose = catalog.LoosePrefabs;
                Array.Resize(ref loose, Math.Max(loose.Length, 32));
                loose[(int)item] = pickups[i]; catalog.LoosePrefabs = loose;
                if (!catalog.Items.Any(e => e != null && e.Item == item)) catalog.Items = catalog.Items.Concat(new[] { new LootCatalog.Entry { Item = item, Name = InventoryIcons.ItemName(item), Weight = 5f } }).ToArray();
                string iconPath = "Assets/UI/Inventory/" + key + ".png";
                InventorySlotSetup.RenderIcon(visuals[i], iconPath);
                AssetDatabase.ImportAsset(iconPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
                var images = icons.Icons; Array.Resize(ref images, Math.Max(images.Length, 32));
                images[(int)item] = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath); icons.Icons = images;
            }
            HandMortarSetup.Edit("Assets/Prefabs/Networking/NetworkPlayer.prefab", root =>
            {
                var weapon = root.GetComponent<NetworkWeapon>();
                var drops = weapon.DropPrefabs; Array.Resize(ref drops, Math.Max(drops.Length, 32));
                var equipment = root.GetComponent<NetworkEquipment>();
                var models = equipment.Models; Array.Resize(ref models, Math.Max(models.Length, 19));
                for (int i = 0; i < 3; i++) { drops[(int)Items[i]] = pickups[i]; models[(int)Items[i] - 13] = visuals[i]; }
                weapon.DropPrefabs = drops; equipment.Models = models; equipment.Staves = settings; equipment.StaffProjectiles = projectiles;
            });
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Math.Max(config.ProtocolVersion, 163);
            foreach (var asset in new UnityEngine.Object[] { registry, catalog, icons, config }) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
            AssetDatabase.SaveAssets();
        }

        static Material Material(string name, Color color)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Part(GameObject root, string name, PrimitiveType type, Vector3 point, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name;
            part.transform.SetParent(root.transform, false); part.transform.localPosition = point; part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        static GameObject CreateVisual(StaffSettings settings)
        {
            if ((settings.Item >= InventoryItem.FireStaff && settings.Item <= InventoryItem.IceStaff) && AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Weapons/" + settings.Item + "/" + settings.Item + ".fbx") != null)
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + settings.Item + "Visual.prefab");
                if (existing != null) return existing;
            }
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(settings.Item + "Visual"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                Part(root, "Shaft", PrimitiveType.Cylinder, new Vector3(0, -.18f, 0), new Vector3(.055f, .65f, .055f), Material("StaffWood", new Color(.24f, .13f, .06f)));
                Part(root, "Crystal", PrimitiveType.Sphere, new Vector3(0, .57f, 0), Vector3.one * .2f, Material(settings.Item + "Crystal", settings.Color));
                var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(root.transform, false); muzzle.localPosition = new Vector3(0, .57f, 0);
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + settings.Item + "Visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static NetworkFish CreatePickup(InventoryItem item, GameObject visual)
        {
            if ((item >= InventoryItem.FireStaff && item <= InventoryItem.IceStaff) && AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Weapons/" + item + "/" + item + ".fbx") != null)
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + item + "Pickup.prefab");
                if (existing != null) return existing.GetComponent<NetworkFish>();
            }
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(item + "Pickup"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkFish>().Item = item;
                var model = UnityEngine.Object.Instantiate(visual, root.transform); model.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var shape = root.AddComponent<BoxCollider>(); shape.center = new Vector3(0, 0, -.08f); shape.size = new Vector3(.22f, .22f, 1.6f);
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + item + "Pickup.prefab").GetComponent<NetworkFish>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static NetworkStaffProjectile CreateProjectile(StaffSettings settings)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(settings.Item + "Projectile"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkStaffProjectile>().Settings = settings;
                Part(root, "Orb", PrimitiveType.Sphere, Vector3.zero, Vector3.one * settings.ProjectileRadius * 2f, Material(settings.Item + "Crystal", settings.Color));
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + settings.Item + "Projectile.prefab").GetComponent<NetworkStaffProjectile>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
