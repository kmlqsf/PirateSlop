using UnityEngine;

namespace PirateSlop
{
    public sealed class GameAudio : MonoBehaviour
    {
        static GameAudio instance;
        GameAudioBank bank;
        AudioSource[] voices;
        AudioSource[] feedbackVoices;
        int nextFeedback;
        AudioSource ocean, wind;
        int nextVoice;

        static GameAudio Get()
        {
            if (!Application.isPlaying || Application.isBatchMode) return null;
            if (instance != null) return instance;
            var bank = Resources.Load<GameAudioBank>("GameAudioBank");
            if (bank == null) return null;
            instance = new GameObject("GameAudio").AddComponent<GameAudio>();
            DontDestroyOnLoad(instance.gameObject);
            instance.bank = bank;
            instance.voices = new AudioSource[32];
            for (int i = 0; i < instance.voices.Length; i++) instance.voices[i] = instance.Source("Effect " + i, false);
            instance.feedbackVoices = new AudioSource[4];
            for (int i = 0; i < instance.feedbackVoices.Length; i++) instance.feedbackVoices[i] = instance.Source("Damage feedback " + i, false);
            instance.ocean = instance.Source("Ocean", true); instance.ocean.clip = bank.Ocean;
            instance.wind = instance.Source("Wind", true); instance.wind.clip = bank.Wind;
            return instance;
        }
        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
        public static void Play(SoundCue cue, Vector3 position, float scale = 1f, bool ui = false)
        {
            var audio = Get(); if (audio == null) return;
            var entry = System.Array.Find(audio.bank.Entries, e => e.Cue == cue);
            if (entry == null || entry.Clips == null || entry.Clips.Length == 0) return;
            bool feedback = cue == SoundCue.Hurt || cue == SoundCue.Death || cue == SoundCue.HitConfirm;
            var source = feedback ? audio.feedbackVoices[audio.nextFeedback] : audio.voices[audio.nextVoice];
            if (feedback) audio.nextFeedback = (audio.nextFeedback + 1) % audio.feedbackVoices.Length;
            else audio.nextVoice = (audio.nextVoice + 1) % audio.voices.Length;
            source.Stop(); source.transform.position = position;
            source.spatialBlend = ui ? 0f : 1f;
            source.minDistance = cue == SoundCue.Cannon ? 8f : 2f; source.maxDistance = entry.Distance;
            source.volume = Mathf.Clamp01(entry.Volume * scale * audio.bank.Master * (ui && !feedback ? audio.bank.Interface : audio.bank.Effects));
            source.pitch = ui ? 1f : Random.Range(.94f, 1.06f);
            source.clip = entry.Clips[Random.Range(0, entry.Clips.Length)]; source.Play();
        }
        public static void Ambience(float speed)
        {
            var audio = Get(); if (audio == null) return;
            audio.ocean.volume = audio.bank.Master * audio.bank.Ambience * Mathf.Lerp(.45f, .75f, speed);
            audio.wind.volume = audio.bank.Master * audio.bank.Ambience * Mathf.Lerp(.15f, .4f, speed);
            if (!audio.ocean.isPlaying && audio.ocean.clip != null) audio.ocean.Play();
            if (!audio.wind.isPlaying && audio.wind.clip != null) audio.wind.Play();
            audio.lastAmbience = Time.unscaledTime;
        }
        float lastAmbience;
        void Update()
        {
            if (Time.unscaledTime - lastAmbience < .3f) return;
            ocean.Stop(); wind.Stop();
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
