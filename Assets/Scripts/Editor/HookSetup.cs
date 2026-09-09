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
    public static class HookSetup
    {
        const string Folder = "Assets/Models/Hooks/";
        public static void Configure()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory("Assets/Resources");
            byte[] source = File.ReadAllBytes("Art/ThirdParty/Hook/Anchor.glb");
            if (BitConverter.ToUInt32(source, 0) != 0x46546c67) throw new InvalidDataException("Invalid source GLB");
            int start = 28 + BitConverter.ToInt32(source, 12);
            File.WriteAllBytes(Folder + "AnchorAtlas.png", source.Skip(start).Take(9014).ToArray());
            AssetDatabase.ImportAsset(Folder + "AnchorAtlas.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Anchor.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Folder + "Anchor.mat"); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "AnchorAtlas.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", .4f); material.SetFloat("_Smoothness", .22f);
            EditorUtility.SetDirty(material);
            var vertices = new Vector3[583]; var normals = new Vector3[583]; var uv = new Vector2[583];
            var orientation = Quaternion.Euler(-90, 0, 0);
            for (int i = 0; i < vertices.Length; i++)
            {
                int p = start + 12280 + i * 12, n = start + 19276 + i * 12, t = start + 35600 + i * 8;
                vertices[i] = orientation * new Vector3(BitConverter.ToSingle(source, p), BitConverter.ToSingle(source, p + 4), -BitConverter.ToSingle(source, p + 8)) * 100;
                normals[i] = orientation * new Vector3(BitConverter.ToSingle(source, n), BitConverter.ToSingle(source, n + 4), -BitConverter.ToSingle(source, n + 8));
                uv[i] = new Vector2(BitConverter.ToSingle(source, t), 1 - BitConverter.ToSingle(source, t + 4));
            }
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(vertex);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            for (int i = 0; i < vertices.Length; i++) vertices[i] = (vertices[i] - bounds.center) / size;
            var triangles = new int[1632];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                triangles[i] = BitConverter.ToUInt16(source, start + 9016 + i * 2);
                triangles[i + 1] = BitConverter.ToUInt16(source, start + 9016 + (i + 2) * 2);
                triangles[i + 2] = BitConverter.ToUInt16(source, start + 9016 + (i + 1) * 2);
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "Anchor.asset");
            if (mesh == null) { mesh = new Mesh { name = "QuaterniusAnchor" }; AssetDatabase.CreateAsset(mesh, Folder + "Anchor.asset"); }
            mesh.Clear(); mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var hand = Model("GrappleHookModel", mesh, material, .5f, false);
            var ammo = Model("BoardingHookAmmo", mesh, material, .32f, false);
            Model("BoardingHookVisual", mesh, material, .75f, true);
            var rope = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/HookRope.mat");
            if (rope == null) { rope = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(rope, "Assets/Resources/HookRope.mat"); }
            rope.SetColor("_BaseColor", new Color(.53f, .36f, .17f)); rope.SetFloat("_Smoothness", 0); EditorUtility.SetDirty(rope);
            const string pickupPath = "Assets/Prefabs/Props/PirateEquipment/GrapplingHookPickup.prefab";
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(pickupPath) != null ? pickupPath : "Assets/Prefabs/Networking/DroppedPistol.prefab");
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                root.name = "GrapplingHookPickup";
                root.transform.localScale = Vector3.one;
                UnityEngine.Object.Instantiate(hand, root.transform);
                var shape = root.GetComponent<BoxCollider>(); shape.center = Vector3.zero; shape.size = new Vector3(.46f, .5f, .15f);
                root.GetComponent<NetworkFish>().Item = InventoryItem.GrapplingHook;
                PrefabUtility.SaveAsPrefabAsset(root, pickupPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(pickupPath).GetComponent<NetworkFish>();
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            var networkObject = pickup.GetComponent<NetworkObject>();
            string hash = new string((pickupPath + pickup.name).ToLowerInvariant().Where(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9').ToArray());
            networkObject.SetAssetPathHash(hash.GetStableHashU64()); EditorUtility.SetDirty(networkObject);
            registry.AddObject(networkObject, true, true); EditorUtility.SetDirty(registry);
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var equipment = player.GetComponent<NetworkEquipment>();
                var models = equipment.Models; Array.Resize(ref models, Mathf.Max(models.Length, 5)); models[4] = hand; equipment.Models = models;
                var weapon = player.GetComponent<NetworkWeapon>();
                var drops = weapon.DropPrefabs; Array.Resize(ref drops, Mathf.Max(drops.Length, 19)); drops[17] = pickup; weapon.DropPrefabs = drops;
                if (drops[4] != null) ConfigureBallAsset(AssetDatabase.GetAssetPath(drops[4]), ammo);
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var ship = PrefabUtility.LoadPrefabContents(shipPath);
            try
            {
                var equipment = ship.GetComponent<ExperimentalShipEquipment>();
                if (!equipment.Prefabs.Contains(pickup)) equipment.Prefabs = equipment.Prefabs.Concat(new[] { pickup }).ToArray();
                var crate = ship.GetComponentInChildren<CannonballCrate>(true);
                SetAmmo(crate.Supply, ammo);
                ConfigureBallAsset(AssetDatabase.GetAssetPath(crate.SpecialSupplyPrefab), ammo);
                PrefabUtility.SaveAsPrefabAsset(ship, shipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(ship); }
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 48); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static GameObject Model(string name, Mesh mesh, Material material, float size, bool collider)
        {
            var root = new GameObject(name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                root.transform.localScale = Vector3.one * size;
                if (collider) root.AddComponent<BoxCollider>().size = new Vector3(.9f, 1, .28f);
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void SetAmmo(Cannonball ball, GameObject model)
        {
            var models = ball.AmmoModels; Array.Resize(ref models, Mathf.Max(models.Length, 6)); models[5] = model; ball.AmmoModels = models;
        }
        static void ConfigureBallAsset(string path, GameObject model)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { SetAmmo(root.GetComponent<Cannonball>(), model); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
