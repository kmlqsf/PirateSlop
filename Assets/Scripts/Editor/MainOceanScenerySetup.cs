using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class MainOceanScenerySetup
    {
        const string Path = "Assets/Resources/MainOcean/Scenery.prefab";
        static readonly string[] Names = { "Sea_Lagoon_Cave", "Reef_Moai_A", "Reef_Spires_A", "Reef_Spires_B", "Reef_Spires_C", "SeaArch_Huge_A", "Reef_ShallowField_A", "SmallSandyIsland", "TwoLandingIsland" };

        [MenuItem("PirateSlop/World/Prepare Main Ocean Scenery")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before preparing scenery.");
            var gallery = Resources.Load<EnvironmentTestGallery>(EnvironmentTestGallery.ResourcePath);
            if (gallery == null) throw new InvalidOperationException("The environment test gallery is missing.");
            var entries = new List<MainOceanScenery.Asset>();
            var versions = new List<string>();
            Directory.CreateDirectory("Assets/Models/World/MainOceanHorizon");
            AssetDatabase.Refresh();
            foreach (string name in Names)
            {
                var sample = gallery.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name && t.GetComponentInChildren<Renderer>(true) != null);
                if (sample == null) throw new InvalidOperationException("Scenery must come from the current test gallery: " + name);
                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sample.gameObject);
                string expected = (name.EndsWith("Island", StringComparison.Ordinal) ? "Assets/Prefabs/World/SandyIslands/" : "Assets/Prefabs/Environment/") + name + ".prefab";
                if (source != expected) throw new InvalidOperationException("Unexpected scenery source: " + source);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                float radius = Radius(prefab);
                bool island = name.EndsWith("Island", StringComparison.Ordinal);
                if (!island && prefab.GetComponentInChildren<CoastalRockVisual>(true) == null)
                    throw new InvalidOperationException("Modern coastal visuals are missing: " + name);
                float minScale = island ? 1f : name == "Reef_Spires_A" ? .78f : .88f;
                float maxScale = island ? 1f : name == "Reef_Spires_A" ? 1.28f : 1.16f;
                entries.Add(new MainOceanScenery.Asset { Key = name, Prefab = prefab, Radius = radius, MinScale = minScale, MaxScale = maxScale });
                versions.Add(name + ":" + radius.ToString("R", CultureInfo.InvariantCulture) + ":" + minScale.ToString("R", CultureInfo.InvariantCulture) + ":" + maxScale.ToString("R", CultureInfo.InvariantCulture) + ":" + AssetDatabase.GetAssetDependencyHash(source));
            }
            string revision = WorldLayout.Hash("main-scenery-2|" + DistantSceneryBaker.Revision + "|" + string.Join("|", versions));
            var existing = AssetDatabase.LoadAssetAtPath<MainOceanScenery>(Path);
            if (existing != null && existing.Revision == revision) return;
            foreach (var entry in entries)
                entry.HorizonMesh = DistantSceneryBaker.Bake(entry.Prefab, entry.Radius, "Assets/Models/World/MainOceanHorizon/" + entry.Key + ".asset");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/DistantScenery.shader");
            if (shader == null) throw new InvalidOperationException("The distant scenery shader is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/DistantScenery.mat");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, "Assets/Resources/DistantScenery.mat"); }
            material.enableInstancing = true; EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            Directory.CreateDirectory("Assets/Resources/MainOcean");
            AssetDatabase.Refresh();
            var root = existing != null ? PrefabUtility.LoadPrefabContents(Path) : new GameObject("MainOceanScenery");
            try
            {
                var catalog = root.GetComponent<MainOceanScenery>() ?? root.AddComponent<MainOceanScenery>();
                catalog.Assets = entries.ToArray(); catalog.Revision = revision;
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static float Radius(GameObject prefab)
        {
            float radius = 0f;
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Bounds bounds;
                if (collider is MeshCollider mesh && mesh.sharedMesh != null) bounds = mesh.sharedMesh.bounds;
                else if (collider is TerrainCollider terrain && terrain.terrainData != null) bounds = terrain.terrainData.bounds;
                else if (collider is BoxCollider box) bounds = new Bounds(box.center, box.size);
                else continue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = prefab.transform.InverseTransformPoint(collider.transform.TransformPoint(corner));
                    radius = Mathf.Max(radius, new Vector2(local.x, local.z).magnitude);
                }
            }
            if (radius < 5f) throw new InvalidOperationException("Scenery has no usable collision footprint: " + prefab.name);
            return Mathf.Ceil(radius + 5f);
        }
    }
}
