using UnityEngine;

namespace PirateSlop
{
    public sealed class StaffVfxEffect : MonoBehaviour
    {
        public Transform Follow, Body;
        public Transform SurfaceAnchor;
        public Vector3 SurfaceLocalPoint;
        public bool Flight, Lightning;
        public float Duration = 1.2f;
        public Light GlowLight;
        public LineRenderer Ring;
        public LineRenderer[] Bolts;
        float started, lightIntensity;
        bool detached;
        void Start() { started = Time.time; lightIntensity = GlowLight != null ? GlowLight.intensity : 0f; }
        void Update()
        {
            if (Flight && !detached)
            {
                if (Follow != null && Follow.gameObject.activeInHierarchy)
                {
                    if (Body != null) Body.Rotate(new Vector3(38f, 95f, 27f) * Time.deltaTime, Space.Self);
                    if (GlowLight != null) GlowLight.intensity = lightIntensity * (.85f + Mathf.Sin(Time.time * 19f) * .15f);
                    return;
                }
                Release();
            }
            Evaluate(Time.time - started);
            if (Time.time - started >= Duration) Destroy(gameObject);
        }
        public void Evaluate(float elapsed)
        {
            if (lightIntensity <= 0f && GlowLight != null) lightIntensity = GlowLight.intensity;
            float t = Mathf.Clamp01(elapsed / Duration);
            if (GlowLight != null) GlowLight.intensity = lightIntensity * Mathf.Pow(1f - t, 3f);
            if (Ring != null)
            {
                float wave = Mathf.Clamp01(elapsed / .48f);
                Ring.transform.localScale = Vector3.one * Mathf.Lerp(.05f, 1f, 1f - Mathf.Pow(1f - wave, 2f));
                Color color = Ring.startColor; color.a = Mathf.Pow(1f - wave, 1.4f); Ring.startColor = Ring.endColor = color;
            }
            if (Bolts != null) foreach (var bolt in Bolts) if (bolt != null)
            {
                float flash = elapsed / .65f;
                float intensity = flash < .12f ? 1f : flash > .25f && flash < .34f ? .8f : flash > .48f && flash < .62f ? .6f : Mathf.Exp(-flash * 7f) * .3f;
                bolt.enabled = flash < 1f;
                Color color = bolt.startColor; color.a = intensity * (bolt.gameObject.name == "LightningAura" ? .4f : 1f); bolt.startColor = bolt.endColor = color;
            }
        }
        void LateUpdate()
        {
            if (SurfaceAnchor != null) transform.SetPositionAndRotation(SurfaceAnchor.TransformPoint(SurfaceLocalPoint), Quaternion.identity);
        }
        public void Release()
        {
            if (detached) return;
            if (Body != null) Body.gameObject.SetActive(false);
            if (Flight) foreach (var renderer in GetComponentsInChildren<MeshRenderer>()) renderer.enabled = false;
            detached = true; Follow = null; transform.SetParent(null, true); started = Time.time;
            foreach (var particles in GetComponentsInChildren<ParticleSystem>()) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var trail = GetComponent<TrailRenderer>(); if (trail != null) trail.emitting = false;
            if (GlowLight != null) GlowLight.enabled = false;
            Destroy(gameObject, Duration);
        }
    }
}
