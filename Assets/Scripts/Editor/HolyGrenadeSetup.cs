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
    public static class HolyGrenadeSetup
    {
        const string Folder = "Assets/Models/HolyGrenade/";
        const string VisualPath = "Assets/Prefabs/Props/PirateEquipment/HolyGrenade.prefab";
        const string PickupPath = "Assets/Prefabs/Props/PirateEquipment/HolyGrenadePickup.prefab";
        [MenuItem("PirateSlop/Configure Holy Grenade")]
        public static void Configure()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing");
            var ivory = Material("HolyIvory", shader, new Color(.94f, .95f, .92f), .12f);
            var gold = Material("HolyGold", shader, new Color(.85f, .57f, .15f), .7f);
            var wickMaterial = Material("HolyWick", shader, new Color(.2f, .12f, .055f), 0);
            var ruby = Material("HolyRuby", shader, new Color(.55f, .02f, .035f), .25f);
            var emberMaterial = Material("HolyEmber", shader, new Color(1f, .33f, .025f), 0);
            emberMaterial.EnableKeyword("_EMISSION");
            emberMaterial.SetColor("_EmissionColor", new Color(5f, 1.2f, .05f));
            EditorUtility.SetDirty(emberMaterial);
            var lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            var lineMaterial = Material("HolyTrajectory", lineShader, new Color(1f, .88f, .45f), 0);
            var root = new GameObject("HolyGrenade");
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "HolyGrenade.fbx");
                if (asset == null) throw new InvalidOperationException("HolyGrenade.fbx is missing");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    string name = renderer.name;
                    renderer.sharedMaterial = name.Contains("Ivory") ? ivory : name.Contains("Wick") ? wickMaterial : name.Contains("Ruby") ? ruby : gold;
                }
                var fuse = root.AddComponent<HolyGrenadeFuse>();
                fuse.Wick = model.GetComponentsInChildren<Transform>().First(t => t.name == "FuseWick");
                var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ember.name = "FuseEmber";
                UnityEngine.Object.DestroyImmediate(ember.GetComponent<Collider>());
                ember.transform.SetParent(root.transform, false);
                ember.transform.localScale = Vector3.one * .018f;
                ember.GetComponent<Renderer>().sharedMaterial = emberMaterial;
                fuse.Ember = ember.transform;
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath) != null ? PickupPath : "Assets/Prefabs/Networking/DroppedPistol.prefab");
            try
            {
                root.name = "HolyGrenadePickup";
                root.transform.localScale = Vector3.one;
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
                var shape = root.GetComponent<BoxCollider>();
                shape.center = new Vector3(0, .06f, 0);
                shape.size = new Vector3(.28f, .4f, .28f);
                root.GetComponent<NetworkFish>().Item = InventoryItem.HolyGrenade;
                if (root.GetComponent<NetworkHolyGrenade>() == null) root.AddComponent<NetworkHolyGrenade>();
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
                var hands = root.GetComponent<NetworkHolyGrenadeHands>() ?? root.AddComponent<NetworkHolyGrenadeHands>();
                hands.Prefab = pickup.GetComponent<NetworkHolyGrenade>();
                hands.ArcMaterial = lineMaterial;
                var equipment = root.GetComponent<NetworkEquipment>();
                var models = equipment.Models;
                Array.Resize(ref models, Mathf.Max(models.Length, 9));
                models[8] = visual;
                equipment.Models = models;
                var weapon = root.GetComponent<NetworkWeapon>();
                var drops = weapon.DropPrefabs;
                Array.Resize(ref drops, Mathf.Max(drops.Length, 22));
                drops[21] = pickup;
                weapon.DropPrefabs = drops;
                PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
            root = PrefabUtility.LoadPrefabContents(shipPath);
            try
            {
                var equipment = root.GetComponent<ExperimentalShipEquipment>();
                if (!equipment.Prefabs.Contains(pickup)) equipment.Prefabs = equipment.Prefabs.Concat(new[] { pickup }).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, shipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            var entry = bank.Entries.FirstOrDefault(e => e.Cue == SoundCue.HolyFlash);
            if (entry == null)
            {
                entry = new GameAudioBank.Entry { Cue = SoundCue.HolyFlash };
                bank.Entries = bank.Entries.Concat(new[] { entry }).ToArray();
            }
            entry.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HolyGrenade/flashbang_explode1.wav") };
            entry.Volume = .8f;
            entry.Distance = 40f;
            EditorUtility.SetDirty(bank);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 92);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static Material Material(string name, Shader shader, Color color, float metallic)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .42f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
