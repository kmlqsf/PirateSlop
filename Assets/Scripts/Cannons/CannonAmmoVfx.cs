using UnityEngine;

namespace PirateSlop
{
    public sealed class CannonAmmoVfx : MonoBehaviour
    {
        Material material;
        public static CannonAmmoVfx Create(Transform parent, Vector3 position, bool frozen)
        {
            var root = new GameObject(frozen ? "FrozenShip" : "CannonFire");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var effect = root.AddComponent<CannonAmmoVfx>();
            effect.Initialize(frozen);
            return effect;
        }

        void Initialize(bool frozen)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            var particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true; main.startLifetime = frozen ? 1.8f : .9f;
            main.startSpeed = frozen ? .3f : 1.5f;
            main.startSize = frozen ? .35f : 1.15f;
            main.maxParticles = frozen ? 700 : 180;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = frozen ? new Color(.4f, .85f, 1f, .8f) : new Color(1f, .45f, .04f, .9f);
            var emission = particles.emission; emission.rateOverTime = frozen ? 250f : 140f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = frozen ? new Vector3(14f, 8f, 46f) : new Vector3(4f, .1f, 4f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = frozen ? .35f : 2f;
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(frozen
                ? new[] { new GradientColorKey(new Color(.6f, .95f, 1f), 0f), new GradientColorKey(new Color(.15f, .45f, 1f), 1f) }
                : new[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(new Color(1f, .15f, .01f), .5f), new GradientColorKey(Color.gray, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.9f, .15f), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;
            var size = particles.sizeOverLifetime;
            size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            if (!frozen) { particles.Play(); return; }
            var light = gameObject.AddComponent<Light>();
            light.color = frozen ? new Color(.25f, .65f, 1f) : new Color(1f, .35f, .05f);
            light.range = frozen ? 28f : 14f; light.intensity = frozen ? 3f : 4f;
            particles.Play();
        }

        void OnDestroy() { if (material != null) Destroy(material); }
    }
}