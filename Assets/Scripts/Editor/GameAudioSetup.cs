using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class GameAudioSetup
    {
        static AudioClip Clip(string name)
        {
            var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }).Select(AssetDatabase.GUIDToAssetPath);
            var path = paths.FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
            if (path == null) throw new InvalidOperationException("Missing audio: " + name);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static GameAudioBank.Entry Entry(SoundCue cue, float volume, float distance, params string[] clips)
            => new GameAudioBank.Entry { Cue = cue, Volume = volume, Distance = distance, Clips = clips.Select(Clip).ToArray() };

        [MenuItem("PirateSlop/Configure Game Audio")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            if (bank == null) { bank = ScriptableObject.CreateInstance<GameAudioBank>(); AssetDatabase.CreateAsset(bank, "Assets/Resources/GameAudioBank.asset"); }
            bank.Entries = new[] {
                Entry(SoundCue.Pistol, .8f, 90, "pistol_black_powder"),
                Entry(SoundCue.Cannon, 1f, 230, "cannon_fire_1"),
                Entry(SoundCue.Knife, .55f, 16, "knifeSlice", "knifeSlice2"),
                Entry(SoundCue.Reload, .4f, 12, "metalLatch"),
                Entry(SoundCue.ReloadReady, .4f, 12, "metalClick"),
                Entry(SoundCue.DryFire, .4f, 8, "metalClick"),
                Entry(SoundCue.Footstep, .35f, 20, "footstep00", "footstep01", "footstep02", "footstep03", "footstep04", "footstep05"),
                Entry(SoundCue.Jump, .35f, 15, "cloth1", "cloth2"),
                Entry(SoundCue.Land, .6f, 22, "footstep06", "footstep07"),
                Entry(SoundCue.Slide, .4f, 20, "cloth3", "cloth4"),
                Entry(SoundCue.Hurt, .6f, 25, "chop"),
                Entry(SoundCue.Death, .6f, 30, "dropLeather"),
                Entry(SoundCue.Respawn, .45f, 15, "clothBelt"),
                Entry(SoundCue.Wheel, .32f, 18, "wheel_oak"),
                Entry(SoundCue.Sail, .5f, 30, "cloth3", "cloth4"),
                Entry(SoundCue.Barrel, .3f, 18, "creak3", "metalLatch"),
                Entry(SoundCue.Load, .4f, 20, "metalPot1", "metalPot2", "metalPot3"),
                Entry(SoundCue.Pickup, .4f, 10, "handleSmallLeather", "handleSmallLeather2"),
                Entry(SoundCue.Place, .5f, 25, "bookPlace1", "bookPlace2"),
                Entry(SoundCue.Select, .35f, 5, "beltHandle1", "beltHandle2"),
                Entry(SoundCue.Impact, .35f, 25, "chop"),
                Entry(SoundCue.ShipHit, .75f, 130, "cannon_hit_ship_short"),
                Entry(SoundCue.ShipDeath, .85f, 200, "ship_destroyed_short"),
                Entry(SoundCue.ShipCollision, .65f, 100, "ship_ram_ship_shortened"),
                Entry(SoundCue.Splash, .6f, 100, "cannon_miss_1"),
                Entry(SoundCue.Creak, .3f, 35, "creak1", "creak2", "creak3")
            };
            bank.Ocean = Clip("ocean"); bank.Wind = Clip("Wind");
            EditorUtility.SetDirty(bank);
            foreach (var path in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool ambience = path.Contains("/Ambience/");
                importer.forceToMono = !ambience;
                var settings = importer.defaultSampleSettings;
                settings.loadType = ambience ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            foreach (var path in new[] { "Assets/Prefabs/Networking/NetworkPlayer.prefab", "Assets/Prefabs/Networking/NetworkShip.prefab", "Assets/Prefabs/Cannons/CannonStation.prefab", "Assets/Prefabs/Cannons/DeployableCannon.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Attach(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var config = AssetDatabase.LoadAssetAtPath<PirateSlop.Networking.SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 5); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static void Attach(GameObject root)
        {
            if (root.GetComponent<GameplayAudio>() == null) root.AddComponent<GameplayAudio>();
        }
    }
}
