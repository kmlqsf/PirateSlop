using System;
using System.Linq;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class CannonInventorySetup
    {
        const string CannonPath = "Assets/Prefabs/Cannons/DeployableCannon.prefab";
        const string CratePath = "Assets/Prefabs/Cannons/CannonballCrate.prefab";
        const string KitPath = "Assets/Prefabs/Cannons/DisassembledCannon.prefab";
        const string PreviewPath = "Assets/Materials/Cannons/PlacementPreview.mat";

        [MenuItem("PirateSlop/Configure Cannon Inventory")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open SampleScene first.");
            CreateCannon();
            CreateModel(CratePath, "Assets/Models/CannonballCrate/SM_CannonballCrate.fbx", true);
            CreateModel(KitPath, "Assets/Models/DisassembledCannon/SM_DisassembledCannon.fbx", false);
            var material = AssetDatabase.LoadAssetAtPath<Material>(PreviewPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = 3000;
                AssetDatabase.CreateAsset(material, PreviewPath);
            }
            var ship = GameObject.Find("SM_PirateSloop");
            var player = GameObject.Find("PlayerCharacter");
            Undo.RegisterFullObjectHierarchyUndo(ship, "Configure cannon inventory");
            Undo.RegisterFullObjectHierarchyUndo(player, "Configure cannon inventory");
            ConfigureShip(ship);
            ConfigurePlayer(player, material);
            foreach (var path in new[] { "Assets/Prefabs/Networking/NetworkShip.prefab", "Assets/Prefabs/Networking/NetworkPlayer.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<ShipController>() != null) ConfigureShip(root);
                    else ConfigurePlayer(root, material);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 3);
            EditorUtility.SetDirty(config);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void Unpack(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        static void CreateCannon()
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cannons/CannonStation.prefab"));
            try
            {
                Unpack(root);
                foreach (var ball in root.GetComponentsInChildren<Cannonball>(true)) UnityEngine.Object.DestroyImmediate(ball.gameObject);
                root.name = "DeployableCannon";
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(root, CannonPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void CreateModel(string path, string modelPath, bool crate)
        {
            var root = new GameObject(crate ? "CannonballCrate" : "DisassembledCannon");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                Unpack(model);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.name.StartsWith("PreviewFloor") || renderer.name == "Cannonball_Pickup")
                    { UnityEngine.Object.DestroyImmediate(renderer.gameObject); continue; }
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var original = materials[i];
                        if (original == null) continue;
                        string materialPath = "Assets/Materials/Cannons/" + original.name + ".mat";
                        var saved = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (saved == null)
                        {
                            saved = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            if (original.HasProperty("_BaseColor")) saved.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                            else if (original.HasProperty("_Color")) saved.SetColor("_BaseColor", original.GetColor("_Color"));
                            saved.SetFloat("_Smoothness", .25f);
                            AssetDatabase.CreateAsset(saved, materialPath);
                        }
                        materials[i] = saved;
                    }
                    renderer.sharedMaterials = materials;
                }
                foreach (var camera in model.GetComponentsInChildren<Camera>()) UnityEngine.Object.DestroyImmediate(camera.gameObject);
                foreach (var light in model.GetComponentsInChildren<Light>()) UnityEngine.Object.DestroyImmediate(light.gameObject);
                if (crate)
                {
                    Box(root, new Vector3(0, .12f, 0), new Vector3(1.15f, .24f, 1.05f));
                    Box(root, new Vector3(-.57f, .4f, 0), new Vector3(.12f, .65f, 1.05f));
                    Box(root, new Vector3(.57f, .4f, 0), new Vector3(.12f, .65f, 1.05f));
                    Box(root, new Vector3(0, .4f, -.51f), new Vector3(1.15f, .65f, .12f));
                    Box(root, new Vector3(0, .4f, .51f), new Vector3(1.15f, .65f, .12f));
                    var spawn = new GameObject("CannonballSpawn").transform;
                    spawn.SetParent(root.transform, false); spawn.localPosition = new Vector3(0, .728f, 0);
                }
                else
                {
                    var renderers = root.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Box(root, bounds.center, bounds.size);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void Box(GameObject root, Vector3 center, Vector3 size)
        {
            var box = root.AddComponent<BoxCollider>(); box.center = center; box.size = size;
        }

        static void ConfigureShip(GameObject ship)
        {
            var existing = ship.GetComponentInChildren<CannonballCrate>(true);
            if (existing != null) { existing.CannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPath).GetComponent<SimpleCannon>(); return; }
            var oldCannon = ship.GetComponentInChildren<SimpleCannon>(true);
            var ball = ship.GetComponentInChildren<Cannonball>(true);
            if (oldCannon != null) Unpack(oldCannon.gameObject);
            var crateRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CratePath), ship.transform);
            crateRoot.transform.localPosition = new Vector3(-1.55f, 5.38f, -10.5f);
            crateRoot.transform.localRotation = Quaternion.identity;
            var crate = crateRoot.AddComponent<CannonballCrate>();
            crate.SpawnPoint = crateRoot.transform.Find("CannonballSpawn");
            crate.CannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPath).GetComponent<SimpleCannon>();
            if (ball == null) ball = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cannons/Cannonball.prefab").GetComponent<Cannonball>());
            ball.transform.SetParent(crate.SpawnPoint, false);
            ball.transform.localPosition = Vector3.zero; ball.transform.localRotation = Quaternion.identity;
            ball.Body.isKinematic = true; ball.gameObject.SetActive(true);
            crate.Supply = ball;
            if (oldCannon != null) UnityEngine.Object.DestroyImmediate(oldCannon.gameObject);
            var kit = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KitPath), ship.transform);
            kit.transform.localPosition = new Vector3(1.4f, 5.38f, -10.5f);
            kit.transform.localRotation = Quaternion.identity;
            kit.AddComponent<CannonPickup>().Crate = crate;
            crate.Kit = kit;
        }

        static void ConfigurePlayer(GameObject player, Material material)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null) inventory = player.AddComponent<PlayerInventory>();
            inventory.CannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPath).GetComponent<SimpleCannon>();
            inventory.PreviewMaterial = material;
        }
    }
}
