using System.Collections.Generic;
using System.Linq;
using PirateSlop.Ships;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipV3SailFootSetup
    {
        [MenuItem("PirateSlop/Ships/Repair Sail Foot Bindings")]
        public static void Configure()
        {
            foreach(var path in new[] {"Assets/Resources/Ships/ShipV3Test.prefab","Assets/Resources/Ships/ShipV3Menu.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try { Apply(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            var seams=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.Contains("_Seam_")).ToArray();
            var features=root.GetComponent<ShipV3Features>();
            if(features!=null) features.Attachments=features.Attachments.Where(a=>a.Object==null || !seams.Any(r=>r.transform==a.Object)).ToArray();
            foreach(var section in root.GetComponentsInChildren<ShipDamageSection>(true))
                section.DependentRenderers=section.DependentRenderers.Where(r=>r==null || !seams.Contains(r)).ToArray();
            var rig=root.GetComponent<ShipV3VisualRig>();
            if(rig!=null)
            {
                var keep=Enumerable.Range(0,rig.RigMeshes.Length).Where(i=>rig.RigMeshes[i]==null || !seams.Contains(rig.RigMeshes[i])).ToArray();
                rig.RigMeshes=keep.Select(i=>rig.RigMeshes[i]).ToArray(); rig.RigSailIndices=keep.Select(i=>rig.RigSailIndices[i]).ToArray();
            }
            foreach(var seam in seams) Object.DestroyImmediate(seam.gameObject);
            if(rig==null) { ApplyMenu(root); return; }
            var material=root.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="V3_Transfer_Rig_Sail_Main_Course_Tie_00").sharedMaterial;
            var meshes=new List<SkinnedMeshRenderer>(); var indices=new List<int>();
            for(int i=0;i<rig.RigMeshes.Length;i++)
                if(rig.RigMeshes[i]!=null && !rig.RigMeshes[i].name.EndsWith("_FootLashings")) { meshes.Add(rig.RigMeshes[i]); indices.Add(rig.RigSailIndices[i]); }
            for(int i=0;i<rig.Sails.Length;i++)
            {
                var sail=rig.Sails[i];
                string tag=sail.name.Replace("V3_Transfer_Outfit_Sail_","");
                var yard=root.GetComponentsInChildren<Transform>(true).First(t=>t.name==(i==4 ? "V3_Transfer_Mizzen_FootBoom_Pivot" : "V3_Transfer_MovingYard_"+tag));
                var motion=i==4 ? rig.Motions.First(m=>m.SailIndex==4 && m.Target==yard) : rig.Motions.Where(m=>m.SailIndex==i && m.Target.name.StartsWith("V3_Piece_")).OrderBy(m=>(m.Target.position-yard.position).sqrMagnitude).First();
                var original=AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Ships/ShipV3/ShipV3.fbx").OfType<Mesh>().First(m=>m.name==sail.name);
                var boom=i==4 ? EnsureMizzenBoom(root.transform,sail,original,motion) : null;
                if(i<4) PinFoot(root.transform,sail,original,motion,tag);
                var old=root.transform.Find(tag+"_FootLashings");
                if(old!=null)
                {
                    var previous=old.GetComponent<Renderer>();
                    foreach(var section in root.GetComponentsInChildren<ShipDamageSection>(true))
                        section.DependentRenderers=section.DependentRenderers.Where(r=>r!=previous).ToArray();
                    Object.DestroyImmediate(old.gameObject);
                }
                var node=new GameObject(tag+"_FootLashings"); node.transform.SetParent(root.transform,false);
                var skin=node.AddComponent<SkinnedMeshRenderer>();
                var generated=Build(root.transform,sail,motion,i==4);
                string path="Assets/Models/Ships/ShipV3/SailFoot_"+tag+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null) { AssetDatabase.CreateAsset(generated,path); saved=generated; }
                else { EditorUtility.CopySerialized(generated,saved); Object.DestroyImmediate(generated); EditorUtility.SetDirty(saved); }
                skin.sharedMesh=saved; skin.sharedMaterial=material; skin.localBounds=saved.bounds;
                meshes.Add(skin); indices.Add(i);
                foreach(var section in root.GetComponentsInChildren<ShipDamageSection>(true))
                    if(section.DependentRenderers.Contains(sail) || section.transform==motion.Target)
                    {
                        section.DependentRenderers=section.DependentRenderers.Append(skin).ToArray();
                        if(boom!=null && !section.DependentRenderers.Contains(boom)) section.DependentRenderers=section.DependentRenderers.Append(boom).ToArray();
                    }
            }
            rig.RigMeshes=meshes.ToArray(); rig.RigSailIndices=indices.ToArray();
        }

        static void ApplyMenu(GameObject root)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
            var rig=source.GetComponent<ShipV3VisualRig>();
            foreach(var sail in rig.Sails)
            {
                var target=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==sail.name);
                target.sharedMesh=sail.sharedMesh; target.localBounds=sail.localBounds;
            }
            foreach(var renderer in rig.RigMeshes.Where(r=>r!=null && r.name.EndsWith("_FootLashings")))
            {
                var old=root.transform.Find(renderer.name); if(old!=null) Object.DestroyImmediate(old.gameObject);
                var copy=Object.Instantiate(renderer.gameObject,root.transform); copy.name=renderer.name;
            }
            var pivot=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="V3_Transfer_Mizzen_FootBoom_Pivot");
            var oldBoom=pivot.Find("Mizzen_FootBoom"); if(oldBoom!=null) Object.DestroyImmediate(oldBoom.gameObject);
            var boom=source.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Mizzen_FootBoom");
            var node=Object.Instantiate(boom.gameObject,pivot); node.name=boom.name;
        }

        static Matrix4x4 YardMatrix(Transform root,ShipV3Motion motion,float deployed)
        {
            float frame=deployed*(motion.Poses.Length-1); int at=Mathf.Min(Mathf.FloorToInt(frame),motion.Poses.Length-2); float blend=frame-at;
            var a=motion.Poses[at]; var b=motion.Poses[at+1];
            return root.worldToLocalMatrix*motion.Target.parent.localToWorldMatrix*Matrix4x4.TRS(Vector3.Lerp(a.Position,b.Position,blend),Quaternion.Slerp(a.Rotation,b.Rotation,blend),Vector3.Lerp(a.Scale,b.Scale,blend));
        }

        static Mesh SaveMesh(Mesh generated,string path)
        {
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) { AssetDatabase.CreateAsset(generated,path); return generated; }
            EditorUtility.CopySerialized(generated,saved); Object.DestroyImmediate(generated); EditorUtility.SetDirty(saved); return saved;
        }

        static void PinFoot(Transform root,SkinnedMeshRenderer sail,Mesh original,ShipV3Motion motion,string tag)
        {
            var matrix=root.worldToLocalMatrix*sail.transform.localToWorldMatrix;
            var uv=original.uv; var source=original.vertices;
            Vector3[] Correct(Vector3[] vertices,float deployed)
            {
                var bar=YardMatrix(root,motion,deployed).MultiplyPoint3x4(Vector3.zero);
                var result=new Vector3[vertices.Length];
                var hem=Enumerable.Range(0,source.Length).Where(v=>uv[v].y<.001f).ToArray();
                for(int v=0;v<vertices.Length;v++)
                {
                    int at=hem.OrderBy(h=>Mathf.Abs(uv[h].x-uv[v].x)).First();
                    var bottom=matrix.MultiplyPoint3x4(vertices[at]);
                    var target=new Vector3(bottom.x,bar.y+.17f-.03f*Mathf.Sin(uv[v].x*Mathf.PI),bar.z);
                    var point=matrix.MultiplyPoint3x4(vertices[v])+(target-bottom)*Mathf.Pow(1f-uv[v].y,3f);
                    result[v]=matrix.inverse.MultiplyPoint3x4(point);
                }
                return result;
            }
            var rest=Correct(source,1f); var mesh=Object.Instantiate(original); mesh.name=original.name+"_FootPinned"; mesh.ClearBlendShapes();
            mesh.vertices=rest; mesh.RecalculateNormals(); mesh.RecalculateTangents(); var normals=mesh.normals;
            mesh.RecalculateBounds(); var bounds=mesh.bounds; var zero=new Vector3[source.Length];
            for(int shape=0;shape<original.blendShapeCount;shape++)
            {
                var delta=new Vector3[source.Length]; original.GetBlendShapeFrameVertices(shape,original.GetBlendShapeFrameCount(shape)-1,delta,new Vector3[source.Length],zero);
                var pose=Correct(source.Select((v,i)=>v+delta[i]).ToArray(),0f);
                mesh.vertices=pose; mesh.RecalculateNormals(); var changed=mesh.normals; var deltaNormals=new Vector3[source.Length];
                for(int v=0;v<source.Length;v++) { bounds.Encapsulate(pose[v]); delta[v]=pose[v]-rest[v]; deltaNormals[v]=changed[v]-normals[v]; }
                mesh.AddBlendShapeFrame(original.GetBlendShapeName(shape),100f,delta,deltaNormals,zero);
            }
            mesh.vertices=rest; mesh.normals=normals; mesh.bounds=bounds;
            sail.sharedMesh=SaveMesh(mesh,"Assets/Models/Ships/ShipV3/SailPinned_"+tag+".asset"); sail.localBounds=bounds;
        }

        static MeshRenderer EnsureMizzenBoom(Transform root,SkinnedMeshRenderer sail,Mesh original,ShipV3Motion motion)
        {
            var old=motion.Target.Find("Mizzen_FootBoom");
            if(old!=null) return old.GetComponent<MeshRenderer>();
            var uv=original.uv;
            var points=original.vertices.Select(v=>root.InverseTransformPoint(sail.transform.TransformPoint(v))).ToArray();
            var hem=Enumerable.Range(0,points.Length).Where(v=>Mathf.Abs(uv[v].y+uv[v].x-1f)<.001f).Select(v=>points[v]).ToArray();
            var a=hem.OrderBy(v=>v.z).First()-Vector3.up*.17f; var b=hem.OrderByDescending(v=>v.z).First()-Vector3.up*.17f;
            var direction=(b-a).normalized; a-=direction*.15f; b+=direction*.15f;
            var normal=Vector3.Cross(direction,Vector3.right).normalized; var vertices=new Vector3[32]; var coords=new Vector2[32]; var indices=new List<int>();
            for(int ring=0;ring<2;ring++)
                for(int v=0;v<16;v++)
                {
                    float angle=v*Mathf.PI/8f; int at=ring*16+v;
                    var point=(ring==0 ? a : b)+(Vector3.right*Mathf.Cos(angle)+normal*Mathf.Sin(angle))*.12f;
                    vertices[at]=motion.Target.InverseTransformPoint(root.TransformPoint(point)); coords[at]=new Vector2(v/16f,ring*Vector3.Distance(a,b));
                    if(ring==0) { int next=(v+1)%16; indices.AddRange(new[]{v,next,next+16,v,next+16,v+16}); }
                }
            var mesh=new Mesh {name="Mizzen_FootBoom",vertices=vertices,uv=coords,triangles=indices.ToArray()}; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var node=new GameObject("Mizzen_FootBoom"); node.transform.SetParent(motion.Target,false); node.AddComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,"Assets/Models/Ships/ShipV3/SailFoot_Mizzen_Boom.asset");
            var renderer=node.AddComponent<MeshRenderer>(); renderer.sharedMaterial=root.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="V3_Transfer_MovingYard_Main_Course").sharedMaterial; return renderer;
        }

        static Mesh Build(Transform root,SkinnedMeshRenderer sail,ShipV3Motion motion,bool mizzen)
        {
            var source=sail.sharedMesh; var vertices=source.vertices;
            var matrix=root.worldToLocalMatrix*sail.transform.localToWorldMatrix;
            var full=vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();
            var delta=new Vector3[vertices.Length]; var zero=new Vector3[vertices.Length];
            int furl=source.GetBlendShapeIndex("Furled");
            if(furl<0) furl=source.GetBlendShapeIndex("Deployed.Furled");
            if(furl>=0) source.GetBlendShapeFrameVertices(furl,source.GetBlendShapeFrameCount(furl)-1,delta,zero,zero);
            Vector3 across=mizzen ? Vector3.forward : Vector3.right;
            Vector3 normal=mizzen ? Vector3.right : Vector3.forward;
            float min=full.Min(v=>Vector3.Dot(v,across)), max=full.Max(v=>Vector3.Dot(v,across));
            var hems=new int[7];
            var edge=Enumerable.Range(0,vertices.Length).Where(v=>mizzen ? Mathf.Abs(source.uv[v].x+source.uv[v].y-1f)<.001f : source.uv[v].y<.001f).ToArray();
            for(int tie=0;tie<hems.Length;tie++)
            {
                float x=Mathf.Lerp(min,max,.08f+tie*.14f), best=float.MaxValue;
                foreach(int v in edge)
                {
                    float score=Mathf.Abs(Vector3.Dot(full[v],across)-x);
                    if(score<best) { best=score; hems[tie]=v; }
                }
            }
            Vector3 barCenter=root.InverseTransformPoint(motion.Target.position);
            float slope=0f;
            if(mizzen)
            {
                var pole=motion.Target.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.gameObject.activeSelf && f.sharedMesh!=null).OrderByDescending(f=>f.sharedMesh.bounds.size.magnitude).First();
                var points=pole.sharedMesh.vertices.Select(v=>root.InverseTransformPoint(pole.transform.TransformPoint(v))).ToArray();
                barCenter=points.Aggregate(Vector3.zero,(sum,p)=>sum+p)/points.Length;
                float covariance=0f,variance=0f;
                foreach(var p in points) { float x=Vector3.Dot(p-barCenter,across); covariance+=x*(p.y-barCenter.y); variance+=x*x; }
                slope=variance>.001f ? covariance/variance : 0f;
            }
            var knotLocal=hems.Select(v=>
            {
                float x=Vector3.Dot(full[v]-barCenter,across);
                return motion.Target.InverseTransformPoint(root.TransformPoint(barCenter+across*x+Vector3.up*(slope*x)));
            }).ToArray();
            var triangles=new List<int>(); var uv=new List<Vector2>();
            Vector3[] Pose(float deployed)
            {
                bool topology=uv.Count==0;
                var yard=YardMatrix(root,motion,deployed);
                var output=new List<Vector3>();
                for(int tie=0;tie<hems.Length;tie++)
                {
                    var hem=matrix.MultiplyPoint3x4(vertices[hems[tie]]+delta[hems[tie]]*(1f-deployed));
                    var bar=yard.MultiplyPoint3x4(knotLocal[tie]);
                    float bottom=bar.y-(mizzen ? .15f : .12f), top=hem.y+.018f;
                    var center=(hem+bar)*.5f; center.y=(bottom+top)*.5f;
                    float height=Mathf.Max(.08f,(top-bottom)*.5f), width=mizzen ? .155f : .125f;
                    const int samples=48, sides=8;
                    float circumference=Mathf.PI*2f*Mathf.Sqrt((height*height+width*width)*.5f);
                    for(int p=0;p<=samples;p++)
                    {
                        float pathAngle=p*Mathf.PI*2f/samples;
                        var point=center+Vector3.up*(Mathf.Cos(pathAngle)*height)+normal*(Mathf.Sin(pathAngle)*width);
                        var direction=(-Vector3.up*(Mathf.Sin(pathAngle)*height)+normal*(Mathf.Cos(pathAngle)*width)).normalized;
                        var side=Vector3.Cross(direction,across).normalized;
                        var other=Vector3.Cross(direction,side).normalized;
                        for(int ring=0;ring<sides;ring++)
                        {
                            float angle=ring*Mathf.PI*2f/sides;
                            output.Add(point+(side*Mathf.Cos(angle)+other*Mathf.Sin(angle))*.017f);
                            if(topology) uv.Add(new Vector2(ring/(float)sides,p/(float)samples*circumference/.12f));
                            if(topology && p>0)
                            {
                                int index=output.Count-1, start=index-ring, previous=start-sides, next=(ring+1)%sides;
                                triangles.Add(previous+ring); triangles.Add(start+ring); triangles.Add(start+next);
                                triangles.Add(previous+ring); triangles.Add(start+next); triangles.Add(previous+next);
                            }
                        }
                    }
                }
                return output.ToArray();
            }
            var rest=Pose(1f);
            var mesh=new Mesh {name=sail.name+"_FootLashings",vertices=rest,uv=uv.ToArray(),triangles=triangles.ToArray()};
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); var normals=mesh.normals;
            var bounds=mesh.bounds; var empty=new Vector3[rest.Length];
            for(int deployment=0;deployment<=100;deployment+=25)
            {
                var pose=Pose(deployment*.01f); var offsets=new Vector3[rest.Length]; var normalDelta=new Vector3[rest.Length];
                foreach(var point in pose) bounds.Encapsulate(point);
                mesh.vertices=pose; mesh.RecalculateNormals(); var changed=mesh.normals;
                for(int v=0;v<rest.Length;v++) { offsets[v]=pose[v]-rest[v]; normalDelta[v]=changed[v]-normals[v]; }
                mesh.AddBlendShapeFrame("Deploy_"+deployment,100f,offsets,normalDelta,empty);
            }
            mesh.vertices=rest; mesh.normals=normals; mesh.bounds=bounds; return mesh;
        }
    }
}
