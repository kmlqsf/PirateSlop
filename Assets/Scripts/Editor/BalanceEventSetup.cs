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
                AddStoneBowl(root);
                var deposit=weightSocket.parent.gameObject.AddComponent<BalanceEventTarget>();deposit.Owner=balance;
                balance.DepositTarget=deposit;
                balance.Stones=BuildStones(root.transform,balance,terrain,ground);
                AddEventPresentation(root, ground);
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

        public static void AddStoneBowl(GameObject root)
        {
            LoadParts();
            var balance = root.GetComponent<NetworkBalanceEvent>();
            var pan = balance.WeightSocket.parent;
            if (pan.Find("IronStoneBowl") != null) return;
            foreach (var child in pan.Cast<Transform>().ToArray())
                if (child.name == "OakTrayPlank" || child.name.Contains("TraySideRail") || child.name.Contains("TrayEndRail")) Object.DestroyImmediate(child.gameObject);
            var baseCollider = pan.GetComponent<BoxCollider>();
            if (baseCollider != null) Object.DestroyImmediate(baseCollider);
            const int sides = 48;
            var profile = new[] { new Vector2(0,.015f), new Vector2(.60f,.015f), new Vector2(.72f,.08f), new Vector2(.88f,.25f), new Vector2(1,.48f), new Vector2(1.06f,.48f), new Vector2(.97f,.18f), new Vector2(.72f,-.07f), new Vector2(0,-.07f) };
            var vertices = new Vector3[profile.Length * (sides + 1)];
            var uv = new Vector2[vertices.Length]; var triangles = new List<int>();
            for (int ring = 0; ring < profile.Length; ring++)
                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides; int index = ring * (sides + 1) + i;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * profile[ring].x * .95f, profile[ring].y, Mathf.Sin(angle) * profile[ring].x * .76f);
                    uv[index] = new Vector2(i / (float)sides, ring / (float)(profile.Length - 1));
                    if (ring == 0 || i == sides) continue;
                    int previous = index - sides - 1;
                    triangles.AddRange(new[] { previous, index + 1, index, previous, previous + 1, index + 1 });
                }
            var mesh = new Mesh { name = "BalanceStoneBowl" }; mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            string path = Folder + "BalanceStoneBowl.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh,path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            var bowl = Child(pan,"IronStoneBowl",Vector3.zero);
            bowl.gameObject.AddComponent<MeshFilter>().sharedMesh = saved;
            bowl.gameObject.AddComponent<MeshRenderer>().sharedMaterials = parts["Band"].Materials;
            bowl.gameObject.AddComponent<MeshCollider>().sharedMesh = saved;
            foreach (var part in balance.WeightHanger.Cast<Transform>().ToArray())
                if (part.name == "HempSuspensionRope" || part.name == "TrayRopeShackle") Object.DestroyImmediate(part.gameObject);
            foreach (float x in new[] {-1f,1f}) foreach (float z in new[] {-1f,1f})
            {
                var corner = new Vector3(x * .67f, pan.localPosition.y + .48f, z * .54f);
                Segment(balance.WeightHanger,"HempSuspensionRope",new Vector3(0,-.08f,0),corner,.026f,.026f,"Rope");
                Model(balance.WeightHanger,"TrayRopeShackle","Shackle",corner,new Vector3(.08f,.13f,.06f),Quaternion.Euler(0,x*20,0));
            }
        }

        public static void AddEventPresentation(GameObject root, float ground)
        {
            LoadParts();
            var balance = root.GetComponent<NetworkBalanceEvent>();
            balance.ShoreHeight = ground;
            if (root.transform.Find("PitEscapeLadder") == null)
            {
                var ladderRoot = Child(root.transform, "PitEscapeLadder", new Vector3(-.27f, ground - 2.96f, 1.05f));
                ladderRoot.localRotation = Quaternion.Euler(0, 180, 0);
                var ladder = ladderRoot.gameObject.AddComponent<ShipLadder>();
                ladder.Height = 2.96f; ladder.HalfWidth = .5f; ladder.BoardingAccess = true;
                ladder.ExitClearance = .2f; ladder.ExitDepth = .95f; ladder.Speed = 2.8f;
                foreach (float x in new[] { -.43f, .43f })
                    Segment(ladderRoot, "WeatheredLadderRail", new Vector3(x, 0, 0), new Vector3(x, 3.15f, 0), .07f, .09f, "Timber");
                for (int i = 0; i < 10; i++) Timber(ladderRoot, "LadderRung", new Vector3(0, .16f + i * .3f, .02f), new Vector3(.88f, .06f, .12f));
            }
            var button = balance.WeightSocket.GetComponent<BoxCollider>();
            if (button != null) Object.DestroyImmediate(button);
            if (balance.BuriedSand == null)
            {
                const int sides = 36, rings = 6;
                var vertices = new List<Vector3> { Vector3.up * 1.15f };
                var triangles = new List<int>(); var uv = new List<Vector2> { new(.5f,.5f) };
                for (int ring = 0; ring < rings; ring++)
                {
                    float radius = (ring + 1f) / rings;
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = i * Mathf.PI * 2 / sides;
                        float height = 1.15f * (1 - radius * radius) + Mathf.Sin(angle * 5 + ring) * .025f * radius;
                        var v = new Vector3(Mathf.Cos(angle) * radius * 1.4f, height, Mathf.Sin(angle) * radius * 1.05f);
                        vertices.Add(v); uv.Add(new Vector2(v.x / 3, v.z / 3));
                        int current = 1 + ring * sides + i, next = 1 + ring * sides + (i + 1) % sides;
                        if (ring == 0) { triangles.AddRange(new[] { 0, next, current }); }
                        else
                        {
                            int previous = current - sides, previousNext = next - sides;
                            triangles.AddRange(new[] { previous, next, current, previous, previousNext, next });
                        }
                    }
                }
                var mesh = new Mesh { name = "BalanceBuriedSand" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var path = Folder + "BalanceBuriedSand.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                balance.BuriedSand = Child(balance.ChestSocket, "BuriedTreasureSand", Vector3.zero);
                balance.BuriedSand.gameObject.AddComponent<MeshFilter>().sharedMesh = saved;
                balance.BuriedSand.gameObject.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SandyIslands/ExcavatedSand.mat");
            }
            if (balance.RevealDust == null)
            {
                var dust = Child(balance.ChestSocket, "TreasureSandFall", Vector3.up * .8f);
                dust.localRotation = Quaternion.Euler(-90, 0, 0);
                var particles = dust.gameObject.AddComponent<ParticleSystem>(); balance.RevealDust = particles;
                var main = particles.main; main.playOnAwake = false; main.loop = false; main.duration = 1.1f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.1f); main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f); main.gravityModifier = 1f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 160;
                var emission = particles.emission; emission.rateOverTime = 55f;
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 55; shape.radius = .8f;
                var materialPath = Folder + "BalanceSandGrains.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor", new Color(.62f,.43f,.21f));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = primitive.GetComponent<MeshFilter>().sharedMesh; renderer.sharedMaterial = material;
                Object.DestroyImmediate(primitive); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
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
