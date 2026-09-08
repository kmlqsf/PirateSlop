using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using FishNet.Object;
using FishNet.Managing.Object;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class CombatEquipmentSetup
    {
        const string PlayerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
        const string SabreModel = "Assets/Models/PirateWeapons/SM_PirateCutlass.fbx";
        const string PistolModel = "Assets/Models/PirateWeapons/SM_FlintlockPistol.fbx";
        static GameObject Model(string path, Transform parent)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    string materialPath = "Assets/Materials/PiratePistol/" + source.name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.SetColor("_BaseColor", source.color);
                        material.SetFloat("_Metallic", source.name.Contains("Steel") || source.name.Contains("Brass") || source.name.Contains("Edge") ? .7f : 0);
                        material.SetFloat("_Smoothness", .35f);
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    return material;
                }).ToArray();
            return model;
        }
        static NetworkFish Drop(string path, string modelPath, InventoryItem item)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = existing != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject(item.ToString() + "Drop");
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                foreach (var c in root.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                foreach (var r in root.GetComponents<Renderer>()) UnityEngine.Object.DestroyImmediate(r);
                foreach (var f in root.GetComponents<MeshFilter>()) UnityEngine.Object.DestroyImmediate(f);
                var visual = Model(modelPath, root.transform);
                visual.transform.localRotation = Quaternion.Euler(90, 0, 0) * visual.transform.localRotation;
                var renderers = root.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                var collider = root.AddComponent<BoxCollider>(); collider.center = root.transform.InverseTransformPoint(bounds.center); collider.size = bounds.size;
                if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
                var drop = root.GetComponent<NetworkFish>() ?? root.AddComponent<NetworkFish>(); drop.Item = item;
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<NetworkFish>();
            }
            finally { if (existing != null) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
        }
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode.");
            PirateWeaponSetup.Install();
            var player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var weapon = player.GetComponent<PirateWeapon>();
                foreach (var t in player.GetComponentsInChildren<Transform>(true).Where(t => t.name == "SabreWorldPivot" || t.name == "SabreViewPivot").ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
                var hand = player.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Hand.R");
                var camera = player.GetComponentInChildren<Camera>(true);
                var world = new GameObject("SabreWorldPivot").transform; world.SetParent(hand, false); world.rotation = player.transform.rotation;
                var view = new GameObject("SabreViewPivot").transform; view.SetParent(camera.transform, false); view.localPosition = new Vector3(.25f, -.30f, .46f); view.localRotation = Quaternion.Euler(-25, 0, -12);
                foreach (var pivot in new[] { world, view })
                {
                    var model = Model(SabreModel, pivot);
                    var grip = model.GetComponentsInChildren<Transform>().Single(t => t.name == "GripSocket_Cutlass");
                    model.transform.position += pivot.position - grip.position;
                    foreach (var r in model.GetComponentsInChildren<Renderer>()) if (pivot == view) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                weapon.SabreWorldPivot = world; weapon.SabreViewPivot = view;
                if (player.GetComponent<DeveloperMenu>() == null) player.AddComponent<DeveloperMenu>();
                var network = player.GetComponent<NetworkWeapon>();
                var drops = network.DropPrefabs;
                string pistolDropPath = AssetDatabase.GetAssetPath(drops[(int)InventoryItem.Pistol]);
                Array.Resize(ref drops, 8);
                drops[1] = Drop(pistolDropPath, PistolModel, InventoryItem.Pistol);
                drops[7] = Drop("Assets/Prefabs/Networking/DroppedSabre.prefab", SabreModel, InventoryItem.Sabre);
                network.DropPrefabs = drops;
                const string targetPath = "Assets/Prefabs/Networking/DeveloperTarget.prefab";
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule); dummy.name = "DeveloperTarget";
                dummy.AddComponent<NetworkObject>();
                var hp = dummy.AddComponent<CombatHealth>(); hp.MaxHealth = 100; hp.BarHeight = 1.3f; hp.RespawnDelay = float.MaxValue;
                dummy.AddComponent<NetworkHealth>(); dummy.AddComponent<DeveloperTarget>();
                var targetPrefab = PrefabUtility.SaveAsPrefabAsset(dummy, targetPath); UnityEngine.Object.DestroyImmediate(dummy);
                network.DeveloperTargetPrefab = targetPrefab.GetComponent<NetworkObject>();
                var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
                registry.AddObject(drops[7].NetworkObject, true, true); registry.AddObject(network.DeveloperTargetPrefab, true, true); EditorUtility.SetDirty(registry);
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
                var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset"); config.ProtocolVersion++; EditorUtility.SetDirty(config);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
        }
    }
}
