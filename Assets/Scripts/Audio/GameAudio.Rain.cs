using UnityEngine;

namespace PirateSlop
{
    public sealed partial class GameAudio
    {
        AudioSource rainAudio, rainDeckAudio;
        AudioLowPassFilter rainFilter, rainDeckFilter;
        float rainBlend, rainShelterBlend, rainDeckBlend, lastRain = -100;
        bool rainOnShip;
        AudioSource[] testThunder;
        int nextTestThunder;

        public static void PrepareTestStormThunder()
        {
            var audio = Get();
            if (audio == null || audio.testThunder != null) return;
            audio.testThunder = new AudioSource[4];
            for (int i = 0; i < audio.testThunder.Length; i++)
            {
                var source = audio.Source("Test storm thunder " + i, false);
                source.spatialBlend = 1;
                source.minDistance = 24; source.maxDistance = 240;
                source.priority = 35;
                audio.testThunder[i] = source;
            }
            if (audio.bank.TestStormThunder != null)
                foreach (var clip in audio.bank.TestStormThunder) if (clip != null) clip.LoadAudioData();
        }

        public static void TestStormThunder(Vector3 position, Vector3 eye)
        {
            var audio = instance;
            float distance = Vector3.Distance(position, eye);
            if (audio == null || audio.testThunder == null || distance >= 240 ||
                audio.bank.TestStormThunder == null || audio.bank.TestStormThunder.Length == 0) return;
            int count = audio.bank.TestStormThunder.Length;
            int index = Random.Range(0, count - (count > 1 ? 1 : 0));
            if (count > 1 && audio.lastStormThunder >= 0 && index >= audio.lastStormThunder) index++;
            var clip = audio.bank.TestStormThunder[index];
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded) return;
            audio.lastStormThunder = index;
            var source = audio.testThunder[audio.nextTestThunder++ % audio.testThunder.Length];
            source.Stop(); source.transform.position = position;
            source.volume = Mathf.Clamp01(audio.bank.ThunderLevel * 2.1f * audio.bank.Master * audio.bank.Effects);
            source.pitch = Random.Range(.93f, 1.02f);
            source.clip = clip; source.Play();
        }

        public static void PrepareStormRain()
        {
            var audio = Get();
            if (audio == null || audio.rainAudio != null || audio.bank.Rain == null) return;
            audio.rainAudio = audio.RainSource("Storm downpour", audio.bank.Rain, out audio.rainFilter);
            if (audio.bank.RainDeck != null)
                audio.rainDeckAudio = audio.RainSource("Rain on ship", audio.bank.RainDeck, out audio.rainDeckFilter);
        }

        AudioSource RainSource(string name, AudioClip clip, out AudioLowPassFilter filter)
        {
            var source = Source(name, true);
            source.GetComponent<SpatialAudioTone>().enabled = false;
            source.spatialBlend = 0;
            source.volume = 0;
            source.priority = 110;
            source.clip = clip;
            filter = source.GetComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 16000;
            clip.LoadAudioData();
            return source;
        }

        public static void RainAmbience(float intensity, bool exposed)
        {
            var audio = instance;
            if (audio == null || audio.rainAudio == null) return;
            audio.lastRain = Time.unscaledTime;
            float blend = 1-Mathf.Exp(-4*Time.deltaTime);
            audio.rainBlend = Mathf.Lerp(audio.rainBlend, intensity, blend);
            audio.rainShelterBlend = Mathf.Lerp(audio.rainShelterBlend, exposed ? 0 : 1, blend);
            audio.rainDeckBlend = Mathf.Lerp(audio.rainDeckBlend, audio.rainOnShip ? 1 : 0, blend);
            float volume = audio.bank.Master*audio.bank.Ambience*audio.bank.RainLevel*audio.rainBlend;
            audio.rainAudio.volume = Mathf.Clamp01(volume*1.6f*Mathf.Lerp(1,.38f,audio.rainShelterBlend));
            audio.rainFilter.cutoffFrequency = Mathf.Lerp(16000,2200,audio.rainShelterBlend);
            if (!audio.rainAudio.isPlaying && audio.rainAudio.clip.loadState == AudioDataLoadState.Loaded)
                audio.rainAudio.Play();
            if (audio.rainDeckAudio != null)
            {
                audio.rainDeckAudio.volume = Mathf.Clamp01(volume*.8f*audio.rainDeckBlend*Mathf.Lerp(.6f,1,audio.cabinBlend));
                audio.rainDeckFilter.cutoffFrequency = Mathf.Lerp(12000,3200,audio.cabinBlend);
                if (!audio.rainDeckAudio.isPlaying && audio.rainDeckAudio.clip.loadState == AudioDataLoadState.Loaded)
                    audio.rainDeckAudio.Play();
            }
        }

        void UpdateRainLifetime()
        {
            var weather = StormVolumeController.Instance;
            if (testThunder != null && (weather == null || !weather.Ready || !weather.TestCloudWall))
                foreach (var source in testThunder) if (source.isPlaying) source.Stop();
            if (Time.unscaledTime-lastRain < .35f) return;
            if (rainAudio != null) rainAudio.Stop();
            if (rainDeckAudio != null) rainDeckAudio.Stop();
            rainBlend = 0;
        }
    }
}
