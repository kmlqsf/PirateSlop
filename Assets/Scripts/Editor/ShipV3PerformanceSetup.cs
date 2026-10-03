using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PirateSlop.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PirateSlop.EditorTools
{
    [InitializeOnLoad]
    public static class ShipV3PerformanceSetup
    {
        const string PrefabPath = "Assets/Resources/Ships/ShipV3Test.prefab";
        const string MeshFolder = "Assets/Models/Ships/ShipV3/RuntimeMeshes/Collision";
        static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        static ShipV3PerformanceSetup()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    typeof(EditorSceneManager).GetMethod("ClearTargetSceneForNewGameObjects", Flags).Invoke(null, null);
            };
        }

        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(MeshFolder);
            AssetDatabase.Refresh();
            var editor = typeof(EditorSceneManager);
            var get = editor.GetMethod("GetTargetSceneForNewGameObjects", Flags);
            var set = editor.GetMethod("SetTargetSceneForNewGameObjects", Flags, null, new[] { typeof(Scene) }, null);
            var clear = editor.GetMethod("ClearTargetSceneForNewGameObjects", Flags);
            Scene previous = (Scene)get.Invoke(null, null);
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                set.Invoke(null, new object[] { root.scene });
                Configure(root);
                File.WriteAllText("Tools/ShipV3/PerformanceStatus.txt", "Saving optimized ship prefab");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                File.AppendAllText("Tools/ShipV3/PerformanceStatus.txt", "\nShip prefab saved");
            }
            finally
            {
                clear.Invoke(null, null);
                if (previous.IsValid() && previous.isLoaded && EditorSceneManager.IsPreviewScene(previous)) set.Invoke(null, new object[] { previous });
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
            ConfigurePipeline();
            File.AppendAllText("Tools/ShipV3/PerformanceStatus.txt", "\nPC rendering budget saved");
        }

        public static void Configure(GameObject root)
        {
            foreach (var previous in root.GetComponentsInChildren<ShipV3CollisionBatch>(true))
            {
                foreach (var source in previous.Sources) if (source != null) source.enabled = true;
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }
            var rig = root.GetComponent<ShipV3VisualRig>();
            var features = root.GetComponent<ShipV3Features>();
            var moving = rig.Motions.Select(m => m.Target)
                .Concat(new[] { rig.Rudder, features.AnchorTravel, features.DispenserLever })
                .Concat(features.Attachments.Where(a => a.Fall).Select(a => a.Object))
                .Concat(root.GetComponentsInChildren<HelmInteraction>(true).SelectMany(h => new[] { h.transform, h.Wheel }))
                .Where(t => t != null).ToArray();
            var body = root.GetComponent<Rigidbody>();
            var sources = root.GetComponent<ShipDestruction>().Sections
                .Where(s => s != null && !s.SurfaceDamage && s.Intact != null && s.Intact.GetComponents<MeshCollider>().Length == 1)
                .Select(s => s.Intact.GetComponent<MeshCollider>())
                .Where(c => c != null && !c.convex && !c.isTrigger && c.sharedMesh != null && c.sharedMesh.isReadable
                    && c.GetComponentInParent<Rigidbody>() == body && !moving.Any(t => c.transform.IsChildOf(t))).Distinct().ToArray();
            int number = 0;
            foreach (var group in sources.GroupBy(c => new
            {
                Cell = Vector3Int.FloorToInt(Vector3.Scale(root.transform.InverseTransformPoint(c.transform.position), new Vector3(.25f, .333333f, .125f))),
                Layer = c.gameObject.layer,
                Material = c.sharedMaterial
            }))
            {
                var members = group.ToArray();
                for (int start = 0; start < members.Length; start += 64)
                {
                    var chunk = members.Skip(start).Take(64).ToArray();
                    if (chunk.Length < 2) continue;
                    string name = "ShipV3Collision_" + number++;
                    var node = new GameObject(name);
                    node.layer = group.Key.Layer;
                    node.transform.SetParent(root.transform, false);
                    var mesh = ShipV3CollisionBatch.BuildMesh(chunk, node.transform, true);
                    string path = MeshFolder + "/" + name + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
                    else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                    AssetDatabase.SaveAssetIfDirty(saved);
                    var collider = node.AddComponent<MeshCollider>();
                    collider.sharedMaterial = group.Key.Material;
                    collider.sharedMesh = saved;
                    var batch = node.AddComponent<ShipV3CollisionBatch>();
                    batch.Sources = chunk;
                    batch.CachedMesh = saved;
                    batch.SourceBounds = chunk.Select(c => BoundsIn(c, node.transform)).ToArray();
                    foreach (var source in chunk) source.enabled = false;
                }
            }
            if (root.GetComponent<ShipV3RenderBudget>() == null) root.AddComponent<ShipV3RenderBudget>();
            foreach (var light in root.GetComponentsInChildren<Light>(true)) light.shadows = LightShadows.None;
            ConfigureShadows(root);
            File.WriteAllText("Tools/ShipV3/PerformanceStatus.txt", "Merged " + sources.Length + " stationary source colliders into " + number + " spatial groups");
        }

        public static void UpdatePresentation()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var editor = typeof(EditorSceneManager);
            var set = editor.GetMethod("SetTargetSceneForNewGameObjects", Flags, null, new[] { typeof(Scene) }, null);
            var clear = editor.GetMethod("ClearTargetSceneForNewGameObjects", Flags);
            GameObject root = null;
            try
            {
                File.WriteAllText("Tools/ShipV3/PresentationStatus.txt", "Loading ship prefab");
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                set.Invoke(null, new object[] { root.scene });
                ShipV3GameplayRepair.ConfigureDispenser(root);
                ShipV3GameplayRepair.ConfigureFlags(root);
                ConfigureShadows(root);
                File.WriteAllText("Tools/ShipV3/PresentationStatus.txt", "Saving ship prefab");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var proxies = root.GetComponentsInChildren<ShipV3RenderBatch>(true).Where(b => b.CachedShadowMesh != null).ToArray();
                File.WriteAllText("Tools/ShipV3/PresentationStatus.txt", "Saved horizontal extended dispenser grip and raised pinned flags; shadow proxy triangles "
                    + proxies.Sum(b => (long)b.CachedMesh.GetIndexCount(0) / 3) + " -> " + proxies.Sum(b => (long)b.CachedShadowMesh.GetIndexCount(0) / 3));
            }
            finally
            {
                clear.Invoke(null, null);
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ConfigureShadows(GameObject root)
        {
            string folder = "Assets/Models/Ships/ShipV3/RuntimeMeshes/Shadows";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            foreach (var batch in root.GetComponentsInChildren<ShipV3RenderBatch>(true))
            {
                if (batch.CachedMesh == null || batch.CachedMesh.GetIndexCount(0) < 90000) continue;
                if (batch.SharedMaterial.HasProperty("_AlphaClip") && batch.SharedMaterial.GetFloat("_AlphaClip") > .5f) continue;
                var original = ShipV3RenderBatch.BuildMesh(batch.Sources, batch.SharedMaterial, batch.transform, true);
                var mesh = ShadowMesh(original);
                UnityEngine.Object.DestroyImmediate(original);
                string path = folder + "/" + batch.name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
                else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                AssetDatabase.SaveAssetIfDirty(saved);
                if (batch.ShadowProxy == null)
                {
                    var node = new GameObject("ShadowProxy");
                    node.layer = batch.gameObject.layer;
                    node.transform.SetParent(batch.transform, false);
                    node.AddComponent<MeshFilter>();
                    batch.ShadowProxy = node.AddComponent<MeshRenderer>();
                }
                batch.CachedShadowMesh = saved;
                batch.ShadowProxy.GetComponent<MeshFilter>().sharedMesh = saved;
                batch.ShadowProxy.sharedMaterial = batch.SharedMaterial;
                batch.ShadowProxy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                batch.ShadowProxy.receiveShadows = false;
                batch.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static Mesh ShadowMesh(Mesh source)
        {
            var cells = new System.Collections.Generic.Dictionary<Vector3Int, int>();
            var points = new System.Collections.Generic.List<Vector3>();
            var counts = new System.Collections.Generic.List<int>();
            var input = source.vertices;
            var remap = new int[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                var key = Vector3Int.FloorToInt(input[i] / .02f);
                if (!cells.TryGetValue(key, out int index))
                {
                    index = points.Count; cells.Add(key, index); points.Add(Vector3.zero); counts.Add(0);
                }
                remap[i] = index;
                points[index] += input[i]; counts[index]++;
            }
            for (int i = 0; i < points.Count; i++) points[i] /= counts[i];
            var triangles = new System.Collections.Generic.List<int>();
            var unique = new System.Collections.Generic.HashSet<(int, int, int)>();
            var indices = source.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = remap[indices[i]], b = remap[indices[i + 1]], c = remap[indices[i + 2]];
                if (a == b || b == c || a == c) continue;
                var key = a <= b && a <= c ? (a, b, c) : b <= a && b <= c ? (b, c, a) : (c, a, b);
                if (!unique.Add(key)) continue;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            var mesh = new Mesh { name = "ShipV3ShadowProxy", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(points); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            mesh.bounds = source.bounds;
            return mesh;
        }

        static Bounds BoundsIn(MeshCollider source, Transform anchor)
        {
            var input = source.sharedMesh.bounds;
            var matrix = anchor.worldToLocalMatrix * source.transform.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(input.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(input.center + Vector3.Scale(input.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return bounds;
        }

        static void ConfigurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            pipeline.shadowDistance = 80f;
            pipeline.shadowCascadeCount = 2;
            pipeline.cascade2Split = .3f;
            pipeline.additionalLightsShadowmapResolution = 1024;
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_SoftShadowQuality").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssetIfDirty(pipeline);
        }
    }
}
