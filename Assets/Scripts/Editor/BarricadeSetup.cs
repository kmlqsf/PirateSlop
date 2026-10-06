using System;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class BarricadeSetup
    {
        const string Models = "Assets/Models/Barricade/";
        const string Prefabs = "Assets/Prefabs/Barricades/";
        const string Player = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
        const string Ship = "Assets/Resources/Ships/ShipV3Test.prefab";
        [MenuItem("PirateSlop/Configure Barricade")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh();
            var material = ConfigureMaterial();
            ImportModel("Barricade"); ImportModel("BarricadeFragments");
            var visual = CreateVisual("Barricade", "BarricadeVisual", material);
            var fragments = CreateVisual("BarricadeFragments", "BarricadeFragmentsVisual", material);
            var placed = CreatePlaced(visual, fragments);
            var pickup = CreatePickup(visual);
            var construction = AssetDatabase.LoadAssetAtPath<Material>(Models + "BarricadeConstruction.mat");
            if (construction == null)
            {
                construction = new Material(Shader.Find("PirateSlop/Barricade Construction"));
                AssetDatabase.CreateAsset(construction, Models + "BarricadeConstruction.mat");
            }
            construction.CopyPropertiesFromMaterial(material);
            construction.renderQueue = 3000;
            construction.EnableKeyword("_NORMALMAP"); construction.EnableKeyword("_METALLICSPECGLOSSMAP");
            construction.SetFloat("_ConstructionProgress", 0f);
            construction.SetFloat("_ConstructionHeight", 2.1f);
            construction.SetFloat("_ConstructionFeather", .045f);
            EditorUtility.SetDirty(construction);
            Edit(Player, root =>
            {
                var inventory = root.GetComponent<PlayerInventory>();
                inventory.BarricadeVisual = visual; inventory.BarricadeConstructionMaterial = construction;
                var weapon = root.GetComponent<NetworkWeapon>(); weapon.BarricadePrefab = placed.GetComponent<NetworkBarricade>();
                var drops = weapon.DropPrefabs; Array.Resize(ref drops, Math.Max(drops.Length, (int)InventoryItem.Barricade + 1));
                drops[(int)InventoryItem.Barricade] = pickup.GetComponent<NetworkFish>(); weapon.DropPrefabs = drops;
            });
            Edit(Ship, root => ConfigureImportedShip(root, pickup.GetComponent<NetworkFish>()));
            var network = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            network.AddObject(placed.GetComponent<NetworkObject>(), true, true);
            network.AddObject(pickup.GetComponent<NetworkObject>(), true, true);
            EditorUtility.SetDirty(network);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Math.Max(config.ProtocolVersion, 122); EditorUtility.SetDirty(config);
            ConfigureAudio(); ConfigureIcon(pickup);
            AssetDatabase.SaveAssets();
        }
        static void ImportModel(string name)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Models + name + ".fbx");
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true; importer.globalScale = 1f;
            importer.SaveAndReimport();
        }
        static Material ConfigureMaterial()
        {
            foreach (var suffix in new[] { "BaseColor.jpg", "Normal.png", "MetalSmooth.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Models + "Textures/Barricade" + suffix);
                importer.textureType = suffix == "Normal.png" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = suffix == "BaseColor.jpg";
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = true; importer.streamingMipmaps = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Models + "Barricade.mat");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, Models + "Barricade.mat"); }
            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/BarricadeBaseColor.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/BarricadeNormal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/BarricadeMetalSmooth.png"));
            material.SetFloat("_Smoothness", .45f); material.SetFloat("_Metallic", 1f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }
        static GameObject CreateVisual(string model, string name, Material material)
        {
            var root = new GameObject(name);
            try
            {
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx"), root.transform);
                PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                { renderer.enabled = true; renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray(); }
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
                return PrefabUtility.SaveAsPrefabAsset(root, Models + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static GameObject CreatePlaced(GameObject visual, GameObject fragments)
        {
            var root = new GameObject("Barricade");
            try
            {
                root.AddComponent<NetworkObject>();
                var network = root.AddComponent<NetworkBarricade>(); network.Fragments = fragments;
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                { var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh; collider.convex = false; }
                root.AddComponent<BulletSurface>().Kind = BulletSurfaceKind.Wood;
                return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "Barricade.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static GameObject CreatePickup(GameObject visual)
        {
            var root = new GameObject("BarricadePickup");
            try
            {
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkFish>().Item = InventoryItem.Barricade;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform); model.transform.localScale = Vector3.one * .5f;
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var shape = root.AddComponent<BoxCollider>(); shape.center = new Vector3(0, .525f, 0); shape.size = new Vector3(.7f, 1.05f, .34f);
                root.AddComponent<BulletSurface>().Kind = BulletSurfaceKind.Wood;
                return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "BarricadePickup.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        public static void ConfigureImportedShip(GameObject root, NetworkFish pickup = null)
        {
            if (pickup == null) pickup = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "BarricadePickup.prefab")?.GetComponent<NetworkFish>();
            var stock = root.GetComponent<ExperimentalShipEquipment>();
            if (pickup == null || stock == null) return;
            var prefabs = stock.Prefabs.ToList(); var items = stock.Items.ToList(); var points = stock.SpawnPoints.ToList();
            int index = prefabs.FindIndex(p => p != null && p.Item == InventoryItem.Barricade);
            if (index < 0)
            {
                var point = root.transform.Find("Loot_Barricade");
                if (point == null) { point = new GameObject("Loot_Barricade").transform; point.SetParent(root.transform, false); }
                point.localPosition = new Vector3(-1.1f, 4.11f, -1.3f);
                prefabs.Add(pickup); items.Add(InventoryItem.Barricade); points.Add(point);
            }
            else prefabs[index] = pickup;
            stock.Prefabs = prefabs.ToArray(); stock.Items = items.ToArray(); stock.SpawnPoints = points.ToArray();
        }
        static void ConfigureAudio()
        {
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            var entries = bank.Entries.ToList(); var entry = entries.FirstOrDefault(e => e.Cue == SoundCue.BarricadeBreak);
            if (entry == null) { entry = new GameAudioBank.Entry { Cue = SoundCue.BarricadeBreak }; entries.Add(entry); }
            entry.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Naval/cannon_hit_ship_short.ogg") };
            entry.Volume = .7f; entry.Distance = 55f;
            bank.Entries = entries.ToArray(); EditorUtility.SetDirty(bank);
        }
        static void ConfigureIcon(GameObject pickup)
        {
            const string path = "Assets/UI/Inventory/Barricade.png";
            InventorySlotSetup.RenderIcon(pickup, path); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset");
            var textures = icons.Icons; Array.Resize(ref textures, Math.Max(textures.Length, (int)InventoryItem.Barricade + 1));
            textures[(int)InventoryItem.Barricade] = AssetDatabase.LoadAssetAtPath<Texture2D>(path); icons.Icons = textures;
            EditorUtility.SetDirty(icons);
        }
        static void Edit(string path, Action<GameObject> action)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
