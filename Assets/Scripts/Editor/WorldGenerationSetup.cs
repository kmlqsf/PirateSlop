using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.EditorTools
{
    public static class WorldGenerationSetup
    {
        static void Folder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }
        [MenuItem("PirateSlop/World/Configure Map Generator")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Folder("Assets/Settings", "World"); Folder("Assets/Materials", "World");
            var profile = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldProfile>();
                AssetDatabase.CreateAsset(profile, "Assets/Settings/World/DefaultWorld.asset");
                string[] names = { "SandyIsland", "HighIsland", "CrescentBay", "Atoll", "Reef", "SeaStack" };
                var definitions = new LocationDefinition[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    var definition = ScriptableObject.CreateInstance<LocationDefinition>();
                    definition.Settings.Id = names[i]; definition.Settings.Shape = (Landform)i;
                    definition.Settings.Radius = i == 5 ? new Vector2(15, 28) : i == 4 ? new Vector2(35, 65) : new Vector2(50, 95);
                    definition.Settings.Height = i == 1 ? new Vector2(30, 55) : i == 3 ? new Vector2(10, 14) : i == 5 ? new Vector2(25, 45) : new Vector2(12, 22);
                    if (i == 1 || i == 5) { definition.Settings.Ground = new Color(.35f, .37f, .3f); definition.Settings.Roughness = .6f; }
                    definition.Points = i == 4 ? new[] { new LocationPointRule { Tag = "underwater_poi", Count = 3, Height = new Vector2(-4, -.3f), MaxSlope = 35 } } : new[] {
                        new LocationPointRule { Tag = "land_spawn", Count = 3, Height = new Vector2(2, 12), MaxSlope = 20, Spacing = 15 },
                        new LocationPointRule { Tag = "loot", Count = 5, Height = new Vector2(1.5f, 35), MaxSlope = 30, Spacing = 8 },
                        new LocationPointRule { Tag = "landmark", Count = 1, Height = new Vector2(5, 60), MaxSlope = 30 }
                    };
                    AssetDatabase.CreateAsset(definition, "Assets/Settings/World/" + names[i] + ".asset"); definitions[i] = definition;
                }
                profile.Locations = definitions;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/World/IslandTerrain.mat");
            if (material == null)
            {
                var shader = Shader.Find("PirateSlop/IslandTerrain"); if (shader == null) throw new InvalidOperationException("Terrain shader missing.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, "Assets/Materials/World/IslandTerrain.mat");
            }
            profile.TerrainMaterial = material; EditorUtility.SetDirty(profile);
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var path in new[] { "Assets/Scenes/NetworkOcean.unity" })
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "ProceduralWorld");
                if (go == null) { go = new GameObject("ProceduralWorld"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); }
                var world = go.GetComponent<ProceduralWorld>() ?? go.AddComponent<ProceduralWorld>();
                world.Profile = profile; world.GenerateOnStart = false;
                if (go.GetComponent<WorldMapDisplay>() == null) go.AddComponent<WorldMapDisplay>();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
            var config = AssetDatabase.LoadAssetAtPath<PirateSlop.Networking.SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 9); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        }
        [MenuItem("PirateSlop/World/Export Map Layout")]
        public static void Export()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
            var path = EditorUtility.SaveFilePanel("Save map layout", "", "Map_" + profile.Seed + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;
            var world = ProceduralWorld.Instance;
            var config = AssetDatabase.LoadAssetAtPath<PirateSlop.Networking.SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            var layout = EditorApplication.isPlaying && world != null && world.Ready ? world.Layout : WorldGenerator.Generate(profile, profile.Seed, config.MaxPlayers, 0);
            System.IO.File.WriteAllText(path, layout.ToJson());
        }
    }
}
