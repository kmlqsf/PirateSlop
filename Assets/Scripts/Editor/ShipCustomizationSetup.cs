using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PirateSlop.Customization;

namespace PirateSlop.EditorTools
{
    public static class ShipCustomizationSetup
    {
        const string ShipPath = "Assets/Resources/Ships/ShipV3Test.prefab";
        const string MenuPath = "Assets/Resources/Ships/ShipV3Menu.prefab";

        [Serializable]
        sealed class GlyphMetadata
        {
            public int codepoint;
            public string meshName;
            public float advance;
        }

        [Serializable]
        sealed class KerningMetadata
        {
            public int left, right;
            public float offset;
        }

        [Serializable]
        sealed class GlyphMetadataSet
        {
            public float spaceAdvance;
            public GlyphMetadata[] glyphs;
            public KerningMetadata[] kerning;
        }

        static ShipNameGlyphLibrary ConfigureGlyphs(Material brass)
        {
            const string folder = "Assets/Models/Ships/ShipNameplate";
            const string libraryPath = "Assets/Resources/Customization/ShipNameGlyphs.asset";
            var data = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/ShipNameGlyphs.json");
            if (data == null) throw new InvalidOperationException("Ship name glyph metadata is missing.");
            var metadata = JsonUtility.FromJson<GlyphMetadataSet>(data.text);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string facePath = folder + "/Materials/ShipNameGlyphFace.mat";
            var face = AssetDatabase.LoadAssetAtPath<Material>(facePath);
            if (face == null)
            {
                face = new Material(shader) { name = "ShipNameGlyphFace" };
                AssetDatabase.CreateAsset(face, facePath);
            }
            face.SetColor("_BaseColor", new Color(.82f, .64f, .32f));
            face.SetFloat("_Metallic", .15f);
            face.SetFloat("_Smoothness", .25f);
            face.SetFloat("_Surface", 0f);
            face.SetFloat("_ZWrite", 1f);
            face.SetFloat("_Cull", 2f);
            face.renderQueue = -1;
            EditorUtility.SetDirty(face);
            string rimPath = folder + "/Materials/ShipNameGlyphRim.mat";
            var rim = AssetDatabase.LoadAssetAtPath<Material>(rimPath);
            if (rim == null)
            {
                rim = new Material(brass) { name = "ShipNameGlyphRim" };
                AssetDatabase.CreateAsset(rim, rimPath);
            }
            rim.SetColor("_BaseColor", new Color(.95f, .74f, .35f));
            rim.SetFloat("_Metallic", .35f);
            rim.SetFloat("_Smoothness", .32f);
            rim.SetFloat("_Surface", 0f);
            rim.SetFloat("_ZWrite", 1f);
            rim.SetFloat("_Cull", 2f);
            rim.renderQueue = -1;
            EditorUtility.SetDirty(rim);
            var importer = (ModelImporter)AssetImporter.GetAtPath(folder + "/ShipNameGlyphs.fbx");
            if (importer == null) throw new InvalidOperationException("Ship name glyph model is missing.");
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ShipNameGlyphFace"), face);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ShipNameplateBrass"), rim);
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/ShipNameGlyphs.fbx");
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            var glyphs = new System.Collections.Generic.List<ShipNameGlyphLibrary.Glyph>();
            foreach (var glyph in metadata.glyphs)
            {
                if (glyph.codepoint == ' ') continue;
                var filter = Array.Find(filters, item => item.name == glyph.meshName);
                if (filter == null) throw new InvalidOperationException("Missing glyph " + glyph.meshName);
                glyphs.Add(new ShipNameGlyphLibrary.Glyph { Codepoint = glyph.codepoint, Advance = glyph.advance, Mesh = filter.sharedMesh });
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Customization")) AssetDatabase.CreateFolder("Assets/Resources", "Customization");
            var library = AssetDatabase.LoadAssetAtPath<ShipNameGlyphLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ShipNameGlyphLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }
            library.Glyphs = glyphs.ToArray();
            library.Pairs = metadata.kerning == null ? Array.Empty<ShipNameGlyphLibrary.Kerning>() : Array.ConvertAll(metadata.kerning,
                pair => new ShipNameGlyphLibrary.Kerning { Left = pair.left, Right = pair.right, Offset = pair.offset });
            library.SpaceAdvance = metadata.spaceAdvance;
            library.Materials = new[] { face, rim };
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            return library;
        }

        public static void Configure(GameObject root)
        {
            if (root.GetComponent<SailCustomizer>() == null) root.AddComponent<SailCustomizer>();
            if (root.GetComponent<ShipNameplate>() == null) root.AddComponent<ShipNameplate>();
            if (root.GetComponent<FishNet.Object.NetworkObject>() != null && root.GetComponent<SailNetworkSync>() == null)
                root.AddComponent<SailNetworkSync>();
            ConfigureNameplate(root);
        }

        static void ConfigureNameplate(GameObject root)
        {
            const string folder = "Assets/Models/Ships/ShipNameplate";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/ShipNameplate.fbx");
            if (model == null) return;
            if (!AssetDatabase.IsValidFolder(folder + "/Materials")) AssetDatabase.CreateFolder(folder, "Materials");
            var woodSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/MainShip/Materials/Mat_StylShip_Masts.mat");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string[] names = { "ShipNameplateWood", "ShipNameplateWoodEdge", "ShipNameplateBrass", "ShipNameplateBrassHighlight" };
            var materials = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (string name in names)
            {
                string path = folder + "/Materials/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = name.Contains("Wood") && woodSource != null ? new Material(woodSource) : new Material(shader);
                    material.name = name;
                    AssetDatabase.CreateAsset(material, path);
                }
                if (name.Contains("Wood"))
                {
                    if (woodSource != null) material.CopyPropertiesFromMaterial(woodSource);
                    material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Ships/MainShip/Textures/StylShip_Masts_AmbientOcclusion.png"));
                    material.SetColor("_BaseColor", name.EndsWith("Edge") ? new Color(.32f, .25f, .18f) : new Color(.55f, .43f, .3f));
                    material.SetFloat("_Smoothness", .18f);
                }
                else
                {
                    material.SetColor("_BaseColor", name.EndsWith("Highlight") ? new Color(.77f, .59f, .25f) : new Color(.57f, .39f, .13f));
                    material.SetFloat("_Metallic", .75f);
                    material.SetFloat("_Smoothness", .38f);
                }
                EditorUtility.SetDirty(material);
                materials[name] = material;
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(folder + "/ShipNameplate.fbx");
            foreach (var pair in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.importAnimation = false;
            importer.SaveAndReimport();
            var board = root.transform.Find("ShipNameplate");
            if (board == null)
            {
                board = new GameObject("ShipNameplate").transform;
                board.SetParent(root.transform, false);
                var geometry = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
                geometry.transform.SetParent(board, false);
            }
            board.localPosition = new Vector3(0, 6.44f, -20.2f);
            board.localRotation = Quaternion.Euler(0f, 180f, 0f);
            board.localScale = Vector3.one;
            foreach (var collider in board.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var oldLabel = board.Find("ShipNameLabel");
            if (oldLabel != null) UnityEngine.Object.DestroyImmediate(oldLabel.gameObject);
            var label = board.Find("ShipNameLetters");
            if (label == null) { label = new GameObject("ShipNameLetters").transform; label.SetParent(board, false); }
            var field = Array.Find(board.GetComponentsInChildren<MeshFilter>(true), filter => filter.name == "ShipNameplateNameField");
            var bounds = field.sharedMesh.bounds;
            var surface = board.InverseTransformPoint(field.transform.TransformPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.max.z)));
            label.localPosition = surface + Vector3.forward * .0006f;
            label.localRotation = Quaternion.Euler(0f, 180f, 0f);
            label.localScale = Vector3.one;
            var nameplate = root.GetComponent<ShipNameplate>();
            nameplate.NameAnchor = label;
            nameplate.GlyphLibrary = ConfigureGlyphs(materials["ShipNameplateBrass"]);
            nameplate.TextWidth = 4.2f;
            nameplate.TextHeight = .60f;
        }

        [MenuItem("PirateSlop/Restore Ship V3 Customization")]
        public static void Restore()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            CreateMenuPrefab();
        }

        public static void CreateMenuPrefab()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath);
                root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                root.SetActive(false);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var sourceNameplate = root.GetComponent<ShipNameplate>();
                var nameAnchor = sourceNameplate.NameAnchor;
                var glyphLibrary = sourceNameplate.GlyphLibrary;
                float textWidth = sourceNameplate.TextWidth;
                float textHeight = sourceNameplate.TextHeight;
                var scripts = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(scripts[i]);
                foreach (var joint in root.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(joint);
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
                root.name = "ShipV3Menu";
                root.AddComponent<SailCustomizer>();
                var nameplate = root.AddComponent<ShipNameplate>();
                nameplate.NameAnchor = nameAnchor;
                nameplate.GlyphLibrary = glyphLibrary;
                nameplate.TextWidth = textWidth;
                nameplate.TextHeight = textHeight;
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, MenuPath);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
