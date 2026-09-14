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
        void Awake() { source = GetComponent<AudioSource>(); filter = GetComponent<AudioLowPassFilter>(); }
        void Update()
        {
            if (!source.isPlaying) return;
            if (Time.unscaledTime >= sampleAt)
            {
                sampleAt = Time.unscaledTime + .18f;
                if (listener == null || !listener.isActiveAndEnabled)
                    foreach (var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                        if (candidate.isActiveAndEnabled) { listener = candidate; break; }
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
                        foreach (var hit in Physics.RaycastAll(transform.position + delta.normalized * .3f, delta.normalized, distance - .6f, ~0, QueryTriggerInteraction.Ignore))
                            if (hit.collider.GetComponentInParent<AdvancedPlayerController>() == null && hit.collider.GetComponentInParent<Networking.NetworkFish>() == null) { blocked = true; break; }
                    if (Environment)
                        foreach (var hit in Physics.RaycastAll(eye, Vector3.up, 4f, ~0, QueryTriggerInteraction.Ignore))
                            if (hit.collider.GetComponentInParent<ShipController>() != null) { blocked = true; break; }
                    float far = Environment ? 0f : Mathf.Clamp01((distance - source.minDistance) / Mathf.Max(1f, source.maxDistance - source.minDistance));
                    target = submerged ? 900f : blocked ? 2800f : Mathf.Lerp(22000f, 3500f, far);
                }
            }
            filter.cutoffFrequency = Mathf.Lerp(filter.cutoffFrequency, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }
    }
}
