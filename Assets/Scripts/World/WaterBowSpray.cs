using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(1060), DisallowMultipleComponent]
    public sealed class WaterBowSpray : MonoBehaviour
    {
        const int MaximumParticles = 4800, ReturnBudget = 256;
        const float EmissionRate = 2400f, MaximumDistance = 140f, ReturnInterval = .1f;
        public static WaterBowSpray Instance { get; private set; }
        public int ActiveCount => particles != null ? particles.particleCount : 0;
        public int BurstCount { get; private set; }
        public int BowBurstCount { get; private set; }
        public Vector3 LastBowPosition { get; private set; }
        public Vector3 LastBowNormal { get; private set; }

        ParticleSystem particles;
        Material sprayMaterial;
        OceanSurface ocean;
        Camera focusCamera;
        WaterSplashContacts contacts;
        readonly ParticleSystem.Particle[] returned = new ParticleSystem.Particle[MaximumParticles];
        float available, budgetAt, burstAt, returnAt;
        int windowBursts, returnCursor;
        uint randomState = 0x6E624EB7u;
        Texture reflection;
        static readonly int ReflectionId = Shader.PropertyToID("_OceanReflection");
        static readonly int ReflectionAvailableId = Shader.PropertyToID("_ReflectionAvailable");
        static readonly int ReflectionStrengthId = Shader.PropertyToID("_ReflectionStrength");

        public static WaterBowSpray Ensure(GameObject owner)
        {
            if (Instance != null && Instance.isActiveAndEnabled) return Instance;
            if (owner == null) return null;
            var component = owner.GetComponent<WaterBowSpray>();
            if (component == null) component = owner.AddComponent<WaterBowSpray>();
            component.enabled = true;
            return component;
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            var shader = Resources.Load<Shader>("EnvironmentTest/WaterBowSpray");
            if (shader == null)
            {
                Debug.LogError("Water bow spray shader is missing.", this);
                return;
            }
            Instance = this;
            ocean = OceanSurface.Instance;
            available = 720f;
            budgetAt = burstAt = returnAt = Time.time;
            windowBursts = returnCursor = BurstCount = BowBurstCount = 0;
            reflection = null;
            sprayMaterial = new Material(shader) { name = "Ocean bow droplets", hideFlags = HideFlags.HideAndDontSave };
            sprayMaterial.SetVector("_Tint", new Vector4(.62f, .9f, .94f, 1f));
            var visual = new GameObject("Water bow spray particles") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            visual.transform.SetParent(transform, false);
            particles = visual.AddComponent<ParticleSystem>();
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = 1f;
            main.gravityModifier = 1f;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .045f);
            main.maxParticles = MaximumParticles;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.enabled = false;
            var drag = particles.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.dampen = 0f;
            drag.drag = .25f;
            drag.multiplyDragByParticleVelocity = false;
            drag.multiplyDragByParticleSize = false;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .06f), new GradientAlphaKey(.85f, .65f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .85f), new Keyframe(.2f, 1f), new Keyframe(1f, .65f)));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sprayMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .004f;
            renderer.lengthScale = 1.4f;
            renderer.cameraVelocityScale = 0f;
            renderer.freeformStretching = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            UpdateReflection();
            particles.Play(false);
            contacts = GetComponent<WaterSplashContacts>();
            if (contacts == null) contacts = gameObject.AddComponent<WaterSplashContacts>();
            contacts.enabled = true;
        }

        public void EmitImpact(Vector3 position, Vector3 waterVelocity, Vector3 relativeHullVelocity, Vector3 outward, float impact, float strength)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || particles == null || !Finite(position) ||
                !Finite(waterVelocity) || !Finite(relativeHullVelocity) || !Finite(outward) ||
                float.IsNaN(impact) || float.IsInfinity(impact) || float.IsNaN(strength) || float.IsInfinity(strength)) return;
            impact = Mathf.Clamp01(impact);
            strength = Mathf.Clamp(strength, 0f, 2f);
            if (impact <= .001f || strength <= .001f) return;
            UpdateFocus();
            float distanceFade = 1f;
            if (focusCamera != null)
            {
                float distance = Vector3.Distance(focusCamera.transform.position, position);
                if (distance >= MaximumDistance) return;
                distanceFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, MaximumDistance, distance));
            }
            float now = Time.time;
            available = Mathf.Min(720f, available + Mathf.Max(0f, now - budgetAt) * EmissionRate);
            budgetAt = now;
            if (now - burstAt >= .1f)
            {
                burstAt = now;
                windowBursts = 0;
            }
            if (windowBursts >= 2) return;
            int count = Mathf.Min(Mathf.CeilToInt(Mathf.Lerp(24f, 56f, impact) * strength * distanceFade),
                Mathf.FloorToInt(available), MaximumParticles - particles.particleCount);
            if (count <= 0) return;
            if (ocean == null || !ocean.isActiveAndEnabled) ocean = OceanSurface.Instance;
            if (ocean != null) position.y = Mathf.Max(position.y, ocean.Height(position) + .055f);
            var normal = outward.sqrMagnitude > .0001f ? outward.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, normal);
            if (side.sqrMagnitude < .0001f) side = Vector3.right;
            side.Normalize();
            for (int i = 0; i < count; i++)
            {
                float size = Mathf.Lerp(.015f, .045f, Next());
                if (Next() > .95f) size = Mathf.Lerp(.045f, .065f, Next());
                var velocity = waterVelocity + relativeHullVelocity * .35f
                    + normal * Mathf.Lerp(1f, 2.5f, impact)
                    + Vector3.up * Mathf.Lerp(2f, 5f, impact) * Mathf.Lerp(.8f, 1.2f, Next())
                    + side * ((Next() - .5f) * Mathf.Lerp(.7f, 2.2f, impact));
                velocity.y = Mathf.Max(1.4f, velocity.y);
                var emit = new ParticleSystem.EmitParams {
                    position = position + normal * .08f + side * ((Next() - .5f) * .65f) + Vector3.up * .025f,
                    velocity = Vector3.ClampMagnitude(velocity, 14f),
                    startLifetime = Mathf.Lerp(.45f, 1.2f, Next()),
                    startSize = size,
                    startColor = new Color(.82f, .98f, 1f, Mathf.Lerp(.3f, .6f, Next()) * Mathf.Lerp(.65f, 1f, impact)),
                    randomSeed = randomState == 0 ? 1u : randomState
                };
                particles.Emit(emit, 1);
            }
            BowBurstCount++;
            LastBowPosition = position;
            LastBowNormal = normal;
            available -= count;
            windowBursts++;
            BurstCount++;
        }

        public void EmitWaterImpact(WaterImpactEvent impact)
        {
            if (!isActiveAndEnabled || particles == null || impact.Drops <= 0) return;
            UpdateFocus();
            float distance = focusCamera != null ? Vector3.Distance(focusCamera.transform.position, impact.Position) : 0f;
            if (distance >= MaximumDistance) return;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, MaximumDistance, distance));
            float now = Time.time;
            available = Mathf.Min(720f, available + Mathf.Max(0f, now - budgetAt) * EmissionRate);
            budgetAt = now;
            if (now - burstAt >= .1f) { burstAt = now; windowBursts = 0; }
            if (windowBursts >= 4) return;
            int count = Mathf.Min(Mathf.CeilToInt(impact.Drops * fade), Mathf.FloorToInt(available), MaximumParticles - particles.particleCount);
            if (count <= 0) return;
            bool projectile = impact.Kind == WaterImpactKind.Projectile;
            Vector3 normal = impact.Normal;
            Vector3 liftDirection = projectile ? Vector3.Lerp(Vector3.up, normal, .3f).normalized : normal;
            Vector3 forward = impact.Tangent.sqrMagnitude > .001f ? impact.Tangent.normalized : Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
            Vector3 side = Vector3.Cross(normal, forward).normalized;
            float footprint = Mathf.Clamp(impact.Radius, .06f, 1.6f);
            for (int i = 0; i < count; i++)
            {
                bool plume = projectile && i % 3 == 0;
                float angle = Next() * Mathf.PI * 2f;
                Vector3 radial = forward * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                float crown = Mathf.Lerp(projectile ? 3f : 1.5f, projectile ? 9f : 4f, Mathf.Clamp01(impact.Strength));
                Vector3 velocity = impact.WaterVelocity + liftDirection * impact.Lift * Mathf.Lerp(plume ? .9f : .65f, 1.2f, Next())
                    + forward * impact.Drift * Mathf.Lerp(.55f, 1.15f, Next()) * (plume ? .35f : 1f) + radial * crown * (plume ? .3f : 1f);
                if (projectile) velocity.y = Mathf.Max(impact.Lift * .85f, velocity.y);
                float size = projectile ? (plume ? Mathf.Lerp(.09f, .16f, Next()) : Mathf.Lerp(.035f, .075f, Next()))
                    : Mathf.Lerp(.018f, .055f, Next()) * Mathf.Lerp(.8f, 1.25f, Mathf.Clamp01(impact.Radius));
                var emit = new ParticleSystem.EmitParams {
                    position = impact.Position + normal * .065f + Vector3.up * (projectile ? .08f : 0f) + radial * footprint * Mathf.Lerp(.12f, .55f, Next()),
                    velocity = Vector3.ClampMagnitude(velocity, 26f),
                    startLifetime = impact.Life * Mathf.Lerp(.75f, 1.1f, Next()),
                    startSize = size,
                    startColor = plume ? new Color(.98f, 1f, 1f, 1f) : new Color(.82f, .98f, 1f, Mathf.Lerp(projectile ? .45f : .35f, .65f, Next())),
                    randomSeed = randomState == 0 ? 1u : randomState
                };
                particles.Emit(emit, 1);
                if (contacts != null && i % Mathf.Max(1, Mathf.CeilToInt(count / 64f)) == 0)
                    contacts.Track(emit.position, emit.velocity, emit.startLifetime, emit.startSize, emit.randomSeed);
            }
            available -= count;
            windowBursts++;
            BurstCount++;
        }

        void LateUpdate()
        {
            if (particles == null) return;
            UpdateReflection();
            int count = particles.GetParticles(returned);
            bool changed = contacts != null && contacts.Sample(returned, count);
            if (changed) particles.SetParticles(returned, count);
            float now = Time.time;
            if (now - returnAt < ReturnInterval) return;
            returnAt = now;
            if (ocean == null || !ocean.isActiveAndEnabled) ocean = OceanSurface.Instance;
            if (ocean == null) return;
            UpdateFocus();
            if (count == 0) { returnCursor = 0; return; }
            int checkedSurface = 0, visited = 0;
            changed = false;
            while (visited < count && checkedSurface < ReturnBudget)
            {
                int index = (returnCursor + visited) % count;
                visited++;
                var particle = returned[index];
                if (particle.remainingLifetime <= 0f || particle.startLifetime - particle.remainingLifetime < .12f || particle.velocity.y >= -.1f) continue;
                if (focusCamera != null && (particle.position - focusCamera.transform.position).sqrMagnitude > 6400f) continue;
                checkedSurface++;
                if (particle.position.y > ocean.Height(particle.position) + .025f) continue;
                particle.remainingLifetime = 0f;
                returned[index] = particle;
                changed = true;
            }
            returnCursor = (returnCursor + visited) % count;
            if (changed) particles.SetParticles(returned, count);
        }

        void UpdateFocus()
        {
            if (focusCamera == null || !focusCamera.isActiveAndEnabled) focusCamera = Camera.main;
        }

        void UpdateReflection()
        {
            if (sprayMaterial == null) return;
            var texture = RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom ? RenderSettings.customReflectionTexture : null;
            if (texture != null && texture.dimension != TextureDimension.Cube) texture = null;
            if (reflection != texture)
            {
                reflection = texture;
                sprayMaterial.SetTexture(ReflectionId, reflection);
            }
            sprayMaterial.SetFloat(ReflectionAvailableId, reflection != null ? 1f : 0f);
            sprayMaterial.SetFloat(ReflectionStrengthId, Mathf.Clamp(RenderSettings.reflectionIntensity, 0f, 2f));
        }

        float Next()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0xFFFFFFu) / 16777216f;
        }

        static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (particles != null)
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.gameObject.SetActive(false);
                Release(particles.gameObject);
            }
            if (sprayMaterial != null) Release(sprayMaterial);
            particles = null;
            sprayMaterial = null;
            reflection = null;
            ocean = null;
            focusCamera = null;
            if (contacts != null) contacts.enabled = false;
        }

        static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
