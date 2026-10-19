#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Experimental
{
    public sealed class UpperImpactPresentation : MonoBehaviour
    {
        ParticleSystem dust, sparks;
        readonly float[] travel = new float[8];
        readonly Vector3[] directions = new Vector3[8];

        public void Initialize(Material dustMaterial, Material sparkMaterial)
        {
            dust = Create("UpperImpactDust", dustMaterial, false);
            sparks = Create("UpperImpactSparks", sparkMaterial, true);
        }

        ParticleSystem Create(string label, Material material, bool streak)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = 512;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.8f, 0f), new GradientAlphaKey(.65f, .6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (streak) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 1.5f; renderer.velocityScale = .04f; }
            system.Play();
            return system;
        }

        public void Emit(UpperImpactShape shape, uint seed)
        {
            var forward = shape.Radial ? shape.Normal : shape.Direction;
            var rotation = Quaternion.FromToRotation(Vector3.forward, forward);
            float range = shape.Radial ? UpperImpactShape.Radius : UpperImpactShape.Length;
            for (int i = 0; i < directions.Length; i++)
            {
                float angle = (i * 137.50776f + seed % 360) * Mathf.Deg2Rad;
                float cosine = Mathf.Lerp(1f, shape.Radial ? .05f : .819152f, (i + .5f) / directions.Length);
                float sine = Mathf.Sqrt(1f - cosine * cosine);
                var direction = rotation * new Vector3(Mathf.Cos(angle) * sine, Mathf.Sin(angle) * sine, cosine);
                directions[i] = direction;
                travel[i] = UpperCannonImpact.ClearDistance(shape.Origin, shape.Origin + direction * range);
            }
            EmitParticles(dust, shape, seed, 24, 7f, new Color(.55f, .43f, .29f, .7f), .12f);
            if (shape.Metal) EmitParticles(sparks, shape, seed + 71, 16, 11f, new Color(1f, .65f, .2f, 1f), .035f);
        }

        void EmitParticles(ParticleSystem system, UpperImpactShape shape, uint seed, int count, float speed, Color color, float size)
        {
            if (system == null) return;
            for (int i = 0; i < count; i++)
            {
                int branch = i % directions.Length;
                bool blocked = travel[branch] < .05f;
                var particle = new ParticleSystem.EmitParams
                {
                    position = shape.Origin, velocity = (blocked ? shape.Normal : directions[branch]) * (blocked ? .4f : speed),
                    startLifetime = blocked ? .25f : travel[branch] / speed, startSize = size * (1f + i % 3 * .3f),
                    startColor = color, randomSeed = seed + (uint)i + 1
                };
                system.Emit(particle, 1);
            }
        }

        void OnDestroy()
        {
            if (dust != null) Destroy(dust.gameObject);
            if (sparks != null) Destroy(sparks.gameObject);
        }
    }
}
#endif
