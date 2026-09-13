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
                EditorUtility.SetDirty(destruction.Profile);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
                config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 74);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
