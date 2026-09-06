using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class SchoonerSceneSetup
    {
        const string Model = "Assets/Models/PirateSchooner/SM_PirateSchooner.fbx";
        const string Ship = "Assets/Prefabs/Networking/NetworkShip.prefab";
        const string Generated = "Assets/Models/PirateSchooner/Collision";
        static readonly float[] Z = {-13,-12,-10,-8,-6,-4,-2,0,2,4,6,8,10,12,13.5f};
        static readonly float[] W = {2.45f,2.8f,3.25f,3.6f,3.8f,3.9f,3.95f,3.9f,3.75f,3.5f,3.12f,2.65f,1.95f,1.02f,.06f};
        public static readonly Vector3 Spawn = new Vector3(0,5.42f,-10.1f);

        [MenuItem("PirateSlop/Install New Schooner")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open SampleScene first.");
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory("Assets/Materials/PirateSchooner");
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.isReadable = true; importer.addCollider = false; importer.SaveAndReimport();
            var original = GameObject.Find("SM_PirateSloop");
            if (original == null) throw new InvalidOperationException("Ship root missing.");
            Undo.RegisterFullObjectHierarchyUndo(original,"Install schooner");
            Apply(original);
            var player = GameObject.Find("PlayerCharacter");
            Undo.RecordObject(player.transform,"Move spawn to quarterdeck");
            player.transform.position = original.transform.TransformPoint(Spawn);
            player.transform.rotation = original.transform.rotation;
            var network = PrefabUtility.LoadPrefabContents(Ship);
            try { Apply(network); PrefabUtility.SaveAsPrefabAsset(network,Ship); }
            finally { PrefabUtility.UnloadPrefabContents(network); }
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.PlayerLocalSpawn = Spawn;
            config.SpawnSpacing = Mathf.Max(config.SpawnSpacing,52);
            EditorUtility.SetDirty(config);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SCHOONER_INSTALLED: existing network prefab/root components preserved.");
        }

        static GameObject Child(Transform parent,string name)
        { var g=new GameObject(name);g.transform.SetParent(parent,false);return g; }
        static void Box(Transform parent,string name,Vector3 p,Vector3 size)
        { var g=Child(parent,name);g.transform.localPosition=p;g.AddComponent<BoxCollider>().size=size; }
        static Mesh Slice(int i,float top,float bottom)
        {
            float a=Z[i],b=Z[i+1],wa=W[i]-.08f,wb=W[i+1]-.08f;
            wa=Mathf.Max(.03f,wa);wb=Mathf.Max(.03f,wb);
            var mesh=new Mesh();mesh.name="HullSlice"+i;
            mesh.vertices=new[]{new Vector3(-wa,bottom,a),new Vector3(wa,bottom,a),new Vector3(wb,bottom,b),new Vector3(-wb,bottom,b),new Vector3(-wa,top,a),new Vector3(wa,top,a),new Vector3(wb,top,b),new Vector3(-wb,top,b)};
            mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Generated+"/"+mesh.name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void Apply(GameObject ship)
        {
            var old=ship.transform.Find("SM_PirateSloop_Model");if(old!=null)old.gameObject.SetActive(false);
            foreach(string n in new[]{"SchoonerVisual","SchoonerCollision"})
            {var t=ship.transform.Find(n);if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);}
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),ship.transform);
            visual.name="SchoonerVisual";
            // Import inspection: Blender +Y becomes Unity -Z. Rotate visual only.
            visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
            visual.transform.localPosition=Vector3.zero;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>{
                    string path="Assets/Materials/PirateSchooner/"+source.name+".mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=source.name;mat.SetColor("_BaseColor",source.color);mat.SetFloat("_Smoothness",.2f);mat.SetFloat("_Cull",0);AssetDatabase.CreateAsset(mat,path);}
                    return mat;
                }).ToArray();
            }
            var wheel=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="HelmWheel");
            var helm=ship.GetComponentInChildren<HelmInteraction>();
            helm.transform.position=wheel.position;helm.Configure(wheel);
            ship.GetComponent<ShipController>().Configure(helm);
            var networkShip=ship.GetComponent<NetworkShip>();
            if(networkShip!=null)
            {
                // Existing circular server separation must not shrink when hull is split into smaller colliders.
                var data=new SerializedObject(networkShip);data.FindProperty("collisionRadiusOverride").floatValue=11.3f;data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(helm);EditorUtility.SetDirty(ship.GetComponent<ShipController>());
            var coll=Child(ship.transform,"SchoonerCollision").transform;
            for(int i=0;i<Z.Length-1;i++)
            {
                var g=Child(coll,"HullDeck_"+i);var c=g.AddComponent<MeshCollider>();c.sharedMesh=Slice(i,3.32f,.6f);c.convex=true;
                if(Z[i+1]<=-6){var d=Child(coll,"Quarterdeck_"+i);var q=d.AddComponent<MeshCollider>();
                    var m=UnityEngine.Object.Instantiate(c.sharedMesh);m.name="QuarterdeckSlice"+i;
                    m.vertices=m.vertices.Select(v=>new Vector3(v.x,v.y>2?5.34f:5.1f,v.z)).ToArray();m.RecalculateBounds();
                    string path=Generated+"/"+m.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing==null)AssetDatabase.CreateAsset(m,path);else{EditorUtility.CopySerialized(m,existing);UnityEngine.Object.DestroyImmediate(m);m=existing;}
                    q.sharedMesh=m;q.convex=true;}
                for(int side=-1;side<=1;side+=2)
                {
                    float y=Z[i+1]<=-6?5.8f:3.8f;
                    // Omit transition segment at staircase, which has separate handrails.
                    if(Z[i]==-6)continue;
                    var a=new Vector3(side*(W[i]-.1f),y,Z[i]);var b=new Vector3(side*(W[i+1]-.1f),y,Z[i+1]);
                    var rail=Child(coll,"SideRail");rail.transform.localPosition=(a+b)/2;rail.transform.localRotation=Quaternion.LookRotation(b-a);
                    rail.AddComponent<BoxCollider>().size=new Vector3(.15f,.94f,(b-a).magnitude);
                }
            }
            Box(coll,"SternRail",new Vector3(0,5.8f,-12.85f),new Vector3(4.7f,1,.16f));
            Box(coll,"BridgeFrontRail",new Vector3(0,5.8f,-6),new Vector3(3.25f,1,.14f));
            Box(coll,"Cabin",new Vector3(0,4.25f,-9.2f),new Vector3(3.5f,1.84f,6.1f));
            foreach(float x in new[]{-2.45f,2.45f})
            {
                Vector3 a=new Vector3(x,3.34f,-2.45f),b=new Vector3(x,5.34f,-6.02f);
                var ramp=Child(coll,"StairRamp");ramp.transform.localPosition=(a+b)/2-Vector3.up*.06f;ramp.transform.localRotation=Quaternion.LookRotation(b-a);
                ramp.AddComponent<BoxCollider>().size=new Vector3(1.5f,.12f,(b-a).magnitude+.15f);
            }
            foreach(float z in new[]{-3.9f,4.6f})Box(coll,"Mast",new Vector3(0,5,z),new Vector3(.5f,3.4f,.5f));
            Box(coll,"HelmPedestal",new Vector3(0,5.92f,-8.3f),new Vector3(.4f,1.16f,.4f));
            Box(coll,"Hatch",new Vector3(0,3.51f,1.2f),new Vector3(1.9f,.38f,2.3f));
        }
    }
}
