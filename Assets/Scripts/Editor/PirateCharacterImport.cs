using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class PirateCharacterImport
    {
        const string ModelPath = "Assets/Models/Characters/Pirate/PirateCharacter.fbx";
        const string MaterialsPath = "Assets/Materials/Characters/Pirate";
        const string PrefabPath = "Assets/Prefabs/Characters/PirateCharacter.prefab";
        [MenuItem("PirateSlop/Import Pirate Character")]
        public static void Configure()
        {
            EnsureFolder(MaterialsPath);
            EnsureFolder("Assets/Prefabs/Characters");
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "Root";
            importer.optimizeGameObjects = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var colors = new Dictionary<string, Color>
            {
                {"Skin",new Color(.58f,.29f,.145f)}, {"SkinLight",new Color(.70f,.38f,.20f)},
                {"Linen",new Color(.81f,.72f,.53f)}, {"Teal",new Color(.027f,.16f,.16f)},
                {"TealLight",new Color(.045f,.235f,.22f)}, {"Sash",new Color(.46f,.065f,.045f)},
                {"Leather",new Color(.115f,.047f,.023f)}, {"LeatherLight",new Color(.23f,.10f,.04f)},
                {"Hat",new Color(.052f,.062f,.067f)}, {"Hair",new Color(.075f,.032f,.018f)},
                {"Brass",new Color(.57f,.32f,.085f)}, {"Eye",new Color(.92f,.88f,.70f)},
                {"Pupil",new Color(.014f,.021f,.022f)}, {"Pants",new Color(.073f,.10f,.12f)}
            };
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is unavailable.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach (var source in model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
            {
                string key = source.name.Replace("Pirate_", "").Split('.')[0];
                if (!colors.TryGetValue(key, out Color color)) throw new InvalidOperationException("Unknown pirate material: " + source.name);
                string path = MaterialsPath + "/Pirate_" + key + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
                material.shader = shader;
                // Blender stores linear RGB; Unity material color properties use sRGB.
                material.SetColor("_BaseColor", color.gamma);
                material.SetFloat("_Smoothness", .22f);
                material.SetFloat("_Metallic", key == "Brass" ? .5f : 0f);
                material.SetFloat("_Cull", 0f);
                EditorUtility.SetDirty(material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),material);
            }
            importer.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "PirateCharacter";
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            Bounds bounds = renderers[0].bounds;
            foreach(var r in renderers) { bounds.Encapsulate(r.bounds); r.updateWhenOffscreen = false; }
            if (bounds.size.y < 1.8f || bounds.size.y > 2.1f) throw new InvalidOperationException("Unexpected character scale: " + bounds.size);
            if (renderers.Any(r => r.sharedMaterials.Any(m => m == null || m.shader != shader))) throw new InvalidOperationException("Missing character materials.");
            PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath);
            string report = "Pirate character imported: meshes="+renderers.Length+", size="+bounds.size+", avatar="+(avatar != null && avatar.isValid)+", materials=URP/Lit; prefab="+PrefabPath;
            Debug.Log(report);
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
        }
        static void EnsureFolder(string path)
        {
            string parent = "Assets";
            foreach(string part in path.Split('/').Skip(1))
            {
                string child = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent,part);
                parent = child;
            }
        }
    }
}
