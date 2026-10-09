using System;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ExplosiveBallSetup
    {
        const string Folder = "Assets/Models/ExplosiveBall/";
        const string PickupPath = "Assets/Prefabs/Networking/ExplosiveBallPickup.prefab";
        const string ProjectilePath = "Assets/Prefabs/Networking/ExplosiveBallProjectile.prefab";

        [MenuItem("PirateSlop/Configure Explosive Ball")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            AssetDatabase.Refresh();
            const string settingsPath = "Assets/Settings/Weapons/ExplosiveBall.asset";
            var settings = AssetDatabase.LoadAssetAtPath<HandMortarSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<HandMortarSettings>();
                settings.LaunchSpeed = NetworkFishProjectile.PufferSpeed; settings.FuseSeconds = NetworkFishProjectile.PufferFuse;
                settings.BallRadius = .12f; settings.Restitution = settings.TangentialRetention = NetworkFishProjectile.PufferBounce;
                settings.BlastRadius = NetworkFishProjectile.PufferRadius; settings.BlastDamage = 40f;
                settings.ExplodeOnLivingHit = false; settings.ExplodeOnWaterEntry = true; settings.BlastFalloff = false; settings.OwnerGraceSeconds = .3f;
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            var visual = CreateVisual();
            var pickup = CreateNetworkPrefab(visual, PickupPath, false);
            var projectile = CreateNetworkPrefab(visual, ProjectilePath, true);
            HandMortarSetup.Edit("Assets/Prefabs/Networking/NetworkPlayer.prefab", root =>
            {
                var weapon = root.GetComponent<NetworkWeapon>(); var drops = weapon.DropPrefabs;
                Array.Resize(ref drops, Math.Max(drops.Length, 29)); drops[28] = pickup.GetComponent<NetworkFish>(); weapon.DropPrefabs = drops;
                var equipment = root.GetComponent<NetworkEquipment>(); var models = equipment.Models;
                Array.Resize(ref models, Math.Max(models.Length, 16)); models[15] = visual; equipment.Models = models;
                equipment.ExplosiveBallSettings = settings; equipment.ExplosiveBallPrefab = projectile.GetComponent<NetworkHandMortarBall>();
            });
            HandMortarSetup.Edit("Assets/Resources/Ships/ShipV3Test.prefab", root => ConfigureImportedShip(root, pickup.GetComponent<NetworkFish>()));
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            HandMortarSetup.Register(pickup.GetComponent<NetworkObject>(), PickupPath, registry); HandMortarSetup.Register(projectile.GetComponent<NetworkObject>(), ProjectilePath, registry);
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var loose = catalog.LoosePrefabs; Array.Resize(ref loose, Math.Max(loose.Length, 29)); loose[28] = pickup.GetComponent<NetworkFish>(); catalog.LoosePrefabs = loose;
            if (!catalog.Items.Any(e => e != null && e.Item == InventoryItem.ExplosiveBall)) catalog.Items = catalog.Items.Concat(new[] { new LootCatalog.Entry { Item = InventoryItem.ExplosiveBall, Name = "Взрывное ядро", Weight = 10f } }).ToArray();
            const string iconPath = "Assets/UI/Inventory/ExplosiveBall.png";
            InventorySlotSetup.RenderIcon(visual, iconPath); AssetDatabase.ImportAsset(iconPath);
            var iconImporter = (TextureImporter)AssetImporter.GetAtPath(iconPath); iconImporter.alphaIsTransparency = true; iconImporter.mipmapEnabled = false; iconImporter.SaveAndReimport();
            var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset"); var images = icons.Icons;
            Array.Resize(ref images, Math.Max(images.Length, 29)); images[28] = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath); icons.Icons = images;
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset"); config.ProtocolVersion = Math.Max(config.ProtocolVersion, 138);
            foreach (var asset in new UnityEngine.Object[] { registry, catalog, icons, config }) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
            AssetDatabase.SaveAssets();
        }

        static Material Material(string name, string shader, Color color)
        {
            string path = Folder + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); EditorUtility.SetDirty(material); return material;
        }

        static GameObject CreateVisual()
        {
            foreach (string suffix in new[] { "basecolor.JPEG", "normal.PNG", "MetalSmooth.png" })
            {
                var texture = (TextureImporter)AssetImporter.GetAtPath(Folder + "ExplosiveBall_" + suffix);
                texture.textureType = suffix == "normal.PNG" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                texture.sRGBTexture = suffix == "basecolor.JPEG"; texture.maxTextureSize = suffix == "basecolor.JPEG" ? 2048 : 1024;
                texture.mipmapEnabled = true; texture.streamingMipmaps = true; texture.textureCompression = TextureImporterCompression.CompressedHQ; texture.SaveAndReimport();
            }
            var material = Material("ExplosiveBall", "Universal Render Pipeline/Lit", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "ExplosiveBall_basecolor.JPEG"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "ExplosiveBall_normal.PNG"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "ExplosiveBall_MetalSmooth.png"));
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Smoothness", .8f); material.SetFloat("_BumpScale", .6f);
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("ExplosiveBallVisual"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "ExplosiveBall.fbx")); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source, scene);
            try
            {
                var filter = source.GetComponentInChildren<MeshFilter>(); var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                var matrix = filter.transform.localToWorldMatrix; mesh.name = "ExplosiveBallBody";
                mesh.vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                var normals = matrix.inverse.transpose; mesh.normals = mesh.normals.Select(n => normals.MultiplyVector(n).normalized).ToArray();
                mesh.tangents = mesh.tangents.Select(v => { var tangent = matrix.MultiplyVector(new Vector3(v.x, v.y, v.z)).normalized; return new Vector4(tangent.x, tangent.y, tangent.z, v.w); }).ToArray(); mesh.RecalculateBounds();
                string path = Folder + "ExplosiveBallBody.asset"; var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) AssetDatabase.CreateAsset(mesh, path); else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); mesh = saved; EditorUtility.SetDirty(mesh); }
                var body = new GameObject("Body"); body.transform.SetParent(root.transform, false); body.AddComponent<MeshFilter>().sharedMesh = mesh; body.AddComponent<MeshRenderer>().sharedMaterial = material;
                var wick = new GameObject("Wick"); wick.transform.SetParent(root.transform, false);
                var line = wick.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 2; line.startWidth = line.endWidth = .005f; line.numCapVertices = 3;
                line.SetPosition(0, new Vector3(0, .115f, 0)); line.SetPosition(1, new Vector3(.025f, .18f, -.01f));
                line.sharedMaterial = Material("Wick", "Universal Render Pipeline/Unlit", new Color(.32f, .22f, .1f)); line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere); ember.name = "Ember"; UnityEngine.Object.DestroyImmediate(ember.GetComponent<Collider>());
                ember.transform.SetParent(root.transform, false); ember.transform.localPosition = new Vector3(.025f, .18f, -.01f); ember.transform.localScale = Vector3.one * .01f;
                ember.GetComponent<Renderer>().sharedMaterial = Material("Ember", "Universal Render Pipeline/Unlit", new Color(2f, 1.1f, .25f)); ember.SetActive(false);
                var fuse = root.AddComponent<ExplosiveBallFuse>(); fuse.Wick = line; fuse.Ember = ember.transform;
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "ExplosiveBallVisual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(source); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static GameObject CreateNetworkPrefab(GameObject visual, string path, bool projectile)
        {
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject(projectile ? "ExplosiveBallProjectile" : "ExplosiveBallPickup"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.AddComponent<NetworkObject>();
                if (projectile) root.AddComponent<NetworkHandMortarBall>();
                else { root.AddComponent<NetworkFish>().Item = InventoryItem.ExplosiveBall; var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; root.AddComponent<SphereCollider>().radius = .125f; root.AddComponent<BulletSurface>().Kind = BulletSurfaceKind.Metal; }
                UnityEngine.Object.Instantiate(visual, root.transform);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void ConfigureImportedShip(GameObject root, NetworkFish pickup = null)
        {
            if (pickup == null) pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath)?.GetComponent<NetworkFish>();
            var stock = root.GetComponent<ExperimentalShipEquipment>(); if (stock == null || pickup == null) return;
            var prefabs = stock.Prefabs.ToList(); var items = stock.Items.ToList(); var points = stock.SpawnPoints.ToList();
            int index = prefabs.FindIndex(p => p != null && p.Item == InventoryItem.ExplosiveBall);
            if (index < 0)
            {
                var point = root.transform.Find("Loot_ExplosiveBall"); if (point == null) { point = new GameObject("Loot_ExplosiveBall").transform; point.SetParent(root.transform, false); }
                point.localPosition = new Vector3(2.3f, 4.11f, -1.3f); prefabs.Add(pickup); items.Add(InventoryItem.ExplosiveBall); points.Add(point);
            }
            else prefabs[index] = pickup;
            stock.Prefabs = prefabs.ToArray(); stock.Items = items.ToArray(); stock.SpawnPoints = points.ToArray();
        }
    }
}
