using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    [DisallowMultipleComponent]
    public sealed class MastFloodGauge : MonoBehaviour
    {
        static readonly int FloodLevels = Shader.PropertyToID("_FloodLevels");
        static GameObject prefab;
        public AudioClip WaterLoop, BloodLoop;
        AudioSource voice;
        float previousWater, waterSoundUntil;
        float gain;
        MeshRenderer face;
        MaterialPropertyBlock properties;
        NetworkShip ship;
        ShipFlooding flooding;
        float displayedWater = -1f;
        Vector2 published = new(-1f, -1f);

        internal static MastFloodGauge Mount(NetworkShip ship, ShipFlooding flooding, Transform stem)
        {
            if (prefab == null) prefab = Resources.Load<GameObject>("Ships/MastFloodGauge");
            if (prefab == null)
            {
                Debug.LogError("Missing Ships/MastFloodGauge prefab.", ship);
                return null;
            }
            var instance = Instantiate(prefab, ship.transform, false);
            instance.transform.SetParent(stem, true);
            var gauge = instance.GetComponent<MastFloodGauge>();
            gauge.previousWater = flooding.Level;
            gauge.ship = ship;
            gauge.flooding = flooding;
            gauge.face = instance.GetComponent<MeshRenderer>();
            gauge.properties = new MaterialPropertyBlock();
            return gauge;
        }

        void UpdateSound(float water, float red)
        {
            if (water > previousWater) waterSoundUntil = Time.time + 1.2f;
            if (water < previousWater) waterSoundUntil = 0f;
            previousWater = water;
            AudioClip wanted = null;
            if (!ship.IsSinking)
            {
                if (red > 0f) wanted = BloodLoop;
                else if (Time.time < waterSoundUntil && water < 1f) wanted = WaterLoop;
            }
            if (voice == null)
            {
                if (wanted == null) return;
                var emitter = new GameObject("Mast Gauge Audio");
                emitter.transform.SetParent(transform, false);
                emitter.transform.localPosition = new Vector3(0f, .12f, -.33f);
                voice = emitter.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = true;
                voice.spatialBlend = 1f;
                voice.dopplerLevel = 0f;
                voice.minDistance = .35f;
                voice.maxDistance = 2f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.volume = 0f;
            }
            if (!voice.isPlaying && wanted == null) return;
            if (!voice.isPlaying && wanted != null)
            {
                voice.clip = wanted;
                voice.Play();
            }
            float target = wanted != null && voice.clip == wanted ? (red > 0f ? Mathf.Lerp(.48f, .85f, red) : .42f) : 0f;
            gain = Mathf.MoveTowards(gain, target, Time.deltaTime * 2.5f);
            voice.volume = gain * .4f * GameAudio.EffectGain;
            if (gain == 0f && wanted != voice.clip) voice.Stop();
        }

        void OnBecameInvisible() => displayedWater = -1f;

        void LateUpdate()
        {
            if (ship == null) return;
            float water = ship.IsSinking ? 1f : flooding.Level;
            float red = ship.FloodDeathProgress;
            UpdateSound(water, red);
            if (!face.isVisible) return;
            if (water == displayedWater && red == published.y) return;
            if (displayedWater < 0f || ship.IsSinking || water >= 1f)
                displayedWater = water;
            else
                displayedWater = Mathf.Lerp(displayedWater, water, 1f - Mathf.Exp(-16f * Time.deltaTime));
            if (Mathf.Abs(displayedWater - water) < .0001f) displayedWater = water;
            var levels = new Vector2(displayedWater, red);
            if (levels.Equals(published)) return;
            published = levels;
            properties.SetVector(FloodLevels, new Vector4(levels.x, levels.y, 0f, 0f));
            face.SetPropertyBlock(properties);
        }
    }
}
