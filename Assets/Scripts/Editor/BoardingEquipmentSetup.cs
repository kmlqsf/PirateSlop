using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PirateSlop.Networking;
using PirateSlop.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.EditorTools
{
    public static class BoardingEquipmentSetup
    {
        const string Folder = "Assets/Models/Loot/Replacement/";
        static readonly string[] keys = { "BoardingHarpoon", "WineBottle", "SpyglassTube" };
        static readonly BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        static readonly MethodInfo setTarget = typeof(EditorSceneManager).GetMethod("SetTargetSceneForNewGameObjects", flags, null, new[] { typeof(Scene) }, null);
        static readonly MethodInfo clearTarget = typeof(EditorSceneManager).GetMethod("ClearTargetSceneForNewGameObjects", flags);
        static readonly MethodInfo getTarget = typeof(EditorSceneManager).GetMethod("GetTargetSceneForNewGameObjects", flags);
        static void Status(string text) => File.WriteAllText("Tools/Art/BoardingEquipmentStatus.txt", text);
        static GameObject Visual(string key) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + key + "/" + key + "Visual.prefab");

        public static void ImportModels()
        {
            foreach (var key in keys)
            {
                string directory = Folder + key + "/";
                foreach (string suffix in new[] { "BaseColor.jpg", "Normal.png", "MetalSmooth.png" })
                {
                    var texture = (TextureImporter)AssetImporter.GetAtPath(directory + key + suffix);
                    texture.textureType = suffix == "Normal.png" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    texture.sRGBTexture = suffix == "BaseColor.jpg"; texture.isReadable = false;
                    texture.maxTextureSize = suffix == "BaseColor.jpg" ? 2048 : 1024;
                    texture.mipmapEnabled = true; texture.streamingMipmaps = true;
                    texture.textureCompression = TextureImporterCompression.CompressedHQ;
                    var platform = texture.GetPlatformTextureSettings("Standalone");
                    platform.overridden = true; platform.maxTextureSize = texture.maxTextureSize;
                    platform.format = suffix == "BaseColor.jpg" ? TextureImporterFormat.DXT1 : TextureImporterFormat.DXT5;
                    texture.SetPlatformTextureSettings(platform); texture.SaveAndReimport();
                }
                var importer = (ModelImporter)AssetImporter.GetAtPath(directory + key + ".fbx");
                importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.isReadable = true; importer.SaveAndReimport();
                string materialPath = directory + key + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
                material.SetColor("_BaseColor", Color.white); material.SetFloat("_Metallic", 1f); material.SetFloat("_Smoothness", 1f);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "BaseColor.jpg"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "Normal.png"));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "MetalSmooth.png"));
                material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                var scene = EditorSceneManager.NewPreviewScene();
                var root = new GameObject(key + "Visual"); SceneManager.MoveGameObjectToScene(root, scene);
                try
                {
                    var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(directory + key + ".fbx"), root.transform);
                    model.name = "Geometry";
                    foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
                    var points = Points(root); var before = Bounds(points);
                    int axis = before.size.x > before.size.y && before.size.x > before.size.z ? 0 : before.size.y > before.size.z ? 1 : 2;
                    var low = Bounds(points.Where(p => p[axis] < before.min[axis] + before.size[axis] * .12f).ToArray()).size;
                    var high = Bounds(points.Where(p => p[axis] > before.max[axis] - before.size[axis] * .12f).ToArray()).size;
                    float lowWidth = low[(axis + 1) % 3] + low[(axis + 2) % 3], highWidth = high[(axis + 1) % 3] + high[(axis + 2) % 3];
                    Vector3 direction = Vector3.zero;
                    direction[axis] = (key == "WineBottle" ? highWidth < lowWidth : highWidth > lowWidth) ? 1f : -1f;
                    Vector3 target = key == "WineBottle" ? Vector3.up : Vector3.forward;
                    model.transform.localRotation = Quaternion.FromToRotation(direction, target) * model.transform.localRotation;
                    var oriented = Bounds(Points(root));
                    float size = key == "WineBottle" ? .449f : key == "SpyglassTube" ? .627f : .72f;
                    model.transform.localScale *= size / (key == "WineBottle" ? oriented.size.y : oriented.size.z);
                    var scaled = Bounds(Points(root)); Vector3 offset = scaled.center;
                    if (key == "WineBottle") offset.y = scaled.min.y;
                    if (key == "BoardingHarpoon") offset.z = scaled.max.z;
                    model.transform.localPosition -= offset;
                    PrefabUtility.SaveAsPrefabAsset(root, directory + key + "Visual.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
                Status("Imported " + key);
            }
            AssetDatabase.SaveAssets(); Status("Three models and materials saved");
        }

        public static void BindItems()
        {
            Edit("Assets/Resources/BoardingHookVisual.prefab", root =>
            {
                root.transform.localScale = Vector3.one; Replace(root, Visual("BoardingHarpoon"), false);
                var box = root.GetComponent<BoxCollider>();
                if (box == null) box = root.AddComponent<BoxCollider>();
                var bounds = Bounds(Points(root)); box.center = bounds.center; box.size = Vector3.Max(bounds.size, Vector3.one * .16f);
            });
            BuildPair();
            Edit("Assets/Resources/BoardingHookAmmo.prefab", root =>
            {
                root.transform.localScale = Vector3.one;
                Replace(root, AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "BoardingHarpoon/BoardingHarpoonPairVisual.prefab"), false);
            });
            foreach (var path in new[] { "Assets/Prefabs/Props/PirateEquipment/JesusWhine.prefab", "Assets/Prefabs/Props/PirateEquipment/JesusWhinePickup.prefab" })
                Edit(path, root => Replace(root, Visual("WineBottle"), true));
            foreach (var path in new[] { "Assets/Prefabs/Props/PirateEquipment/Spyglass.prefab", "Assets/Prefabs/Props/PirateEquipment/SpyglassPickup.prefab" })
                Edit(path, root => Replace(root, Visual("SpyglassTube"), true));
            foreach (var path in new[] { "Assets/Prefabs/Cannons/Cannonball.prefab", "Assets/Prefabs/Cannons/FiredCannonball.prefab", "Assets/Prefabs/Networking/DroppedCannonball.prefab", "Assets/Prefabs/Loot/IslandCannonball.prefab", "Assets/Prefabs/Cannons/DeployableCannon.prefab", "Assets/Prefabs/Cannons/CannonStation.prefab" })
                Edit(path, BindAmmo);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 111); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets(); Status("Pickup, equipment and ammunition prefabs saved; protocol 111");
        }

        static void BuildPair()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("BoardingHarpoonPairVisual"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var model = UnityEngine.Object.Instantiate(Visual("BoardingHarpoon"), root.transform);
                    model.name = "Harpoon_" + i;
                    model.transform.localPosition = new Vector3(i == 0 ? -.12f : .12f, 0f, .36f);
                }
                var line = root.AddComponent<LineRenderer>();
                line.useWorldSpace = false; line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/HookRope.mat");
                line.widthMultiplier = .025f; line.positionCount = 12; line.generateLightingData = true;
                for (int i = 0; i < 12; i++)
                { float t = i / 11f; line.SetPosition(i, new Vector3(Mathf.Lerp(-.12f, .12f, t), -Mathf.Sin(t * Mathf.PI) * .04f, -.2f)); }
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "BoardingHarpoon/BoardingHarpoonPairVisual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void BindShip(string path)
        {
            Edit(path, ConfigureImportedShip);
            Status("Saved " + path);
        }

        public static void ConfigureImportedShip(GameObject root)
        {
            if (Visual("SpyglassTube") == null) return;
            BindAmmo(root);
            foreach (var spyglass in root.GetComponentsInChildren<ShipSpyglass>(true))
            {
                var model = spyglass.transform.Find("Spyglass");
                if (model != null) Replace(model.gameObject, Visual("SpyglassTube"), true);
            }
            var features = root.GetComponent<ShipV3Features>();
            if (features == null) return;
            if (features.BellGrip != null)
            {
                var capsule = features.BellGrip.GetComponent<CapsuleCollider>(); capsule.radius = .22f;
                capsule.height = Mathf.Max(.6f, capsule.height);
            }
            if (features.DispenserGrip != null) features.DispenserGrip.GetComponent<SphereCollider>().radius = .22f;
            var equipment = root.GetComponent<ExperimentalShipEquipment>();
            if (equipment == null || equipment.SpawnPoints == null || equipment.SpawnPoints.Length < 7) return;
            var items = equipment.Items.ToList(); var prefabs = equipment.Prefabs.ToList(); var markers = equipment.SpawnPoints.ToList();
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkPlayer.prefab");
            var drops = player.GetComponent<NetworkWeapon>().DropPrefabs;
            float middle = markers.Take(7).Average(p => p.localPosition.z);
            var deck = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.StartsWith("V3_Deck_", StringComparison.Ordinal)).Select(m =>
                Bounds(m.sharedMesh.vertices.Select((root.transform.worldToLocalMatrix * m.transform.localToWorldMatrix).MultiplyPoint3x4).ToArray())).ToArray();
            foreach (var item in new[] { InventoryItem.BoardingHook, InventoryItem.Wine, InventoryItem.Spyglass })
            {
                if (items.Contains(item)) continue;
                int index = items.Count; float x = index % 2 == 0 ? -2.6f : 2.6f;
                float z = middle + (index / 2 - 1.5f) * 1.65f;
                var surfaces = deck.Where(b => x >= b.min.x && x <= b.max.x && z >= b.min.z && z <= b.max.z).ToArray();
                if (surfaces.Length == 0) throw new InvalidOperationException("No deck under new equipment: " + item);
                var marker = new GameObject("Loot_" + item).transform; marker.SetParent(markers[0].parent, false);
                marker.localPosition = new Vector3(x, surfaces.Max(b => b.max.y), z);
                items.Add(item); prefabs.Add(drops[CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item]); markers.Add(marker);
            }
            equipment.Items = items.ToArray(); equipment.Prefabs = prefabs.ToArray(); equipment.SpawnPoints = markers.ToArray();
        }

        public static void BindAmmo(GameObject root)
        {
            var pair = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "BoardingHarpoon/BoardingHarpoonPairVisual.prefab");
            if (pair == null) return;
            foreach (var ball in root.GetComponentsInChildren<Cannonball>(true))
            {
                var models = ball.AmmoModels ?? Array.Empty<GameObject>(); Array.Resize(ref models, Mathf.Max(6, models.Length));
                models[5] = pair; ball.AmmoModels = models;
                if (ball.Ammo == InventoryItem.BoardingHook) ball.RefreshVisual();
            }
        }

        static void Replace(GameObject root, GameObject visual, bool align)
        {
            var previousBounds = Bounds(Points(root));
            var previous = root.transform.Find("EquipmentReplacementVisual");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
                UnityEngine.Object.DestroyImmediate(filter);
            }
            var model = UnityEngine.Object.Instantiate(visual, root.transform);
            model.name = "EquipmentReplacementVisual";
            if (align) model.transform.localPosition += previousBounds.center - Bounds(Points(root)).center;
            var box = root.GetComponent<BoxCollider>();
            if (box != null) { var bounds = Bounds(Points(root)); box.center = bounds.center; box.size = Vector3.Max(bounds.size, Vector3.one * .1f); }
        }

        public static void RenderIcons()
        {
            var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset");
            foreach (var item in new[] { InventoryItem.BoardingHook, InventoryItem.Wine, InventoryItem.Spyglass })
            {
                string key = item == InventoryItem.BoardingHook ? "BoardingHarpoon/BoardingHarpoonPair" : item == InventoryItem.Wine ? "WineBottle/WineBottle" : "SpyglassTube/SpyglassTube";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + key + "Visual.prefab");
                string path = "Assets/UI/Inventory/" + item + ".png";
                InventorySlotSetup.RenderIcon(model, path); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                icons.Icons[(int)item] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            EditorUtility.SetDirty(icons); AssetDatabase.SaveAssets(); Status("Three inventory icons saved");
        }

        static Vector3[] Points(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null)
            .SelectMany(m => m.sharedMesh.vertices.Select((root.transform.worldToLocalMatrix * m.transform.localToWorldMatrix).MultiplyPoint3x4)).ToArray();
        static Bounds Bounds(Vector3[] points)
        {
            if (points.Length == 0) throw new InvalidOperationException("Missing equipment geometry");
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point); return bounds;
        }
        static void Edit(string path, Action<GameObject> change)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            Scene old = (Scene)getTarget.Invoke(null, null); GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path); setTarget.Invoke(null, new object[] { root.scene });
                change(root); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                clearTarget.Invoke(null, null); if (old.IsValid() && old.isLoaded && EditorSceneManager.IsPreviewScene(old)) setTarget.Invoke(null, new object[] { old });
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
