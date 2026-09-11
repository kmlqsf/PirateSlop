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
        AudioLowPassFilter[] shotFilters;
        AudioListener listener;
        int nextShot;
        int nextFeedback;
        AudioSource ocean, wind, deck;
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
            instance.shotFilters = new AudioLowPassFilter[12];
            for (int i = 0; i < instance.shotVoices.Length; i++)
            {
                instance.shotVoices[i] = instance.Source("Gunshot " + i, false);
                instance.shotFilters[i] = instance.shotVoices[i].gameObject.AddComponent<AudioLowPassFilter>();
            }
            for (int i = 0; i < instance.feedbackVoices.Length; i++) instance.feedbackVoices[i] = instance.Source("Damage feedback " + i, false);
            instance.ocean = instance.Source("Ocean", true); instance.ocean.clip = bank.Ocean;
            instance.wind = instance.Source("Wind", true); instance.wind.clip = bank.Wind;
            instance.deck = instance.Source("Deck creaks", true); instance.deck.clip = bank.DeckCreaks;
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
            bool feedback = cue == SoundCue.Hurt || cue == SoundCue.Death || cue == SoundCue.HitConfirm;
            bool gunshot = firearm!=null || cue == SoundCue.Pistol || cue == SoundCue.Musket || cue == SoundCue.DoubleBarrel;
            var source = gunshot ? audio.shotVoices[audio.nextShot] : feedback ? audio.feedbackVoices[audio.nextFeedback] : audio.voices[audio.nextVoice];
            bool blocked=false;
            if(gunshot)
            {
                if(audio.listener==null || !audio.listener.isActiveAndEnabled)
                    foreach(var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                        if(candidate.isActiveAndEnabled) { audio.listener=candidate;break; }
                if(audio.listener!=null)
                {
                    Vector3 delta=audio.listener.transform.position-position;
                    blocked=delta.sqrMagnitude>36 && Physics.Raycast(position+delta.normalized*.5f,delta.normalized,delta.magnitude-.8f,~0,QueryTriggerInteraction.Ignore);
                }
                audio.shotFilters[audio.nextShot].cutoffFrequency=blocked?3200:22000;
            }
            if (gunshot) audio.nextShot = (audio.nextShot + 1) % audio.shotVoices.Length;
            else if (feedback) audio.nextFeedback = (audio.nextFeedback + 1) % audio.feedbackVoices.Length;
            else audio.nextVoice = (audio.nextVoice + 1) % audio.voices.Length;
            source.Stop(); source.transform.position = position;
            source.spatialBlend = ui ? 0f : 1f;
            source.minDistance = cue == SoundCue.Cannon ? 8f : gunshot ? 5f : 2f; source.maxDistance = entry.Distance;
            source.priority = gunshot ? 40 : feedback ? 32 : 128;
            source.volume = Mathf.Clamp01(entry.Volume * scale * audio.bank.Master * (ui && !feedback ? audio.bank.Interface : audio.bank.Effects));
            if(blocked) source.volume*=.65f;
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
            }
            source.Stop(); source.minDistance = 2f; source.maxDistance = entry.Distance;
            source.volume = entry.Volume * audio.bank.Master * audio.bank.Effects;
            source.clip = entry.Clips[Random.Range(0, entry.Clips.Length)]; source.Play();
        }
        public static void Ambience(float speed, bool onShip = false)
        {
            var audio = Get(); if (audio == null) return;
            audio.ocean.volume = audio.bank.Master * audio.bank.Ambience * Mathf.Lerp(.45f, .75f, speed);
            audio.wind.volume = audio.bank.Master * audio.bank.Ambience * Mathf.Lerp(.15f, .4f, speed);
            if (!audio.ocean.isPlaying && audio.ocean.clip != null) audio.ocean.Play();
            if (!audio.wind.isPlaying && audio.wind.clip != null) audio.wind.Play();
            audio.deck.volume = audio.bank.Master * audio.bank.Ambience * Mathf.Lerp(.2f, .4f, speed);
            if (onShip && !audio.deck.isPlaying && audio.deck.clip != null) audio.deck.Play();
            else if (!onShip) audio.deck.Stop();
            audio.lastAmbience = Time.unscaledTime;
        }
        float lastAmbience;
        void Update()
        {
            if (Time.unscaledTime - lastAmbience < .3f) return;
            ocean.Stop(); wind.Stop(); deck.Stop();
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
