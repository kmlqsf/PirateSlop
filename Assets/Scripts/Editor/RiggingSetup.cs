using UnityEngine;
using UnityEditor;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class RiggingSetup
    {
        [MenuItem("PirateSlop/Configure Climbing Rigging")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) return;
            const string path = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Ships/ClimbingRigging/ClimbingRigging.fbx");
            if (model == null) throw new System.InvalidOperationException("ClimbingRigging.fbx is missing.");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var previous = root.transform.Find("ClimbingRigging");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                var rig = new GameObject("ClimbingRigging");
                rig.transform.SetParent(root.transform, false);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, rig.transform);
                var rope = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/Frigate/Materials/Frigate_Rope.mat");
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = rope;
                foreach (var ladder in root.GetComponentsInChildren<ShipLadder>(true))
                    if (ladder.name == "MastLadder") Object.DestroyImmediate(ladder.gameObject);
                var oldVisual = root.transform.Find("FrigateVisual/MastLadder");
                if (oldVisual != null) oldVisual.gameObject.SetActive(false);
                var brackets = root.transform.Find("FrigateVisual/LadderBrackets");
                if (brackets != null) brackets.gameObject.SetActive(false);
                foreach (int side in new[] { -1, 1 })
                {
                    var go = new GameObject(side < 0 ? "PortShrouds" : "StarboardShrouds");
                    go.transform.SetParent(rig.transform, false);
                    go.transform.localPosition = new Vector3(side * 6f, 4.3f, -4.3f);
                    go.transform.localRotation = Quaternion.Euler(0, side * 90f, 0);
                    var ladder = go.AddComponent<ShipLadder>();
                    ladder.RopeClimb = true; ladder.Height = 29f; ladder.Speed = 2f;
                    ladder.TopLean = -5.2f * 29f / 30f;
                    ladder.ExitPoint = go.transform.InverseTransformPoint(root.transform.TransformPoint(new Vector3(side * .55f, 33.3f, -3.1f)));
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 41);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
}
