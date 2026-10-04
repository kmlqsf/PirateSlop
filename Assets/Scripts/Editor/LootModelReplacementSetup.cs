using System;
using System.IO;
using System.Linq;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class LootModelReplacementSetup
    {
        const string Folder = "Assets/Models/Loot/Replacement/";
        static readonly string[] Keys = { "CannonballStandard", "CannonballFire", "CannonballIce", "CannonballPush", "RumBottle", "GrappleHook", "HolyGrenade", "CannonKit", "CannonBase", "CannonMount", "CannonBarrel", "CannonWheel" };
        static string VisualPath(string key) => Folder + key + "/" + key + "Visual.prefab";
        static GameObject Visual(string key) => AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath(key));
        static void Status(string text) => File.WriteAllText("Temp/LootReplacement/SetupStatus.txt", text);

        public static void ImportModels()
        {
            AssetDatabase.Refresh();
            foreach (var key in Keys)
            {
                string directory = Folder + key + "/";
                foreach (string suffix in new[] { "BaseColor.jpg", "Normal.png", "MetalSmooth.png" })
                {
                    var texture = (TextureImporter)AssetImporter.GetAtPath(directory + key + suffix);
                    texture.textureType = suffix == "Normal.png" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    texture.sRGBTexture = suffix == "BaseColor.jpg";
                    texture.maxTextureSize = suffix == "BaseColor.jpg" ? 2048 : 1024;
                    texture.isReadable = false; texture.mipmapEnabled = true;
                    texture.streamingMipmaps = true; texture.textureCompression = TextureImporterCompression.CompressedHQ;
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
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "BaseColor.jpg"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "Normal.png"));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "MetalSmooth.png"));
                material.SetFloat("_Smoothness", 1f); material.SetFloat("_Metallic", 1f);
                material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.enableInstancing = true;
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
                var root = new GameObject(key + "Visual");
                try
                {
                    var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(directory + key + ".fbx"), root.transform);
                    model.name = "Geometry";
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
                    var bounds = Bounds(root);
                    float dimension = key.StartsWith("Cannonball", StringComparison.Ordinal) ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) :
                        key == "CannonBase" || key == "CannonMount" || key == "CannonBarrel" ? bounds.size.z : key == "CannonKit" ? Mathf.Max(bounds.size.x, bounds.size.z) : bounds.size.y;
                    float size = key.StartsWith("Cannonball", StringComparison.Ordinal) ? .24f : key == "RumBottle" ? .5f : key == "GrappleHook" ? .37f :
                        key == "HolyGrenade" ? .38f : key == "CannonKit" ? 1.6f : key == "CannonBase" ? 1.7f : key == "CannonMount" ? 1.55f : key == "CannonBarrel" ? 2.7f : .52f;
                    float scale = size / dimension;
                    model.transform.localScale *= scale;
                    Vector3 center = bounds.center;
                    if (!key.StartsWith("Cannonball", StringComparison.Ordinal) && key != "CannonWheel") center.y = bounds.min.y;
                    model.transform.localPosition -= center * scale;
                    PrefabUtility.SaveAsPrefabAsset(root, VisualPath(key));
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
                Status("Imported " + key);
            }
            Status("Models and materials saved");
        }

        public static void BindItems()
        {
            Replace("Assets/Prefabs/Cannons/DisassembledCannon.prefab", "CannonKit");
            Replace("Assets/Prefabs/Networking/DroppedCannon.prefab", "CannonKit");
            Replace("Assets/Prefabs/Loot/RumBottle.prefab", "RumBottle");
            Replace("Assets/Resources/GrappleHookModel.prefab", "GrappleHook");
            Replace("Assets/Prefabs/Props/PirateEquipment/GrapplingHookPickup.prefab", "GrappleHook");
            Replace("Assets/Prefabs/Props/PirateEquipment/HolyGrenade.prefab", "HolyGrenade");
            Replace("Assets/Prefabs/Props/PirateEquipment/HolyGrenadePickup.prefab", "HolyGrenade");
            foreach (string path in new[] { "Assets/Prefabs/Cannons/Cannonball.prefab", "Assets/Prefabs/Cannons/FiredCannonball.prefab", "Assets/Prefabs/Networking/DroppedCannonball.prefab", "Assets/Prefabs/Loot/IslandCannonball.prefab" })
                Edit(path, root => BindAmmo(root));
            Status("Pickup, held equipment and ammunition models saved");
        }

        static void Replace(string path, string key) => Edit(path, root =>
        {
            ReplaceAligned(root, Visual(key));
            var box = root.GetComponent<BoxCollider>();
            if (box != null) { var bounds = Bounds(root); box.center = bounds.center; box.size = bounds.size; }
        });

        static void ReplaceAligned(GameObject root, GameObject source)
        {
            var before = Bounds(root);
            ReplaceGeometry(root, source);
            var model = root.transform.Find("ReplacementVisual");
            model.localPosition += before.center - Bounds(model.gameObject, root.transform).center;
        }

        static void ReplaceGeometry(GameObject root, GameObject source)
        {
            var previous = root.transform.Find("ReplacementVisual");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var fuse = root.GetComponentInChildren<HolyGrenadeFuse>(true);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (fuse != null && (filter.transform == fuse.Wick || filter.transform == fuse.Ember)) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
                UnityEngine.Object.DestroyImmediate(filter);
            }
            var model = UnityEngine.Object.Instantiate(source, root.transform);
            model.name = "ReplacementVisual"; model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        static void BindAmmo(GameObject root)
        {
            foreach (var ball in root.GetComponentsInChildren<Cannonball>(true))
            {
                var models = ball.AmmoModels ?? Array.Empty<GameObject>();
                Array.Resize(ref models, Mathf.Max(5, models.Length));
                for (int i = 0; i < 4; i++) models[i] = Visual(Keys[i]);
                ball.AmmoModels = models; ball.RefreshVisual();
            }
        }

        public static void BindCannons()
        {
            foreach (string path in new[] { "Assets/Prefabs/Cannons/DeployableCannon.prefab", "Assets/Prefabs/Cannons/CannonStation.prefab" })
                Edit(path, root => { ConfigureCannon(root.GetComponent<SimpleCannon>()); BindAmmo(root); });
            Status("Four-part cannons saved");
        }

        static void ConfigureCannon(SimpleCannon gun)
        {
            var root = gun.gameObject;
            var previous = root.transform.Find("ReplacementCannon");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
                UnityEngine.Object.DestroyImmediate(filter);
            }
            var assembly = Child(root.transform, "ReplacementCannon");
            var carriage = AddVisual("CannonBase", assembly);
            var baseBounds = Bounds(carriage.gameObject);
            float radius = Bounds(Visual("CannonWheel")).extents.y;
            carriage.localPosition = new Vector3(0, radius - baseBounds.size.y * .5f, 0);
            var wheels = new System.Collections.Generic.List<Transform>();
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                {
                    var wheel = AddVisual("CannonWheel", assembly);
                    wheel.name = "Wheel_" + side + "_" + end;
                    wheel.localPosition = new Vector3(side * (baseBounds.extents.x + .08f), radius, end * baseBounds.extents.z * .77f);
                    if (side > 0) wheel.localRotation = Quaternion.Euler(0, 180, 0);
                    wheels.Add(wheel);
                }
            var yaw = Child(assembly, "TraversePivot");
            yaw.localPosition = new Vector3(0, carriage.localPosition.y + baseBounds.max.y - .10f, 0);
            var mount = AddVisual("CannonMount", yaw);
            var mountBounds = Bounds(mount.gameObject);
            var pitch = Child(yaw, "BarrelPitch");
            pitch.localPosition = new Vector3(0, mountBounds.max.y - .14f, 0);
            pitch.localRotation = Quaternion.Euler(-3f, 0, 0);
            var barrel = AddVisual("CannonBarrel", pitch);
            var barrelBounds = Bounds(barrel.gameObject);
            var collision = root.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c => c.name == "BarrelCollision");
            if (collision == null) collision = Child(barrel, "BarrelCollision").gameObject.AddComponent<BoxCollider>();
            collision.transform.SetParent(barrel, false);
            collision.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            collision.transform.localScale = Vector3.one;
            collision.center = barrelBounds.center; collision.size = barrelBounds.size;
            var points = Points(barrel.gameObject);
            float edge = points.Max(p => Mathf.Abs(p.x));
            var pins = points.Where(p => Mathf.Abs(p.x) > edge * .84f).ToArray();
            var pinBounds = new Bounds(pins[0], Vector3.zero);
            foreach (var pin in pins) pinBounds.Encapsulate(pin);
            Vector3 axle = pinBounds.center;
            axle.x = 0f;
            barrel.localPosition = -axle;
            var muzzle = Child(pitch, "Muzzle");
            var mouth = points.Where(p => p.z > barrelBounds.max.z - barrelBounds.size.z * .025f).ToArray();
            var mouthBounds = new Bounds(mouth[0], Vector3.zero);
            foreach (var point in mouth) mouthBounds.Encapsulate(point);
            muzzle.localPosition = new Vector3(mouthBounds.center.x, mouthBounds.center.y, barrelBounds.max.z) - axle;
            foreach (var existingHandle in root.GetComponentsInChildren<ShipControlHandle>(true).Where(h => h.Cannon == gun))
            {
                var existingCollider = existingHandle.GetComponent<SphereCollider>();
                if (existingCollider != null) UnityEngine.Object.DestroyImmediate(existingCollider);
                UnityEngine.Object.DestroyImmediate(existingHandle);
            }
            var breech = Child(pitch, "Breech");
            breech.localPosition = new Vector3(0, muzzle.localPosition.y, barrelBounds.min.z - axle.z);
            breech.gameObject.AddComponent<SphereCollider>().radius = .13f;
            var handle = breech.gameObject.AddComponent<ShipControlHandle>(); handle.Cannon = gun;
            gun.TraversePivot = yaw; gun.BarrelPivot = pitch; gun.Muzzle = muzzle; gun.Breech = breech;
            var visual = root.GetComponent<CannonWheelVisual>();
            if (visual == null) visual = root.AddComponent<CannonWheelVisual>();
            visual.Wheels = wheels.ToArray(); visual.Radius = radius;
            var shape = root.GetComponent<BoxCollider>();
            shape.size = new Vector3(2f * (baseBounds.extents.x + .08f + Bounds(Visual("CannonWheel")).extents.x), 1.05f, Mathf.Max(2f, baseBounds.size.z));
        }

        public static void BindShip(string path)
        {
            Edit(path, ConfigureImportedShip);
            Status("Saved " + path);
        }

        public static void ConfigureImportedShip(GameObject root)
        {
            if (Keys.Any(key => Visual(key) == null)) return;
            BindAmmo(root);
            foreach (var pickup in root.GetComponentsInChildren<CannonPickup>(true)) ReplaceAligned(pickup.gameObject, Visual("CannonKit"));
            foreach (var shelf in root.GetComponentsInChildren<RumShelf>(true))
                foreach (var bottle in shelf.Bottles.Where(b => b != null))
                {
                    var before = WorldBounds(bottle);
                    Vector3 position = new Vector3(before.center.x, before.min.y, before.center.z);
                    bottle.transform.localScale = Vector3.one;
                    bottle.transform.position = position;
                    ReplaceGeometry(bottle, Visual("RumBottle"));
                }
            if (root.GetComponent<PirateSlop.Ships.ShipV3Features>() != null) ConfigureNewShip(root);
            BoardingEquipmentSetup.ConfigureImportedShip(root);
            FirearmModelReplacementSetup.ConfigureImportedShip(root);
        }

        public static void RenderIcons()
        {
            var icons = AssetDatabase.LoadAssetAtPath<InventoryIcons>("Assets/UI/Inventory/InventoryIcons.asset");
            var items = new[] { InventoryItem.Cannonball, InventoryItem.FireCannonball, InventoryItem.IceCannonball, InventoryItem.PushCannonball, InventoryItem.Rum, InventoryItem.GrapplingHook, InventoryItem.HolyGrenade, InventoryItem.Cannon };
            for (int i = 0; i < items.Length; i++)
            {
                string path = "Assets/UI/Inventory/" + items[i] + ".png";
                InventorySlotSetup.RenderIcon(Visual(Keys[i]), path);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                icons.Icons[(int)items[i]] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            EditorUtility.SetDirty(icons); AssetDatabase.SaveAssetIfDirty(icons);
            Status("Eight inventory icons saved");
        }

        static void ConfigureNewShip(GameObject root)
        {
            var previous = root.transform.Find("ReplacementRumShelf");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var rack = Child(root.transform, "ReplacementRumShelf");
            var shelves = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.StartsWith("V17_Rum_Shelf_", StringComparison.Ordinal) && m.name.EndsWith("_Shelf", StringComparison.Ordinal))
                .Select(m => Bounds(m.gameObject, root.transform)).OrderBy(b => b.max.y).ToArray();
            if (shelves.Length != 4) throw new InvalidOperationException("Expected four authored rum shelves");
            var storage = rack.gameObject.AddComponent<RumShelf>();
            storage.Bottles = new GameObject[NetworkShip.RumCapacity];
            for (int row = 0; row < shelves.Length; row++)
                for (int column = 0; column < 3; column++)
                {
                    int index = row * 3 + column;
                    var bottle = AddVisual("RumBottle", rack);
                    bottle.localScale *= .72f;
                    bottle.localPosition = new Vector3(shelves[row].center.x, shelves[row].max.y + .008f, Mathf.Lerp(shelves[row].min.z + .3f, shelves[row].max.z - .3f, column * .5f));
                    bottle.gameObject.SetActive(index < 3); storage.Bottles[index] = bottle.gameObject;
                }
            var shape = rack.gameObject.AddComponent<BoxCollider>();
            var shelfBounds = shelves[0]; foreach (var bounds in shelves.Skip(1)) shelfBounds.Encapsulate(bounds);
            shelfBounds.Encapsulate(WorldBounds(rack.gameObject));
            shape.center = shelfBounds.center; shape.size = shelfBounds.size + new Vector3(.20f, .04f, .08f);
            var equipment = root.GetComponent<ExperimentalShipEquipment>();
            if (equipment == null) equipment = root.AddComponent<ExperimentalShipEquipment>();
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkPlayer.prefab");
            var drops = player.GetComponent<NetworkWeapon>().DropPrefabs;
            equipment.Items = new[] { InventoryItem.Cannon, InventoryItem.FireCannonball, InventoryItem.IceCannonball, InventoryItem.PushCannonball, InventoryItem.Rum, InventoryItem.GrapplingHook, InventoryItem.HolyGrenade };
            equipment.Prefabs = equipment.Items.Select(item => drops[CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item]).ToArray();
            var oldPoints = root.transform.Find("ReplacementLootPoints");
            if (oldPoints != null) UnityEngine.Object.DestroyImmediate(oldPoints.gameObject);
            var markers = Child(root.transform, "ReplacementLootPoints");
            var deck = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.StartsWith("V3_Deck_", StringComparison.Ordinal)).Select(m => Bounds(m.gameObject, root.transform)).ToArray();
            var main = root.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "V3_Main_MastTop_Platform");
            var fore = root.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "V3_Fore_MastTop_Platform");
            float middle = (Bounds(main.gameObject, root.transform).center.z + Bounds(fore.gameObject, root.transform).center.z) * .5f;
            equipment.SpawnPoints = equipment.Items.Select((item, index) =>
            {
                float x = index % 2 == 0 ? -2.6f : 2.6f, z = middle + (index / 2 - 1.5f) * 1.65f;
                var surfaces = deck.Where(b => x >= b.min.x && x <= b.max.x && z >= b.min.z && z <= b.max.z).ToArray();
                if (surfaces.Length == 0) throw new InvalidOperationException("Loot point has no authored deck: " + item);
                var marker = Child(markers, "Loot_" + item); marker.localPosition = new Vector3(x, surfaces.Max(b => b.max.y), z);
                return marker;
            }).ToArray();
        }

        static Transform AddVisual(string key, Transform parent)
        {
            var model = UnityEngine.Object.Instantiate(Visual(key), parent);
            model.name = key; model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return model.transform;
        }

        static Transform Child(Transform parent, string name)
        {
            var target = new GameObject(name).transform; target.SetParent(parent, false); return target;
        }

        static Vector3[] Points(GameObject target, Transform relative = null)
        {
            var origin = relative != null ? relative : target.transform;
            return target.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).SelectMany(m =>
            {
                var mapping = origin.worldToLocalMatrix * m.transform.localToWorldMatrix;
                return m.sharedMesh.vertices.Select(mapping.MultiplyPoint3x4);
            }).ToArray();
        }

        static Bounds Bounds(GameObject target, Transform relative = null)
        {
            var points = Points(target, relative);
            if (points.Length == 0) throw new InvalidOperationException("Missing geometry: " + target.name);
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point); return bounds;
        }

        static Bounds WorldBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }

        static void Edit(string path, Action<GameObject> change)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            var root = PrefabUtility.LoadPrefabContents(path);
            try { change(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
