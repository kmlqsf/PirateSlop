using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.Editor
{
    public static class StarterIslandSetup
    {
        const string Models = "Assets/Models/World/StarterIsland/";
        const string Prefabs = "Assets/Prefabs/World/StarterIsland/";
        const string Materials = "Assets/Materials/World/StarterIsland/";
        static readonly string[] Names = { "RockMedium", "RockLarge", "Palm", "Bush", "DockStraight", "DockPile", "Ramp", "Floor", "Wall", "Doorway", "RoofSlope", "Crate" };

        [MenuItem("PirateSlop/World/Configure Starter Island")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var name in Names)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx") == null) throw new InvalidOperationException("Missing model: " + name);
            Folder(Prefabs.TrimEnd('/')); Folder(Materials.TrimEnd('/'));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var layer = LayerMask.NameToLayer("WorldStatic");
            if (layer < 0)
            {
                var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layers = tags.FindProperty("layers");
                for (int i = 8; i < layers.arraySize; i++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    { layers.GetArrayElementAtIndex(i).stringValue = "WorldStatic"; layer = i; break; }
                if (layer < 0) throw new InvalidOperationException("No layer slot available for WorldStatic.");
                tags.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(tags.targetObject);
            }
            var palette = new Dictionary<string, Color> {
                { "Wood", new Color(.28f,.13f,.065f) }, { "WoodLight", new Color(.48f,.28f,.12f) },
                { "WoodDark", new Color(.14f,.075f,.045f) }, { "Leaf", new Color(.20f,.39f,.10f) },
                { "LeafLight", new Color(.37f,.52f,.15f) }, { "Rock", new Color(.37f,.40f,.39f) },
                { "RockLight", new Color(.48f,.50f,.46f) }, { "Metal", new Color(.10f,.14f,.15f) }
            };
            var materials = new Dictionary<string, Material>();
            foreach (var entry in palette)
            {
                var path = Materials + entry.Key + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.SetColor("_BaseColor", entry.Value); mat.SetFloat("_Smoothness", .12f);
                if (entry.Key.StartsWith("Leaf")) mat.SetFloat("_Cull", 0);
                EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
                materials.Add("Island_" + entry.Key, mat);
            }
            foreach (var name in Names)
            {
                var part = new GameObject(name);
                try
                {
                    var visual = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx"), part.transform);
                    visual.name = "Visual";
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && materials.ContainsKey(m.name) ? materials[m.name] : materials["Island_Wood"]).ToArray();
                    foreach (var t in part.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
                    switch (name)
                    {
                        case "Floor": Box(part, new Vector3(0,.08f,0), new Vector3(2,.16f,2)); break;
                        case "Wall": Box(part, new Vector3(0,1.5f,0), new Vector3(2,3,.24f)); break;
                        case "Doorway":
                            Box(part, new Vector3(-1.55f,1.5f,0), new Vector3(.9f,3,.24f));
                            Box(part, new Vector3(1.55f,1.5f,0), new Vector3(.9f,3,.24f));
                            Box(part, new Vector3(0,2.8f,0), new Vector3(2.2f,.4f,.24f)); break;
                        case "DockStraight": Box(part, new Vector3(0,0,0), new Vector3(2,.4f,4)); break;
                        case "DockPile": Box(part, new Vector3(0,4,0), new Vector3(.46f,8,.46f)); break;
                        case "Crate": Box(part, new Vector3(0,.5f,0), new Vector3(1.1f,1,.94f)); break;
                        case "Ramp":
                            var ramp = new GameObject("WalkSurface"); ramp.transform.SetParent(part.transform, false);
                            ramp.layer = layer; ramp.transform.localPosition = new Vector3(0,.57f,0);
                            ramp.transform.localRotation = Quaternion.Euler(Mathf.Atan(.25f) * Mathf.Rad2Deg,0,0);
                            Box(ramp, Vector3.zero, new Vector3(2,.1f,Mathf.Sqrt(17))); break;
                        case "Bush": break;
                        default:
                            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>()) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                            break;
                    }
                    PrefabUtility.SaveAsPrefabAsset(part, Prefabs + name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(part); }
            }
            var camp = new GameObject("StarterSupplyCamp");
            try
            {
                for (int x = -3; x <= 3; x += 2) for (int z = -3; z <= 3; z += 2) Place(camp,"Floor",new Vector3(x,.15f,z));
                foreach (var z in new [] { -4f,4f })
                {
                    Place(camp,"Doorway",new Vector3(0,.31f,z));
                    Place(camp,"Wall",new Vector3(-3,.31f,z)); Place(camp,"Wall",new Vector3(3,.31f,z));
                }
                foreach (var x in new [] { -4f,4f }) for (int z = -3; z <= 3; z += 2) Place(camp,"Wall",new Vector3(x,.31f,z),90);
                for (int z = -3; z <= 3; z += 2)
                { Place(camp,"RoofSlope",new Vector3(0,3.31f,z)); Place(camp,"RoofSlope",new Vector3(0,3.31f,z),180); }
                Place(camp,"Ramp",new Vector3(0,-.0372f,-6),180,new Vector3(1.2f,.31f,1));
                Place(camp,"Ramp",new Vector3(0,-.0372f,6),0,new Vector3(1.2f,.31f,1));
                foreach (var x in new [] {-3.8f,3.8f}) foreach (var z in new [] {-3.8f,3.8f}) Place(camp,"DockPile",new Vector3(x,-.5f,z),0,new Vector3(1,.10f,1));
                Place(camp,"Crate",new Vector3(-2,.31f,2));
                Place(camp,"Crate",new Vector3(-2.9f,.31f,2.8f),18);
                Place(camp,"Ramp",new Vector3(0,-2.2412f,-32),180,new Vector3(1.5f,2.01f,4));
                for (int z = -42; z >= -70; z -= 4) Place(camp,"DockStraight",new Vector3(0,-2.2f,z),0,new Vector3(1.5f,1,1));
                var terrain = new LocationRecord { Radius = 60, Height = 4, Type = new LocationSettings { Shape = Landform.SupplyIsland, Roughness = .16f }, Seed = 1 };
                for (int z = -40; z >= -72; z -= 8) foreach (var x in new [] {-1.3f,1.3f})
                {
                    float bottom = WorldGenerator.Height(terrain,new Vector3(x,0,z),45) - 4.15f - .5f;
                    Place(camp,"DockPile",new Vector3(x,bottom,z),0,new Vector3(1,(-1.85f-bottom)/8,1));
                }
                Place(camp,"RockMedium",new Vector3(-15,-.22f,-12),35);
                Place(camp,"RockLarge",new Vector3(16,-.22f,8),120);
                foreach (var p in new [] {new Vector3(-13,-.15f,6),new Vector3(12,-.15f,-12),new Vector3(8,-.15f,18),new Vector3(-18,-.15f,-5)}) Place(camp,"Palm",p,p.x*11);
                foreach (var p in new [] {new Vector3(-10,-.15f,-9),new Vector3(10,-.15f,8),new Vector3(-8,-.15f,14),new Vector3(15,-.15f,-6),new Vector3(-16,-.15f,10)}) Place(camp,"Bush",p,p.z*9);
                var prefab = PrefabUtility.SaveAsPrefabAsset(camp,Prefabs + "StarterSupplyCamp.prefab");
                var definitionPath = "Assets/Settings/World/StarterSupplyIsland.asset";
                var definition = AssetDatabase.LoadAssetAtPath<LocationDefinition>(definitionPath);
                if (definition == null) { definition = ScriptableObject.CreateInstance<LocationDefinition>(); AssetDatabase.CreateAsset(definition,definitionPath); }
                definition.Settings = new LocationSettings { Id = "supply_island", Shape = Landform.SupplyIsland, Weight = .25f, Radius = new Vector2(60,60), Height = new Vector2(4,4), Roughness = .16f };
                definition.Points = new [] {
                    new LocationPointRule { Tag = "landmark", Count = 1, AtLocationOrigin = true, StaticPrefab = prefab, PrefabVersion = 1 },
                    Point("loot",new Vector3(2,.36f,2)), Point("loot",new Vector3(-2,.36f,-1)), Point("loot",new Vector3(6,0,-12)),
                    Point("land_spawn",new Vector3(0,.4f,-10)), Point("landing",new Vector3(6,-2,-42))
                };
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                var profile = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
                if (!profile.Locations.Contains(definition)) profile.Locations = profile.Locations.Concat(new [] {definition}).ToArray();
                profile.CatalogRevision = Math.Max(profile.CatalogRevision,2);
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                var layout = WorldGenerator.Generate(profile,17421,32,0);
                System.IO.File.WriteAllText("Assets/Settings/World/ExampleMap.json",layout.ToJson());
                AssetDatabase.ImportAsset("Assets/Settings/World/ExampleMap.json");
            }
            finally { UnityEngine.Object.DestroyImmediate(camp); }
            Debug.Log("Starter island configured: 12 models, camp prefab, supply island, loot markers. No Play Mode test performed.");
        }

        static LocationPointRule Point(string tag, Vector3 offset) => new LocationPointRule { Tag = tag, Count = 1, AtLocationOrigin = true, LocalOffset = offset };
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0,path.LastIndexOf('/')); Folder(parent);
            AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
        }
        static void Box(GameObject target, Vector3 center, Vector3 size)
        {
            var collider = target.AddComponent<BoxCollider>(); collider.center = center; collider.size = size;
        }
        static void Place(GameObject parent, string name, Vector3 position, float yaw = 0, Vector3? scale = null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab"),parent.transform);
            instance.transform.localPosition = position; instance.transform.localRotation = Quaternion.Euler(0,yaw,0);
            instance.transform.localScale = scale ?? Vector3.one;
        }
    }
}
