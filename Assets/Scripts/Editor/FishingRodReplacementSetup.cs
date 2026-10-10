using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class FishingRodReplacementSetup
    {
        const string ModelPath = "Assets/Models/Fishing/Replacement/FishingRodReplacement.fbx";
        const string TextureFolder = "Assets/Models/Fishing/Replacement/Textures/";
        const string RodPath = "Assets/Models/Fishing/FishingRod.prefab";
        const string DropPath = "Assets/Prefabs/Networking/DroppedRod.prefab";

        [MenuItem("PirateSlop/Replace Fishing Rod Model")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) return;
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            Directory.CreateDirectory("Assets/Materials/Fishing");
            AssetDatabase.Refresh();
            var material = Pbr("FishingRodReplacement", "FishingRod");
            var mount = Pbr("ReelMount", "ReelMount");
            var spool = Pbr("ReelMechanism", "ReelMechanism");
            var brass = Plain("ReelBrass", new Color(.37f, .21f, .075f), .7f, .34f);
            var line = Plain("ReelThread", new Color(.43f, .48f, .42f), 0f, .8f);
            var grip = Plain("ReelGrip", new Color(.075f, .026f, .012f), 0f, .6f);
            Replace(RodPath, model, material, mount, spool, brass, line, grip, false);
            Replace(DropPath, model, material, mount, spool, brass, line, grip, true);
            SetLine("Assets/Prefabs/Networking/NetworkPlayer.prefab", line, false);
            SetLine("Assets/Resources/Ships/ShipV3Test.prefab", line, true);
            AssetDatabase.SaveAssets();
        }

        static Material Pbr(string name, string prefix)
        {
            Texture(prefix + "Normal.png", true);
            Texture(prefix + "MetalSmooth.png", false);
            string path = "Assets/Materials/Fishing/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + prefix + "BaseColor.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + prefix + "Normal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + prefix + "MetalSmooth.png"));
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Plain(string name, Color color, float metal, float roughness)
        {
            string path = "Assets/Materials/Fishing/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", 1f - roughness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void SetLine(string path, Material line, bool ship)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ship) root.GetComponent<Ships.ShipMonkey>().FishingLineMaterial = line;
                else root.GetComponent<Networking.NetworkFishing>().LineMaterial = line;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Texture(string name, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TextureFolder + name);
            if (normal) importer.textureType = TextureImporterType.NormalMap;
            else importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }

        static void Replace(string path, GameObject model, Material material, Material mount, Material spool, Material brass, Material line, Material grip, bool pickup)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visual = pickup ? root.transform.Find("Visual") : root.transform;
                if (visual == null) throw new System.InvalidOperationException("Missing rod visual: " + path);
                foreach (Transform child in visual.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var geometry = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
                geometry.name = "FishingRodGeometry";
                foreach (var renderer in geometry.GetComponentsInChildren<Renderer>(true))
                {
                    var selected = renderer.name.StartsWith("ReelMount") ? mount : renderer.name.StartsWith("ReelSpool") ? spool :
                        renderer.name.StartsWith("ReelWinding") ? line : renderer.name.StartsWith("ReelCrankGrip") ? grip :
                        renderer.name.StartsWith("ReelCrank") ? brass : material;
                    renderer.sharedMaterials = Enumerable.Repeat(selected, renderer.sharedMaterials.Length).ToArray();
                }
                var reel = visual.GetComponent<FishingRodReel>();
                if (reel == null) reel = visual.gameObject.AddComponent<FishingRodReel>();
                reel.LineMaterial = line;
                if (!pickup)
                {
                    var bend = root.GetComponent<FishingRodBend>();
                    if (bend == null) bend = root.AddComponent<FishingRodBend>();
                    bend.TipLocalPoint = new Vector3(0f, .133f, 1.69f);
                }
                if (pickup)
                {
                    var points = geometry.GetComponentsInChildren<MeshFilter>(true)
                        .SelectMany(filter => filter.sharedMesh.vertices.Select(vertex => root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)))).ToArray();
                    var bounds = new Bounds(points[0], Vector3.zero);
                    foreach (var point in points) bounds.Encapsulate(point);
                    var box = root.GetComponent<BoxCollider>();
                    box.center = bounds.center;
                    box.size = Vector3.Max(bounds.size, Vector3.one * .03f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
