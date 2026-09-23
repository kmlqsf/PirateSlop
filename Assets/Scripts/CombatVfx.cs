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
            if (cannon && Available) SeaMistRendererFeature.CannonFlash(position);
            FirstPersonFeedback.Kick(position, -direction, cannon ? .055f : .012f);
            float scale = cannon ? 3f : 1f;
            
            // Add a plausible muzzle flash billboard
            if (Available)
            {
                if (material == null) material = Resources.Load<Material>("CombatParticles");
                if (material != null)
                {
                    var flashGo = new GameObject("MuzzleFlashEffect");
                    flashGo.transform.SetPositionAndRotation(position + direction * (cannon ? 0.8f : 0.2f), Quaternion.LookRotation(direction));
                    var flashPs = flashGo.AddComponent<ParticleSystem>();
                    flashPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var fMain = flashPs.main;
                    fMain.duration = 0.1f; fMain.startLifetime = 0.15f; fMain.startSpeed = 1f;
                    fMain.startSize = cannon ? 7f : 1.5f; fMain.startRotation = new ParticleSystem.MinMaxCurve(0, 360 * Mathf.Deg2Rad);
                    fMain.startColor = new Color(1f, 0.8f, 0.2f, 0.95f); fMain.maxParticles = 1; fMain.simulationSpace = ParticleSystemSimulationSpace.World;
                    var fEmission = flashPs.emission; fEmission.rateOverTime = 0; fEmission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
                    var fFade = flashPs.colorOverLifetime; fFade.enabled = true;
                    var fGradient = new Gradient(); fGradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }); fFade.color = fGradient;
                    var fSize = flashPs.sizeOverLifetime; fSize.enabled = true; fSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.2f, 1, 1.5f));
                    var fRenderer = flashPs.GetComponent<ParticleSystemRenderer>(); fRenderer.sharedMaterial = material; fRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    flashPs.Play(); Object.Destroy(flashGo, 0.2f);
                }
            }

            Burst(position, direction, new Color(1, .58f, .12f, .9f), cannon ? 12 : 5, .3f * scale, 7 * scale, .09f);
            Burst(position, direction, new Color(.7f, .69f, .65f, .25f), cannon ? 28 : 12, .45f * scale, 1.4f * scale, cannon ? 4 : 2, -.025f);
            Burst(position, direction, new Color(1, .65f, .2f), cannon ? 16 : 6, .025f * scale, 9 * scale, .25f, .2f);
            if (!Available) return;
            var go = new GameObject("MuzzleLight"); go.transform.position = position;
            var light = go.AddComponent<Light>(); light.color = new Color(1, .55f, .18f); light.intensity = cannon ? 8 : 2; light.range = cannon ? 15 : 3;
            Object.Destroy(go, .085f);
        }
        public static void Impact(Vector3 position, Vector3 normal, bool cannon, bool wood = false)
        {
            float scale = cannon ? 3 : 1;
            Burst(position, normal, new Color(.43f, .32f, .2f, .6f), cannon ? 24 : 8, .3f * scale, 2 * scale, 1);
            if (wood) Splinters(position, normal);
        }
        static void Splinters(Vector3 point, Vector3 normal)
        {
            if (!Available) return;
            if (chipMesh == null) chipMesh = Resources.Load<Mesh>("CombatVfx/WoodChip");
            if (chipMaterial == null) chipMaterial = Resources.Load<Material>("CombatVfx/WoodChip");
            if (chipMesh == null || chipMaterial == null) return;
            var root = new GameObject("HullSplinters");
            root.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal.sqrMagnitude > .001f ? normal : Vector3.up));
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.duration = .1f; main.playOnAwake = false;
            main.maxParticles = 14; main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
            main.gravityModifier = 1f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(.025f, .07f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(.15f, .45f);
            main.startSizeZ = .035f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 65f; shape.radius = .12f;
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = chipMesh;
            renderer.sharedMaterial = chipMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play(); Object.Destroy(root, 1.7f);
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
            if (!Available) return;
            if (material == null) material = Resources.Load<Material>("CombatParticles");
            if (material == null) return;
            var root = new GameObject("CrewRespawnMist");
            root.transform.position = position + Vector3.up * .6f;
            if (platform != null) root.transform.SetParent(platform, true);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.duration = .1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .8f);
            main.startSpeed = .35f; main.startSize = new ParticleSystem.MinMaxCurve(.12f, .3f);
            main.startColor = new Color(.55f, .85f, .78f, .35f); main.maxParticles = 18;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .55f;
            var emission = particles.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play(); Object.Destroy(root, 1f);
        }
        public static void FireSmoke(Vector3 position)
        {
            Burst(position, Vector3.up, new Color(.19f, .18f, .17f, .32f), 3, 1.4f, 1.5f, 3.5f, -.04f);
        }
    }
}
