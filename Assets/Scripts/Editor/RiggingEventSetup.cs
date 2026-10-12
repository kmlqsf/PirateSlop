using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static class RiggingEventSetup
    {
        const string Folder = "Assets/Models/SeaEvents/Rigging/";
        const string PrefabPath = "Assets/Resources/SeaEvents/RiggingEvent.prefab";
        sealed class GeometryGroup
        {
            public int Stage;
            public Material Material;
            public readonly List<CombineInstance> Parts = new();
        }
        static readonly Vector3 MastFoot = new(0, 5, -2.51f);
        static readonly Vector3 ForeFoot = new(0, 8, 11);
        static readonly Matrix4x4 ForeBreak = Matrix4x4.Translate(ForeFoot) * Matrix4x4.Rotate(Quaternion.Euler(18, 0, -38)) * Matrix4x4.Translate(-ForeFoot);
        static readonly Quaternion HullHeel = Quaternion.Euler(0, 0, 7);
        static readonly Vector3 HullOffset = new(0, -1.4f, 0);
        static readonly Dictionary<Material,Material> fadedMaterials = new();

        [MenuItem("PirateSlop/Art/Prepare Rigging Chest Event")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var source = PrefabUtility.LoadPrefabContents("Assets/Resources/Ships/ShipV3Test.prefab");
            var root = new GameObject("RiggingChestWreckEvent");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            fadedMaterials.Clear();
            AssetDatabase.StartAssetEditing();
            try
            {
                var rig = source.GetComponentInChildren<ShipV3VisualRig>(true);
                if (rig != null)
                {
                    foreach (var motion in rig.Motions) motion.Apply(1);
                    foreach (var sail in rig.Sails) if (sail != null) for (int i = 0; i < sail.sharedMesh.blendShapeCount; i++) sail.SetBlendShapeWeight(i, 0);
                }
                var network = root.AddComponent<NetworkObject>(); network.SetAssetPathHash(PrefabPath.GetStableHashU64());
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeAll;
                var owner = root.AddComponent<NetworkRiggingEvent>(); owner.Catalog = catalog;
                var wreck = Child(root.transform, "BrokenShipV3", HullOffset); wreck.localRotation = HullHeel;
                var stages = new Transform[6];
                stages[0] = Child(wreck, "RemainingHull", Vector3.zero);
                stages[1] = Child(wreck, "RottenForeMast", ForeFoot);
                stages[2] = Child(wreck, "RottenSternRails", new Vector3(0, 5, -18));
                stages[3] = Child(wreck, "RottenPortShrouds", new Vector3(-3, 15, -3));
                stages[4] = Child(wreck, "SplinteringYardEdge", new Vector3(6, 26.6f, -2.51f));
                stages[5] = Child(wreck, "LastMainMast", MastFoot);
                var breaks = new[]
                {
                    new RiggingBreakPart { Pivot = stages[1], AtSeconds = 25, Fall = new Vector3(-4,-28,3), Rotation = new Vector3(25,10,-40) },
                    new RiggingBreakPart { Pivot = stages[2], AtSeconds = 50, Fall = new Vector3(2,-12,-3), Rotation = new Vector3(-35,12,55) },
                    new RiggingBreakPart { Pivot = stages[3], AtSeconds = 75, Fall = new Vector3(-2,-35,0), Rotation = new Vector3(0,-25,-20) },
                    new RiggingBreakPart { Pivot = stages[4], AtSeconds = 95, Fall = new Vector3(4,-24,1), Rotation = new Vector3(25,15,-90) },
                    new RiggingBreakPart { Pivot = stages[5], AtSeconds = 110, Fall = new Vector3(0,-32,0), Rotation = new Vector3(24,0,-74) }
                };
                owner.BreakParts = breaks;
                BuildShipGeometry(source, stages);
                var board = FindPart(source, f => f.GetComponent<MeshRenderer>() != null && f.GetComponent<MeshRenderer>().sharedMaterials.Any(m => m != null && m.name.Contains("Deck_Worn")) && WorldBounds(f).size.x < 5 && WorldBounds(f).size.z < 5);
                var yard = FindPart(source, f => f.name == "V3_Transfer_S070_Yards_P1311_Intact");
                var ropeMaterial = source.GetComponentsInChildren<ShipV3RenderBatch>(true).First(b => b.SharedMaterial != null && b.SharedMaterial.name.Contains("Hemp_Rope")).SharedMaterial;
                ropeMaterial = RiggingRopeMaterial(ropeMaterial);
                var block = FindPart(source, f => f.GetComponent<MeshRenderer>() != null && f.GetComponent<MeshRenderer>().sharedMaterials.Any(m => m != null && m.name.Contains("RiggingBlock")));
                var winch = FindPart(source, f => f.name == "V9_Capstan_Rotating_Drum");
                var brakeHandle = FindPart(source, f => f.name == "V9_Capstan_Beam_1");
                FitPart(yard, stages[5], "MainWalkYard", new Vector3(4.5f, 26.5f, -2.51f), new Vector3(9.8f,.35f,.4f), true);
                for (int i = 0; i < 8; i++)
                    FitPart(board, stages[5], "YardFootboard_" + i, new Vector3(1.85f + i * .78f,26.72f,-2.51f), new Vector3(.8f,.16f,.9f), true);
                FitPart(yard, stages[5], "EntryBypassYard", new Vector3(2.5f,26.5f,-3.8f), new Vector3(4.5f,.35f,.35f), true);
                for(int i=0;i<5;i++) FitPart(board,stages[5],"EntryBypassBoard_"+i,new Vector3(.9f+i*.78f,26.72f,-3.8f),new Vector3(.8f,.16f,.75f),true);
                var cross=FitPart(yard,stages[5],"EntryCrossYard",new Vector3(4.15f,26.5f,-3.15f),new Vector3(1.6f,.35f,.35f),true);cross.localRotation=Quaternion.Euler(0,90,0);
                FitPart(board,stages[5],"EntryReturnBoard",new Vector3(4.15f,26.72f,-3.18f),new Vector3(.85f,.16f,1.6f),true);
                for (int i = 0; i < 4; i++)
                {
                    var wood = FitPart(board, stages[4], "RottenYardSplinter_"+i, new Vector3(5.1f+i*.65f,26.66f,-1.82f), new Vector3(.75f,.13f,.25f), false);
                    wood.localRotation = Quaternion.Euler(0,i*7-12,i*3);
                }
                var ladders = BuildLadders(source, wreck, stages, breaks);
                owner.Ladders = ladders;
                BuildBoardingNets(ladders,wreck,stages[0],board,ropeMaterial);
                var brake = Child(stages[5], "HoistBrakeAssembly", new Vector3(6.65f,27.1f,-2.51f) - MastFoot);
                FitPart(winch, brake, "ShipCapstanBrakeDrum", brake.position, new Vector3(.75f,.8f,.65f), false, true);
                owner.BrakeGrip = Child(brake, "BrakeGrip", new Vector3(.3f,.4f,.2f));
                owner.BrakeRotor = FitPart(winch, brake, "BrakeWheel", brake.position + Vector3.up * .35f, new Vector3(.62f,.45f,.62f), false, true);
                FitPart(brakeHandle, brake, "BrakeWoodHandle", owner.BrakeGrip.position, new Vector3(.8f,.15f,.18f), false, true);
                owner.Pulley = Child(stages[5], "HoistPulley", new Vector3(10.2f,28.55f,-4.01f)-MastFoot);
                FitPart(block, owner.Pulley, "ShipRiggingPulley", owner.Pulley.position, new Vector3(.62f,.75f,.38f), false, true);
                Rope(owner.Pulley,"PulleyLashing",ropeMaterial,new[]{new Vector3(-.18f,.45f,0),new Vector3(-.18f,-.35f,0),new Vector3(.18f,-.35f,0),new Vector3(.18f,.45f,0)},.05f,false);
                var boomFoot=new Vector3(6.65f,26.9f,-2.51f);var boomEnd=new Vector3(10.2f,28.55f,-4.01f);
                var brace = FitPart(yard, stages[5], "PulleyGallows", (boomFoot+boomEnd)*.5f, new Vector3((boomEnd-boomFoot).magnitude,.2f,.25f), false);
                brace.localRotation = Quaternion.FromToRotation(Vector3.right,boomEnd-boomFoot);
                Rope(stages[5],"HoistStay",ropeMaterial,new[]{stages[5].InverseTransformPoint(wreck.TransformPoint(new Vector3(0,27,-2.51f))),stages[5].InverseTransformPoint(owner.Pulley.position)},.045f,false);
                for (int i=2;i<5;i++)
                {
                    float x=2.2f+i*1.1f;
                    Rope(stages[5],"YardBoardLashing_"+i,ropeMaterial,new[]{new Vector3(x,26.82f,-2.99f)-MastFoot,new Vector3(x,26.30f,-2.99f)-MastFoot,new Vector3(x,26.30f,-2.03f)-MastFoot,new Vector3(x,26.82f,-2.03f)-MastFoot},.028f,false);
                }
                for(int i=0;i<3;i++)
                {
                    float x=1.2f+i*1.1f;
                    Rope(stages[5],"BypassBoardLashing_"+i,ropeMaterial,new[]{new Vector3(x,26.82f,-4.2f)-MastFoot,new Vector3(x,26.30f,-4.2f)-MastFoot,new Vector3(x,26.30f,-3.4f)-MastFoot,new Vector3(x,26.82f,-3.4f)-MastFoot},.026f,false);
                }
                owner.ChestHigh = owner.Pulley.position-Vector3.up*6.5f;
                owner.ChestLow = new Vector3(owner.ChestHigh.x,3.75f,owner.ChestHigh.z);
                owner.ChestSocket = Child(root.transform,"ChestSocket",owner.ChestHigh);
                owner.LandingPoint = Child(root.transform,"ChestLandingPoint",owner.ChestLow);
                for(int i=0;i<5;i++) FitPart(board,root.transform,"ChestLandingBoard_"+i,owner.ChestLow+new Vector3(0,-.09f,(i-2)*.52f),new Vector3(2f,.18f,.5f),true);
                for(int i=0;i<2;i++)
                {
                    var support=FitPart(yard,root.transform,"LandingShelfBrace_"+i,owner.ChestLow+new Vector3(-.4f,-1,(i==0?-1:1)),new Vector3(2.8f,.18f,.22f),false);
                    support.localRotation=Quaternion.Euler(0,0,42);
                    Rope(root.transform,"LandingShelfLashing_"+i,ropeMaterial,new[]{owner.ChestLow+new Vector3(-1,0,i==0?-1:1),owner.ChestLow+new Vector3(.9f,-.5f,i==0?-1:1)},.045f,false);
                }
                owner.HangingCradle = Child(root.transform,"ChestRopeCradle",owner.ChestHigh);
                for(int side=-1;side<=1;side+=2)
                    Rope(owner.HangingCradle,"ChestSling_"+side,ropeMaterial,new[]{new Vector3(0,1.05f,0),new Vector3(side*.38f,.43f,-.35f),new Vector3(side*.38f,.02f,-.35f),new Vector3(side*.38f,-.042f,-.27f),new Vector3(side*.38f,-.042f,.27f),new Vector3(side*.38f,.02f,.35f),new Vector3(side*.38f,.43f,.35f),new Vector3(0,1.05f,0)},.032f,false);
                var shackle=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SeaEvents/RopeShackle.prefab");
                if(shackle!=null)
                {
                    var metal=Object.Instantiate(shackle,owner.HangingCradle,false); metal.name="ChestHoistShackle";metal.transform.localPosition=new Vector3(0,.99f,0);metal.transform.localScale=Vector3.one*.7f;
                    foreach(var c in metal.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                }
                owner.HoistRope = Rope(root.transform,"ChestLoweringRope",ropeMaterial,new[]{owner.BrakeGrip.position,owner.Pulley.position,owner.ChestHigh+Vector3.up*1.05f},.04f,true);
                var loosePoints=new Vector3[25];var looseA=stages[5].InverseTransformPoint(wreck.TransformPoint(new Vector3(-.18f,17,-2.51f)));var looseB=stages[5].InverseTransformPoint(wreck.TransformPoint(new Vector3(-5,13,2)));var looseC=stages[5].InverseTransformPoint(wreck.TransformPoint(new Vector3(-7,4,5)));
                for(int i=0;i<loosePoints.Length;i++){float t=i/(float)(loosePoints.Length-1);loosePoints[i]=(1-t)*(1-t)*looseA+2*(1-t)*t*looseB+t*t*looseC;}
                Rope(stages[5],"LooseSnappedStay",ropeMaterial,loosePoints,.035f,false).gameObject.AddComponent<RiggingLooseRope>();
                foreach(var part in breaks)
                {
                    var disabled=part.Pivot.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.gameObject).ToList();
                    disabled.AddRange(part.Disable);part.Disable=disabled.Distinct().ToArray();
                }
                var chestPreview=Object.Instantiate(catalog.ChestPrefab.gameObject,root.transform,false);chestPreview.name="SuspendedChestEditorPreview";chestPreview.transform.localPosition=owner.ChestHigh;
                Object.DestroyImmediate(chestPreview.GetComponent<NetworkLootChest>());Object.DestroyImmediate(chestPreview.GetComponent<NetworkObject>());
                foreach(var behaviour in chestPreview.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach(var c in chestPreview.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach(var rb in chestPreview.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
                chestPreview.AddComponent<RiggingEditorPreview>();
                foreach(var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=LayerMask.NameToLayer("WorldStatic");
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                PrefabUtility.UnloadPrefabContents(source);Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var registry=AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            registry.AddObject(prefab.GetComponent<NetworkObject>(),true,true);EditorUtility.SetDirty(registry);
            catalog.RiggingEventPrefab=prefab.GetComponent<NetworkRiggingEvent>();EditorUtility.SetDirty(catalog);
            var config=AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion=Mathf.Max(165,config.ProtocolVersion);EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("Rigging chest event saved: reused wrecked Ship V3, torn sails, climbable nets, yard brake, hoist, staged collapse and F8 destination.");
        }

        static void BuildShipGeometry(GameObject source,Transform[] stages)
        {
            var groups=new Dictionary<string,GeometryGroup>();
            var batchSources=source.GetComponentsInChildren<ShipV3RenderBatch>(true).SelectMany(b=>b.Sources).Where(r=>r!=null).Distinct().ToHashSet();
            var all=source.GetComponentsInChildren<MeshRenderer>(true).Concat(batchSources).Distinct();
            foreach(var renderer in all)
            {
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter==null || filter.sharedMesh==null || filter.sharedMesh.vertexCount==0 || !ActiveRelative(renderer.transform,source.transform)) continue;
                if(!renderer.enabled&&!batchSources.Contains(renderer))continue;
                if(ExcludedBranch(renderer.transform,source.transform))continue;
                if(renderer.GetComponent<ShipV3RenderBatch>()!=null || renderer.name.Contains("Shadow") || renderer.shadowCastingMode==ShadowCastingMode.ShadowsOnly) continue;
                string name=renderer.name;
                if(name.Contains("Loot_")||name.Contains("SlotMachine")||name.Contains("ShipMonkey")||name.Contains("Bilge")||name.Contains("Nameplate")||name.Contains("Glyph")) continue;
                if(name.StartsWith("V3_Transfer_Rig_",StringComparison.Ordinal)&&!name.Contains("Standing"))continue;
                if(name.StartsWith("V3_Transfer_Control_",StringComparison.Ordinal)&&renderer.sharedMaterials.Any(m=>m!=null&&m.name.Contains("Rope")))continue;
                if(name.Contains("Boarding_Net")||name.Contains("Fore_Main_Stay"))continue;
                var bounds=WorldBounds(filter);var p=bounds.center;
                if(p.y<5 && p.y>-.5f && p.z>5 && p.z<14 && p.x<-3.8f) continue;
                if(p.y>4 && p.y<9 && p.z<-15 && p.x>2.3f) continue;
                if(name.Contains("MastTop_Rim")||name.Contains("Main_MastTop_Post")&&Mathf.Abs(p.x)>1.2f||name.Contains("Main_MastTop_GatePost")&&Mathf.Abs(p.x)>1.2f) continue;
                if(name.Contains("Main")&&name.Contains("MastTop")&&Mathf.Abs(p.x)>1.2f&&renderer.sharedMaterials.Any(m=>m!=null&&m.name.Contains("Rope")))continue;
                int stage=Stage(name,p);
                var matrix=filter.transform.localToWorldMatrix;
                if(stage==1) matrix=ForeBreak*matrix;
                Add(groups,filter.sharedMesh,renderer.sharedMaterials,matrix,p,stage);
            }
            foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!ActiveRelative(skin.transform,source.transform)&&!skin.name.Contains("Boarding_Net")||skin.sharedMesh==null||ExcludedBranch(skin.transform,source.transform)) continue;
                if(skin.name.Contains("Boarding_Net")||skin.name.Contains("FootLashings")||skin.GetComponent<RopeTubeVisual>()!=null)continue;
                var baked=new Mesh();skin.BakeMesh(baked,false);
                if(baked.vertexCount==0){Object.DestroyImmediate(baked);continue;}
                var vertices=baked.vertices;
                var matrix=skin.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++) vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                baked.vertices=vertices;baked.RecalculateBounds();baked.RecalculateNormals();
                bool flag=skin.name.Contains("Flag");bool boarding=skin.name.Contains("Boarding_Net");
                if(skin.name.Contains("Sail")||flag||boarding)
                {
                    if(boarding)
                    {
                        var original=source.GetComponentsInChildren<ShipLadder>(true).First(l=>l.name=="Climb_Boarding_"+(skin.name.Contains("Starboard")?"Starboard":"Port"));
                        for(int i=0;i<vertices.Length;i++){float h=original.transform.InverseTransformPoint(vertices[i]).y;vertices[i]+=original.transform.forward*((.75f-original.TopLean)*Mathf.Clamp01(h/original.Height));}
                        baked.vertices=vertices;baked.RecalculateBounds();
                    }
                    else TearSail(baked,flag);
                    var center=baked.bounds.center;int stage=boarding||center.z<-9?0:center.z>6?1:5;
                    if(stage==1){vertices=baked.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=ForeBreak.MultiplyPoint3x4(vertices[i]);baked.vertices=vertices;baked.RecalculateBounds();}
                    var pivot=stages[stage];vertices=baked.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]-=pivot.localPosition;baked.vertices=vertices;baked.RecalculateBounds();
                    var cloth=Child(pivot,(boarding?"Sway_":"Torn_")+skin.name,Vector3.zero);
                    cloth.gameObject.AddComponent<MeshFilter>().sharedMesh=SaveMesh(Object.Instantiate(baked),(boarding?"Sway_":"Torn_")+skin.name);
                    var output=cloth.gameObject.AddComponent<MeshRenderer>();output.sharedMaterials=boarding?skin.sharedMaterials:skin.sharedMaterials.Select(FadedSail).ToArray();
                    var motion=cloth.gameObject.AddComponent<RiggingTornCloth>();motion.Strength=boarding?.1f:flag?.5f:.4f;
                    motion.PinAxis=flag?Vector3.right:Vector3.up;if(stage==1)motion.PinAxis=ForeBreak.MultiplyVector(motion.PinAxis).normalized;
                    if(boarding)motion.MotionAxis=Vector3.right;
                }
                else
                {
                    int stage=Stage(skin.name,baked.bounds.center);
                    Add(groups,SaveMesh(Object.Instantiate(baked),"Baked_"+skin.name),skin.sharedMaterials,stage==1?ForeBreak:Matrix4x4.identity,baked.bounds.center,stage);
                }
                Object.DestroyImmediate(baked);
            }
            int index=0;
            foreach(var group in groups.Values)
            {
                var pivot=stages[group.Stage];
                var parts=group.Parts.Select(p=>new CombineInstance{mesh=p.mesh,subMeshIndex=p.subMeshIndex,transform=Matrix4x4.Translate(-pivot.localPosition)*p.transform}).ToArray();
                var mesh=new Mesh { indexFormat=IndexFormat.UInt32 };mesh.CombineMeshes(parts,true,true);mesh.RecalculateBounds();
                if(group.Stage==5&&!group.Material.name.Contains("Rope"))CutClimbGates(mesh,pivot.localPosition);
                mesh=SaveMesh(mesh,"WreckBatch_"+index++);
                var output=Child(pivot,"Wreck_"+group.Material.name+"_"+index,Vector3.zero);
                output.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                output.gameObject.AddComponent<MeshRenderer>().sharedMaterial=group.Material;
                if(group.Material.name.Contains("Rope")||group.Material.name.Contains("Glass")||mesh.bounds.size.y>12 && mesh.bounds.size.x<1) continue;
                Collision(output,mesh);
            }
        }
        static int Stage(string name,Vector3 p)
        {
            bool net=name.Contains("Ratline")||name.Contains("Shroud")||name.Contains("Standing");
            if(name.Contains("Fore")&&(net||name.Contains("Yard")||name.Contains("Seizing")))return 1;
            if(name.Contains("Main")&&name.Contains("Yard"))return 5;
            if(name.Contains("Main")&&net)return name.Contains("_Port_")?3:name.Contains("_Starboard_")?5:p.x<-1?3:5;
            if(p.y>12 && p.z>6) return 1;
            if(p.z<-15 && p.y>4 && p.y<9) return 2;
            if(p.y>8 && p.x<-2 && p.z>-7 && p.z<1 && (name.Contains("Ratline")||name.Contains("Shroud")||name.Contains("Rope"))) return 3;
            if(p.y>9 && p.z>-9 && p.z<5) return 5;
            return 0;
        }
        static void Add(Dictionary<string,GeometryGroup> groups,Mesh mesh,Material[] materials,Matrix4x4 matrix,Vector3 point,int stage)
        {
            for(int sub=0;sub<mesh.subMeshCount && materials.Length>0;sub++)
            {
                var material=materials[Mathf.Min(sub,materials.Length-1)];if(material==null || mesh.GetIndexCount(sub)==0) continue;
                string key=stage+"/"+material.GetEntityId()+"/"+Mathf.FloorToInt(point.z/10)+"/"+Mathf.FloorToInt(point.y/14);
                if(!groups.TryGetValue(key,out var group)) groups[key]=group=new GeometryGroup{Stage=stage,Material=material};
                group.Parts.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,transform=matrix});
            }
        }
        static ShipLadder[] BuildLadders(GameObject source,Transform wreck,Transform[] stages,RiggingBreakPart[] breaks)
        {
            var result=new List<ShipLadder>();
            foreach(var original in source.GetComponentsInChildren<ShipLadder>(true))
            {
                if(!original.name.Contains("Main") && !original.name.Contains("Boarding")) continue;
                var target=Child(wreck,original.name,original.transform.position);target.localRotation=original.transform.rotation;
                var ladder=target.gameObject.AddComponent<ShipLadder>();EditorUtility.CopySerialized(original,ladder);
                if(original.name.Contains("Boarding"))
                {
                    target.localPosition-=target.localRotation*Vector3.up*1.5f;
                    ladder.Height+=1.5f;ladder.ExitPoint+=Vector3.up*1.5f;ladder.TopLean=.75f;
                }
                else
                {
                    target.localPosition-=target.localRotation*Vector3.up*.9f;
                    ladder.Height+=.9f;ladder.ExitPoint+=Vector3.up*.9f+Vector3.forward*.55f;
                }
                result.Add(ladder);
                if(original.name.Contains("Main_Port")) breaks[2].Disable=new[]{target.gameObject};
                if(original.name.Contains("Main")) breaks[4].Disable=breaks[4].Disable.Append(target.gameObject).ToArray();
            }
            return result.ToArray();
        }
        static void BuildBoardingNets(ShipLadder[] ladders,Transform wreck,Transform parent,MeshFilter board,Material material)
        {
            foreach(var ladder in ladders.Where(l=>l.name.Contains("Boarding")))
            {
                var parts=new List<Mesh>();float half=ladder.HalfWidth*.9f;
                Vector3 Point(float side,float height,float depth)
                    =>wreck.InverseTransformPoint(ladder.transform.TransformPoint(new Vector3(ladder.RopeSide(height)+side,height,depth)));
                for(int rail=0;rail<4;rail++)
                {
                    float side=Mathf.Lerp(-half,half,rail/3f);var points=new List<Vector3>();
                    for(int i=0;i<=16;i++){float h=ladder.Height*i/16;points.Add(Point(side,h,ladder.RopeDepth(h)));}
                    if(rail==0||rail==3)
                    {
                        points.Add(Point(side,ladder.Height+.12f,1.12f));points.Add(Point(side,ladder.Height+.06f,1.9f));
                        for(int i=0;i<=32;i++){float angle=i*Mathf.PI/8;points.Add(Point(side+.15f*Mathf.Cos(angle),ladder.Height+.04f+.07f*i/32,2.05f+.15f*Mathf.Sin(angle)));}
                        FitPart(board,parent,"BoardingRopeCleat_"+ladder.name+"_"+rail,Point(side,ladder.Height+.04f,2.05f),new Vector3(.25f,.24f,.25f),false);
                    }
                    parts.Add(RiggingRopeVisual.CreateTube(points,.035f));
                }
                int rungs=Mathf.CeilToInt(ladder.Height/.43f);
                for(int rung=0;rung<=rungs;rung++)
                {
                    float h=ladder.Height*rung/rungs;var points=new List<Vector3>();
                    for(int i=0;i<=8;i++){float t=i/8f;points.Add(Point(Mathf.Lerp(-half,half,t),h-Mathf.Sin(t*Mathf.PI)*.025f,ladder.RopeDepth(h)));}
                    parts.Add(RiggingRopeVisual.CreateTube(points,.028f));
                }
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),true,true);
                foreach(var part in parts)Object.DestroyImmediate(part);
                string name="Sway_V3_Transfer_Boarding_Net_"+(ladder.name.Contains("Starboard")?"Starboard":"Port");
                var net=Child(parent,name,Vector3.zero);net.gameObject.AddComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,name);
                net.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
                var motion=net.gameObject.AddComponent<RiggingTornCloth>();motion.Strength=.07f;motion.MotionAxis=Vector3.right;motion.PinnedBand=.45f;
            }
        }
        static Material RiggingRopeMaterial(Material source)
        {
            string path=Folder+"RiggingHemp.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}else EditorUtility.CopySerialized(source,material);
            material.SetColor("_BaseColor",new Color(.78f,.68f,.5f,1));material.SetFloat("_Smoothness",.16f);material.SetFloat("_BumpScale",.35f);
            EditorUtility.SetDirty(material);return material;
        }
        static void TearSail(Mesh mesh,bool flag)
        {
            mesh.ClearBlendShapes();mesh.boneWeights=Array.Empty<BoneWeight>();mesh.bindposes=Array.Empty<Matrix4x4>();
            Subdivide(mesh,flag?1:2,null);
            var uv=mesh.uv;var vertices=mesh.vertices;var bounds=mesh.bounds;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var triangles=mesh.GetTriangles(sub);var kept=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var p=(uv[triangles[i]]+uv[triangles[i+1]]+uv[triangles[i+2]])/3;
                    float u=p.x,v=p.y;
                    var center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3;
                    float free=Mathf.InverseLerp(bounds.max.x,bounds.min.x,center.x);
                    bool hole=Mathf.Pow((u-.28f)/.13f,2)+Mathf.Pow((v-.46f)/.2f,2)<1 || Mathf.Pow((u-.73f)/.14f,2)+Mathf.Pow((v-.63f)/.13f,2)<1;
                    float edge=.16f+.18f*(.5f+.5f*Mathf.Sin(u*37))+.08f*Mathf.Sin(u*83);
                    if(flag ? free>.84f+.09f*Mathf.Sin(v*19) || Mathf.Pow((free-.63f)/.10f,2)+Mathf.Pow((v-.55f)/.14f,2)<1 : hole || v<edge || u>.48f && u<.56f && v<.7f) continue;
                    kept.Add(triangles[i]);kept.Add(triangles[i+1]);kept.Add(triangles[i+2]);
                }
                mesh.SetTriangles(kept,sub);
            }
            RemoveDetachedCloth(mesh,flag?Vector3.right:Vector3.up);
            mesh.ClearBlendShapes();mesh.boneWeights=Array.Empty<BoneWeight>();mesh.bindposes=Array.Empty<Matrix4x4>();mesh.RecalculateNormals();mesh.RecalculateTangents();
        }
        static void RemoveDetachedCloth(Mesh mesh,Vector3 pinAxis)
        {
            var vertices=mesh.vertices;var roots=new int[vertices.Length];var points=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++)
            {
                var point=Vector3Int.RoundToInt(vertices[i]*10000);
                if(points.TryGetValue(point,out int first))roots[i]=first;else{roots[i]=i;points[point]=i;}
            }
            int Root(int index)
            {
                while(roots[index]!=index){roots[index]=roots[roots[index]];index=roots[index];}
                return index;
            }
            var submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(sub=>mesh.GetTriangles(sub)).ToArray();
            foreach(var triangles in submeshes)
                for(int i=0;i<triangles.Length;i+=3){int root=Root(triangles[i]);roots[Root(triangles[i+1])]=root;roots[Root(triangles[i+2])]=root;}
            var tops=new Dictionary<int,float>();float top=float.MinValue;
            foreach(int index in submeshes.SelectMany(t=>t))
            {
                int root=Root(index);float height=Vector3.Dot(vertices[index],pinAxis);top=Mathf.Max(top,height);
                tops[root]=tops.TryGetValue(root,out float previous)?Mathf.Max(previous,height):height;
            }
            for(int sub=0;sub<submeshes.Length;sub++)
            {
                var kept=new List<int>();var triangles=submeshes[sub];
                for(int i=0;i<triangles.Length;i+=3)
                    if(tops[Root(triangles[i])]>=top-.15f){kept.Add(triangles[i]);kept.Add(triangles[i+1]);kept.Add(triangles[i+2]);}
                submeshes[sub]=kept.ToArray();
            }
            var used=submeshes.SelectMany(t=>t).Distinct().OrderBy(i=>i).ToArray();var remap=new int[vertices.Length];
            for(int i=0;i<used.Length;i++)remap[used[i]]=i;
            var uv=mesh.uv;var normals=mesh.normals;var tangents=mesh.tangents;var colors=mesh.colors;
            mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=used.Select(i=>vertices[i]).ToArray();
            if(uv.Length==vertices.Length)mesh.uv=used.Select(i=>uv[i]).ToArray();
            if(normals.Length==vertices.Length)mesh.normals=used.Select(i=>normals[i]).ToArray();
            if(tangents.Length==vertices.Length)mesh.tangents=used.Select(i=>tangents[i]).ToArray();
            if(colors.Length==vertices.Length)mesh.colors=used.Select(i=>colors[i]).ToArray();
            mesh.subMeshCount=submeshes.Length;
            for(int sub=0;sub<submeshes.Length;sub++)mesh.SetTriangles(submeshes[sub].Select(i=>remap[i]).ToArray(),sub);
            mesh.RecalculateBounds();
        }
        static Material FadedSail(Material original)
        {
            string path=Folder+"Torn_"+original.name+".mat";
            if(fadedMaterials.TryGetValue(original,out var saved))return saved;
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(original);AssetDatabase.CreateAsset(material,path);}else EditorUtility.CopySerialized(original,material);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",new Color(.63f,.58f,.46f,1));
            if(material.HasProperty("_Color"))material.SetColor("_Color",new Color(.63f,.58f,.46f,1));
            if(material.HasProperty("_Cull"))material.SetInt("_Cull",0);material.doubleSidedGI=true;EditorUtility.SetDirty(material);fadedMaterials[original]=material;return material;
        }
        static MeshFilter FindPart(GameObject source,Func<MeshFilter,bool> predicate)
            => source.GetComponentsInChildren<MeshFilter>(true).First(f=>f.sharedMesh!=null && f.sharedMesh.vertexCount>0 && predicate(f));
        static Bounds WorldBounds(MeshFilter filter)
        {
            var b=filter.sharedMesh.bounds;var output=new Bounds(filter.transform.TransformPoint(b.center),Vector3.zero);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)output.Encapsulate(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
            return output;
        }
        static bool ActiveRelative(Transform t,Transform root)
        {
            for(;t!=null && t!=root;t=t.parent)if(!t.gameObject.activeSelf)return false;
            return true;
        }
        static bool ExcludedBranch(Transform t,Transform root)
        {
            for(;t!=null&&t!=root;t=t.parent)
                if(t.name=="ShipMonkey"||t.name=="SlotMachine"||t.name=="ReplacementLootPoints"||t.name.StartsWith("Loot_",StringComparison.Ordinal))return true;
            return false;
        }
        static void CutClimbGates(Mesh mesh,Vector3 offset)
        {
            Subdivide(mesh,5,(a,b,c)=>
            {
                a+=offset;b+=offset;c+=offset;
                var min=Vector3.Min(a,Vector3.Min(b,c));var max=Vector3.Max(a,Vector3.Max(b,c));
                return max.y>25.3f&&min.y<27.2f&&max.z>-3.25f&&min.z<-1.78f&&
                    (max.x>1.25f&&min.x<3.35f||max.x>-3.35f&&min.x<-1.25f)&&
                    Mathf.Max((a-b).sqrMagnitude,Mathf.Max((b-c).sqrMagnitude,(c-a).sqrMagnitude))>.18f*.18f;
            });
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var vertices=mesh.vertices;var triangles=mesh.GetTriangles(sub);var keep=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var p=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3+offset;
                    if(Mathf.Abs(p.x)>1.25f&&Mathf.Abs(p.x)<3.35f&&p.y>25.3f&&p.y<27.2f&&p.z>-3.25f&&p.z<-1.78f)continue;
                    keep.Add(triangles[i]);keep.Add(triangles[i+1]);keep.Add(triangles[i+2]);
                }
                mesh.SetTriangles(keep,sub);
            }
            mesh.RecalculateBounds();
        }
        static void Subdivide(Mesh mesh,int passes,Func<Vector3,Vector3,Vector3,bool> filter)
        {
            var vertices=mesh.vertices.ToList();
            if(mesh.normals.Length!=vertices.Count)mesh.RecalculateNormals();
            var normals=mesh.normals.ToList();var uv=mesh.uv.Length==vertices.Count?mesh.uv.ToList():Enumerable.Repeat(Vector2.zero,vertices.Count).ToList();
            var tangents=mesh.tangents.Length==vertices.Count?mesh.tangents.ToList():null;
            var colors=mesh.colors.Length==vertices.Count?mesh.colors.ToList():null;
            var submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(sub=>mesh.GetTriangles(sub)).ToArray();
            for(int pass=0;pass<passes;pass++)
            {
                var edges=new Dictionary<long,int>();
                int Midpoint(int a,int b)
                {
                    long key=((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                    if(edges.TryGetValue(key,out int found))return found;
                    int index=vertices.Count;edges[key]=index;
                    vertices.Add((vertices[a]+vertices[b])*.5f);normals.Add((normals[a]+normals[b]).normalized);uv.Add((uv[a]+uv[b])*.5f);
                    if(tangents!=null)tangents.Add((tangents[a]+tangents[b])*.5f);
                    if(colors!=null)colors.Add((colors[a]+colors[b])*.5f);
                    return index;
                }
                for(int sub=0;sub<submeshes.Length;sub++)
                {
                    var triangles=submeshes[sub];var output=new List<int>();
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                        if(filter!=null&&!filter(vertices[a],vertices[b],vertices[c])){output.Add(a);output.Add(b);output.Add(c);continue;}
                        int ab=Midpoint(a,b),bc=Midpoint(b,c),ca=Midpoint(c,a);
                        output.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
                    }
                    submeshes[sub]=output.ToArray();
                }
            }
            mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=vertices.ToArray();mesh.normals=normals.ToArray();mesh.uv=uv.ToArray();
            mesh.tangents=tangents!=null?tangents.ToArray():Array.Empty<Vector4>();mesh.colors=colors!=null?colors.ToArray():Array.Empty<Color>();
            mesh.subMeshCount=submeshes.Length;for(int sub=0;sub<submeshes.Length;sub++)mesh.SetTriangles(submeshes[sub],sub);
            mesh.RecalculateBounds();
        }
        static Transform FitPart(MeshFilter source,Transform parent,string name,Vector3 position,Vector3 size,bool collision,bool world=false)
        {
            var mesh=Object.Instantiate(source.sharedMesh);mesh.name=name;
            var vertices=mesh.vertices;var transform=source.transform.localToWorldMatrix;
            for(int i=0;i<vertices.Length;i++)vertices[i]=transform.MultiplyPoint3x4(vertices[i]);
            mesh.vertices=vertices;mesh.RecalculateBounds();var bounds=mesh.bounds;
            if(bounds.size.z>bounds.size.x && size.x>size.z)
            {for(int i=0;i<vertices.Length;i++)vertices[i]=Quaternion.Euler(0,90,0)*(vertices[i]-bounds.center);mesh.vertices=vertices;mesh.RecalculateBounds();bounds=mesh.bounds;}
            for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i]-bounds.center,new Vector3(size.x/Mathf.Max(.001f,bounds.size.x),size.y/Mathf.Max(.001f,bounds.size.y),size.z/Mathf.Max(.001f,bounds.size.z)));
            mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();
            if(!world&&parent.name=="LastMainMast")CutClimbGates(mesh,position);
            mesh=SaveMesh(mesh,name);
            var t=Child(parent,name,world?parent.InverseTransformPoint(position):position-parent.localPosition);
            if(!world)
            {
                var wreck=parent;while(wreck.parent!=null && wreck.name!="BrokenShipV3")wreck=wreck.parent;
                if(wreck.name=="BrokenShipV3")t.position=wreck.TransformPoint(position);else t.localPosition=position;
            }
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
            if(collision)Collision(t,mesh);return t;
        }
        static void Collision(Transform visual,Mesh mesh)
        {
            if(mesh==null||Enumerable.Range(0,mesh.subMeshCount).All(sub=>mesh.GetIndexCount(sub)==0))return;
            var physics=Child(visual,"Collision",Vector3.zero);
            var collider=physics.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
        }
        static LineRenderer Rope(Transform parent,string name,Material material,Vector3[] points,float radius,bool world)
        {
            var t=Child(parent,name,Vector3.zero);var line=t.gameObject.AddComponent<LineRenderer>();line.useWorldSpace=world;
            line.sharedMaterial=material;line.widthMultiplier=radius*2;line.positionCount=points.Length;line.SetPositions(points);
            line.textureMode=LineTextureMode.Tile;t.gameObject.AddComponent<RiggingRopeVisual>();
            return line;
        }
        static Transform Child(Transform parent,string name,Vector3 point)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=point;return t;
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=Folder+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name=name;
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            existing.Clear(false);existing.indexFormat=mesh.indexFormat;
            existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.tangents=mesh.tangents;existing.colors32=mesh.colors32;
            var uv=new List<Vector4>();
            for(int channel=0;channel<8;channel++){uv.Clear();mesh.GetUVs(channel,uv);existing.SetUVs(channel,uv);}
            existing.subMeshCount=mesh.subMeshCount;
            for(int sub=0;sub<mesh.subMeshCount;sub++)existing.SetTriangles(mesh.GetTriangles(sub),sub,false);
            existing.bounds=mesh.bounds;existing.name=name;existing.UploadMeshData(false);
            EditorUtility.SetDirty(existing);return existing;
        }
    }
}
