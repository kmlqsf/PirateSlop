using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PirateSlop.EditorTools
{
    public static class WhaleEventSetup
    {
        const string Folder = "Assets/Models/Whale";
        const string WhaleSource = Folder + "/Coralback/CoralbackWhale/tripo_convert_c44df717-9a58-4bb0-8550-c36476f6da12.fbx";
        const string HarpoonSource = Folder + "/Harpoon/WhaleHarpoon.fbx";
        const string PrefabPath = "Assets/Resources/Whale/WhaleLootPOI.prefab";

        [MenuItem("PirateSlop/Prepare Coralback Whale Event")]
        public static void Configure()
        {
            string source = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).Parent.FullName, "NewShip", "Гарпун", "гарпун", "tripo_convert_d9459e62-ac48-43dc-9ebd-276476e99e34.fbm");
            System.IO.Directory.CreateDirectory(Folder + "/Harpoon/Textures");
            if (System.IO.Directory.Exists(source)) foreach (var file in System.IO.Directory.GetFiles(source)) System.IO.File.Copy(file, Folder + "/Harpoon/Textures/" + System.IO.Path.GetFileName(file), true);
            AssetDatabase.Refresh();
            var whaleMaterial = Material(Folder + "/Coralback", "Кит");
            var harpoonMaterial = Material(Folder + "/Harpoon", "гарпун");
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var root = new GameObject("WhaleLootPOI");
            try
            {
                var net = root.AddComponent<NetworkObject>();
                net.SetAssetPathHash(new string((PrefabPath + root.name).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()).GetStableHashU64());
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.None;
                var whale = root.AddComponent<WhaleLootPoint>(); whale.Catalog = catalog;
                var model = Child(root.transform, "CoralbackWhale"); whale.WhaleModel = model;
                var mesh = NormalizedMesh(WhaleSource, Folder + "/Coralback/CoralbackRig.asset", Quaternion.identity, Vector3.zero);
                var collider = model.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                var bones = new List<Transform> { Child(model, "Body") };
                Transform tail = bones[0];
                foreach (float z in new[] { -7f, -11f, -15f }) { tail = Child(tail, "Tail_" + bones.Count); tail.position = new Vector3(0, 6, z); bones.Add(tail); }
                for (int side = -1; side <= 1; side += 2) { var fin = Child(bones[0], side < 0 ? "Flipper_Left" : "Flipper_Right"); fin.position = new Vector3(side * 3.5f, 4, 4.5f); bones.Add(fin); }
                var weights = new BoneWeight[mesh.vertexCount];
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var v = vertices[i]; float tailWeight = Mathf.SmoothStep(0, 1, (-v.z - 5) / 3);
                    float finWeight = Mathf.SmoothStep(0, 1, (Mathf.Abs(v.x) - 3.5f) / 2.1f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2, 6, Mathf.Abs(v.z - 4.5f)))) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.2f, 6.5f, v.y)));
                    if (tailWeight > .01f)
                    {
                        float f = Mathf.Clamp((-v.z - 7) / 4, 0, 2); int a = Mathf.Min(3, (int)f + 1), b = Mathf.Min(3, a + 1); float t = f - Mathf.Floor(f);
                        weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1 - tailWeight, boneIndex1 = a, weight1 = tailWeight * (1 - t), boneIndex2 = b, weight2 = tailWeight * t };
                    }
                    else weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1 - finWeight, boneIndex1 = v.x < 0 ? 4 : 5, weight1 = finWeight };
                }
                mesh.boneWeights = weights; mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * model.localToWorldMatrix).ToArray(); EditorUtility.SetDirty(mesh);
                var skin = model.gameObject.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = mesh; skin.sharedMaterial = whaleMaterial; skin.bones = bones.ToArray(); skin.rootBone = bones[0]; skin.localBounds = new Bounds(new Vector3(0, 5, 0), new Vector3(23, 16, 42)); skin.updateWhenOffscreen = false;
                whale.TailBones = bones.Skip(1).Take(3).ToArray(); whale.FlipperBones = bones.Skip(4).ToArray();
                whale.ChestSocket = Child(root.transform, "ChestSocket"); whale.ChestSocket.position = new Vector3(0, Height(collider, 0, .5f) + .05f, .5f); whale.ChestSocket.rotation = Quaternion.Euler(0, 90, 0);
                var harpoonMesh = NormalizedMesh(HarpoonSource, Folder + "/Harpoon/HarpoonNormalized.asset", Quaternion.Euler(0, -90, 0), new Vector3(0, -.51f, 1.2f));
                AlignHarpoon(harpoonMesh);
                var hemp = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Models/Ships/ShipV3/Materials/Ship_Hemp_Rope_PBR.001.mat");
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 3; i++)
                    {
                        float z = 8 - i * 6;
                        var outward = new Vector3(side * .65f, .76f, 0).normalized;
                        var surface = new Vector3(side * 2.8f, Height(collider, side * 2.8f, z), z);
                        var ray = new Ray(surface + outward * 6f, -outward);
                        if (!collider.Raycast(ray, out var hit, 12)) throw new InvalidOperationException("Harpoon surface missing: " + side + "/" + i);
                        var pinRoot = Child(root.transform, (side < 0 ? "Left" : "Right") + "Harpoon" + (i + 1));
                        pinRoot.position = hit.point - outward * .65f; pinRoot.rotation = Quaternion.LookRotation(outward, Vector3.up);
                        pinRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = harpoonMesh;
                        pinRoot.gameObject.AddComponent<MeshRenderer>().sharedMaterial = harpoonMaterial;
                        var hitbox = pinRoot.gameObject.AddComponent<CapsuleCollider>(); hitbox.direction = 2; hitbox.center = new Vector3(0, 0, 1.32f); hitbox.height = 2.7f; hitbox.radius = .22f;
                        var pin = pinRoot.gameObject.AddComponent<WhaleHarpoonPin>(); pin.Owner = whale; pin.Index = whale.Harpoons.Count; pin.LocalOutward = outward;
                        pin.Grip = Child(pinRoot, "HandGrip"); pin.Grip.localPosition = new Vector3(0, 0, 1.72f);
                        pin.RopeEye = Child(pinRoot, "RopeEye"); pin.RopeEye.localPosition = new Vector3(0, .30f, .9f);
                        var eye = new Vector3[33];
                        for (int j = 0; j < eye.Length; j++) { float angle = j * Mathf.PI * 2f / (eye.Length - 1); eye[j] = new Vector3(0, .16f + Mathf.Cos(angle) * .14f, .9f + Mathf.Sin(angle) * .14f); }
                        Tube(pinRoot, "ForgedRopeEye", eye, .024f, harpoonMaterial);
                        var knot = new List<Vector3>();
                        knot.Add(pin.RopeEye.localPosition);
                        for (int j = 0; j <= 64; j++)
                        {
                            float t = j / 64f, angle = t * Mathf.PI * 4f;
                            knot.Add(new Vector3(Mathf.Sin(angle) * .105f, Mathf.Cos(angle) * .105f, Mathf.Lerp(.81f, .98f, t)));
                        }
                        knot.Add(new Vector3(-.045f, .18f, .91f)); knot.Add(new Vector3(.045f, .25f, .9f)); knot.Add(pin.RopeEye.localPosition);
                        Tube(pinRoot, "RopeEyeSeizing", knot.ToArray(), .032f, hemp);
                        whale.Harpoons.Add(pin);
                    }
                var ropes = new List<WhaleHarpoonRope>();
                var contactChest = Object.Instantiate(catalog.ChestPrefab.gameObject, whale.ChestSocket.position, whale.ChestSocket.rotation);
                var chestContacts = new List<MeshCollider>();
                foreach (var filter in contactChest.GetComponentsInChildren<MeshFilter>()) { var contact = filter.gameObject.AddComponent<MeshCollider>(); contact.sharedMesh = filter.sharedMesh; chestContacts.Add(contact); }
                Physics.SyncTransforms();
                try
                {
                for (int i = 0; i < 3; i++)
                {
                    var a = whale.Harpoons[i]; var b = whale.Harpoons[5 - i];
                    var ropeRoot = Child(root.transform, "CrossedHempRope" + (i + 1));
                    var line = ropeRoot.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.sharedMaterial = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Resources/HookRope.mat"); line.widthCurve = AnimationCurve.Constant(0, 1, 1); line.widthMultiplier = .09f; line.numCapVertices = 3; line.textureMode = LineTextureMode.Tile;
                    var lid = chestContacts.First(c => c.name == "ChestLid");
                    var lidBounds = lid.bounds;
                    float diagonal = i == 0 ? .82f : i == 2 ? -.82f : .28f;
                    float leftZ = lidBounds.center.z + lidBounds.extents.z * diagonal;
                    float rightZ = lidBounds.center.z + lidBounds.extents.z * (i == 1 ? diagonal : -diagonal);
                    var left = LidContact(lid, lidBounds.min.x + .025f, leftZ);
                    var right = LidContact(lid, lidBounds.max.x - .025f, rightZ);
                    var guides = new[] { a.RopePoint, left, right, b.RopePoint };
                    var route = new List<Vector3> { guides[0] };
                    for (int leg = 0; leg < guides.Length - 1; leg++)
                    {
                        var supports = TautLeg(guides[leg], guides[leg + 1], leg == 1 ? new[] { lid } : chestContacts.Concat(new[] { collider }).ToArray(), leg == 1 ? ropes : null);
                        route.AddRange(supports.Skip(1));
                    }
                    route[^1] = b.RopePoint;
                    line.positionCount = route.Count; line.SetPositions(route.ToArray());
                    var tube = ropeRoot.gameObject.AddComponent<RopeTubeVisual>(); tube.Line = line; tube.Radius = .045f; tube.TilesPerMeter = 5;
                    var rope = ropeRoot.gameObject.AddComponent<WhaleHarpoonRope>(); rope.A = a; rope.B = b; rope.Line = line; rope.Route = route.ToArray();
                    for (int j = 1; j < route.Count; j++) rope.RestLength += Vector3.Distance(route[j - 1], route[j]);
                    ropes.Add(rope);
                }
                }
                finally { Object.DestroyImmediate(contactChest); }
                whale.Ropes = ropes.ToArray();
                for (int side = -1; side <= 1; side += 2)
                {
                    var access = Child(root.transform, side < 0 ? "LeftBoardingRope" : "RightBoardingRope");
                    access.position = new Vector3(side * 5.2f, 5.1f, 1.5f); access.rotation = Quaternion.Euler(0, side < 0 ? 270 : 90, 0);
                    var ladder = access.gameObject.AddComponent<ShipLadder>(); ladder.Height = 3.7f; ladder.RopeClimb = true; ladder.BoardingAccess = true; ladder.FollowRopePath = true; ladder.TopLean = -1.8f; ladder.BothSides = true; ladder.HalfWidth = .8f; ladder.ExitDepth = 1.5f; ladder.ExitPoint = new Vector3(0, 4.2f, -2.4f);
                    var line = access.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 12; line.sharedMaterial = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Resources/HookRope.mat"); line.widthCurve = AnimationCurve.Constant(0, 1, 1); line.widthMultiplier = .08f; line.textureMode = LineTextureMode.Tile;
                    for (int i = 0; i < 12; i++) { float t = i / 11f; line.SetPosition(i, new Vector3(0, t * 3.7f, -1.8f * t)); }
                    var tube = access.gameObject.AddComponent<RopeTubeVisual>(); tube.Line = line; tube.Radius = .04f;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try { if (player.GetComponent<WhalePlayerInteraction>() == null) player.AddComponent<WhalePlayerInteraction>(); PrefabUtility.SaveAsPrefabAsset(player, playerPath); }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset"); registry.AddObject(prefab.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(registry);
            catalog.WhaleEventPrefab = prefab.GetComponent<WhaleLootPoint>(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static Vector3 LidContact(MeshCollider lid, float x, float z)
        {
            for (int i = 0; i <= 12; i++)
            {
                var origin = new Vector3(Mathf.Lerp(x, lid.bounds.center.x, i / 12f), 25, z);
                if (lid.Raycast(new Ray(origin, Vector3.down), out var hit, 30)) return hit.point + Vector3.up * .049f;
            }
            throw new InvalidOperationException("Chest lid contact missing");
        }
        static List<Vector3> TautLeg(Vector3 start, Vector3 end, MeshCollider[] surfaces, List<WhaleHarpoonRope> crossings)
        {
            var hull = new List<Vector3> { start };
            float length = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
            int steps = Mathf.Max(2, Mathf.CeilToInt(length / .04f));
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                var point = Vector3.Lerp(start, end, t);
                if (i < steps)
                {
                    foreach (var surface in surfaces) point.y = Mathf.Max(point.y, Height(surface, point.x, point.z) + .049f);
                    if (crossings != null) foreach (var rope in crossings)
                        for (int j = 1; j < rope.Route.Length; j++)
                        {
                            var a = rope.Route[j - 1]; var b = rope.Route[j];
                            var direction = new Vector2(b.x - a.x, b.z - a.z);
                            float along = direction.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(new Vector2(point.x - a.x, point.z - a.z), direction) / direction.sqrMagnitude) : 0;
                            var support = Vector3.Lerp(a, b, along);
                            float squared = new Vector2(point.x - support.x, point.z - support.z).sqrMagnitude;
                            if (squared < .009025f) point.y = Mathf.Max(point.y, support.y + Mathf.Sqrt(.009025f - squared));
                        }
                }
                while (hull.Count >= 2)
                {
                    var a = hull[^2]; var b = hull[^1];
                    float ab = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                    float bc = Vector2.Distance(new Vector2(b.x, b.z), new Vector2(point.x, point.z));
                    if ((b.y - a.y) * bc > (point.y - b.y) * ab + .00001f) break;
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(point);
            }
            return hull;
        }
        static void AlignHarpoon(Mesh mesh)
        {
            var vertices = mesh.vertices;
            float min = vertices.Min(v => v.z), max = vertices.Max(v => v.z);
            var tip = vertices.Where(v => v.z < min + .035f).Aggregate(Vector3.zero, (a, b) => a + b) / vertices.Count(v => v.z < min + .035f);
            var tail = vertices.Where(v => v.z > max - .08f).Aggregate(Vector3.zero, (a, b) => a + b) / vertices.Count(v => v.z > max - .08f);
            var rotation = Quaternion.FromToRotation((tail - tip).normalized, Vector3.forward);
            mesh.vertices = vertices.Select(v => rotation * (v - tip)).ToArray(); mesh.normals = mesh.normals.Select(n => rotation * n).ToArray();
            var tangents = mesh.tangents;
            for (int i = 0; i < tangents.Length; i++) { var t = rotation * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z); tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w); }
            mesh.tangents = tangents; mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        }
        static void Tube(Transform parent, string name, Vector3[] points, float radius, UnityEngine.Material material)
        {
            var root = Child(parent, name);
            var tube = root.gameObject.AddComponent<SeaRopeVisual>(); tube.SetPath(points, points.Length, radius);
            var mesh = Object.Instantiate(root.GetComponent<MeshFilter>().sharedMesh); mesh.name = name;
            string path = Folder + "/Harpoon/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            Object.DestroyImmediate(tube); root.GetComponent<MeshFilter>().sharedMesh = saved; root.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        static float Height(MeshCollider collider, float x, float z) => collider.Raycast(new Ray(new Vector3(x, 25, z), Vector3.down), out var hit, 30) ? hit.point.y : 0;
        static Mesh NormalizedMesh(string path, string output, Quaternion rotation, Vector3 offset)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>();
            var mesh = Object.Instantiate(source.sharedMesh); mesh.name = System.IO.Path.GetFileNameWithoutExtension(output);
            var matrix = Matrix4x4.TRS(offset, rotation, Vector3.one) * source.transform.localToWorldMatrix;
            mesh.vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray(); mesh.normals = mesh.normals.Select(n => matrix.MultiplyVector(n).normalized).ToArray();
            var tangents = mesh.tangents; for (int i = 0; i < tangents.Length; i++) { Vector3 t = matrix.MultiplyVector(tangents[i]).normalized; tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w); } mesh.tangents = tangents; mesh.RecalculateBounds();
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(output); if (old == null) { AssetDatabase.CreateAsset(mesh, output); return mesh; } EditorUtility.CopySerialized(mesh, old); Object.DestroyImmediate(mesh); return old;
        }
        static UnityEngine.Material Material(string folder, string stem)
        {
            string[] assets = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string Path(string suffix) => assets.First(p => System.IO.Path.GetFileNameWithoutExtension(p) == stem + "_" + suffix);
            foreach (var key in new[] { "basecolor", "normal", "metallic", "roughness" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Path(key)); importer.textureType = key == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default; importer.sRGBTexture = key == "basecolor"; importer.isReadable = key == "metallic" || key == "roughness"; importer.maxTextureSize = 2048; importer.SaveAndReimport();
            }
            var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(Path("metallic")); var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(Path("roughness"));
            var packed = new Texture2D(metal.width, metal.height, TextureFormat.RGBA32, false, true); var colors = new Color[metal.width * metal.height];
            for (int y = 0; y < metal.height; y++) for (int x = 0; x < metal.width; x++) { float m = metal.GetPixel(x, y).r, r = rough.GetPixelBilinear((x + .5f) / metal.width, (y + .5f) / metal.height).r; colors[y * metal.width + x] = new Color(m, m, m, 1 - r); }
            packed.SetPixels(colors); packed.Apply(); string packedPath = folder + "/MetalSmooth.png"; System.IO.File.WriteAllBytes(packedPath, packed.EncodeToPNG()); Object.DestroyImmediate(packed); AssetDatabase.ImportAsset(packedPath);
            var pi = (TextureImporter)AssetImporter.GetAtPath(packedPath); pi.sRGBTexture = false; pi.SaveAndReimport();
            string materialPath = folder + "/EventSkin.mat"; var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(materialPath);
            if (material == null) { material = new UnityEngine.Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Path("basecolor"))); material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Path("normal"))); material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath)); material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", .7f); material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
    }
}
