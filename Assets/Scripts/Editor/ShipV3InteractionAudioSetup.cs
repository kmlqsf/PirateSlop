using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipV3InteractionAudioSetup
    {
        const string Folder = "Assets/Audio/ShipInteractions/";

        public static void Configure()
        {
            Export("CandleSource", "FlameLight", .97f, 1.27f, .65f);
            Export("CandleSource", "FlameExtinguish", 5.58f, 5.96f, .55f);
            Export("DiceTableSource", "DiceSlide1", .80f, 1.07f, .48f);
            Export("DiceTableSource", "DiceSlide2", 1.38f, 1.65f, .48f);
            Export("DiceTableSource", "DiceSlide3", 1.69f, 1.92f, .48f);
            Export("DiceCupSource", "DiceImpact1", .68f, .80f, .55f);
            Export("DiceCupSource", "DiceImpact2", 1.83f, 1.95f, .55f);
            Export("DiceCupSource", "DiceImpact3", 2.15f, 2.28f, .55f);
            Export("DiceCupSource", "DiceCup1", .90f, 1.12f, .5f);
            Export("DiceCupSource", "DiceCup2", 1.30f, 1.52f, .5f);
            Export("DiceCupSource", "DiceCup3", 1.65f, 1.87f, .5f);
            Export("BellSingleSource", "BellRing1", .09f, 4.32f, .7f);
            Export("BellDoubleSource", "BellRing2", .39f, .70f, .7f);
            Export("BellDoubleSource", "BellRing3", .70f, 2.90f, .7f);
            AssetDatabase.Refresh();
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            var entries = bank.Entries.ToList();
            Assign(entries, SoundCue.FlameLight, new[] { "FlameLight" }, .5f, 12f);
            Assign(entries, SoundCue.FlameExtinguish, new[] { "FlameExtinguish" }, .6f, 12f);
            Assign(entries, SoundCue.DiceSlide, new[] { "DiceSlide1", "DiceSlide2", "DiceSlide3" }, .6f, 12f);
            Assign(entries, SoundCue.DiceImpact, new[] { "DiceImpact1", "DiceImpact2", "DiceImpact3" }, .55f, 12f);
            Assign(entries, SoundCue.DiceCup, new[] { "DiceCup1", "DiceCup2", "DiceCup3" }, .6f, 12f);
            var bell = entries.Single(e => e.Cue == SoundCue.ShipBell);
            var retained = bell.Clips.Where(c => c != null && !AssetDatabase.GetAssetPath(c).StartsWith(Folder, StringComparison.Ordinal)).ToArray();
            Assign(entries, SoundCue.ShipBell, new[] { "BellRing1", "BellRing2", "BellRing3" }, bell.Volume, bell.Distance);
            bell.Clips = retained.Concat(bell.Clips).ToArray();
            bank.Entries = entries.ToArray();
            EditorUtility.SetDirty(bank); AssetDatabase.SaveAssetIfDirty(bank);
        }

        static void Assign(System.Collections.Generic.List<GameAudioBank.Entry> entries, SoundCue cue, string[] names, float volume, float distance)
        {
            var entry = entries.FirstOrDefault(e => e.Cue == cue);
            if (entry == null) { entry = new GameAudioBank.Entry { Cue = cue }; entries.Add(entry); }
            entry.Volume = volume; entry.Distance = distance;
            entry.Clips = names.Select(name => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav")).ToArray();
            if (entry.Clips.Any(c => c == null)) throw new InvalidOperationException("Audio import incomplete: " + cue);
        }

        static void Export(string source, string name, float start, float end, float peak)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + source + ".mp3");
            var input = new float[clip.samples * clip.channels];
            if (!clip.GetData(input, 0)) throw new InvalidOperationException("Cannot read audio: " + source);
            int begin = Mathf.RoundToInt(start * clip.frequency), count = Mathf.RoundToInt((end - start) * clip.frequency);
            var mono = new float[count]; float max = .0001f;
            for (int frame = 0; frame < count; frame++)
            {
                for (int channel = 0; channel < clip.channels; channel++) mono[frame] += input[(begin + frame) * clip.channels + channel] / clip.channels;
                max = Mathf.Max(max, Mathf.Abs(mono[frame]));
            }
            int fade = Mathf.RoundToInt(.01f * clip.frequency);
            string path = Folder + name + ".wav";
            using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(clip.frequency); writer.Write(clip.frequency * 2);
                writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int frame = 0; frame < count; frame++)
                {
                    float envelope = Mathf.Min(1f, Mathf.Min(frame / (float)fade, (count - 1 - frame) / (float)fade));
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(mono[frame] * peak / max * envelope, -1f, 1f) * 32767f));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
        }
    }
}
