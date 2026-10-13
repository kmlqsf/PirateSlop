using System;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class LegendaryMortarSetup
    {
        const string Models = "Assets/Models/LegendaryMortar/";
        const string Mortar = "Assets/Prefabs/Cannons/BowMortar.prefab";
        const string Kit = "Assets/Prefabs/Networking/MortarKitPickup.prefab";

        static Material Material(string part)
        {
            string path = Models + part + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            foreach (var suffix in new[] { "BaseColor.jpeg", "Normal.png", "MetallicSmoothness.png" })
            {
                var importer = AssetImporter.GetAtPath(Models + "Textures/" + part + "_" + suffix) as TextureImporter;
                importer.sRGBTexture = suffix.StartsWith("BaseColor");
                if (suffix.StartsWith("Normal")) importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/" + part + "_BaseColor.jpeg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/" + part + "_Normal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/" + part + "_MetallicSmoothness.png"));
            material.SetFloat("_Metallic", 1f); material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform Node(string name, Transform parent, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false); node.localPosition = position;
            return node;
        }

        static Transform Mesh(string name, Transform parent, MeshFilter source, Material material, Vector3 position)
        {
            var node = Node(name, parent, position);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return node;
        }

        [MenuItem("PirateSlop/Configure Legendary Mortar Models")]
        public static void Configure()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "MortarAssembly.fbx");
            var parts = model.GetComponentsInChildren<MeshFilter>().ToDictionary(m => m.name);
            var root = PrefabUtility.LoadPrefabContents(Mortar);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                foreach (var collider in root.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                Mesh("MortarBase", root.transform, parts["MortarBase"], Material("Base"), Vector3.zero).localScale = new Vector3(.85f, 1f, .85f);
                var traverse = Node("TraversePivot", root.transform, Vector3.zero);
                var cradle = Mesh("MortarCradle", traverse, parts["MortarCradle"], Material("Cradle"), new Vector3(0, .18f, .2f));
                cradle.localScale = new Vector3(.7f, .55f, .55f);
                cradle.localRotation = Quaternion.identity;
                var pitch = Node("BarrelPivot", traverse, new Vector3(0, .9324f, -.22449f));
                pitch.localRotation = Quaternion.Euler(-3f, 0, 0);
                Mesh("MortarBarrel", pitch, parts["MortarBarrel"], Material("Barrel"), Vector3.zero).localScale = Vector3.one * .72f;
                var muzzle = Node("Muzzle", pitch, new Vector3(0, .033f, 1.444f) * .72f);
                var breech = Node("Breech", traverse, new Vector3(0, 1.1f, -1.8f));
                var baseCollider = root.AddComponent<BoxCollider>(); baseCollider.center = new Vector3(0, .22f, 0); baseCollider.size = new Vector3(1.7f, .44f, 1.7f);
                var supportCollider = cradle.gameObject.AddComponent<BoxCollider>(); supportCollider.center = parts["MortarCradle"].sharedMesh.bounds.center; supportCollider.size = parts["MortarCradle"].sharedMesh.bounds.size;
                var barrelCollider = pitch.gameObject.AddComponent<CapsuleCollider>(); barrelCollider.direction = 2; barrelCollider.center = new Vector3(0, .03f, .23f) * .72f; barrelCollider.height = 2.43f * .72f; barrelCollider.radius = .58f * .72f;
                var cannon = root.GetComponent<SimpleCannon>();
                cannon.TraversePivot = traverse; cannon.BarrelPivot = pitch; cannon.Muzzle = muzzle; cannon.Breech = breech;
                PrefabUtility.SaveAsPrefabAsset(root, Mortar);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/DroppedSabre.prefab"));
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "MortarKitPickup";
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                root.GetComponent<NetworkFish>().Item = InventoryItem.MortarKit;
                var kitMesh = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "MortarKit.fbx").GetComponentInChildren<MeshFilter>();
                Mesh("MortarKit", root.transform, kitMesh, Material("Kit"), Vector3.zero);
                var collider = root.GetComponent<BoxCollider>(); collider.center = kitMesh.sharedMesh.bounds.center; collider.size = kitMesh.sharedMesh.bounds.size;
                PrefabUtility.SaveAsPrefabAsset(root, Kit);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(Kit).GetComponent<NetworkFish>();
            var mortar = AssetDatabase.LoadAssetAtPath<GameObject>(Mortar).GetComponent<SimpleCannon>();
            foreach (var path in new[] { "Assets/Resources/Ships/ShipV3Test.prefab", "Assets/Prefabs/Networking/NetworkShip.prefab", "Assets/Prefabs/Networking/NetworkPlayer.prefab" })
            {
                root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var crate = root.GetComponentInChildren<CannonballCrate>(true);
                    if (crate != null) { crate.MortarKitPrefab = pickup; crate.MortarPrefab = mortar; }
                    var inventory = root.GetComponent<PlayerInventory>();
                    if (inventory != null) inventory.MortarPrefab = mortar;
                    var weapon = root.GetComponent<NetworkWeapon>();
                    if (weapon != null) { var drops = weapon.DropPrefabs; Array.Resize(ref drops, Math.Max(drops.Length, 33)); drops[32] = pickup; weapon.DropPrefabs = drops; }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            registry.AddObject(pickup.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(registry);
            var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset");
            var images = icons.Icons; Array.Resize(ref images, Math.Max(images.Length, 33));
            images[32] = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/RoguelikeUI/BowMortar.png"); icons.Icons = images; EditorUtility.SetDirty(icons);
            AssetDatabase.SaveAssets();
        }
    }
}
