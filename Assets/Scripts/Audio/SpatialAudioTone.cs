using UnityEngine;

namespace PirateSlop
{
    [RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
    public sealed class SpatialAudioTone : MonoBehaviour
    {
        public bool Environment;
        AudioSource source;
        AudioLowPassFilter filter;
        AudioListener listener;
        float sampleAt, target = 22000f;

        static AudioListener cachedListener;
        static float listenerCheckTime;
        static readonly RaycastHit[] hitBuffer = new RaycastHit[32];

        public static AudioListener FindListener()
        {
            if (cachedListener != null && cachedListener.isActiveAndEnabled) return cachedListener;
            if (Time.unscaledTime < listenerCheckTime) return cachedListener;
            listenerCheckTime = Time.unscaledTime + 1f;
            cachedListener = Object.FindAnyObjectByType<AudioListener>();
            return cachedListener;
        }

        void Awake() { source = GetComponent<AudioSource>(); filter = GetComponent<AudioLowPassFilter>(); }
        void Update()
        {
            if (!source.isPlaying) return;
            if (source.spatialBlend <= 0f && !Environment)
            {
                if (filter.enabled) filter.enabled = false;
                return;
            }
            if (!filter.enabled) filter.enabled = true;

            if (Time.unscaledTime >= sampleAt)
            {
                sampleAt = Time.unscaledTime + .18f;
                listener = FindListener();
                target = 22000f;
                if (listener != null && (source.spatialBlend > .5f || Environment))
                {
                    Vector3 eye = listener.transform.position;
                    var ocean = OceanSurface.Instance;
                    bool submerged = ocean != null && eye.y < ocean.Height(eye) - .1f;
                    Vector3 delta = eye - transform.position;
                    float distance = delta.magnitude;
                    bool blocked = false;
                    if (!Environment && distance > 2f)
                    {
                        int hitCount = Physics.RaycastNonAlloc(transform.position + delta.normalized * .3f, delta.normalized, hitBuffer, distance - .6f, ~0, QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < hitCount; i++)
                        {
                            var col = hitBuffer[i].collider;
                            if (col != null && col.GetComponentInParent<AdvancedPlayerController>() == null && col.GetComponentInParent<Networking.NetworkFish>() == null) { blocked = true; break; }
                        }
                    }
                    if (Environment)
                    {
                        int hitCount = Physics.RaycastNonAlloc(eye, Vector3.up, hitBuffer, 4f, ~0, QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < hitCount; i++)
                        {
                            var col = hitBuffer[i].collider;
                            if (col != null && col.GetComponentInParent<ShipController>() != null) { blocked = true; break; }
                        }
                    }
                    float far = Environment ? 0f : Mathf.Clamp01((distance - source.minDistance) / Mathf.Max(1f, source.maxDistance - source.minDistance));
                    target = submerged ? 900f : blocked ? 2800f : Mathf.Lerp(22000f, 3500f, far);
                }
            }
            filter.cutoffFrequency = Mathf.Lerp(filter.cutoffFrequency, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }
    }
}
