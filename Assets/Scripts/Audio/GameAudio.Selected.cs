using UnityEngine;

namespace PirateSlop
{
    public sealed partial class GameAudio
    {
        AudioSource lowHealthAudio, whirlpoolRumble, whirlpoolWater;
        public static bool HasCue(SoundCue cue)
        {
            var audio = Get();
            return audio != null && audio.entryMap.TryGetValue(cue, out var entry) && entry.Clips != null && entry.Clips.Length > 0;
        }
        public static SoundCue ResolveCue(SoundCue cue, SoundCue fallback) => HasCue(cue) ? cue : fallback;
        public static void StopLoop(AudioSource source) { if (source != null) { source.Stop(); source.volume = 0f; } }
        public static void Loop(ref AudioSource source, SoundCue cue, Transform parent, float scale, bool ui = false, bool ambience = false, int variant = 0)
        {
            var audio = Get();
            if (audio == null || !audio.entryMap.TryGetValue(cue, out var entry) || entry.Clips == null || entry.Clips.Length == 0) { StopLoop(source); return; }
            scale = Mathf.Max(0f, scale);
            if (source == null)
            {
                if (scale <= .001f) return;
                source = audio.Source(cue.ToString(), true);
                source.transform.SetParent(parent, false);
                source.volume = 0f;
                source.clip = entry.Clips[Mathf.Clamp(variant, 0, entry.Clips.Length - 1)];
                source.GetComponent<SpatialAudioTone>().Environment = ambience;
            }
            source.spatialBlend = ui ? 0f : 1f;
            source.minDistance = 2f; source.maxDistance = entry.Distance;
            float volume = Mathf.Clamp01(entry.Volume * audio.bank.Master * (ambience ? audio.bank.Ambience : audio.bank.Effects) * scale);
            source.volume = Mathf.Lerp(source.volume, volume, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            if (scale > .001f && !source.isPlaying && source.clip != null) source.Play();
            else if (scale <= .001f && source.volume < .001f) source.Stop();
        }
        void UpdateSelectedAmbience()
        {
            var health = listener != null ? listener.GetComponentInParent<CombatHealth>() : null;
            float fraction = health != null && !health.IsDead ? health.Current / Mathf.Max(1f, health.MaxHealth) : 1f;
            Loop(ref lowHealthAudio, SoundCue.LowHealth, transform, fraction < .25f ? Mathf.InverseLerp(.25f, .05f, fraction) * .65f : 0f, true);
            var sea = OceanSurface.Instance;
            float amount = 0f;
            Vector3 point = Vector3.zero;
            if (listener != null && sea != null && sea.WhirlpoolRadius > 0f)
            {
                Vector3 delta = listener.transform.position - sea.WhirlpoolCenter;
                delta.y = 0f;
                float clearance = Mathf.Max(0f, delta.magnitude - sea.WhirlpoolRadius);
                amount = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(clearance / 150f));
                point = sea.WhirlpoolCenter + delta.normalized * Mathf.Min(delta.magnitude, sea.WhirlpoolRadius);
                point.y = sea.Height(point);
            }
            float seaGain = Mathf.Lerp(1f, .3f, amount);
            ocean.volume *= seaGain; wind.volume *= seaGain;
            Loop(ref whirlpoolRumble, SoundCue.WhirlpoolNear, transform, amount * 2f, false, true, 0);
            if (entryMap.TryGetValue(SoundCue.WhirlpoolNear, out var water) && water.Clips != null && water.Clips.Length > 1)
                Loop(ref whirlpoolWater, SoundCue.WhirlpoolNear, transform, amount * 1.2f, false, true, 1);
            if (whirlpoolRumble != null) whirlpoolRumble.transform.position = point;
            if (whirlpoolWater != null) whirlpoolWater.transform.position = point;
        }
        public static void PlayRarity(string rarity)
        {
            var cue = rarity == "Legendary" ? SoundCue.UpgradeLegendary : rarity == "Epic" ? SoundCue.UpgradeEpic : rarity == "Rare" ? SoundCue.UpgradeRare : SoundCue.Select;
            Play(ResolveCue(cue, SoundCue.Select), Vector3.zero, 1f, true);
        }
    }
}
