using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using PirateSlop.Ships;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PirateSlop.Editor
{
    public static class SternCargoEventSetup
    {
        const string Folder = "Assets/Models/SeaEvents/SternCargo/";
        const string ShipPath = "Assets/Resources/SeaEvents/SternCargoShip.prefab";
        const string CargoPath = "Assets/Resources/SeaEvents/TowedCargo.prefab";
        [MenuItem("PirateSlop/Art/Prepare Stern Cargo Event")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); meshes.Clear();
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var source = PrefabUtility.LoadPrefabContents("Assets/Resources/Ships/ShipV3Test.prefab");
            GameObject cargo = null, merchant = null;
            try
            {
                var rope = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/SeaEvents/Rigging/RiggingHemp.mat");
                if (rope == null) throw new InvalidOperationException("Missing rigging hemp material.");
                cargo = PrefabUtility.LoadPrefabContents(CargoPath);
                foreach (Transform child in cargo.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                cargo.name = "TowedCargoItem";
                var body = cargo.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var payload = cargo.GetComponent<NetworkTowedCargo>();
                payload.HoistSpeed = 1.6f;
                payload.ChestSocket = Child(cargo.transform,"ChestSocket",Vector3.zero);
                payload.SlingEye = Child(cargo.transform,"SlingEye",new Vector3(0,1.3f,0));
                var barrel = source.GetComponentsInChildren<Transform>(true).First(t => t.name == "V17_Dice_Game_Barrel");
                var visual = FittedVisual(barrel,cargo.transform,"BarrelPayload",new Vector3(0,.54f,0),new Vector3(.8f,1.08f,.8f),Quaternion.identity,"Barrel");
                payload.BarrelVisual = visual.gameObject;
                var collider = cargo.GetComponent<BoxCollider>(); if (collider == null) collider = cargo.AddComponent<BoxCollider>();
                collider.center = new Vector3(0,.54f,0); collider.size = new Vector3(.8f,1.08f,.8f);
                payload.BarrelSling = Child(cargo.transform,"BarrelRopeSling",Vector3.zero).gameObject;
                payload.ChestSling = Child(cargo.transform,"ChestRopeSling",Vector3.zero).gameObject;
                BuildBarrelSling(payload.BarrelSling.transform,rope,visual);
                BuildChestSling(payload.ChestSling.transform,rope);
                payload.ChestSling.SetActive(false);
                payload.TowRope = Rope(cargo.transform,"IndividualTowRope",rope,Enumerable.Repeat(Vector3.zero,43).ToArray(),.015f,true);
                var ropeTail = cargo.GetComponent<SternCargoRopeTail>(); if (ropeTail == null) ropeTail = cargo.AddComponent<SternCargoRopeTail>();
                ropeTail.Cargo = payload; ropeTail.Loaded = payload.TowRope;
                ropeTail.Tail = Rope(cargo.transform,"GatheredDeckRope",rope,new[]{Vector3.zero,Vector3.up*.02f},.015f,true);
                ropeTail.Tail.enabled = false;
                foreach (var node in cargo.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 0;
                PrefabUtility.SaveAsPrefabAsset(cargo,CargoPath);

                merchant = PrefabUtility.LoadPrefabContents(ShipPath);
                var owner = merchant.GetComponent<NetworkSternCargoEvent>();
                owner.Catalog = catalog; owner.CargoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPath).GetComponent<NetworkTowedCargo>(); owner.ReelSeconds = 20;
                foreach (string name in new[]{"SternCargoWinch","SternTowOutlet","SternCargoLanding","WinchLeadCable","SternCargoRig"})
                { var node = merchant.transform.Find(name); if (node != null) Object.DestroyImmediate(node.gameObject); }
                var rig = merchant.GetComponent<ShipV3VisualRig>();
                var sailMat = CanvasMaterial(rig.Sails[0].sharedMaterial,false); var triangularMat = CanvasMaterial(rig.Sails[0].sharedMaterial,true);
                foreach (var sail in rig.Sails) if (sail != null) sail.sharedMaterial = sail.name.Contains("Mizzen") ? triangularMat : sailMat;
                foreach (var flag in merchant.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == "V17_Main_Pirate_Flag_Cloth" || r.name == "V17_Fore_Pirate_Flag_Cloth")) flag.sharedMaterials = Enumerable.Repeat(sailMat,Mathf.Max(1,flag.sharedMaterials.Length)).ToArray();
                var frame = Child(merchant.transform,"SternCargoRig",Vector3.zero);
                var spar = source.GetComponentsInChildren<Transform>(true).First(t => t.name == "BilgeBeam_Stern_Port_Intact");
                var block = source.GetComponentsInChildren<MeshRenderer>(true).First(r => r.sharedMaterials.Any(m => m != null && m.name.Contains("RiggingBlock"))).transform;
                for (int side=-1;side<=1;side+=2)
                {
                    for (int end=0;end<2;end++)
                        FittedVisual(spar,frame,"GantryPost_"+side+"_"+end,new Vector3(side*3.4f,8.77f,end==0?-19.7f:-17.8f),new Vector3(.28f,3.6f,.28f),Quaternion.identity,"Spar");
                    var a=new Vector3(side*3.4f,7.08f,-19.7f); var b=new Vector3(side*3.4f,10.57f,-23.45f);
                    FittedVisual(spar,frame,"OutboardGantryBrace_"+side,(a+b)*.5f,new Vector3(.22f,Vector3.Distance(a,b),.22f),Quaternion.FromToRotation(Vector3.up,b-a),"Spar");
                }
                foreach (float z in new[]{-23.45f,-18.05f})
                    FittedVisual(spar,frame,"GantryCrossbeam_"+z,new Vector3(0,10.66f,z),new Vector3(.28f,6.9f,.28f),Quaternion.Euler(0,0,90),"Spar");
                owner.RopeStations = new SternRopeStation[6];
                for (int i=0;i<6;i++)
                {
                    float x=(i-2.5f)*1.02f;
                    float knotX=new[]{-2.807f,-1.969f,-1.0f,1.008f,1.974f,2.813f}[i];
                    var stationRoot = Child(frame,"SternRopeStation_"+i,Vector3.zero);
                    var slot = new SternRopeStation(); owner.RopeStations[i] = slot;
                    slot.Grip = Child(stationRoot,"RopeKnotGrip",new Vector3(knotX,7.115f,-19.99f));
                    slot.Knot = Child(stationRoot,"RailHitch",slot.Grip.localPosition);
                    var beam=source.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name==(knotX<0?"V3_Plank_Transom_27_-01":"V3_Plank_Transom_27_000"));
                    var hitchSurface=beam.gameObject.AddComponent<MeshCollider>();hitchSurface.sharedMesh=beam.sharedMesh;Physics.SyncTransforms();
                    var hitch = new Vector3[65];
                    for (int j=0;j<hitch.Length;j++)
                    {
                        float t=j/(float)(hitch.Length-1); float angle=t*Mathf.PI*4;
                        var direction=new Vector3(0,Mathf.Cos(angle),Mathf.Sin(angle));
                        var center=new Vector3(knotX+(t-.5f)*.065f,7.034f,-20.056f);
                        if(!hitchSurface.Raycast(new Ray(center+direction*.5f,-direction),out var contact,1)) throw new InvalidOperationException("Rail hitch missed authored timber.");
                        hitch[j]=slot.Knot.InverseTransformPoint(contact.point+contact.normal*.0145f);
                    }
                    Object.DestroyImmediate(hitchSurface);
                    Rope(slot.Knot,"TwoTurnRailHitch",rope,hitch,.014f,false);
                    Rope(slot.Knot,"SlippedHitchBight",rope,new[]{new Vector3(-.04f,0,.055f),new Vector3(-.1f,.03f,.13f),new Vector3(-.09f,.01f,.22f),new Vector3(.03f,.0f,.24f),new Vector3(.1f,.02f,.13f),new Vector3(.04f,0,.04f)},.014f,false);
                    FittedVisual(spar,stationRoot,"PulleyTrack",new Vector3(x,10.62f,-20.75f),new Vector3(.16f,5.7f,.18f),Quaternion.Euler(90,0,0),"Spar");
                    slot.Outside=Child(stationRoot,"OutboardLiftGuide",new Vector3(x,10.32f,-23.15f));
                    slot.Inside=Child(stationRoot,"InboardLiftGuide",new Vector3(x,10.32f,-18.3f));
                    slot.Landing=Child(stationRoot,"CargoDeckLanding",new Vector3(x,7.01f,-18.3f));
                    slot.Pulley=Child(stationRoot,"TravelingCargoBlock",slot.Outside.localPosition);
                    FittedVisual(block,slot.Pulley,"CargoRiggingBlock",Vector3.zero,new Vector3(.22f,.4f,.22f),Quaternion.identity,"Block");
                    Rope(slot.Pulley,"TrolleyLashing",rope,new[]{new Vector3(-.06f,.08f,0),new Vector3(-.06f,.37f,0),new Vector3(.06f,.37f,0),new Vector3(.06f,.08f,0)},.012f,false);
                }
                PrefabUtility.SaveAsPrefabAsset(merchant,ShipPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
                if (merchant != null) PrefabUtility.UnloadPrefabContents(merchant);
                if (cargo != null) PrefabUtility.UnloadPrefabContents(cargo);
            }
            var registry=AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            var shipPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath); var cargoPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(CargoPath);
            registry.AddObject(shipPrefab.GetComponent<NetworkObject>(),true,true); registry.AddObject(cargoPrefab.GetComponent<NetworkObject>(),true,true);EditorUtility.SetDirty(registry);
            catalog.SternCargoEventPrefab=shipPrefab.GetComponent<NetworkSternCargoEvent>();EditorUtility.SetDirty(catalog);
            var config=AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");config.ProtocolVersion=Mathf.Max(174,config.ProtocolVersion);EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets(); Debug.Log("Stern cargo saved: six rail hitches, independent hidden payloads, sling hoists, tracked cargo blocks and VOC masthead flags.");
        }
        static Transform Child(Transform parent,string name,Vector3 point)
        { var node=new GameObject(name).transform;node.SetParent(parent,false);node.localPosition=point;return node; }
        static readonly Dictionary<string,Mesh> meshes = new();
        static Transform FittedVisual(Transform source,Transform parent,string name,Vector3 center,Vector3 size,Quaternion rotation,string key)
        {
            string path=Folder+"Reused_"+key+".asset";
            if (!meshes.TryGetValue(key,out var mesh) || mesh == null)
            {
                var parts=new List<CombineInstance>();
                var frame=Matrix4x4.Translate(-source.position);
                foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null || filter.sharedMesh.vertexCount==0) continue;
                    bool active=true;
                    for(var node=filter.transform;node!=null && node!=source.parent;node=node.parent) if(!node.gameObject.activeSelf) active=false;
                    if(!active) continue;
                    for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++) parts.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sub,transform=frame*filter.transform.localToWorldMatrix});
                }
                var baked=new Mesh{name="SternCargoReused"+key,indexFormat=IndexFormat.UInt32};baked.CombineMeshes(parts.ToArray(),true,true);
                var vertices=baked.vertices;var offset=baked.bounds.center;for(int i=0;i<vertices.Length;i++)vertices[i]-=offset;baked.vertices=vertices;baked.RecalculateBounds();
                mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=baked;AssetDatabase.CreateAsset(mesh,path);}
                else
                {
                    mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=baked.vertices;mesh.normals=baked.normals;mesh.tangents=baked.tangents;mesh.uv=baked.uv;mesh.triangles=baked.triangles;mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);Object.DestroyImmediate(baked);
                }
                meshes[key]=mesh;
            }
            var output=Child(parent,name,center);output.localRotation=rotation;
            var bounds=mesh.bounds.size;output.localScale=new Vector3(size.x/Mathf.Max(.001f,bounds.x),size.y/Mathf.Max(.001f,bounds.y),size.z/Mathf.Max(.001f,bounds.z));
            output.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            output.gameObject.AddComponent<MeshRenderer>().sharedMaterial=source.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.sharedMaterial!=null).sharedMaterial;
            return output;
        }
        static LineRenderer Rope(Transform parent,string name,Material material,Vector3[] points,float radius,bool world)
        {
            var node=Child(parent,name,Vector3.zero);var line=node.gameObject.AddComponent<LineRenderer>();line.sharedMaterial=material;
            line.useWorldSpace=world;line.widthMultiplier=radius*2;line.positionCount=points.Length;line.SetPositions(points);line.textureMode=LineTextureMode.Tile;
            var visual=node.gameObject.AddComponent<RiggingRopeVisual>();visual.TilesPerMeter=4;return line;
        }
        static void BuildBarrelSling(Transform parent,Material rope,Transform visual)
        {
            var surface=visual.gameObject.AddComponent<MeshCollider>();surface.sharedMesh=visual.GetComponent<MeshFilter>().sharedMesh;
            Physics.SyncTransforms();
            for(int band=0;band<2;band++)
            {
                var ring=new Vector3[49];
                for(int i=0;i<ring.Length;i++){float a=i/(float)(ring.Length-1)*Mathf.PI*2;var dir=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var start=parent.TransformPoint(dir*2+Vector3.up*(band==0?.22f:.82f));
                    if(!surface.Raycast(new Ray(start,-parent.TransformDirection(dir)),out var contact,3)) throw new InvalidOperationException("Barrel sling missed authored surface.");
                    ring[i]=parent.InverseTransformPoint(contact.point+contact.normal*.015f);}
                Rope(parent,"BarrelGirth_"+band,rope,ring,.014f,false);
            }
            Vector3 Contact(Vector3 direction,float y)
            {
                var start=parent.TransformPoint(direction*2+Vector3.up*y);
                if(!surface.Raycast(new Ray(start,-parent.TransformDirection(direction)),out var hit,3)) throw new InvalidOperationException("Barrel basket missed authored surface.");
                return parent.InverseTransformPoint(hit.point+hit.normal*.016f);
            }
            for(int axis=0;axis<2;axis++)
            {
                var direction=axis==0?Vector3.right:Vector3.forward;
                var points=new[]{new Vector3(0,1.3f,0),Contact(direction,1.02f)+Vector3.up*.08f,Contact(direction,.85f),Contact(direction,.4f),Contact(direction,.04f),direction*.25f+Vector3.down*.018f,
                    -direction*.25f+Vector3.down*.018f,Contact(-direction,.04f),Contact(-direction,.4f),Contact(-direction,.85f),Contact(-direction,1.02f)+Vector3.up*.08f,new Vector3(0,1.3f,0)};
                Rope(parent,"BarrelBasketStrop_"+axis,rope,points,.016f,false);
            }
            Object.DestroyImmediate(surface);
        }
        static void BuildChestSling(Transform parent,Material rope)
        {
            for(int side=-1;side<=1;side+=2)
                Rope(parent,"ChestBasketStrop_"+side,rope,new[]{new Vector3(0,1.3f,0),new Vector3(side*.37f,.535f,-.325f),new Vector3(side*.37f,.035f,-.325f),new Vector3(side*.3f,-.018f,-.325f),new Vector3(side*.3f,-.018f,.325f),new Vector3(side*.37f,.035f,.325f),new Vector3(side*.37f,.535f,.325f),new Vector3(0,1.3f,0)},.016f,false);
        }
        static Material CanvasMaterial(Material source, bool triangle)
        {
            string suffix = triangle ? "Mizzen" : "Square";
            string texturePath = Folder + "VOC_" + suffix + "_Canvas.png";
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var uv = new Vector2(x / (float)(size - 1), y / (float)(size - 1));
                var p = triangle ? (uv - new Vector2(.31f,.32f)) / .63f + new Vector2(.5f,.5f) : uv;
                float mark = Mathf.Min(Segment(p,new Vector2(.28f,.77f),new Vector2(.5f,.2f)),Segment(p,new Vector2(.5f,.2f),new Vector2(.72f,.77f)));
                mark = Mathf.Min(mark,Segment(p,new Vector2(.22f,.77f),new Vector2(.35f,.77f)));
                mark = Mathf.Min(mark,Segment(p,new Vector2(.65f,.77f),new Vector2(.78f,.77f)));
                var o = new Vector2((p.x-.365f)/.09f,(p.y-.49f)/.14f);
                mark = Mathf.Min(mark,Mathf.Abs(o.magnitude-1)*.09f);
                var c = new Vector2((p.x-.645f)/.095f,(p.y-.49f)/.14f);
                if (c.x < .65f || Mathf.Abs(c.y) > .65f) mark = Mathf.Min(mark,Mathf.Abs(c.magnitude-1)*.095f);
                float ink = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.011f,.016f,mark));
                float weave = .985f + .015f*Mathf.Sin(x*2.1f)*Mathf.Sin(y*2.3f);
                float wear = .93f + .07f*Mathf.PerlinNoise(x*.045f,y*.045f);
                pixels[y*size+x] = Color.Lerp(new Color(.86f,.81f,.68f)*weave,new Color(.39f,.055f,.036f),ink*wear);
            }
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(texturePath,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath); importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 1024; importer.SaveAndReimport();
            string materialPath = Folder + "VOC_" + suffix + "_Canvas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material,materialPath); }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)); material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Cull",0); material.SetFloat("_Smoothness",.12f); EditorUtility.SetDirty(material); return material;
        }
        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = b-a; return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));
        }
    }
}
