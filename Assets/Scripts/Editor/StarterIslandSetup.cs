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
        static readonly string[] Names = { "RockMedium", "RockLarge", "Palm", "Bush", "DockStraight", "DockPile", "Ramp", "Floor", "Wall", "Doorway", "RoofSlope", "Crate", "CliffWallA", "CliffWallB", "CliffWallC", "PalmBent", "PalmYoung", "Fern", "Barrel", "RopeCoil", "Lantern", "CanvasAwning" };

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
                { "RockLight", new Color(.48f,.50f,.46f) }, { "Metal", new Color(.10f,.14f,.15f) }, { "Rope", new Color(.53f,.41f,.24f) }, { "Glass", new Color(.95f,.58f,.18f) }, { "Canvas", new Color(.64f,.52f,.32f) }
            };
            var materials = new Dictionary<string, Material>();
            foreach (var entry in palette)
            {
                var path = Materials + entry.Key + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.SetColor("_BaseColor", entry.Value); mat.SetFloat("_Smoothness", .12f);
                if (entry.Key.StartsWith("Leaf") || entry.Key == "Canvas") mat.SetFloat("_Cull", 0);
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
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && materials.ContainsKey(m.name.Split('.')[0]) ? materials[m.name.Split('.')[0]] : materials["Island_Wood"]).ToArray();
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
                        case "Bush": case "Fern": case "RopeCoil": case "Lantern": break;
                        case "Barrel": Box(part,new Vector3(0,.7f,0),new Vector3(1.05f,1.4f,1.05f)); break;
                        case "CanvasAwning":
                            foreach(float x in new [] {-2.6f,2.6f}) foreach(float z in new [] {-1.7f,1.7f}) Box(part,new Vector3(x,1.05f,z),new Vector3(.19f,5.1f,.19f)); break;
                        default:
                            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>()) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                            break;
                    }
                    PrefabUtility.SaveAsPrefabAsset(part, Prefabs + name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(part); }
            }
            ConfigureLocations();
            Debug.Log("Supply islands configured: 22 models, hills, rocky shores and 1-3 piers. Play Mode not tested.");
        }

        static void ConfigureLocations()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
            for (int count = 1; count <= 3; count++)
            {
                var terrain = new LocationRecord { Radius = 90, Height = 6, Type = new LocationSettings { Shape = Landform.SupplyIsland, PierCount = count, Roughness = .16f }, Seed = 1 };
                var camp = new GameObject(count == 1 ? "StarterSupplyCamp" : "SupplyCamp" + count + "Piers");
                try
                {
                    BuildHut(camp);
                    var rules = new List<LocationPointRule> {
                        Point("loot",new Vector3(2,.36f,2)), Point("loot",new Vector3(-2,.36f,-1)),
                        Point("land_spawn",new Vector3(0,.4f,-10))
                    };
                    for (int pier = 0; pier < count; pier++)
                    {
                        float yaw = -pier * 360f / count;
                        var entrance = new GameObject("PierEntrance_" + (pier + 1)); entrance.transform.SetParent(camp.transform,false);
                        entrance.transform.localRotation = Quaternion.Euler(0,yaw,0);
                        Place(entrance,"Ramp",new Vector3(0,-4.094f,-64),180,new Vector3(2,1.2f,3));
                        for (int z = -72; z >= -100; z -= 4) Place(entrance,"DockStraight",new Vector3(0,-4.15f,z),0,new Vector3(2,1,1));
                        for (int z = -70; z >= -102; z -= 8) foreach (float x in new [] {-1.65f,1.65f})
                        {
                            var local = Quaternion.Euler(0,yaw,0) * new Vector3(x,0,z);
                            float bottom = WorldGenerator.Height(terrain,local,45) - 6.15f - .5f;
                            Place(entrance,"DockPile",new Vector3(x,bottom,z),0,new Vector3(1,(-3.8f-bottom)/8,1));
                        }
                        Place(entrance,"RopeCoil",new Vector3(1,-3.94f,-97));
                        Place(entrance,"Barrel",new Vector3(-1.25f,-3.95f,-80));
                        Place(entrance,"Lantern",new Vector3(-1.25f,-2.55f,-80));
                        rules.Add(Point("landing",Quaternion.Euler(0,yaw,0) * new Vector3(0,-3.9f,-97)));
                        float cacheAngle = (pier + .5f) * Mathf.PI * 2 / count;
                        var cache = new Vector3(Mathf.Sin(cacheAngle)*20,0,-Mathf.Cos(cacheAngle)*20);
                        PlaceOnGround(camp,terrain,"CanvasAwning",cache,cacheAngle*Mathf.Rad2Deg);
                        PlaceOnGround(camp,terrain,"Barrel",cache + new Vector3(1.6f,0,.8f),20);
                        PlaceOnGround(camp,terrain,"Crate",cache + new Vector3(-1.5f,0,1),-15);
                        PlaceOnGround(camp,terrain,"RopeCoil",cache + new Vector3(-.8f,0,-1),0);
                        var loot = cache + new Vector3(0,0,-1);
                        loot.y = WorldGenerator.Height(terrain,loot,45) - 6.15f + .15f;
                        rules.Add(Point("loot",loot));
                    }
                    var rng = new MapRandom((uint)(7400+count));
                    for (int i = 0; i < 40; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 40;
                        var p = new Vector3(Mathf.Sin(angle)*75.6f,0,-Mathf.Cos(angle)*75.6f);
                        if (WorldGenerator.ApproachDistance(new Vector2(p.x,p.z),count)<14) continue;
                        Place(camp,"CliffWall"+(char)('A'+i%3),new Vector3(p.x,-9.15f,p.z),-angle*Mathf.Rad2Deg,
                            new Vector3(rng.Range(1.05f,1.2f),rng.Range(.92f,1.18f),rng.Range(.9f,1.15f)));
                    }
                    var placed = new List<Vector3>();
                    Scatter(camp,terrain,ref rng,placed,new [] {"Palm","PalmBent","PalmYoung"},28,5,.2f);
                    Scatter(camp,terrain,ref rng,placed,new [] {"RockMedium","RockLarge"},16,4,.3f);
                    Scatter(camp,terrain,ref rng,placed,new [] {"Bush"},38,2.2f,.12f);
                    Scatter(camp,terrain,ref rng,placed,new [] {"Fern"},32,1.6f,.02f);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(camp,Prefabs + camp.name + ".prefab");
                    var path = "Assets/Settings/World/" + (count == 1 ? "StarterSupplyIsland" : "SupplyIsland"+count+"Piers") + ".asset";
                    var definition = AssetDatabase.LoadAssetAtPath<LocationDefinition>(path);
                    if (definition == null) { definition = ScriptableObject.CreateInstance<LocationDefinition>(); AssetDatabase.CreateAsset(definition,path); }
                    definition.Settings = new LocationSettings { Id = count == 1 ? "supply_island" : "supply_"+count+"piers", Shape = Landform.SupplyIsland, PierCount = count, Weight = .4f, Radius = new Vector2(90,90), Height = new Vector2(6,6), Roughness = .16f };
                    rules.Insert(0,new LocationPointRule { Tag="landmark", Count=1, AtLocationOrigin=true, StaticPrefab=prefab, PrefabVersion=2 });
                    definition.Points=rules.ToArray(); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                    if (!profile.Locations.Contains(definition)) profile.Locations=profile.Locations.Concat(new [] {definition}).ToArray();
                }
                finally { UnityEngine.Object.DestroyImmediate(camp); }
            }
            profile.CatalogRevision=Math.Max(profile.CatalogRevision,3);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            var layout=WorldGenerator.Generate(profile,17421,32,0);
            System.IO.File.WriteAllText("Assets/Settings/World/ExampleMap.json",layout.ToJson());
            AssetDatabase.ImportAsset("Assets/Settings/World/ExampleMap.json");
        }

        static void BuildHut(GameObject camp)
        {
            for (int x=-3;x<=3;x+=2) for(int z=-3;z<=3;z+=2) Place(camp,"Floor",new Vector3(x,.15f,z));
            foreach(float z in new [] {-4f,4f})
            {
                Place(camp,"Doorway",new Vector3(0,.31f,z));
                Place(camp,"Wall",new Vector3(-3,.31f,z)); Place(camp,"Wall",new Vector3(3,.31f,z));
            }
            foreach(float x in new [] {-4f,4f}) for(int z=-3;z<=3;z+=2) Place(camp,"Wall",new Vector3(x,.31f,z),90);
            for(int z=-3;z<=3;z+=2) { Place(camp,"RoofSlope",new Vector3(0,3.31f,z)); Place(camp,"RoofSlope",new Vector3(0,3.31f,z),180); }
            Place(camp,"Ramp",new Vector3(0,-.0372f,-6),180,new Vector3(1.2f,.31f,1));
            Place(camp,"Ramp",new Vector3(0,-.0372f,6),0,new Vector3(1.2f,.31f,1));
            foreach(float x in new [] {-3.8f,3.8f}) foreach(float z in new [] {-3.8f,3.8f}) Place(camp,"DockPile",new Vector3(x,-.5f,z),0,new Vector3(1,.1f,1));
            Place(camp,"Crate",new Vector3(-2,.31f,2)); Place(camp,"Crate",new Vector3(-2.9f,.31f,2.8f),18);
            Place(camp,"Barrel",new Vector3(2.9f,.31f,-2)); Place(camp,"RopeCoil",new Vector3(-2,1.35f,2));
            Place(camp,"Lantern",new Vector3(2.9f,1.73f,-2));
        }

        static void Scatter(GameObject parent, LocationRecord terrain, ref MapRandom rng, List<Vector3> placed, string[] names, int count, float spacing, float sink)
        {
            for (int attempt=0,added=0;attempt<count*60 && added<count;attempt++)
            {
                float angle=rng.Range(0,Mathf.PI*2),radius=rng.Range(15,66);
                var p=new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);
                if(WorldGenerator.ApproachDistance(new Vector2(p.x,p.z),terrain.Type.PierCount)<9) continue;
                bool cache=false;
                for(int i=0;i<terrain.Type.PierCount;i++)
                {
                    float a=(i+.5f)*Mathf.PI*2/terrain.Type.PierCount;
                    if(Vector3.Distance(p,new Vector3(Mathf.Sin(a)*20,0,-Mathf.Cos(a)*20))<6) cache=true;
                }
                if(cache || placed.Any(other=>Vector3.Distance(other,p)<spacing)) continue;
                float height=WorldGenerator.Height(terrain,p,45);
                float dx=WorldGenerator.Height(terrain,p+Vector3.right,45)-height;
                float dz=WorldGenerator.Height(terrain,p+Vector3.forward,45)-height;
                if(dx*dx+dz*dz>.3f) continue;
                placed.Add(p);
                p.y=height-6.15f-sink;
                float scale=rng.Range(.8f,1.2f);
                Place(parent,names[added%names.Length],p,rng.Range(0,360),Vector3.one*scale);
                added++;
            }
        }

        static void PlaceOnGround(GameObject parent, LocationRecord terrain, string name, Vector3 p, float yaw)
        {
            p.y=WorldGenerator.Height(terrain,p,45)-6.15f;
            Place(parent,name,p,yaw);
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
