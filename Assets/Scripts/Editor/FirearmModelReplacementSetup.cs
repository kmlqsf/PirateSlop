using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class FirearmModelReplacementSetup
    {
        const string Folder = "Assets/Models/Firearms/";
        const string PlayerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
        const string EquipmentFolder = "Assets/Prefabs/Props/PirateEquipment/";
        static readonly string[] Keys = { "Pistol", "Musket", "Shotgun" };
        static GameObject Visual(string key) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + key + "Visual.prefab");

        [MenuItem("PirateSlop/Replace Firearm Models")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            ImportModels();
            BindModels();
            Edit("Assets/Resources/Ships/ShipV3Test.prefab", ConfigureImportedShip);
            AssetDatabase.SaveAssets();
        }

        public static void ImportModels()
        {
            Directory.CreateDirectory(Folder + "Meshes");
            AssetDatabase.Refresh();
            foreach (string key in Keys.Concat(new[] { "Trigger", "Hammer", "Frizzen" }))
            {
                foreach (string suffix in new[] { "BaseColor.jpg", "Normal.png", "MetalSmooth.png" })
                {
                    string path = Folder + "Textures/" + key + suffix;
                    var texture = (TextureImporter)AssetImporter.GetAtPath(path);
                    texture.textureType = suffix == "Normal.png" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    texture.sRGBTexture = suffix == "BaseColor.jpg";
                    texture.isReadable = false;
                    texture.maxTextureSize = suffix == "BaseColor.jpg" ? 2048 : 1024;
                    texture.mipmapEnabled = true;
                    texture.streamingMipmaps = true;
                    texture.textureCompression = TextureImporterCompression.CompressedHQ;
                    var platform = texture.GetPlatformTextureSettings("Standalone");
                    platform.overridden = true;
                    platform.maxTextureSize = texture.maxTextureSize;
                    platform.format = suffix == "BaseColor.jpg" ? TextureImporterFormat.DXT1 : TextureImporterFormat.DXT5;
                    texture.SetPlatformTextureSettings(platform);
                    texture.SaveAndReimport();
                }
                string materialPath = Folder + key + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Textures/" + key + "BaseColor.jpg"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Textures/" + key + "Normal.png"));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Textures/" + key + "MetalSmooth.png"));
                material.SetFloat("_Metallic", 1);
                material.SetFloat("_Smoothness", 1);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
            }
            foreach (string key in Keys) ImportAssembly(key);
            AssetDatabase.SaveAssets();
        }

        static void ImportAssembly(string key)
        {
            string path = Folder + key + "Assembly.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var preview = EditorSceneManager.NewPreviewScene();
            var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            SceneManager.MoveGameObjectToScene(source, preview);
            var root = new GameObject(key + "Visual");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var mirror = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                var nodes = new Dictionary<Transform, Transform> { { source.transform, root.transform } };
                foreach (var t in source.GetComponentsInChildren<Transform>(true))
                {
                    if (t == source.transform) continue;
                    var target = new GameObject(t.name).transform;
                    target.SetParent(nodes[t.parent], false);
                    target.position = mirror.MultiplyPoint3x4(t.position);
                    nodes[t] = target;
                    var filter = t.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    mesh.name = t.name;
                    var matrix = mirror * t.localToWorldMatrix;
                    Vector3 origin = target.position;
                    mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v) - origin).ToArray();
                    var normalMatrix = matrix.inverse.transpose;
                    mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
                    mesh.tangents = mesh.tangents.Select(v =>
                    {
                        var tangent = matrix.MultiplyVector(new Vector3(v.x, v.y, v.z)).normalized;
                        return new Vector4(tangent.x, tangent.y, tangent.z, -v.w);
                    }).ToArray();
                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        var triangles = mesh.GetTriangles(i);
                        for (int j = 0; j < triangles.Length; j += 3)
                        {
                            int swap = triangles[j]; triangles[j] = triangles[j + 2]; triangles[j + 2] = swap;
                        }
                        mesh.SetTriangles(triangles, i);
                    }
                    mesh.RecalculateBounds();
                    string meshPath = Folder + "Meshes/" + t.name + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        mesh = existing;
                        EditorUtility.SetDirty(mesh);
                    }
                    target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    string materialKey = t.name.StartsWith("FlintHammerMesh") ? "Hammer" : t.name.StartsWith("FrizzenMesh") ? "Frizzen" : t.name.StartsWith("TriggerMesh") ? "Trigger" : key;
                    target.gameObject.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + materialKey + ".mat");
                }
                var mechanism = root.AddComponent<FirearmMechanism>();
                var all = root.GetComponentsInChildren<Transform>();
                mechanism.Item = key == "Pistol" ? InventoryItem.Pistol : key == "Musket" ? InventoryItem.Musket : InventoryItem.DoubleBarrel;
                mechanism.TriggerPivot = all.Single(t => t.name.StartsWith("TriggerPivot_"));
                mechanism.Hammers = all.Where(t => t.name.StartsWith("FlintHammerPivot")).OrderBy(t => t.name).ToArray();
                mechanism.Frizzens = all.Where(t => t.name.StartsWith("FrizzenPivot")).OrderBy(t => t.name).ToArray();
                mechanism.StrikePoints = all.Where(t => t.name.StartsWith("FlintStrike")).OrderBy(t => t.name).ToArray();
                mechanism.SparkScale = key == "Pistol" ? .75f : .95f;
                var grip = new GameObject("GripSocket_Firearm").transform;
                grip.SetParent(root.transform, false);
                grip.localPosition = key == "Pistol" ? new Vector3(0, .023f, .029f) : mechanism.TriggerPivot.localPosition + new Vector3(0, -.025f, -.025f);
                if (key != "Pistol")
                {
                    var support = new GameObject("SupportSocket_Firearm").transform;
                    support.SetParent(root.transform, false);
                    support.localPosition = grip.localPosition + (key == "Musket" ? new Vector3(0, .035f, .28f) : new Vector3(0, .025f, .065f));
                }
                if (key == "Musket")
                {
                    var shoulder = new GameObject("ShoulderSocket_Firearm").transform;
                    shoulder.SetParent(root.transform, false);
                    shoulder.localPosition = new Vector3(0, .0565f, -.02f);
                }
                root.transform.localScale = Vector3.one * (key == "Pistol" ? .72f : .9f);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + key + "Visual.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(source);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static void BindModels()
        {
            RepairCharacterPoses();
            foreach (string key in new[] { "Musket", "Shotgun" })
            {
                string actionName = key == "Musket" ? "MusketActionModel" : "ShotgunActionModel";
                Edit(EquipmentFolder + actionName + ".prefab", root =>
                {
                    foreach (var child in root.transform.Cast<Transform>().Where(t => !t.name.StartsWith("Action")).ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    var visual = UnityEngine.Object.Instantiate(Visual(key), root.transform).transform;
                    visual.name = "FirearmAssembly";
                    visual.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.forward);
                    visual.localScale *= .01f;
                    var definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>("Assets/Settings/Weapons/" + (key == "Musket" ? "Musket" : "DoubleBarrel") + ".asset");
                    visual.localPosition = root.transform.Find("ActionMuzzle").localPosition - visual.localRotation * (definition.MuzzleOffset * visual.localScale.x);
                    var ramrod = root.transform.Find("ActionRamrod");
                    if (ramrod != null)
                    {
                        foreach (var renderer in ramrod.GetComponentsInChildren<Renderer>(true)) UnityEngine.Object.DestroyImmediate(renderer);
                        foreach (var filter in ramrod.GetComponentsInChildren<MeshFilter>(true)) UnityEngine.Object.DestroyImmediate(filter);
                    }
                });
            }
            Edit(PlayerPath, root =>
            {
                var weapon = root.GetComponent<PirateWeapon>();
                ConfigurePistolFingers(root);
                var equipment = root.GetComponent<NetworkEquipment>();
                equipment.Models[(int)InventoryItem.Musket - 13] = Visual("Musket");
                equipment.Models[(int)InventoryItem.DoubleBarrel - 13] = Visual("Shotgun");
                foreach (var pivot in new[] { weapon.WorldPivot, weapon.ViewPivot })
                {
                    Clear(pivot.gameObject);
                    var visual = UnityEngine.Object.Instantiate(Visual("Pistol"), pivot);
                    visual.name = "PistolAssembly";
                    var muzzle = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Muzzle_Pistol");
                    if (pivot == weapon.WorldPivot) weapon.WorldMuzzle = muzzle;
                    else
                    {
                        weapon.ViewMuzzle = muzzle;
                        foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            });
            BindPickup("Assets/Prefabs/Networking/DroppedPistol.prefab", "Pistol");
            BindPickup(EquipmentFolder + "SniperMusketPickup.prefab", "Musket");
            BindPickup(EquipmentFolder + "PirateDoubleBarrelPickup.prefab", "Shotgun");
            foreach (var pair in new[] { new[] { "SniperMusket", "Musket" }, new[] { "PirateDoubleBarrel", "Shotgun" } })
                Edit(EquipmentFolder + pair[0] + ".prefab", root =>
                {
                    Clear(root);
                    UnityEngine.Object.Instantiate(Visual(pair[1]), root.transform);
                    var box = root.GetComponent<BoxCollider>();
                    if (box != null) { var bounds = Bounds(root); box.center = bounds.center; box.size = bounds.size; }
                });
        }

        public static void RepairCharacterPoses()
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animations/Player/PiratePlayer.controller");
            var layer = controller.layers.First(l => l.name == "ItemActions");
            var grip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/New_PistolAim.anim");
            foreach (var state in layer.stateMachine.states.Select(s => s.state))
                if (state.name.EndsWith("Ready") || state.name.EndsWith("Reload") || state.name.EndsWith("Aim"))
                { state.motion = grip; EditorUtility.SetDirty(state); }
            EditorUtility.SetDirty(controller);
        }

        static void ConfigurePistolFingers(GameObject root)
        {
            var rig = root.GetComponent<WeaponArmRig>();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/New_PistolAim.anim");
            rig.PistolFingers = rig.BodyRig.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("mixamorig:RightHand") && t.name != "mixamorig:RightHand").ToArray();
            var bindings = AnimationUtility.GetCurveBindings(clip);
            rig.PistolFingerGrip = rig.PistolFingers.Select(bone =>
            {
                string path = AnimationUtility.CalculateTransformPath(bone, rig.BodyRig);
                var value = bone.localRotation;
                var components = new[] { value.x, value.y, value.z, value.w };
                for (int i = 0; i < 4; i++)
                {
                    string property = "m_LocalRotation." + "xyzw"[i];
                    var binding = bindings.FirstOrDefault(b => b.path == path && b.propertyName == property);
                    if (binding.path != null) components[i] = AnimationUtility.GetEditorCurve(clip, binding).Evaluate(0);
                }
                return new Quaternion(components[0], components[1], components[2], components[3]).normalized;
            }).ToArray();
        }

        static void BindPickup(string path, string key)
        {
            Edit(path, root =>
            {
                Clear(root);
                root.transform.localScale = Vector3.one;
                var model = UnityEngine.Object.Instantiate(Visual(key), root.transform).transform;
                model.localRotation = Quaternion.Euler(0, 0, 90);
                var bounds = Bounds(root);
                model.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foreach (var collider in root.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                var box = root.AddComponent<BoxCollider>();
                bounds = Bounds(root);
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, Vector3.one * .035f);
                var body = root.GetComponent<Rigidbody>();
                if (body != null) { body.isKinematic = true; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeAll; }
            });
        }

        public static void ConfigureImportedShip(GameObject root)
        {
            if (Visual("Musket") == null || Visual("Shotgun") == null) return;
            var stock = root.GetComponent<ExperimentalShipEquipment>();
            if (stock == null || stock.SpawnPoints.Length == 0) return;
            var items = stock.Items.ToList();
            var prefabs = stock.Prefabs.ToList();
            var points = stock.SpawnPoints.ToList();
            var deck = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("V3_Deck_"))
                .Select(f => TransformBounds(f.sharedMesh.bounds, root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix)).ToArray();
            float height = points[0].localPosition.y;
            float middle = points.Take(7).Average(t => t.localPosition.z);
            foreach (var item in new[] { InventoryItem.Musket, InventoryItem.DoubleBarrel })
            {
                if (items.Contains(item)) continue;
                Vector3 location = default;
                bool found = false;
                for (int i = 0; i < 36 && !found; i++)
                {
                    float x = i % 2 == 0 ? -2.6f : 2.6f;
                    float z = middle + (i / 2 - 5) * 1.4f;
                    var surfaces = deck.Where(b => x >= b.min.x && x <= b.max.x && z >= b.min.z && z <= b.max.z && Mathf.Abs(b.max.y - height) < .5f).ToArray();
                    if (surfaces.Length == 0 || points.Any(t => new Vector2(t.localPosition.x - x, t.localPosition.z - z).sqrMagnitude < 1.8f)) continue;
                    location = new Vector3(x, surfaces.Max(b => b.max.y), z);
                    found = true;
                }
                if (!found) throw new InvalidOperationException("No free authored deck point for " + item);
                var marker = new GameObject("Loot_" + item).transform;
                marker.SetParent(points[0].parent, false);
                marker.localPosition = location;
                string pickup = item == InventoryItem.Musket ? "SniperMusketPickup" : "PirateDoubleBarrelPickup";
                items.Add(item);
                prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentFolder + pickup + ".prefab").GetComponent<NetworkFish>());
                points.Add(marker);
            }
            stock.Items = items.ToArray(); stock.Prefabs = prefabs.ToArray(); stock.SpawnPoints = points.ToArray();
        }

        static Bounds Bounds(GameObject root)
        {
            var shapes = root.GetComponentsInChildren<MeshFilter>(true).Select(f => TransformBounds(f.sharedMesh.bounds, root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix)).ToArray();
            var bounds = shapes[0];
            foreach (var shape in shapes.Skip(1)) bounds.Encapsulate(shape);
            return bounds;
        }
        static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            var bounds = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
            for (int i = 0; i < 8; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return bounds;
        }
        static void Clear(GameObject root)
        {
            foreach (var child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (var renderer in root.GetComponents<Renderer>()) UnityEngine.Object.DestroyImmediate(renderer);
            foreach (var filter in root.GetComponents<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter);
        }
        static void Edit(string path, Action<GameObject> change)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { change(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                typeof(EditorSceneManager).GetMethod("ClearTargetSceneForNewGameObjects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
            }
        }
    }
}
