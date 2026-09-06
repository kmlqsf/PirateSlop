using UnityEngine;

namespace PirateSlop
{
    public static class CombatVfx
    {
        static Material material;
        static bool Available => Application.isPlaying && !Application.isBatchMode;
        static void Burst(Vector3 position, Vector3 direction, Color color, int count, float size, float speed, float life, float gravity = 0)
        {
            if (!Available) return;
            if (material == null) material = Resources.Load<Material>("CombatParticles");
            if (material == null) return;
            var go = new GameObject("CombatParticles");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : Vector3.up));
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = .1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .65f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
            main.startColor = color; main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count; main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 22; shape.radius = size * .15f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .06f), new GradientAlphaKey(0, 1) }); fade.color = gradient;
            var growth = ps.sizeOverLifetime; growth.enabled = true; growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .3f, 1, 1.7f));
            var noise = ps.noise; noise.enabled = true; noise.strength = size * .25f; noise.frequency = .7f; noise.scrollSpeed = .3f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play(); Object.Destroy(go, life + 1);
        }
        public static void Fire(Vector3 position, Vector3 direction, bool cannon)
        {
            float scale = cannon ? 3f : 1f;
            Burst(position, direction, new Color(1, .58f, .12f, .9f), cannon ? 12 : 5, .3f * scale, 7 * scale, .09f);
            Burst(position, direction, new Color(.7f, .69f, .65f, .25f), cannon ? 28 : 12, .45f * scale, 1.4f * scale, cannon ? 4 : 2, -.025f);
            Burst(position, direction, new Color(1, .65f, .2f), cannon ? 16 : 6, .025f * scale, 9 * scale, .25f, .2f);
            if (!Available) return;
            var go = new GameObject("MuzzleLight"); go.transform.position = position;
            var light = go.AddComponent<Light>(); light.color = new Color(1, .55f, .18f); light.intensity = cannon ? 5 : 2; light.range = cannon ? 9 : 3;
            Object.Destroy(go, .065f);
        }
        public static void Impact(Vector3 position, Vector3 normal, bool cannon)
        {
            float scale = cannon ? 3 : 1;
            Burst(position, normal, new Color(.43f, .32f, .2f, .6f), cannon ? 24 : 8, .3f * scale, 2 * scale, 1);
            Burst(position, normal, new Color(.35f, .22f, .1f), cannon ? 18 : 5, .04f * scale, 4 * scale, .6f, 1);
        }
        public static void Splash(Vector3 position, float scale = 1)
        {
            Burst(position, Vector3.up, new Color(.65f, .85f, .9f, .6f), 24, .18f * scale, 6 * scale, 1, 1);
            Burst(position, Vector3.up, new Color(.85f, .95f, 1, .3f), 10, .6f * scale, .8f, 1.5f);
        }
    }
}
