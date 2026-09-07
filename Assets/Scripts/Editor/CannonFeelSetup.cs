using UnityEditor;
using UnityEngine;
using PirateSlop.Networking;
namespace PirateSlop.EditorTools
{
    public static class CannonFeelSetup
    {
        [MenuItem("PirateSlop/Configure Cannon Feel")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            foreach(var path in new[]{"Assets/Prefabs/Cannons/DeployableCannon.prefab","Assets/Prefabs/Cannons/CannonStation.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if(root.GetComponent<CannonCarriage>()==null) root.AddComponent<CannonCarriage>();
                    root.GetComponent<SimpleCannon>().LaunchSpeed=45f;
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var config=AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion=Mathf.Max(config.ProtocolVersion,24);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        }
    }
}
