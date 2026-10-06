using UnityEngine;

namespace PirateSlop
{
    public sealed class DistanceShotAudio : MonoBehaviour
    {
        AudioSource near, distant;
        AudioLowPassFilter filter;
        float volume, distantVolume, startDistance, endDistance;
        bool synthetic;
        public void Configure(AudioSource source, GameAudioBank.Entry entry, int variant)
        {
            near = source; volume = source.volume;
            startDistance = entry.DistantStart; endDistance = Mathf.Max(startDistance + 1f, entry.DistantEnd);
            distantVolume = entry.DistantVolume;
            synthetic = entry.DistantClips == null || entry.DistantClips.Length == 0;
            if (distant == null)
            {
                var child = new GameObject("Distant gunshot"); child.transform.SetParent(transform, false);
                distant = child.AddComponent<AudioSource>(); distant.playOnAwake = false;
                child.AddComponent<SpatialAudioTone>();
                filter = child.GetComponent<AudioLowPassFilter>();
            }
            distant.Stop(); distant.clip = synthetic ? near.clip : entry.DistantClips[entry.DistantClips.Length == entry.Clips.Length ? variant : Random.Range(0, entry.DistantClips.Length)];
            distant.spatialBlend = near.spatialBlend; distant.rolloffMode = near.rolloffMode;
            distant.minDistance = near.minDistance; distant.maxDistance = near.maxDistance;
            distant.pitch = near.pitch; distant.priority = near.priority; distant.dopplerLevel = 0f;
            filter.cutoffFrequency = synthetic ? 1800f : 6000f;
            distant.volume = 0f; enabled = true;
            UpdateBlend(); distant.Play();
        }
        void UpdateBlend()
        {
            var listener = SpatialAudioTone.FindListener();
            float distance = listener != null ? Vector3.Distance(listener.transform.position, transform.position) : 0f;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(startDistance, endDistance, distance));
            near.volume = volume * (1f - blend);
            distant.volume = volume * distantVolume * blend;
            filter.cutoffFrequency = Mathf.Min(filter.cutoffFrequency, synthetic ? Mathf.Lerp(3500f, 1400f, blend) : Mathf.Lerp(10000f, 3500f, blend));
        }
        void LateUpdate()
        {
            if (near == null || !near.isPlaying && (distant == null || !distant.isPlaying)) { enabled = false; return; }
            UpdateBlend();
        }
        public void ResetPlayback() { if (distant != null) distant.Stop(); enabled = false; }
        void OnDisable() { if (distant != null) distant.Stop(); }
    }
}
