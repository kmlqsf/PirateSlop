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
            board.localRotation = Quaternion.identity;
            board.localScale = Vector3.one;
            foreach (var collider in board.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var label = board.Find("ShipNameLabel");
            if (label == null) { label = new GameObject("ShipNameLabel").transform; label.SetParent(board, false); }
            label.localPosition = new Vector3(0, 0, -.04f);
            label.localRotation = Quaternion.identity;
            var text = label.GetComponent<TextMesh>();
            if (text == null) text = label.gameObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 128;
            text.characterSize = .08f;
            text.color = new Color(.94f, .84f, .57f);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.richText = false;
            text.text = "";
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = text.font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var nameplate = root.GetComponent<ShipNameplate>();
            nameplate.Label = text;
            nameplate.TextWidth = 4.1f;
            nameplate.TextHeight = .5f;
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
                var scripts = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(scripts[i]);
                foreach (var joint in root.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(joint);
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
                root.name = "ShipV3Menu";
                root.AddComponent<SailCustomizer>();
                var nameplate = root.AddComponent<ShipNameplate>();
                var text = Array.Find(root.GetComponentsInChildren<TextMesh>(true), label => label.name == "ShipNameLabel");
                nameplate.Label = text;
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
