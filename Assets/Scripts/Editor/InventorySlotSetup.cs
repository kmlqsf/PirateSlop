using System.IO;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class InventorySlotSetup
    {
        [MenuItem("PirateSlop/Configure Inventory Slots")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory("Assets/UI/Inventory"); AssetDatabase.Refresh();
            const string ballPath = "Assets/Prefabs/Networking/DroppedCannonball.prefab";
            var ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ballPath);
            if (ballPrefab == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cannons/Cannonball.prefab");
                var root = Visual(source); root.name = "DroppedCannonball";
                try
                {
                    var bounds = Bounds(root);
                    var box = root.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
                    root.AddComponent<NetworkObject>(); root.AddComponent<NetworkFish>().Item = InventoryItem.Cannonball;
                    ballPrefab = PrefabUtility.SaveAsPrefabAsset(root, ballPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            ConfigureCannonballPhysics();
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var weapon = player.GetComponent<NetworkWeapon>();
                var drops = weapon.DropPrefabs; System.Array.Resize(ref drops, 5);
                drops[4] = ballPrefab.GetComponent<NetworkFish>(); weapon.DropPrefabs = drops;
                const string iconsPath = "Assets/UI/Inventory/InventoryIcons.asset";
                var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>(iconsPath);
                if (icons == null) { icons = ScriptableObject.CreateInstance<InventoryIcons>(); AssetDatabase.CreateAsset(icons, iconsPath); }
                icons.Icons = new Texture2D[5];
                for (int i = 0; i < drops.Length; i++)
                {
                    string path = "Assets/UI/Inventory/" + (InventoryItem)i + ".png";
                    RenderIcon(drops[i].gameObject, path);
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                    icons.Icons[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                EditorUtility.SetDirty(icons); player.GetComponent<PlayerInventory>().Icons = icons;
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var prefabs = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            prefabs.AddObject(ballPrefab.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(prefabs);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 20); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        public static void ConfigureCannonballPhysics()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            const string path = "Assets/Prefabs/Networking/DroppedCannonball.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var bounds = Bounds(root);
                var box = root.GetComponent<BoxCollider>(); if (box != null) Object.DestroyImmediate(box);
                if (root.GetComponent<Cannonball>() == null) root.AddComponent<Cannonball>();
                var sphere = root.GetComponent<SphereCollider>(); sphere.center = root.transform.InverseTransformPoint(bounds.center);
                sphere.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                var body = root.GetComponent<Rigidbody>(); body.mass = 5; body.useGravity = true; body.isKinematic = false;
                body.linearDamping = .05f; body.angularDamping = .05f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                if (root.GetComponent<NetworkLooseCannonball>() == null) root.AddComponent<NetworkLooseCannonball>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 21); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static GameObject Visual(GameObject source)
        {
            if (source == null) throw new System.InvalidOperationException("Item model is missing.");
            var root = new GameObject("ItemVisual");
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;
                var part = new GameObject(filter.name); part.transform.SetParent(root.transform, false);
                part.transform.localPosition = Vector3.Scale(source.transform.InverseTransformPoint(filter.transform.position), source.transform.localScale);
                part.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * filter.transform.rotation;
                Vector3 scale = source.transform.lossyScale, size = filter.transform.lossyScale;
                part.transform.localScale = Vector3.Scale(new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z), source.transform.localScale);
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }

            return root;
        }
        static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new System.InvalidOperationException("Item has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        public static void RenderIcon(GameObject source, string path)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var root = Visual(source); preview.AddSingleGO(root);
                var bounds = Bounds(root); float radius = Mathf.Max(.1f, bounds.extents.magnitude);
                preview.camera.orthographic = true; preview.camera.orthographicSize = radius * 1.08f;
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = radius * 10 + 10;
                preview.camera.transform.position = bounds.center + new Vector3(1, .7f, -1).normalized * radius * 4;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = Color.clear;
                preview.lights[0].intensity = 1.6f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
                preview.lights[1].intensity = 1f; preview.ambientColor = new Color(.5f, .5f, .5f);
                preview.BeginStaticPreview(new Rect(0, 0, 128, 128)); preview.Render(true);
                var image = preview.EndStaticPreview();
                File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
    }
}
