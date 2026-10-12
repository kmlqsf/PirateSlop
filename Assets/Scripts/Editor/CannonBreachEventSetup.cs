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
    public static class CannonBreachEventSetup
    {
        const string Folder = "Assets/Models/SeaEvents/CannonBreach/";
        public const string PrefabPath = "Assets/Resources/SeaEvents/CannonBreachFort.prefab";
        struct Part { public Mesh Mesh; public Material[] Materials; }
        static readonly Dictionary<string, Part> parts = new();
        static Material stone, trim;

        [MenuItem("PirateSlop/Art/Prepare Cannon Breach Event")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); parts.Clear();
            var sourceRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CoastalEnvironment/CoastalRock.mat");
            stone = Material("FortWeatheredStone", sourceRock, new Color(1.05f, .99f, .88f));
            trim = Material("FortLimestoneTrim", sourceRock, new Color(1.22f, 1.16f, 1.03f));
            foreach (string key in new[] { "StoneWall", "StonePillar", "StoneFloor" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/World/LocationExpansion/" + key + ".fbx");
                Load(key, model.GetComponentInChildren<MeshFilter>(), false);
            }
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
            var filters = ship.GetComponentsInChildren<MeshFilter>(true);
            Load("ShipOak", filters.First(f => f.name == "V9_Capstan_Beam_-1"), true);
            Load("ForgedIron", filters.First(f => f.name == "V10_Port_Reel_Fixed_Axle"), true);
            Load("IronBolt", filters.First(f => f.name == "V3_Fore_Port_Chainplate_Bolts_00"), true);
            Load("IronBand", filters.First(f => f.name == "V9_Capstan_Beam_EndBand_-1"), false);
            PrepareTimberFragments();
            var root = new GameObject("CannonBreachFortIsland");
            try
            {
                var network = root.AddComponent<NetworkObject>();
                network.SetAssetPathHash(PrefabPath.GetStableHashU64());
                var owner = root.AddComponent<NetworkCannonBreachEvent>();
                owner.HitsToBreach = 6;
                var island = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SandyIslandTestSetup.Prefabs + "/TwoLandingIsland.prefab"), root.transform);
                island.name = "EventTwoLandingIsland";
                BuildCompactIsland(island);
                owner.IslandTerrain = island.GetComponentInChildren<Terrain>();
                owner.IslandRadius = 32;
                float ground = float.MinValue;
                for (int x = -10; x <= 10; x += 2) for (int z = -11; z <= 11; z += 2)
                    ground = Mathf.Max(ground, Height(owner.IslandTerrain, new Vector3(x, 0, z)));
                float floor = ground + .025f;
                var fort = Child(root.transform, "WeatheredCoastalFort", new Vector3(0, floor, 0));
                BuildFort(fort);
                owner.Gates = new FortBreachGate[2];
                for (int i = 0; i < 2; i++) owner.Gates[i] = BuildGate(fort, owner, i);
                owner.ChestSocket = Child(fort, "ChestSocket", new Vector3(0, .06f, 0));
                var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
                var reward = catalog.ChestPrefab;
                var chestBounds = reward.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.bounds).ToArray();
                if (chestBounds.Length > 0)
                {
                    float bottom = chestBounds.Min(b => b.min.y - reward.transform.position.y);
                    owner.ChestSocket.localPosition = new Vector3(0, .035f - bottom, 0);
                }
                BuildApproaches(fort, owner.IslandTerrain, floor);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("WorldStatic");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                catalog.CannonBreachEventPrefab = prefab.GetComponent<NetworkCannonBreachEvent>(); EditorUtility.SetDirty(catalog);
                var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
                registry.AddObject(prefab.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();
                Debug.Log("Saved Cannon Breach: separate two-landing island, two cannon gates, barred windows, public chest and event teleport.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("PirateSlop/Art/Repair Cannon Breach Floor")]
        public static void RepairFloor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var fort = root.transform.Find("WeatheredCoastalFort");
                fort.Find("StoneFoundation").localPosition = new Vector3(0, -.32f, 0);
                foreach (Transform tile in fort)
                {
                    if (tile.name != "CourtyardFlagstones") continue;
                    tile.localScale = new Vector3(3.2f, .18f, 3.6f);
                    var collider = tile.GetComponent<BoxCollider>();
                    if (collider == null) collider = tile.gameObject.AddComponent<BoxCollider>();
                    collider.center = Vector3.zero; collider.size = Vector3.one; collider.enabled = true;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void BuildFort(Transform fort)
        {
            Model(fort, "StoneFoundation", "StoneFloor", new Vector3(0, -.32f, 0), new Vector3(17.6f, .48f, 19.6f), Quaternion.identity, true, stone);
            for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
                Model(fort, "CourtyardFlagstones", "StoneFloor", new Vector3(x * 3.2f, -.09f, z * 3.6f), new Vector3(3.2f, .18f, 3.6f), Quaternion.identity, true, trim);
            foreach (int side in new[] { -1, 1 })
            {
                for (int row = 0; row < 2; row++) foreach (int span in new[] { -1, 1 })
                    Model(fort, "GateFlankingMasonry", "StoneWall", new Vector3(span * 5.15f, 1.4f + row * 2.8f, side * 9), new Vector3(5.7f, 2.8f, 1.25f), Quaternion.identity, true, row == 0 ? stone : trim);
                Model(fort, "GateLintel", "StoneWall", new Vector3(0, 4.9f, side * 9), new Vector3(4.6f, 1.4f, 1.25f), Quaternion.identity, true, trim);
                foreach (int jamb in new[] { -1, 1 })
                    Model(fort, "GateStoneJamb", "StonePillar", new Vector3(jamb * 2.65f, 2.05f, side * 9.25f), new Vector3(.7f, 4.1f, 1.45f), Quaternion.identity, true, trim);

                foreach (int span in new[] { -1, 1 })
                    Model(fort, "WindowFlankingMasonry", "StoneWall", new Vector3(side * 8, 2.8f, span * 5.35f), new Vector3(7.3f, 5.6f, 1.25f), Quaternion.Euler(0, 90, 0), true, stone);
                Model(fort, "WindowSillWall", "StoneWall", new Vector3(side * 8, .425f, 0), new Vector3(3.4f, .85f, 1.25f), Quaternion.Euler(0, 90, 0), true, stone);
                Model(fort, "WindowHeaderWall", "StoneWall", new Vector3(side * 8, 4.175f, 0), new Vector3(3.4f, 2.85f, 1.25f), Quaternion.Euler(0, 90, 0), true, stone);
                var window = Child(fort, side < 0 ? "WestBarredWindow" : "EastBarredWindow", new Vector3(side * 8, 1.8f, 0));
                window.localRotation = Quaternion.Euler(0, 90, 0);
                foreach (int end in new[] { -1, 1 })
                {
                    Model(window, "CarvedWindowJamb", "StonePillar", new Vector3(end * 1.8f, 0, 0), new Vector3(.35f, 2.3f, 1.4f), Quaternion.identity, true, trim);
                    Model(window, "CarvedWindowLintel", "StoneFloor", new Vector3(0, end * 1.02f, 0), new Vector3(3.8f, .2f, 1.45f), Quaternion.identity, true, trim);
                }
                for (int bar = -5; bar <= 5; bar++)
                    Model(window, "ForgedWindowBar", "ForgedIron", new Vector3(bar * .29f, 0, 0), new Vector3(.09f, 1.95f, .09f), Quaternion.identity, true);
                foreach (float y in new[] { -.6f, .6f })
                    Model(window, "IronCrossRail", "ForgedIron", new Vector3(0, y, 0), new Vector3(.09f, 3.25f, .1f), Quaternion.Euler(0, 0, 90), true);

                foreach (int corner in new[] { -1, 1 })
                {
                    var tower = Child(fort, "CornerBastion", new Vector3(side * 8, 0, corner * 9));
                    for (int row = 0; row < 3; row++)
                        Model(tower, "BastionStoneCourse", "StonePillar", new Vector3(0, 1 + row * 2, 0), new Vector3(2.1f, 2, 2.1f), Quaternion.identity, true, row % 2 == 0 ? stone : trim);
                    Model(tower, "BastionCornice", "StoneFloor", new Vector3(0, 6.1f, 0), new Vector3(2.55f, .35f, 2.55f), Quaternion.identity, true, trim);
                    foreach (int a in new[] { -1, 1 }) foreach (int b in new[] { -1, 1 })
                        Model(tower, "BastionMerlon", "StonePillar", new Vector3(a * .8f, 6.75f, b * .8f), new Vector3(.8f, 1.05f, .8f), Quaternion.identity, true, stone);
                }
                for (int m = -3; m <= 3; m++)
                {
                    Model(fort, "GateParapetMerlon", "StonePillar", new Vector3(m * 2, 6.3f, side * 9), new Vector3(.95f, 1.05f, 1.25f), Quaternion.identity, true, stone);
                    Model(fort, "SideParapetMerlon", "StonePillar", new Vector3(side * 8, 6.3f, m * 2.2f), new Vector3(1.25f, 1.05f, .95f), Quaternion.identity, true, stone);
                }
                foreach (int lamp in new[] { -1, 1 })
                {
                    var lantern = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/StarterIsland/Lantern.prefab"), fort);
                    lantern.name = "GateLantern"; lantern.transform.localPosition = new Vector3(lamp * 3.25f, 3.1f, side * 9.8f);
                    var light = lantern.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1, .64f, .29f); light.range = 5; light.intensity = 1.4f; light.shadows = LightShadows.None;
                }
            }
            Model(fort, "SealedStoneRoof", "StoneFloor", new Vector3(0, 5.67f, 0), new Vector3(17.6f, .22f, 19.6f), Quaternion.identity, true, trim);
            var chestLight = Child(fort, "WarmChestLight", new Vector3(0, 3.2f, 0)).gameObject.AddComponent<Light>();
            chestLight.type = LightType.Point; chestLight.color = new Color(1, .78f, .46f); chestLight.range = 13; chestLight.intensity = 2.5f; chestLight.shadows = LightShadows.None;
        }

        static FortBreachGate BuildGate(Transform fort, NetworkCannonBreachEvent owner, int index)
        {
            int side = index == 0 ? -1 : 1;
            var root = Child(fort, index == 0 ? "SouthBoardedGate" : "NorthBoardedGate", new Vector3(0, 0, side * 9.8f));
            root.localRotation = Quaternion.Euler(0, side < 0 ? 180 : 0, 0);
            var target = root.gameObject.AddComponent<FortGateTarget>(); target.Owner = owner; target.GateIndex = index;
            var blocker = root.gameObject.AddComponent<BoxCollider>(); blocker.center = new Vector3(0, 2.1f, 0); blocker.size = new Vector3(4.6f, 4.2f, 1.1f); blocker.enabled = false;
            var fragments = new List<FortGateFragment>();
            for (int column = 0; column < 10; column++)
            {
                for (int row = 0; row < 3; row++)
                {
                    var timber = Child(root, "BreachOakFragment_" + column + "_" + row, new Vector3((column - 4.5f) * .47f, .7f + row * 1.4f, 0));
                    var visual = Model(timber, "JaggedShipOak", "GateTimber_" + row, Vector3.zero, Vector3.one, Quaternion.identity, false);
                    var solid = visual.gameObject.AddComponent<BoxCollider>(); var bounds = parts["GateTimber_" + row].Mesh.bounds;
                    solid.center = bounds.center; solid.size = bounds.size;
                    float band = (row - 1) * .05f;
                    Model(timber, "ForgedGateBand", "IronBand", new Vector3(0, band, 0), new Vector3(.53f, .25f, .66f), Quaternion.identity, false);
                    Model(timber, "ForgedGateRivet", "IronBolt", new Vector3(0, band, .365f), new Vector3(.12f, .08f, .12f), Quaternion.Euler(90, 0, 0), false);
                    fragments.Add(new FortGateFragment { Root = timber, Solid = solid, Group = (byte)(row * 2 + (column >= 5 ? 1 : 0)) });
                }
            }
            for (int rail = 0; rail < 5; rail++)
            {
                var frame = Child(root, "RearFrameRail_" + rail, new Vector3(0, .4f + rail * .85f, -.46f));
                var visual = Model(frame, "OakSupportRail", "ShipOak", Vector3.zero, new Vector3(.32f, 4.7f, .3f), Quaternion.Euler(0, 0, 90), true);
                fragments.Add(new FortGateFragment { Root = frame, Solid = visual.GetComponent<BoxCollider>(), Group = 6 });
            }
            foreach (int brace in new[] { -1, 1 })
            {
                var diagonal = Child(root, "RearCrossBrace_" + brace, new Vector3(0, 2.1f, -.75f));
                var visual = Model(diagonal, "HeavyOakBrace", "ShipOak", Vector3.zero, new Vector3(.42f, 5.15f, .32f), Quaternion.Euler(0, 0, brace * 46), true);
                fragments.Add(new FortGateFragment { Root = diagonal, Solid = visual.GetComponent<BoxCollider>(), Group = 6 });
            }
            return new FortBreachGate { Root = root, Blocker = blocker, Fragments = fragments.ToArray() };
        }

        struct FractureVertex
        {
            public Vector3 Point;
            public Vector2 UV;
            public static FractureVertex Lerp(FractureVertex a, FractureVertex b, float t)
                => new() { Point = Vector3.Lerp(a.Point, b.Point, t), UV = Vector2.Lerp(a.UV, b.UV, t) };
        }

        static void PrepareTimberFragments()
        {
            var source = parts["ShipOak"];
            for (int row = 0; row < 3; row++)
            {
                var mesh = SliceTimber(source.Mesh, row);
                string key = "GateTimber_" + row;
                parts[key] = new Part { Mesh = SaveMesh(mesh, key), Materials = source.Materials };
            }
        }

        static Mesh SliceTimber(Mesh source, int row)
        {
            var points = source.vertices; var uv = source.uv;
            var vertices = new List<Vector3>(); var texcoords = new List<Vector2>();
            var triangles = new List<int>[source.subMeshCount];
            var lowerCap = new List<FractureVertex>(); var upperCap = new List<FractureVertex>();
            float center = (row - 1) * 1.4f;
            for (int slot = 0; slot < triangles.Length; slot++)
            {
                triangles[slot] = new List<int>(); var indices = source.GetTriangles(slot);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var polygon = new List<FractureVertex>();
                    for (int j = 0; j < 3; j++)
                    {
                        int index = indices[i + j];
                        polygon.Add(new FractureVertex { Point = Vector3.Scale(points[index], new Vector3(.48f, 4.2f, .58f)), UV = uv[index] });
                    }
                    if (row > 0) polygon = ClipTimber(polygon, center - .7f, true, lowerCap);
                    if (row < 2) polygon = ClipTimber(polygon, center + .7f, false, upperCap);
                    AppendPolygon(polygon, vertices, texcoords, triangles[slot], center);
                }
            }
            AppendFractureCap(lowerCap, false, vertices, texcoords, triangles[0], center);
            AppendFractureCap(upperCap, true, vertices, texcoords, triangles[0], center);
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, texcoords); mesh.subMeshCount = triangles.Length;
            for (int slot = 0; slot < triangles.Length; slot++) mesh.SetTriangles(triangles[slot], slot);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        static float CutDistance(Vector3 point, float height)
            => point.y - height - point.x * .17f - point.z * .11f - Mathf.Sin(point.x * 29 + point.z * 17) * .045f;

        static List<FractureVertex> ClipTimber(List<FractureVertex> input, float height, bool above, List<FractureVertex> cap)
        {
            var output = new List<FractureVertex>(); if (input.Count == 0) return output;
            var previous = input[input.Count - 1]; float pd = CutDistance(previous.Point, height);
            foreach (var current in input)
            {
                float cd = CutDistance(current.Point, height);
                bool pi = above ? pd >= 0 : pd <= 0, ci = above ? cd >= 0 : cd <= 0;
                if (pi != ci)
                {
                    var cut = FractureVertex.Lerp(previous, current, Mathf.Clamp01(pd / (pd - cd)));
                    output.Add(cut); cap.Add(cut);
                }
                if (ci) output.Add(current);
                previous = current; pd = cd;
            }
            return output;
        }

        static void AppendPolygon(List<FractureVertex> polygon, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, float center)
        {
            if (polygon.Count < 3) return;
            int first = vertices.Count;
            foreach (var vertex in polygon) { vertices.Add(vertex.Point - Vector3.up * center); uv.Add(vertex.UV); }
            for (int i = 1; i < polygon.Count - 1; i++) { triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1); }
        }

        static void AppendFractureCap(List<FractureVertex> input, bool up, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, float center)
        {
            var polygon = new List<FractureVertex>();
            foreach (var vertex in input)
                if (!polygon.Any(p => (p.Point - vertex.Point).sqrMagnitude < .0000001f)) polygon.Add(vertex);
            if (polygon.Count < 3) return;
            Vector3 point = Vector3.zero; Vector2 tex = Vector2.zero;
            foreach (var vertex in polygon) { point += vertex.Point; tex += vertex.UV; }
            point /= polygon.Count; tex /= polygon.Count;
            polygon.Sort((a, b) => Mathf.Atan2(a.Point.z - point.z, a.Point.x - point.x).CompareTo(Mathf.Atan2(b.Point.z - point.z, b.Point.x - point.x)));
            for (int i = 0; i < polygon.Count; i++)
            {
                var triangle = new List<FractureVertex> { new() { Point = point, UV = tex }, polygon[i], polygon[(i + 1) % polygon.Count] };
                if (up) triangle.Reverse(); AppendPolygon(triangle, vertices, uv, triangles, center);
            }
        }

        static void BuildApproaches(Transform fort, Terrain terrain, float floor)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float outer = Height(terrain, new Vector3(0, 0, side * 14));
                float rise = floor - outer;
                float length = 4.2f;
                Model(fort, "StoneDoorApproach", "StoneFloor", new Vector3(0, -rise * .5f - .09f, side * 11.8f), new Vector3(4.6f, .18f, Mathf.Sqrt(length * length + rise * rise)), Quaternion.Euler(side * Mathf.Atan2(rise, length) * Mathf.Rad2Deg, 0, 0), true, trim);
            }
        }

        static float Height(Terrain terrain, Vector3 point)
        {
            var local = point - terrain.transform.localPosition;
            return terrain.transform.localPosition.y + terrain.terrainData.GetInterpolatedHeight(local.x / terrain.terrainData.size.x, local.z / terrain.terrainData.size.z);
        }

        static void BuildCompactIsland(GameObject island)
        {
            const float horizontal = .3f, elevation = .26f;
            const string dataPath = "Assets/Settings/World/SandyIslands/CannonBreachIsland.asset";
            var terrain = island.GetComponentInChildren<Terrain>();
            var source = terrain.terrainData;
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (data == null) { data = Object.Instantiate(source); data.name = "CannonBreachIsland"; AssetDatabase.CreateAsset(data, dataPath); }
            data.heightmapResolution = source.heightmapResolution;
            data.alphamapResolution = source.alphamapResolution;
            data.size = new Vector3(source.size.x * horizontal, source.size.y, source.size.z * horizontal);
            data.terrainLayers = source.terrainLayers;
            data.SetAlphamaps(0, 0, source.GetAlphamaps(0, 0, source.alphamapWidth, source.alphamapHeight));
            var heights = source.GetHeights(0, 0, source.heightmapResolution, source.heightmapResolution);
            float baseY = terrain.transform.localPosition.y;
            for (int z = 0; z < heights.GetLength(0); z++) for (int x = 0; x < heights.GetLength(1); x++)
            {
                float h = baseY + heights[z, x] * source.size.y;
                float lowered = h >= 0 ? h * elevation : h;
                heights[z, x] = (lowered - baseY) / data.size.y;
            }
            data.SetHeights(0, 0, heights); data.SetHoles(0, 0, source.GetHoles(0, 0, source.holesResolution, source.holesResolution));
            terrain.terrainData = data; terrain.GetComponent<TerrainCollider>().terrainData = data;
            terrain.transform.localPosition = new Vector3(terrain.transform.localPosition.x * horizontal, baseY, terrain.transform.localPosition.z * horizontal);
            EditorUtility.SetDirty(data);
            foreach (Transform child in island.transform.Cast<Transform>().ToArray())
            {
                if (child == terrain.transform) continue;
                if (child.name == "NaturalRockRim") { child.localScale *= horizontal; continue; }
                Object.DestroyImmediate(child.gameObject);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                AddIslandPlant(island.transform, terrain, "PalmYoung", new Vector3(side * 13.6f, 0, side * 4.2f), .78f);
                AddIslandPlant(island.transform, terrain, "Bush", new Vector3(side * 13, 0, -side * 5.7f), .78f);
                AddIslandPlant(island.transform, terrain, "Fern", new Vector3(side * 12, 0, side * 7.5f), .9f);
            }
        }

        static void AddIslandPlant(Transform parent, Terrain terrain, string name, Vector3 point, float scale)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/StarterIsland/" + name + ".prefab");
            var plant = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            point.y = Height(terrain, point); plant.transform.localPosition = point;
            plant.transform.localRotation = Quaternion.Euler(0, point.x * 19, 0); plant.transform.localScale *= scale;
        }

        static Material Material(string name, Material source, Color color)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_TextureScale", .55f); material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }

        static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child;
        }

        static Transform Model(Transform parent, string name, string key, Vector3 position, Vector3 size, Quaternion rotation, bool collision, Material material = null)
        {
            var t = Child(parent, name, position); t.localRotation = rotation; t.localScale = size;
            var part = parts[key]; t.gameObject.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = material == null ? part.Materials : Enumerable.Repeat(material, part.Mesh.subMeshCount).ToArray();
            if (collision) t.gameObject.AddComponent<BoxCollider>().size = Vector3.one;
            return t;
        }

        static void Load(string key, MeshFilter source, bool longestUp)
        {
            var mesh = Object.Instantiate(source.sharedMesh); var vertices = mesh.vertices;
            var matrix = source.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices; mesh.RecalculateBounds(); var b = mesh.bounds;
            Vector3 axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : b.size.y >= b.size.z ? Vector3.up : Vector3.forward;
            var rotation = longestUp ? Quaternion.FromToRotation(axis, Vector3.up) : Quaternion.identity;
            if (key == "IronBand")
            {
                Vector3 thin = b.size.x <= b.size.y && b.size.x <= b.size.z ? Vector3.right : b.size.y <= b.size.z ? Vector3.up : Vector3.forward;
                rotation = Quaternion.FromToRotation(thin, Vector3.up);
            }
            for (int i = 0; i < vertices.Length; i++) vertices[i] = rotation * (vertices[i] - b.center);
            mesh.vertices = vertices; mesh.RecalculateBounds(); Vector3 size = mesh.bounds.size;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3(vertices[i].x / size.x, vertices[i].y / size.y, vertices[i].z / size.z);
            mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            parts[key] = new Part { Mesh = SaveMesh(mesh, key), Materials = source.GetComponent<MeshRenderer>().sharedMaterials };
        }

        static Mesh SaveMesh(Mesh mesh, string key)
        {
            string path = Folder + key + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { mesh.name = key; AssetDatabase.CreateAsset(mesh, path); }
            else
            {
                saved.Clear(); saved.indexFormat = mesh.indexFormat; saved.vertices = mesh.vertices; saved.normals = mesh.normals; saved.tangents = mesh.tangents;
                saved.uv = mesh.uv; saved.uv2 = mesh.uv2; saved.colors = mesh.colors; saved.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++) saved.SetTriangles(mesh.GetTriangles(i), i);
                saved.RecalculateBounds(); saved.UploadMeshData(false); Object.DestroyImmediate(mesh); mesh = saved; EditorUtility.SetDirty(saved);
            }
            return mesh;
        }
    }
}
