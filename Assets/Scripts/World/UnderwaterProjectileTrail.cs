using UnityEngine;

namespace PirateSlop
{
    public sealed class UnderwaterProjectileTrail : MonoBehaviour
    {
        const float Lifetime = 2.4f;
        ParticleSystem bubbles;
        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[768];
        float spacing, size, carry, nextSurfaceCheck;
        public static UnderwaterProjectileTrail Create(Vector3 point, float radius)
        {
            if (!Application.isPlaying || Application.isBatchMode) return null;
            var material = Resources.Load<Material>("Underwater/Bubbles");
            if (material == null) return null;
            var trail = new GameObject("UnderwaterProjectileTrail").AddComponent<UnderwaterProjectileTrail>();
            trail.transform.position = point;
            trail.spacing = radius < .04f ? .12f : .2f;
            trail.size = Mathf.Clamp(radius * .6f, .04f, .11f);
            trail.bubbles = trail.gameObject.AddComponent<ParticleSystem>();
            trail.bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = trail.bubbles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.maxParticles = radius < .04f ? 256 : 768;
            main.startColor = new Color(.65f, .9f, 1f, .58f);
            var emission = trail.bubbles.emission;
            emission.enabled = false;
            var shape = trail.bubbles.shape;
            shape.enabled = false;
            var sizeOverLife = trail.bubbles.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.35f));
            var colorOverLife = trail.bubbles.colorOverLifetime;
            colorOverLife.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.7f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(.6f, .65f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = fade;
            var renderer = trail.bubbles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            trail.bubbles.Play();
            return trail;
        }
        public void Segment(Vector3 start, Vector3 end)
        {
            float distance = Vector3.Distance(start, end);
            if (distance < .00001f || bubbles == null) return;
            Vector3 direction = (end - start) / distance;
            float offset = spacing - carry;
            int emitted = 0;
            for (; offset <= distance && emitted < 40; offset += spacing)
            {
                Vector3 point = start + direction * offset;
                if (!ProjectileWaterFlight.IsSubmerged(point)) continue;
                point += Random.insideUnitSphere * size * .3f;
                var particle = new ParticleSystem.EmitParams {
                    position = point, velocity = Vector3.up * Random.Range(.12f, .28f) + Random.insideUnitSphere * .035f,
                    startSize = size * Random.Range(.65f, 1.35f), startLifetime = Lifetime
                };
                bubbles.Emit(particle, 1);
                emitted++;
            }
            carry = Mathf.Repeat(carry + distance, spacing);
        }
        public void Finish()
        {
            if (bubbles != null) bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, Lifetime + .1f);
        }
        void Update()
        {
            if (bubbles == null || Time.time < nextSurfaceCheck) return;
            nextSurfaceCheck = Time.time + .1f;
            int count = bubbles.GetParticles(particles);
            for (int i = 0; i < count; i++)
                if (!ProjectileWaterFlight.IsSubmerged(particles[i].position)) particles[i].remainingLifetime = 0f;
            bubbles.SetParticles(particles, count);
        }
    }
}
