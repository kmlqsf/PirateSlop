using UnityEngine;

namespace PirateSlop
{
    public sealed class GameAudio : MonoBehaviour
    {
        static GameAudio instance;
        GameAudioBank bank;
        AudioSource[] voices;
        AudioSource[] feedbackVoices;
        AudioSource[] shotVoices;
        AudioListener listener;
        int nextShot;
        int nextFeedback;
        AudioSource ocean, wind, deck;
        AudioSource underwater;
        AudioSource flooding;
        float floodBlend;
        float cabinBlend, underwaterBlend, environmentAt, cabinTarget, underwaterTarget;
        float underwaterVolume;
        int nextVoice;

        static GameAudio Get()
        {
            if (!Application.isPlaying || Application.isBatchMode) return null;
            if (instance != null) return instance;
            var bank = Resources.Load<GameAudioBank>("GameAudioBank");
            if (bank == null) return null;
            bank.Master = PlayerPrefs.GetFloat("AudioMaster",bank.Master);
            bank.Effects = PlayerPrefs.GetFloat("AudioEffects",bank.Effects);
            bank.Ambience = PlayerPrefs.GetFloat("AudioAmbience",bank.Ambience);
            bank.Interface = PlayerPrefs.GetFloat("AudioInterface",bank.Interface);
            instance = new GameObject("GameAudio").AddComponent<GameAudio>();
            DontDestroyOnLoad(instance.gameObject);
            instance.bank = bank;
            instance.voices = new AudioSource[32];
            for (int i = 0; i < instance.voices.Length; i++) instance.voices[i] = instance.Source("Effect " + i, false);
            instance.feedbackVoices = new AudioSource[4];
            instance.shotVoices = new AudioSource[12];
            for (int i = 0; i < instance.shotVoices.Length; i++)
            {
                instance.shotVoices[i] = instance.Source("Gunshot " + i, false);
            }
            for (int i = 0; i < instance.feedbackVoices.Length; i++) instance.feedbackVoices[i] = instance.Source("Damage feedback " + i, false);
            instance.ocean = instance.Source("Ocean", true); instance.ocean.clip = bank.Ocean;
            instance.wind = instance.Source("Wind", true); instance.wind.clip = bank.Wind;
            instance.deck = instance.Source("Deck creaks", true); instance.deck.clip = bank.DeckCreaks;
            instance.underwater = instance.Source("Underwater bubbles", true);
            instance.underwater.GetComponent<SpatialAudioTone>().Environment = false;
            instance.flooding = instance.Source("Water inside hull", true);
            instance.flooding.pitch = .65f;
            var bubbles = System.Array.Find(bank.Entries, e => e.Cue == SoundCue.UnderwaterBubbles);
            if (bubbles != null && bubbles.Clips != null && bubbles.Clips.Length > 0)
            {
                instance.underwater.clip = bubbles.Clips[0];
                instance.flooding.clip = bubbles.Clips[0];
                instance.underwaterVolume = bubbles.Volume;
            }
            return instance;
        }
        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            go.AddComponent<SpatialAudioTone>().Environment = loop;
            return source;
        }
        public static void Firearm(FirearmDefinition definition,Vector3 position)
        {
            Play(definition.Sound,position,1,false,definition);
        }
        public static void Play(SoundCue cue, Vector3 position, float scale = 1f, bool ui = false,FirearmDefinition firearm=null)
        {
            var audio = Get(); if (audio == null) return;
            var entry = System.Array.Find(audio.bank.Entries, e => e.Cue == cue);
            if(firearm!=null && firearm.ShotClips!=null && firearm.ShotClips.Length>0)
                entry=new GameAudioBank.Entry { Cue=cue,Clips=firearm.ShotClips,Volume=firearm.ShotVolume,Distance=firearm.AudibleDistance };
            if (entry == null || entry.Clips == null || entry.Clips.Length == 0) return;
            bool feedback = cue == SoundCue.Hurt || cue == SoundCue.Death || cue == SoundCue.HitConfirm || cue == SoundCue.AirWarning;
            bool gunshot = firearm!=null || cue == SoundCue.Pistol || cue == SoundCue.Musket || cue == SoundCue.DoubleBarrel;
            var source = gunshot ? audio.shotVoices[audio.nextShot] : feedback ? audio.feedbackVoices[audio.nextFeedback] : audio.voices[audio.nextVoice];
            if (gunshot) audio.nextShot = (audio.nextShot + 1) % audio.shotVoices.Length;
            else if (feedback) audio.nextFeedback = (audio.nextFeedback + 1) % audio.feedbackVoices.Length;
            else audio.nextVoice = (audio.nextVoice + 1) % audio.voices.Length;
            source.Stop(); source.transform.position = position;
            source.spatialBlend = ui ? 0f : 1f;
            source.minDistance = cue == SoundCue.Cannon ? 8f : gunshot ? 5f : 2f; source.maxDistance = entry.Distance;
            source.priority = gunshot ? 40 : feedback ? 32 : 128;
            source.volume = Mathf.Clamp01(entry.Volume * scale * audio.bank.Master * (ui && !feedback ? audio.bank.Interface : audio.bank.Effects));
            source.pitch = ui ? 1f : gunshot ? Random.Range(.975f,1.025f) : Random.Range(.94f, 1.06f);
            source.clip = entry.Clips[Random.Range(0, entry.Clips.Length)]; source.Play();
        }
        public static void Attached(ref AudioSource source, SoundCue cue, Transform parent)
        {
            var audio = Get();
            if (audio == null) return;
            var entry = System.Array.Find(audio.bank.Entries, e => e.Cue == cue);
            if (entry == null || entry.Clips == null || entry.Clips.Length == 0) return;
            if (source == null)
            {
                var go = new GameObject("Attached audio");
                go.transform.SetParent(parent, false);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false; source.dopplerLevel = 0f;
                source.spatialBlend = 1f; source.rolloffMode = AudioRolloffMode.Linear;
                go.AddComponent<SpatialAudioTone>();
            }
            source.Stop(); source.minDistance = 2f; source.maxDistance = entry.Distance;
            source.volume = entry.Volume * audio.bank.Master * audio.bank.Effects;
            source.clip = entry.Clips[Random.Range(0, entry.Clips.Length)]; source.Play();
        }
        public static void Ambience(float speed, bool onShip = false, float floodLevel = 0f)
        {
            var audio = Get(); if (audio == null) return;
            audio.UpdateEnvironment(onShip);
            audio.floodBlend = Mathf.Lerp(audio.floodBlend, onShip ? Mathf.Clamp01(floodLevel) : 0f, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            audio.flooding.volume = audio.bank.Master * audio.bank.Ambience * audio.floodBlend * Mathf.Lerp(.25f, .75f, audio.cabinBlend);
            audio.flooding.pitch = Mathf.Lerp(.65f, .9f, audio.floodBlend);
            if (audio.floodBlend > .005f && !audio.flooding.isPlaying && audio.flooding.clip != null) audio.flooding.Play();
            else if (audio.floodBlend <= .005f) audio.flooding.Stop();
            audio.ocean.volume = audio.bank.Master * audio.bank.Ambience * Mathf.Lerp(.45f, .75f, speed);
            audio.wind.volume = audio.bank.Master * audio.bank.Ambience * Mathf.Lerp(.15f, .4f, speed);
            audio.ocean.volume *= Mathf.Lerp(1f, .4f, audio.cabinBlend) * Mathf.Lerp(1f, .18f, audio.underwaterBlend);
            audio.wind.volume *= Mathf.Lerp(1f, .15f, audio.cabinBlend) * (1f - audio.underwaterBlend);
            if (!audio.ocean.isPlaying && audio.ocean.clip != null) audio.ocean.Play();
            if (!audio.wind.isPlaying && audio.wind.clip != null) audio.wind.Play();
            audio.deck.volume = audio.bank.Master * audio.bank.Ambience * Mathf.Lerp(.2f, .4f, speed);
            audio.deck.volume *= Mathf.Lerp(1f, 1.6f, audio.cabinBlend) * Mathf.Lerp(1f, .3f, audio.underwaterBlend);
            audio.underwater.volume = audio.bank.Master * audio.bank.Ambience * audio.underwaterVolume * audio.underwaterBlend;
            if (audio.underwaterBlend > .005f && !audio.underwater.isPlaying && audio.underwater.clip != null) audio.underwater.Play();
            else if (audio.underwaterBlend <= .005f) audio.underwater.Stop();
            if (onShip && !audio.deck.isPlaying && audio.deck.clip != null) audio.deck.Play();
            else if (!onShip) audio.deck.Stop();
            audio.lastAmbience = Time.unscaledTime;
        }
        float lastAmbience;
        void UpdateEnvironment(bool onShip)
        {
            if (Time.unscaledTime >= environmentAt)
            {
                environmentAt = Time.unscaledTime + .25f;
                if (listener == null || !listener.isActiveAndEnabled)
                    foreach (var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                        if (candidate.isActiveAndEnabled) { listener = candidate; break; }
                cabinTarget = underwaterTarget = 0f;
                if (listener != null)
                {
                    Vector3 eye = listener.transform.position;
                    var sea = OceanSurface.Instance;
                    underwaterTarget = sea != null && eye.y < sea.Height(eye) - .1f ? 1f : 0f;
                    if (onShip)
                        foreach (var hit in Physics.RaycastAll(eye, Vector3.up, 4f, ~0, QueryTriggerInteraction.Ignore))
                            if (hit.collider.GetComponentInParent<ShipController>() != null && hit.collider.GetComponentInParent<AdvancedPlayerController>() == null)
                            { cabinTarget = 1f; break; }
                }
            }
            float blend = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            cabinBlend = Mathf.Lerp(cabinBlend, cabinTarget, blend);
            underwaterBlend = Mathf.Lerp(underwaterBlend, underwaterTarget, blend);
        }
        void Update()
        {
            if (Time.unscaledTime - lastAmbience < .3f) return;
            ocean.Stop(); wind.Stop(); deck.Stop(); underwater.Stop(); flooding.Stop();
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
