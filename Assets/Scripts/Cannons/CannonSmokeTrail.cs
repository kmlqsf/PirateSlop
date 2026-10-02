using UnityEngine;

namespace PirateSlop
{
    public sealed class CannonSmokeTrail : MonoBehaviour
    {
        public const float Lifetime = 3.3f;
        ParticleSystem particles;
        float untilNext;
        float scale, spacing;
        int segmentLimit;

        public static CannonSmokeTrail Create(Vector3 position, float sizeScale = 1f)
        {
            if (Application.isBatchMode || !Application.isPlaying) return null;
            var material = Resources.Load<Material>("CombatParticles");
            if (material == null) return null;
            var root = new GameObject(sizeScale < 1f ? "FirearmSmokeTrail" : "CannonSmokeTrail");
            root.transform.position = position;
            var trail = root.AddComponent<CannonSmokeTrail>();
            trail.scale = Mathf.Clamp(sizeScale, .1f, 1f);
            trail.spacing = sizeScale < 1f ? .12f : .35f;
            trail.segmentLimit = sizeScale < 1f ? 2048 : 48;
            trail.Initialize(material);
            trail.Emit(position);
            trail.untilNext = trail.spacing;
            return trail;
        }

        void Initialize(Material material)
        {
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.maxParticles = scale < 1f ? 4096 : 1024;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.85f, .25f), new GradientAlphaKey(.4f, .65f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var growth = particles.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.5f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        void Emit(Vector3 position)
        {
            var puff = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = new Vector3(Random.Range(-.04f, .04f), Random.Range(.08f, .14f), Random.Range(-.04f, .04f)) * scale,
                startSize = Random.Range(.45f, .55f) * scale,
                startLifetime = Lifetime,
                startColor = new Color(.60f, .59f, .55f, .4f),
                rotation = Random.Range(0f, 360f)
            };
            particles.Emit(puff, 1);
        }

        public void Segment(Vector3 from, Vector3 to)
        {
            float length = Vector3.Distance(from, to);
            if (length < .0001f) return;
            Vector3 direction = (to - from) / length;
            if (length / spacing > segmentLimit)
            {
                for (int i = 1; i <= segmentLimit; i++) Emit(Vector3.Lerp(from, to, i / (float)segmentLimit));
                untilNext = spacing;
                return;
            }
            float sample = untilNext;
            for (; sample <= length; sample += spacing) Emit(from + direction * sample);
            untilNext = sample - length;
        }

        public void Finish()
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, Lifetime + .1f);
        }
    }
}
