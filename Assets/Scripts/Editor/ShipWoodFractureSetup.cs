using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class ShipWoodFractureSetup
    {
        const string ShipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
        [MenuItem("PirateSlop/Prepare Wood Destruction")]
        public static void PrepareAll()
        {
            ShipConnectedFractureSetup.Configure();
        }
        public static void SaveSections()
        {
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                var profile = root.GetComponent<ShipDestruction>().Profile;
                profile.SectionsPrefab = PrefabUtility.SaveAsPrefabAsset(root.transform.Find("ShipDestructionSections").gameObject,"Assets/Prefabs/ShipDestruction/MainShipSections.prefab");
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void ConfigureEffects()
        {
            string folder="Assets/Models/Ships/MainShip/Destruction/Splinters";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var profile=AssetDatabase.LoadAssetAtPath<ShipDestructionProfile>("Assets/Settings/ShipDestruction/MainShipDestruction.asset");
            var meshes=new Mesh[6];
            for(int i=0;i<meshes.Length;i++)
            {
                var mesh=new Mesh {name="WoodSplinter"+i};float length=.22f+i*.075f,width=.016f+i*.005f;
                mesh.vertices=new[]{new Vector3(-width,0,0),new Vector3(0,width*.5f,0),new Vector3(width,0,0),new Vector3(0,-width*.5f,0),new Vector3(width*.6f,0,length),new Vector3(-width*.3f,0,-length*.6f)};
                mesh.triangles=new[]{0,1,4,1,2,4,2,3,4,3,0,4,1,0,5,2,1,5,3,2,5,0,3,5};mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=folder+"/WoodSplinter"+i+".asset";
                var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(stored==null){AssetDatabase.CreateAsset(mesh,path);stored=mesh;}else{EditorUtility.CopySerialized(mesh,stored);UnityEngine.Object.DestroyImmediate(mesh);}
                meshes[i]=stored;
            }
            profile.SplinterMeshes=meshes;profile.SplinterMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/MainShip/Destruction/FreshSplitWood.mat");
            profile.EnableFlooding=false;EditorUtility.SetDirty(profile);
            var config=AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");config.ProtocolVersion=Mathf.Max(config.ProtocolVersion,71);EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        public static string Prepare(int sectionId)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode");
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            GameObject staging = null; Mesh compressed = null;
            try
            {
                var destruction = root.GetComponent<ShipDestruction>();
                var section = destruction.Sections.First(s => s.SectionId == sectionId);
                var definition = destruction.Profile.Sections.First(s => s.SectionId == sectionId);
                if (!section.SafeColliderReplacement) return "Protected support retained: " + sectionId;
                string folder = "Assets/Models/Ships/MainShip/Destruction/WoodFragments";
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                string packedPath = folder + "/SD" + sectionId + ".asset";
                var original = section.Intact.GetComponent<MeshFilter>();
                compressed = UnityEngine.Object.Instantiate(original.sharedMesh);
                var allTriangles = compressed.triangles;
                compressed.subMeshCount = 1;
                compressed.SetTriangles(allTriangles,0);
                var scale = definition.Type == ShipSectionType.Mast || definition.Type == ShipSectionType.Yard ? new Vector3(1f,.3f,1f) : new Vector3(1f,1f,.55f);
                compressed.vertices = compressed.vertices.Select(v => Vector3.Scale(v,scale)).ToArray();
                compressed.RecalculateNormals(); compressed.RecalculateBounds();
                staging = new GameObject("WoodFracturePreparation");
                staging.AddComponent<MeshFilter>().sharedMesh = compressed;
                staging.AddComponent<MeshRenderer>().sharedMaterials = section.Intact.GetComponent<Renderer>().sharedMaterials;
                var fresh = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/MainShip/Destruction/FreshSplitWood.mat");
                var parameters = new LibreFracture.VoronoiParameters { totalChunks = definition.Type == ShipSectionType.Hull ? 64 : 32, insideMaterial = fresh, enableIslands = false, totalObjectMass = definition.DebrisMass };
                LibreFracture.LibreFracture.Fracture(staging,parameters,false,true);
                var fragments = staging.GetComponentsInChildren<MeshFilter>().Where(f => f.gameObject != staging && f.sharedMesh.vertexCount > 3).ToArray();
                if (fragments.Length < 4 || fragments.Length > 64) throw new InvalidOperationException("Invalid fragment count: " + fragments.Length);
                foreach (var behaviour in staging.GetComponentsInChildren<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach (var joint in staging.GetComponentsInChildren<Joint>()) UnityEngine.Object.DestroyImmediate(joint);
                foreach (var collider in staging.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var body in staging.GetComponentsInChildren<Rigidbody>()) UnityEngine.Object.DestroyImmediate(body);
                var previous = section.transform.Find("WoodFragments");
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var container = new GameObject("WoodFragments"); container.transform.SetParent(section.transform,false);
                var damage = section.DamageColliders.Where(c=>c!=null).ToList();
                for (int i=0;i<fragments.Length;i++)
                {
                    var filter = fragments[i]; var mesh = filter.sharedMesh;
                    mesh.vertices = mesh.vertices.Select(v=>new Vector3(v.x/scale.x,v.y/scale.y,v.z/scale.z)).ToArray();
                    var collisionMesh=UnityEngine.Object.Instantiate(mesh);
                    collisionMesh.RecalculateBounds();
                    var storedCollision=ShipFragmentPacking.Store(collisionMesh,packedPath,"Collision"+i.ToString("00"));

                    mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                    var stored=ShipFragmentPacking.Store(mesh,packedPath,"Fragment"+i.ToString("00"));
                    filter.sharedMesh=stored; filter.name="WoodFragment"+i.ToString("00");
                    filter.transform.SetParent(container.transform,false);filter.transform.localPosition=Vector3.zero;filter.transform.localRotation=Quaternion.identity;filter.transform.localScale=Vector3.one;
                    filter.gameObject.layer=section.gameObject.layer;
                    var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=storedCollision;
                    damage.Add(collider);filter.gameObject.SetActive(false);
                }
                section.Fragments=fragments.Select(f=>f.gameObject).ToArray();section.DamageColliders=damage.ToArray();
                section.Apply(ShipSectionState.Intact);
                destruction.Profile.EnableFlooding=false;
                var water=root.transform.Find("HoldFloodWater");if(water!=null)UnityEngine.Object.DestroyImmediate(water.gameObject);
                root.GetComponent<ShipFlooding>().WaterVisual=null;
                EditorUtility.SetDirty(destruction.Profile);
                PrefabUtility.SaveAsPrefabAsset(root,ShipPath);AssetDatabase.SaveAssets();
                return sectionId+": "+fragments.Length+" grain-aligned fragments";
            }
            finally
            {
                if(staging!=null)UnityEngine.Object.DestroyImmediate(staging);
                if(compressed!=null)UnityEngine.Object.DestroyImmediate(compressed);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
