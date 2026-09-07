using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class FishingSetup
    {
        const string Folder = "Assets/Models/Fishing";
        [MenuItem("PirateSlop/Configure Fishing")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory("Assets/Audio/Fishing");
            AssetDatabase.Refresh();
            var wood = Material("RodWood", new Color(.25f, .12f, .045f), .25f);
            var metal = Material("ReelBrass", new Color(.65f, .43f, .16f), .65f);
            var skin = Material("FishSilver", new Color(.22f, .39f, .41f), .18f);
            var fin = Material("FishFins", new Color(.12f, .25f, .27f), .15f);
            foreach (var fishMaterial in new[] { skin, fin })
            {
                fishMaterial.SetFloat("_SpecularHighlights", 0f);
                fishMaterial.SetFloat("_EnvironmentReflections", 0f);
                fishMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                fishMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                EditorUtility.SetDirty(fishMaterial);
            }
            var white = Material("FloatWhite", new Color(.87f, .84f, .69f), .3f);
            var red = Material("FloatRed", new Color(.72f, .12f, .06f), .35f);
            var black = Material("Line", new Color(.08f, .1f, .09f), .1f);
            var rod = new GameObject("FishingRod");
            Segment(rod.transform, "Grip", new Vector3(0, 0, -.15f), new Vector3(0, 0, .3f), .027f, wood);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f, b = (i + 1) / 7f;
                Segment(rod.transform, "RodSection", new Vector3(0, .15f * a * a, .3f + 1.4f * a), new Vector3(0, .15f * b * b, .3f + 1.4f * b), Mathf.Lerp(.018f, .005f, a), wood);
                Sphere(rod.transform, "Binding", new Vector3(0, .15f * a * a, .3f + 1.4f * a), new Vector3(.043f * (1 - a * .65f), .043f * (1 - a * .65f), .025f), metal);
            }
            Sphere(rod.transform, "Reel", new Vector3(0, -.065f, .12f), new Vector3(.12f, .14f, .14f), metal);
            Segment(rod.transform, "Crank", new Vector3(.05f, -.065f, .12f), new Vector3(.12f, -.11f, .12f), .008f, metal);
            Sphere(rod.transform, "Handle", new Vector3(.12f, -.11f, .12f), new Vector3(.035f, .025f, .025f), wood);
            var rodPrefab = Save(rod, "FishingRod");
            var fish = new GameObject("FishVisual");
            var body = new GameObject("Body"); body.transform.SetParent(fish.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = FishMesh(); body.AddComponent<MeshRenderer>().sharedMaterial = skin;
            Fin(fish.transform, "Tail", new[] { new Vector3(0,0,-.22f), new Vector3(0,.16f,-.38f), new Vector3(0,0,-.32f), new Vector3(0,-.16f,-.38f) }, fin);
            Fin(fish.transform, "DorsalFin", new[] { new Vector3(0,.1f,.06f), new Vector3(0,.22f,-.11f), new Vector3(0,.075f,-.2f) }, fin);
            foreach (float side in new[] { -1f, 1f })
            {
                Sphere(fish.transform, "Eye", new Vector3(.065f * side, .045f, .17f), new Vector3(.029f, .037f, .037f), white);
                Sphere(fish.transform, "Pupil", new Vector3(.078f * side, .045f, .174f), new Vector3(.012f, .022f, .022f), black);
                Fin(fish.transform, side < 0 ? "LeftFin" : "RightFin", new[] { new Vector3(.055f * side, -.025f, .05f), new Vector3(.19f * side, -.08f, -.09f), new Vector3(.045f * side, -.05f, -.1f) }, fin);
            }
            var fishPrefab = Save(fish, "FishVisual");
            var bobber = new GameObject("FishingFloat");
            Sphere(bobber.transform, "Body", Vector3.zero, new Vector3(.1f, .16f, .1f), white);
            Sphere(bobber.transform, "Tip", new Vector3(0,.09f,0), new Vector3(.065f,.1f,.065f), red);
            var floatPrefab = Save(bobber, "FishingFloat");
            var dropped = new GameObject("NetworkFish");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(fishPrefab); visual.transform.SetParent(dropped.transform, false);
            var collider = dropped.AddComponent<BoxCollider>(); collider.center = new Vector3(0,0,-.04f); collider.size = new Vector3(.18f,.28f,.65f);
            dropped.AddComponent<NetworkObject>(); dropped.AddComponent<NetworkFish>();
            const string dropPath = "Assets/Prefabs/Networking/NetworkFish.prefab";
            var dropPrefab = PrefabUtility.SaveAsPrefabAsset(dropped, dropPath); Object.DestroyImmediate(dropped);
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var fishing = player.GetComponent<NetworkFishing>(); if (fishing == null) fishing = player.AddComponent<NetworkFishing>();
                fishing.RodModel = rodPrefab; fishing.FishModel = fishPrefab; fishing.FloatModel = floatPrefab;
                fishing.FishPrefab = dropPrefab.GetComponent<NetworkFish>(); fishing.LineMaterial = black;
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var prefabs = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            prefabs.AddObject(dropPrefab.GetComponent<NetworkObject>(), true, true); EditorUtility.SetDirty(prefabs);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(11, config.ProtocolVersion); EditorUtility.SetDirty(config);
            CreateAudio(); ConfigureInventoryDrops(); AssetDatabase.SaveAssets();
        }
        [MenuItem("PirateSlop/Configure Inventory Drops")]
        public static void ConfigureInventoryDrops()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkShip.prefab");
            var drops = new NetworkFish[4];
            drops[0] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkFish.prefab").GetComponent<NetworkFish>();
            drops[1] = CreateDrop(InventoryItem.Pistol, playerAsset.GetComponent<PirateWeapon>().WorldPivot.Find("SM_PiratePistol").gameObject);
            drops[2] = CreateDrop(InventoryItem.Rod, AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/FishingRod.prefab"));
            drops[3] = CreateDrop(InventoryItem.Cannon, ship.GetComponentInChildren<CannonballCrate>(true).Kit);
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                player.GetComponent<NetworkWeapon>().DropPrefabs = drops;
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var prefabs = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            foreach (var drop in drops) prefabs.AddObject(drop.GetComponent<NetworkObject>(), true, true);
            EditorUtility.SetDirty(prefabs);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(13, config.ProtocolVersion); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static NetworkFish CreateDrop(InventoryItem kind, GameObject source)
        {
            var root = new GameObject("Dropped" + kind);
            try
            {
                var visual = Object.Instantiate(source, root.transform);
                visual.name = "Visual"; visual.SetActive(true);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = source.transform.lossyScale;
                foreach (var script in visual.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) { renderer.enabled = true; bounds.Encapsulate(renderer.bounds); }
                var box = root.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkFish>().Item = kind;
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Networking/Dropped" + kind + ".prefab").GetComponent<NetworkFish>();
            }
            finally { Object.DestroyImmediate(root); }
        }
        static Material Material(string name, Color color, float smoothness)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; material.SetFloat("_Smoothness", smoothness); EditorUtility.SetDirty(material); return material;
        }
        static GameObject Save(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab"); Object.DestroyImmediate(root); return prefab;
        }
        static void Sphere(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
        }
        static void Segment(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = (start + end) * .5f; go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            go.transform.localScale = new Vector3(radius * 2, Vector3.Distance(start, end) * .5f, radius * 2);
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
        }
        static Mesh StoreMesh(string name, Vector3[] vertices, int[] triangles, Color[] colors = null)
        {
            string path = Folder + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = triangles; if (colors != null) mesh.colors = colors;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
        }
        static Mesh FishMesh()
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            float[] z = { -.26f, -.19f, -.09f, .04f, .15f, .24f, .28f };
            float[] r = { .015f, .045f, .075f, .085f, .067f, .035f, .001f };
            for (int j = 0; j < z.Length; j++) for (int i = 0; i < 12; i++)
            { float a = i * Mathf.PI / 6; vertices.Add(new Vector3(Mathf.Cos(a) * r[j], Mathf.Sin(a) * r[j] * 1.5f, z[j])); }
            for (int j = 0; j < z.Length - 1; j++) for (int i = 0; i < 12; i++)
            {
                int a = j * 12 + i, b = j * 12 + (i + 1) % 12;
                indices.AddRange(new[] { a, b, a + 12, b, b + 12, a + 12 });
            }
            return StoreMesh("FishBody", vertices.ToArray(), indices.ToArray());
        }
        static void Fin(Transform parent, string name, Vector3[] vertices, Material material)
        {
            var indices = new List<int>();
            int count = vertices.Length;
            var sides = vertices.Concat(vertices).ToArray();
            for (int i = 1; i < count - 1; i++) indices.AddRange(new[] { 0, i, i + 1, count, count + i + 1, count + i });
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = StoreMesh(name, sides, indices.ToArray()); go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        static void CreateAudio()
        {
            var cues = new[] { SoundCue.FishingCast, SoundCue.FishingBite, SoundCue.FishingReel, SoundCue.FishingCatch, SoundCue.FishDrop, SoundCue.FishEat, SoundCue.FishingEscape };
            foreach (var cue in cues) WriteSound(cue);
            AssetDatabase.Refresh();
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            var entries = bank.Entries.ToList();
            foreach (var cue in cues)
            {
                var entry = entries.Find(e => e.Cue == cue);
                if (entry == null) { entry = new GameAudioBank.Entry { Cue = cue }; entries.Add(entry); }
                entry.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Fishing/" + cue + ".wav") };
                entry.Volume = cue == SoundCue.FishingBite ? .9f : .65f; entry.Distance = 30f;
            }
            bank.Entries = entries.ToArray(); EditorUtility.SetDirty(bank);
        }
        static void WriteSound(SoundCue cue)
        {
            const int rate = 44100; float duration = cue == SoundCue.FishEat ? 1.1f : cue == SoundCue.FishingReel ? .36f : .65f;
            int count = Mathf.RoundToInt(duration * rate); var random = new System.Random(8321 + (int)cue);
            using var writer = new BinaryWriter(File.Create("Assets/Audio/Fishing/" + cue + ".wav"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            float filtered = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate, noise = (float)random.NextDouble() * 2 - 1;
                filtered = Mathf.Lerp(filtered, noise, .14f);
                float envelope = Mathf.Min(1f, t * 80) * Mathf.Pow(1f - t / duration, 1.7f), value;
                switch (cue)
                {
                    case SoundCue.FishingBite:
                        value = Mathf.Sin(t * 2 * Mathf.PI * 1650) * Mathf.Exp(-t * 10) + .45f * Mathf.Sin(t * 2 * Mathf.PI * 2475) * Mathf.Exp(-t * 13) + filtered * .3f; break;
                    case SoundCue.FishingReel:
                        value = noise * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 2 * Mathf.PI * 28)), 16) * .65f + filtered * .15f; break;
                    case SoundCue.FishEat:
                        value = (noise * .28f + filtered) * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 2 * Mathf.PI * 4)), 3); break;
                    case SoundCue.FishDrop:
                        value = Mathf.Sin(2 * Mathf.PI * (120 * t - 35 * t * t)) * Mathf.Exp(-t * 24) + filtered * Mathf.Exp(-t * 10); break;
                    case SoundCue.FishingCast:
                        value = noise * .18f * Mathf.Sin(t / duration * Mathf.PI) + filtered * .8f; break;
                    case SoundCue.FishingCatch:
                        value = filtered * .9f + Mathf.Sin(2 * Mathf.PI * (430 * t - 190 * t * t)) * Mathf.Exp(-t * 8) * .4f; break;
                    default:
                        value = filtered * .7f + Mathf.Sin(2 * Mathf.PI * (700 * t - 350 * t * t)) * Mathf.Exp(-t * 10) * .35f; break;
                }
                writer.Write((short)(Mathf.Clamp(value * envelope * .7f, -1, 1) * short.MaxValue));
            }
        }
    }
}
