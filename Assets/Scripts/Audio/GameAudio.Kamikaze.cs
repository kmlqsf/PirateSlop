using UnityEngine;

namespace PirateSlop
{
    public sealed partial class GameAudio
    {
        public static void KamikazeExplosion(AudioClip clip, Vector3 position)
        {
            var audio = Get();
            if (audio == null || clip == null) return;
            var source = audio.voices[audio.nextVoice];
            audio.nextVoice = (audio.nextVoice + 1) % audio.voices.Length;
            source.GetComponent<DistanceShotAudio>()?.ResetPlayback();
            source.Stop(); source.transform.position = position;
            source.loop = false; source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 5f; source.maxDistance = 140f;
            source.priority = 80; source.pitch = 1f;
            source.volume = .7f * audio.bank.Master * audio.bank.Effects;
            source.clip = clip; source.Play();
        }

        public static void KamikazeLoop(ref AudioSource source, AudioClip clip, Transform parent, bool playing, float volume, float distance)
        {
            if (!playing || clip == null)
            {
                if (source != null && source.isPlaying) source.Stop();
                return;
            }
            var audio = Get();
            if (audio == null) return;
            if (source == null)
            {
                var go = new GameObject("Kamikaze scream");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.up * 1.5f;
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = true; source.dopplerLevel = 0;
                source.spatialBlend = 1f; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f; source.priority = 100;
                go.AddComponent<SpatialAudioTone>();
            }
            source.maxDistance = Mathf.Max(4f, distance);
            source.volume = Mathf.Clamp01(volume) * audio.bank.Master * audio.bank.Effects;
            if (source.clip != clip) { source.Stop(); source.clip = clip; }
            if (!source.isPlaying)
            {
                source.time = Random.value * Mathf.Max(0f, clip.length - .01f);
                source.Play();
            }
        }
    }
}
