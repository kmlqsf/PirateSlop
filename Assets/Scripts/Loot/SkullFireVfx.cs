using UnityEngine;

namespace PirateSlop
{
    public sealed class SkullFireVfx : MonoBehaviour
    {
        public ParticleSystem[] Systems;
        public Light[] Lights;
        public Renderer[] FlameRenderers;
        public float FadeSeconds = .55f;
        bool burning, initialized;
        float fade;
        MaterialPropertyBlock properties;

        public void SetBurning(bool value, bool immediate = false)
        {
            if (initialized && burning == value && !immediate) return;
            initialized = true;
            burning = value;
            if (value)
            {
                fade = 1f;
                foreach (var system in Systems) if (system != null) system.Play(false);
            }
            else
            {
                foreach (var system in Systems)
                    if (system != null) system.Stop(false, immediate ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
                if (immediate) fade = 0f;
            }
            ApplyFade();
        }

        void Update()
        {
            if (!burning && fade > 0f)
            {
                fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime / Mathf.Max(.01f, FadeSeconds));
                if (fade == 0f) foreach (var system in Systems) if (system != null) system.Clear(false);
            }
            ApplyFade();
        }

        void ApplyFade()
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetFloat("_Fade", fade);
            foreach (var renderer in FlameRenderers) if (renderer != null) renderer.SetPropertyBlock(properties);
            var camera = Camera.main;
            float lightDistance = 100f * transform.lossyScale.x;
            bool nearby = camera != null && (camera.transform.position - transform.position).sqrMagnitude < lightDistance * lightDistance;
            for (int i = 0; i < Lights.Length; i++)
            {
                var light = Lights[i];
                if (light == null) continue;
                light.enabled = nearby && fade > 0f;
                light.intensity = fade * Mathf.Lerp(3f, 5f, Mathf.PerlinNoise(i * 17.3f + transform.position.x, Time.time * 8f));
            }
        }
    }
}
