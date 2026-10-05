using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FishNet.Object;
using PirateSlop.Networking;
using PirateSlop.Harpoon;
using PirateSlop.Ships;

namespace PirateSlop.EditorTools
{
    public static class ShipV3ImportSetup
    {
        const string Folder = "Assets/Models/Ships/ShipV3";
        const string PrefabPath = "Assets/Resources/Ships/ShipV3Test.prefab";
        const string SpawnablesPath = "Assets/Settings/Networking/NetworkPrefabs.asset";
        static Dictionary<string, Transform> objects;
        static Dictionary<string, JObject> records;
        static Dictionary<string, Material> materials;
        static Dictionary<int, ShipDamageSection> sections;
        static Matrix4x4 basis, axes;
        static GameObject root;
        static ShipV3Features features;
        static ShipV3VisualRig visual;
        static List<Rigidbody> bodies;
        static List<bool> pendulums;
        static JObject document;

        [MenuItem("PirateSlop/Import Ship V3 For Test Map")]
        public static void Configure()
        {
            File.WriteAllText("Tools/ShipV3/ImportStatus.txt", "Preparing materials");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before import.");
            if (!File.Exists(Folder + "/ShipV3.json") || !File.Exists(Folder + "/ShipV3.fbx")) throw new InvalidOperationException("Export the current fitted Blender source first.");
            Directory.CreateDirectory(Folder + "/Materials");
            Directory.CreateDirectory(Folder + "/RuntimeMeshes");
            Directory.CreateDirectory("Assets/Settings/ShipDestruction");
            Directory.CreateDirectory("Assets/Resources/Ships");
            AssetDatabase.Refresh();
            document = JObject.Parse(File.ReadAllText(Folder + "/ShipV3.json"));
            records = document["objects"].Cast<JObject>().ToDictionary(item => (string)item["name"]);
            ImportMaterials();
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/ShipV3.fbx");
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.importBlendShapes = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.preserveHierarchy = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var material in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.Key), material.Value);
            importer.SaveAndReimport();
            File.WriteAllText("Tools/ShipV3/ImportStatus.txt", "Preparing ship components");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ShipV3.fbx");
            if (source == null) throw new InvalidOperationException("ShipV3 FBX import failed.");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                root = new GameObject("ShipV3Test");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "ShipV3Visual"; model.transform.SetParent(root.transform, true);
                objects = model.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
                var origin = objects["UnityV3_Origin"].position;
                var forward = (objects["UnityV3_Forward"].position - origin).normalized;
                var up = (objects["UnityV3_Up"].position - origin).normalized;
                model.transform.rotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up)) * model.transform.rotation;
                origin = objects["UnityV3_Origin"].position;
                if (Vector3.Dot(objects["UnityV3_Right"].position - origin, Vector3.right) < 0f)
                    model.transform.localScale = new Vector3(-model.transform.localScale.x, model.transform.localScale.y, model.transform.localScale.z);
                model.transform.position -= objects["UnityV3_Origin"].position + Vector3.up * (float)document["waterline"];
                origin = objects["UnityV3_Origin"].position;
                basis = Matrix4x4.identity;
                basis.SetColumn(0, new Vector4((objects["UnityV3_Right"].position - origin).x, (objects["UnityV3_Right"].position - origin).y, (objects["UnityV3_Right"].position - origin).z, 0));
                basis.SetColumn(1, new Vector4((objects["UnityV3_Forward"].position - origin).x, (objects["UnityV3_Forward"].position - origin).y, (objects["UnityV3_Forward"].position - origin).z, 0));
                basis.SetColumn(2, new Vector4((objects["UnityV3_Up"].position - origin).x, (objects["UnityV3_Up"].position - origin).y, (objects["UnityV3_Up"].position - origin).z, 0));
                basis.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
                axes = basis; axes.SetColumn(3, new Vector4(0, 0, 0, 1));
                bodies = new List<Rigidbody>(); pendulums = new List<bool>();
                var main = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkShip.prefab");
                var body = root.AddComponent<Rigidbody>();
                EditorUtility.CopySerialized(main.GetComponent<Rigidbody>(), body);
                body.isKinematic = true; body.useGravity = false;
                root.AddComponent<NetworkObject>();
                var motor = root.AddComponent<ShipController>();
                EditorUtility.CopySerialized(main.GetComponent<ShipController>(), motor);
                root.AddComponent<NetworkShip>();
                var observer = root.AddComponent<FishNet.Observing.NetworkObserver>();
                EditorUtility.CopySerialized(main.GetComponent<FishNet.Observing.NetworkObserver>(), observer);
                root.AddComponent<SailSystem>();
                features = root.AddComponent<ShipV3Features>();
                visual = root.AddComponent<ShipV3VisualRig>();
                ConfigureSections();
                File.WriteAllText("Tools/ShipV3/ImportStatus.txt", "Binding mechanisms");
                ConfigureControls(motor);
                ConfigureLanterns();
                ConfigureBell();
                ConfigureDoor();
                ConfigureDice();
                ConfigureHarpoons();
                ConfigureDispenser(main);
                ConfigureSupports();
                ConfigureWind();
                features.PhysicsBodies = bodies.ToArray(); features.Pendulums = pendulums.ToArray();
                ConfigureProfile();
                features.RespawnPoint = Child(root.transform, "CrewRescueSpawn");
                features.RespawnPoint.position = DeckSpawn();
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                ShipV3BindingRepair.Configure(root, document);
                LootModelReplacementSetup.ConfigureImportedShip(root);
                ConfigureGeometryBudget(root);
                ConfigureLanternLighting(root);
                HandLanternSetup.ConfigureImportedShip(root);
                ShipMonkeySetup.Configure(root);
                ShipCustomizationSetup.Configure(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("ShipV3 prefab was not saved.");
                RemoveUnusedBatchMeshes(root);
                RegisterNetworkPrefab();
                AssetDatabase.SaveAssets();
                File.WriteAllText("Tools/ShipV3/ImportStatus.txt", "Saved " + PrefabPath);
                Debug.Log("Ship V3 imported: " + sections.Count + " wooden pieces, " + bodies.Count + " physical props. Test map only.");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static void RegisterNetworkPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var registry = AssetDatabase.LoadAssetAtPath<FishNet.Managing.Object.SinglePrefabObjects>(SpawnablesPath);
            if (prefab == null || registry == null)
                throw new InvalidOperationException("Ship V3 prefab or session spawnable collection is unavailable.");
            var networkObject = prefab.GetComponent<NetworkObject>();
            if (networkObject == null)
                throw new InvalidOperationException("Ship V3 prefab is missing NetworkObject.");
            registry.AddObject(networkObject, true, false);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
        }

        [MenuItem("PirateSlop/Repair Ship V3 Mechanisms And Materials")]
        public static void RepairMechanismBindings()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before repair.");
            document = JObject.Parse(File.ReadAllText(Folder + "/ShipV3.json"));
            AssetDatabase.Refresh();
            ImportMaterials();
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var renderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && materials.TryGetValue(m.name, out var replacement) ? replacement : m).ToArray();
                ShipV3BindingRepair.Configure(prefabRoot, document);
                ConfigureGeometryBudget(prefabRoot);
                ConfigureLanternLighting(prefabRoot);
                HandLanternSetup.ConfigureImportedShip(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                RemoveUnusedBatchMeshes(prefabRoot);
                RegisterNetworkPrefab();
                AssetDatabase.SaveAssets();
                File.WriteAllText("Tools/ShipV3/BindingRepairStatus.txt", "Saved corrected mechanisms, interaction targets, chain routing and material references.");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
        }

        [MenuItem("PirateSlop/Optimize Ship V3 Runtime Geometry")]
        public static void OptimizeRuntimeGeometry()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before optimization.");
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigureGeometryBudget(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                RemoveUnusedBatchMeshes(prefabRoot);
                RegisterNetworkPrefab();
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
        }

        public static void ConfigureGeometryBudget(GameObject prefabRoot)
        {
            Directory.CreateDirectory(Folder + "/RuntimeMeshes/Batches");
            AssetDatabase.Refresh();
            foreach (var previous in prefabRoot.GetComponentsInChildren<ShipV3RenderBatch>(true))
            {
                foreach (var source in previous.Sources) if (source != null) source.enabled = true;
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }
            var destruction = prefabRoot.GetComponent<ShipDestruction>();
            foreach (var section in destruction.Sections)
            {
                section.LazyFragmentColliders = true;
                foreach (var fragment in section.Fragments)
                    foreach (var collider in fragment.GetComponents<Collider>())
                        UnityEngine.Object.DestroyImmediate(collider);
                section.DamageColliders = section.Intact.GetComponents<Collider>();
            }
            var rig = prefabRoot.GetComponent<ShipV3VisualRig>();
            var mechanics = prefabRoot.GetComponent<ShipV3Features>();
            var moving = rig.Motions.Select(m => m.Target)
                .Concat(new[] { rig.Rudder, mechanics.DoorHinge, mechanics.AnchorTravel, mechanics.DispenserLever })
                .Concat(mechanics.Attachments.Where(a => a.Fall).Select(a => a.Object))
                .Concat(prefabRoot.GetComponentsInChildren<HelmInteraction>(true).SelectMany(h => new[] { h.transform, h.Wheel }))
                .Where(t => t != null).ToArray();
            var shipBody = prefabRoot.GetComponent<Rigidbody>();
            var dependent = new HashSet<Renderer>(destruction.Sections.SelectMany(s => s.DependentRenderers));
            var chainLinks = new HashSet<Transform>(rig.ChainLinks);
            var sources = prefabRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMaterials.Length == 1 && r.sharedMaterial != null
                    && r.GetComponent<MeshFilter>() is MeshFilter filter && filter.sharedMesh != null && filter.sharedMesh.isReadable
                    && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder + "/", StringComparison.Ordinal)
                    && r.GetComponentInParent<Rigidbody>() == shipBody
                    && r.GetComponentInParent<ShipControlHandle>() == null
                    && r.GetComponentInParent<HarpoonGun>() == null
                    && r.GetComponentInParent<ShipV3ClothMotion>() == null
                    && !dependent.Contains(r) && !chainLinks.Contains(r.transform)
                    && !moving.Any(t => r.transform.IsChildOf(t))).ToArray();
            int number = 0, batchedSources = 0;
            foreach (var group in sources.GroupBy(r => new {
                Material = r.sharedMaterial,
                Cell = Mathf.FloorToInt(prefabRoot.transform.InverseTransformPoint(r.bounds.center).z / 8f)
            }))
            {
                var members = group.ToArray();
                for (int start = 0; start < members.Length; start += 96)
                {
                    var chunk = members.Skip(start).Take(96).ToArray();
                    if (chunk.Length < 2) continue;
                    batchedSources += chunk.Length;
                    string name = "ShipV3Batch_" + number++;
                    var mesh = ShipV3RenderBatch.BuildMesh(chunk, group.Key.Material, prefabRoot.transform, true);
                    var saved = StoreMesh(mesh, "Batches/" + name);
                    var batchObject = new GameObject(name);
                    batchObject.transform.SetParent(prefabRoot.transform, false);
                    var filter = batchObject.AddComponent<MeshFilter>(); filter.sharedMesh = saved;
                    var renderer = batchObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key.Material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    var batch = batchObject.AddComponent<ShipV3RenderBatch>();
                    batch.Sources = chunk; batch.CachedMesh = saved;
                    batch.SharedMaterial = group.Key.Material; batch.BatchAnchor = prefabRoot.transform;
                    foreach (var source in chunk) source.enabled = false;
                }
            }
            ConfigureChainInstances(prefabRoot, rig);
            foreach (var light in prefabRoot.GetComponentsInChildren<Light>(true)) ConfigureLanternShadowBudget(light);
            File.WriteAllText("Tools/ShipV3/GeometryBudgetStatus.txt", "Batched " + batchedSources + " stationary renderers into " + number + " groups; fragment colliders are lazy; " + rig.ChainLinks.Length + " chain links are instanced.");
            ShipV3PerformanceSetup.Configure(prefabRoot);
        }

        static void ConfigureChainInstances(GameObject prefabRoot, ShipV3VisualRig rig)
        {
            if (rig.ChainLinks == null || rig.ChainLinks.Length == 0) return;
            var first = rig.ChainLinks.First(t => t != null);
            var copy = new Material(first.GetComponent<MeshRenderer>().sharedMaterial) { name = "AnchorChainInstanced", enableInstancing = true };
            string path = Folder + "/Materials/AnchorChainInstanced.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { AssetDatabase.CreateAsset(copy, path); material = copy; }
            else { EditorUtility.CopySerialized(copy, material); UnityEngine.Object.DestroyImmediate(copy); EditorUtility.SetDirty(material); }
            foreach (var link in rig.ChainLinks)
            {
                if (link == null) continue;
                var renderer = link.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.enabled = true;
            }
            var instances = prefabRoot.GetComponent<ShipV3ChainInstances>();
            if (instances == null) instances = prefabRoot.AddComponent<ShipV3ChainInstances>();
            instances.Links = rig.ChainLinks;
            instances.SharedMesh = first.GetComponent<MeshFilter>().sharedMesh;
            instances.SharedMaterial = material;
        }

        static void RemoveUnusedBatchMeshes(GameObject prefabRoot)
        {
            var used = new HashSet<string>(prefabRoot.GetComponentsInChildren<ShipV3RenderBatch>(true)
                .Select(batch => AssetDatabase.GetAssetPath(batch.CachedMesh)));
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { Folder + "/RuntimeMeshes/Batches" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!used.Contains(path) && Path.GetFileName(path).StartsWith("ShipV3Batch_", StringComparison.Ordinal))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        static void ConfigureLanternShadowBudget(Light light)
        {
            light.shadows = LightShadows.Soft;
            var data = light.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            if (data == null) data = light.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            var serialized = new SerializedObject(data);
            serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue =
                UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("PirateSlop/Repair Ship V3 Lantern Lighting")]
        public static void RepairLanternLighting()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before repair.");
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigureLanternLighting(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
        }

        static void ConfigureLanternLighting(GameObject prefabRoot)
        {
            var lamps = prefabRoot.GetComponent<ShipV3Features>().Lanterns;
            foreach (var lamp in lamps)
            {
                HandLanternSetup.RepairPaneMesh(lamp);
                if (lamp.Light != null)
                {
                    var light = lamp.Light;
                    light.color = new Color(1f, .57f, .24f);
                    light.intensity = ShipV3Features.LanternIntensity;
                    light.range = ShipV3Features.LanternRange;
                    ConfigureLanternShadowBudget(light);
                    light.shadows = LightShadows.None;
                    light.shadowStrength = .65f;
                    light.shadowNearPlane = .06f;
                    light.shadowBias = .025f;
                    light.shadowNormalBias = .08f;
                }
                if (lamp.Glass != null && lamp.GlassSlot >= 0 && lamp.GlassSlot < lamp.Glass.sharedMaterials.Length)
                    ConfigureLanternGlass(lamp.Glass.sharedMaterials[lamp.GlassSlot]);
            }
        }

        static void ConfigureLanternGlass(Material material)
        {
            if (material == null || material.name != "V3_Lantern_AmberGlass_Emissive") return;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", .45f);
            material.SetColor("_BaseColor", new Color(1f, .92f, .8f, .22f));
            material.SetColor("_EmissionColor", ShipV3Features.LanternEmission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            BaseShaderGUI.SetMaterialKeywords(material);
            material.EnableKeyword("_EMISSION");
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
        }

        static Vector3 Vector(JToken value) => new((float)value[0], (float)value[1], (float)value[2]);
        static Vector3 Point(JToken value) => basis.MultiplyPoint3x4(Vector(value));
        static Transform Find(string name) => objects.TryGetValue(name, out var value) ? value : null;
        static Transform Require(string name) => Find(name) ?? throw new InvalidOperationException("Missing imported object: " + name);
        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var scale = parent.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
            return go.transform;
        }
        static void Ref(UnityEngine.Object component, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException("Missing field " + field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Number(UnityEngine.Object component, string field, float value)
        {
            var serialized = new SerializedObject(component); serialized.FindProperty(field).floatValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Refs(UnityEngine.Object component, string field, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(component); var property = serialized.FindProperty(field); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static string Property(JObject record, string key)
        {
            var item = record["properties"].FirstOrDefault(value => (string)value["key"] == key);
            return item == null ? "" : item["value"].ToString();
        }
        static Bounds Bounds(Transform target)
        {
            var filter = target.GetComponent<MeshFilter>();
            if (filter != null) return filter.sharedMesh.bounds;
            var skin = target.GetComponent<SkinnedMeshRenderer>();
            return skin != null ? skin.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * .1f);
        }
        static BoxCollider Box(Transform target)
        {
            var bounds = Bounds(target); var collider = target.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center; collider.size = bounds.size; return collider;
        }
        static MeshCollider MeshCollider(Transform target, bool convex = false)
        {
            var collider = target.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = target.GetComponent<MeshFilter>().sharedMesh; collider.convex = convex; return collider;
        }
        static void Target(Transform target, ShipV3TargetKind kind, int index = 0)
        {
            var marker = target.gameObject.AddComponent<ShipV3InteractionTarget>();
            marker.Ship = features; marker.Kind = kind; marker.Index = index;
        }
        static Rigidbody Body(Transform target, float mass, bool hanging)
        {
            var body = target.GetComponent<Rigidbody>();
            if (body == null) body = target.gameObject.AddComponent<Rigidbody>();
            body.mass = mass; body.linearDamping = .12f; body.angularDamping = .25f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            bodies.Add(body); pendulums.Add(hanging); return body;
        }
        static void Joint(Rigidbody body, Vector3 anchor, float limit, Rigidbody connected = null)
        {
            var joint = body.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connected != null ? connected : root.GetComponent<Rigidbody>();
            joint.axis = body.transform.InverseTransformDirection(Vector3.right);
            joint.secondaryAxis = body.transform.InverseTransformDirection(Vector3.up);
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = body.transform.InverseTransformPoint(anchor);
            joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(anchor);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.lowAngularXLimit = new SoftJointLimit { limit = -limit };
            joint.highAngularXLimit = new SoftJointLimit { limit = limit };
            joint.angularZLimit = new SoftJointLimit { limit = limit };
            joint.angularXLimitSpring = joint.angularYZLimitSpring = new SoftJointLimitSpring { spring = 3, damper = .8f };
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = .03f; joint.projectionAngle = 4f;
        }

        static void ImportMaterials()
        {
            materials = new Dictionary<string, Material>();
            foreach (JObject record in document["materials"])
            {
                string name = (string)record["name"];
                string path = Folder + "/Materials/" + name.Replace('/', '_') + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
                var color = record["color"];
                material.SetColor("_BaseColor", new Color((float)color[0], (float)color[1], (float)color[2], (float)color[3]));
                material.SetFloat("_Metallic", (float)record["metallic"]);
                material.SetFloat("_Smoothness", 1f - (float)record["roughness"]);
                material.SetFloat("_Cull", name.Contains("Canvas") || name.Contains("Flag") ? 0 : 2);
                foreach (JObject texture in record["textures"])
                {
                    string channel = (string)texture["channel"], texturePath = (string)texture["path"];
                    var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (textureImporter == null) throw new InvalidOperationException("Texture missing: " + texturePath);
                    bool changed = false;
                    if (channel == "Normal" && textureImporter.textureType != TextureImporterType.NormalMap) { textureImporter.textureType = TextureImporterType.NormalMap; changed = true; }
                    if (channel != "Base Color" && channel != "Emission Color" && textureImporter.sRGBTexture) { textureImporter.sRGBTexture = false; changed = true; }
                    changed |= ApplyRuntimeTextureBudget(textureImporter, channel == "Base Color" || channel == "Emission Color");
                    if (changed) textureImporter.SaveAndReimport();
                    if (channel != "Base Color" && channel != "Normal" && channel != "MetallicGloss") continue;
                    var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (channel == "Base Color") material.SetTexture("_BaseMap", asset);
                    else if (channel == "Normal") { material.SetTexture("_BumpMap", asset); material.SetFloat("_BumpScale", .65f); material.EnableKeyword("_NORMALMAP"); }
                    else if (channel == "MetallicGloss") { material.SetTexture("_MetallicGlossMap", asset); material.SetFloat("_Smoothness", 1f); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                }
                if (name.Contains("Glass"))
                {
                    material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", new Color(.65f, .22f, .045f));
                }
                ConfigureLanternGlass(material);
                materials.Add(name, material); EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        static bool ApplyRuntimeTextureBudget(TextureImporter importer, bool colour)
        {
            int limit = colour ? 2048 : 1024;
            bool changed = false;
            if (importer.maxTextureSize != limit) { importer.maxTextureSize = limit; changed = true; }
            if (importer.isReadable) { importer.isReadable = false; changed = true; }
            if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
            if (!importer.streamingMipmaps) { importer.streamingMipmaps = true; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            { importer.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
            var platform = importer.GetPlatformTextureSettings("Standalone");
            if (!platform.overridden || platform.maxTextureSize != limit || platform.format != TextureImporterFormat.Automatic ||
                platform.textureCompression != TextureImporterCompression.CompressedHQ || platform.crunchedCompression)
            {
                platform.overridden = true; platform.maxTextureSize = limit;
                platform.format = TextureImporterFormat.Automatic;
                platform.textureCompression = TextureImporterCompression.CompressedHQ;
                platform.crunchedCompression = false;
                importer.SetPlatformTextureSettings(platform); changed = true;
            }
            return changed;
        }

        [MenuItem("PirateSlop/Optimize Ship V3 Runtime Textures")]
        public static void OptimizeRuntimeTextures()
        {
            OptimizeRuntimeTextureBatch(0, int.MaxValue);
        }

        public static int OptimizeRuntimeTextureBatch(int start, int count)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before texture import.");
            var source = JObject.Parse(File.ReadAllText(Folder + "/ShipV3.json"));
            var colourPaths = source["materials"].SelectMany(material => material["textures"])
                .Where(texture => (string)texture["channel"] == "Base Color" || (string)texture["channel"] == "Emission Color")
                .Select(texture => (string)texture["path"]).ToHashSet();
            var paths = AssetDatabase.GetDependencies(PrefabPath, true)
                .Where(path => path.StartsWith(Folder + "/Textures/") && AssetImporter.GetAtPath(path) is TextureImporter).OrderBy(path => path).ToArray();
            int end = (int)Math.Min(paths.Length, (long)start + count);
            for (int i = start; i < end; i++)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(paths[i]);
                if (ApplyRuntimeTextureBudget(importer, colourPaths.Contains(paths[i]))) importer.SaveAndReimport();
                File.WriteAllText("Tools/ShipV3/TextureBudgetStatus.txt", "Imported " + (i + 1) + "/" + paths.Length);
            }
            AssetDatabase.SaveAssets();
            return paths.Length;
        }

        static void ConfigureSections()
        {
            sections = new Dictionary<int, ShipDamageSection>();
            foreach (JObject piece in document["pieces"])
            {
                int id = (int)piece["piece_id"];
                var intact = Require((string)piece["intact"]);
                var parent = Child(intact.parent, "V3_Piece_" + id);
                parent.SetPositionAndRotation(intact.position, intact.rotation);
                intact.SetParent(parent, true);
                var section = parent.gameObject.AddComponent<ShipDamageSection>();
                section.SectionId = id + 10000;
                section.Intact = intact.gameObject;
                var collider = MeshCollider(intact);
                section.GameplayColliders = new Collider[] { collider };
                var fragments = new List<GameObject>();
                foreach (string name in piece["fragment_names"].Values<string>())
                {
                    var fragment = Require(name);
                    fragment.SetParent(parent, true);
                    var fragmentCollider = MeshCollider(fragment);
                    fragment.gameObject.SetActive(false); fragments.Add(fragment.gameObject);
                }
                section.Fragments = fragments.ToArray();
                section.DamageColliders = new Collider[] { collider }.Concat(fragments.Select(item => item.GetComponent<Collider>())).ToArray();
                section.SafeColliderReplacement = true;
                sections.Add(id, section);
            }
            foreach (var record in records.Values)
            {
                string name = (string)record["name"];
                if ((bool)record["geometry"] && !sections.Values.Any(section => section.Intact.name == name) &&
                    Find(name)?.GetComponent<MeshFilter>() != null && (name.Contains("Nest") || name.Contains("Platform") || name.Contains("Step") || name.Contains("Dispenser") || name.Contains("Doorway")))
                    MeshCollider(Find(name));
            }
            var destruction = root.AddComponent<ShipDestruction>();
            destruction.Sections = sections.Values.ToArray();
            root.AddComponent<ShipFlooding>(); root.AddComponent<ShipDestructionVisuals>(); root.AddComponent<ShipDebrisPool>();
        }

        static void ConfigureControls(ShipController motor)
        {
            var wheel = Require("V3_Transfer_S029_Helm_P0554_Intact");
            var wheelPivot = Child(wheel.parent, "HelmWheelPivot");
            wheelPivot.position = wheel.TransformPoint(Bounds(wheel).center);
            wheelPivot.rotation = root.transform.rotation;
            wheel.SetParent(wheelPivot, true);
            var helm = wheelPivot.gameObject.AddComponent<HelmInteraction>(); helm.Configure(wheelPivot);
            var wheelCollider = wheel.GetComponent<Collider>();
            var handle = wheel.gameObject.AddComponent<ShipControlHandle>(); handle.Helm = helm;
            var sails = root.GetComponent<SailSystem>(); sails.ExternalVisualRig = true;
            string[] tags = { "Main_Course", "Main_Topsail", "Fore_Course", "Fore_Topsail", "Mizzen" };
            var sailObjects = tags.Select(tag => Require("V3_Transfer_Outfit_Sail_" + tag)).ToArray();
            Refs(sails, "sailMeshes", sailObjects);
            sails.RopeHandles = new ShipControlHandle[tags.Length];
            sails.RopeNames = new[] { "Главный нижний парус", "Главный верхний парус", "Носовой нижний парус", "Носовой верхний парус", "Бизань" };
            visual.Sails = sailObjects.Select(t => t.GetComponent<SkinnedMeshRenderer>()).ToArray();
            var rigMeshes = new List<SkinnedMeshRenderer>(); var rigIndices = new List<int>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                for (int i = 0; i < tags.Length; i++)
                    if (renderer.name.Contains(tags[i]) && !renderer.name.Contains("Flag") || i == 4 && renderer.name.Contains("Mizzen") && !renderer.name.Contains("Flag"))
                    { rigMeshes.Add(renderer); rigIndices.Add(i); break; }
            visual.RigMeshes = rigMeshes.ToArray(); visual.RigSailIndices = rigIndices.ToArray();
            for (int i = 0; i < tags.Length; i++)
            {
                var pivot = Require("V3_Transfer_Control_" + tags[i] + "_LeverPivot");
                var grip = Child(pivot, "SailLeverGrip");
                var ropeAnchor = Require("V3_Transfer_Control_" + tags[i] + "_RopeAnchor");
                grip.position = ropeAnchor.position;
                var collider = grip.gameObject.AddComponent<SphereCollider>(); collider.radius = .15f;
                var sailHandle = grip.gameObject.AddComponent<ShipControlHandle>(); sailHandle.Sails = sails; sailHandle.RopeIndex = i;
                sails.RopeHandles[i] = sailHandle;
            }
            Ref(motor, "sailSystem", sails); Ref(motor, "helm", helm);
            var capstan = Require("V9_AnchorSystem_Control").gameObject.AddComponent<CapstanStation>();
            foreach (int side in new[] { -1, 1 })
            {
                var grip = Require("V9_Capstan_Player_Grip_" + side);
                var collider = grip.gameObject.AddComponent<SphereCollider>(); collider.radius = .14f / Mathf.Abs(grip.lossyScale.x);
                var capstanHandle = grip.gameObject.AddComponent<ShipControlHandle>(); capstanHandle.Capstan = capstan; capstanHandle.SpokeIndex = side == -1 ? 0 : 1;
            }
            var capstanGrip = Child(capstan.transform, "CapstanBodyGrip"); capstanGrip.position = capstan.transform.position + Vector3.up * .45f;
            var capstanCollider = capstanGrip.gameObject.AddComponent<SphereCollider>(); capstanCollider.radius = .55f;
            Ref(capstan, "bodyCollider", capstanCollider);
            var motions = new List<ShipV3Motion>();
            foreach (var source in document["sail_samples"].Concat(document["anchor_samples"]))
            {
                var target = Require((string)source["name"]);
                var movingSection = target.GetComponentInParent<ShipDamageSection>();
                if (movingSection != null && movingSection.Intact.transform == target) target = movingSection.transform;
                var samples = source["samples"].ToArray();
                var baseSource = samples[(int)source["index"] < 0 ? 0 : samples.Length - 1];
                Vector3 basePosition = Vector(baseSource["position"]);
                var baseRotation = new Quaternion((float)baseSource["rotation"][1], (float)baseSource["rotation"][2], (float)baseSource["rotation"][3], (float)baseSource["rotation"][0]);
                Quaternion baseline = target.localRotation;
                var poses = new List<ShipV3Pose>();
                foreach (var sample in samples)
                {
                    var rotation = new Quaternion((float)sample["rotation"][1], (float)sample["rotation"][2], (float)sample["rotation"][3], (float)sample["rotation"][0]);
                    var relative = axes * Matrix4x4.Rotate(rotation * Quaternion.Inverse(baseRotation)) * axes.inverse;
                    var parentRotation = target.parent != null ? target.parent.rotation : Quaternion.identity;
                    var delta = axes.MultiplyVector(Vector(sample["position"]) - basePosition);
                    poses.Add(new ShipV3Pose { Position = target.localPosition + (target.parent != null ? target.parent.InverseTransformVector(delta) : delta), Rotation = Quaternion.Inverse(parentRotation) * relative.rotation * parentRotation * baseline, Scale = target.localScale });
                }
                motions.Add(new ShipV3Motion { Target = target, SailIndex = (int)source["index"], Poses = poses.ToArray() });
            }
            visual.Motions = motions.ToArray();
            visual.Rudder = Require("V5_Rudder_Pivot");
            visual.RudderAxis = visual.Rudder.InverseTransformDirection(Vector3.up);
            ConfigureChain();
        }

        static Vector3 DeckSpawn()
        {
            var candidates = sections.Values.Where(section => section.Intact.name.Contains("Deck") && !section.Intact.name.Contains("SternRoom"));
            float score = float.MaxValue; Vector3 best = new(0, 4.5f, 0);
            foreach (var section in candidates)
            {
                var bounds = section.Intact.GetComponent<Renderer>().bounds; Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                float candidate = new Vector2(top.x, top.z).sqrMagnitude;
                if (candidate < score) { score = candidate; best = top + Vector3.up * .2f; }
            }
            return best;
        }

        static void ConfigureLanterns()
        {
            var lamps = new List<ShipV3Lantern>();
            foreach (string region in new[] { "Bow", "Stern", "Hold" })
                foreach (string side in new[] { "Port", "Starboard" })
                {
                    string prefix = "V3_Lamp_" + region + "_" + side;
                    var pivot = Require(prefix + "_Pivot"); var bodyMesh = Require(prefix + "_Body");
                    var body = Body(pivot, 2f, true); Joint(body, pivot.position, 12f);
                    var collider = bodyMesh.gameObject.AddComponent<BoxCollider>(); var bounds = Bounds(bodyMesh);
                    collider.center = bounds.center; collider.size = bounds.size * .75f;
                    Target(bodyMesh, ShipV3TargetKind.Lantern, lamps.Count);
                    var socket = Require(prefix + "_LightSocket"); var light = socket.gameObject.AddComponent<Light>();
                    light.type = LightType.Point; light.color = new Color(1, .59f, .25f); light.intensity = .72f; light.range = 4.5f;
                    ConfigureLanternShadowBudget(light); light.shadowStrength = .55f; light.shadowBias = .05f;
                    var renderer = bodyMesh.GetComponent<Renderer>();
                    int slot = Array.FindIndex(renderer.sharedMaterials, material => material != null && material.name.Contains("Glass"));
                    lamps.Add(new ShipV3Lantern { Light = light, Glass = renderer, GlassSlot = Mathf.Max(0, slot), Grip = bodyMesh });
                }
            features.Lanterns = lamps.ToArray();
        }

        static void ConfigureBell()
        {
            var bellPivot = Require("V5_MainMast_Bell_SwingPivot");
            var clapperPivot = Require("V5_MainMast_Bell_ClapperPivot");
            var shell = Require("V8_Bell_Body"); var clapper = Require("V8_Bell_Clapper");
            var bellBody = Body(bellPivot, 8f, true); Joint(bellBody, bellPivot.position, 10f);
            var clapperBody = Body(clapperPivot, .7f, true); Joint(clapperBody, clapperPivot.position, 30f, bellBody);
            var bellContact = bellPivot.gameObject.AddComponent<ShipV3BellContact>(); bellContact.Ship = features; bellContact.Clapper = clapperBody;
            var shellBounds = shell.GetComponent<Renderer>().bounds; var clapperBounds = clapper.GetComponent<Renderer>().bounds;
            var striker = Child(clapper, "BellClapperContact"); striker.position = new Vector3(clapperBounds.center.x, clapperBounds.min.y + clapperBounds.size.x * .4f, clapperBounds.center.z);
            var sphere = striker.gameObject.AddComponent<SphereCollider>();
            sphere.radius = Mathf.Max(.02f, Mathf.Min(clapperBounds.size.x, clapperBounds.size.z) * .4f);
            Vector3 center = shellBounds.center;
            float radius = Mathf.Max(shellBounds.extents.x, shellBounds.extents.z) * .82f;
            float height = shellBounds.size.y * .55f;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                var wall = Child(bellPivot, "BellInnerWall_" + i);
                wall.position = center + root.transform.TransformDirection(new Vector3(Mathf.Cos(angle) * radius, -height * .3f, Mathf.Sin(angle) * radius));
                wall.rotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
                var collider = wall.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(.035f, height, radius * .43f);
            }
            var grip = Require("V5_MainMast_Bell_PullTarget");
            var gripCollider = grip.gameObject.AddComponent<SphereCollider>(); gripCollider.radius = .1f / Mathf.Abs(grip.lossyScale.x);
            Target(grip, ShipV3TargetKind.Bell);
            features.BellClapper = clapperBody; features.BellGrip = grip;
        }

        static void ConfigureDoor()
        {
            ShipV3BindingRepair.RemoveDoor(root);
        }

        static void ConfigureDice()
        {
            var station = Require("V17_Dice_Game_Station");
            var barrel = Require("V17_Dice_Game_Barrel");
            features.DiceTable = Child(station, "DiceInteraction");
            var bounds = barrel.GetComponent<Renderer>().bounds; var top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            features.DiceTable.position = top;
            features.DiceTable.rotation = root.transform.rotation;
            var tableCollider = features.DiceTable.gameObject.AddComponent<BoxCollider>(); tableCollider.size = new Vector3(.8f, .06f, .8f);
            var physics = new PhysicsMaterial("DiceTableFriction") { dynamicFriction = .55f, staticFriction = .6f, bounciness = .12f };
            var savedPhysics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/DiceTablePhysics.asset");
            if (savedPhysics == null) { AssetDatabase.CreateAsset(physics, Folder + "/DiceTablePhysics.asset"); savedPhysics = physics; }
            else UnityEngine.Object.DestroyImmediate(physics);
            tableCollider.sharedMaterial = savedPhysics;
            Target(features.DiceTable, ShipV3TargetKind.Dice);
            features.DiceView = Child(station, "DiceCameraView");
            features.DiceView.position = top + Vector3.up * 1.8f;
            features.DiceView.rotation = Quaternion.LookRotation(Vector3.down, root.transform.forward);
            var slots = new List<ShipV3DiceSlot>();
            for (int slot = 1; slot <= 3; slot++)
            {
                var cup = Require("V17_Player_" + slot + "_Wooden_Cup");
                var cupBounds = Bounds(cup); var cupBody = Body(cup, .25f, false); cupBody.isKinematic = true;
                var worldBounds = cup.GetComponent<Renderer>().bounds;
                float radius = Mathf.Max(cupBounds.extents.x, cupBounds.extents.z);
                var baseTransform = Child(cup, "CupBottom"); baseTransform.position = new Vector3(worldBounds.center.x, worldBounds.min.y + .008f, worldBounds.center.z); baseTransform.rotation = Quaternion.identity;
                var bottom = baseTransform.gameObject.AddComponent<BoxCollider>();
                bottom.size = new Vector3(worldBounds.size.x * .75f, .016f, worldBounds.size.z * .75f);
                float worldRadius = Mathf.Max(worldBounds.extents.x, worldBounds.extents.z);
                for (int wallIndex = 0; wallIndex < 10; wallIndex++)
                {
                    float angle = wallIndex * Mathf.PI * 2 / 10;
                    var wall = Child(cup, "CupWall_" + wallIndex);
                    wall.position = worldBounds.center + new Vector3(Mathf.Cos(angle) * worldRadius * .87f, 0, Mathf.Sin(angle) * worldRadius * .87f);
                    wall.rotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
                    var collider = wall.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(.009f, worldBounds.size.y, worldRadius * .68f);
                }
                var dice = new List<Rigidbody>();
                Vector3[] faceNormals = null; int[] faceValues = null;
                for (int die = 1; die <= 5; die++)
                {
                    var mesh = Require("V17_Player_" + slot + "_Die_" + die.ToString("00"));
                    var body = Body(mesh, .025f, false); var collider = Box(mesh); collider.sharedMaterial = savedPhysics;
                    body.maxAngularVelocity = 30f; body.linearDamping = .18f; body.angularDamping = .35f;
                    dice.Add(body);
                    if (faceNormals == null) DiceFaces(mesh.GetComponent<MeshFilter>().sharedMesh, out faceNormals, out faceValues);
                }
                slots.Add(new ShipV3DiceSlot { Cup = cupBody, Dice = dice.ToArray(), FaceNormals = faceNormals, FaceValues = faceValues,
                    RestCup = root.transform.InverseTransformPoint(cup.position), RestRotation = Quaternion.Inverse(root.transform.rotation) * cup.rotation });
            }
            features.DiceSlots = slots.ToArray();
        }

        static void DiceFaces(Mesh mesh, out Vector3[] normals, out int[] values)
        {
            normals = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            values = new int[6];
            var vertices = mesh.vertices;
            int[] parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Root(int index) { while (parent[index] != index) index = parent[index]; return index; }
            if (mesh.subMeshCount < 2) throw new InvalidOperationException("Dice pip material is missing.");
            var triangles = mesh.GetTriangles(1);
            var used = new HashSet<int>();
            var welded = new Dictionary<Vector3Int, int>();
            foreach (int index in triangles.Distinct())
            {
                Vector3 v = vertices[index] * 100000000f;
                var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                if (welded.TryGetValue(key, out int other)) parent[Root(index)] = Root(other);
                else welded[key] = index;
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int first = Root(triangles[i]);
                for (int offset = 0; offset < 3; offset++) { parent[Root(triangles[i + offset])] = first; used.Add(triangles[i + offset]); }
            }
            var groups = used.GroupBy(Root);
            foreach (var group in groups)
            {
                Vector3 center = Vector3.zero;
                foreach (int index in group) center += vertices[index];
                center = center / group.Count() - mesh.bounds.center;
                int best = 0; float dot = -2;
                for (int i = 0; i < normals.Length; i++)
                {
                    float score = Vector3.Dot(normals[i], center.normalized);
                    if (score > dot) { dot = score; best = i; }
                }
                values[best]++;
            }
            if (!values.OrderBy(value => value).SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 }))
                throw new InvalidOperationException("Cannot infer physical dice face numbers: " + string.Join(",", values));
        }

        static void ConfigureHarpoons()
        {
            var mount = root.AddComponent<HarpoonShipMount>();
            var old = AssetDatabase.LoadAssetAtPath<HarpoonGun>("Assets/Prefabs/Cannons/HarpoonGun.prefab");
            var oldSerialized = new SerializedObject(old);
            var oldProjectile = (HarpoonProjectile)oldSerialized.FindProperty("projectilePrefab").objectReferenceValue;
            foreach (string side in new[] { "Port", "Starboard" })
            {
                var anchor = Require("V10_Harpoon_" + side + "_Mount");
                var gun = anchor.gameObject.AddComponent<HarpoonGun>();
                EditorUtility.CopySerialized(old, gun);
                var yaw = Require("V10_Harpoon_" + side + "_Yaw");
                var pitch = Require("V10_Harpoon_" + side + "_Pitch");
                var reel = Require("V10_Harpoon_" + side + "_Reel");
                var projectile = Require("V10_Harpoon_" + side + "_Projectile");
                var muzzle = Require("V10_" + side + "_Muzzle");
                Vector3 launchDirection = projectile.up;
                var muzzleAdapter = Child(muzzle, "LaunchForward");
                muzzleAdapter.rotation = Quaternion.LookRotation(launchDirection, root.transform.up);
                var camera = Child(pitch, "HarpoonCameraMount");
                camera.position = muzzle.position - launchDirection * .65f + Vector3.up * .08f;
                camera.rotation = Quaternion.LookRotation(launchDirection, Vector3.up);
                var rope = anchor.gameObject.AddComponent<LineRenderer>(); rope.useWorldSpace = true;
                rope.startWidth = rope.endWidth = .018f; rope.positionCount = 32;
                rope.sharedMaterial = materials.Values.FirstOrDefault(material => material.name.Contains("Rope")) ?? materials.Values.First();
                Ref(gun, "baseYaw", yaw); Ref(gun, "barrelPitch", pitch); Ref(gun, "muzzle", muzzleAdapter);
                Ref(gun, "winchDrum", reel); Ref(gun, "restingHarpoon", projectile); Ref(gun, "cameraMount", camera);
                Ref(gun, "drumAxle", reel); Ref(gun, "ropeRenderer", rope);
                Ref(gun, "mountSection", sections.Values.OrderBy(section => Vector3.SqrMagnitude(section.Intact.GetComponent<Renderer>().bounds.center - anchor.position)).First());
                gun.ExternalVisualRig = true;
                var importedProjectile = new GameObject("ShipV3HarpoonProjectile");
                importedProjectile.transform.SetParent(root.transform, false);
                importedProjectile.AddComponent<Rigidbody>();
                var behavior = importedProjectile.AddComponent<HarpoonProjectile>();
                EditorUtility.CopySerialized(oldProjectile, behavior);
                var projectileVisual = UnityEngine.Object.Instantiate(projectile.gameObject, importedProjectile.transform);
                projectileVisual.name = "Visual";
                projectileVisual.transform.localPosition = Vector3.zero;
                projectileVisual.transform.localRotation = Quaternion.identity;
                projectileVisual.transform.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                var meshBounds = Bounds(projectileVisual.transform);
                var projectileCollider = importedProjectile.AddComponent<CapsuleCollider>();
                projectileCollider.radius = .035f; projectileCollider.height = .7f; projectileCollider.direction = 2;
                var knot = Child(importedProjectile.transform, "RopeKnot"); knot.localPosition = new Vector3(0, 0, -.15f);
                Ref(behavior, "knotTransform", knot);
                string path = "Assets/Resources/Ships/ShipV3Harpoon" + side + ".prefab";
                var projectilePrefab = PrefabUtility.SaveAsPrefabAsset(importedProjectile, path);
                UnityEngine.Object.DestroyImmediate(importedProjectile);
                Ref(gun, "projectilePrefab", projectilePrefab.GetComponent<HarpoonProjectile>());
                var controller = anchor.gameObject.AddComponent<ShipV3HarpoonVisual>();
                controller.Gun = gun; controller.Reel = reel; controller.Rope = rope;
                controller.ReelExit = Require("V10_" + side + "_Free_Rope_ReelExit");
                controller.Guide = Require("V10_" + side + "_Free_Rope_Guide_03");
                controller.Knot = Require("V10_" + side + "_Projectile_Rope_Attach");
                foreach (var record in records.Values)
                    if (((string)record["name"]).StartsWith("V10_" + side + "_Free_Rope") && Find((string)record["name"])?.GetComponent<Renderer>() != null)
                        Find((string)record["name"]).GetComponent<Renderer>().enabled = false;
                var gunGrip = Child(anchor, "HarpoonInteractCollider"); gunGrip.position = pitch.position;
                var gunCollider = gunGrip.gameObject.AddComponent<BoxCollider>(); gunCollider.size = new Vector3(.3f, .3f, .45f);
                if (side == "Port") mount.ExistingPort = gun; else mount.ExistingStarboard = gun;
            }
        }

        static void ConfigureDispenser(GameObject main)
        {
            var originalCrate = main.GetComponentInChildren<CannonballCrate>(true);
            var dispenser = Require("V15_Hold_Cannonball_Dispenser");
            var bounds = dispenser.GetComponent<Renderer>().bounds;
            features.DispenserMouth = Child(dispenser, "CannonballMouth");
            features.DispenserMouth.position = new Vector3(bounds.center.x, bounds.center.y + bounds.size.y * .12f, bounds.center.z + bounds.size.z * .15f);
            features.DispenserDirection = Vector3.back;
            features.CannonballPrefab = originalCrate.SpecialSupplyPrefab;
            var crate = Child(dispenser.parent, "DispenserSupplyManager").gameObject.AddComponent<CannonballCrate>();
            crate.DispenserManaged = true; crate.SpawnPoint = features.DispenserMouth;
            crate.CannonPrefab = originalCrate.CannonPrefab; crate.MortarPrefab = null; crate.SpecialSupplyPrefab = originalCrate.SpecialSupplyPrefab;
            var supply = UnityEngine.Object.Instantiate(originalCrate.Supply.gameObject, crate.transform);
            crate.Supply = supply.GetComponent<Cannonball>(); supply.SetActive(false);
            crate.Kit = UnityEngine.Object.Instantiate(originalCrate.Kit, crate.transform);
            crate.Kit.SetActive(false);
            foreach (var pickup in crate.Kit.GetComponentsInChildren<CannonPickup>(true)) pickup.Crate = crate;
            root.AddComponent<NetworkCannon>();
        }

        static void ConfigureSupports()
        {
            var attachments = new List<ShipV3Attachment>();
            foreach (var record in document["attachments"]["objects"])
            {
                if ((bool)record["wooden"]) continue;
                var target = Find((string)record["object"]);
                if (target == null) continue;
                var supportGroups = new List<ShipV3Support>();
                var endpoints = record["endpoint_groups"] as JArray;
                IEnumerable<JToken> groups = endpoints != null && endpoints.Count > 0 ? endpoints.Select(item => item["anchors"]) : new[] { record["anchors"] };
                foreach (var group in groups)
                {
                    if (group == null) continue;
                    var supportSections = new List<ShipDamageSection>(); var fragments = new List<int>();
                    foreach (var anchor in group)
                    {
                        if (!sections.TryGetValue((int)anchor["piece_id"], out var section)) continue;
                        var fragment = section.Fragments.Select((item, index) => new { item, index }).FirstOrDefault(pair => pair.item.name == (string)anchor["object"]);
                        if (fragment == null) continue;
                        supportSections.Add(section); fragments.Add(fragment.index);
                    }
                    if (supportSections.Count > 0) supportGroups.Add(new ShipV3Support { Sections = supportSections.ToArray(), Fragments = fragments.ToArray() });
                }
                attachments.Add(new ShipV3Attachment { Object = target, Supports = supportGroups.ToArray(),
                    Dependencies = record["depends_on_objects"].Values<string>().Concat(endpoints != null ? endpoints.Where(item => item["objects"] != null).SelectMany(item => item["objects"].Values<string>()) : Enumerable.Empty<string>()).Distinct().Select(Find).Where(t => t != null).ToArray(),
                    Fall = (string)record["on_support_loss"] == "DetachAndFall" });
            }
            features.Attachments = attachments.ToArray();
        }

        static void ConfigureProfile()
        {
            string path = "Assets/Settings/ShipDestruction/ShipV3Destruction.asset";
            var profile = AssetDatabase.LoadAssetAtPath<ShipDestructionProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<ShipDestructionProfile>(); AssetDatabase.CreateAsset(profile, path); }
            profile.DamageAdjacentFragments = true;
            var definitions = new List<ShipSectionDefinition>();
            var fragmentNodes = new List<ShipFragmentConnection>();
            var fragmentIndexes = new Dictionary<string, int>();
            var supportedSails = new Dictionary<int, HashSet<string>>();
            foreach (var sail in visual.Sails)
                foreach (var attachment in document["attachments"]["objects"].Where(item => (string)item["object"] == sail.name))
                    foreach (var anchor in attachment["anchors"])
                    {
                        int pieceId = (int)anchor["piece_id"];
                        if (!supportedSails.TryGetValue(pieceId, out var names)) supportedSails[pieceId] = names = new HashSet<string>();
                        names.Add(sail.name);
                    }
            foreach (JObject piece in document["pieces"])
            {
                int id = (int)piece["piece_id"]; string family = (string)piece["family"], name = (string)piece["intact"];
                var type = family == "Hull" ? ShipSectionType.Hull : family == "Deck" ? ShipSectionType.Deck : family == "Rails" ? ShipSectionType.Railing :
                    name.Contains("Mast") ? ShipSectionType.Mast : name.Contains("Rudder") ? ShipSectionType.Rudder : name.Contains("Yard") ? ShipSectionType.Yard :
                    name.Contains("Helm") ? ShipSectionType.Helm : name.Contains("Capstan") ? ShipSectionType.Capstan : name.Contains("Stair") || name.Contains("Step") ? ShipSectionType.Stairs : ShipSectionType.Fitting;
                definitions.Add(new ShipSectionDefinition { SectionId = id + 10000, Name = name, SourceGroup = name, Type = type, Repairable = true,
                    MaxHealth = type == ShipSectionType.Hull ? 130 : 90, CanFlood = type == ShipSectionType.Hull, BreachArea = .06f,
                    BreachAnchor = root.transform.InverseTransformPoint(sections[id].Intact.transform.TransformPoint(Bounds(sections[id].Intact.transform).center)),
                    DebrisMass = 8f, MaxDebris = 3, DebrisLifetime = 15f,
                    SailNames = supportedSails.TryGetValue(id, out var sailNames) ? sailNames.ToArray() : Array.Empty<string>() });
                for (int i = 0; i < sections[id].Fragments.Length; i++)
                {
                    fragmentIndexes.Add(sections[id].Fragments[i].name, fragmentNodes.Count);
                    fragmentNodes.Add(new ShipFragmentConnection { SectionId = id + 10000, Fragment = i });
                }
            }
            var adjacent = Enumerable.Range(0, fragmentNodes.Count).Select(_ => new HashSet<int>()).ToArray();
            foreach (var connection in document["structure"]["connections"])
            {
                if (!fragmentIndexes.TryGetValue((string)connection["a"]["object"], out int first) || !fragmentIndexes.TryGetValue((string)connection["b"]["object"], out int second)) continue;
                adjacent[first].Add(second); adjacent[second].Add(first);
            }
            foreach (var foundation in document["structure"]["foundation_fragments"])
                if (fragmentIndexes.TryGetValue((string)foundation["object"], out int index)) fragmentNodes[index].Anchor = true;
            for (int i = 0; i < fragmentNodes.Count; i++) fragmentNodes[i].Neighbours = adjacent[i].ToArray();
            profile.Sections = definitions.ToArray(); profile.Structure = fragmentNodes.ToArray();
            profile.FallbackSectionId = definitions.First(definition => definition.Type == ShipSectionType.Hull).SectionId;
            profile.EnableFlooding = true; profile.OrdinaryCannonballsOnly = true;
            profile.SplintersPerHit = 0; profile.PhysicalLimit = 18; profile.CosmeticLimit = 64;
            root.GetComponent<ShipDestruction>().Profile = profile;
            EditorUtility.SetDirty(profile);
        }

        static Mesh StoreMesh(Mesh mesh, string name)
        {
            string path = Folder + "/RuntimeMeshes/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); return saved;
        }

        static void ConfigureChain()
        {
            var chain = objects.Values.FirstOrDefault(t => t.GetComponent<MeshFilter>() != null && t.name.StartsWith("V9_") && t.name.Contains("Chain") && !t.name.Contains("Capstan"));
            if (chain == null) throw new InvalidOperationException("Evaluated anchor chain mesh is missing.");
            var source = chain.GetComponent<MeshFilter>().sharedMesh;
            var triangles = source.triangles; var vertices = source.vertices;
            var neighbours = new Dictionary<int, List<int>>();
            for (int i = 0; i < triangles.Length; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    int index = triangles[i + j];
                    if (!neighbours.TryGetValue(index, out var next)) neighbours[index] = next = new List<int>();
                    next.Add(triangles[i + (j + 1) % 3]); next.Add(triangles[i + (j + 2) % 3]);
                }
            var welded = new Dictionary<Vector3Int, int>();
            foreach (int index in neighbours.Keys.ToArray())
            {
                Vector3 vertex = vertices[index] * 100000000f;
                var key = new Vector3Int(Mathf.RoundToInt(vertex.x), Mathf.RoundToInt(vertex.y), Mathf.RoundToInt(vertex.z));
                if (welded.TryGetValue(key, out int other)) { neighbours[index].Add(other); neighbours[other].Add(index); }
                else welded[key] = index;
            }
            var component = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(triangles[0]);
            while (queue.Count > 0)
            {
                int index = queue.Dequeue(); if (!component.Add(index)) continue;
                foreach (int next in neighbours[index]) if (!component.Contains(next)) queue.Enqueue(next);
            }
            var ids = component.OrderBy(index => index).ToArray(); var map = ids.Select((id, index) => new { id, index }).ToDictionary(pair => pair.id, pair => pair.index);
            var bounds = new Bounds(vertices[ids[0]], Vector3.zero); foreach (int id in ids) bounds.Encapsulate(vertices[id]);
            Vector3 longAxis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? Vector3.right : bounds.size.y >= bounds.size.z ? Vector3.up : Vector3.forward;
            var rotate = Quaternion.FromToRotation(longAxis, Vector3.forward);
            var mesh = new Mesh { name = "ShipV3ChainLink" };
            mesh.vertices = ids.Select(id => rotate * Vector3.Scale(vertices[id] - bounds.center, chain.lossyScale)).ToArray();
            if (source.uv.Length == vertices.Length) mesh.uv = ids.Select(id => source.uv[id]).ToArray();
            var faces = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
                if (map.ContainsKey(triangles[i])) for (int j = 0; j < 3; j++) faces.Add(map[triangles[i + j]]);
            mesh.triangles = faces.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (mesh.vertexCount < 24) throw new InvalidOperationException("Anchor chain link extraction is incomplete.");
            mesh = StoreMesh(mesh, "AnchorChainLink");
            chain.GetComponent<Renderer>().enabled = false;
            var links = new List<Transform>();
            var parent = Child(root.transform, "AnimatedAnchorChain");
            var material = chain.GetComponent<Renderer>().sharedMaterial;
            for (int i = 0; i < 145; i++)
            {
                var link = Child(parent, "ChainLink_" + i);
                link.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                link.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                links.Add(link);
            }
            visual.ChainLinks = links.ToArray();
            visual.Drum = Require("V9_Capstan_Rotation_Pivot");
            visual.AnchorEye = Require("V9_Anchor_Shackle_Pivot");
            var hawse = Require("V9_Anchor_Chain_Swing_Pivot");
            visual.ChainHawse = hawse;
            var route = objects.Values.Where(t => t.name.StartsWith("V9_") && t.name.Contains("Chain") &&
                (t.name.Contains("Guide") || t.name.Contains("Hawse") || t.name.Contains("DeckEntry")) && t.GetComponent<MeshFilter>() == null)
                .OrderBy(t => Vector3.Distance(t.position, visual.Drum.position)).Select(t => root.transform.InverseTransformPoint(t.position)).ToList();
            if (route.Count == 0) route.Add(root.transform.InverseTransformPoint(visual.Drum.position + Vector3.forward * .8f - Vector3.up * .25f));
            route.Add(root.transform.InverseTransformPoint(hawse.position));
            visual.ChainRoute = route.ToArray();
        }

        static void ConfigureWind()
        {
            foreach (var record in records.Values)
            {
                string name = (string)record["name"];
                if (!(bool)record["geometry"] || Find(name)?.GetComponent<Renderer>() == null) continue;
                bool cloth = name.Contains("Flag") && name.Contains("Cloth") || name.Contains("Net") && !name.Contains("Ear") && !name.Contains("Hook");
                if (!cloth) continue;
                var target = Find(name);
                var motion = target.gameObject.AddComponent<ShipV3ClothMotion>();
                motion.PinTop = name.Contains("Net"); motion.Strength = motion.PinTop ? .1f : .24f;
            }
            var anchor = Require("V9_Anchor_Shackle_Pivot");
            var anchorBody = Body(anchor, 95f, true); Joint(anchorBody, anchor.position, 12f);
            features.AnchorTravel = Require("V9_Anchor_Travel_Pivot");
            features.MovingAnchorJoint = anchorBody.GetComponent<ConfigurableJoint>();
        }
    }
}
