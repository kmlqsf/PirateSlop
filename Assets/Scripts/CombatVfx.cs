using UnityEngine;

namespace PirateSlop
{
    public static class CombatVfx
    {
        static Material material;
        static Mesh chipMesh;
        static Material chipMaterial;
        static bool Available => Application.isPlaying && !Application.isBatchMode;
        static void Burst(Vector3 position, Vector3 direction, Color color, int count, float size, float speed, float life, float gravity = 0)
        {
            if (!Available) return;
            var rot = Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : Vector3.up);
            var go = VfxPool.Instance != null ? VfxPool.Instance.Get("CombatParticles", position, rot) : null;
            if (go == null) return;
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.duration = .1f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(life * .65f, life);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
                main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
                main.startColor = color; main.gravityModifier = gravity;
                main.maxParticles = count; main.stopAction = ParticleSystemStopAction.None;
                var emission = ps.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
                var shape = ps.shape; shape.radius = size * .15f;
                var noise = ps.noise; noise.strength = size * .25f;
                ps.Play();
            }
            if (VfxPool.Instance != null)
                VfxPool.Instance.Return("CombatParticles", go, life + 1f);
            else
                Object.Destroy(go, life + 1f);
        }

        public static void Fire(Vector3 position, Vector3 direction, bool cannon)
        {
            if (cannon && Available) SeaMistRendererFeature.CannonFlash(position);
            FirstPersonFeedback.Kick(position, -direction, cannon ? .055f : .012f);
            float scale = cannon ? 3f : 1f;

            if (Available && VfxPool.Instance != null)
            {
                var flashPos = position + direction * (cannon ? 0.8f : 0.2f);
                var flashRot = Quaternion.LookRotation(direction);
                var flashGo = VfxPool.Instance.Get("MuzzleFlash", flashPos, flashRot);
                if (flashGo != null)
                {
                    var flashPs = flashGo.GetComponent<ParticleSystem>();
                    if (flashPs != null)
                    {
                        flashPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var fMain = flashPs.main;
                        fMain.startSize = cannon ? 7f : 1.5f;
                        flashPs.Play();
                    }
                    var light = flashGo.GetComponentInChildren<Light>();
                    if (light != null)
                    {
                        light.intensity = cannon ? 8 : 2;
                        light.range = cannon ? 15 : 3;
                    }
                    VfxPool.Instance.Return("MuzzleFlash", flashGo, 0.2f);
                }
            }

            Burst(position, direction, new Color(1, .58f, .12f, .9f), cannon ? 12 : 5, .3f * scale, 7 * scale, .09f);
            Burst(position, direction, new Color(.7f, .69f, .65f, .25f), cannon ? 28 : 12, .45f * scale, 1.4f * scale, cannon ? 4 : 2, -.025f);
            Burst(position, direction, new Color(1, .65f, .2f), cannon ? 16 : 6, .025f * scale, 9 * scale, .25f, .2f);
        }
        public static void Impact(Vector3 position, Vector3 normal, bool cannon, bool wood = false)
        {
            float scale = cannon ? 3 : 1;
            Burst(position, normal, new Color(.43f, .32f, .2f, .6f), cannon ? 24 : 8, .3f * scale, 2 * scale, 1);
            if (wood) Splinters(position, normal);
        }
        static void Splinters(Vector3 point, Vector3 normal)
        {
            if (!Available || VfxPool.Instance == null) return;
            var rot = Quaternion.LookRotation(normal.sqrMagnitude > .001f ? normal : Vector3.up);
            var root = VfxPool.Instance.Get("HullSplinters", point, rot);
            if (root == null) return;
            var particles = root.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play();
            }
            VfxPool.Instance.Return("HullSplinters", root, 1.7f);
        }
        public static void Splash(Vector3 position, float scale = 1)
        {
            if (!Available) return;
            if (!GpuWaterSpray.Spawn(position, scale))
                Burst(position, Vector3.up, new Color(.65f, .85f, .9f, .6f), 24, .18f * scale, 6 * scale, 1, 1);
            Burst(position, Vector3.up, new Color(.85f, .95f, 1, .3f), 10, .6f * scale, .8f, 1.5f);
        }
        public static void Respawn(Vector3 position, Transform platform)
        {
            if (!Available || VfxPool.Instance == null) return;
            Vector3 spawnPos = position + Vector3.up * .6f;
            var root = VfxPool.Instance.Get("CrewRespawnMist", spawnPos, Quaternion.identity);
            if (root == null) return;
            if (platform != null) root.transform.SetParent(platform, true);
            var particles = root.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play();
            }
            VfxPool.Instance.Return("CrewRespawnMist", root, 1f);
        }
        public static void FireSmoke(Vector3 position)
        {
            Burst(position, Vector3.up, new Color(.19f, .18f, .17f, .32f), 3, 1.4f, 1.5f, 3.5f, -.04f);
        }
    }
}
