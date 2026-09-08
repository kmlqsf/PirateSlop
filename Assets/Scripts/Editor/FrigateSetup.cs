using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class FrigateSetup
    {
        [Serializable] public sealed class BoxData { public string name; public float[] p, s, r; }
        [Serializable] public sealed class HullData { public string name; public float[] points; }
        [Serializable] public sealed class MaterialData { public string name; public float[] color; }
        [Serializable] public sealed class SailData { public string name; public float[] pivot; }
        [Serializable] public sealed class Layout { public BoxData[] boxes; public HullData[] hulls; public MaterialData[] materials; public SailData[] sails; }
        const string Folder = "Assets/Models/Ships/Frigate";
        static Vector3 V(float[] p) => new Vector3(p[0], p[1], p[2]);
        [MenuItem("PirateSlop/Configure Pirate Frigate")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder + "/Meshes"); Directory.CreateDirectory(Folder + "/Materials"); AssetDatabase.Refresh();
            var data = JsonUtility.FromJson<Layout>(File.ReadAllText(Folder + "/FrigateLayout.json"));
            var materials = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var entry in data.materials)
            {
                string path = Folder + "/Materials/" + entry.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
                mat.color = new Color(entry.color[0],entry.color[1],entry.color[2],1);
                mat.SetFloat("_Smoothness", entry.name.Contains("Brass") ? .4f : .18f);
                mat.SetFloat("_Metallic", entry.name.Contains("Brass") || entry.name.Contains("Iron") ? .6f : 0);
                mat.SetFloat("_Cull",0); mat.enableInstancing = true; EditorUtility.SetDirty(mat); materials.Add(entry.name,mat);
            }
            const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var root = PrefabUtility.LoadPrefabContents(shipPath);
            try
            {
                foreach (string name in new[] { "SchoonerVisual", "SchoonerCollision", "ShipLadders", "FrigateVisual", "FrigateCollision", "FrigateLadders" })
                {
                    var old = root.transform.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                var visual = new GameObject("FrigateVisual").transform; visual.SetParent(root.transform,false);
                var collision = new GameObject("FrigateCollision").transform; collision.SetParent(root.transform,false);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PirateFrigate.fbx");
                var anchors = source.GetComponentsInChildren<Transform>();
                var origin = anchors.First(t => t.name=="Anchor_Origin").position;
                var right = anchors.First(t => t.name=="Anchor_Right").position-origin;
                var up = anchors.First(t => t.name=="Anchor_Up").position-origin;
                var forward = anchors.First(t => t.name=="Anchor_Forward").position-origin;
                var basis = Matrix4x4.identity; basis.SetColumn(0,new Vector4(right.x,right.y,right.z,0)); basis.SetColumn(1,new Vector4(up.x,up.y,up.z,0)); basis.SetColumn(2,new Vector4(forward.x,forward.y,forward.z,0)); basis.SetColumn(3,new Vector4(origin.x,origin.y,origin.z,1));
                var correction = basis.inverse;
                Transform wheel = null;
                var sails = new System.Collections.Generic.List<Transform>();
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>(); if (renderer == null) continue;
                    var go = new GameObject(filter.name); go.transform.SetParent(visual,false);
                    Vector3 pivot = Vector3.zero;
                    var sail = data.sails.FirstOrDefault(s=>s.name==filter.name);
                    if (sail != null) { pivot=V(sail.pivot); sails.Add(go.transform); }
                    if (filter.name=="HelmWheel") { pivot=new Vector3(0,8.45f,-16.8f); wheel=go.transform; }
                    go.transform.localPosition=pivot;
                    var matrix=Matrix4x4.Translate(-pivot)*correction*filter.transform.localToWorldMatrix;
                    var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name=filter.name;
                    mesh.vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();
                    if (matrix.determinant<0)
                        for(int sub=0;sub<mesh.subMeshCount;sub++)
                        { var tris=mesh.GetTriangles(sub); for(int i=0;i<tris.Length;i+=3) { int tmp=tris[i];tris[i]=tris[i+2];tris[i+2]=tmp; } mesh.SetTriangles(tris,sub); }
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    string path=Folder+"/Meshes/"+filter.name+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing==null) AssetDatabase.CreateAsset(mesh,path);
                    else { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; }
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials.Select(m=>materials[m.name]).ToArray();
                }
                foreach(var box in data.boxes)
                {
                    var go=new GameObject(box.name); go.transform.SetParent(collision,false);
                    go.transform.localPosition=V(box.p); go.transform.localRotation=Quaternion.Euler(V(box.r));
                    go.AddComponent<BoxCollider>().size=V(box.s);
                }
                CreateDeckCollision(collision, data.hulls);
                var helm=root.GetComponentInChildren<HelmInteraction>(true);
                helm.transform.localPosition=new Vector3(0,8.2f,-17.1f); helm.Configure(wheel);
                var handle=wheel.gameObject.AddComponent<ShipControlHandle>(); handle.Helm=helm;
                var wheelBox=wheel.gameObject.AddComponent<BoxCollider>(); wheelBox.center=wheel.GetComponent<MeshFilter>().sharedMesh.bounds.center; wheelBox.size=wheel.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                var sailSystem=root.GetComponent<SailSystem>();
                sailSystem.MastControls=collision.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.EndsWith("Mast")).Cast<Collider>().ToArray();
                var so=new SerializedObject(sailSystem); var array=so.FindProperty("sailMeshes"); array.arraySize=sails.Count;
                for(int i=0;i<sails.Count;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=sails[i]; so.ApplyModifiedPropertiesWithoutUndo();
                var crate=root.GetComponentInChildren<CannonballCrate>(true); crate.transform.localPosition=new Vector3(-2.2f,1.03f,-13f);
                crate.Kit.transform.localPosition=new Vector3(2.2f,1.03f,-13f);
                if (crate.DeckSupplyPoint != null) crate.DeckSupplyPoint.localPosition=new Vector3(-2.2f,1.3f,-11f);
                var ladderGroup=new GameObject("FrigateLadders").transform; ladderGroup.SetParent(root.transform,false);
                AddLadder(ladderGroup,"MastLadder",new Vector3(0,4.3f,-3.85f),180,28.9f,.8f);
                var boarding=AddLadder(ladderGroup,"BoardingLadder",new Vector3(6.7f,-2,0),90,6.5f,1.2f);
                for(float y=.15f;y<6.5f;y+=.32f) Primitive(boarding,"Rung",new Vector3(0,y,0),new Vector3(.95f,.085f,.12f),materials["Frigate_Wood"]);
                foreach(float x in new[]{-.48f,.48f}) Primitive(boarding,"Rail",new Vector3(x,3.3f,0),new Vector3(.1f,7.2f,.12f),materials["Frigate_Wood"]);
                var motor=new SerializedObject(root.GetComponent<ShipController>()); motor.FindProperty("floatLength").floatValue=18; motor.FindProperty("floatWidth").floatValue=5.5f; motor.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,shipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var config=AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.PlayerLocalSpawn=new Vector3(0,7.2f,-19.2f); config.ProtocolVersion=Mathf.Max(config.ProtocolVersion,23); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            RiggingSetup.Configure();
        }
        static Transform AddLadder(Transform parent,string name,Vector3 position,float yaw,float height,float depth)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var ladder=go.AddComponent<ShipLadder>();ladder.Height=height;ladder.ExitDepth=depth;return go.transform;
        }
        static void Primitive(Transform parent,string name,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
        }
        static void CreateDeckCollision(Transform parent, HullData[] hulls)
        {
            int index=0;
            foreach(var hull in hulls)
            {
                var vertices=new Vector3[8];for(int i=0;i<8;i++) vertices[i]=new Vector3(hull.points[i*3],hull.points[i*3+1],hull.points[i*3+2]);
                var mesh=new Mesh();mesh.vertices=vertices;mesh.triangles=new[]{0,3,2,0,2,1,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=Folder+"/Meshes/Collision_"+(index++)+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old==null) AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}
                var go=new GameObject(hull.name);go.transform.SetParent(parent,false);var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
            }
            if(index==0) throw new InvalidOperationException("Frigate deck collision data missing.");
        }
    }
}
