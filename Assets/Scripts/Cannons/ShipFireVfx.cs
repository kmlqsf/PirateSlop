using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed class ShipFireVfx : MonoBehaviour
    {
        [SerializeField] Material flameMaterial;
        [SerializeField] Material smokeMaterial;
        [SerializeField] Material emberMaterial;
        public static Vector3 WindVelocity = new(.22f, 0f, .12f);
        static readonly List<ShipFireVfx> active = new();
        static GameObject template;
        static ShipFireVfx lightOwner;
        static Camera camera;
        static float nextCameraCheck, nextLightCheck;
        static bool missingReported;
        ParticleSystem flame, smoke, embers;
        ParticleSystemRenderer flameRenderer, smokeRenderer, emberRenderer;
        MaterialPropertyBlock fadeProperties;
        Light fireLight;
        float size, finishStarted, nextDistanceCheck;
        bool crew, finishing, initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            active.Clear();
            template = null;
            lightOwner = null;
            camera = null;
            nextCameraCheck = nextLightCheck = 0f;
            missingReported = false;
        }

        static ShipFireVfx Resource()
        {
            if (template == null) template = Resources.Load<GameObject>("VFX/ShipFireVfx");
            var resource = template != null ? template.GetComponent<ShipFireVfx>() : null;
            if (resource != null && resource.flameMaterial != null && resource.smokeMaterial != null && resource.emberMaterial != null) return resource;
            if (!missingReported)
            {
                missingReported = true;
                Debug.LogError("Ship fire VFX resources are missing. Run ShipFireVfxSetup.Configure.");
            }
            return null;
        }

        public static ShipFireVfx Create(Transform anchor, Vector3 localPoint, Vector3 localNormal, float size = 1f, bool crew = false)
        {
            if (Application.isBatchMode || Resource() == null) return null;
            var root = Instantiate(template, anchor, false);
            root.name = crew ? "CrewFire" : "ShipFire";
            root.transform.localPosition = localPoint;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            var effect = root.GetComponent<ShipFireVfx>();
            effect.Initialize(localNormal, Mathf.Clamp(size, .1f, 3f), crew);
            return effect;
        }

        void Initialize(Vector3 localNormal, float scale, bool onCrew)
        {
            initialized = true;
            size = scale;
            crew = onCrew;
            Vector3 normal = localNormal.sqrMagnitude > .001f ? localNormal.normalized : Vector3.up;
            float radius = size * .32f;
            flame = MakeSystem(transform, "Flame", flameMaterial, false, 72, radius, normal);
            smoke = MakeSystem(transform, "Smoke", smokeMaterial, true, 32, radius * .55f, normal);
            embers = MakeSystem(transform, "Embers", emberMaterial, true, 16, radius * .65f, normal);
            ConfigureFlame(flame, size, crew);
            ConfigureSmoke(smoke, size, crew, false);
            ConfigureEmbers(embers, size, crew);
            flameRenderer = flame.GetComponent<ParticleSystemRenderer>();
            smokeRenderer = smoke.GetComponent<ParticleSystemRenderer>();
            emberRenderer = embers.GetComponent<ParticleSystemRenderer>();
            fadeProperties = new MaterialPropertyBlock();
            if (!crew)
            {
                fireLight = gameObject.AddComponent<Light>();
                fireLight.color = new Color(1f, .47f, .16f);
                fireLight.range = 4.5f * Mathf.Sqrt(size);
                fireLight.shadows = LightShadows.None;
                fireLight.enabled = false;
            }
            active.Add(this);
            flame.Play();
            smoke.Play();
            embers.Play();
        }

        static ParticleSystem MakeSystem(Transform parent, string name, Material material, bool world, int maximum, float radius, Vector3 localNormal)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localNormal * .025f;
            root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localNormal);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.maxParticles = maximum;
            main.startSpeed = 0f;
            main.startColor = Color.white;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            if (world)
            {
                main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
                var inherit = particles.inheritVelocity;
                inherit.enabled = true;
                inherit.mode = ParticleSystemInheritVelocityMode.Initial;
                inherit.curve = .25f;
            }
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        static void ConfigureFlame(ParticleSystem particles, float size, bool crew)
        {
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .72f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .38f, size * .62f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.15f, .15f);
            var emission = particles.emission;
            emission.rateOverTime = crew ? 30f : 38f;
            var velocity = particles.velocityOverLifetime;
            velocity.y = crew ? .76f : 1.05f * Mathf.Sqrt(size);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = size * .055f;
            noise.frequency = 1.1f;
            noise.scrollSpeed = .6f;
            SetGradient(particles, new Color(1f, .8f, .3f), new Color(1f, .38f, .065f), new Color(.52f, .11f, .018f), .82f);
            SetSize(particles, .45f, 1f, .75f, .2f);
            SetSheet(particles, 3, true);
        }

        static void ConfigureSmoke(ParticleSystem particles, float size, bool crew, bool steam)
        {
            var main = particles.main;
            main.startLifetime = steam ? new ParticleSystem.MinMaxCurve(.45f, .85f) : crew ? new ParticleSystem.MinMaxCurve(.8f, 1.4f) : new ParticleSystem.MinMaxCurve(1.3f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .32f, size * .56f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var emission = particles.emission;
            emission.rateOverTime = steam ? 0f : crew ? 2.5f : 5f;
            var velocity = particles.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(steam ? .45f : .8f, steam ? .8f : 1.3f);
            velocity.x = new ParticleSystem.MinMaxCurve(WindVelocity.x, WindVelocity.x);
            velocity.z = new ParticleSystem.MinMaxCurve(WindVelocity.z, WindVelocity.z);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = .12f * size;
            noise.frequency = .5f;
            noise.scrollSpeed = .3f;
            if (steam) SetGradient(particles, new Color(.82f, .83f, .8f), new Color(.78f, .8f, .77f), new Color(.66f, .68f, .66f), .2f);
            else SetGradient(particles, new Color(.14f, .13f, .11f), new Color(.29f, .28f, .25f), new Color(.42f, .41f, .38f), crew ? .24f : .36f);
            SetSize(particles, .6f, 1f, 1.75f, 2.4f);
            SetSheet(particles, 2, false);
        }

        static void ConfigureEmbers(ParticleSystem particles, float size, bool crew)
        {
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .8f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .014f, size * .028f);
            var emission = particles.emission;
            emission.rateOverTime = crew ? .6f : 1.5f;
            var velocity = particles.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            velocity.x = new ParticleSystem.MinMaxCurve(WindVelocity.x - .2f, WindVelocity.x + .2f);
            velocity.z = new ParticleSystem.MinMaxCurve(WindVelocity.z - .2f, WindVelocity.z + .2f);
            SetGradient(particles, new Color(1f, .68f, .23f), new Color(1f, .3f, .045f), new Color(.35f, .06f, .01f), .8f);
            SetSize(particles, 1f, .9f, .5f, 0f);
        }

        static void SetGradient(ParticleSystem particles, Color first, Color middle, Color last, float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(first, 0f), new GradientColorKey(middle, .45f), new GradientColorKey(last, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, .1f), new GradientAlphaKey(alpha * .65f, .65f), new GradientAlphaKey(0f, 1f) });
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            colors.color = gradient;
        }

        static void SetSize(ParticleSystem particles, float first, float peak, float middle, float last)
        {
            var module = particles.sizeOverLifetime;
            module.enabled = true;
            module.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, first), new Keyframe(.12f, peak), new Keyframe(.65f, middle), new Keyframe(1f, last)));
        }

        static void SetSheet(ParticleSystem particles, int tiles, bool animated)
        {
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = sheet.numTilesY = tiles;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = animated ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, .999f)) : new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = animated ? new ParticleSystem.MinMaxCurve(0f, .18f) : new ParticleSystem.MinMaxCurve(0f, .999f);
            sheet.cycleCount = 1;
        }

        public void Finish()
        {
            if (!initialized || finishing) return;
            finishing = true;
            finishStarted = Time.time;
            flame.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            smoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            embers.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (fireLight != null) fireLight.enabled = false;
            transform.SetParent(null, true);
            var steam = MakeSystem(transform, "ExtinguishSteam", smokeMaterial, true, 12, size * .24f, Vector3.up);
            ConfigureSmoke(steam, Mathf.Max(.4f, size), crew, true);
            steam.Play(false);
            steam.Emit(crew ? 4 : 7);
            Destroy(gameObject, 2.6f);
            nextLightCheck = 0f;
        }

        public static void Impact(Vector3 worldPoint, Vector3 worldNormal)
        {
            if (Application.isBatchMode) return;
            var resource = Resource();
            if (resource == null) return;
            Vector3 normal = worldNormal.sqrMagnitude > .001f ? worldNormal.normalized : Vector3.up;
            var root = new GameObject("ShipFireImpact");
            root.transform.position = worldPoint + normal * .03f;
            var flash = MakeSystem(root.transform, "Flash", resource.flameMaterial, true, 20, .16f, normal);
            ConfigureFlame(flash, .9f, false);
            var main = flash.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.15f, .32f);
            main.startSize = new ParticleSystem.MinMaxCurve(.2f, .55f);
            var emission = flash.emission;
            emission.rateOverTime = 0f;
            var spark = MakeSystem(root.transform, "Sparks", resource.emberMaterial, true, 18, .12f, normal);
            ConfigureEmbers(spark, 1f, false);
            emission = spark.emission;
            emission.rateOverTime = 0f;
            flash.Play(false);
            spark.Play(false);
            flash.Emit(10);
            for (int i = 0; i < 12; i++)
            {
                var sample = Random.insideUnitSphere;
                var emit = new ParticleSystem.EmitParams { velocity = normal * Random.Range(1.4f, 3.1f) + Vector3.up * Random.Range(.3f, 1.5f) + sample * 1.2f };
                spark.Emit(emit, 1);
            }
            Destroy(root, 1.2f);
        }

        static Camera ViewCamera()
        {
            if (camera != null && camera.isActiveAndEnabled) return camera;
            if (Time.unscaledTime >= nextCameraCheck)
            {
                nextCameraCheck = Time.unscaledTime + .75f;
                camera = Camera.main;
            }
            return camera;
        }

        void Update()
        {
            if (!initialized) return;
            if (finishing)
            {
                float fade = 1f - Mathf.Clamp01((Time.time - finishStarted) / .22f);
                fadeProperties.SetFloat("_Fade", fade);
                flameRenderer.SetPropertyBlock(fadeProperties);
                if (fade <= 0f && flame.particleCount > 0) flame.Clear();
                return;
            }
            var view = ViewCamera();
            if (Time.unscaledTime >= nextLightCheck)
            {
                nextLightCheck = Time.unscaledTime + .3f;
                lightOwner = null;
                float nearest = 55f * 55f;
                if (view != null)
                    foreach (var effect in active)
                    {
                        if (effect == null || effect.finishing || effect.fireLight == null) continue;
                        float distance = (effect.transform.position - view.transform.position).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; lightOwner = effect; }
                    }
            }
            if (fireLight != null)
            {
                fireLight.enabled = lightOwner == this;
                fireLight.intensity = Mathf.Lerp(.7f, 1.25f, Mathf.PerlinNoise(transform.position.x, Time.time * 7f));
            }
            if (Time.unscaledTime >= nextDistanceCheck)
            {
                nextDistanceCheck = Time.unscaledTime + .4f;
                float distance = view != null ? (transform.position - view.transform.position).sqrMagnitude : float.MaxValue;
                bool visible = distance <= 180f * 180f;
                flameRenderer.enabled = smokeRenderer.enabled = emberRenderer.enabled = visible;
                bool near = distance <= 80f * 80f;
                var emission = flame.emission;
                emission.rateOverTime = !visible ? 0f : near ? crew ? 30f : 38f : 12f;
                emission = smoke.emission;
                emission.rateOverTime = !visible ? 0f : near ? crew ? 2.5f : 5f : 2f;
                emission = embers.emission;
                emission.rateOverTime = visible && near ? crew ? .6f : 1.5f : 0f;
            }
            var flow = smoke.velocityOverLifetime;
            flow.x = new ParticleSystem.MinMaxCurve(WindVelocity.x, WindVelocity.x);
            flow.z = new ParticleSystem.MinMaxCurve(WindVelocity.z, WindVelocity.z);
        }

        void OnDestroy()
        {
            active.Remove(this);
            if (lightOwner == this) { lightOwner = null; nextLightCheck = 0f; }
        }
    }
}
