using System.IO;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class LootSetup
    {
        [MenuItem("PirateSlop/Configure Island Loot")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory("Assets/Settings/Loot");
            AssetDatabase.Refresh();
            const string catalogPath = "Assets/Settings/Loot/DefaultLoot.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>(catalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<LootCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            const string prefabPath = "Assets/Prefabs/Networking/NetworkLootChest.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Fishing/RodWood.mat");
                var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Fishing/ReelBrass.mat");
                var root = new GameObject("NetworkLootChest");
                try
                {
                    root.AddComponent<NetworkObject>();
                    var chest = root.AddComponent<NetworkLootChest>(); chest.Catalog = catalog;
                    var box = root.AddComponent<BoxCollider>(); box.center = new Vector3(0, .4f, 0); box.size = new Vector3(1.2f, .8f, .8f);
                    Part(root.transform, "Bottom", new Vector3(0, .08f, 0), new Vector3(1.2f, .16f, .8f), wood);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Part(root.transform, "Side", new Vector3(side * .54f, .4f, 0), new Vector3(.12f, .6f, .8f), wood);
                        Part(root.transform, "Wall", new Vector3(0, .4f, side * .34f), new Vector3(1.2f, .6f, .12f), wood);
                        Part(root.transform, "Band", new Vector3(side * .38f, .4f, -.405f), new Vector3(.09f, .62f, .035f), metal);
                    }
                    var lid = new GameObject("Lid").transform; lid.SetParent(root.transform, false); lid.localPosition = new Vector3(0, .72f, .4f);
                    Part(lid, "LidPanel", new Vector3(0, .06f, -.4f), new Vector3(1.24f, .12f, .84f), wood);
                    foreach (float side in new[] { -1f, 1f })
                        Part(lid, "LidBand", new Vector3(side * .38f, .13f, -.4f), new Vector3(.09f, .025f, .85f), metal);
                    Part(lid, "Latch", new Vector3(0, -.04f, -.83f), new Vector3(.15f, .2f, .04f), metal);
                    chest.Lid = lid;
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            catalog.ChestPrefab = prefab.GetComponent<NetworkLootChest>(); EditorUtility.SetDirty(catalog);
            var prefabs = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            prefabs.AddObject(prefab.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(prefabs);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.Loot = catalog; config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 19); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
