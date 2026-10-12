using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PirateSlop.Editor
{
    public static class SeagullKeyEventSetup
    {
        const string Folder = "Assets/Models/SeaEvents/SeagullKey/";
        const string PrefabPath = "Assets/Resources/SeaEvents/SeagullKeyEvent.prefab";
        static Mesh timber;
        static Material oak, iron, brass, dark;
        static readonly Dictionary<Vector3, Mesh> timberMeshes = new();

        [MenuItem("PirateSlop/Art/Prepare Seagull Key Event")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var source = PrefabUtility.LoadPrefabContents("Assets/Resources/SeaEvents/RiggingEvent.prefab");
            var root = new GameObject("SeagullKeyWreckEvent");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                timberMeshes.Clear();
                var materials = source.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToArray();
                oak = materials.First(m => m.name.Contains("Deck_Worn"));
                iron = materials.First(m => m.name.Contains("Iron") || m.name.Contains("Chainplate"));
                brass = Material(iron, "WeatheredBrass", new Color(.58f, .36f, .095f), .85f, .5f);
                dark = Material(iron, "LockKeyhole", new Color(.025f, .021f, .017f), .1f, .2f);
                timber = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/SeaEvents/CannonBreach/ShipOak.asset");
                if (timber == null) throw new InvalidOperationException("Prepared ship oak geometry is required.");
                var network = root.AddComponent<NetworkObject>(); network.SetAssetPathHash(PrefabPath.GetStableHashU64());
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeAll;
                var owner = root.AddComponent<NetworkSeagullEvent>(); owner.Catalog = catalog; owner.PlatformRadius = 30;
                owner.KeyPrefab = PrepareKey(); owner.PadlockPrefab = PreparePadlock();
                var originalWreck = source.transform.Find("BrokenShipV3");
                var wreck = Child(root.transform, "Shipwreck", originalWreck.localPosition);
                wreck.localRotation = originalWreck.localRotation;
                foreach (string part in new[] { "RemainingHull", "RottenSternRails" })
                {
                    var original = originalWreck.Find(part);
                    var copy = Object.Instantiate(original.gameObject, wreck, false);
                    copy.name = part;
                    foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true).ToArray())
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        if (renderer == null || renderer.bounds.max.y <= 9.6f) continue;
                        Object.DestroyImmediate(filter.gameObject);
                    }
                }
                CutBoardingPassages(root);
                float deckHeight = Surface(root, new Vector3(0, 30, -8));
                if (deckHeight < .5f || deckHeight > 7) throw new InvalidOperationException("Wreck deck surface was not found.");
                float pedestal = deckHeight + .16f;
                for (int i = 0; i < 4; i++) Beam(root.transform, "ChestPlinthBoard_" + i, new Vector3(0, pedestal - .065f, -8 + (i - 1.5f) * .47f), new Vector3(2f, .13f, .45f), Quaternion.identity, true);
                owner.ChestSocket = Child(root.transform, "VisibleChestSocket", new Vector3(0, pedestal, -8));
                for (int side = -1; side <= 1; side += 2)
                {
                    var start = new Vector3(side * 15, -.42f, -8);
                    float DeckAt(float x) => deckHeight - wreck.up.x * x / Mathf.Max(.1f, wreck.up.y);
                    var end = new Vector3(side * 4, DeckAt(side * 4) + .65f, -8);
                    var direction = end - start;
                    var rotation = Quaternion.FromToRotation(Vector3.right, direction);
                    for (int segment = 0; segment < 8; segment++)
                        for (int lane = 0; lane < 5; lane++)
                        {
                            var point = Vector3.Lerp(start, end, (segment + .5f) / 8) + Vector3.forward * ((lane - 2) * .49f);
                            Beam(root.transform, "BoardingRamp_" + side + "_" + segment + "_" + lane, point,
                                new Vector3(direction.magnitude / 8 + .02f, .16f, .47f), rotation, true);
                        }
                    for (int lane = -1; lane <= 1; lane += 2)
                        Beam(root.transform, "RampUnderbeam_" + side + "_" + lane, (start + end) * .5f + new Vector3(0, -.2f, lane * .94f),
                            new Vector3(direction.magnitude, .23f, .25f), rotation, false);
                    var landing = new Vector3(side * 1.2f, DeckAt(side * 1.2f) + .14f, -8);
                    var landingDirection = landing - end;
                    for (int segment = 0; segment < 3; segment++) for (int lane = 0; lane < 5; lane++)
                        Beam(root.transform, "DeckWalkway_" + side + "_" + segment + "_" + lane,
                            Vector3.Lerp(end, landing, (segment + .5f) / 3) + Vector3.forward * ((lane - 2) * .49f),
                            new Vector3(landingDirection.magnitude / 3 + .03f, .16f, .47f), Quaternion.FromToRotation(Vector3.right, landingDirection), true);
                    var support = new Vector3(side * 8.5f, .9f, -8);
                    Beam(root.transform, "RampBrokenRib_" + side, support, new Vector3(.28f, 3.1f, .3f), Quaternion.Euler(0, 0, side * -12), false);
                }
                var mastMaterial = materials.First(m => m.name.Contains("Mast_Carved_Shaft"));
                var mast = SaveMesh(TaperedMast(), "BrokenMast");
                var fallen = Model(root.transform, "FallenMast", mast, mastMaterial, new Vector3(1, 1.8f, 3), Quaternion.Euler(16, 0, 61), Vector3.one, true);
                for (int i = 0; i < 3; i++) Beam(fallen, "Splinter_" + i, new Vector3((i - 1) * .18f, 6.4f + i * .14f, .12f), new Vector3(.16f, 1.7f - i * .22f, .18f), Quaternion.Euler(0, 0, i * 7 - 6), false);
                Beam(root.transform, "BrokenYard", new Vector3(-2, .65f, 11), new Vector3(9, .27f, .34f), Quaternion.Euler(4, 32, -7), true);
                var sail = source.transform.Find("BrokenShipV3/RemainingHull/Torn_V3_Transfer_Outfit_Sail_Mizzen");
                if (sail != null)
                {
                    var cloth = Object.Instantiate(sail.gameObject, fallen, false);
                    cloth.name = "DrapedTornSail";
                    var filter = cloth.GetComponent<MeshFilter>();
                    var mesh = Object.Instantiate(filter.sharedMesh); mesh.name = "DrapedSail";
                    var bounds = mesh.bounds; var vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var v = vertices[i] - bounds.center;
                        float u = v.y / Mathf.Max(.01f, bounds.size.y) + .5f;
                        vertices[i] = new Vector3(v.x * .9f, u * 4 - 2, v.z * .7f + Mathf.Sin(u * Mathf.PI) * .7f);
                    }
                    mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    filter.sharedMesh = SaveMesh(mesh, "DrapedSail");
                    cloth.transform.localPosition = new Vector3(.45f, 2.4f, .2f);
                    cloth.transform.localRotation = Quaternion.Euler(0, 18, -18); cloth.transform.localScale = Vector3.one;
                    foreach (var collider in cloth.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                }
                for (int i = 0; i < 7; i++)
                    Beam(root.transform, "WreckagePlank_" + i, new Vector3((i % 2 == 0 ? -1 : 1) * (8 + i * .7f), -.12f + i % 3 * .09f, 5 + i * 2.2f),
                        new Vector3(2.4f + i % 3 * .6f, .14f, .36f), Quaternion.Euler(i % 2 * 4, i * 31, i % 3 * 5), false);
                var preview = Object.Instantiate(catalog.ChestPrefab.gameObject, root.transform, false);
                preview.name = "LockedChestEditorPreview"; preview.transform.SetPositionAndRotation(owner.ChestSocket.position, owner.ChestSocket.rotation);
                Object.DestroyImmediate(preview.GetComponent<NetworkLootChest>()); Object.DestroyImmediate(preview.GetComponent<NetworkObject>());
                foreach (var behaviour in preview.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var rigidbody in preview.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rigidbody);
                preview.AddComponent<RiggingEditorPreview>();
                var padlock = Object.Instantiate(owner.PadlockPrefab, preview.transform, false); padlock.transform.localPosition = new Vector3(0, .335f, .337f);
                foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = LayerMask.NameToLayer("WorldStatic");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source); Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            registry.AddObject(prefab.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(registry);
            catalog.SeagullEventPrefab = prefab.GetComponent<NetworkSeagullEvent>(); EditorUtility.SetDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            var defaults = ScriptableObject.CreateInstance<SessionConfig>();
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, defaults.ProtocolVersion); EditorUtility.SetDirty(config); Object.DestroyImmediate(defaults);
            AssetDatabase.SaveAssets();
            Debug.Log("Seagull key event saved with shipwreck, two boarding ramps, locked chest and key art.");
        }
        static float Surface(GameObject root, Vector3 from)
        {
            float height = float.NegativeInfinity;
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>())
                if (collider.Raycast(new Ray(from, Vector3.down), out var hit, 80)) height = Mathf.Max(height, hit.point.y);
            return height;
        }
        static void CutBoardingPassages(GameObject root)
        {
            var openings = new[] { new Bounds(new Vector3(-6.1f, 3.1f, -8), new Vector3(3.8f, 6, 3)), new Bounds(new Vector3(6.1f, 3.1f, -8), new Vector3(3.8f, 6, 3)) };
            int index = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>().ToArray())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !openings.Any(b => b.Intersects(renderer.bounds))) continue;
                var original = filter.sharedMesh;
                var vertices = original.vertices;
                var points = vertices.Select(v => root.transform.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
                var mesh = Object.Instantiate(original); bool changed = false;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var triangles = mesh.GetTriangles(sub); var remaining = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var bounds = new Bounds(points[triangles[i]], Vector3.zero);
                        bounds.Encapsulate(points[triangles[i + 1]]); bounds.Encapsulate(points[triangles[i + 2]]);
                        if (openings.Any(b => b.Intersects(bounds))) { changed = true; continue; }
                        remaining.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
                    }
                    mesh.SetTriangles(remaining, sub);
                }
                if (!changed) { Object.DestroyImmediate(mesh); continue; }
                if (Enumerable.Range(0, mesh.subMeshCount).All(sub => mesh.GetIndexCount(sub) == 0))
                { Object.DestroyImmediate(mesh); Object.DestroyImmediate(filter.gameObject); continue; }
                bool collision = filter.GetComponentsInChildren<Collider>().Length > 0;
                foreach (var collider in filter.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                mesh.RecalculateBounds(); filter.sharedMesh = SaveMesh(mesh, "BreachedHull_" + index++);
                if (collision) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }
        static Transform Child(Transform parent, string name, Vector3 point)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = point; return child;
        }
        static Transform Beam(Transform parent, string name, Vector3 point, Vector3 size, Quaternion rotation, bool collision)
        {
            var normalized = Matrix4x4.TRS(-timber.bounds.center, Quaternion.identity, Vector3.one);
            var go = Child(parent, name, point); go.localRotation = rotation;
            if (!timberMeshes.TryGetValue(size, out var mesh))
            {
                mesh = Object.Instantiate(timber);
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(normalized.MultiplyPoint3x4(vertices[i]), new Vector3(size.x / timber.bounds.size.x, size.y / timber.bounds.size.y, size.z / timber.bounds.size.z));
                mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                mesh = SaveMesh(mesh, "WreckTimber_" + timberMeshes.Count); timberMeshes[size] = mesh;
            }
            go.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; go.gameObject.AddComponent<MeshRenderer>().sharedMaterial = oak;
            if (collision) go.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
        static Transform Model(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale, bool collision = false)
        {
            var go = Child(parent, name, position); go.localRotation = rotation; go.localScale = scale;
            go.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; go.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) go.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
        static Material Material(Material source, string name, Color color, float metallic, float smoothness)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_BaseMap", null); material.SetTexture("_BumpMap", null); material.SetTexture("_MetallicGlossMap", null);
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            material.DisableKeyword("_NORMALMAP"); material.DisableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material); return material;
        }
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            mesh.name = name; string path = Folder + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            existing.Clear(); existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.uv = mesh.uv;
            existing.tangents = mesh.tangents; existing.uv2 = mesh.uv2; existing.colors = mesh.colors;
            existing.subMeshCount = mesh.subMeshCount;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) existing.SetTriangles(mesh.GetTriangles(sub), sub);
            existing.RecalculateBounds(); existing.UploadMeshData(false); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); return existing;
        }
        static GameObject PrepareKey()
        {
            var root = new GameObject("WreckChestKey");
            try
            {
                var points = new List<Vector3>();
                for (int i = 0; i <= 32; i++) { float a = i * Mathf.PI * 2 / 32; points.Add(new Vector3(Mathf.Sin(a) * .065f, -.065f + Mathf.Cos(a) * .065f, 0)); }
                Model(root.transform, "KeyBow", SaveMesh(Tube(points, .012f), "KeyBow"), brass, Vector3.zero, Quaternion.identity, Vector3.one);
                var shaft = new List<Vector3> { new(0, -.12f, 0), new(0, -.37f, 0) };
                Model(root.transform, "KeyShaft", SaveMesh(Tube(shaft, .013f), "KeyShaft"), brass, Vector3.zero, Quaternion.identity, Vector3.one);
                var tooth = SaveMesh(BeveledBox(.07f, .022f, .025f, .004f), "KeyTooth");
                for (int i = 0; i < 3; i++) Model(root.transform, "KeyTooth_" + i, tooth, brass, new Vector3(.027f, -.29f - i * .033f, 0), Quaternion.identity, Vector3.one);
                var collar = SaveMesh(BeveledBox(.046f, .025f, .035f, .005f), "KeyCollar");
                Model(root.transform, "KeyCollar", collar, brass, new Vector3(0, -.135f, 0), Quaternion.identity, Vector3.one);
                var collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.center = new Vector3(.025f, -.17f, 0); collider.size = new Vector3(.22f, .43f, .18f);
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "WreckChestKey.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        static GameObject PreparePadlock()
        {
            var root = new GameObject("WreckChestPadlock");
            try
            {
                Model(root.transform, "ForgedLockBody", SaveMesh(BeveledBox(.18f, .15f, .085f, .022f), "LockBody"), iron, new Vector3(0, -.14f, 0), Quaternion.identity, Vector3.one);
                Model(root.transform, "BrassEscutcheon", SaveMesh(BeveledBox(.117f, .10f, .007f, .017f), "LockFace"), brass, new Vector3(0, -.14f, .044f), Quaternion.identity, Vector3.one);
                var points = new List<Vector3> { new(-.06f, -.094f, 0), new(-.06f, -.046f, 0) };
                for (int i = 0; i <= 18; i++) { float a = Mathf.PI - i * Mathf.PI / 18; points.Add(new Vector3(Mathf.Cos(a) * .06f, -.046f + Mathf.Sin(a) * .065f, 0)); }
                points.Add(new Vector3(.06f, -.094f, 0));
                Model(root.transform, "IronShackle", SaveMesh(Tube(points, .012f), "LockShackle"), iron, Vector3.zero, Quaternion.identity, Vector3.one);
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var hole = sphere.GetComponent<MeshFilter>().sharedMesh;
                Model(root.transform, "KeyholeEye", hole, dark, new Vector3(0, -.127f, .050f), Quaternion.identity, new Vector3(.026f, .026f, .003f));
                Model(root.transform, "KeyholeStem", SaveMesh(BeveledBox(.012f, .028f, .003f, .001f), "KeyholeStem"), dark, new Vector3(0, -.148f, .050f), Quaternion.identity, Vector3.one);
                foreach (int side in new[] { -1, 1 }) Model(root.transform, "FaceRivet_" + side, hole, iron, new Vector3(side * .041f, -.14f, .051f), Quaternion.identity, new Vector3(.009f, .009f, .005f));
                Object.DestroyImmediate(sphere);
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "WreckChestPadlock.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        static Mesh BeveledBox(float width, float height, float depth, float bevel)
        {
            float x = width * .5f, y = height * .5f;
            var ring = new[] { new Vector2(-x + bevel, -y), new Vector2(x - bevel, -y), new Vector2(x, -y + bevel), new Vector2(x, y - bevel), new Vector2(x - bevel, y), new Vector2(-x + bevel, y), new Vector2(-x, y - bevel), new Vector2(-x, -y + bevel) };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int side = 0; side < 2; side++) for (int i = 0; i < ring.Length; i++) vertices.Add(new Vector3(ring[i].x, ring[i].y, (side == 0 ? -1 : 1) * depth * .5f));
            for (int i = 1; i < 7; i++) { triangles.AddRange(new[] { 0, i + 1, i, 8, 8 + i, 9 + i }); }
            for (int i = 0; i < 8; i++) { int n = (i + 1) % 8; triangles.AddRange(new[] { i, n, n + 8, i, n + 8, i + 8 }); }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh Tube(IReadOnlyList<Vector3> points, float radius)
        {
            const int sides = 8;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                var tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                var side = Vector3.Cross(tangent, Vector3.forward).normalized;
                for (int j = 0; j < sides; j++) { float a = j * Mathf.PI * 2 / sides; vertices.Add(points[i] + (side * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a)) * radius); }
                if (i == 0) continue;
                for (int j = 0; j < sides; j++) { int a = (i - 1) * sides + j, b = (i - 1) * sides + (j + 1) % sides; triangles.AddRange(new[] { a, b + sides, b, a, a + sides, b + sides }); }
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh TaperedMast()
        {
            const int sides = 12;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int row = 0; row < 3; row++) for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides; float radius = .3f - row * .045f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, row == 2 ? 6.9f + Mathf.Sin(i * 2.7f) * .19f : row * 3.2f, Mathf.Sin(angle) * radius)); uv.Add(new Vector2(i / (float)sides, row * 2));
                if (row == 0 || i == sides) continue;
                int a = (row - 1) * (sides + 1) + i, b = a + 1;
                triangles.AddRange(new[] { a, a + sides + 1, b + sides + 1, a, b + sides + 1, b });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
