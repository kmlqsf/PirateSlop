using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.Editor
{
    public static class LocationExpansionSetup
    {
        const string Parts = "Assets/Prefabs/World/LocationExpansion/";
        const string Starter = "Assets/Prefabs/World/StarterIsland/";
        const string Materials = "Assets/Materials/World/LocationExpansion/";
        static readonly string[] Names = { "StoneWall", "BrokenWall", "StoneCorner", "StoneArch", "StonePillar", "StoneFloor", "StoneStairs", "Rubble", "WreckHull", "WreckBow", "WreckStern", "WreckDeck", "WreckMast", "BranchCoral", "FanCoral", "Seaweed", "DockCorner", "Railing", "WindowWall", "Shutter", "BeaconTower" };

        [MenuItem("PirateSlop/World/Configure Location Expansion")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (string name in Names)
                if (!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/World/LocationExpansion/"+name+".fbx")) throw new InvalidOperationException("Missing model "+name);
            Folder(Parts.TrimEnd('/')); Folder(Materials.TrimEnd('/'));
            BuildParts();
            var profile=AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
            for (int variant=0;variant<2;variant++)
            {
                BuildLand(profile,"smuggler_cove",variant);
                BuildLand(profile,"ruined_outpost",variant);
                BuildLand(profile,"wreck_island",variant);
                BuildPassage(profile,false,variant);
                BuildPassage(profile,true,variant);
            }
            profile.CatalogRevision=Math.Max(profile.CatalogRevision,4);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            System.IO.File.WriteAllText("Assets/Settings/World/ExampleMap.json",WorldGenerator.Generate(profile,17421,32,0).ToJson());
            AssetDatabase.ImportAsset("Assets/Settings/World/ExampleMap.json");
            Debug.Log("Location expansion saved: five themes, two layouts each. Gameplay not tested.");
        }

        static void BuildParts()
        {
            var palette=new Dictionary<string,Color> {
                {"Wood",new Color(.28f,.13f,.065f)}, {"WoodLight",new Color(.48f,.28f,.12f)}, {"WoodDark",new Color(.14f,.075f,.045f)},
                {"Metal",new Color(.10f,.14f,.15f)}, {"Stone",new Color(.40f,.42f,.37f)}, {"StoneLight",new Color(.53f,.52f,.44f)},
                {"Coral",new Color(.60f,.28f,.15f)}, {"CoralLight",new Color(.73f,.46f,.24f)}, {"Seaweed",new Color(.13f,.31f,.24f)},
                {"Canvas",new Color(.64f,.52f,.32f)}, {"Glass",new Color(.95f,.58f,.18f)}
            };
            var materials=new Dictionary<string,Material>();
            foreach(var pair in palette)
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(Materials+pair.Key+".mat");
                if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,Materials+pair.Key+".mat"); }
                material.SetColor("_BaseColor",pair.Value); material.SetFloat("_Smoothness",.12f);
                if(pair.Key=="Canvas"||pair.Key=="Seaweed") material.SetFloat("_Cull",0);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); materials.Add("Location_"+pair.Key,material);
            }
            foreach(string name in Names)
            {
                var part=new GameObject(name);
                try
                {
                    var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/World/LocationExpansion/"+name+".fbx"),part.transform);
                    visual.name="Visual";
                    foreach(var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterials=r.sharedMaterials.Select(m=>materials[m.name.Split('.')[0]]).ToArray();
                    foreach(var t in part.GetComponentsInChildren<Transform>()) t.gameObject.layer=LayerMask.NameToLayer("WorldStatic");
                    if(name=="WindowWall")
                    {
                        Box(part,new Vector3(-.78f,1.5f,0),new Vector3(.44f,3,.2f)); Box(part,new Vector3(.78f,1.5f,0),new Vector3(.44f,3,.2f));
                        Box(part,new Vector3(0,.55f,0),new Vector3(1.13f,1.1f,.2f)); Box(part,new Vector3(0,2.65f,0),new Vector3(1.13f,.7f,.2f));
                    }
                    else if(name=="WreckDeck") Box(part,new Vector3(0,.09f,0),new Vector3(5,.18f,6));
                    else if(name=="StoneFloor") Box(part,new Vector3(0,.09f,0),new Vector3(4,.18f,4));
                    else if(name=="DockCorner") Box(part,Vector3.zero,new Vector3(2,.4f,2));
                    else if(name!="BranchCoral"&&name!="FanCoral"&&name!="Seaweed"&&name!="Shutter")
                        foreach(var f in visual.GetComponentsInChildren<MeshFilter>()) f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;
                    PrefabUtility.SaveAsPrefabAsset(part,Parts+name+".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(part); }
            }
        }

        static void BuildLand(WorldProfile profile,string theme,int variant)
        {
            int count=theme=="smuggler_cove"?variant+1:variant+2;
            string baseName=count==1?"StarterSupplyIsland":"SupplyIsland"+count+"Piers";
            var baseDefinition=AssetDatabase.LoadAssetAtPath<LocationDefinition>("Assets/Settings/World/"+baseName+".asset");
            var camp=UnityEngine.Object.Instantiate(baseDefinition.Points[0].StaticPrefab);
            if(PrefabUtility.IsPartOfPrefabInstance(camp)) PrefabUtility.UnpackPrefabInstance(camp,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
            camp.name=theme+"_"+(variant==0?"a":"b");
            try
            {
                foreach(var t in camp.transform.Cast<Transform>().ToArray())
                    if(new Vector2(t.localPosition.x,t.localPosition.z).magnitude<10 && !t.name.StartsWith("PierEntrance")) UnityEngine.Object.DestroyImmediate(t.gameObject);
                var settings=JsonUtility.FromJson<LocationSettings>(JsonUtility.ToJson(baseDefinition.Settings));
                settings.Id=camp.name; settings.Weight=.35f; settings.LayoutVariant=variant;
                if(theme=="smuggler_cove") settings.Shape=Landform.SmugglerCove;
                var terrain=new LocationRecord {Type=settings,Radius=90,Height=6,Seed=1};
                var rules=baseDefinition.Points.Where(p=>p.Tag!="landmark" && (p.Tag!="loot" || new Vector2(p.LocalOffset.x,p.LocalOffset.z).magnitude>10))
                    .Select(p=>JsonUtility.FromJson<LocationPointRule>(JsonUtility.ToJson(p))).ToList();
                if(theme=="smuggler_cove")
                {
                    AdjustCoveDecor(camp,terrain,baseDefinition.Settings);
                    if(variant==0) Warehouse(camp,Vector3.zero,8,10);
                    else { Warehouse(camp,new Vector3(-4,0,0),6,6); Warehouse(camp,new Vector3(4,0,0),6,6); }
                    Place(camp,"Crate",new Vector3(-2,.31f,2)); Place(camp,"Barrel",new Vector3(2,.31f,2));
                    rules.Add(Point("loot",new Vector3(-2,.36f,-2))); rules.Add(Point("loot",new Vector3(2,.36f,-2)));
                    var beacon=new Vector3(-24,0,13); beacon.y=WorldGenerator.Height(terrain,beacon,45)-6.15f-.25f;
                    Place(camp,"BeaconTower",beacon,0,new Vector3(.75f,.75f,.75f));
                }
                else if(theme=="ruined_outpost") Ruins(camp,variant,rules);
                else Wreck(camp,variant,rules);
                var prefab=PrefabUtility.SaveAsPrefabAsset(camp,Parts+camp.name+".prefab");
                rules.Insert(0,new LocationPointRule {Tag="landmark",Count=1,AtLocationOrigin=true,StaticPrefab=prefab,PrefabVersion=1});
                SaveDefinition(profile,camp.name,settings,rules);
            }
            finally { UnityEngine.Object.DestroyImmediate(camp); }
        }

        static void AdjustCoveDecor(GameObject camp,LocationRecord terrain,LocationSettings oldSettings)
        {
            var original=new LocationRecord {Type=oldSettings,Radius=90,Height=6,Seed=1};
            foreach(var entrance in camp.transform.Cast<Transform>().Where(t=>t.name.StartsWith("PierEntrance")))
                foreach(var pile in entrance.Cast<Transform>().Where(t=>t.name.StartsWith("DockPile")))
                {
                    var p=pile.localPosition;
                    var surface=entrance.localRotation*new Vector3(p.x,0,p.z);
                    float bottom=WorldGenerator.Height(terrain,surface,45)-6.65f;
                    pile.localPosition=new Vector3(p.x,bottom,p.z);
                    pile.localScale=new Vector3(1,(-3.8f-bottom)/8,1);
                }
            foreach(var t in camp.transform.Cast<Transform>().ToArray())
            {
                if(!new [] {"Palm","Bush","Fern","Rock"}.Any(prefix=>t.name.StartsWith(prefix))) continue;
                var p=t.localPosition; var surface=new Vector3(p.x,0,p.z);
                float h=WorldGenerator.Height(terrain,surface,45);
                if(h<1.5f) UnityEngine.Object.DestroyImmediate(t.gameObject);
                else { p.y+=h-WorldGenerator.Height(original,surface,45); t.localPosition=p; }
            }
        }

        static void Warehouse(GameObject parent,Vector3 offset,int width,int length)
        {
            var room=new GameObject("Warehouse"); room.transform.SetParent(parent.transform,false); room.transform.localPosition=offset;
            for(int x=-width/2+1;x<width/2;x+=2) for(int z=-length/2+1;z<length/2;z+=2) Place(room,"Floor",new Vector3(x,.15f,z));
            foreach(float z in new [] {-length*.5f,length*.5f})
            {
                float doorX=width==6?-1:0;
                Place(room,"Doorway",new Vector3(doorX,.31f,z));
                if(width==8) Place(room,"Wall",new Vector3(-3,.31f,z));
                Place(room,"Wall",new Vector3(width/2-1,.31f,z));
                Place(room,"Ramp",new Vector3(doorX,-.0372f,z+Mathf.Sign(z)*2),z<0?180:0,new Vector3(1.2f,.31f,1));
            }
            foreach(float x in new [] {-width*.5f,width*.5f}) for(int z=-length/2+1;z<length/2;z+=2)
            {
                Place(room,z%3==0?"Wall":"WindowWall",new Vector3(x,.31f,z),90);
                if(z%3!=0) Place(room,"Shutter",new Vector3(x+Mathf.Sign(x)*.12f,1.41f,z+.72f),x>0?130:50);
            }
            for(int z=-length/2+1;z<length/2;z+=2) foreach(float yaw in new [] {0f,180f})
                Place(room,"RoofSlope",new Vector3(0,3.31f,z),yaw,new Vector3((width*.5f+.5f)/4.5f,1,1));
        }

        static void Ruins(GameObject camp,int variant,List<LocationPointRule> rules)
        {
            for(int x=-4;x<=4;x+=4) for(int z=-4;z<=4;z+=4) Place(camp,"StoneFloor",new Vector3(x,.13f,z));
            foreach(float z in new [] {-6f,6f})
            {
                Place(camp,"StoneArch",new Vector3(0,.31f,z));
                Place(camp,"BrokenWall",new Vector3(-4,.31f,z)); Place(camp,"StoneWall",new Vector3(4,.31f,z));
            }
            foreach(float x in new [] {-6f,6f}) for(int z=-4;z<=4;z+=4) Place(camp,z==0?"BrokenWall":"StoneWall",new Vector3(x,.31f,z),90);
            if(variant==0)
            {
                foreach(float x in new [] {-3f,3f}) foreach(float z in new [] {-3f,3f}) Place(camp,"StonePillar",new Vector3(x,.31f,z));
                Place(camp,"CanvasAwning",new Vector3(0,.31f,2),90,new Vector3(.9f,1,.9f));
                Place(camp,"Crate",new Vector3(-4,.31f,3));
                rules.Add(Point("loot",new Vector3(0,.36f,2))); rules.Add(Point("loot",new Vector3(4,.36f,-3)));
            }
            else
            {
                Place(camp,"StoneWall",new Vector3(0,.31f,3),0,new Vector3(1,.5f,6));
                Place(camp,"StoneFloor",new Vector3(0,1.63f,3)); Place(camp,"StoneStairs",new Vector3(0,.31f,-1),180);
                Place(camp,"StonePillar",new Vector3(-4,.31f,-3)); Place(camp,"StonePillar",new Vector3(4,.31f,3));
                Place(camp,"Railing",new Vector3(-2,1.81f,3),90); Place(camp,"Railing",new Vector3(2,1.81f,3),90);
                rules.Add(Point("loot",new Vector3(0,1.86f,3))); rules.Add(Point("loot",new Vector3(-4,.36f,2)));
            }
            Place(camp,"Rubble",new Vector3(-8,-.1f,4),35); Place(camp,"Rubble",new Vector3(8,-.1f,-3),130);
        }

        static void Wreck(GameObject camp,int variant,List<LocationPointRule> rules)
        {
            var wreck=new GameObject("AccessibleWreck"); wreck.transform.SetParent(camp.transform,false);
            wreck.transform.localRotation=Quaternion.Euler(0,variant==0?0:90,0);
            Place(wreck,"WreckHull",Vector3.zero); Place(wreck,"WreckBow",new Vector3(0,0,6),180);
            Place(wreck,"WreckDeck",new Vector3(0,1.1f,0));
            Place(wreck,"Ramp",new Vector3(0,-.1536f,-5),180,new Vector3(1.4f,1.28f,1));
            Place(wreck,"WreckStern",new Vector3(8,-.3f,3),variant==0?55:125);
            var mast=Place(wreck,"WreckMast",new Vector3(0,.5f,2)); mast.transform.localRotation=Quaternion.Euler(30,0,65);
            Place(wreck,"Crate",new Vector3(-1.5f,1.28f,1.5f)); Place(wreck,"Barrel",new Vector3(1.5f,1.28f,1.5f));
            Place(wreck,"RopeCoil",new Vector3(1.4f,1.29f,-1.6f));
            var rotation=wreck.transform.localRotation;
            rules.Add(Point("loot",rotation*new Vector3(-1.4f,1.33f,-1.5f)));
            rules.Add(Point("loot",rotation*new Vector3(1.2f,1.33f,.1f)));
            rules.Add(Point("loot",rotation*new Vector3(6,.15f,-4)));
        }

        static void BuildPassage(WorldProfile profile,bool reef,int variant)
        {
            string id=(reef?"reef_passage_":"rock_passage_")+(variant==0?"a":"b");
            var settings=new LocationSettings {Id=id,Shape=reef?Landform.ReefPassage:Landform.RockPassage,LayoutVariant=variant,Weight=.4f,Radius=new Vector2(100,100),Height=new Vector2(18,18),Roughness=.15f};
            var terrain=new LocationRecord {Type=settings,Radius=100,Height=18,Seed=1};
            var root=new GameObject(id);
            try
            {
                var rng=new MapRandom((uint)(8700+variant+(reef?30:0)));
                for(int side=-1;side<=1;side+=2) for(int i=0;i<(reef?24:9);i++)
                {
                    float z=reef?rng.Range(-58,58):-56+i*14;
                    float bend=variant==0?0:10*Mathf.Sin(z*.04f);
                    float x=side*(reef?rng.Range(35,51):45)+bend;
                    var p=new Vector3(x,0,z); p.y=WorldGenerator.Height(terrain,p,45)-.25f;
                    string name=reef?(i%3==0?"BranchCoral":i%3==1?"FanCoral":"Seaweed"):(i%2==0?"RockLarge":"CliffWallA");
                    float scale=reef?rng.Range(1,1.8f):(name=="RockLarge"?2f:.8f);
                    Place(root,name,p,rng.Range(0,360),Vector3.one*scale);
                }
                if(reef) foreach(float side in new [] {-1f,1f})
                    Place(root,"RockLarge",new Vector3(side*40,-1.4f,0),side*35,new Vector3(.65f,.8f,.65f));
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Parts+id+".prefab");
                var rules=new List<LocationPointRule> {new LocationPointRule {Tag="landmark",Count=1,AtLocationOrigin=true,AtSeaLevel=true,LocalOffset=Vector3.down*.15f,StaticPrefab=prefab,PrefabVersion=1}};
                foreach(float z in new [] {-85f,85f}) rules.Add(new LocationPointRule {Tag="sea_route",Count=1,AtLocationOrigin=true,AtSeaLevel=true,LocalOffset=new Vector3(0,-.15f,z)});
                SaveDefinition(profile,id,settings,rules);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void SaveDefinition(WorldProfile profile,string id,LocationSettings settings,List<LocationPointRule> rules)
        {
            string path="Assets/Settings/World/"+id+".asset";
            var definition=AssetDatabase.LoadAssetAtPath<LocationDefinition>(path);
            if(definition==null) { definition=ScriptableObject.CreateInstance<LocationDefinition>(); AssetDatabase.CreateAsset(definition,path); }
            definition.Settings=settings; definition.Points=rules.ToArray(); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            if(!profile.Locations.Contains(definition)) profile.Locations=profile.Locations.Concat(new [] {definition}).ToArray();
        }
        static LocationPointRule Point(string tag,Vector3 p) => new LocationPointRule {Tag=tag,Count=1,AtLocationOrigin=true,LocalOffset=p};
        static GameObject Place(GameObject parent,string name,Vector3 p,float yaw=0,Vector3? scale=null)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Parts+name+".prefab") ?? AssetDatabase.LoadAssetAtPath<GameObject>(Starter+name+".prefab");
            if(prefab==null) throw new InvalidOperationException("Missing part "+name);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.transform);
            obj.transform.localPosition=p; obj.transform.localRotation=Quaternion.Euler(0,yaw,0); obj.transform.localScale=scale??Vector3.one;
            return obj;
        }
        static void Box(GameObject target,Vector3 center,Vector3 size) {var c=target.AddComponent<BoxCollider>(); c.center=center; c.size=size;}
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            int index=path.LastIndexOf('/'); Folder(path.Substring(0,index)); AssetDatabase.CreateFolder(path.Substring(0,index),path.Substring(index+1));
        }
    }
}
