#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public sealed class UpperDestructionSettings : ScriptableObject
    {
        public const string Root = "Assets/Experimental/UpperDestructionV2";
        public const string AssetPath = Root + "/Settings.asset";
        public SessionConfig Session;
        public NetworkObject Ship;
        public SinglePrefabObjects Prefabs;

        public void Apply(SessionController controller)
        {
            if (Session == null || Session.ProtocolVersion != 199 || Ship == null || Prefabs == null)
                throw new System.InvalidOperationException("Upper destruction assets are incomplete.");
            controller.Config = Session;
            controller.ShipPrefab = Ship;
            var manager = controller.GetComponent<NetworkManager>();
            if (manager.SpawnablePrefabs == Prefabs) return;
            manager.SpawnablePrefabs = Prefabs;
            if (Application.isPlaying)
            {
                Prefabs.InitializePrefabRange(0);
            }
        }

        public static void SelectInEditor(SessionController controller)
        {
#if UNITY_EDITOR
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<UpperDestructionSettings>(AssetPath);
            if (settings == null) throw new System.InvalidOperationException("Prepare UpperDestructionV2 assets before starting Multiplayer.");
            settings.Apply(controller);
#endif
        }
    }
}
#endif
