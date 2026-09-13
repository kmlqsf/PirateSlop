using System;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class GameplayImprovementsSetup
    {
        const string Folder = "Assets/Models/FishingWeapons/";
        public static void Configure()
        {
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            string[] names = { "Pufferfish", "Swordfish" };
            var models = new GameObject[2];
            var pickups = new NetworkFish[2];
            for (int i = 0; i < 2; i++)
            {
                models[i] = CreateModel(names[i]);
                string path = Folder + names[i] + "Pickup.prefab";
                var root = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null ? path : "Assets/Prefabs/Networking/DroppedPistol.prefab");
                try
                {
                    foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    root.name = names[i] + "Pickup";
                    var visual = UnityEngine.Object.Instantiate(models[i], root.transform);
                    visual.transform.localPosition = Vector3.zero;
                    var shape = root.GetComponent<BoxCollider>();
                    shape.center = i == 0 ? Vector3.zero : new Vector3(0, 0, .15f);
                    shape.size = i == 0 ? new Vector3(.6f, .55f, .75f) : new Vector3(.45f, .6f, 1.7f);
                    root.GetComponent<NetworkFish>().Item = (InventoryItem)(19 + i);
                    if (root.GetComponent<NetworkFishProjectile>() == null) root.AddComponent<NetworkFishProjectile>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                pickups[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NetworkFish>();
                var net = pickups[i].GetComponent<NetworkObject>();
                string hash = new string((path + pickups[i].name).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                net.SetAssetPathHash(hash.GetStableHashU64());
                EditorUtility.SetDirty(net);
                registry.AddObject(net, true, true);
            }
            EditorUtility.SetDirty(registry);
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var drops = player.GetComponent<NetworkWeapon>().DropPrefabs;
                Array.Resize(ref drops, Mathf.Max(21, drops.Length));
                for (int i = 0; i < 2; i++) drops[19 + i] = pickups[i];
                player.GetComponent<NetworkWeapon>().DropPrefabs = drops;
                var equipment = player.GetComponent<NetworkEquipment>();
                var visuals = equipment.Models;
                Array.Resize(ref visuals, Mathf.Max(8, visuals.Length));
                for (int i = 0; i < 2; i++) visuals[6 + i] = models[i];
                equipment.Models = visuals;
                if (player.GetComponent<NetworkCrewBell>() == null) player.AddComponent<NetworkCrewBell>();
                var hitbox = player.transform.Find("PlayerHitbox");
                if (hitbox == null)
                {
                    hitbox = new GameObject("PlayerHitbox").transform;
                    hitbox.SetParent(player.transform, false);
                    var shape = hitbox.gameObject.AddComponent<CapsuleCollider>();
                    shape.isTrigger = true;
                    var body = player.GetComponent<CharacterController>();
                    shape.center = body.center;
                    shape.radius = body.radius * 1.65f;
                    shape.height = Mathf.Max(body.height + .2f, shape.radius * 2f);
                    hitbox.gameObject.AddComponent<PlayerHitbox>();
                }
                var icons = player.GetComponent<PlayerInventory>().Icons;
                var textures = icons.Icons;
                Array.Resize(ref textures, Mathf.Max(21, textures.Length));
                for (int i = 0; i < 2; i++) textures[19 + i] = CreateIcon(names[i], i == 0);
                icons.Icons = textures;
                EditorUtility.SetDirty(icons);
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var loose = catalog.LoosePrefabs;
            Array.Resize(ref loose, Mathf.Max(21, loose.Length));
            for (int i = 0; i < 2; i++) loose[19 + i] = pickups[i];
            catalog.LoosePrefabs = loose;
            EditorUtility.SetDirty(catalog);
            var bellModel = CreateModel("CrewBell");
            const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var ship = PrefabUtility.LoadPrefabContents(shipPath);
            try
            {
                var bell = ship.transform.Find("CrewBell");
                if (bell == null)
                {
                    bell = new GameObject("CrewBell").transform;
                    bell.SetParent(ship.transform, false);
                    UnityEngine.Object.Instantiate(bellModel, bell);
                    var shape = bell.gameObject.AddComponent<BoxCollider>();
                    shape.center = new Vector3(0, -.1f, 0);
                    shape.size = new Vector3(.52f, .95f, .54f);
                }
                bell.localPosition = new Vector3(1.4f, 5.85f, -8.8f);
                var wheel = ship.transform.Find("MainShipVisual/F2_Wheel");
                wheel.localScale = Vector3.one * 1.5f;
                var section = ship.transform.Find("ShipDestructionSections/F2_Wheel_0");
                if (section != null)
                {
                    var fragment = section.GetComponent<ShipDamageSection>().Fragments.FirstOrDefault();
                    if (fragment != null)
                    {
                        float size = fragment.GetComponent<MeshRenderer>().bounds.size.magnitude;
                        float ratio = size > .001f ? wheel.GetComponent<MeshRenderer>().bounds.size.magnitude / size : 1f;
                        section.localScale *= ratio;
                        section.localPosition = wheel.localPosition + (section.localPosition - wheel.localPosition) * ratio;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(ship, shipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(ship); }
            var ocean = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ocean.mat");
            ocean.SetFloat("_WaveStrength", 1.25f);
            ocean.SetFloat("_WaveScale", .14f);
            EditorUtility.SetDirty(ocean);
            CreateAudio();
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 75);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static GameObject CreateModel(string name)
        {
            var root = new GameObject(name);
            var visual = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".fbx"), root.transform);
            visual.transform.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    string materialPath = Folder + source.name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.SetFloat("_Smoothness", .3f);
                        if (source.name.Contains("Brass")) material.SetFloat("_Metallic", .65f);
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    material.color = source.name switch
                    {
                        "FishIvory" => new Color(.83f, .77f, .49f),
                        "PufferGold" => new Color(.64f, .42f, .11f),
                        "SwordfishBlue" => new Color(.08f, .24f, .32f),
                        "FishSilver" => new Color(.45f, .63f, .65f),
                        "FishEyes" => new Color(.014f, .021f, .026f),
                        "BellBrass" => new Color(.65f, .39f, .12f),
                        "BellIron" => new Color(.055f, .067f, .07f),
                        "BellRope" => new Color(.35f, .24f, .13f),
                        _ => source.color
                    };
                    EditorUtility.SetDirty(material);
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
            }
            string path = Folder + name + "Visual.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }
        static Texture2D CreateIcon(string name, bool puffer)
        {
            var texture = new Texture2D(96, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 96; x++)
            {
                float u = (x - 43f) / 30f, v = (y - 32f) / 23f;
                bool body = puffer ? u * u + v * v < 1 : u * u + v * v * 5 < 1;
                bool tail = u < -.7f && u > -1.35f && Mathf.Abs(v) < (-u - .7f) * 1.5f;
                bool bill = !puffer && u > .7f && u < 1.65f && Mathf.Abs(v) < .07f * (1.65f - u);
                bool fin = !puffer && u > -.4f && u < .3f && v > 0 && v < (.3f - u) * 1.4f;
                bool spine = puffer && u * u + v * v < 1.25f && Mathf.Cos(Mathf.Atan2(v, u) * 16) > .8f;
                texture.SetPixel(x, y, body || tail || bill || fin || spine ? Color.white : Color.clear);
            }
            texture.Apply();
            string path = Folder + name + "Icon.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void CreateAudio()
        {
            Directory.CreateDirectory("Assets/Audio/FishingWeapons");
            var bank = Resources.Load<GameAudioBank>("GameAudioBank");
            var entries = bank.Entries.ToList();
            var cues = new[] { SoundCue.PufferThrow, SoundCue.PufferBurst, SoundCue.SwordfishThrow, SoundCue.SwordfishStick, SoundCue.ShipBell };
            foreach (var cue in cues)
            {
                const int rate = 44100;
                float seconds = cue == SoundCue.ShipBell ? 4f : cue == SoundCue.PufferBurst ? 1f : .5f;
                int length = Mathf.RoundToInt(rate * seconds);
                string path = "Assets/Audio/FishingWeapons/" + cue + ".wav";
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + length * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                    writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(length * 2);
                    var random = new System.Random((int)cue);
                    float low = 0;
                    for (int i = 0; i < length; i++)
                    {
                        float t = i / (float)rate;
                        float noise = (float)random.NextDouble() * 2 - 1;
                        low = Mathf.Lerp(low, noise, .12f);
                        float value;
                        if (cue == SoundCue.ShipBell)
                            value = (.4f * Mathf.Sin(t * 2 * Mathf.PI * 620) * Mathf.Exp(-t * 1.2f) + .22f * Mathf.Sin(t * 2 * Mathf.PI * 1649) * Mathf.Exp(-t * 2.1f) + .12f * Mathf.Sin(t * 2 * Mathf.PI * 3361) * Mathf.Exp(-t * 3f)) * Mathf.Min(1, t * 800);
                        else if (cue == SoundCue.PufferBurst)
                            value = (.7f * low + .3f * Mathf.Sin(2 * Mathf.PI * (100 * t - 30 * t * t))) * Mathf.Exp(-t * 7) * Mathf.Min(1, t * 500);
                        else if (cue == SoundCue.SwordfishStick)
                            value = (.55f * noise * Mathf.Exp(-t * 70) + .35f * Mathf.Sin(t * 2 * Mathf.PI * 180) * Mathf.Exp(-t * 16)) * Mathf.Min(1, t * 1000);
                        else
                            value = (cue == SoundCue.PufferThrow ? low + .16f * Mathf.Sin(2 * Mathf.PI * (500 * t - 250 * t * t)) : noise - low) * Mathf.Sin(Mathf.PI * t / seconds) * Mathf.Exp(-t * 5) * .65f;
                        writer.Write((short)(Mathf.Clamp(value, -1f, 1f) * 30000));
                    }
                }
                AssetDatabase.ImportAsset(path);
                entries.RemoveAll(e => e.Cue == cue);
                entries.Add(new GameAudioBank.Entry { Cue = cue, Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(path) }, Volume = .8f, Distance = cue == SoundCue.ShipBell ? 90 : 30 });
            }
            bank.Entries = entries.ToArray();
            EditorUtility.SetDirty(bank);
        }
    }
}
