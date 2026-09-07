using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.Editor
{
    public static class SupplyCompositionSetup
    {
        const string Parts = "Assets/Prefabs/World/StarterIsland/";
        static GameObject Part(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Parts + name + ".prefab") ?? throw new InvalidOperationException("Missing module: " + name);

        [MenuItem("PirateSlop/World/Configure Supply Compositions")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int count = 1; count <= 3; count++)
            {
                string name = count == 1 ? "StarterSupplyCamp" : "SupplyCamp" + count + "Piers";
                var camp = PrefabUtility.LoadPrefabContents(Parts + name + ".prefab");
                try
                {
                    var children = camp.transform.Cast<Transform>().ToArray();
                    string[] scatter = { "Palm", "PalmBent", "PalmYoung", "RockMedium", "RockLarge", "Bush", "Fern" };
                    foreach (var child in children)
                        if (scatter.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    var composition = camp.AddComponent<SupplyIslandComposition>();
                    composition.Palms = new[] { Part("Palm"), Part("PalmBent"), Part("PalmYoung") };
                    composition.Rocks = new[] { Part("RockMedium"), Part("RockLarge") };
                    composition.Bush = Part("Bush"); composition.Fern = Part("Fern");
                    composition.Awning = Part("CanvasAwning"); composition.Barrel = Part("Barrel"); composition.Crate = Part("Crate");
                    composition.SideWalls = camp.transform.Cast<Transform>().Where(t => t.name == "Wall" && Mathf.Abs(t.localPosition.x) > 3.9f).Select(t => t.gameObject).ToArray();
                    composition.CacheAwnings = camp.transform.Cast<Transform>().Where(t => t.name == "CanvasAwning").Select(t => t.gameObject).ToArray();
                    var prefab = PrefabUtility.SaveAsPrefabAsset(camp, Parts + name + "Procedural.prefab");
                    string path = "Assets/Settings/World/" + (count == 1 ? "StarterSupplyIsland" : "SupplyIsland" + count + "Piers") + ".asset";
                    var definition = AssetDatabase.LoadAssetAtPath<LocationDefinition>(path);
                    var rule = definition.Points.First(p => p.StaticPrefab != null && p.Tag == "landmark");
                    rule.StaticPrefab = prefab; rule.PrefabVersion = Math.Max(rule.PrefabVersion, 3);
                    EditorUtility.SetDirty(definition);
                }
                finally { PrefabUtility.UnloadPrefabContents(camp); }
            }
            var profile = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
            profile.CatalogRevision = Math.Max(profile.CatalogRevision, 6);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            var map = WorldGenerator.Generate(profile, profile.Seed, 32, 0);
            System.IO.File.WriteAllText("Assets/Settings/World/ExampleMap.json", map.ToJson());
            AssetDatabase.ImportAsset("Assets/Settings/World/ExampleMap.json");
        }
    }
}
