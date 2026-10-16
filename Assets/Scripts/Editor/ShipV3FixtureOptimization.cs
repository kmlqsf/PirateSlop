using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PirateSlop.Ships;
using PirateSlop.Harpoon;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static class ShipV3FixtureOptimization
    {
        const string PrefabPath = "Assets/Resources/Ships/ShipV3Test.prefab";
        const string Folder = "Assets/Models/Ships/ShipV3/RuntimeMeshes/Fixtures";

        public static string Apply()
        {
            if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Stop Play Mode and wait for the build first.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                string report = Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return report;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static string Configure(GameObject root)
        {
            Directory.CreateDirectory(Folder);
            var report = new List<string>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                int limit = filter.name == "DiceCandle" ? 4096 : filter.name.Contains("_Reel_Winding_Strand_") ? 8192 : 0;
                if (limit == 0) continue;
                if (filter.sharedMesh == null || filter.sharedMesh.blendShapeCount != 0)
                    throw new InvalidOperationException("Unexpected prop mesh: " + filter.name);
                if (AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder + "/", StringComparison.Ordinal)) continue;
                var source = filter.sharedMesh;
                var mesh = Reduce(source, limit);
                int reducedCount = mesh.triangles.Length / 3;
                filter.sharedMesh = Store(mesh, Folder + "/" + filter.name + ".asset");
                report.Add(filter.name + ": " + source.triangles.Length / 3 + " -> " + reducedCount);
            }
            int groups = AddMaterialBatches(root, report);
            report.Add("New material batches: " + groups);
            report.Add("GPU pinned nets: " + ConfigurePinnedNets(root));
            return string.Join("\n", report);
        }

        public static int ConfigurePinnedNets(GameObject root)
        {
            int count = 0;
            foreach (var motion in root.GetComponentsInChildren<ShipV3ClothMotion>(true))
            {
                if (!motion.PinTop || motion.SailIndex >= 0 || motion.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
                if (motion.GetComponent<SkinnedMeshRenderer>() != null) continue;
                var original = motion.GetComponent<MeshRenderer>();
                if (original == null) continue;
                var settings = new { original.sharedMaterials, original.enabled, original.forceRenderingOff,
                    original.shadowCastingMode, original.receiveShadows, original.renderingLayerMask,
                    original.lightProbeUsage, original.reflectionProbeUsage, original.motionVectorGenerationMode,
                    original.probeAnchor, original.sortingLayerID, original.sortingOrder, original.allowOcclusionWhenDynamic,
                    original.lightmapIndex, original.lightmapScaleOffset, original.realtimeLightmapIndex, original.realtimeLightmapScaleOffset };
                var references = new List<(SerializedObject Owner, string Path)>();
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    var serialized = new SerializedObject(behaviour);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == original)
                            references.Add((serialized, property.propertyPath));
                }
                var skin = motion.gameObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = motion.GetComponent<MeshFilter>().sharedMesh;
                skin.sharedMaterials = settings.sharedMaterials;
                skin.enabled = settings.enabled;
                skin.forceRenderingOff = settings.forceRenderingOff;
                skin.shadowCastingMode = settings.shadowCastingMode;
                skin.receiveShadows = settings.receiveShadows;
                skin.renderingLayerMask = settings.renderingLayerMask;
                skin.lightProbeUsage = settings.lightProbeUsage;
                skin.reflectionProbeUsage = settings.reflectionProbeUsage;
                skin.motionVectorGenerationMode = settings.motionVectorGenerationMode;
                skin.probeAnchor = settings.probeAnchor;
                skin.sortingLayerID = settings.sortingLayerID;
                skin.sortingOrder = settings.sortingOrder;
                skin.allowOcclusionWhenDynamic = settings.allowOcclusionWhenDynamic;
                skin.lightmapIndex = settings.lightmapIndex;
                skin.lightmapScaleOffset = settings.lightmapScaleOffset;
                skin.realtimeLightmapIndex = settings.realtimeLightmapIndex;
                skin.realtimeLightmapScaleOffset = settings.realtimeLightmapScaleOffset;
                skin.localBounds = skin.sharedMesh.bounds;
                skin.quality = SkinQuality.Bone2;
                skin.updateWhenOffscreen = false;
                foreach (var reference in references)
                {
                    reference.Owner.FindProperty(reference.Path).objectReferenceValue = skin;
                    reference.Owner.ApplyModifiedPropertiesWithoutUndo();
                }
                if (original != null) UnityEngine.Object.DestroyImmediate(original);
                motion.GpuPinnedMotion = true;
                count++;
            }
            return count;
        }

        public static Mesh Reduce(Mesh source, int triangleLimit)
        {
            if (!source.isReadable || source.subMeshCount != 1 || source.blendShapeCount != 0 || source.bindposes.Length != 0)
                throw new InvalidOperationException("Only readable static single-submesh props can be reduced.");
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                MeshLodUtility.GenerateMeshLods(copy, 8);
                var ranges = copy.GetLods(0);
                var range = ranges.FirstOrDefault(r => r.indexCount > 0 && r.indexCount / 3 <= triangleLimit);
                if (range.indexCount == 0) throw new InvalidOperationException("No suitable reduction for " + source.name);
                int[] selected = new int[range.indexCount];
                using (var data = Mesh.AcquireReadOnlyMeshData(copy))
                {
                    if (copy.indexFormat == IndexFormat.UInt32)
                    {
                        var indices = data[0].GetIndexData<uint>();
                        for (int i = 0; i < selected.Length; i++) selected[i] = (int)indices[(int)range.indexStart + i];
                    }
                    else
                    {
                        var indices = data[0].GetIndexData<ushort>();
                        for (int i = 0; i < selected.Length; i++) selected[i] = indices[(int)range.indexStart + i];
                    }
                }
                var remap = new Dictionary<int, int>();
                var retained = new List<int>();
                for (int i = 0; i < selected.Length; i++)
                {
                    int old = selected[i];
                    if (!remap.TryGetValue(old, out int next))
                    {
                        next = retained.Count;
                        remap.Add(old, next);
                        retained.Add(old);
                    }
                    selected[i] = next;
                }
                var mesh = new Mesh { name = source.name + "Reduced", indexFormat = retained.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                var positions = copy.vertices;
                mesh.vertices = retained.Select(i => positions[i]).ToArray();
                var normals = copy.normals;
                if (normals.Length == copy.vertexCount) mesh.normals = retained.Select(i => normals[i]).ToArray();
                var tangents = copy.tangents;
                if (tangents.Length == copy.vertexCount) mesh.tangents = retained.Select(i => tangents[i]).ToArray();
                var colors = copy.colors32;
                if (colors.Length == copy.vertexCount) mesh.colors32 = retained.Select(i => colors[i]).ToArray();
                for (int channel = 0; channel < 8; channel++)
                {
                    var values = new List<Vector4>();
                    copy.GetUVs(channel, values);
                    if (values.Count == copy.vertexCount) mesh.SetUVs(channel, retained.Select(i => values[i]).ToList());
                }
                mesh.triangles = selected;
                mesh.bounds = source.bounds;
                return mesh;
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        static int AddMaterialBatches(GameObject root, List<string> report)
        {
            var rig = root.GetComponent<ShipV3VisualRig>();
            var features = root.GetComponent<ShipV3Features>();
            var destruction = root.GetComponent<ShipDestruction>();
            var moving = rig.Motions.Select(m => m.Target)
                .Concat(new[] { rig.Rudder, features.DoorHinge, features.AnchorTravel, features.DispenserLever })
                .Concat(features.Attachments.Where(a => a.Fall).Select(a => a.Object))
                .Concat(root.GetComponentsInChildren<HelmInteraction>(true).SelectMany(h => new[] { h.transform, h.Wheel }))
                .Concat(root.GetComponentsInChildren<ShipBilgePump>(true).Select(p => p.LeverPivot))
                .Where(t => t != null).ToArray();
            var dependent = new HashSet<Renderer>(destruction.Sections.SelectMany(s => s.DependentRenderers));
            var existing = new HashSet<Renderer>(root.GetComponentsInChildren<ShipV3RenderBatch>(true).SelectMany(b => b.Sources));
            var body = root.GetComponent<Rigidbody>();
            var sources = root.GetComponentsInChildren<MeshRenderer>(true).Where(r =>
                r.enabled && r.gameObject.activeInHierarchy && !existing.Contains(r) && r.sharedMaterials.Length > 1 && r.sharedMaterials.All(m => m != null) &&
                r.GetComponent<MeshFilter>() is MeshFilter f && f.sharedMesh != null && f.sharedMesh.isReadable &&
                AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/Models/Ships/ShipV3/", StringComparison.Ordinal) &&
                r.GetComponentInParent<Rigidbody>() == body && r.GetComponentInParent<ShipControlHandle>() == null &&
                r.GetComponentInParent<HarpoonGun>() == null && r.GetComponentInParent<ShipV3ClothMotion>() == null &&
                !dependent.Contains(r) && !moving.Any(t => r.transform.IsChildOf(t))).ToArray();
            var pairs = sources.SelectMany(r => r.sharedMaterials.Where(m => m != null).Distinct().Select(m => new { Renderer = r, Material = m }));
            int number = 0;
            var groups = pairs.GroupBy(p => new {
                p.Material, Cell = Mathf.FloorToInt(root.transform.InverseTransformPoint(p.Renderer.bounds.center).z / 8f),
                p.Renderer.shadowCastingMode, p.Renderer.receiveShadows, p.Renderer.renderingLayerMask,
                p.Renderer.lightProbeUsage, p.Renderer.reflectionProbeUsage, p.Renderer.motionVectorGenerationMode,
                p.Renderer.probeAnchor, Layer = p.Renderer.gameObject.layer }).ToArray();
            var eligible = new HashSet<MeshRenderer>(groups.Where(g => g.Count() > 1).SelectMany(g => g.Select(p => p.Renderer)));
            foreach (var group in groups)
            {
                var members = group.Select(p => p.Renderer).Where(eligible.Contains).ToArray();
                if (members.Length == 0) continue;
                string name = "ShipV3FixtureBatch_" + number++;
                var mesh = ShipV3RenderBatch.BuildMesh(members, group.Key.Material, root.transform, true);
                mesh = Store(mesh, Folder + "/" + name + ".asset");
                var node = new GameObject(name);
                node.transform.SetParent(root.transform, false);
                node.layer = group.Key.Layer;
                node.AddComponent<MeshFilter>().sharedMesh = mesh;
                var output = node.AddComponent<MeshRenderer>();
                output.sharedMaterial = group.Key.Material;
                output.shadowCastingMode = group.Key.shadowCastingMode;
                output.receiveShadows = group.Key.receiveShadows;
                output.renderingLayerMask = group.Key.renderingLayerMask;
                output.lightProbeUsage = group.Key.lightProbeUsage;
                output.reflectionProbeUsage = group.Key.reflectionProbeUsage;
                output.motionVectorGenerationMode = group.Key.motionVectorGenerationMode;
                output.probeAnchor = group.Key.probeAnchor;
                var batch = node.AddComponent<ShipV3RenderBatch>();
                batch.Sources = members;
                batch.CachedMesh = mesh;
                batch.SharedMaterial = group.Key.Material;
                batch.BatchAnchor = root.transform;
            }
            var batched = new HashSet<MeshRenderer>(root.GetComponentsInChildren<ShipV3RenderBatch>(true)
                .Where(b => b.name.StartsWith("ShipV3FixtureBatch_", StringComparison.Ordinal)).SelectMany(b => b.Sources));
            foreach (var source in batched)
            {
                var covered = new HashSet<Material>(root.GetComponentsInChildren<ShipV3RenderBatch>(true)
                    .Where(b => b.Sources.Contains(source)).Select(b => b.SharedMaterial));
                if (source.sharedMaterials.Any(m => m != null && !covered.Contains(m)))
                    throw new InvalidOperationException("Incomplete material coverage for " + source.name);
                source.enabled = false;
            }
            report.Add("Multi-material source renderers: " + batched.Count);
            return number;
        }

        static Mesh Store(Mesh mesh, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved);
            saved.MarkModified();
            saved.UploadMeshData(false);
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssetIfDirty(saved);
            UnityEngine.Object.DestroyImmediate(mesh);
            return saved;
        }
    }
}
