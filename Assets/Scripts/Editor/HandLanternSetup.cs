using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using PirateSlop.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.EditorTools
{
    public static class HandLanternSetup
    {
        const string ShipPath = "Assets/Resources/Ships/ShipV3Test.prefab";
        const string PlayerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
        const string Folder = "Assets/Models/Loot/HandLantern";
        const string PickupPath = "Assets/Prefabs/Networking/LanternPickup.prefab";
        const string PaneMeshPath = "Assets/Models/Ships/ShipV3/RuntimeMeshes/Lanterns/ShipLanternClearPanes.asset";
        public static string Status { get; private set; } = "Idle";

        [MenuItem("PirateSlop/Configure Hand Lantern")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Status = "Repairing ship lanterns";
            try
            {
                ShipV3ImportSetup.RepairLanternLighting();
                Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
                var visual = CreateVisual();
                var pickup = CreatePickup(visual);
                Status = "Binding player and ship";
                Edit(PlayerPath, root =>
                {
                    var weapon = root.GetComponent<NetworkWeapon>(); var drops = weapon.DropPrefabs;
                    Array.Resize(ref drops, Math.Max(drops.Length, (int)InventoryItem.Lantern + 1));
                    drops[(int)InventoryItem.Lantern] = pickup.GetComponent<NetworkFish>(); weapon.DropPrefabs = drops;
                    var equipment = root.GetComponent<NetworkEquipment>(); var models = equipment.Models;
                    Array.Resize(ref models, Math.Max(models.Length, (int)InventoryItem.Lantern - 13 + 1));
                    models[(int)InventoryItem.Lantern - 13] = visual; equipment.Models = models;
                });
                Edit(ShipPath, root => ConfigureImportedShip(root, pickup.GetComponent<NetworkFish>()));
                var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
                registry.AddObject(pickup.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(registry);
                var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
                config.ProtocolVersion = Math.Max(config.ProtocolVersion, 125); EditorUtility.SetDirty(config);
                var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset"); var loose = catalog.LoosePrefabs;
                Array.Resize(ref loose, Math.Max(loose.Length, (int)InventoryItem.Lantern + 1));
                loose[(int)InventoryItem.Lantern] = pickup.GetComponent<NetworkFish>(); catalog.LoosePrefabs = loose;
                if (!catalog.Items.Any(e => e != null && e.Item == InventoryItem.Lantern))
                    catalog.Items = catalog.Items.Concat(new[] { new LootCatalog.Entry { Item = InventoryItem.Lantern, Name = "Ручной фонарь", Weight = 0f } }).ToArray();
                EditorUtility.SetDirty(catalog);
                Status = "Creating inventory icon";
                const string iconPath = "Assets/UI/Inventory/Lantern.png";
                InventorySlotSetup.RenderIcon(visual, iconPath); AssetDatabase.ImportAsset(iconPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset"); var textures = icons.Icons;
                Array.Resize(ref textures, Math.Max(textures.Length, (int)InventoryItem.Lantern + 1));
                textures[(int)InventoryItem.Lantern] = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath); icons.Icons = textures; EditorUtility.SetDirty(icons);
                foreach (var asset in new UnityEngine.Object[] { registry, config, catalog, icons }) AssetDatabase.SaveAssetIfDirty(asset);
                Status = "Saved hand lantern, deck spawn, inventory and corrected ship panes";
            }
            catch (Exception exception) { Status = "Failed: " + exception.Message; Debug.LogException(exception); }
        }

        public static void RepairPaneMesh(ShipV3Lantern lamp)
        {
            if (lamp.Glass == null) return;
            var filter = lamp.Glass.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            var source = filter.sharedMesh;
            if (AssetDatabase.GetAssetPath(source) == PaneMeshPath) return;
            if (!source.name.StartsWith("V3_Lamp_", StringComparison.Ordinal) || !source.name.EndsWith("_Body", StringComparison.Ordinal)) return;
            var fixedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PaneMeshPath);
            if (fixedMesh == null)
            {
                int[] metal = source.GetTriangles(0), glass = source.GetTriangles(1);
                if (source.vertexCount != 572 || metal.Length != 389 * 3 || glass.Length != 59 * 3 ||
                    metal[27] != 24 || metal[39] != 36 || metal[603] != 287 || metal[606] != 288)
                    throw new InvalidOperationException("Lantern topology changed; pane repair needs review.");
                var frame = new List<int>(metal.Length - 12); var panes = new List<int>(glass);
                var moved = new HashSet<int> { 9, 13, 201, 202 };
                for (int i = 0; i < metal.Length; i += 3)
                {
                    var target = moved.Contains(i / 3) ? panes : frame;
                    target.Add(metal[i]); target.Add(metal[i + 1]); target.Add(metal[i + 2]);
                }
                fixedMesh = UnityEngine.Object.Instantiate(source); fixedMesh.name = "ShipLanternClearPanes";
                fixedMesh.SetTriangles(frame, 0); fixedMesh.SetTriangles(panes, 1);
                Directory.CreateDirectory(Path.GetDirectoryName(PaneMeshPath)); AssetDatabase.Refresh();
                AssetDatabase.CreateAsset(fixedMesh, PaneMeshPath);
            }
            filter.sharedMesh = fixedMesh;
        }

        static GameObject CreateVisual()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath).GetComponent<ShipV3Features>().Lanterns[0];
            var bodySource = source.Glass.GetComponent<MeshFilter>(); Vector3 grip = source.Glass.transform.parent.position;
            Matrix4x4 mapping = Matrix4x4.Scale(Vector3.one * .65f) * Matrix4x4.Translate(-grip) * bodySource.transform.localToWorldMatrix;
            var mesh = UnityEngine.Object.Instantiate(bodySource.sharedMesh); mesh.name = "HandLanternBody";
            mesh.vertices = mesh.vertices.Select(mapping.MultiplyPoint3x4).ToArray();
            var normalMatrix = mapping.inverse.transpose;
            mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
            mesh.tangents = mesh.tangents.Select(t =>
            {
                var v = mapping.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                return new Vector4(v.x, v.y, v.z, t.w * Mathf.Sign(mapping.determinant));
            }).ToArray();
            if (mapping.determinant < 0f)
                for (int slot = 0; slot < mesh.subMeshCount; slot++)
                {
                    var triangles = mesh.GetTriangles(slot);
                    for (int i = 0; i < triangles.Length; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                    mesh.SetTriangles(triangles, slot);
                }
            mesh.RecalculateBounds(); mesh = SaveMesh(mesh, Folder + "/HandLanternBody.asset");
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("HandLanternVisual");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var swing = new GameObject("Swing"); swing.transform.SetParent(root.transform, false);
                swing.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = swing.AddComponent<MeshRenderer>(); renderer.sharedMaterials = source.Glass.sharedMaterials;
                var lamp = new GameObject("LightSocket"); lamp.transform.SetParent(swing.transform, false);
                lamp.transform.localPosition = (source.Light.transform.position - grip) * .65f;
                var light = lamp.AddComponent<Light>(); EditorUtility.CopySerialized(source.Light, light); light.enabled = false;
                var data = lamp.AddComponent<UniversalAdditionalLightData>(); var serialized = new SerializedObject(data);
                serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var visual = root.AddComponent<HandLanternVisual>();
                visual.Swing = swing.transform; visual.Glass = renderer; visual.GlassSlot = source.GlassSlot; visual.Light = light;
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/HandLanternVisual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static GameObject CreatePickup(GameObject visual)
        {
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("LanternPickup");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkFish>().Item = InventoryItem.Lantern; root.AddComponent<NetworkLantern>();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var bounds = model.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                var shape = root.AddComponent<BoxCollider>(); shape.center = bounds.center; shape.size = bounds.size;
                root.AddComponent<BulletSurface>().Kind = BulletSurfaceKind.Metal;
                return PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void ConfigureImportedShip(GameObject root, NetworkFish pickup = null)
        {
            if (pickup == null) pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath)?.GetComponent<NetworkFish>();
            var stock = root.GetComponent<ExperimentalShipEquipment>();
            if (pickup == null || stock == null) return;
            var prefabs = stock.Prefabs.ToList(); var items = stock.Items.ToList(); var points = stock.SpawnPoints.ToList();
            int index = prefabs.FindIndex(p => p != null && p.Item == InventoryItem.Lantern);
            if (index < 0)
            {
                var point = root.transform.Find("Loot_Lantern");
                if (point == null) { point = new GameObject("Loot_Lantern").transform; point.SetParent(root.transform, false); }
                point.localPosition = new Vector3(1.1f, 4.11f, -1.3f);
                prefabs.Add(pickup); items.Add(InventoryItem.Lantern); points.Add(point);
            }
            else prefabs[index] = pickup;
            stock.Prefabs = prefabs.ToArray(); stock.Items = items.ToArray(); stock.SpawnPoints = points.ToArray();
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); return saved;
        }

        static void Edit(string path, Action<GameObject> action)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
