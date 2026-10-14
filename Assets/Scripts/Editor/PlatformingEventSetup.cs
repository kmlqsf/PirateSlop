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
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PirateSlop.Editor
{
    public static class PlatformingEventSetup
    {
        const string Folder = "Assets/Models/SeaEvents/Platforming/";
        const string PrefabPath = "Assets/Resources/SeaEvents/PlatformingEvent.prefab";
        static Mesh timber;
        static Material[] oak;
        static Material iron, marker, hemp;
        static Mesh stoneFloor, stonePillar, mast, barrel, pileMesh;
        static Material stoneMaterial, mastMaterial, barrelMaterial;

        enum Surface { Deck, Stone, Rock, Pile, Barrel, Beam }
        readonly struct RouteNode
        {
            public readonly string Section;
            public readonly Surface Surface;
            public readonly Vector3 Point;
            public readonly float Width, Length, Yaw;
            public RouteNode(string section, Surface surface, float x, float y, float z, float width, float length, float yaw = 0)
            { Section = section; Surface = surface; Point = new Vector3(x, y, z); Width = width; Length = length; Yaw = yaw; }
        }

        static RouteNode[] Route() => new[]
        {
            new RouteNode("BrokenPier", Surface.Deck, -18, 2.2f, -22, 3, 3),
            new RouteNode("BrokenPier", Surface.Deck, -17, 2.7f, -17.5f, 1.1f, 2.1f, 25),
            new RouteNode("BrokenPier", Surface.Stone, -13.7f, 3.45f, -17, 1.4f, 1.6f, 70),
            new RouteNode("BrokenPier", Surface.Pile, -12.5f, 4.1f, -14.5f, .5f, .5f),
            new RouteNode("BrokenPier", Surface.Rock, -15.2f, 4.8f, -12.6f, 1.7f, 1.5f, -50),
            new RouteNode("BrokenPier", Surface.Beam, -17.3f, 5.25f, -9.6f, .32f, 4.5f, -35),
            new RouteNode("StoneTeeth", Surface.Barrel, -20, 5.7f, -5.1f, .82f, .82f),
            new RouteNode("StoneTeeth", Surface.Rock, -17.2f, 6.5f, -2.8f, 1.5f, 1.6f, 45),
            new RouteNode("StoneTeeth", Surface.Stone, -12.5f, 6.8f, -2, 3, 2.7f, 75),
            new RouteNode("StoneTeeth", Surface.Stone, -10, 7.65f, 1, .95f, 1.1f, -25),
            new RouteNode("StoneTeeth", Surface.Pile, -7, 8.1f, -.5f, .46f, .46f),
            new RouteNode("StoneTeeth", Surface.Stone, -4, 8.85f, 2, 1.15f, 2, 65),
            new RouteNode("MastCrossing", Surface.Beam, -2.5f, 9.15f, 7, .26f, 7.5f, 15),
            new RouteNode("MastCrossing", Surface.Pile, 1.3f, 9.8f, 12.1f, .48f, .48f),
            new RouteNode("MastCrossing", Surface.Rock, 4.5f, 10.55f, 10.3f, 1.35f, 1.5f, 60),
            new RouteNode("MastCrossing", Surface.Deck, 7.8f, 10.25f, 12.5f, .9f, 2, 30),
            new RouteNode("BrokenAqueduct", Surface.Stone, 9, 11.05f, 17, 2.6f, 2.8f, -45),
            new RouteNode("BrokenAqueduct", Surface.Pile, 5.8f, 11.9f, 19, .5f, .5f),
            new RouteNode("BrokenAqueduct", Surface.Stone, 3, 12.65f, 17.3f, .65f, .75f, 15),
            new RouteNode("BrokenAqueduct", Surface.Beam, -1, 13.25f, 19.2f, .24f, 5.4f, -68),
            new RouteNode("BrokenAqueduct", Surface.Barrel, -5.4f, 13.9f, 23.1f, .74f, .74f),
            new RouteNode("BrokenAqueduct", Surface.Rock, -8.5f, 14.75f, 21.8f, 1.3f, 1.4f, -40),
            new RouteNode("BrokenAqueduct", Surface.Stone, -11.3f, 15.15f, 24.6f, .55f, .65f, 30),
            new RouteNode("CrowsNest", Surface.Deck, -14.5f, 15.9f, 22.5f, .75f, 1.8f, -35),
            new RouteNode("CrowsNest", Surface.Deck, -17.8f, 16.4f, 25, 2.6f, 2.7f, -35),
            new RouteNode("CrowsNest", Surface.Beam, -20.5f, 16.65f, 29.6f, .23f, 5, -30),
            new RouteNode("CrowsNest", Surface.Pile, -19.3f, 17.45f, 34, .42f, .42f),
            new RouteNode("CrowsNest", Surface.Stone, -15.8f, 18.2f, 35.5f, .8f, .95f, 70),
            new RouteNode("CrowsNest", Surface.Barrel, -13.5f, 18.6f, 32.6f, .65f, .65f),
            new RouteNode("CrowsNest", Surface.Pile, -10.1f, 19.35f, 34.1f, .42f, .42f),
            new RouteNode("CrowsNest", Surface.Deck, -7, 20.05f, 37.2f, 3.3f, 3.3f, 45)
        };

        [MenuItem("PirateSlop/Art/Prepare Platforming Event")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SeaEvents/RiggingEvent.prefab");
            timber = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/SeaEvents/CannonBreach/ShipOak.asset");
            if (catalog == null || catalog.ChestPrefab == null || source == null || timber == null) throw new InvalidOperationException("Existing ship art and loot catalog are required.");
            var materials = source.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            oak = materials.Where(m => m.name.Contains("Deck_Worn")).OrderBy(m => m.name).ToArray();
            iron = materials.First(m => m.name.Contains("Oxidized_Iron"));
            hemp = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/SeaEvents/Rigging/RiggingHemp.mat");
            stoneFloor = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/SeaEvents/CannonBreach/StoneFloor.asset");
            stonePillar = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/SeaEvents/CannonBreach/StonePillar.asset");
            mast = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/SeaEvents/SeagullKey/BrokenMast.asset");
            barrel = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/SeaEvents/SternCargo/Reused_Barrel.asset");
            stoneMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/SeaEvents/CannonBreach/FortWeatheredStone.mat");
            mastMaterial = materials.First(m => m.name.Contains("Mast_Carved_Shaft"));
            barrelMaterial = materials.First(m => m.name == "wooden_barrels_01_barrel01");
            marker = AssetDatabase.LoadAssetAtPath<Material>(Folder + "FadedOchre.mat");
            if (marker == null) { marker = new Material(oak[0]); AssetDatabase.CreateAsset(marker, Folder + "FadedOchre.mat"); }
            marker.SetColor("_BaseColor", new Color(.76f, .5f, .17f));
            EditorUtility.SetDirty(marker);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(primitive, scene);
            pileMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);
            var root = new GameObject("PlatformingEvent");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var network = root.AddComponent<NetworkObject>();
                network.SetAssetPathHash(PrefabPath.GetStableHashU64());
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeAll;
                var owner = root.AddComponent<NetworkPlatformingEvent>(); owner.Catalog = catalog;
                var geometry = Child(root.transform, "Scaffold", Vector3.zero);
                owner.CourseRoot = geometry;
                owner.PlatformRadius = 46;
                var route = Route();
                for (int i = 0; i < route.Length; i++)
                {
                    var spec = route[i];
                    var platform = Child(geometry, "Landing_" + i.ToString("00") + "_" + spec.Section + "_" + spec.Surface, spec.Point);
                    platform.localRotation = Quaternion.Euler(0, spec.Yaw, 0);
                    BuildLanding(platform, spec, i);
                    if (i == 0) Entrance(geometry, platform);
                    if (i == 0 || i == 8 || i == 16 || i == 24 || i == route.Length - 1)
                    {
                        Pennant(platform, source, new Vector3(-spec.Width * .5f - .28f, 0, .4f), i == route.Length - 1 ? 4.5f : 2.5f);
                        Lantern(platform, new Vector3(spec.Width * .5f + .12f, 1.7f, .5f), i == 0 || i == route.Length - 1);
                    }
                    if (i == 8 || i == 16) BrokenArch(platform, spec.Width);
                    if (i == route.Length - 1)
                    {
                        owner.ChestSocket = Child(platform, "ChestSocket", new Vector3(0, .015f, .55f));
                        float mastHeight = spec.Point.y + 9;
                        Fitted(platform, "CrowsNestMainMast", mast, mastMaterial, new Vector3(-1.55f, 4 - mastHeight * .5f, 1.4f), new Vector3(.65f, mastHeight, .65f), Quaternion.identity, true);
                        for (int side = -1; side <= 1; side += 2)
                            Rope(platform, "SummitStay", new[] { new Vector3(-1.55f, 3.8f, 1.4f), new Vector3(side * 1.5f, -.25f, -1.35f) }, .035f);
                    }
                }
                for (int i = 0; i < geometry.childCount; i++) Batch(geometry.GetChild(i), "Landing" + i.ToString("00"));
                var fragments = geometry.GetComponentsInChildren<MeshFilter>().Select(f => f.transform).ToList();
                owner.IntactRoot = Child(root.transform, "IntactCourse", Vector3.zero);
                foreach (var fragment in fragments)
                {
                    var copy = Object.Instantiate(fragment.gameObject, owner.IntactRoot, false).transform;
                    copy.SetPositionAndRotation(fragment.position, fragment.rotation); copy.localScale = fragment.lossyScale;
                }
                Batch(owner.IntactRoot, "IntactCourse");
                foreach (var fragment in fragments) fragment.gameObject.SetActive(false);
                BuildReleaseMechanism(root.transform, owner);
                fragments.Add(owner.LeverRoot);
                owner.CollapsePieces = fragments.ToArray();
                foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = LayerMask.NameToLayer("WorldStatic");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkPlatformingEvent>(PrefabPath);
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            registry.AddObject(prefab.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(registry);
            catalog.PlatformingEventPrefab = prefab; EditorUtility.SetDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 196); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("Smugglers Ascent rebuilt: 31 authored landings, six surface types, five sections, 20.05m summit.");
        }

        static Transform Child(Transform parent, string name, Vector3 point)
        {
            var node = new GameObject(name).transform; node.SetParent(parent, false); node.localPosition = point; return node;
        }

        static void BuildReleaseMechanism(Transform root, NetworkPlatformingEvent owner)
        {
            var platform = owner.ChestSocket.parent;
            owner.LeverRoot = Child(platform, "ChestReleaseLever", new Vector3(-1.05f, 0, -.8f));
            owner.LeverRoot.gameObject.AddComponent<PlatformingLeverTarget>().Owner = owner;
            Beam(owner.LeverRoot, "IronFootplate", new Vector3(0, .035f, 0), new Vector3(.5f, .07f, .45f), Quaternion.identity, iron, true);
            Beam(owner.LeverRoot, "CastIronPedestal", new Vector3(0, .32f, 0), new Vector3(.3f, .64f, .28f), Quaternion.identity, iron, true);
            Fitted(owner.LeverRoot, "LeverPivotBearing", pileMesh, iron, new Vector3(0, .61f, 0), new Vector3(.24f, .22f, .24f), Quaternion.Euler(0, 0, 90), false);
            owner.LeverPivot = Child(owner.LeverRoot, "LeverHandlePivot", new Vector3(0, .6f, 0));
            Beam(owner.LeverPivot, "IronLeverArm", new Vector3(0, .36f, 0), new Vector3(.09f, .75f, .09f), Quaternion.identity, iron, false);
            Beam(owner.LeverPivot, "WornWoodHandle", new Vector3(0, .75f, 0), new Vector3(.4f, .1f, .12f), Quaternion.identity, oak[0], false);
            owner.LeverGrip = Child(owner.LeverPivot, "LeverGrip", new Vector3(0, .75f, 0));
            var trigger = owner.LeverRoot.gameObject.AddComponent<SphereCollider>();
            trigger.center = new Vector3(0, 1.1f, 0); trigger.radius = .36f; trigger.isTrigger = true;
            owner.ChainsRoot = Child(root, "LockedChestChains", owner.ChestSocket.position);
            owner.ChainsRoot.rotation = owner.ChestSocket.rotation;
            var chainMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Ships/ShipV3/RuntimeMeshes/AnchorChainLink.asset");
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * .34f;
                var path = new[] { new Vector3(x, .015f, -.35f), new Vector3(x, .36f, -.35f), new Vector3(x, .59f, -.24f), new Vector3(x, .65f, 0), new Vector3(x, .59f, .24f), new Vector3(x, .36f, .35f), new Vector3(x, .015f, .35f), new Vector3(x, .015f, -.35f) };
                Chain(owner.ChainsRoot, chainMesh, path);
            }
            Chain(owner.ChainsRoot, chainMesh, new[] { new Vector3(-.34f, .35f, .37f), new Vector3(.34f, .35f, .37f) });
            Batch(owner.ChainsRoot, "SummitChains");
            var padlock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SeaEvents/SeagullKey/WreckChestPadlock.prefab");
            if (padlock == null) throw new InvalidOperationException("Existing chest padlock is required.");
            var lockObject = Object.Instantiate(padlock, owner.ChainsRoot, false);
            lockObject.name = "SummitPadlock"; lockObject.transform.localPosition = new Vector3(0, .5f, .4f); lockObject.transform.localScale = Vector3.one * 1.4f;
            owner.LockShackle = lockObject.transform.Find("IronShackle");
        }

        static void Chain(Transform parent, Mesh mesh, Vector3[] path)
        {
            int link = 0;
            for (int segment = 1; segment < path.Length; segment++)
            {
                var delta = path[segment] - path[segment - 1];
                int count = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .075f));
                for (int i = 0; i < count; i++)
                {
                    var rotation = Quaternion.FromToRotation(Vector3.up, delta) * Quaternion.Euler(0, link % 2 * 90, 0);
                    Fitted(parent, "ForgedChainLink_" + link++, mesh, iron, Vector3.Lerp(path[segment - 1], path[segment], (i + .5f) / count), new Vector3(.095f, .145f, .055f), rotation, false);
                }
            }
        }

        static Transform Fitted(Transform parent, string name, Mesh mesh, Material material, Vector3 center, Vector3 size, Quaternion rotation, bool collision)
        {
            if (mesh == null || material == null) throw new InvalidOperationException("Missing reused part: " + name);
            var scale = new Vector3(size.x / mesh.bounds.size.x, size.y / mesh.bounds.size.y, size.z / mesh.bounds.size.z);
            var node = Child(parent, name, center - rotation * Vector3.Scale(mesh.bounds.center, scale));
            node.localRotation = rotation; node.localScale = scale;
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(material, mesh.subMeshCount).ToArray();
            if (collision) node.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            return node;
        }

        static void Pile(Transform parent, Vector3 top, float width, float bottom, string name = "CutMooringPile")
        {
            float height = top.y - bottom;
            Fitted(parent, name, pileMesh, mastMaterial, top - Vector3.up * (height * .5f), new Vector3(width, height, width), Quaternion.identity, true);
            var turns = new Vector3[49];
            for (int i = 0; i < turns.Length; i++)
            {
                float a = i * Mathf.PI * 4 / (turns.Length - 1);
                turns[i] = top + new Vector3(Mathf.Cos(a) * width * .53f, -.25f - i * .004f, Mathf.Sin(a) * width * .53f);
            }
            Rope(parent, "SaltwornHempBinding", turns, .026f);
        }

        static void BuildLanding(Transform platform, RouteNode spec, int index)
        {
            float bottom = -spec.Point.y - 5;
            switch (spec.Surface)
            {
                case Surface.Deck:
                    Deck(platform, spec.Width, spec.Length, index);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * Mathf.Max(.12f, spec.Width * .5f - .18f);
                        Pile(platform, new Vector3(x, -.22f, 0), .3f, bottom);
                        Link(platform, "SplayedUnderdeckBrace", new Vector3(x, -1.8f, 0), new Vector3(0, -.25f, spec.Length * .4f), .16f, oak[index % oak.Length]);
                    }
                    Beam(platform, "DeepDeckCrosshead", new Vector3(0, -.34f, 0), new Vector3(spec.Width + .25f, .26f, .32f), Quaternion.identity, oak[0], false);
                    break;
                case Surface.Beam:
                    Beam(platform, "NarrowBalanceYard", new Vector3(0, -.14f, 0), new Vector3(spec.Width, .28f, spec.Length), Quaternion.identity, oak[index % oak.Length], true);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var end = new Vector3(0, -.3f, side * spec.Length * .42f);
                        Pile(platform, end, .3f, bottom);
                        Beam(platform, "YardIronFerrule", end + Vector3.up * .12f, new Vector3(spec.Width + .04f, .29f, .09f), Quaternion.identity, iron, false);
                    }
                    break;
                case Surface.Pile:
                    Pile(platform, Vector3.zero, spec.Width, bottom, "PrecisionPileTop");
                    break;
                case Surface.Barrel:
                    float height = spec.Width * 1.3f;
                    Fitted(platform, "StandingSmugglersBarrel", barrel, barrelMaterial, Vector3.down * height * .5f, new Vector3(spec.Width, height, spec.Length), Quaternion.identity, true);
                    Pile(platform, Vector3.down * height, .34f, bottom, "BarrelPedestal");
                    break;
                case Surface.Stone:
                case Surface.Rock:
                    float cap = spec.Surface == Surface.Rock ? .2f : .38f;
                    var landing = Fitted(platform, "WeatheredStoneLanding", stoneFloor, stoneMaterial, Vector3.down * cap * .5f, new Vector3(spec.Width, cap, spec.Length), Quaternion.identity, false);
                    var stoneCollision = landing.gameObject.AddComponent<BoxCollider>();
                    stoneCollision.center = stoneFloor.bounds.center; stoneCollision.size = stoneFloor.bounds.size;
                    float shaftWidth = Mathf.Max(.38f, spec.Width * .68f);
                    float shaftDepth = Mathf.Max(.38f, spec.Length * .68f);
                    float shaftHeight = -bottom - cap;
                    Fitted(platform, "DrownedMasonryPier", stonePillar, stoneMaterial, new Vector3(0, -cap - shaftHeight * .5f, 0), new Vector3(shaftWidth, shaftHeight, shaftDepth), Quaternion.Euler(0, 0, 0), true);
                    Beam(platform, "MasonryInnerCore", new Vector3(0, -cap - shaftHeight * .5f, 0), new Vector3(shaftWidth * .58f, shaftHeight, shaftDepth * .58f), Quaternion.identity, stoneMaterial, true);
                    if (spec.Surface == Surface.Rock) RockShoulder(platform, spec);
                    else if (spec.Width > 2)
                        for (int corner = -1; corner <= 1; corner += 2)
                            Fitted(platform, "BrokenCornice", stoneFloor, stoneMaterial, new Vector3(corner * spec.Width * .45f, -.5f, 0), new Vector3(.35f, .3f, spec.Length * .9f), Quaternion.Euler(0, 0, corner * 8), true);
                    break;
            }
        }

        static void RockShoulder(Transform platform, RouteNode spec)
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/World/CoastalEnvironment/RockMedium_LOD0.fbx").GetComponentInChildren<MeshFilter>();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CoastalEnvironment/TripoNative_12_RockElongated_B.mat");
            Fitted(platform, "NaturalRockShoulder", original.sharedMesh, material, new Vector3(.1f, -1.1f, -.05f), new Vector3(spec.Width + .6f, 2, spec.Length + .4f), Quaternion.Euler(0, spec.Yaw * .4f, 0), true);
        }

        static void BrokenArch(Transform platform, float width)
        {
            float x = width * .5f + .5f;
            Fitted(platform, "BrokenArchButtress", stonePillar, stoneMaterial, new Vector3(-x, 1.2f, .7f), new Vector3(.65f, 2.8f, .7f), Quaternion.Euler(0, 0, -6), true);
            Fitted(platform, "BrokenArchOppositeStump", stonePillar, stoneMaterial, new Vector3(x, .4f, .7f), new Vector3(.7f, 1.2f, .75f), Quaternion.Euler(0, 12, 0), true);
            Fitted(platform, "TruncatedStoneLintel", stoneFloor, stoneMaterial, new Vector3(-x + .65f, 2.5f, .7f), new Vector3(1.7f, .45f, .8f), Quaternion.Euler(0, 0, 12), true);
        }

        static void Deck(Transform parent, float width, float length, int seed)
        {
            int boards = Mathf.CeilToInt(width / .3f);
            float step = width / boards;
            for (int j = 0; j < boards; j++)
                Beam(parent, "WornDeckBoard", new Vector3((j + .5f) * step - width * .5f, -.105f, 0), new Vector3(step - .012f, .21f, length + Mathf.Sin(j * 7 + seed) * .035f), Quaternion.identity, oak[(j + seed) % oak.Length], false);
            var collision = parent.gameObject.AddComponent<BoxCollider>();
            collision.center = new Vector3(0, -.105f, 0); collision.size = new Vector3(width, .21f, length);
            for (int side = -1; side <= 1; side += 2)
                Beam(parent, "IronDeckStrap", new Vector3(0, -.025f, side * (length * .5f - .22f)), new Vector3(width, .035f, .07f), Quaternion.identity, iron, false);
        }

        static void Entrance(Transform parent, Transform landing)
        {
            var start = landing.position - landing.forward * 10 - Vector3.up * 4.5f;
            var end = landing.position - landing.forward * 1.3f - Vector3.up * .11f;
            var direction = end - start;
            var ramp = Child(parent, "SubmergedBoardingRamp", (start + end) * .5f);
            ramp.localRotation = Quaternion.LookRotation(direction);
            Deck(ramp, 2.6f, direction.magnitude, 1);
            for (int side = -1; side <= 1; side += 2)
                Beam(ramp, "RampStringer", new Vector3(side, -.33f, 0), new Vector3(.18f, .28f, direction.magnitude), Quaternion.identity, oak[0], false);
        }

        static void Pennant(Transform parent, GameObject source, Vector3 position, float height)
        {
            Beam(parent, "SignalPole", position + Vector3.up * height * .5f, new Vector3(.14f, height, .14f), Quaternion.Euler(0, 0, -5), oak[0], false);
            var flag = source.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.Contains("Main_Pirate_Flag") && f.GetComponent<MeshRenderer>() != null);
            if (flag == null) return;
            var size = flag.sharedMesh.bounds.size;
            float scale = 2.1f / Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var node = Child(parent, "TornPiratePennant", position + Vector3.up * (height - .55f) + Vector3.right * .65f);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = flag.sharedMesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterials = flag.GetComponent<MeshRenderer>().sharedMaterials;
            node.localScale = Vector3.one * scale;
            node.localPosition -= flag.sharedMesh.bounds.center * scale;
        }

        static void Rope(Transform parent, string name, Vector3[] points, float radius)
        {
            var node = Child(parent, name, Vector3.zero);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = RiggingRopeVisual.CreateTube(points, radius);
            node.GetComponent<MeshFilter>().sharedMesh.name = "PlatformingTemporaryRope";
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = hemp;
        }

        static void Lantern(Transform parent, Vector3 position, bool light)
        {
            Beam(parent, "LanternHookPost", position - Vector3.up * .85f, new Vector3(.13f, 1.7f, .13f), Quaternion.identity, oak[0], false);
            Beam(parent, "LanternHook", position + Vector3.right * .25f, new Vector3(.6f, .13f, .13f), Quaternion.identity, iron, false);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Loot/HandLantern/HandLanternVisual.prefab");
            var node = Object.Instantiate(original, parent, false);
            node.name = "WarmLandingLantern"; node.transform.localPosition = position + new Vector3(.45f, 0, 0);
            foreach (var behaviour in node.GetComponentsInChildren<MonoBehaviour>())
                if (behaviour.GetType().Name == "HandLanternVisual") Object.DestroyImmediate(behaviour);
            foreach (var lamp in node.GetComponentsInChildren<Light>()) { lamp.enabled = light; lamp.range = 4; lamp.intensity = 1.2f; lamp.shadows = LightShadows.None; }
        }

        static Transform Beam(Transform parent, string name, Vector3 point, Vector3 size, Quaternion rotation, Material material, bool collision)
        {
            var node = Child(parent, name, point); node.localRotation = rotation; node.localScale = size;
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = timber;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) node.gameObject.AddComponent<BoxCollider>();
            return node;
        }

        static void Link(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material)
        {
            Beam(parent, name, (a + b) * .5f, new Vector3(width, (b - a).magnitude, width), Quaternion.FromToRotation(Vector3.up, b - a), material, false);
        }

        static void Batch(Transform root, string prefix = "ScaffoldBatch")
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var pieces = new List<(Material Material, CombineInstance Combine)>();
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var materials = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    pieces.Add((materials[Mathf.Min(sub, materials.Length - 1)], new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix }));
            }
            int index = 0;
            foreach (var group in pieces.GroupBy(p => p.Material))
            {
                var combines = group.Select(p => p.Combine).ToArray();
                var mesh = new Mesh { name = prefix + "_" + index, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(combines, true, true); mesh.RecalculateBounds();
                var center = mesh.bounds.center;
                var centered = mesh.vertices;
                for (int i = 0; i < centered.Length; i++) centered[i] -= center;
                mesh.vertices = centered; mesh.RecalculateBounds();
                string path = Folder + mesh.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else
                {
                    existing.Clear(); existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.uv = mesh.uv; existing.tangents = mesh.tangents; existing.triangles = mesh.triangles;
                    existing.RecalculateBounds(); existing.UploadMeshData(false); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); mesh = existing;
                }
                var node = Child(root, prefix + "_" + index++, center);
                node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter.sharedMesh.name == "PlatformingTemporaryRope" && !AssetDatabase.Contains(filter.sharedMesh)) Object.DestroyImmediate(filter.sharedMesh);
                Object.DestroyImmediate(filter); Object.DestroyImmediate(renderer);
            }
        }
    }
}
