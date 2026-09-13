using UnityEditor;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class ShipFloodingSetup
    {
        [MenuItem("PirateSlop/Configure Ship Flooding")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            const string path = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var destruction = root.GetComponent<ShipDestruction>();
                var flooding = root.GetComponent<ShipFlooding>();
                destruction.Profile.EnableFlooding = true;
                foreach (var definition in destruction.Profile.Sections)
                    definition.CanFlood = definition.Type == ShipSectionType.Hull;
                flooding.FullWaterline = 4.1f;
                flooding.FullBowPitch = 8f;
                flooding.FloodSecondsByHits = new Vector4(60f, 40f, 20f, 10f);
                flooding.DrainDuration = 30f;
                flooding.WaterlineAllowance = 3.5f;
                EditorUtility.SetDirty(destruction.Profile);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
                config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 80);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var repair = player.GetComponent<NetworkHullRepair>() ?? player.AddComponent<NetworkHullRepair>();
                repair.MalletModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Repair/SM_RepairMallet.prefab");
                var weapon = player.GetComponent<NetworkWeapon>();
                var drops = weapon.DropPrefabs;
                drops[(int)InventoryItem.Mallet] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Repair/DroppedMallet.prefab").GetComponent<NetworkFish>();
                weapon.DropPrefabs = drops;
                var icons = player.GetComponent<PlayerInventory>().Icons;
                icons.Icons[(int)InventoryItem.Mallet] = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Inventory/Mallet.png");
                EditorUtility.SetDirty(icons);
                var registry = AssetDatabase.LoadAssetAtPath<FishNet.Managing.Object.SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
                registry.AddObject(drops[(int)InventoryItem.Mallet].NetworkObject, true, true);
                EditorUtility.SetDirty(registry);
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
        }
    }
}
