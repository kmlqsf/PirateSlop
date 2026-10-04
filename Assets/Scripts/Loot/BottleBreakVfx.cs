using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public static class BottleBreakVfx
    {
        static readonly ParticleSystem[] bursts = new ParticleSystem[16];
        static int next;
        public static void Present(Vector3 point, bool water)
        {
            if (!Application.isPlaying || Application.isBatchMode) return;
            GameAudio.Play(SoundCue.BottleBreak, point);
            if (water) { GameAudio.Play(SoundCue.WaterSplash, point, .6f); CombatVfx.Splash(point, .5f); }
            var material = Resources.Load<Material>("BottleGlassShard");
            if (material == null) return;
            int index = next++ % bursts.Length;
            var particles = bursts[index];
            if (particles == null) particles = bursts[index] = new GameObject("BottleGlassShards").AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.gameObject.SetActive(true);
            particles.transform.SetPositionAndRotation(point, Quaternion.LookRotation(Vector3.up));
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.duration = .1f; main.maxParticles = 16;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.006f, .026f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.38f, .65f, .8f, .7f), new Color(.9f, .96f, 1f, .9f));
            main.gravityModifier = 1; main.stopAction = ParticleSystemStopAction.Disable;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 90; shape.radius = .04f;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)Random.Range(10, 17)) });
            var spin = particles.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-15, 15);
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .4f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play();
        }
    }
}
