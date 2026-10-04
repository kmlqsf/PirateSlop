using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static class BottleFishReplacementSetup
    {
        const string Folder = "Assets/Models/Loot/Replacement/";

        public static void ConfigureBottles()
        {
            RequireEditMode();
            string directory = Folder + "WhiskyBottle/";
            ImportModel(directory + "WhiskyBottleEmpty.fbx");
            ImportTexture(directory + "CorkBaseColor.png", false, true, 1024);
            ImportTexture(directory + "CorkNormal.png", true, false, 1024);
            var glass = Material(directory + "WhiskyGlass.mat", "Universal Render Pipeline/Lit");
            glass.SetColor("_BaseColor", new Color(.9f, .97f, 1f, .12f));
            glass.SetFloat("_Smoothness", .92f);
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f);
            glass.SetFloat("_Cull", (float)CullMode.Back);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.SetShaderPassEnabled("ShadowCaster", false);
            glass.SetFloat("_QueueOffset", 10f);
            glass.renderQueue = 3010;
            EditorUtility.SetDirty(glass);
            var cork = Material(directory + "WhiskyCork.mat", "Universal Render Pipeline/Lit");
            cork.SetColor("_BaseColor", Color.white);
            cork.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "CorkBaseColor.png"));
            cork.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "CorkNormal.png"));
            cork.SetFloat("_Smoothness", .18f);
            cork.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(cork);
            var swirl = Material(directory + "WhiskySwirl.mat", "PirateSlop/BottleVortex");
            swirl.SetColor("_BaseColor", new Color(.05f, .38f, 1f, .85f));
            swirl.SetFloat("_EmissionStrength", 3.5f);
            swirl.renderQueue = 3000;
            EditorUtility.SetDirty(swirl);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(directory + "WhiskyBottleEmpty.fbx");
            foreach (string key in new[] { "FogBottle", "VortexBottle" })
                Edit("Assets/Prefabs/Loot/" + key + ".prefab", root =>
                {
                    ClearChildren(root.transform);
                    var bottle = new GameObject("Bottle");
                    bottle.transform.SetParent(root.transform, false);
                    bottle.transform.localPosition = new Vector3(0, -.28f, 0);
                    var geometry = (GameObject)PrefabUtility.InstantiatePrefab(source, bottle.transform);
                    geometry.name = "WhiskyBottleEmpty";
                    foreach (var renderer in geometry.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => renderer.name.StartsWith("Cork", StringComparison.Ordinal) ? cork : glass).ToArray();
                        if (!renderer.name.StartsWith("Cork", StringComparison.Ordinal))
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                        }
                    }
                    if (key == "FogBottle")
                    {
                        var mistObject = new GameObject("BottleMist");
                        mistObject.transform.SetParent(bottle.transform, false);
                        mistObject.transform.localPosition = new Vector3(0, .085f, 0);
                        var mist = mistObject.AddComponent<FogCloudVisual>();
                        mist.Radius = .09f;
                        mist.Height = .3f;
                        mist.Density = 16f;
                        mist.InsideBottle = true;
                        var visual = root.GetComponent<FogBottleVisual>() ?? root.AddComponent<FogBottleVisual>();
                        visual.Mist = mist;
                    }
                    else
                    {
                        var visual = bottle.AddComponent<VortexBottleVisual>();
                        visual.SwirlMaterial = swirl;
                        visual.RotationSpeed = 90f;
                    }
                });
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureBottleBreakAudio()
        {
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            var entries = bank.Entries.ToList();
            var entry = entries.FirstOrDefault(e => e.Cue == SoundCue.BottleBreak);
            if (entry == null) { entry = new GameAudioBank.Entry { Cue = SoundCue.BottleBreak }; entries.Add(entry); }
            entry.Clips = Enumerable.Range(1, 3).Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BottleBreak/BottleBreak0" + i + ".wav")).ToArray();
            entry.Volume = .75f; entry.Distance = 24;
            bank.Entries = entries.ToArray(); EditorUtility.SetDirty(bank);
            const string materialPath = "Assets/Resources/BottleGlassShard.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("PirateSlop/GlassShard")); AssetDatabase.CreateAsset(material, materialPath); }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureFish()
        {
            RequireEditMode();
            string[] keys = { "Fish", "Pufferfish", "Swordfish" };
            string[] paths = { "Assets/Models/Fishing/FishVisual.prefab", "Assets/Models/FishingWeapons/PufferfishVisual.prefab", "Assets/Models/FishingWeapons/SwordfishVisual.prefab" };
            float[] lengths = { .66f, .758f, 1.72f };
            Vector3[] centers = { new Vector3(0, .03f, -.05f), new Vector3(0, 0, -.051f), new Vector3(0, .06f, .16f) };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                string directory = Folder + key + "/";
                ImportModel(directory + key + ".fbx", true);
                ImportTexture(directory + key + "_basecolor.jpeg", false, true, 2048);
                ImportTexture(directory + key + "_normal.png", true, false, 1024);
                ImportTexture(directory + key + "MetalSmooth.png", false, false, 1024);
                var material = Material(directory + key + ".mat", "Universal Render Pipeline/Lit");
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "_basecolor.jpeg"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "_normal.png"));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + key + "MetalSmooth.png"));
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_Metallic", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                EditorUtility.SetDirty(material);
                int index = i;
                Edit(paths[i], root =>
                {
                    ClearChildren(root.transform);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(directory + key + ".fbx"), root.transform);
                    model.name = "Geometry";
                    model.transform.localRotation = Quaternion.Euler(0, key == "Fish" ? 90f : -90f, 0) * AssetDatabase.LoadAssetAtPath<GameObject>(directory + key + ".fbx").transform.localRotation;
                    foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
                    var bounds = Bounds(root);
                    float scale = lengths[index] / bounds.size.z;
                    model.transform.localScale *= scale;
                    model.transform.localPosition = centers[index] - bounds.center * scale;
                });
                if (i > 0)
                    Edit("Assets/Models/FishingWeapons/" + key + "Pickup.prefab", root =>
                    {
                        ClearChildren(root.transform);
                        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]), root.transform);
                        FitFishCollider(root);
                    });
            }
            Edit("Assets/Prefabs/Networking/NetworkFish.prefab", FitFishCollider);
            AssetDatabase.SaveAssets();
        }

        static void FitFishCollider(GameObject root)
        {
            var collider = root.GetComponent<BoxCollider>();
            var fish = root.GetComponent<PirateSlop.Networking.NetworkFish>();
            if (collider == null || fish == null) return;
            var bounds = PirateSlop.Networking.LootPlacement.VisualBounds(fish);
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        static void RequireEditMode()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        }

        static void ImportModel(string path, bool readable = false)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        static void ImportTexture(string path, bool normal, bool srgb, int size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = size;
            importer.isReadable = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Material Material(string path, string shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.enableInstancing = true;
            return material;
        }

        static void Edit(string path, Action<GameObject> change)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                change(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                typeof(UnityEditor.SceneManagement.EditorSceneManager)
                    .GetMethod("ClearTargetSceneForNewGameObjects", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(null, null);
            }
        }

        static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
