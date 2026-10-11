using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PirateSlop.Editor
{
    public static class BalanceEventSetup
    {
        const string Folder = "Assets/Models/World/SandyIslands/Balance/";
        const string PrefabPath = "Assets/Prefabs/Networking/NetworkBalanceEvent.prefab";
        struct Part { public Mesh Mesh; public Material[] Materials; }
        static readonly Dictionary<string,Part> parts = new();
        [MenuItem("PirateSlop/Art/Prepare Island Balance Event")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            LoadParts();
            var terrain = AssetDatabase.LoadAssetAtPath<GameObject>(SandyIslandTestSetup.Prefabs+"/BalanceSandyIsland.prefab").GetComponentInChildren<Terrain>();
            float ground=terrain.transform.localPosition.y+terrain.terrainData.GetInterpolatedHeight(.5f,.5f);
            var root=new GameObject("IslandBalanceEvent");
            try
            {
                var network=root.AddComponent<NetworkObject>();
                network.SetAssetPathHash(PrefabPath.GetStableHashU64());
                var body=root.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                var balance=root.AddComponent<NetworkBalanceEvent>();
                var assembly=Child(root.transform,"ShipTimberBalanceMechanism",new Vector3(5.7f*Mathf.Cos(balance.InitialAngle*Mathf.Deg2Rad),ground,0));
                for(int side=-1;side<=1;side+=2)
                {
                    var z=side*.68f;
                    Timber(assembly,"BuriedTimberFoot",new Vector3(0,.12f,z),new Vector3(1.75f,.2f,.25f));
                    Segment(assembly,"BracedOakUpright",new Vector3(-.75f,.18f,z),new Vector3(0,2.22f,z),.23f,.23f,"Timber");
                    Segment(assembly,"BracedOakUpright",new Vector3(.75f,.18f,z),new Vector3(0,2.22f,z),.23f,.23f,"Timber");
                    Model(assembly,"DeckBearing", "Bearing",new Vector3(0,2.24f,z),new Vector3(.55f,.09f,.55f),Quaternion.Euler(90,0,0));
                    foreach(float x in new[]{-.65f,.65f}) Model(assembly,"ShipFootBolt","Bolt",new Vector3(x,.245f,z),Vector3.one*.09f,Quaternion.identity);
                }
                Segment(assembly,"ForgedAxle",new Vector3(0,2.24f,-.92f),new Vector3(0,2.24f,.92f),.16f,.16f,"Axle");
                var beam=Child(assembly,"TiltingOakBeam",new Vector3(0,2.24f,0));
                Timber(beam,"ShipCapstanOakBeam",new Vector3(-1.475f,0,0),new Vector3(8.75f,.27f,.3f));
                foreach(float x in new[]{-5.7f,0,2.75f})
                {
                    Model(beam,"IronBeamBand","Band",new Vector3(x,0,0),new Vector3(.38f,.34f,.36f),Quaternion.Euler(0,0,90));
                    Model(beam,"BeamPin","Bolt",new Vector3(x,0,-.18f),new Vector3(.13f,.09f,.13f),Quaternion.Euler(90,0,0));
                }
                balance.Beam=beam;
                balance.RewardHanger=BuildPan(beam,"ChestPan",-5.7f,new Vector2(1.58f,1.02f),true,out var chestSocket);
                balance.WeightHanger=BuildPan(beam,"StonePan",2.75f,new Vector2(1.7f,1.36f),false,out var weightSocket);
                balance.ChestSocket=chestSocket;balance.WeightSocket=weightSocket;
                var deposit=weightSocket.parent.gameObject.AddComponent<BalanceEventTarget>();deposit.Owner=balance;
                var depositCollider=weightSocket.gameObject.AddComponent<BoxCollider>();depositCollider.size=new Vector3(1.58f,.25f,1.24f);depositCollider.center=Vector3.up*.06f;
                balance.DepositTarget=deposit;
                balance.Stones=BuildStones(root.transform,balance,terrain,ground);
                balance.ApplyPose(balance.InitialAngle);
                foreach(var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=LayerMask.NameToLayer("WorldStatic");
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                var registry=AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
                registry.AddObject(prefab.GetComponent<NetworkObject>(),true,true);EditorUtility.SetDirty(registry);
                var catalog=AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
                catalog.BalanceEventPrefab=prefab.GetComponent<NetworkBalanceEvent>();EditorUtility.SetDirty(catalog);
                AddPlayerComponent();AssetDatabase.SaveAssets();
                Debug.Log("Saved island balance event: five stones, ship timber mechanism, network chest and player interaction.");
            }
            finally {Object.DestroyImmediate(root);}
        }

        static void AddPlayerComponent()
        {
            string path="Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player=PrefabUtility.LoadPrefabContents(path);
            try {if(player.GetComponent<BalanceStonePlayer>()==null) player.AddComponent<BalanceStonePlayer>();PrefabUtility.SaveAsPrefabAsset(player,path);}
            finally {PrefabUtility.UnloadPrefabContents(player);}
        }

        static BalanceEventTarget[] BuildStones(Transform root,NetworkBalanceEvent balance,Terrain terrain,float ground)
        {
            var random=new System.Random(17821);
            var points=new List<Vector2>();var result=new BalanceEventTarget[5];
            var island=AssetDatabase.LoadAssetAtPath<GameObject>(SandyIslandTestSetup.Prefabs+"/BalanceSandyIsland.prefab");
            var decor=island.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==island.transform && t.name!="EditableSandyGround" && !t.name.Contains("Sand") && !t.name.Contains("Pit")).ToArray();
            for(int i=0;i<5;i++)
            {
                Vector2 p=default;
                for(int attempt=0;attempt<200;attempt++)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2,radius=9+(float)random.NextDouble()*10;
                    p=new Vector2(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                    if(points.Any(q=>Vector2.Distance(q,p)<5) || decor.Any(q=>Vector2.Distance(new Vector2(q.localPosition.x,q.localPosition.z),p)<2.4f)) continue;
                    break;
                }
                points.Add(p);
                var stoneRoot=Child(root,"CarryStone_"+(i+1),Vector3.zero);
                var target=stoneRoot.gameObject.AddComponent<BalanceEventTarget>();target.Owner=balance;target.StoneIndex=i;result[i]=target;
                string source=i%2==0 ? "RockMedium" : "RockLarge";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/StarterIsland/"+source+".prefab");
                var sourceRoot=prefab.transform.Find("CoastalVisual/LOD0");
                var meshes=sourceRoot.GetComponentsInChildren<MeshFilter>();
                var bounds=new Bounds();bool first=true;
                foreach(var filter in meshes)
                {
                    var r=filter.GetComponent<MeshRenderer>();
                    if(r==null || r.sharedMaterial.shader.name.Contains("Foliage")) continue;
                    if(first) {bounds=r.bounds;first=false;} else bounds.Encapsulate(r.bounds);
                }
                float diameter=.44f+(float)random.NextDouble()*.05f;
                float scale=diameter/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                var visual=Child(stoneRoot,"NativeRockMesh",Vector3.zero);
                foreach(var filter in meshes)
                {
                    var r=filter.GetComponent<MeshRenderer>();if(r==null || r.sharedMaterial.shader.name.Contains("Foliage")) continue;
                    var part=Child(visual,filter.name,Vector3.zero);
                    part.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;part.gameObject.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;
                    part.localPosition=prefab.transform.InverseTransformPoint(filter.transform.position)-bounds.center;
                    part.localRotation=Quaternion.Inverse(prefab.transform.rotation)*filter.transform.rotation;
                    part.localScale=filter.transform.lossyScale;
                }
                visual.localScale=Vector3.one*scale;
                float h=terrain.transform.localPosition.y+terrain.terrainData.GetInterpolatedHeight((p.x+64)/128,(p.y+64)/128);
                stoneRoot.localPosition=new Vector3(p.x,h+bounds.extents.y*scale+.018f,p.y);
                stoneRoot.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                var collider=stoneRoot.gameObject.AddComponent<BoxCollider>();collider.size=bounds.size*scale;
            }
            return result;
        }

        static Transform BuildPan(Transform beam,string name,float x,Vector2 size,bool reward,out Transform socket)
        {
            var hanger=Child(beam,name+"LevelHanger",new Vector3(x,0,0));
            float drop=reward ? 2.14f : 1.54f;
            var pan=Child(hanger,name,new Vector3(0,-drop,0));
            for(int i=0;i<5;i++) Timber(pan,"OakTrayPlank",new Vector3(0,-.05f,(i-2)*size.y/5),new Vector3(size.x,.1f,size.y/5*.98f));
            foreach(float side in new[]{-1f,1f})
            {
                Timber(pan,"TraySideRail",new Vector3(0,.05f,side*size.y*.5f),new Vector3(size.x+.14f,.18f,.11f));
                Timber(pan,"TrayEndRail",new Vector3(side*size.x*.5f,.05f,0),new Vector3(.11f,.18f,size.y));
            }
            foreach(float sx in new[]{-1f,1f}) foreach(float sz in new[]{-1f,1f})
            {
                var corner=new Vector3(sx*size.x*.43f,-drop+.08f,sz*size.y*.43f);
                Segment(hanger,"HempSuspensionRope",new Vector3(0,-.08f,0),corner,.026f,.026f,"Rope");
                Model(hanger,"TrayRopeShackle","Shackle",corner,new Vector3(.08f,.13f,.06f),Quaternion.Euler(0,sx*20,0));
            }
            socket=Child(pan,reward ? "ActualLootChestSocket" : "FiveStoneSocket",Vector3.up*.01f);
            var baseCollider=pan.gameObject.AddComponent<BoxCollider>();baseCollider.center=new Vector3(0,-.05f,0);baseCollider.size=new Vector3(size.x,.1f,size.y);
            return hanger;
        }

        static Transform Child(Transform parent,string name,Vector3 point)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=point;return t;}
        static Transform Model(Transform parent,string name,string key,Vector3 point,Vector3 size,Quaternion rotation)
        {
            var part=Child(parent,name,point);part.localRotation=rotation;part.localScale=size;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh=parts[key].Mesh;part.gameObject.AddComponent<MeshRenderer>().sharedMaterials=parts[key].Materials;return part;
        }
        static void Timber(Transform parent,string name,Vector3 point,Vector3 size)
        {var t=Model(parent,name,"Timber",point,new Vector3(size.y,size.x,size.z),Quaternion.Euler(0,0,90));t.gameObject.AddComponent<BoxCollider>().size=Vector3.one;}
        static void Segment(Transform parent,string name,Vector3 from,Vector3 to,float width,float depth,string key)
        {var t=Model(parent,name,key,(from+to)*.5f,new Vector3(width,Vector3.Distance(from,to),depth),Quaternion.FromToRotation(Vector3.up,to-from));if(key=="Timber")t.gameObject.AddComponent<BoxCollider>().size=Vector3.one;}

        static void LoadParts()
        {
            parts.Clear();var ship=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
            var filters=ship.GetComponentsInChildren<MeshFilter>(true);
            Load("Timber",filters.First(f=>f.name=="V9_Capstan_Beam_-1"),true);
            Load("Bearing",filters.First(f=>f.name=="V9_Capstan_Deck_Bearing"),false);
            Load("Band",filters.First(f=>f.name=="V9_Capstan_Beam_EndBand_-1"),false);
            Load("Axle",filters.First(f=>f.name=="V10_Port_Reel_Fixed_Axle"),true);
            Load("Bolt",filters.First(f=>f.name=="V3_Fore_Port_Chainplate_Bolts_00"),true);
            Load("Rope",filters.First(f=>f.name=="V8_Bell_PullRope"),true);
            Load("Shackle",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SeaEvents/RopeShackle.prefab").GetComponentInChildren<MeshFilter>(),true);
        }
        static void Load(string key,MeshFilter source,bool longestUp)
        {
            var mesh=Object.Instantiate(source.sharedMesh);var b=mesh.bounds;
            Vector3 axis=longestUp ? b.size.x>=b.size.y && b.size.x>=b.size.z ? Vector3.right : b.size.y>=b.size.z ? Vector3.up : Vector3.forward
                : b.size.x<=b.size.y && b.size.x<=b.size.z ? Vector3.right : b.size.y<=b.size.z ? Vector3.up : Vector3.forward;
            var rotation=Quaternion.FromToRotation(axis,Vector3.up);var v=mesh.vertices;
            for(int i=0;i<v.Length;i++) v[i]=rotation*(v[i]-b.center);
            mesh.vertices=v;mesh.RecalculateBounds();var size=mesh.bounds.size;
            for(int i=0;i<v.Length;i++) v[i]=new Vector3(v[i].x/size.x,v[i].y/size.y,v[i].z/size.z);
            mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string path=Folder+key+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null) {mesh.name=key;AssetDatabase.CreateAsset(mesh,path);} else {EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(old);}
            parts[key]=new Part {Mesh=mesh,Materials=source.GetComponent<MeshRenderer>().sharedMaterials};
        }
    }
}
