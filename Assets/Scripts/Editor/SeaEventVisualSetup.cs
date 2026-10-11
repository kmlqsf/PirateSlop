using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class SeaEventVisualSetup
    {
        const string Folder = "Assets/Resources/SeaEvents/";
        static Material hemp, iron, timber, cloth;
        static readonly List<Mesh> temporary = new();

        public static string Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
            if (ship == null) throw new InvalidOperationException("ShipV3Test is required.");
            hemp = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/ShipV3/Materials/Ship_Hemp_Rope_PBR.001.mat");
            iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/ShipV3/Materials/V3_Lantern_Bracket_ForgedIron.mat");
            timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/ShipV3/Materials/V4_Frame_Ship_Art_Frame_Warm_Timber.mat");
            if (hemp == null || iron == null || timber == null) throw new InvalidOperationException("Ship materials are required.");
            cloth = AssetDatabase.LoadAssetAtPath<Material>(Folder + "SignalCanvas.mat");
            if (cloth == null) { cloth = new Material(timber.shader); AssetDatabase.CreateAsset(cloth, Folder + "SignalCanvas.mat"); }
            cloth.SetColor("_BaseColor", new Color(.8f, .68f, .43f));
            cloth.SetFloat("_Metallic", 0f); cloth.SetFloat("_Smoothness", .1f); cloth.SetFloat("_Cull", 0f);
            cloth.enableInstancing = true;
            EditorUtility.SetDirty(cloth);
            var filters = ship.GetComponentsInChildren<MeshFilter>(true);
            var sources = new List<MeshFilter>();
            foreach (var f in filters)
                if (f.name.StartsWith("V3_Deck_Quarterdeck_", StringComparison.Ordinal) && f.sharedMesh.vertexCount > 100 && f.sharedMesh.bounds.size.y > .03f)
                {
                    sources.Add(f);
                    if (sources.Count == 4) break;
                }
            if (sources.Count < 4) throw new InvalidOperationException("Four complete ship deck planks are required.");
            MeshFilter stem = Array.Find(filters, f => f.name == "V3_ForeStem");
            if (stem == null) throw new InvalidOperationException("Ship fore stem is required.");
            var boards = new Mesh[4]; var boardMaterials = new Material[4];
            for (int i = 0; i < boards.Length; i++)
            {
                boards[i] = CopyOriented(sources[i], "SalvagedDeck" + i);
                boardMaterials[i] = sources[i].GetComponent<MeshRenderer>().sharedMaterial;
            }
            var beam = CopyOriented(stem, "SalvagedTimber");
            var art = AssetDatabase.LoadAssetAtPath<SeaEventArt>(Folder + "SeaEventArt.asset");
            if (art == null) { art = ScriptableObject.CreateInstance<SeaEventArt>(); AssetDatabase.CreateAsset(art, Folder + "SeaEventArt.asset"); }
            art.Hemp = hemp;
            BakeChestSurface(art);
            var raft = new GameObject("SalvagedShipRaft");
            try
            {
                for (int i = 0; i < 7; i++)
                    Fit(raft.transform, "ShipDeckPlank" + i, boards[i % 4], boardMaterials[i % 4], new Vector3((i - 3) * .55f, .345f, (i % 3 - 1) * .035f), new Vector3(.535f, .16f, 3.94f));
                foreach (float z in new[] { -1.27f, 1.27f })
                {
                    Fit(raft.transform, "ShipCrossTimber", beam, timber, new Vector3(0, .13f, z), new Vector3(3.86f, .27f, .3f));
                    foreach (float x in new[] { -1.63f, 1.63f })
                    {
                        var points = new Vector3[65];
                        for (int p = 0; p < points.Length; p++)
                        {
                            float a = p / 64f * Mathf.PI * 6f;
                            points[p] = new Vector3(x + (p / 64f - .5f) * .17f, .25f + Mathf.Cos(a) * .206f, z + Mathf.Sin(a) * .18f);
                        }
                        Tube(raft.transform, "HempLashing", points, .024f, hemp);
                    }
                }
                Fit(raft.transform, "PortSalvagedRail", beam, timber, new Vector3(-1.855f, .155f, 0), new Vector3(.17f, .23f, 3.95f));
                Fit(raft.transform, "StarboardSalvagedRail", beam, timber, new Vector3(1.855f, .155f, 0), new Vector3(.17f, .23f, 3.95f));
                Batch(raft, "Raft");
                var floor = raft.AddComponent<BoxCollider>(); floor.center = new Vector3(0, .22f, 0); floor.size = new Vector3(3.83f, .41f, 4f);
                art.Raft = PrefabUtility.SaveAsPrefabAsset(raft, Folder + "Raft.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(raft); }
            var station = new GameObject("CaptureSignalStation");
            try
            {
                var baseRaft = UnityEngine.Object.Instantiate(art.Raft, station.transform);
                baseRaft.transform.localScale = Vector3.one * .72f;
                UnityEngine.Object.DestroyImmediate(baseRaft.GetComponent<Collider>());
                for (int i = 0; i < 3; i++)
                    Fit(station.transform, "SignalMastSection" + i, beam, timber, new Vector3(0, 1.65f + i * 2.55f, 0), new Vector3(.22f - i * .025f, 2.62f, .22f - i * .025f));
                for (int i = 0; i < 9; i++)
                    Fit(station.transform, "MastLadderRung", beam, timber, new Vector3(0, 1.1f + i * .72f, -.15f), new Vector3(.68f, .055f, .07f));
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * .5f;
                    var p = new Vector3(Mathf.Cos(a) * 1.04f, .35f, Mathf.Sin(a) * 1.04f);
                    Tube(station.transform, "MastStay", new[] { p, new Vector3(0, 2.3f, 0) }, .015f, hemp);
                }
                HeadPulley(station.transform, 8f);
                Batch(station, "Station");
                Flag(station.transform, "CaptureFlag", 2.8f, 1.65f, new Vector3(.16f, 1.35f, 0));
                Flag(station.transform, "ContestPennant", 1.4f, .46f, new Vector3(.11f, 7.65f, 0));
                BuildSignalRig(station, "CaptureFlag", 1.35f, 6.3f, 8f, 1.45f);
                station.transform.localScale = Vector3.one * 1.8f;
                art.CaptureStation = PrefabUtility.SaveAsPrefabAsset(station, Folder + "CaptureStation.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(station); }
            var buoy = new GameObject("CaptureBoundaryBuoy");
            try
            {
                var staves = new Mesh[4];
                for (int i = 0; i < staves.Length; i++) staves[i] = BuoyStave(boards[i], i);
                for (int i = 0; i < 12; i++)
                {
                    var stave = new GameObject("ShipOakFloatStave" + i); stave.transform.SetParent(buoy.transform, false); stave.transform.localRotation = Quaternion.Euler(0, i * 30f, 0);
                    stave.AddComponent<MeshFilter>().sharedMesh = staves[i % 4]; stave.AddComponent<MeshRenderer>().sharedMaterial = boardMaterials[i % 4];
                }
                foreach (float y in new[] { -.42f, .19f, .84f })
                {
                    float radius = FloatRadius(y) + .052f;
                    Lathe(buoy.transform, "ForgedFlatHoop", new[] { new Vector2(radius - .012f, y - .055f), new Vector2(radius + .012f, y - .055f), new Vector2(radius + .012f, y + .055f), new Vector2(radius - .012f, y + .055f), new Vector2(radius - .012f, y - .055f) }, 48, iron, false);
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = i * Mathf.PI / 6f;
                        var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                        var rivet = Lathe(buoy.transform, "HoopRivet", new[] { new Vector2(.023f, 0), new Vector2(.025f, .01f), new Vector2(.016f, .024f), new Vector2(0, .029f) }, 8, iron);
                        rivet.SetLocalPositionAndRotation(radial * (radius + .01f) + Vector3.up * y, Quaternion.FromToRotation(Vector3.up, radial));
                    }
                }
                for (int i = 0; i < 5; i++)
                {
                    float x = (i - 2) * .32f, length = Mathf.Sqrt(.84f * .84f - x * x) * 2f;
                    Fit(buoy.transform, "OakPlatformPlank", boards[i % 4], boardMaterials[i % 4], new Vector3(x, .99f, 0), new Vector3(.305f, .12f, length));
                    Fit(buoy.transform, "OakFloatBottom", boards[(i + 1) % 4], boardMaterials[(i + 1) % 4], new Vector3(x * .76f, -.67f, 0), new Vector3(.24f, .09f, length * .78f));
                }
                Fit(buoy.transform, "ShipMarkerPole", beam, timber, new Vector3(0, 2.56f, 0), new Vector3(.19f, 3.3f, .19f));
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * .5f;
                    var foot = new Vector3(Mathf.Cos(angle) * .64f, 1.08f, Mathf.Sin(angle) * .64f);
                    var tip = new Vector3(Mathf.Cos(angle) * .11f, 1.9f, Mathf.Sin(angle) * .11f);
                    var brace = new GameObject("MastOakBrace"); brace.transform.SetParent(buoy.transform, false); brace.transform.localPosition = (foot + tip) * .5f; brace.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (tip - foot).normalized);
                    Fit(brace.transform, "ShipTimberBrace", beam, timber, Vector3.zero, new Vector3(.11f, Vector3.Distance(foot, tip), .13f));
                    Primitive(buoy.transform, "BraceIronShoe", PrimitiveType.Cube, foot, new Vector3(.18f, .045f, .18f), Quaternion.Euler(0, i * 90f, 0), iron);
                    var rivet = Lathe(buoy.transform, "ShoeRivet", new[] { new Vector2(.026f, 0), new Vector2(.028f, .012f), new Vector2(.017f, .028f), new Vector2(0, .033f) }, 8, iron);
                    rivet.localPosition = foot + Vector3.up * .023f;
                }
                var lashing = new Vector3[97];
                for (int i = 0; i < lashing.Length; i++) { float angle = i / 96f * Mathf.PI * 8f; lashing[i] = new Vector3(Mathf.Cos(angle) * .145f, 1.85f + i / 96f * .24f, Mathf.Sin(angle) * .145f); }
                Tube(buoy.transform, "MastHempSeizing", lashing, .024f, hemp);
                var mooring = new Vector3[33];
                for (int i = 0; i < mooring.Length; i++) { float angle = i / 32f * Mathf.PI * 2f; mooring[i] = new Vector3(Mathf.Sin(angle) * .15f, .82f + Mathf.Cos(angle) * .15f, -.78f); }
                Tube(buoy.transform, "MooringIronEye", mooring, .027f, iron);
                Fit(buoy.transform, "MastHeadCrosspiece", beam, timber, new Vector3(0, 4.07f, 0), new Vector3(.65f, .1f, .12f));
                HeadPulley(buoy.transform, 4.2f);
                Batch(buoy, "Buoy");
                Flag(buoy.transform, "BoundaryFlag", 1.6f, .9f, new Vector3(.16f, 1.4f, 0));
                Flag(buoy.transform, "ContestPennant", .85f, .3f, new Vector3(.1f, 3.87f, 0));
                BuildSignalRig(buoy, "BoundaryFlag", 1.4f, 3.05f, 4.2f, .76f, 1.25f);
                buoy.transform.localScale = Vector3.one * 2.2f;
                art.CaptureBuoy = PrefabUtility.SaveAsPrefabAsset(buoy, Folder + "CaptureBuoy.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(buoy); }
            var shackle = new GameObject("ForgedRopeShackle");
            try
            {
                var bow = new Vector3[25];
                for (int i = 0; i < bow.Length; i++) { float a = i / 24f * Mathf.PI; bow[i] = new Vector3(0, Mathf.Cos(a) * .053f, .023f + Mathf.Sin(a) * .08f); }
                Tube(shackle.transform, "ForgedBow", bow, .011f, iron);
                Tube(shackle.transform, "ClevisPin", new[] { new Vector3(0, -.065f, .023f), new Vector3(0, .065f, .023f) }, .013f, iron);
                foreach (float y in new[] { -.053f, .053f })
                {
                    Primitive(shackle.transform, "RivetedEyePlate", PrimitiveType.Cylinder, new Vector3(0, y, .006f), new Vector3(.073f, .006f, .073f), Quaternion.Euler(90, 0, 0), iron);
                    Primitive(shackle.transform, "PinHead", PrimitiveType.Sphere, new Vector3(0, y * 1.19f, .023f), new Vector3(.034f, .018f, .034f), Quaternion.identity, iron);
                    foreach (float x in new[] { -.022f, .022f })
                        Primitive(shackle.transform, "ShipRivet", PrimitiveType.Sphere, new Vector3(x, y, .012f), new Vector3(.012f, .012f, .008f), Quaternion.identity, iron);
                }
                Batch(shackle, "Shackle");
                art.RopeShackle = PrefabUtility.SaveAsPrefabAsset(shackle, Folder + "RopeShackle.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(shackle); }
            var grip = new GameObject("HempStopperKnot");
            try
            {
                var points = new Vector3[65];
                for (int i = 0; i < points.Length; i++) { float a = i / 64f * Mathf.PI * 4f; points[i] = new Vector3(Mathf.Cos(a) * .03f, (i / 64f - .5f) * .095f, Mathf.Sin(a) * .03f); }
                Tube(grip.transform, "StopperKnot", points, .023f, hemp);
                Batch(grip, "Grip");
                var collider = grip.AddComponent<SphereCollider>(); collider.radius = .09f;
                art.RopeGrip = PrefabUtility.SaveAsPrefabAsset(grip, Folder + "RopeGrip.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(grip); }
            EditorUtility.SetDirty(art); AssetDatabase.SaveAssets();
            foreach (var mesh in temporary) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            temporary.Clear();
            return "Saved ship raft, capture station, boundary buoys, rope shackles and hemp knot.";
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = Folder + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); return saved;
        }

        static void BakeChestSurface(SeaEventArt art)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkLootChest.prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var shape = root.GetComponent<BoxCollider>();
                art.ChestRopeMinY = shape.center.y - shape.size.y * .5f + .065f;
                art.ChestRopeMaxY = shape.center.y + shape.size.y * .5f - .045f;
                var colliders = new List<MeshCollider>();
                foreach (var f in root.transform.Find("ChestVisual/ChestBase").GetComponentsInChildren<MeshFilter>())
                {
                    var collider = f.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = f.sharedMesh; colliders.Add(collider);
                }
                const int rows = 17, columns = 256;
                art.ChestRopeSurface = new Vector3[rows * columns]; art.ChestRopeNormals = new Vector3[rows * columns];
                int misses = 0;
                for (int row = 0; row < rows; row++) for (int column = 0; column < columns; column++)
                {
                    float angle = column * Mathf.PI * 2f / columns;
                    var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var center = new Vector3(shape.center.x, Mathf.Lerp(art.ChestRopeMinY, art.ChestRopeMaxY, row / (float)(rows - 1)), shape.center.z);
                    var ray = new Ray(center + radial * 2f, -radial);
                    float nearest = 2f; RaycastHit best = default;
                    foreach (var collider in colliders)
                        if (collider.Raycast(ray, out var hit, 2f) && hit.distance < nearest) { nearest = hit.distance; best = hit; }
                    if (best.collider == null) { misses++; continue; }
                    int index = row * columns + column;
                    art.ChestRopeSurface[index] = best.point; art.ChestRopeNormals[index] = best.normal;
                }
                if (misses > 0) throw new InvalidOperationException("Chest rope contact bake missed " + misses + " surface samples.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void HeadPulley(Transform parent, float height)
        {
            Primitive(parent, "ForgedHalyardSheave", PrimitiveType.Cylinder, new Vector3(0, height, 0), new Vector3(.58f, .045f, .58f), Quaternion.Euler(90, 0, 0), iron);
            Tube(parent, "PulleyBracket", new[] { new Vector3(-.3f, height - .24f, 0), new Vector3(-.3f, height + .1f, 0), new Vector3(.3f, height + .1f, 0), new Vector3(.3f, height - .24f, 0) }, .04f, iron);
        }

        static void BuildSignalRig(GameObject root, string flagName, float low, float high, float head, float attachmentHeight, float winchHeight = .65f)
        {
            var rig = root.AddComponent<SeaSignalRig>();
            rig.Flag = root.transform.Find(flagName); rig.Contest = root.transform.Find("ContestPennant");
            rig.Low = low; rig.High = high; rig.Head = head; rig.Hemp = hemp;
            var carriage = new GameObject("FlagCarriage"); carriage.transform.SetParent(root.transform, false);
            foreach (float y in new[] { 0f, attachmentHeight })
            {
                var ring = new Vector3[33];
                for (int p = 0; p < ring.Length; p++) { float a = p / 32f * Mathf.PI * 2f; ring[p] = new Vector3(Mathf.Cos(a) * .15f, y, Mathf.Sin(a) * .15f); }
                Tube(carriage.transform, "SlidingMastRing", ring, .026f, iron);
                Tube(carriage.transform, "FlagCornerTie", new[] { new Vector3(.15f, y, .04f), new Vector3(.16f, y, 0), new Vector3(.15f, y, -.04f) }, .016f, hemp);
            }
            Batch(carriage, root.name + "Carriage"); rig.Carriage = carriage.transform;
            var weight = new GameObject("HalyardCounterweight"); weight.transform.SetParent(root.transform, false);
            Primitive(weight.transform, "IronWeight", PrimitiveType.Capsule, Vector3.zero, new Vector3(.2f, .26f, .2f), Quaternion.identity, iron);
            Batch(weight, root.name + "Weight"); rig.Counterweight = weight.transform;
            var winch = new GameObject("HoistWinch"); winch.transform.SetParent(root.transform, false); winch.transform.localPosition = new Vector3(0, winchHeight, .28f);
            Primitive(winch.transform, "RatchetDrum", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.4f, .12f, .4f), Quaternion.identity, iron);
            Tube(winch.transform, "WinchCrank", new[] { new Vector3(0, .16f, 0), new Vector3(.32f, .16f, 0), new Vector3(.32f, .25f, 0) }, .032f, iron);
            Batch(winch, root.name + "Winch"); rig.Winch = winch.transform;
        }

        static Mesh CopyOriented(MeshFilter source, string name)
        {
            var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            var points = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
            var rotation = source.transform.rotation;
            Vector3 center = rotation * mesh.bounds.center;
            for (int i = 0; i < points.Length; i++) points[i] = rotation * points[i] * 100f - center * 100f;
            for (int i = 0; i < normals.Length; i++) normals[i] = rotation * normals[i];
            for (int i = 0; i < tangents.Length; i++) { Vector3 t = rotation * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z); tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w); }
            mesh.vertices = points; mesh.normals = normals; mesh.tangents = tangents; mesh.RecalculateBounds();
            return SaveMesh(mesh, name);
        }

        static float FloatRadius(float y) => .65f + Mathf.Sin(Mathf.Clamp01((y + .65f) / 1.7f) * Mathf.PI) * .16f;

        static Mesh BuoyStave(Mesh board, int index)
        {
            var mesh = UnityEngine.Object.Instantiate(board);
            var b = mesh.bounds.size;
            var axis = b.x >= b.y && b.x >= b.z ? Vector3.right : b.y >= b.z ? Vector3.up : Vector3.forward;
            var rotation = Quaternion.FromToRotation(axis, Vector3.up);
            var points = mesh.vertices;
            for (int i = 0; i < points.Length; i++) points[i] = rotation * points[i];
            mesh.vertices = points; mesh.RecalculateBounds(); var dimensions = mesh.bounds.size; var center = mesh.bounds.center;
            SubdivideStave(mesh, dimensions.y / 8f); points = mesh.vertices;
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i] - center;
                float y = p.y / dimensions.y * 1.7f + .2f;
                float radius = FloatRadius(y);
                points[i] = new Vector3(p.x / dimensions.x * ((radius + .045f) * 2f * Mathf.Tan(Mathf.PI / 12f) * .97f), y, radius + p.z / dimensions.z * .095f);
            }
            mesh.vertices = points; mesh.RecalculateBounds(); mesh.RecalculateNormals(); mesh.RecalculateTangents();
            return SaveMesh(mesh, "BuoyOakStave" + index);
        }

        static void SubdivideStave(Mesh mesh, float step)
        {
            var vertices = new List<Vector3>(mesh.vertices); var uv = new List<Vector2>(mesh.uv); var result = new List<int>();
            var edges = new Dictionary<(int, int), int>();
            int Midpoint(int a, int b)
            {
                var key = a < b ? (a, b) : (b, a);
                if (edges.TryGetValue(key, out int value)) return value;
                value = vertices.Count; vertices.Add((vertices[a] + vertices[b]) * .5f); uv.Add((uv[a] + uv[b]) * .5f); edges.Add(key, value); return value;
            }
            void Split(int a, int b, int c, int depth)
            {
                float ab = Mathf.Abs(vertices[a].y - vertices[b].y), bc = Mathf.Abs(vertices[b].y - vertices[c].y), ca = Mathf.Abs(vertices[c].y - vertices[a].y);
                if (depth >= 10 || Mathf.Max(ab, Mathf.Max(bc, ca)) <= step) { result.Add(a); result.Add(b); result.Add(c); return; }
                if (ab >= bc && ab >= ca) { int m = Midpoint(a, b); Split(a, m, c, depth + 1); Split(m, b, c, depth + 1); }
                else if (bc >= ca) { int m = Midpoint(b, c); Split(a, b, m, depth + 1); Split(a, m, c, depth + 1); }
                else { int m = Midpoint(c, a); Split(a, b, m, depth + 1); Split(m, b, c, depth + 1); }
            }
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3) Split(triangles[i], triangles[i + 1], triangles[i + 2], 0);
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(result, 0);
        }

        static void Fit(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 size)
        {
            var part = new GameObject(name); part.transform.SetParent(parent, false); part.transform.localPosition = position;
            var b = mesh.bounds.size;
            Vector3 sourceAxis = b.x >= b.y && b.x >= b.z ? Vector3.right : b.y >= b.z ? Vector3.up : Vector3.forward;
            Vector3 targetAxis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
            var rotation = Quaternion.FromToRotation(sourceAxis, targetAxis);
            var localSize = Quaternion.Inverse(rotation) * size;
            part.transform.localRotation = rotation;
            part.transform.localScale = new Vector3(Mathf.Abs(localSize.x) / b.x, Mathf.Abs(localSize.y) / b.y, Mathf.Abs(localSize.z) / b.z);
            part.AddComponent<MeshFilter>().sharedMesh = mesh; part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(position, rotation); part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>()); part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Tube(Transform parent, string name, Vector3[] points, float radius, Material material)
        {
            var part = new GameObject(name); part.transform.SetParent(parent, false);
            var tube = part.AddComponent<SeaRopeVisual>(); tube.GetComponent<MeshRenderer>().sharedMaterial = material;
            tube.SetPath(points, points.Length, radius);
            var mesh = UnityEngine.Object.Instantiate(part.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.DestroyImmediate(tube); part.GetComponent<MeshFilter>().sharedMesh = mesh; temporary.Add(mesh);
        }

        static void Batch(GameObject root, string key)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var sources = root.GetComponentsInChildren<MeshFilter>();
            foreach (var f in sources)
            {
                if (f.name.StartsWith("SignalMastSection", StringComparison.Ordinal)) continue;
                var renderer = f.GetComponent<MeshRenderer>();
                for (int sub = 0; sub < f.sharedMesh.subMeshCount; sub++)
                {
                    var material = renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)];
                    if (!groups.TryGetValue(material, out var group)) groups[material] = group = new List<CombineInstance>();
                    group.Add(new CombineInstance { mesh = f.sharedMesh, subMeshIndex = sub, transform = root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix });
                }
            }
            foreach (var f in sources) if (!f.name.StartsWith("SignalMastSection", StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(f.gameObject);
            int index = 0;
            foreach (var group in groups)
            {
                var mesh = new Mesh(); mesh.CombineMeshes(group.Value.ToArray(), true, true); mesh.RecalculateBounds();
                var part = new GameObject(group.Key.name); part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, key + "Geometry" + index++);
                part.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
        }

        static void Flag(Transform parent, string name, float width, float height, Vector3 position)
        {
            const int columns = 16, rows = 6;
            var vertices = new Vector3[(columns + 1) * (rows + 1)]; var uv = new Vector2[vertices.Length]; var triangles = new List<int>();
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                int i = y * (columns + 1) + x; float u = x / (float)columns, v = y / (float)rows;
                vertices[i] = new Vector3(u * width, v * height, 0); uv[i] = new Vector2(u, v);
                if (y == rows || x == columns) continue;
                triangles.Add(i); triangles.Add(i + columns + 1); triangles.Add(i + 1);
                triangles.Add(i + 1); triangles.Add(i + columns + 1); triangles.Add(i + columns + 2);
            }
            var mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateTangents();
            var part = new GameObject(name); part.transform.SetParent(parent, false); part.transform.localPosition = position;
            part.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, parent.name + name + "Mesh"); part.AddComponent<MeshRenderer>().sharedMaterial = cloth;
        }

        static Transform Lathe(Transform parent, string name, Vector2[] profile, int sides, Material material, bool capped = true)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int row = 0; row < profile.Length; row++) for (int side = 0; side <= sides; side++)
            {
                float a = side / (float)sides * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(a) * profile[row].x, profile[row].y, Mathf.Sin(a) * profile[row].x)); uv.Add(new Vector2(side / (float)sides, profile[row].y));
                if (row == profile.Length - 1 || side == sides) continue;
                int i = row * (sides + 1) + side, b = i + sides + 1;
                triangles.Add(i); triangles.Add(b); triangles.Add(i + 1); triangles.Add(i + 1); triangles.Add(b); triangles.Add(b + 1);
            }
            int bottom = vertices.Count; vertices.Add(new Vector3(0, profile[0].y, 0)); uv.Add(Vector2.zero);
            int top = vertices.Count; vertices.Add(new Vector3(0, profile[profile.Length - 1].y, 0)); uv.Add(Vector2.one);
            for (int s = 0; capped && s < sides; s++)
            {
                triangles.Add(bottom); triangles.Add(s); triangles.Add(s + 1);
                int end = (profile.Length - 1) * (sides + 1) + s;
                triangles.Add(top); triangles.Add(end + 1); triangles.Add(end);
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents();
            var part = new GameObject(name); part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh; part.AddComponent<MeshRenderer>().sharedMaterial = material; temporary.Add(mesh);
            return part.transform;
        }
    }
}
