using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class CoastalEnvironmentSetup
    {
        const string Models = "Assets/Models/World/CoastalEnvironment";
        const string Materials = "Assets/Materials/CoastalEnvironment";
        const string Gallery = "Assets/Resources/EnvironmentTest/Gallery.prefab";
        const float GalleryGap = 30f;
        static readonly string[] Environment = { "Sea_Lagoon_Cave", "Reef_Moai_A", "Reef_Spires_A", "Reef_Spires_B", "Reef_Spires_C", "SeaArch_Huge_A", "Reef_ShallowField_A" };
        static readonly string[] Starter = { "RockLarge", "RockMedium", "CliffWallA", "CliffWallB", "CliffWallC", "Palm", "PalmBent", "PalmYoung", "Bush", "Fern" };

        public static bool IsPrepared => Environment.All(n => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/" + n + ".prefab")?.GetComponentInChildren<CoastalRockVisual>(true) != null);

        [MenuItem("PirateSlop/Art/Apply Coastal Environment C")]
        public static void Apply() => ApplyModels(Environment.Concat(Starter).ToArray());

        public static void ApplyModels(params string[] names)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before updating environment art.");
            if (names.Length == 0 || names.Any(n => !Environment.Contains(n) && !Starter.Contains(n))) throw new ArgumentException("Unknown coastal model.");
            foreach (var name in names.Where(IsRock)) PrepareCollisionSource(name);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            var materials = PrepareMaterials();
            foreach (var name in names.Distinct())
            {
                string path = (Environment.Contains(name) ? "Assets/Prefabs/Environment/" : "Assets/Prefabs/World/StarterIsland/") + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var oldVisual = root.transform.Find("CoastalVisual");
                    if (oldVisual != null) UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    var visual = new GameObject("CoastalVisual");
                    visual.transform.SetParent(root.transform, false);
                    var marker = visual.AddComponent<CoastalRockVisual>();
                    var levels = new List<LOD>();
                    for (int level = 0; level < 3; level++)
                    {
                        string modelPath = Models + "/" + name + "_LOD" + level + ".fbx";
                        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                        if (importer == null) throw new InvalidOperationException("Missing model " + modelPath);
                        bool changed = importer.importAnimation || importer.animationType != ModelImporterAnimationType.None || importer.importCameras || importer.importLights || importer.addCollider || importer.isReadable != (level == 0) || importer.meshCompression != ModelImporterMeshCompression.Off || importer.importTangents != ModelImporterTangents.CalculateMikk;
                        importer.importAnimation = false;
                        importer.animationType = ModelImporterAnimationType.None;
                        importer.importCameras = false;
                        importer.importLights = false;
                        importer.addCollider = false;
                        importer.isReadable = level == 0;
                        importer.meshCompression = ModelImporterMeshCompression.Off;
                        importer.importTangents = ModelImporterTangents.CalculateMikk;
                        changed |= importer.importNormals != ModelImporterNormals.Calculate || importer.normalCalculationMode != ModelImporterNormalCalculationMode.AreaAndAngleWeighted || importer.normalSmoothingSource != ModelImporterNormalSmoothingSource.FromAngle || importer.normalSmoothingAngle != 65f;
                        importer.importNormals = ModelImporterNormals.Calculate;
                        importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
                        importer.normalSmoothingSource = ModelImporterNormalSmoothingSource.FromAngle;
                        importer.normalSmoothingAngle = 65f;
                        if (changed) importer.SaveAndReimport();
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual.transform);
                        instance.name = "LOD" + level;
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                            {
                                if (m == null) throw new InvalidOperationException(modelPath + " has a null material.");
                                string key = m.name.Split('.')[0];
                                if (!materials.TryGetValue(key, out var material)) throw new InvalidOperationException("Unknown coastal material " + m.name);
                                return material;
                            }).ToArray();
                            renderer.shadowCastingMode = level == 2 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                            renderer.enabled = true;
                        }
                        levels.Add(new LOD(level == 0 ? .45f : level == 1 ? .16f : .015f, instance.GetComponentsInChildren<Renderer>(true)));
                    }
                    marker.Contours = name.StartsWith("Palm", StringComparison.Ordinal) || name == "Fern" || name == "Bush" ? Array.Empty<CoastalShoreContour>() : BuildContours(root, true);
                    var group = visual.AddComponent<LODGroup>();
                    group.SetLODs(levels.ToArray());
                    group.RecalculateBounds();
                    foreach (var node in visual.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = root.layer;
                    if (visual.GetComponentsInChildren<Collider>(true).Length > 0) throw new InvalidOperationException("New visuals must not contain colliders.");
                    if (IsRock(name)) ApplyCollision(root, name);
                    if (IsRock(name)) SeagullSetup.BuildPerches(visual);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var profile = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
            profile.TerrainMaterial = materials["CoastalTerrain"];
            EditorUtility.SetDirty(profile);
            foreach (var name in names.Distinct())
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Models + "/" + name + "_LOD0.fbx");
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            RefreshCatalogVersions();
        }

        static bool IsRock(string name) => !name.StartsWith("Palm", StringComparison.Ordinal) && name != "Bush" && name != "Fern";

        [MenuItem("PirateSlop/Art/Repair Coastal Surface Normals")]
        public static void RepairSurfaceNormals()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before updating environment art.");
            foreach (var name in Environment.Concat(Starter).Where(IsRock))
                for (int level = 0; level < 3; level++)
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(Models + "/" + name + "_LOD" + level + ".fbx");
                    if (importer == null) throw new InvalidOperationException("Missing coastal model " + name);
                    if (importer.importNormals == ModelImporterNormals.Calculate && importer.normalCalculationMode == ModelImporterNormalCalculationMode.AreaAndAngleWeighted && importer.normalSmoothingSource == ModelImporterNormalSmoothingSource.FromAngle && importer.normalSmoothingAngle == 65f) continue;
                    importer.importNormals = ModelImporterNormals.Calculate;
                    importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
                    importer.normalSmoothingSource = ModelImporterNormalSmoothingSource.FromAngle;
                    importer.normalSmoothingAngle = 65f;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.SaveAndReimport();
                }
        }

        [MenuItem("PirateSlop/Art/Repair Exact Coastal Collision")]
        public static void RepairExactCollision() => ApplyCollisionModels(Environment.Concat(Starter).Where(IsRock).ToArray());

        static void PrepareCollisionSource(string name)
        {
            string path = Models + "/" + name + "_LOD0.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing coastal visual model " + name);
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        }

        public static void ApplyCollisionModels(params string[] names)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before updating environment collision.");
            if (names.Length == 0 || names.Any(n => !IsRock(n) || !Environment.Contains(n) && !Starter.Contains(n))) throw new ArgumentException("Unknown coastal rock model.");
            foreach (var name in names.Distinct()) PrepareCollisionSource(name);
            foreach (var name in names.Distinct())
            {
                string path = PrefabPath(name);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyCollision(root, name);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var name in names.Distinct())
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Models + "/" + name + "_LOD0.fbx");
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            RefreshCatalogVersions();
        }

        static void ApplyCollision(GameObject root, string name)
        {
            var previous = root.transform.Find("CoastalCollision");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                if (!collider.isTrigger) collider.enabled = false;
            var source = root.transform.Find("CoastalVisual/LOD0");
            if (source == null) throw new InvalidOperationException("Missing coastal LOD0 " + name);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<Renderer>();
                bool rock = filter.name.EndsWith("_Rock_LOD0", StringComparison.Ordinal) || renderer != null && renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null && (m.name.StartsWith("CoastalRock", StringComparison.Ordinal) || m.name.StartsWith("TripoNative_", StringComparison.Ordinal) && m.name.Contains("_Rock")));
                if (!rock) continue;
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) throw new InvalidOperationException("Unreadable coastal LOD0 " + filter.name);
                var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                int offset = vertices.Count;
                foreach (var vertex in mesh.vertices) vertices.Add(matrix.MultiplyPoint3x4(vertex));
                var indices = mesh.triangles;
                bool mirrored = matrix.determinant < 0;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    triangles.Add(offset + indices[i]);
                    triangles.Add(offset + indices[i + (mirrored ? 2 : 1)]);
                    triangles.Add(offset + indices[i + (mirrored ? 1 : 2)]);
                }
            }
            if (triangles.Count == 0) throw new InvalidOperationException("No coastal rock triangles " + name);
            string path = Models + "/" + name + "_Collision.asset";
            var collisionMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (collisionMesh == null)
            {
                collisionMesh = new Mesh { name = name + "_Collision" };
                AssetDatabase.CreateAsset(collisionMesh, path);
            }
            collisionMesh.Clear();
            collisionMesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            collisionMesh.SetVertices(vertices);
            collisionMesh.SetTriangles(triangles, 0);
            collisionMesh.RecalculateBounds();
            EditorUtility.SetDirty(collisionMesh);
            AssetDatabase.SaveAssetIfDirty(collisionMesh);
            var collision = new GameObject("CoastalCollision") { layer = root.layer };
            collision.transform.SetParent(root.transform, false);
            var exactCollider = collision.AddComponent<MeshCollider>();
            exactCollider.sharedMesh = collisionMesh;
            exactCollider.convex = false;
        }

        static Dictionary<string, Material> PrepareMaterials()
        {
            var result = new Dictionary<string, Material>();
            foreach (var name in new[] { "CoastalRock", "CoastalRockDark", "CoastalRockLight", "CoastalRockWarm", "CoastalMoss", "CoastalBark", "CoastalLeaf", "CoastalLeafLight", "CoastalPalm", "CoastalFern", "CoastalTerrain" })
            {
                bool foliage = name == "CoastalPalm" || name == "CoastalFern";
                bool rock = name.StartsWith("CoastalRock", StringComparison.Ordinal) || name == "CoastalTerrain";
                var shader = Shader.Find(foliage ? "PirateSlop/CoastalFoliage" : rock ? "PirateSlop/CoastalRock" : "Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("Missing coastal shader.");
                string path = Materials + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                if (rock)
                {
                    material.SetTexture("_BaseMap", Texture("RockColour.png", false));
                    material.SetTexture("_BumpMap", Texture("RockNormal.png", true));
                    material.SetColor("_BaseColor", name == "CoastalRockDark" ? new Color(.82f, .82f, .78f) : new Color(.98f, .98f, .94f));
                    if (name == "CoastalRockLight") material.SetColor("_BaseColor", new Color(1.03f, 1.02f, .98f));
                    if (name == "CoastalRockWarm") material.SetColor("_BaseColor", new Color(1f, .95f, .87f));
                    material.SetFloat("_NormalScale", .6f);
                    material.SetFloat("_TextureScale", .35f);
                    material.SetFloat("_Smoothness", .12f);
                    material.SetFloat("_TerrainBlend", name == "CoastalTerrain" ? 1 : 0);
                }
                else if (foliage)
                {
                    material.SetTexture("_BaseMap", Texture(name == "CoastalPalm" ? "PalmColour.tga" : "FernColour.png", false));
                    material.SetTexture("_BumpMap", Texture(name == "CoastalPalm" ? "PalmNormal.tga" : "FernNormal.png", true));
                    material.SetTexture("_AlphaMap", name == "CoastalPalm" ? Texture2D.whiteTexture : Texture("FernAlpha.png", false, false));
                    material.SetColor("_BaseColor", new Color(.92f, 1f, .85f));
                    material.SetFloat("_Cutoff", .4f);
                    material.SetFloat("_NormalScale", .55f);
                    material.SetFloat("_LeafTintStrength", name == "CoastalPalm" ? .62f : .15f);
                }
                else
                {
                    material.SetColor("_BaseColor", name == "CoastalBark" ? new Color(.32f,.23f,.14f) : new Color(.26f,.35f,.12f));
                    material.SetFloat("_Smoothness", .18f);
                    material.SetFloat("_Cull", 0);
                }
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                result[name] = material;
            }
            var tripoShader = Shader.Find("PirateSlop/CoastalTripoRock");
            if (tripoShader == null) throw new InvalidOperationException("Missing Tripo rock shader.");
            foreach (string file in Directory.GetFiles(Models + "/Textures/Tripo", "*_basecolor.jpg"))
            {
                string module = Path.GetFileName(file).Replace("_basecolor.jpg", "");
                string name = "TripoNative_" + module;
                string path = Materials + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(tripoShader); AssetDatabase.CreateAsset(material, path); }
                material.shader = tripoShader;
                material.SetTexture("_BaseMap", Texture("Tripo/" + module + "_basecolor.jpg", false));
                material.SetTexture("_BumpMap", Texture("Tripo/" + module + "_normal.png", true));
                material.SetTexture("_MetallicRoughnessMap", Texture("Tripo/" + module + "_rm.jpg", false, false));
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_NormalScale", 1f);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                result[name] = material;
            }
            result["TripoArch_NativePalm"] = result["CoastalPalm"];
            result["TripoArch_NativeFern"] = result["CoastalFern"];
            return result;
        }

        static Texture2D Texture(string name, bool normal, bool srgb = true)
        {
            string path = Models + "/Textures/" + name;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture " + path);
            string before = EditorJsonUtility.ToJson(importer);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = normal ? false : srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !normal && name.Contains("Colour") && !name.Contains("Rock");
            importer.mipmapEnabled = true;
            importer.wrapMode = name.StartsWith("Rock", StringComparison.Ordinal) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static CoastalShoreContour[] BuildContours(GameObject root, bool visualOnly = false)
        {
            var contours = new List<CoastalShoreContour>();
            var filters = visualOnly ? root.transform.Find("CoastalVisual/LOD0").GetComponentsInChildren<MeshFilter>(true) : root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if ((!visualOnly && filter.GetComponentInParent<CoastalRockVisual>() != null) || filter.sharedMesh == null) continue;
                var collider = filter.GetComponent<MeshCollider>();
                if (!visualOnly && (collider == null || !collider.enabled || collider.isTrigger)) continue;
                var mesh = visualOnly ? filter.sharedMesh : collider.sharedMesh;
                var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
                var renderer = filter.GetComponent<Renderer>();
                var triangles = visualOnly ? Enumerable.Range(0, mesh.subMeshCount)
                    .Where(s => renderer != null && s < renderer.sharedMaterials.Length && (renderer.sharedMaterials[s].shader.name == "PirateSlop/CoastalRock" || renderer.sharedMaterials[s].shader.name == "PirateSlop/CoastalTripoRock"))
                    .SelectMany(s => mesh.GetTriangles(s)).ToArray() : mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var intersections = new List<Vector3>(2);
                    for (int edge = 0; edge < 3; edge++)
                    {
                        var a = vertices[triangles[i + edge]];
                        var b = vertices[triangles[i + (edge + 1) % 3]];
                        if ((a.y <= 0 && b.y > 0) || (b.y <= 0 && a.y > 0)) intersections.Add(Vector3.Lerp(a, b, -a.y / (b.y - a.y)));
                    }
                    if (intersections.Count != 2 || Vector3.Distance(intersections[0], intersections[1]) < .03f) continue;
                    var normal = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                    if (Vector3.Dot(intersections[1] - intersections[0], Vector3.Cross(Vector3.up, normal)) < 0) intersections.Reverse();
                    contours.Add(new CoastalShoreContour { Points = intersections.ToArray() });
                }
            }
            return ExposedContours(contours);
        }

        static CoastalShoreContour[] ExposedContours(List<CoastalShoreContour> segments)
        {
            if (segments.Count == 0) return Array.Empty<CoastalShoreContour>();
            var points = segments.SelectMany(s => s.Points).ToArray();
            var min = new Vector2(points.Min(p => p.x), points.Min(p => p.z));
            var max = new Vector2(points.Max(p => p.x), points.Max(p => p.z));
            float step = Mathf.Max(.75f, Mathf.Max(max.x - min.x, max.y - min.y) / 510f);
            min -= Vector2.one * step;
            int width = Mathf.CeilToInt((max.x - min.x) / step) + 1;
            int depth = Mathf.CeilToInt((max.y - min.y) / step) + 1;
            var rows = Enumerable.Range(0, depth).Select(_ => new List<Vector2>()).ToArray();
            foreach (var segment in segments)
            {
                var a = segment.Points[0]; var b = segment.Points[1];
                if (Mathf.Abs(a.z - b.z) < .00001f) continue;
                int first = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(a.z, b.z) - min.y) / step - .5f));
                int last = Mathf.Min(depth - 1, Mathf.CeilToInt((Mathf.Max(a.z, b.z) - min.y) / step - .5f) - 1);
                for (int z = first; z <= last; z++)
                {
                    float t = (min.y + (z + .5f) * step - a.z) / (b.z - a.z);
                    rows[z].Add(new Vector2(Mathf.Lerp(a.x, b.x, t), b.z > a.z ? 1 : -1));
                }
            }
            var occupied = new bool[width * depth];
            for (int z = 0; z < depth; z++)
            {
                rows[z].Sort((a, b) => a.x.CompareTo(b.x));
                int winding = 0; float previous = min.x;
                foreach (var crossing in rows[z])
                {
                    if (winding != 0)
                    {
                        int first = Mathf.Max(0, Mathf.CeilToInt((previous - min.x) / step - .5f));
                        int last = Mathf.Min(width - 1, Mathf.CeilToInt((crossing.x - min.x) / step - .5f) - 1);
                        for (int x = first; x <= last; x++) occupied[z * width + x] = true;
                    }
                    winding += (int)crossing.y;
                    previous = crossing.x;
                }
            }
            var contours = new List<CoastalShoreContour>();
            bool Filled(int x, int z) => x >= 0 && x < width && z >= 0 && z < depth && occupied[z * width + x];
            void Add(int x, int z, int endX, int endZ)
            {
                contours.Add(new CoastalShoreContour { Points = new[] {
                    new Vector3(min.x + x * step, 0, min.y + z * step),
                    new Vector3(min.x + endX * step, 0, min.y + endZ * step) } });
            }
            for (int z = 0; z <= depth; z++)
                for (int side = 0; side < 2; side++)
                {
                    int start = -1;
                    for (int x = 0; x <= width; x++)
                    {
                        bool edge = side == 0 ? Filled(x, z) && !Filled(x, z - 1) : Filled(x, z - 1) && !Filled(x, z);
                        if (edge && start < 0) start = x;
                        if (!edge && start >= 0) { Add(start, z, x, z); start = -1; }
                    }
                }
            for (int x = 0; x <= width; x++)
                for (int side = 0; side < 2; side++)
                {
                    int start = -1;
                    for (int z = 0; z <= depth; z++)
                    {
                        bool edge = side == 0 ? Filled(x, z) && !Filled(x - 1, z) : Filled(x - 1, z) && !Filled(x, z);
                        if (edge && start < 0) start = z;
                        if (!edge && start >= 0) { Add(x, start, x, z); start = -1; }
                    }
                }
            return contours.ToArray();
        }

        public static void RefreshCatalogVersions()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/Settings/World/DefaultWorld.asset");
            foreach (var definition in profile.Decorations)
                definition.PrefabVersion = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(definition.Prefab)).ToString();
            foreach (var location in profile.Locations)
            {
                foreach (var point in location.Points)
                    if (point.StaticPrefab != null && (point.StaticPrefab.GetComponentInChildren<CoastalRockVisual>(true) != null || point.StaticPrefab.GetComponent<SupplyIslandComposition>() != null))
                        point.PrefabVersion = Convert.ToInt32(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(point.StaticPrefab)).ToString().Substring(0, 7), 16) + 1;
                EditorUtility.SetDirty(location);
                AssetDatabase.SaveAssetIfDirty(location);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            var gallery = PrefabUtility.LoadPrefabContents(Gallery);
            try
            {
                gallery.GetComponent<EnvironmentTestGallery>().Revision = GalleryRevision();
                PrefabUtility.SaveAsPrefabAsset(gallery, Gallery);
            }
            finally { PrefabUtility.UnloadPrefabContents(gallery); }
        }

        static string GalleryRevision()
        {
            return WorldLayout.Hash("CoastalEnvironmentCRow30|" + string.Join("|", Environment.Concat(Starter).Select(n => AssetDatabase.GetAssetDependencyHash(PrefabPath(n)).ToString())));
        }

        static string PrefabPath(string name) => (Environment.Contains(name) ? "Assets/Prefabs/Environment/" : "Assets/Prefabs/World/StarterIsland/") + name + ".prefab";

        [MenuItem("PirateSlop/Art/Arrange Coastal Test Gallery")]
        public static void ArrangeTestGallery()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before arranging the gallery.");
            var gallery = PrefabUtility.LoadPrefabContents(Gallery);
            try
            {
                foreach (var child in gallery.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var labels = new List<Transform>();
                var entries = new List<Transform>();
                float cursor = 0, depth = 0;
                foreach (var name in Environment.Concat(Starter))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gallery.transform);
                    var renderers = instance.transform.Find("CoastalVisual/LOD0").GetComponentsInChildren<Renderer>(true);
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    instance.transform.localPosition = new Vector3(cursor - bounds.min.x, 0, -bounds.center.z);
                    entries.Add(instance.transform);
                    var label = new GameObject(name).transform;
                    label.SetParent(gallery.transform, false);
                    label.localPosition = new Vector3(cursor + bounds.extents.x, Mathf.Max(12, bounds.max.y + 8), 0);
                    labels.Add(label);
                    cursor += bounds.size.x + GalleryGap;
                    depth = Mathf.Max(depth, bounds.extents.z);
                }
                float half = (cursor - GalleryGap) * .5f;
                foreach (var entry in entries) entry.localPosition -= Vector3.right * half;
                foreach (var label in labels) label.localPosition -= Vector3.right * half;
                var component = gallery.GetComponent<EnvironmentTestGallery>();
                component.Labels = labels.ToArray();
                component.Spawn = new Vector3(-half, 0, -depth - 120);
                component.Radius = Mathf.Max(2000, half + depth + 650);
                component.Revision = GalleryRevision();
                PrefabUtility.SaveAsPrefabAsset(gallery, Gallery);
            }
            finally { PrefabUtility.UnloadPrefabContents(gallery); }
        }
    }
}
