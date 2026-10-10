using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using PirateSlop.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static class ShipV3BilgeSetup
    {
        const string Folder = "Assets/Models/Ships/ShipV3/Bilge";
        const string ShipPath = "Assets/Resources/Ships/ShipV3Test.prefab";
        [MenuItem("PirateSlop/Add Bilge Deck And Pump To Ship V3")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            ConfigureImport();
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var player = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Networking/NetworkPlayer.prefab");
            try
            {
                if (player.GetComponent<ShipBilgePumpPlayer>() == null) player.AddComponent<ShipBilgePumpPlayer>();
                PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Networking/NetworkPlayer.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var config = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Networking/SessionConfig.asset"));
            var protocol = config.FindProperty("ProtocolVersion");
            if (protocol.intValue == 136) { protocol.intValue = 137; config.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssets();
            Debug.Log("Ship V3 bilge deck and pump saved. Gameplay validation remains manual.");
        }
        static void ConfigureImport()
        {
            Directory.CreateDirectory(Folder + "/Meshes");
            AssetDatabase.Refresh();
            ConfigureWaterProfile();
            foreach (string part in new[] { "Body", "Lever" })
            {
                ConfigureTexture(part + "_basecolor.jpeg", false, true);
                ConfigureTexture(part + "_normal.png", true, false);
                ConfigureTexture(part + "_MetallicSmoothness.png", false, false);
                string path = Folder + "/Pump" + part + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + part + "_basecolor.jpeg"));
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + part + "_normal.png"));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + part + "_MetallicSmoothness.png"));
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_Metallic", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                EditorUtility.SetDirty(material);
            }
            var importer = AssetImporter.GetAtPath(Folder + "/BilgeDeck.fbx") as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Bilge FBX unavailable.");
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.preserveHierarchy = true;
            importer.SaveAndReimport();
        }
        static void ConfigureWaterProfile()
        {
            var data = JObject.Parse(File.ReadAllText(Folder + "/InteriorWaterProfile.json"));
            int width = (int)data["size"][0], height = (int)data["size"][1];
            string path = Folder + "/InteriorWaterProfile.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool create = texture == null;
            if (create) texture = new Texture2D(width, height, TextureFormat.RFloat, false, true) { name = "ShipInteriorWidthProfile" };
            else texture.Reinitialize(width, height, TextureFormat.RFloat, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixelData(data["widths"].Values<float>().ToArray(), 0);
            texture.Apply(false, false);
            if (create) AssetDatabase.CreateAsset(texture, path);
            EditorUtility.SetDirty(texture);
        }
        static void ConfigureTexture(string filename, bool normal, bool srgb)
        {
            string path = Folder + "/Textures/" + filename;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Pump texture unavailable: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        public static void Apply(GameObject root)
        {
            if (!File.Exists(Folder + "/BilgeDeck.json")) return;
            if (AssetDatabase.LoadAssetAtPath<Material>(Folder + "/PumpBody.mat") == null) ConfigureImport();
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/InteriorWaterProfile.asset") == null) ConfigureWaterProfile();
            var manifest = JObject.Parse(File.ReadAllText(Folder + "/BilgeDeck.json"));
            var waterProfile = JObject.Parse(File.ReadAllText(Folder + "/InteriorWaterProfile.json"));
            var interior = root.GetComponent<ShipWaterInterior>();
            if (interior == null) interior = root.AddComponent<ShipWaterInterior>();
            interior.WidthProfile = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/InteriorWaterProfile.asset");
            interior.ProfileBounds = new Vector4((float)waterProfile["bounds"][0], (float)waterProfile["bounds"][1], (float)waterProfile["bounds"][2], (float)waterProfile["bounds"][3]);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/BilgeDeck.fbx");
            if (asset == null) throw new InvalidOperationException("Bilge FBX not imported.");
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject model = null;
            try
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var sources = model.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
                var origin = sources["BilgeOrigin"];
                var forward = (sources["BilgeForward"].position - origin.position).normalized;
                var up = (sources["BilgeUp"].position - origin.position).normalized;
                model.transform.rotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up)) * model.transform.rotation;
                if (Vector3.Dot(sources["BilgeRight"].position - origin.position, Vector3.right) < 0f)
                    model.transform.localScale = new Vector3(-model.transform.localScale.x, model.transform.localScale.y, model.transform.localScale.z);
                model.transform.position -= origin.position + Vector3.up * (float)manifest["waterline"];
                model.transform.SetParent(root.transform, false);
                var targets = root.GetComponentsInChildren<Transform>(true).Where(t => !t.IsChildOf(model.transform)).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var changed = new HashSet<MeshRenderer>();
                var restored = new HashSet<string>();
                var emptyColliders = new HashSet<Collider>();
                var emptyNames = new HashSet<string>(manifest["empty_objects"].Values<string>());
                foreach (string name in manifest["modified_objects"].Values<string>())
                {
                    if (!targets.TryGetValue(name, out var target)) throw new InvalidOperationException("Ship part not found: " + name);
                    var filter = target.GetComponent<MeshFilter>();
                    bool wasEmpty = filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0;
                    var mesh = emptyNames.Contains(name) ? new Mesh { name = name } : TransformMesh(sources[name].GetComponent<MeshFilter>().sharedMesh, target.worldToLocalMatrix * sources[name].localToWorldMatrix);
                    filter.sharedMesh = SaveMesh(mesh, name);
                    if (wasEmpty && filter.sharedMesh.vertexCount != 0) restored.Add(name);
                    foreach (var collider in target.GetComponents<MeshCollider>())
                    {
                        collider.sharedMesh = filter.sharedMesh.vertexCount != 0 ? filter.sharedMesh : null;
                        collider.enabled &= filter.sharedMesh.vertexCount != 0;
                        if (filter.sharedMesh.vertexCount == 0) emptyColliders.Add(collider);
                    }
                    var renderer = target.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        if (!emptyNames.Contains(name)) renderer.sharedMaterials = sources[name].GetComponent<MeshRenderer>().sharedMaterials.Select(m => MaterialFor(m, name, renderer.sharedMaterials)).ToArray();
                        changed.Add(renderer);
                    }
                }
                var prior = root.transform.Find("BilgePermanent");
                if (prior != null) UnityEngine.Object.DestroyImmediate(prior.gameObject);
                var permanent = new GameObject("BilgePermanent");
                permanent.transform.SetParent(root.transform, false);
                permanent.AddComponent<ShipPermanentPart>();
                var staticSources = new List<MeshRenderer>();
                var pumpRoot = new GameObject("BilgePump");
                pumpRoot.transform.SetParent(permanent.transform, false);
                pumpRoot.transform.localPosition = new Vector3(0f, (float)manifest["floor_z"] - (float)manifest["waterline"], 0f);
                var pump = pumpRoot.AddComponent<ShipBilgePump>();
                pump.DownAngle = -(float)manifest["pump_down_angle"];
                var pivot = new GameObject("PumpLeverPivot").transform;
                pivot.SetParent(pumpRoot.transform, false);
                pivot.localPosition = new Vector3(0f, 1.06119f, 0f);
                var grip = new GameObject("PumpGrip").transform;
                grip.SetParent(pivot, false);
                grip.localPosition = new Vector3(0f, .45f, -.61f);
                var trigger = grip.gameObject.AddComponent<SphereCollider>();
                trigger.radius = .14f;
                trigger.isTrigger = true;
                pump.LeverPivot = pivot;
                pump.LeverGrip = grip;
                foreach (string name in manifest["new_objects"].Values<string>())
                {
                    if (name.StartsWith("BilgeBeam_") || name.StartsWith("BilgeLamp_")) continue;
                    if (!sources.TryGetValue(name, out var source) || source.GetComponent<MeshFilter>() == null) continue;
                    Transform parent = name == "V19_PumpBody" ? pumpRoot.transform : name == "V19_PumpLever" ? pivot : permanent.transform;
                    var node = new GameObject(name);
                    node.transform.SetParent(parent, false);
                    var mesh = TransformMesh(source.GetComponent<MeshFilter>().sharedMesh, parent.worldToLocalMatrix * source.localToWorldMatrix);
                    var filter = node.AddComponent<MeshFilter>();
                    filter.sharedMesh = SaveMesh(mesh, name);
                    var renderer = node.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials.Select(m => MaterialFor(m, name)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    if (name == "V19_PumpBody")
                    {
                        var body = node.AddComponent<BoxCollider>();
                        body.center = filter.sharedMesh.bounds.center;
                        body.size = filter.sharedMesh.bounds.size;
                    }
                    else if (name != "V19_PumpLever")
                    {
                        var collider = node.AddComponent<MeshCollider>();
                        collider.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
                        collider.sharedMesh = filter.sharedMesh;
                        staticSources.Add(renderer);
                    }
                }
                ShipV3HoldSupportsSetup.Apply(root, sources, manifest);
                var destruction = root.GetComponent<ShipDestruction>();
                var protectedIds = new HashSet<int>(manifest["protected_sections"].Values<int>());
                var fragmentMasks = (JObject)manifest["protected_fragments"];
                foreach (var section in destruction.Sections)
                {
                    section.DamageColliders = section.DamageColliders.Where(c => !emptyColliders.Contains(c)).ToArray();
                    section.GameplayColliders = section.GameplayColliders.Where(c => !emptyColliders.Contains(c)).ToArray();
                    section.DamagedColliders = section.DamagedColliders.Where(c => !emptyColliders.Contains(c)).ToArray();
                    section.CriticalColliders = section.CriticalColliders.Where(c => !emptyColliders.Contains(c)).ToArray();
                    section.ReplacementColliders = section.ReplacementColliders.Where(c => !emptyColliders.Contains(c)).ToArray();
                    section.Indestructible = protectedIds.Contains(section.SectionId);
                    section.ProtectedFragments = fragmentMasks.TryGetValue(section.SectionId.ToString(), out var mask) ? (ulong)mask : 0;
                }
                var foundation = new HashSet<string>(manifest["foundation_fragments"].Values<string>());
                var original = JObject.Parse(File.ReadAllText("Assets/Models/Ships/ShipV3/ShipV3.json"));
                var originalFoundation = new HashSet<string>(original["structure"]["foundation_fragments"].Select(n => (string)n["object"]));
                var hullFragments = new HashSet<string>(original["pieces"].Where(p => (string)p["family"] == "Hull").SelectMany(p => p["fragment_names"].Values<string>()));
                var byId = destruction.Sections.ToDictionary(s => s.SectionId);
                foreach (var node in destruction.Profile.Structure)
                {
                    if (!byId.TryGetValue(node.SectionId, out var section) || node.Fragment >= section.Fragments.Length) continue;
                    string name = section.Fragments[node.Fragment].name;
                    if (hullFragments.Contains(name)) node.Anchor = originalFoundation.Contains(name) || foundation.Contains(name) || section.Indestructible;
                    else if (foundation.Contains(name) || section.Indestructible) node.Anchor = true;
                    if (restored.Contains(name)) node.LoadBearing = true;
                    if (emptyNames.Contains(name) && !foundation.Contains(name)) node.LoadBearing = false;
                }
                EditorUtility.SetDirty(destruction.Profile);
                if (manifest["hull_trim_sections"] != null) ConfigureHullTrim(destruction, new HashSet<int>(manifest["hull_trim_sections"].Values<int>()));
                var modifiedNames = new HashSet<string>(manifest["modified_objects"].Values<string>());
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!modifiedNames.Contains(filter.name)) continue;
                    foreach (var collider in filter.GetComponents<MeshCollider>())
                    {
                        collider.sharedMesh = filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0 ? filter.sharedMesh : null;
                        collider.enabled &= collider.sharedMesh != null;
                    }
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer != null) changed.Add(renderer);
                }
                var rig = root.GetComponent<ShipV3VisualRig>();
                if (rig != null && rig.ChainHawse != null)
                {
                    var outlet = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "V9_Anchor_Chain_Swing_Pivot");
                    if (outlet != null) rig.ChainHawse = outlet;
                    var hawse = root.transform.InverseTransformPoint(rig.ChainHawse.position);
                    float side = Mathf.Sign(hawse.x);
                    rig.ChainRoute = new[] { new Vector3(side * 1.45f, 5.19f, 15.7f), new Vector3(side * 1.45f, 4.88f, 15.7f), hawse };
                    float routeLength = 6.63f;
                    var previous = root.transform.InverseTransformPoint(rig.Drum.position);
                    foreach (var point in rig.ChainRoute) { routeLength += Vector3.Distance(previous, point); previous = point; }
                    routeLength += Vector3.Distance(previous, root.transform.InverseTransformPoint(rig.AnchorEye.position));
                    int required = Mathf.CeilToInt(routeLength / .085f) + 3;
                    var links = rig.ChainLinks.ToList();
                    while (links.Count < required)
                    {
                        var link = UnityEngine.Object.Instantiate(links[0].gameObject, links[0].parent).transform;
                        link.name = "V9_Anchor_Chain_Link_" + links.Count.ToString("000");
                        link.gameObject.SetActive(true);
                        links.Add(link);
                    }
                    rig.ChainLinks = links.ToArray();
                    rig.ChainLength = routeLength;
                    var instances = root.GetComponent<ShipV3ChainInstances>();
                    if (instances != null) instances.Links = rig.ChainLinks;
                }
                ConfigureFlooding(root);
                RebuildCaches(root, changed);
                foreach (var collider in emptyColliders) if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
                BatchPermanent(staticSources, permanent.transform);
                ConfigureDamagePreparation(root);
            }
            finally
            {
                if (model != null) UnityEngine.Object.DestroyImmediate(model);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        static void ConfigureHullTrim(ShipDestruction destruction, HashSet<int> trimIds)
        {
            var definitions = destruction.Profile.Sections.ToDictionary(d => d.SectionId);
            var hull = destruction.Sections.Where(s => definitions[s.SectionId].Type == ShipSectionType.Hull).SelectMany(s => s.Fragments.Select((fragment, index) => new { Section = s, Fragment = fragment, Index = index })).Where(f => f.Fragment.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0).ToArray();
            var nodes = destruction.Profile.Structure;
            var indices = nodes.Select((node, index) => new { node.SectionId, node.Fragment, Index = index }).ToDictionary(n => ((long)n.SectionId << 6) | (uint)n.Fragment, n => n.Index);
            var trimNodes = new HashSet<int>(nodes.Select((n, i) => new { Node = n, Index = i }).Where(n => trimIds.Contains(n.Node.SectionId)).Select(n => n.Index));
            foreach (var node in nodes) node.Neighbours = node.Neighbours.Where(i => !trimNodes.Contains(i)).ToArray();
            foreach (var section in destruction.Sections.Where(s => trimIds.Contains(s.SectionId)))
            {
                var supports = new HashSet<ShipDamageSection>();
                for (int i = 0; i < section.Fragments.Length; i++)
                {
                    var filter = section.Fragments[i].GetComponent<MeshFilter>();
                    var center = filter.transform.TransformPoint(filter.sharedMesh.bounds.center);
                    var nearest = hull.OrderBy(h =>
                    {
                        var mesh = h.Fragment.GetComponent<MeshFilter>();
                        var local = mesh.transform.InverseTransformPoint(center);
                        return (mesh.transform.TransformPoint(mesh.sharedMesh.bounds.ClosestPoint(local)) - center).sqrMagnitude;
                    }).First();
                    if (!indices.TryGetValue(((long)section.SectionId << 6) | (uint)i, out int trimIndex) || !indices.TryGetValue(((long)nearest.Section.SectionId << 6) | (uint)nearest.Index, out int hullIndex)) continue;
                    nodes[trimIndex].Neighbours = new[] { hullIndex };
                    nodes[trimIndex].Anchor = false;
                    nodes[trimIndex].LoadBearing = false;
                    nodes[hullIndex].Neighbours = nodes[hullIndex].Neighbours.Append(trimIndex).Distinct().ToArray();
                    supports.Add(nearest.Section);
                }
                section.HullSupports = supports.ToArray();
            }
            EditorUtility.SetDirty(destruction.Profile);
        }
        internal static Material MaterialFor(Material source, string name, Material[] existing = null)
        {
            if (name.StartsWith("BilgeBeam_")) return AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/ShipV3/Materials/V4_Hull_Ship_Art_Hull_Honey_Oak_V0.mat");
            if (name == "V19_PumpBody" || name == "V19_PumpLever") return AssetDatabase.LoadAssetAtPath<Material>(Folder + (name == "V19_PumpBody" ? "/PumpBody.mat" : "/PumpLever.mat"));
            if (source == null) throw new InvalidOperationException("Missing bilge material on " + name);
            string key = source.name.Replace(" (Instance)", "");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/ShipV3/Materials/" + key + ".mat");
            if (material == null && existing != null)
            {
                string baseName = System.Text.RegularExpressions.Regex.Replace(key, @"\.\d{3}$", "");
                var matches = existing.Where(m => m != null && System.Text.RegularExpressions.Regex.Replace(m.name, @"\.\d{3}$", "") == baseName).Distinct().ToArray();
                if (matches.Length == 1) material = matches[0];
            }
            if (material == null) throw new InvalidOperationException("Original ship material unavailable: " + key);
            return material;
        }
        static void ConfigureDamagePreparation(GameObject root)
        {
            string path = Folder + "/DamageWarmup.shadervariants";
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
            if (collection == null)
            {
                collection = new ShaderVariantCollection { name = "ShipV3DamageWarmup" };
                AssetDatabase.CreateAsset(collection, path);
            }
            collection.Clear();
            var water = root.GetComponent<ShipBilgeWater>();
            var materials = root.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Concat(new[] { water.WaterMaterial, water.LeakMaterial }).Where(m => m != null).Distinct();
            foreach (var material in materials)
                foreach (string shadow in new[] { "", "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE" })
                    foreach (string fog in new[] { "", "FOG_LINEAR", "FOG_EXP", "FOG_EXP2" })
                        foreach (bool soft in new[] { false, true })
                        {
                            var keywords = new List<string>(material.shaderKeywords);
                            foreach (string keyword in new[] { shadow, fog, soft ? "_SHADOWS_SOFT" : "" })
                                if (!string.IsNullOrEmpty(keyword) && material.shader.keywordSpace.FindKeyword(keyword).isValid && !keywords.Contains(keyword)) keywords.Add(keyword);
                            collection.Add(new ShaderVariantCollection.ShaderVariant(material.shader, PassType.ScriptableRenderPipeline, keywords.ToArray()));
                        }
            EditorUtility.SetDirty(collection);
            var preparation = root.GetComponent<ShipDamagePreparation>();
            if (preparation == null) preparation = root.AddComponent<ShipDamagePreparation>();
            preparation.Shaders = collection;
        }
        static void ConfigureFlooding(GameObject root)
        {
            var flood = root.GetComponent<ShipFlooding>();
            flood.UseBilgeFlooding = true;
            flood.EmptyHeight = .61f;
            flood.FullHeight = 3.95f;
            flood.FullWaterline = 4.11f;
            flood.FullBowPitch = 0f;
            flood.StrongHoleFillSeconds = 540f;
            flood.ReferenceBreachArea = .35f;
            flood.ReferencePressureHead = 1f;
            flood.UpperLeakPeriod = 3.2f;
            flood.UpperLeakBurstDuration = .8f;
            flood.UpperLeakAverage = .3f;
            root.GetComponent<ShipController>().InitialDraft = 2.01f;
            if (flood.WaterVisual == null)
            {
                flood.WaterVisual = new GameObject("BilgeWater").transform;
                flood.WaterVisual.SetParent(root.transform, false);
            }
            flood.WaterVisual.gameObject.SetActive(false);
            var water = root.GetComponent<ShipBilgeWater>();
            if (water == null) water = root.AddComponent<ShipBilgeWater>();
            water.WaterMaterial = BilgeMaterial("BilgeWater", "PirateSlop/Bilge Water");
            water.LeakMaterial = BilgeMaterial("BilgeLeak", "PirateSlop/Bilge Leak");
            var visual = flood.WaterVisual.gameObject;
            if (visual.GetComponent<MeshFilter>() == null) visual.AddComponent<MeshFilter>();
            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = water.WaterMaterial;
            renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var pump = root.GetComponentInChildren<ShipBilgePump>(true);
            pump.FullStrokeDuration = .68f;
            pump.ReturnDuration = .32f;
            var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Networking/SessionConfig.asset"));
            var protocol = settings.FindProperty("ProtocolVersion");
            if (protocol.intValue < 138) { protocol.intValue = 138; settings.ApplyModifiedPropertiesWithoutUndo(); }
        }
        static Material BilgeMaterial(string name, string shaderName)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Bilge shader unavailable: " + shaderName);
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        internal static Mesh TransformMesh(Mesh source, Matrix4x4 matrix)
        {
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.vertices = source.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var normals = matrix.inverse.transpose;
            mesh.normals = source.normals.Select(n => normals.MultiplyVector(n).normalized).ToArray();
            if (matrix.determinant < 0f)
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var indices = mesh.GetTriangles(i);
                    for (int j = 0; j < indices.Length; j += 3) (indices[j], indices[j + 1]) = (indices[j + 1], indices[j]);
                    mesh.SetTriangles(indices, i);
                }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
        internal static Mesh SaveMesh(Mesh replacement, string name)
        {
            string path = Folder + "/Meshes/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { replacement.name = name; AssetDatabase.CreateAsset(replacement, path); return replacement; }
            CopyMesh(replacement, saved);
            UnityEngine.Object.DestroyImmediate(replacement);
            return saved;
        }
        static void CopyMesh(Mesh source, Mesh target)
        {
            if (source.indexFormat == target.indexFormat && source.vertexCount == target.vertexCount && source.subMeshCount == target.subMeshCount &&
                source.vertices.SequenceEqual(target.vertices) && source.normals.SequenceEqual(target.normals) && source.tangents.SequenceEqual(target.tangents) &&
                source.uv.SequenceEqual(target.uv) && source.uv2.SequenceEqual(target.uv2) && source.colors32.SequenceEqual(target.colors32) &&
                Enumerable.Range(0, source.subMeshCount).All(i => source.GetTriangles(i).SequenceEqual(target.GetTriangles(i)))) return;
            target.Clear();
            target.indexFormat = source.indexFormat;
            target.vertices = source.vertices;
            target.normals = source.normals;
            target.tangents = source.tangents;
            target.uv = source.uv;
            target.uv2 = source.uv2;
            target.colors32 = source.colors32;
            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i);
            target.RecalculateBounds();
            EditorUtility.SetDirty(target);
        }
        static void RebuildCaches(GameObject root, HashSet<MeshRenderer> changed)
        {
            foreach (var batch in root.GetComponentsInChildren<ShipV3RenderBatch>(true))
            {
                if (!batch.Sources.Any(changed.Contains)) continue;
                var mesh = ShipV3RenderBatch.BuildMesh(batch.Sources, batch.SharedMaterial, batch.transform, true);
                CopyMesh(mesh, batch.CachedMesh);
                batch.GetComponent<MeshFilter>().sharedMesh = batch.CachedMesh;
                if (batch.CachedShadowMesh != null) CopyMesh(mesh, batch.CachedShadowMesh);
                if (batch.ShadowProxy != null) batch.ShadowProxy.GetComponent<MeshFilter>().sharedMesh = batch.CachedShadowMesh != null ? batch.CachedShadowMesh : batch.CachedMesh;
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            foreach (var batch in root.GetComponentsInChildren<ShipV3CollisionBatch>(true))
            {
                var sources = batch.Sources.Where(c => c != null && c.sharedMesh != null && c.sharedMesh.vertexCount > 0 && (c.GetComponent<MeshFilter>() == null || c.GetComponent<MeshFilter>().sharedMesh != null && c.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0)).ToArray();
                bool removed = sources.Length != batch.Sources.Length;
                if (!removed && !sources.Any(c => changed.Contains(c.GetComponent<MeshRenderer>()))) continue;
                batch.Sources = sources;
                var mesh = ShipV3CollisionBatch.BuildMesh(batch.Sources, batch.transform, true);
                CopyMesh(mesh, batch.CachedMesh);
                batch.GetComponent<MeshCollider>().sharedMesh = batch.CachedMesh;
                batch.SourceBounds = batch.Sources.Select(c => c != null && c.sharedMesh != null ? BoundsIn(c.sharedMesh, batch.transform.worldToLocalMatrix * c.transform.localToWorldMatrix) : new Bounds()).ToArray();
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        static Bounds BoundsIn(Mesh mesh, Matrix4x4 matrix)
        {
            var bounds = new Bounds();
            var points = mesh.vertices;
            if (points.Length == 0) return bounds;
            bounds = new Bounds(matrix.MultiplyPoint3x4(points[0]), Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(matrix.MultiplyPoint3x4(point));
            return bounds;
        }
        static void BatchPermanent(List<MeshRenderer> sources, Transform parent)
        {
            int index = 0;
            foreach (var group in sources.Where(r => r.sharedMaterials.Length == 1).GroupBy(r => r.sharedMaterial))
            {
                var members = group.ToArray();
                if (members.Length < 2) continue;
                var mesh = ShipV3RenderBatch.BuildMesh(members, group.Key, parent, true);
                var node = new GameObject("BilgeBatch_" + index);
                node.transform.SetParent(parent, false);
                var filter = node.AddComponent<MeshFilter>();
                filter.sharedMesh = SaveMesh(mesh, "BilgeBatch_" + index++);
                var renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.Key;
                var batch = node.AddComponent<ShipV3RenderBatch>();
                batch.Sources = members;
                batch.CachedMesh = filter.sharedMesh;
                batch.SharedMaterial = group.Key;
                batch.BatchAnchor = parent;
                foreach (var member in members) member.enabled = false;
            }
        }
    }
}
