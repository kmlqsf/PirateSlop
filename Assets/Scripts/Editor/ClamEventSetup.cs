using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PirateSlop.EditorTools
{
    public static class ClamEventSetup
    {
        const string Folder = "Assets/Models/SeaEvents/Clam/";
        const string PrefabPath = "Assets/Resources/SeaEvents/ClamEvent.prefab";
        static MeshFilter pumpBody, pumpLever;
        sealed class ReusedPart { public Mesh Mesh; public Material[] Materials; }
        static readonly Dictionary<string, ReusedPart> parts = new();
        const float Scale = 14f, OffsetY = -2.43f;
        static readonly Vector3 Hinge = new(0, .245f, -.485f);

        [MenuItem("PirateSlop/Prepare Clam Chest Event")]
        public static string Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var art = AssetDatabase.LoadAssetAtPath<SeaEventArt>("Assets/Resources/SeaEvents/SeaEventArt.asset");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Clam.fbx");
            if (source == null || art == null || art.Raft == null || catalog == null || catalog.ChestPrefab == null) throw new InvalidOperationException("Clam model, SeaEventArt raft and chest catalog are required.");
            LoadMechanismParts();
            var material = ShellMaterial();
            var f = source.GetComponentInChildren<MeshFilter>();
            var original = f.sharedMesh;
            var vertices = original.vertices.Select(v => Quaternion.Euler(0, 180, 0) * f.transform.TransformPoint(v)).ToArray();
            var originalNormals = original.normals.Select(v => Quaternion.Euler(0, 180, 0) * f.transform.TransformDirection(v)).ToArray();
            var closedMatrix = Matrix4x4.Scale(new Vector3(1, 1, 1.45f)) * Matrix4x4.Rotate(Quaternion.Euler(68, 0, 0));
            var lowerVertices = vertices.Select(v => ShapeBowl(v * Scale + Vector3.up * OffsetY)).ToArray();
            var upperVertices = vertices.Select(v => (closedMatrix.MultiplyPoint3x4(v - Hinge) + Vector3.up * .0185f) * Scale).ToArray();
            var upperNormals = originalNormals.Select(n => closedMatrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
            var lowerTriangles = new List<int>(); var upperTriangles = new List<int>();
            var triangles = original.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float y = (vertices[triangles[i]].y + vertices[triangles[i + 1]].y + vertices[triangles[i + 2]].y) / 3f;
                var target = y > .245f ? upperTriangles : lowerTriangles;
                target.Add(triangles[i]); target.Add(triangles[i + 1]); target.Add(triangles[i + 2]);
            }
            var lowerMesh = PartMesh(lowerVertices, originalNormals, original.uv, lowerTriangles); lowerMesh.RecalculateNormals(); lowerMesh.RecalculateTangents();
            var lower = SaveMesh(lowerMesh, "ClamLower");
            var upper = SaveMesh(PartMesh(upperVertices, upperNormals, original.uv, upperTriangles), "ClamUpper");
            var root = new GameObject("ClamChestEvent");
            var previewScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previewScene);
            try
            {
                var net = root.AddComponent<NetworkObject>();
                net.SetAssetPathHash(new string((PrefabPath + root.name).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()).GetStableHashU64());
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeAll;
                var clam = root.AddComponent<NetworkClamEvent>(); clam.Catalog = catalog;
                var bowl = Child(root.transform, "LowerShell");
                bowl.gameObject.AddComponent<MeshFilter>().sharedMesh = lower;
                bowl.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                bowl.gameObject.AddComponent<MeshCollider>().sharedMesh = lower;
                var lid = Child(root.transform, "UpperShellPivot"); lid.localPosition = Hinge * Scale + Vector3.up * OffsetY;
                var bones = new[] { Child(lid, "SkinRoot") };
                clam.ShellPivot = lid;
                var weights = new BoneWeight[upper.vertexCount]; var uv = upper.vertices;
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1 };
                }
                upper.boneWeights = weights; upper.bindposes = new[] { Matrix4x4.identity };
                upper.ClearBlendShapes();
                for (int side = 0; side < 2; side++) upper.AddBlendShapeFrame(side == 0 ? "LeftLipLift" : "RightLipLift", 100, uv.Select(v => Vector3.up * LipWeight(v, side)).ToArray(), null, null);
                EditorUtility.SetDirty(upper);
                var skin = lid.gameObject.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = upper; skin.sharedMaterial = material; skin.bones = bones; skin.rootBone = lid;
                clam.ShellSkin = skin;
                skin.localBounds = new Bounds(new Vector3(0, 5, 5), new Vector3(15, 23, 20));
                var collisionMesh = Object.Instantiate(upper); collisionMesh.ClearBlendShapes(); collisionMesh.boneWeights = Array.Empty<BoneWeight>(); collisionMesh.bindposes = Array.Empty<Matrix4x4>();
                clam.ShellCollider = lid.gameObject.AddComponent<MeshCollider>(); clam.ShellCollider.sharedMesh = SaveMesh(collisionMesh, "ClamLidCollision0");
                BuildHinge(root.transform, lid.localPosition);
                clam.ChestSocket = Child(root.transform, "ChestSocket"); clam.ChestSocket.localPosition = new Vector3(0, SurfaceHeight(lower, 0, -2.5f) - ChestBottom(catalog.ChestPrefab.transform), -2.5f);
                clam.Levers = new ClamLeverTarget[2];
                for (int side = 0; side < 2; side++)
                {
                    int sign = side == 0 ? -1 : 1;
                    var platform = Object.Instantiate(art.Raft, root.transform); platform.name = side == 0 ? "LeftWoodenPlatform" : "RightWoodenPlatform";
                    platform.transform.localPosition = new Vector3(sign * 6.2f, 0, 3.65f); platform.transform.localScale = new Vector3(.8f, 1, .9f);
                    var desired = new Vector3(sign * 4.4f, 0, 10.4f);
                    var rim = upper.vertices.OrderBy(v => Mathf.Abs(v.x - desired.x) * 1.5f + Mathf.Abs(v.z - desired.z)).First();
                    var hook = Child(lid, side == 0 ? "LeftRimHook" : "RightRimHook"); hook.localPosition = rim;
                    BuildHook(hook);
                    clam.Levers[side] = BuildJack(root.transform, clam, side, new Vector3(sign * 6.2f, .425f, 3.1f), hook);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            registry.AddObject(prefab.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(registry);
            catalog.ClamEventPrefab = prefab.GetComponent<NetworkClamEvent>(); EditorUtility.SetDirty(catalog);
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try { if (player.GetComponent<ClamPlayerInteraction>() == null) player.AddComponent<ClamPlayerInteraction>(); PrefabUtility.SaveAsPrefabAsset(player, playerPath); }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            return "Clam saved: intact ship pump mechanisms, exterior platforms, empty shell interior and chest socket.";
        }

        static Vector3 ShapeBowl(Vector3 point)
        {
            if (point.y < .425f) point.y = Mathf.Lerp(point.y, .425f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-1.75f, -1.5f, point.y)));
            return point;
        }

        static float LipWeight(Vector3 point, int side) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.2f, 3.8f, point.x * (side == 0 ? -1 : 1)))
            * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4f, 9f, point.z));

        static float SurfaceHeight(Mesh mesh, float x, float z)
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles; float height = float.NegativeInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < .000001f) continue;
                float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / denominator;
                float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / denominator;
                if (u >= 0 && v >= 0 && u + v <= 1) height = Mathf.Max(height, u * a.y + v * b.y + (1 - u - v) * c.y);
            }
            if (!float.IsFinite(height)) throw new InvalidOperationException("Chest point has no shell surface.");
            return height;
        }

        static float ChestBottom(Transform root)
        {
            float bottom = float.PositiveInfinity;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                    bottom = Mathf.Min(bottom, root.InverseTransformPoint(filter.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)))).y);
            }
            return bottom;
        }

        static ClamLeverTarget BuildJack(Transform root, NetworkClamEvent owner, int side, Vector3 point, Transform hook)
        {
            var t = Child(root, side == 0 ? "LeftLeverJack" : "RightLeverJack"); t.localPosition = point;
            var lever = t.gameObject.AddComponent<ClamLeverTarget>(); lever.Owner = owner; lever.Side = side; lever.HookSocket = hook;
            lever.HookRestPoint = hook.localPosition; lever.HookLiftWeight = LipWeight(hook.localPosition, side);
            var pump = Child(t, "ShipPumpAssembly"); pump.localRotation = Quaternion.Euler(0, 180, 0);
            SourceVisual(pump, pumpBody, "IntactPumpBody");
            var bodyHit = pump.gameObject.AddComponent<BoxCollider>(); bodyHit.center = new Vector3(0, .57f, 0); bodyHit.size = new Vector3(.62f, 1.15f, .58f);
            lever.HandlePivot = Child(pump, "HandlePivot"); lever.HandlePivot.localPosition = pumpLever.transform.parent.localPosition;
            SourceVisual(lever.HandlePivot, pumpLever, "IntactPumpHandle");
            lever.Grip = Child(lever.HandlePivot, "Grip"); lever.Grip.localPosition = new Vector3(0, .45f, -.61f);
            var gripHit = lever.Grip.gameObject.AddComponent<SphereCollider>(); gripHit.radius = .18f;
            var handleHit = lever.HandlePivot.gameObject.AddComponent<BoxCollider>(); handleHit.center = new Vector3(0, .225f, -.305f); handleHit.size = new Vector3(.25f, .65f, .85f);
            lever.LiftArm = Child(t, "HingedTelescopicJack"); lever.LiftArm.localPosition = new Vector3(0, .22f, -.1f);
            VisualPart(lever.LiftArm, "Shackle", "JackBaseClevis", Vector3.zero, .28f, Quaternion.identity);
            var size = parts["Axle"].Mesh.bounds.size;
            lever.SectionLength = Vector3.Distance(hook.position, lever.LiftArm.position);
            lever.LiftSections = new Transform[5];
            for (int i = 0; i < lever.LiftSections.Length; i++)
            {
                var section = VisualPart(lever.LiftArm, "Axle", "MetalJackSection" + i, Vector3.zero, 1, Quaternion.identity);
                float diameter = .3f - i * .045f;
                section.localScale = new Vector3(diameter / size.x, lever.SectionLength, diameter / size.z); lever.LiftSections[i] = section;
            }
            lever.BoardingPoint = Child(t, "BoardingPoint"); lever.BoardingPoint.localPosition = new Vector3(0, .025f, 1.25f);
            lever.Animate();
            return lever;
        }

        static void BuildHook(Transform hook)
        {
            VisualPart(hook, "Hook", "ShipGrappleRimHook", Vector3.up * .06f, .28f, Quaternion.Euler(0, 0, 180));
            VisualPart(hook, "Shackle", "RimClevis", Vector3.zero, .2f, Quaternion.identity);
        }

        static void BuildHinge(Transform root, Vector3 pivot)
        {
            var axle = VisualPart(root, "Axle", "RearHingeAxle", pivot, 1, Quaternion.Euler(0, 0, 90));
            var size = parts["Axle"].Mesh.bounds.size;
            axle.localScale = new Vector3(.16f / size.x, 3.8f, .16f / size.z);
        }

        static Mesh PartMesh(Vector3[] positions, Vector3[] normals, Vector2[] uv, List<int> triangles)
        {
            var indices = new Dictionary<int, int>(); var v = new List<Vector3>(); var n = new List<Vector3>(); var u = new List<Vector2>(); var t = new List<int>();
            foreach (int source in triangles)
            {
                if (!indices.TryGetValue(source, out int index))
                { index = v.Count; indices.Add(source, index); v.Add(positions[source]); n.Add(normals[source]); u.Add(uv[source]); }
                t.Add(index);
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, u); mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = Folder + name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); return existing;
        }

        static Transform Child(Transform parent, string name)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }

        static Transform SourceVisual(Transform parent, MeshFilter source, string name)
        {
            var t = Child(parent, name);
            t.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation); t.localScale = source.transform.localScale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            return t;
        }

        static Transform VisualPart(Transform parent, string key, string name, Vector3 position, float size, Quaternion rotation)
        {
            var t = Child(parent, name); t.SetLocalPositionAndRotation(position, rotation); t.localScale = Vector3.one * size;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = parts[key].Mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = parts[key].Materials;
            return t;
        }

        static void LoadMechanismParts()
        {
            parts.Clear();
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
            var filters = ship.GetComponentsInChildren<MeshFilter>(true);
            pumpBody = filters.First(f => f.name == "V19_PumpBody");
            pumpLever = filters.First(f => f.name == "V19_PumpLever");
            LoadPart("Bearing", filters.First(f => f.name == "V9_Capstan_Deck_Bearing"), false);
            LoadPart("Axle", filters.First(f => f.name == "ShipNameplatePortLowerPin"), true);
            LoadPart("Shackle", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SeaEvents/RopeShackle.prefab").GetComponentInChildren<MeshFilter>(), true);
            LoadPart("Hook", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Loot/Replacement/GrappleHook/GrappleHook.fbx").GetComponentInChildren<MeshFilter>(), true,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Loot/Replacement/GrappleHook/GrappleHook.mat"));
        }

        static void LoadPart(string key, MeshFilter source, bool longestUp, Material overrideMaterial = null)
        {
            var sources = source.transform.parent != null && source.transform.parent.name.StartsWith("V3_Piece_")
                ? source.transform.parent.GetComponentsInChildren<MeshFilter>(true) : new[] { source };
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var f in sources)
            {
                if (f.sharedMesh == null) continue;
                var materials = f.GetComponent<MeshRenderer>().sharedMaterials;
                for (int sub = 0; sub < f.sharedMesh.subMeshCount; sub++)
                {
                    var material = overrideMaterial != null ? overrideMaterial : materials[Mathf.Min(sub, materials.Length - 1)];
                    if (!groups.TryGetValue(material, out var instances)) { instances = new List<CombineInstance>(); groups.Add(material, instances); }
                    instances.Add(new CombineInstance { mesh = f.sharedMesh, subMeshIndex = sub, transform = f.transform.localToWorldMatrix });
                }
            }
            var combined = new List<CombineInstance>(); var temporary = new List<Mesh>();
            foreach (var group in groups)
            {
                var sub = new Mesh(); sub.CombineMeshes(group.Value.ToArray(), true, true); temporary.Add(sub);
                combined.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
            }
            var mesh = new Mesh(); mesh.CombineMeshes(combined.ToArray(), false, false);
            foreach (var temporaryMesh in temporary) Object.DestroyImmediate(temporaryMesh);
            var b = mesh.bounds;
            var axis = longestUp ? b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : b.size.y >= b.size.z ? Vector3.up : Vector3.forward
                : b.size.x <= b.size.y && b.size.x <= b.size.z ? Vector3.right : b.size.y <= b.size.z ? Vector3.up : Vector3.forward;
            var rotation = Quaternion.FromToRotation(axis, Vector3.up);
            float unit = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            var v = mesh.vertices; var n = mesh.normals;
            for (int i = 0; i < v.Length; i++) v[i] = rotation * (v[i] - b.center) / unit;
            for (int i = 0; i < n.Length; i++) n[i] = rotation * n[i];
            mesh.vertices = v; mesh.normals = n; mesh.RecalculateBounds(); mesh.RecalculateTangents();
            parts[key] = new ReusedPart { Mesh = SaveMesh(mesh, "Reused" + key), Materials = groups.Keys.ToArray() };
        }

        static Material ShellMaterial()
        {
            foreach (string name in new[] { "ClamBaseColor.jpg", "ClamMetallic.jpg", "ClamRoughness.jpg", "ClamNormal.png" })
            {
                var importer = AssetImporter.GetAtPath(Folder + name) as TextureImporter;
                importer.textureType = name.Contains("Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = name.Contains("BaseColor"); importer.isReadable = name.Contains("Metallic") || name.Contains("Roughness"); importer.maxTextureSize = 2048; importer.SaveAndReimport();
            }
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "ClamMetallic.jpg");
            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "ClamRoughness.jpg");
            var m = metallic.GetPixels(); var r = roughness.GetPixels(); var colors = new Color[m.Length];
            if (metallic.width != roughness.width || metallic.height != roughness.height) throw new InvalidOperationException("Metallic and roughness map sizes differ.");
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(m[i].r, m[i].r, m[i].r, 1 - r[i].r);
            var packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true); packed.SetPixels(colors); packed.Apply();
            string packedPath = Folder + "ClamMetalSmooth.png"; System.IO.File.WriteAllBytes(packedPath, packed.EncodeToPNG()); Object.DestroyImmediate(packed); AssetDatabase.ImportAsset(packedPath);
            var packedImporter = (TextureImporter)AssetImporter.GetAtPath(packedPath); packedImporter.sRGBTexture = false; packedImporter.SaveAndReimport();
            string materialPath = Folder + "ClamSkin.mat"; var skin = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (skin == null) { skin = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(skin, materialPath); }
            skin.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "ClamBaseColor.jpg"));
            skin.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "ClamNormal.png"));
            skin.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
            skin.SetFloat("_Metallic", 1); skin.SetFloat("_Smoothness", .65f); skin.EnableKeyword("_NORMALMAP"); skin.EnableKeyword("_METALLICSPECGLOSSMAP"); skin.enableInstancing = true;
            EditorUtility.SetDirty(skin); return skin;
        }
    }
}
