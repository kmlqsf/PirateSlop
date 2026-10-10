using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(1100)]
    public sealed class UnderwaterEnvironment : MonoBehaviour
    {
        ParticleSystem suspension;
        Material material;
        System.Random random;
        float remainder;
        bool submerged;
        Vector3 lastCameraPosition;
        readonly ParticleSystem.Particle[] prefilled = new ParticleSystem.Particle[240];

        void OnEnable()
        {
            random = new System.Random(73126);
            var shader = Resources.Load<Shader>("EnvironmentTest/UnderwaterSuspension");
            if (shader == null) return;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var volume = new GameObject("Underwater suspension");
            volume.transform.SetParent(transform, false);
            suspension = volume.AddComponent<ParticleSystem>();
            suspension.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = suspension.main;
            main.loop = true;
            main.maxParticles = 240;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0;
            main.startLifetime = 18;
            var emission = suspension.emission;
            emission.enabled = false;
            var shape = suspension.shape;
            shape.enabled = false;
            var lifetime = suspension.colorOverLifetime;
            lifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            lifetime.color = gradient;
            var renderer = suspension.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            suspension.Play();
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            var surface = OceanSurface.Instance;
            if (suspension == null || camera == null || surface == null) return;
            var position = camera.transform.position;
            float height = Ships.ShipWaterInterior.TryWaterHeight(position, out float bilgeHeight) ? bilgeHeight : surface.Height(position);
            bool inside = height > position.y;
            if (inside && (!submerged || (position - lastCameraPosition).sqrMagnitude > 225))
            {
                suspension.Clear();
                for (int i = 0; i < 200; i++) Emit(position, 0f);
                int prefilledCount = suspension.GetParticles(prefilled);
                for (int i = 0; i < prefilledCount; i++) prefilled[i].remainingLifetime = 4f + (float)random.NextDouble() * 14f;
                suspension.SetParticles(prefilled, prefilledCount);
            }
            lastCameraPosition = position;
            submerged = inside;
            if (!inside) { remainder = 0; return; }
            remainder += Time.deltaTime * 16;
            int count = Mathf.Min(8, Mathf.FloorToInt(remainder));
            remainder -= count;
            for (int i = 0; i < count; i++) Emit(position, 0);
        }

        void Emit(Vector3 camera, float age)
        {
            var offset = new Vector3((float)random.NextDouble() * 24 - 12, (float)random.NextDouble() * 16 - 8, (float)random.NextDouble() * 24 - 12);
            var particle = new ParticleSystem.EmitParams
            {
                position = camera + offset,
                velocity = new Vector3((float)random.NextDouble() * .06f - .03f, .03f + (float)random.NextDouble() * .07f, .025f),
                startLifetime = 18 - age,
                startSize = .020f + (float)random.NextDouble() * .040f,
                startColor = new Color(.60f, .80f, .77f, .08f + (float)random.NextDouble() * .10f)
            };
            suspension.Emit(particle, 1);
        }

        void OnDisable()
        {
            if (suspension != null) Destroy(suspension.gameObject);
            if (material != null) Destroy(material);
            suspension = null;
            material = null;
        }
    }
}
