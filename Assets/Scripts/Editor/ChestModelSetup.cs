using System.IO;
using System.Linq;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ChestModelSetup
    {
        const string Folder = "Assets/Models/Loot/Chest/";
        const string BaseFile = "tripo_convert_f24c7824-0801-40ca-a4a6-5177deaa3702";
        const string LidFile = "tripo_convert_fc547d5f-a403-4e58-9507-d859a80e5520";

        [MenuItem("PirateSlop/Configure Chest Model")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            var bodyMaterial = ConfigureMaterial("ChestBase", BaseFile, "основа_сундука");
            var lidMaterial = ConfigureMaterial("ChestLid", LidFile, "крышка_сундука");
            const string path = "Assets/Prefabs/Networking/NetworkLootChest.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var child in root.transform.Cast<Transform>().ToArray())
                    if (new[] { "Bottom", "Side", "Wall", "Band", "Lid", "ChestVisual" }.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
                var visual = new GameObject("ChestVisual").transform;
                visual.SetParent(root.transform, false);
                var body = AddModel(visual, "ChestBase", BaseFile, bodyMaterial, 1.2f);
                var bodyBounds = Bounds(body);
                body.transform.localPosition -= new Vector3(bodyBounds.center.x, bodyBounds.min.y, bodyBounds.center.z);
                bodyBounds = Bounds(body);
                var lid = AddModel(visual, "ChestLid", LidFile, lidMaterial, bodyBounds.size.x);
                var hinge = new GameObject("LidHinge").transform;
                hinge.SetParent(visual, false);
                lid.transform.SetParent(hinge, true);
                var box = root.GetComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(bodyBounds.center);
                box.size = bodyBounds.size;
                var chest = root.GetComponent<NetworkLootChest>();
                chest.Lid = hinge;
                chest.LidOpenSeconds = 1.1f;
                FitLid(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var bank = Resources.Load<GameAudioBank>("GameAudioBank");
            var entries = bank.Entries.ToList();
            var creak = entries.FirstOrDefault(e => e.Cue == SoundCue.ChestLidCreak);
            if (creak == null) { creak = new GameAudioBank.Entry { Cue = SoundCue.ChestLidCreak }; entries.Add(creak); }
            creak.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambience/Sea/ShipCreakA.mp3") };
            creak.Volume = .5f;
            creak.Distance = 12f;
            bank.Entries = entries.ToArray();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssetIfDirty(bank);
        }

        [MenuItem("PirateSlop/Art/Repair Chest Lid Fit")]
        public static void RepairLidFit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            const string path = "Assets/Prefabs/Networking/NetworkLootChest.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                FitLid(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void FitLid(GameObject root)
        {
            var visual = root.transform.Find("ChestVisual");
            var body = visual.Find("ChestBase");
            var hinge = visual.Find("LidHinge");
            var lid = hinge.Find("ChestLid");
            var importers = new[] { body, lid }.Select(t => (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t.GetComponent<MeshFilter>().sharedMesh))).ToArray();
            var readable = importers.Select(i => i.isReadable).ToArray();
            try
            {
                foreach (var importer in importers)
                    if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
                hinge.localRotation = Quaternion.identity;
                lid.SetParent(visual, true);
                var bodyPoints = Points(body, visual);
                var bodyBounds = PointBounds(bodyPoints);
                var rim = bodyPoints.Where(p => p.y >= bodyBounds.min.y + bodyBounds.size.y * .83f && p.y <= bodyBounds.min.y + bodyBounds.size.y * .97f).ToArray();
                if (rim.Length < 8) throw new System.InvalidOperationException("Chest rim geometry is missing.");
                var rimBounds = PointBounds(rim);
                var lidBounds = PointBounds(Points(lid, visual));
                float xRatio = rimBounds.size.x / lidBounds.size.x;
                float zRatio = rimBounds.size.z / lidBounds.size.z;
                float Factor(Vector3 axis)
                {
                    var direction = visual.InverseTransformDirection(axis);
                    return Mathf.Abs(direction.x) * xRatio + Mathf.Abs(direction.y) + Mathf.Abs(direction.z) * zRatio;
                }
                lid.localScale = Vector3.Scale(lid.localScale, new Vector3(Factor(lid.right), Factor(lid.up), Factor(lid.forward)));
                var lidPoints = Points(lid, visual);
                lidBounds = PointBounds(lidPoints);
                var panel = lidPoints.Where(p => Mathf.Abs(p.x - lidBounds.center.x) < lidBounds.extents.x * .65f && Mathf.Abs(p.z - lidBounds.center.z) < lidBounds.extents.z * .65f).Select(p => p.y).OrderBy(y => y).ToArray();
                if (panel.Length < 8) throw new System.InvalidOperationException("Chest lid panel geometry is missing.");
                var rimHeights = rim.Select(p => p.y).OrderBy(y => y).ToArray();
                float seat = rimHeights[Mathf.FloorToInt((rimHeights.Length - 1) * .75f)] - .004f;
                float underside = panel[Mathf.FloorToInt((panel.Length - 1) * .05f)];
                lid.localPosition += new Vector3(rimBounds.center.x - lidBounds.center.x, seat - underside, rimBounds.center.z - lidBounds.center.z);
                hinge.localPosition = new Vector3(rimBounds.center.x, seat, rimBounds.min.z + .012f);
                hinge.localRotation = Quaternion.identity;
                lid.SetParent(hinge, true);
                var box = hinge.GetComponent<BoxCollider>();
                if (box == null) box = hinge.gameObject.AddComponent<BoxCollider>();
                var closedBounds = PointBounds(Points(lid, hinge));
                box.center = closedBounds.center;
                box.size = closedBounds.size;
                var chest = root.GetComponent<NetworkLootChest>();
                chest.Lid = hinge;
                chest.LidOpenAngle = -105f;
            }
            finally
            {
                if (lid.parent != hinge) lid.SetParent(hinge, true);
                for (int i = 0; i < importers.Length; i++)
                    if (importers[i].isReadable != readable[i]) { importers[i].isReadable = readable[i]; importers[i].SaveAndReimport(); }
            }
        }

        static Vector3[] Points(Transform model, Transform anchor) => model.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices.Select(v => anchor.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();

        static Bounds PointBounds(Vector3[] points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points.Skip(1)) bounds.Encapsulate(point);
            return bounds;
        }

        static Material ConfigureMaterial(string name, string file, string prefix)
        {
            string directory = Folder + name + "/";
            string textures = directory + file + ".fbm/";
            string basePath = textures + prefix + "_basecolor.JPEG";
            string normalPath = textures + prefix + "_normal.PNG";
            ConfigureTexture(basePath, false, true, false, 2048);
            ConfigureTexture(normalPath, true, false, false, 1024);
            string metallicPath = textures + prefix + "_metallic.JPEG";
            string roughnessPath = textures + prefix + "_roughness.JPEG";
            ConfigureTexture(metallicPath, false, false, true, 1024);
            ConfigureTexture(roughnessPath, false, false, true, 1024);
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);
            var packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
            try
            {
                var pixels = metallic.GetPixels();
                for (int y = 0; y < packed.height; y++)
                    for (int x = 0; x < packed.width; x++)
                    {
                        int index = y * packed.width + x;
                        float smoothness = 1f - roughness.GetPixelBilinear((x + .5f) / packed.width, (y + .5f) / packed.height).r;
                        pixels[index] = new Color(pixels[index].r, 0f, 0f, smoothness);
                    }
                packed.SetPixels(pixels);
                packed.Apply();
                string packedPath = directory + name + "MetalSmooth.png";
                File.WriteAllBytes(packedPath, packed.EncodeToPNG());
                AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureTexture(packedPath, false, false, false, 1024);
                string materialPath = directory + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                return material;
            }
            finally
            {
                Object.DestroyImmediate(packed);
                ConfigureTexture(metallicPath, false, false, false, 1024);
                ConfigureTexture(roughnessPath, false, false, false, 1024);
            }
        }

        static void ConfigureTexture(string path, bool normal, bool srgb, bool readable, int size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.isReadable = readable;
            importer.maxTextureSize = size;
            importer.mipmapEnabled = importer.streamingMipmaps = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static GameObject AddModel(Transform parent, string name, string file, Material material, float width)
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + "/" + file + ".fbx"), parent);
            model.name = name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale *= width / Bounds(model).size.x;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
            return model;
        }

        static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
