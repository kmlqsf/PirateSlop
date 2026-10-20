using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Experimental;
using PirateSlop.Networking;
using PirateSlop.Ships;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static partial class UpperDestructionAssetSetup
    {
        const string BaseShip = "Assets/Resources/Ships/ShipV3Test.prefab";
        const string Folder = UpperDestructionSettings.Root;
        static readonly string[] sailTags = { "Main_Course", "Main_Topsail", "Fore_Course", "Fore_Topsail", "Mizzen" };

        public static string Prepare()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Unity must be idle outside Play Mode.");
            EnsureFolder("Assets/Experimental"); EnsureFolder(Folder);
            var root = PrefabUtility.LoadPrefabContents(BaseShip);
            try
            {
                root.name = "ShipV3UpperDestruction";
                var damage = root.GetComponent<ShipDestruction>();
                var profile = UnityEngine.Object.Instantiate(damage.Profile);
                profile.name = "ShipV3UpperDestruction";
                if (profile.SplinterMaterial == null)
                    profile.SplinterMaterial = root.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null && m.name == "Ship_Art_Fracture_Fresh_Wood");
                if (profile.SplinterMaterial == null) throw new InvalidOperationException("The existing fresh wood material is missing.");
                damage.Profile = Save(profile, Folder + "/Destruction.asset");
                var upper = root.AddComponent<UpperDestructionV2>();
                ConfigureAssemblies(root, upper, damage);
                PrepareSupportBindings(root, upper, damage);
                PrepareClosedMastFragments(root, upper, damage);
                PrepareUpperMaterials(damage);
                PrepareImpactMaterials(upper);
                upper.Debris = PrepareDebris(damage);
                upper.AttachmentDebris = PrepareAttachmentDebris(root, upper);
                PrepareMastRepairPreviews(root, upper);
                PrepareMastVisuals(root, upper);
                PrepareDrapedRigging(root, upper);
                upper.AssetRevision = 3;
                var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkPlayer.prefab").GetComponent<NetworkWeapon>();
                var crate = root.GetComponentInChildren<CannonballCrate>(true);
                upper.FallenCannon = PrepareDroppedWeapon(player.DropPrefabs[(int)InventoryItem.Cannon], crate.CannonPrefab, false);
                upper.FallenMortar = PrepareDroppedWeapon(player.DropPrefabs[(int)InventoryItem.MortarKit], crate.MortarPrefab, true);
                EditorUtility.SetDirty(damage.Profile);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Ship.prefab");
                if (saved == null) throw new InvalidOperationException("Experimental ship could not be saved.");
                var session = AssetDatabase.LoadAssetAtPath<SessionConfig>(UpperDestructionSettings.SessionPath);
                if (session == null) throw new InvalidOperationException("The shared session config is missing.");
                var original = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
                var collection = ScriptableObject.CreateInstance<SinglePrefabObjects>(); collection.name = "UpperDestructionPrefabs";
                collection.AddObjects(original.Prefabs.ToArray(), false, false);
                collection.AddObject(saved.GetComponent<NetworkObject>(), false, false);
                collection.AddObject(upper.FallenCannon.GetComponent<NetworkObject>(), false, false);
                collection.AddObject(upper.FallenMortar.GetComponent<NetworkObject>(), false, false);
                collection = Save(collection, Folder + "/NetworkPrefabs.asset");
                var settings = ScriptableObject.CreateInstance<UpperDestructionSettings>();
                settings.Ship = saved.GetComponent<NetworkObject>(); settings.Session = session; settings.Prefabs = collection;
                Save(settings, UpperDestructionSettings.AssetPath);
                PruneGeometry(upper);
                AssetDatabase.SaveAssets();
                return "UpperDestructionV2 prepared: " + upper.Assemblies.Length + " assemblies, " + upper.Debris.Length + " debris recipes, " + collection.GetObjectCount() + " network prefabs.";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void ConfigureAssemblies(GameObject root, UpperDestructionV2 upper, ShipDestruction damage)
        {
            var definitions = damage.Profile.Sections.ToDictionary(x => x.SectionId);
            var groups = new List<UpperAssembly>();
            foreach (var key in new[] { "Main", "Fore", "Mizzen" }) groups.Add(new UpperAssembly { Key = key, Kind = key == "Main" ? UpperAssemblyKind.MainMast : key == "Fore" ? UpperAssemblyKind.ForeMast : UpperAssemblyKind.MizzenMast });
            groups.Add(new UpperAssembly { Key = "Helm", Kind = UpperAssemblyKind.Helm });
            groups.Add(new UpperAssembly { Key = "Capstan", Kind = UpperAssemblyKind.Capstan });
            groups.Add(new UpperAssembly { Key = "Door", Kind = UpperAssemblyKind.Door });
            groups.Add(new UpperAssembly { Key = "Dice", Kind = UpperAssemblyKind.Dice });
            for (int i = 0; i < sailTags.Length; i++) groups.Add(new UpperAssembly { Key = "Control_" + sailTags[i], Kind = UpperAssemblyKind.SailControl, SailIndex = i });
            groups.Add(new UpperAssembly { Key = "HarpoonPort", Kind = UpperAssemblyKind.Harpoon, SailIndex = 0 });
            groups.Add(new UpperAssembly { Key = "HarpoonStarboard", Kind = UpperAssemblyKind.Harpoon, SailIndex = 1 });
            var sections = groups.ToDictionary(x => x.Key, _ => new List<ShipDamageSection>());
            var stems = damage.Sections.Where(x => definitions[x.SectionId].SourceGroup is "V3_Mast_Main" or "V3_Mast_Fore" or "V3_Mast_Mizzen").ToDictionary(x => MastKey(definitions[x.SectionId].SourceGroup), x => WorldCenter(x.Intact.transform));
            foreach (var section in damage.Sections)
            {
                var d = definitions[section.SectionId]; string name = d.SourceGroup;
                if (d.Type == ShipSectionType.Hull || d.Type == ShipSectionType.Rudder || name.StartsWith("BilgeBeam_", StringComparison.Ordinal)) continue;
                if (d.Type == ShipSectionType.Deck && section.Intact.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0) section.Indestructible = false;
                string key = null;
                if (d.Type == ShipSectionType.Helm) key = "Helm";
                else if (d.Type == ShipSectionType.Capstan) key = "Capstan";
                else if (name.Contains("V10_Port_")) key = "HarpoonPort";
                else if (name.Contains("V10_Starboard_")) key = "HarpoonStarboard";
                else if (name.Contains("Door_Leaf")) key = "Door";
                else if (name.Contains("Dice_Game") || name.Contains("Player_") && name.Contains("Wooden_Cup")) key = "Dice";
                else if (name.Contains("_Control_") && !name.EndsWith("_UpperBlock", StringComparison.Ordinal))
                    foreach (var tag in sailTags) if (name.Contains("_Control_" + tag + "_")) { key = "Control_" + tag; break; }
                if (key == null && d.Type != ShipSectionType.Deck && d.Type != ShipSectionType.Stairs) key = MastKey(name);
                if (key == null && d.Type == ShipSectionType.Yard && stems.Count > 0)
                {
                    var position = WorldCenter(section.Intact.transform);
                    key = stems.OrderBy(x => Vector3.ProjectOnPlane(x.Value - position, root.transform.up).sqrMagnitude).First().Key;
                }
                if (key == null || !sections.ContainsKey(key)) continue;
                sections[key].Add(section);
                if (key == "Main") section.Indestructible = true;
            }
            var features = root.GetComponent<ShipV3Features>();
            foreach (var group in groups)
            {
                group.Sections = sections[group.Key].ToArray();
                if (group.Kind is UpperAssemblyKind.MainMast or UpperAssemblyKind.ForeMast or UpperAssemblyKind.MizzenMast)
                {
                    group.Attachments = features.Attachments.Where(x => x.Object != null && MastKey(x.Object.name) == group.Key).Select(x => x.Object).Distinct().ToArray();
                }
                else if (group.Kind == UpperAssemblyKind.Dice) group.Attachments = new[] { features.DiceTable };
                else if (group.Kind == UpperAssemblyKind.Door) group.Attachments = new[] { features.DoorGrip };
                if (group.Kind == UpperAssemblyKind.SailControl)
                {
                    var sails = root.GetComponent<SailSystem>();
                    group.Controls = sails.RopeHandles[group.SailIndex].GetComponentsInChildren<Collider>(true);
                    group.ParentMast = groups.FindIndex(x => x.Key == MastKey(sailTags[group.SailIndex]));
                    group.Attachments = features.Attachments.Where(x => x.Object != null && x.Object.name.Contains("_Control_" + sailTags[group.SailIndex] + "_")).Select(x => x.Object).Distinct().ToArray();
                    if (group.ParentMast >= 0 && groups[group.ParentMast].Kind == UpperAssemblyKind.MainMast) group.Attachments = Array.Empty<Transform>();
                }
            }
            upper.Assemblies = groups.ToArray();
            if (groups.Any(x => (x.Kind is UpperAssemblyKind.MainMast or UpperAssemblyKind.ForeMast or UpperAssemblyKind.MizzenMast) && x.Sections.Length == 0)) throw new InvalidOperationException("A mast assembly has no mapped sections.");
            ConnectIntactStructure(root, upper, damage);
            var graph = new ShipStructuralGraph(damage.Profile);
            var unsupported = graph.Unsupported(_ => 0);
            if (unsupported.Count != 0) throw new InvalidOperationException("The intact structural graph contains unsupported sections: " + unsupported.Count);
        }

        static void ConnectIntactStructure(GameObject root, UpperDestructionV2 upper, ShipDestruction damage)
        {
            var nodes = damage.Profile.Structure;
            var sections = damage.Sections.ToDictionary(x => x.SectionId);
            var bounds = new Bounds[nodes.Length];
            var main = new HashSet<int>(upper.Assemblies.First(x => x.Kind == UpperAssemblyKind.MainMast).Sections.Select(x => x.SectionId));
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var section = sections[node.SectionId];
                if (main.Contains(node.SectionId)) node.Anchor = true;
                var source = section.RepairTransform(node.Fragment); var local = section.RepairBounds(node.Fragment);
                var matrix = root.transform.worldToLocalMatrix * source.localToWorldMatrix;
                bounds[i] = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
                for (int corner = 0; corner < 8; corner++) bounds[i].Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
            }
            for (int pass = 0; pass < nodes.Length; pass++)
            {
                var unsupported = new ShipStructuralGraph(damage.Profile).Unsupported(_ => 0);
                if (unsupported.Count == 0) return;
                bool Missing(int index) => unsupported.TryGetValue(nodes[index].SectionId, out ulong mask) && (mask & (1UL << nodes[index].Fragment)) != 0;
                int from = -1, to = -1;
                float best = float.PositiveInfinity;
                bool hasBearing = Enumerable.Range(0, nodes.Length).Any(i => Missing(i) && nodes[i].LoadBearing);
                for (int a = 0; a < nodes.Length; a++)
                {
                    if (!Missing(a) || hasBearing && !nodes[a].LoadBearing) continue;
                    for (int b = 0; b < nodes.Length; b++)
                    {
                        if (Missing(b) || !nodes[b].LoadBearing) continue;
                        var pa = bounds[a].ClosestPoint(bounds[b].center); var pb = bounds[b].ClosestPoint(pa);
                        float distance = (pa - pb).sqrMagnitude + (bounds[a].center - bounds[b].center).sqrMagnitude * .000001f;
                        if (distance < best) { from = a; to = b; best = distance; }
                    }
                }
                if (from < 0 || to < 0) throw new InvalidOperationException("An intact structure component has no load-bearing support.");
                nodes[from].Neighbours = nodes[from].Neighbours.Append(to).Distinct().ToArray();
                nodes[to].Neighbours = nodes[to].Neighbours.Append(from).Distinct().ToArray();
            }
            throw new InvalidOperationException("The intact structural graph could not be connected.");
        }

        static string MastKey(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Contains("MainDeck") || name.Contains("BilgeBeam")) return null;
            if (name.Contains("Fore_Main_Stay")) return "Fore";
            if (name.Contains("Main_Mizzen_Stay")) return "Mizzen";
            if (name.Contains("MainMast") || name.Contains("Mast_Main") || System.Text.RegularExpressions.Regex.IsMatch(name, @"(?:^|_)Main(?:_|\d|$)")) return "Main";
            if (name.Contains("ForeMast") || name.Contains("Mast_Fore") || System.Text.RegularExpressions.Regex.IsMatch(name, @"(?:^|_)Fore(?:_|\d|$)")) return "Fore";
            if (name.Contains("MizzenMast") || name.Contains("Mast_Mizzen") || System.Text.RegularExpressions.Regex.IsMatch(name, @"(?:^|_)Mizzen(?:_|\d|$)")) return "Mizzen";
            return null;
        }

        public static void PrepareUpperMaterials(ShipDestruction damage)
        {
            var definitions = damage.Profile.Sections.ToDictionary(d => d.SectionId);
            var changed = new HashSet<MeshRenderer>();
            var upper = damage.GetComponent<UpperDestructionV2>();
            var mastSections = new HashSet<ShipDamageSection>(upper.Assemblies.Where(a => a.Kind is UpperAssemblyKind.MainMast or UpperAssemblyKind.ForeMast or UpperAssemblyKind.MizzenMast).SelectMany(a => a.Sections));
            foreach (var section in damage.Sections)
            {
                var definition = definitions[section.SectionId];
                if (definition.Type is ShipSectionType.Hull or ShipSectionType.Rudder || definition.SourceGroup.StartsWith("BilgeBeam_", StringComparison.Ordinal)) continue;
                foreach (var source in new[] { section.Intact }.Concat(section.Fragments))
                {
                    var renderer = source != null ? source.GetComponent<MeshRenderer>() : null;
                    var mesh = source != null ? source.GetComponent<MeshFilter>()?.sharedMesh : null;
                    if (renderer == null || mesh == null) continue;
                    if (mastSections.Contains(section)) changed.Add(renderer);
                    var materials = renderer.sharedMaterials;
                    if (materials.Length >= mesh.subMeshCount && materials.All(m => m != null)) continue;
                    if (materials.Length < mesh.subMeshCount) Array.Resize(ref materials, mesh.subMeshCount);
                    for (int i = 0; i < materials.Length; i++) if (materials[i] == null) materials[i] = damage.Profile.SplinterMaterial;
                    renderer.sharedMaterials = materials;
                    changed.Add(renderer);
                }
            }
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            if (container == null) { container = ScriptableObject.CreateInstance<UpperDebrisGeometry>(); AssetDatabase.CreateAsset(container, Folder + "/DebrisGeometry.asset"); }
            foreach (var batch in damage.GetComponentsInChildren<ShipV3RenderBatch>(true))
            {
                if (!batch.Sources.Any(changed.Contains)) continue;
                var mesh = ShipV3RenderBatch.BuildMesh(batch.Sources, batch.SharedMaterial, batch.transform, true);
                mesh.name = "UpperMaterialBatch_" + batch.name;
                batch.CachedMesh = SavePreparedMesh(mesh, container);
            }
            EditorUtility.SetDirty(container);
        }

        public static void PrepareMastRepairPreviews(GameObject root, UpperDestructionV2 upper)
        {
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            foreach (var assembly in upper.Assemblies)
            {
                if (assembly.Kind == UpperAssemblyKind.MainMast) continue;
                var sources = new HashSet<MeshFilter>();
                foreach (var section in assembly.Sections)
                {
                    var filter = section.Intact != null ? section.Intact.GetComponent<MeshFilter>() : null;
                    if (filter != null) sources.Add(filter);
                }
                var combine = new List<CombineInstance>();
                foreach (var filter in sources)
                    if (filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0)
                        for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                            if (filter.sharedMesh.GetIndexCount(sub) > 0)
                                combine.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                var preview = new Mesh { name = "MastRepairPreview_" + assembly.Key, indexFormat = IndexFormat.UInt32 };
                preview.CombineMeshes(combine.ToArray(), true, true);
                assembly.RepairPreview = SavePreparedMesh(preview, container);
            }
            EditorUtility.SetDirty(container);
        }

        public static void PrepareMastVisuals(GameObject root, UpperDestructionV2 upper)
        {
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            var definitions = root.GetComponent<ShipDestruction>().Profile.Sections.ToDictionary(d => d.SectionId);
            foreach (var assembly in upper.Assemblies)
            {
                if (assembly.Kind != UpperAssemblyKind.ForeMast && assembly.Kind != UpperAssemblyKind.MizzenMast) continue;
                var filters = new HashSet<MeshFilter>();
                var children = upper.Assemblies.Where(a => a.ParentMast == Array.IndexOf(upper.Assemblies, assembly)).ToArray();
                var attachments = assembly.Attachments.Concat(children.SelectMany(a => a.Attachments)).Distinct().ToArray();
                foreach (var section in assembly.Sections.Concat(children.SelectMany(a => a.Sections)))
                    if (!assembly.BaseSections.Contains(section))
                    if (section.Intact != null && section.Intact.TryGetComponent<MeshFilter>(out var filter)) filters.Add(filter);
                var rigidFilters = new HashSet<MeshFilter>(filters);
                foreach (var attachment in attachments)
                    if (attachment != null)
                        foreach (var filter in attachment.GetComponentsInChildren<MeshFilter>(true))
                            if (filter.gameObject.activeInHierarchy && filter.GetComponent<ShipV3RenderBatch>() == null) filters.Add(filter);
                var materials = new Dictionary<(Material Material, bool Flexible), List<CombineInstance>>();
                foreach (var filter in filters)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null) continue;
                    var slots = renderer.sharedMaterials;
                    for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                    {
                        if (filter.sharedMesh.GetIndexCount(sub) == 0 || slots.Length == 0) continue;
                        var material = slots[Mathf.Min(sub, slots.Length - 1)];
                        if (material == null) throw new InvalidOperationException("Missing mast material: " + filter.name);
                        var key = (material, !rigidFilters.Contains(filter) && (material.name.Contains("Rope") || IsFlexible(filter.name)));
                        if (!materials.TryGetValue(key, out var entries)) materials[key] = entries = new List<CombineInstance>();
                        entries.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    }
                }
                var visuals = new List<UpperAttachmentRecipe>();
                foreach (var pair in materials)
                {
                    var mesh = new Mesh { name = "MastRest_" + assembly.Key + "_" + pair.Key.Material.name + (pair.Key.Flexible ? "_Flexible" : ""), indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                    mesh = SavePreparedMesh(mesh, container);
                    visuals.Add(new UpperAttachmentRecipe { Source = root.transform, Poses = new[] { mesh }, Offsets = new[] { Vector3.zero }, Materials = new[] { pair.Key.Material }, SailIndex = -1, Flexible = pair.Key.Flexible });
                }
                foreach (var recipe in upper.AttachmentDebris)
                    if (attachments.Any(a => a != null && (recipe.Source == a || recipe.Source.IsChildOf(a)))) visuals.Add(recipe);
                assembly.MastVisuals = visuals.ToArray();
                int stemId = definitions.Values.First(d => d.SourceGroup == "V3_Mast_" + assembly.Key).SectionId;
                var stem = assembly.Sections.First(s => s.SectionId == stemId);
                var source = stem.Intact.GetComponent<MeshFilter>();
                var matrix = root.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                var bounds = new Bounds(matrix.MultiplyPoint3x4(source.sharedMesh.bounds.center), Vector3.zero);
                for (int corner = 0; corner < 8; corner++) bounds.Encapsulate(matrix.MultiplyPoint3x4(source.sharedMesh.bounds.center + Vector3.Scale(source.sharedMesh.bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
                assembly.CollapsePivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var foot = assembly.BaseSections.FirstOrDefault()?.Intact.GetComponent<MeshFilter>();
                assembly.CollapseDrop = foot != null ? Mathf.Max(0f, bounds.min.y - ShapeBounds(root.transform, foot.transform, foot.sharedMesh.bounds).min.y - bounds.extents.x) : 0f;
            }
            EditorUtility.SetDirty(container);
        }

        static Mesh SavePreparedMesh(Mesh mesh, UpperDebrisGeometry container)
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(Folder + "/DebrisGeometry.asset").OfType<Mesh>().FirstOrDefault(m => m.name == mesh.name);
            if (existing == null) { AssetDatabase.AddObjectToAsset(mesh, container); return mesh; }
            existing.Clear(); existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.tangents = mesh.tangents; existing.colors32 = mesh.colors32;
            var coordinates = new List<Vector4>();
            for (int channel = 0; channel < 8; channel++) { mesh.GetUVs(channel, coordinates); if (coordinates.Count > 0) existing.SetUVs(channel, coordinates); }
            existing.subMeshCount = mesh.subMeshCount;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) existing.SetIndices(mesh.GetIndices(sub), mesh.GetTopology(sub), sub, false);
            existing.bounds = mesh.bounds; existing.UploadMeshData(false);
            UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing);
            return existing;
        }

        static void PrepareImpactMaterials(UpperDestructionV2 upper)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new InvalidOperationException("The URP particle shader is required.");
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "UpperImpactParticle", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - radius), 1.5f));
            }
            texture.SetPixels(pixels); texture.Apply();
            texture = Save(texture, Folder + "/ImpactParticle.asset");
            Material Make(string label, bool additive)
            {
                var material = new Material(shader) { name = label, renderQueue = 3000 };
                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", texture);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                return Save(material, Folder + "/" + label + ".mat");
            }
            upper.DustMaterial = Make("ImpactDust", false); upper.SparkMaterial = Make("ImpactSparks", true);
        }

        static NetworkFish PrepareDroppedWeapon(NetworkFish template, SimpleCannon cannon, bool mortar)
        {
            if (template == null || cannon == null) throw new InvalidOperationException("Weapon pickup and cannon models are required.");
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(template));
            try
            {
                root.transform.localScale = Vector3.one;
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var model = UnityEngine.Object.Instantiate(cannon.gameObject, root.transform);
                model.name = "FallenWeaponModel"; model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                var barrel = model.GetComponent<SimpleCannon>().BarrelPivot;
                var traverse = model.GetComponent<SimpleCannon>().TraversePivot;
                foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                foreach (var collider in root.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                var bounds = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    var b = filter.sharedMesh.bounds; var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var point = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; } else bounds.Encapsulate(point);
                    }
                }
                var box = root.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = Vector3.Max(bounds.size, Vector3.one * .05f);
                var dropped = root.AddComponent<UpperDroppedWeapon>(); dropped.Barrel = barrel; dropped.Traverse = traverse;
                var fish = root.GetComponent<NetworkFish>(); fish.Item = mortar ? InventoryItem.MortarKit : InventoryItem.Cannon;
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + (mortar ? "/FallenMortar.prefab" : "/FallenCannon.prefab")).GetComponent<NetworkFish>();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static UpperDebrisRecipe[] PrepareDebris(ShipDestruction damage)
        {
            var result = new List<UpperDebrisRecipe>();
            var cache = new Dictionary<Mesh, (Mesh First, Mesh Second, Vector3 FirstOffset, Vector3 SecondOffset)>();
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            if (container == null) { container = ScriptableObject.CreateInstance<UpperDebrisGeometry>(); AssetDatabase.CreateAsset(container, Folder + "/DebrisGeometry.asset"); }
            foreach (var section in damage.Sections)
            {
                if (section.Indestructible) continue;
                for (int i = 0; i < section.RepairCount; i++)
                {
                    if (section.SurfaceDamage && section.Fragments.Length == 0) continue;
                    var source = section.RepairTransform(i);
                    var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
                    if (filter == null || renderer == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) continue;
                    var original = filter.sharedMesh;
                    if (!cache.TryGetValue(original, out var pair))
                    {
                        pair = Split(original);
                        pair.First.name = "PieceA_" + original.name; pair.Second.name = "PieceB_" + original.name;
                        AssetDatabase.AddObjectToAsset(pair.First, container); AssetDatabase.AddObjectToAsset(pair.Second, container);
                        cache[original] = pair;
                    }
                    var originalMaterials = renderer.sharedMaterials;
                    var metal = originalMaterials.FirstOrDefault(m => m != null && m.HasProperty("_Metallic") && m.GetFloat("_Metallic") >= .5f);
                    if (metal != null && originalMaterials.Any(m => m != metal && m != damage.Profile.SplinterMaterial)) metal = null;
                    var materials = new Material[original.subMeshCount + 1];
                    for (int m = 0; m < original.subMeshCount; m++) materials[m] = metal != null ? metal : originalMaterials.Length > 0 && originalMaterials[Mathf.Min(m, originalMaterials.Length - 1)] != null ? originalMaterials[Mathf.Min(m, originalMaterials.Length - 1)] : damage.Profile.SplinterMaterial;
                    materials[original.subMeshCount] = metal != null ? metal : damage.Profile.SplinterMaterial;
                    if (metal != null) renderer.sharedMaterials = materials.Take(original.subMeshCount).ToArray();
                    result.Add(new UpperDebrisRecipe { Section = section.SectionId, Fragment = i, Source = source, First = pair.First, Second = pair.Second,
                        FirstOffset = pair.FirstOffset, SecondOffset = pair.SecondOffset, Materials = materials });
                }
            }
            EditorUtility.SetDirty(container);
            return result.ToArray();
        }

        static void PruneGeometry(UpperDestructionV2 upper)
        {
            var keep = new HashSet<Mesh>();
            foreach (var recipe in upper.Debris) { keep.Add(recipe.First); keep.Add(recipe.Second); }
            foreach (var recipe in upper.AttachmentDebris) foreach (var mesh in recipe.Poses) keep.Add(mesh);
            foreach (var assembly in upper.Assemblies) if (assembly.RepairPreview != null) keep.Add(assembly.RepairPreview);
            foreach (var assembly in upper.Assemblies) foreach (var recipe in assembly.MastVisuals) foreach (var mesh in recipe.Poses) keep.Add(mesh);
            foreach (var assembly in upper.Assemblies) foreach (var recipe in assembly.MastVisuals) foreach (var mesh in recipe.DrapedPoses) keep.Add(mesh);
            foreach (var filter in upper.GetComponentsInChildren<MeshFilter>(true)) if (filter.sharedMesh != null) keep.Add(filter.sharedMesh);
            foreach (var batch in upper.GetComponentsInChildren<ShipV3RenderBatch>(true)) if (batch.CachedMesh != null) keep.Add(batch.CachedMesh);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Folder + "/DebrisGeometry.asset"))
                if (asset is Mesh mesh && !keep.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh, true);
        }

        static UpperAttachmentRecipe[] PrepareAttachmentDebris(GameObject root, UpperDestructionV2 upper)
        {
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            var result = new List<UpperAttachmentRecipe>();
            var visited = new HashSet<SkinnedMeshRenderer>();
            var rig = root.GetComponent<ShipV3VisualRig>();
            foreach (var assembly in upper.Assemblies)
            {
                if (assembly.Kind == UpperAssemblyKind.MainMast) continue;
                foreach (var attachment in assembly.Attachments)
                {
                    if (attachment == null) continue;
                    foreach (var renderer in attachment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (!visited.Add(renderer) || renderer.sharedMesh == null) continue;
                        int index = Array.IndexOf(rig.Sails, renderer);
                        if (index < 0)
                        {
                            int rigIndex = Array.IndexOf(rig.RigMeshes, renderer);
                            index = rigIndex >= 0 ? rig.RigSailIndices[rigIndex] : 0;
                        }
                        int furl = renderer.sharedMesh.GetBlendShapeIndex("Furled");
                        if (furl < 0) furl = renderer.sharedMesh.GetBlendShapeIndex("Deployed.Furled");
                        int poses = furl >= 0 ? 3 : 1;
                        var meshes = new Mesh[poses]; var offsets = new Vector3[poses];
                        float rest = furl >= 0 ? renderer.GetBlendShapeWeight(furl) : 0f;
                        try
                        {
                            for (int pose = 0; pose < poses; pose++)
                            {
                                if (furl >= 0) renderer.SetBlendShapeWeight(furl, 100f - pose * 50f);
                                var mesh = new Mesh { name = "FallingRig_" + renderer.name + "_" + pose };
                                renderer.BakeMesh(mesh, false);
                                offsets[pose] = Center(mesh); meshes[pose] = mesh;
                                AssetDatabase.AddObjectToAsset(mesh, container);
                            }
                        }
                        finally { if (furl >= 0) renderer.SetBlendShapeWeight(furl, rest); }
                        result.Add(new UpperAttachmentRecipe { Source = renderer.transform, Poses = meshes, Offsets = offsets, Materials = renderer.sharedMaterials, SailIndex = index });
                    }
                }
            }
            EditorUtility.SetDirty(container);
            return result.ToArray();
        }

        struct Vertex : IEquatable<Vertex>
        {
            public Vector3 Point, Normal;
            public Vector2 Uv;
            public Vector4 Tangent;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new() { Point = Vector3.Lerp(a.Point, b.Point, t), Normal = Vector3.Lerp(a.Normal, b.Normal, t), Uv = Vector2.Lerp(a.Uv, b.Uv, t), Tangent = Vector4.Lerp(a.Tangent, b.Tangent, t) };
            public bool Equals(Vertex other) => Point.Equals(other.Point) && Normal.Equals(other.Normal) && Uv.Equals(other.Uv) && Tangent.Equals(other.Tangent);
            public override bool Equals(object other) => other is Vertex vertex && Equals(vertex);
            public override int GetHashCode() => HashCode.Combine(Point, Normal, Uv, Tangent);
        }

        static (Mesh First, Mesh Second, Vector3 FirstOffset, Vector3 SecondOffset) Split(Mesh original)
        {
            if (!original.isReadable) throw new InvalidOperationException("Debris source is not readable: " + original.name);
            var size = original.bounds.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            Mesh Make(int side)
            {
                var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tangents = new List<Vector4>();
                var triangles = Enumerable.Range(0, original.subMeshCount + 1).Select(_ => new List<int>()).ToArray();
                var points = original.vertices; var sourceNormals = original.normals; var sourceUvs = original.uv; var sourceTangents = original.tangents;
                var cuts = new List<Vector3>();
                var unique = new Dictionary<Vertex, int>();
                void Add(List<Vertex> polygon, int sub)
                {
                    var indices = new int[polygon.Count];
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        var v = polygon[i];
                        if (!unique.TryGetValue(v, out int index))
                        {
                            index = vertices.Count; unique[v] = index;
                            vertices.Add(v.Point); normals.Add(v.Normal.normalized); uvs.Add(v.Uv); tangents.Add(v.Tangent);
                        }
                        indices[i] = index;
                    }
                    for (int i = 1; i + 1 < polygon.Count; i++) { triangles[sub].Add(indices[0]); triangles[sub].Add(indices[i]); triangles[sub].Add(indices[i + 1]); }
                }
                for (int sub = 0; sub < original.subMeshCount; sub++)
                {
                    var indices = original.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var polygon = new List<Vertex>();
                        for (int edge = 0; edge < 3; edge++)
                        {
                            int ai = indices[i + edge], bi = indices[i + (edge + 1) % 3];
                            Vertex Get(int n) => new() { Point = points[n], Normal = sourceNormals.Length == points.Length ? sourceNormals[n] : Vector3.up,
                                Uv = sourceUvs.Length == points.Length ? sourceUvs[n] : Vector2.zero, Tangent = sourceTangents.Length == points.Length ? sourceTangents[n] : new Vector4(1, 0, 0, 1) };
                            var a = Get(ai); var b = Get(bi);
                            float da = (a.Point[axis] - original.bounds.center[axis]) * side, db = (b.Point[axis] - original.bounds.center[axis]) * side;
                            bool insideA = da >= -1e-6f, insideB = db >= -1e-6f;
                            if (insideA) polygon.Add(a);
                            if (insideA == insideB) continue;
                            var v = Vertex.Lerp(a, b, da / (da - db)); polygon.Add(v);
                            if (!cuts.Any(p => (p - v.Point).sqrMagnitude < 1e-10f)) cuts.Add(v.Point);
                        }
                        if (polygon.Count >= 3) Add(polygon, sub);
                    }
                }
                if (cuts.Count >= 3)
                {
                    var center = cuts.Aggregate(Vector3.zero, (a, b) => a + b) / cuts.Count;
                    Vector3 normal = Vector3.zero; normal[axis] = -side;
                    var tangent = Vector3.Cross(normal, axis == 1 ? Vector3.right : Vector3.up).normalized;
                    var bitangent = Vector3.Cross(normal, tangent);
                    cuts.Sort((a, b) => Mathf.Atan2(Vector3.Dot(a - center, bitangent), Vector3.Dot(a - center, tangent)).CompareTo(Mathf.Atan2(Vector3.Dot(b - center, bitangent), Vector3.Dot(b - center, tangent))));
                    Add(cuts.Select(p => new Vertex { Point = p, Normal = normal, Uv = new Vector2(Vector3.Dot(p - center, tangent), Vector3.Dot(p - center, bitangent)), Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1) }).ToList(), original.subMeshCount);
                }
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTangents(tangents); mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
            var first = Make(1); var second = Make(-1);
            var firstOffset = Center(first); var secondOffset = Center(second);
            if (first.vertexCount == 0 || second.vertexCount == 0) throw new InvalidOperationException("Debris split produced an empty piece: " + original.name);
            return (first, second, firstOffset, secondOffset);
        }

        static Vector3 Center(Mesh mesh)
        {
            var offset = mesh.bounds.center; var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] -= offset;
            mesh.vertices = vertices; mesh.RecalculateBounds(); return offset;
        }
        static Vector3 WorldCenter(Transform source)
        {
            var filter = source.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null ? source.TransformPoint(filter.sharedMesh.bounds.center) : source.position;
        }
        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(value); return existing;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
