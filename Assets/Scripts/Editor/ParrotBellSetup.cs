using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class ParrotBellSetup
    {
        [MenuItem("PirateSlop/Configure Parrot Flight And Bell Rope")]
        public static void Configure()
        {
            const string path = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var bell = root.transform.Find("CrewBell");
                if (bell == null) throw new System.InvalidOperationException("CrewBell is missing");
                if (bell.GetComponent<CrewBellMotion>() == null) bell.gameObject.AddComponent<CrewBellMotion>();
                var body = bell.GetComponent<BoxCollider>();
                body.center = new Vector3(0, .06f, 0); body.size = new Vector3(.52f, .48f, .54f);
                var grip = bell.Find("BellRopeGrip");
                if (grip == null) { grip = new GameObject("BellRopeGrip").transform; grip.SetParent(bell, false); }
                grip.localPosition = new Vector3(0, -.4f, 0);
                var collider = grip.GetComponent<BoxCollider>();
                if (collider == null) collider = grip.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(.19f, .38f, .19f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var config = AssetDatabase.LoadAssetAtPath<Networking.SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 91);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
}
