using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipLadderSetup
    {
        [MenuItem("PirateSlop/Configure Ship Ladders")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            const string path = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/LadderWood.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = new Color(.28f, .15f, .065f);
                material.SetFloat("_Smoothness", .15f); AssetDatabase.CreateAsset(material, "Assets/Materials/LadderWood.mat");
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old = root.transform.Find("ShipLadders"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var group = new GameObject("ShipLadders").transform; group.SetParent(root.transform, false);
                Ladder(group, "BoardingLadder", new Vector3(4.1f, -2, 0), 90, 6.55f, material);
                Ladder(group, "MastLadder", new Vector3(0, 3.35f, -5.35f), 180, 11.1f, material);
                Box(group, "LookoutFloor", new Vector3(0, 13.9f, -3.9f), new Vector3(2.6f, .2f, 2.6f), material, true);
                Box(group, "LookoutRailLeft", new Vector3(-1.23f, 14.65f, -3.9f), new Vector3(.12f, .14f, 2.6f), material, true);
                Box(group, "LookoutRailRight", new Vector3(1.23f, 14.65f, -3.9f), new Vector3(.12f, .14f, 2.6f), material, true);
                Box(group, "LookoutRailFront", new Vector3(0, 14.65f, -2.65f), new Vector3(2.6f, .14f, .12f), material, true);
                foreach (float x in new[] { -1.23f, 1.23f }) foreach (float z in new[] { -5.13f, -2.67f })
                    Box(group, "LookoutPost", new Vector3(x, 14.35f, z), new Vector3(.12f, .85f, .12f), material, true);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var config = AssetDatabase.LoadAssetAtPath<PirateSlop.Networking.SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 10); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        }
        static void Ladder(Transform parent, string name, Vector3 position, float yaw, float height, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.AddComponent<ShipLadder>().Height = height;
            foreach (float x in new[] { -.45f, .45f }) Box(go.transform, "Rail", new Vector3(x, height / 2, 0), new Vector3(.12f, height + .6f, .14f), material, false);
            for (float y = .15f; y < height; y += .32f) Box(go.transform, "Rung", new Vector3(0, y, .03f), new Vector3(.9f, .08f, .13f), material, false);
        }
        static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
