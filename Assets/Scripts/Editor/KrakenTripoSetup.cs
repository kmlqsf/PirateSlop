using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class KrakenTripoSetup
    {
        const string Folder="Assets/Models/Kraken/";

        public static string Configure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Configure Kraken in Edit Mode.");
            string modelPath="Assets/Models/KrakenTentacles.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.isReadable=true;
            importer.optimizeGameObjects=false;
            importer.optimizeBones=false;
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.importAnimation=false;
            importer.importCameras=false;
            importer.importLights=false;
            importer.SaveAndReimport();
            Texture(Folder+"KrakenBaseColor.jpg",false,true);
            Texture(Folder+"KrakenNormal.png",true,false);
            Texture(Folder+"KrakenMetalSmooth.png",false,false);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"KrakenTripoSkin.mat");
            if(material==null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material,Folder+"KrakenTripoSkin.mat");
            }
            material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"KrakenBaseColor.jpg"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"KrakenNormal.png"));
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"KrakenMetalSmooth.png"));
            material.SetFloat("_Smoothness",1);
            material.SetFloat("_Metallic",1);
            material.SetFloat("_BumpScale",1);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            var root=new GameObject("KrakenTentacle");
            try
            {
                var geometry=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                geometry.transform.SetParent(root.transform,false);
                var renderer=geometry.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.sharedMaterial=material;
                renderer.updateWhenOffscreen=true;
                renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*80);
                renderer.quality=SkinQuality.Bone2;
                var bones=geometry.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("TentacleBone_",StringComparison.Ordinal)).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray();
                if(bones.Length!=17) throw new InvalidOperationException("Expected 17 ordered tentacle bones.");
                renderer.rootBone=bones[0];
                var animator=root.AddComponent<TentacleAnimator>();
                var animationData=new SerializedObject(animator);
                animationData.FindProperty("meshRenderer").objectReferenceValue=renderer;
                var list=animationData.FindProperty("bones");
                list.arraySize=bones.Length;
                for(int i=0;i<bones.Length;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=bones[i];
                animationData.ApplyModifiedPropertiesWithoutUndo();
                var tentacle=root.AddComponent<KrakenTentacle>();
                var tentacleData=new SerializedObject(tentacle);
                tentacleData.FindProperty("animator").objectReferenceValue=animator;
                tentacleData.ApplyModifiedPropertiesWithoutUndo();
                for(int i=0;i<16;i+=2)
                {
                    var collider=bones[i].gameObject.AddComponent<CapsuleCollider>();
                    collider.direction=1;
                    collider.radius=Mathf.Lerp(2.9f,.2f,i/16f);
                    collider.height=3+collider.radius*2;
                    collider.center=new Vector3(0,1.5f,0);
                }
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/KrakenTentacle.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var encounter=PrefabUtility.LoadPrefabContents("Assets/Resources/KrakenEncounter.prefab");
            try
            {
                var data=new SerializedObject(encounter.GetComponent<KrakenEncounter>());
                data.FindProperty("tentaclePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/KrakenTentacle.prefab");
                data.FindProperty("attackSystem").objectReferenceValue=encounter.GetComponent<KrakenAttackSystem>();
                data.FindProperty("sinkSpeed").floatValue=8;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(encounter,"Assets/Resources/KrakenEncounter.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(encounter); }
            var ship=PrefabUtility.LoadPrefabContents("Assets/Resources/Ships/ShipV3Test.prefab");
            try
            {
                var manager=ship.GetComponent<KrakenEncounterManager>() ?? ship.AddComponent<KrakenEncounterManager>();
                var data=new SerializedObject(manager);
                data.FindProperty("encounterPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/KrakenEncounter.prefab");
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(ship,"Assets/Resources/Ships/ShipV3Test.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(ship); }
            AssetDatabase.SaveAssets();
            return "Kraken model, material, ordered bones, collisions and both existing prefab GUIDs saved.";
        }

        static void Texture(string path,bool normal,bool srgb)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture=srgb;
            importer.maxTextureSize=4096;
            importer.SaveAndReimport();
        }
    }
}
