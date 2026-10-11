using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class SharkTripoSetup
    {
        const string Folder="Assets/Models/Shark/";

        [MenuItem("PirateSlop/Sea Events/Replace Shark With Tripo Model")]
        public static void Configure()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"SharkTripo.fbx");
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true;
            importer.optimizeGameObjects=false;
            importer.importCameras=false; importer.importLights=false;
            importer.SaveAndReimport();
            var definitions=importer.defaultClipAnimations;
            foreach(var clip in definitions)
            {
                clip.loopTime=clip.name.Contains("Swim"); clip.loopPose=clip.loopTime;
                clip.lockRootPositionXZ=true; clip.lockRootHeightY=true; clip.lockRootRotation=true;
            }
            importer.clipAnimations=definitions; importer.SaveAndReimport();
            Texture(Folder+"SharkBaseColor.jpg",false,true);
            Texture(Folder+"SharkNormal.png",true,false);
            var metal=Texture(Folder+"SharkMetallic.jpg",false,false,true);
            var rough=Texture(Folder+"SharkRoughness.jpg",false,false,true);
            var pixels=metal.GetPixels32(); var roughness=rough.GetPixels32();
            if(metal.width!=rough.width || metal.height!=rough.height) throw new InvalidOperationException("Shark material maps have different sizes.");
            for(int i=0;i<pixels.Length;i++) pixels[i]=new Color32(pixels[i].r,0,0,(byte)(255-roughness[i].r));
            var packed=new Texture2D(metal.width,metal.height,TextureFormat.RGBA32,false,true); packed.SetPixels32(pixels); packed.Apply();
            File.WriteAllBytes(Folder+"SharkMetalSmooth.png",packed.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(Folder+"SharkMetalSmooth.png",ImportAssetOptions.ForceSynchronousImport);
            Texture(Folder+"SharkMetalSmooth.png",false,false);
            Texture(Folder+"SharkMetallic.jpg",false,false); Texture(Folder+"SharkRoughness.jpg",false,false);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"SharkTripoSkin.mat");
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,Folder+"SharkTripoSkin.mat"); }
            material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"SharkBaseColor.jpg"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"SharkNormal.png"));
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"SharkMetalSmooth.png"));
            material.SetFloat("_Smoothness",1); material.SetFloat("_Metallic",1); material.SetFloat("_BumpScale",1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); EditorUtility.SetDirty(material);
            var clips=AssetDatabase.LoadAllAssetsAtPath(Folder+"SharkTripo.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"Shark.controller");
            foreach(var state in controller.layers[0].stateMachine.states)
                state.state.motion=clips.First(c=>c.name.Contains(state.state.name));
            EditorUtility.SetDirty(controller);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"SharkTripo.fbx"));
            try
            {
                PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                root.name="SharkVisual";
                var animator=root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
                animator.runtimeAnimatorController=controller; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.sharedMaterial=material; renderer.quality=SkinQuality.Bone4;
                    renderer.localBounds=new Bounds(renderer.sharedMesh.bounds.center,renderer.sharedMesh.bounds.size+Vector3.one*1.2f);
                }
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/SharkVisual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssetIfDirty(material); AssetDatabase.SaveAssetIfDirty(controller);
        }

        static Texture2D Texture(string path,bool normal,bool srgb,bool readable=false)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture=srgb; importer.isReadable=readable; importer.maxTextureSize=2048;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
