using PirateSlop.Networking;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(1100)]
    public sealed partial class StormRainController : MonoBehaviour
    {
        const int Grid = 8;
        const float Cell = 5, FieldSize = Grid * Cell;
        public static StormRainController Instance { get; private set; }
        public int PhysicsQueries { get; private set; }
        public int HeightQueries { get; private set; }
        public int ActiveDrops => near != null ? near.particleCount + far.particleCount : 0;
        public float Intensity { get; private set; }
        public bool Exposed { get; private set; }
        public bool Submerged { get; private set; }
        public Material RainMaterial, ImpactMaterial;
        static readonly ProfilerMarker UpdateMarker = new("Storm rain update");
        readonly float[] field = new float[Grid * Grid], shifted = new float[Grid * Grid];
        readonly RaycastHit[] hits = new RaycastHit[16];
        readonly Vector4[] lens = new Vector4[24], visibleLens = new Vector4[24], visibleMotion = new Vector4[24];
        readonly float[] lensStarted = new float[24], lensSeed = new float[24];
        Texture2D heightTexture;
        Material rain, contactRain;
        ParticleSystem near, far;
        Camera viewer;
        StormVolumeController owner;
        Vector2 fieldOrigin;
        Vector3 previousEye;
        float exposureAt, submergedAt, lensTokens, impactTokens;
        int cursor, nextLens;
        uint randomState = 0x3289ABD1;
        bool downpour;
        bool preparedDownpour;
        float cameraWet;
        readonly Vector3 velocity = new(4, -26, 2);
        public Vector3 Velocity => velocity;
        public bool RendersCamera(Camera camera) => viewer != null && camera == viewer;

        public static float AmountAt(Vector3 point, StormVolumeController storm)
        {
            if (storm == null || !storm.Ready || storm.IsMenuPreview) return 0;
            float distance = Vector2.Distance(new Vector2(point.x, point.z), new Vector2(storm.CurrentCenter.x, storm.CurrentCenter.z)) - storm.CurrentRadius;
            float half = storm.TestCloudWall ? 2 : Mathf.Max(1, storm.WeatherTransitionWidth) * .5f;
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-half, half, distance));
        }

        float Next01()
        {
            randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
            return (randomState & 0x00ffffff) / 16777216f;
        }

        void OnEnable()
        {
            if (!Application.isPlaying || RainMaterial == null || ImpactMaterial == null) return;
            Instance = this;
            owner = GetComponent<StormVolumeController>();
            rain = new Material(RainMaterial) { hideFlags = HideFlags.HideAndDontSave };
            contactRain = new Material(ImpactMaterial) { hideFlags = HideFlags.HideAndDontSave };
            heightTexture = new Texture2D(Grid, Grid, TextureFormat.RFloat, false, true)
            {
                name = "Rain shelter height", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            for (int i = 0; i < field.Length; i++) field[i] = 100000;
            for (int i = 0; i < lens.Length; i++) lensStarted[i] = -100;
            heightTexture.SetPixelData(field, 0); heightTexture.Apply(false, false);
            rain.SetTexture("_ShelterHeight", heightTexture);
            near = MakeRain("Near rain", 5200, 32, .019f, .55f);
            far = MakeRain("Distant rain", 1800, 100, .033f, .24f);
            InitializeImpacts();
            if (owner != null && owner.TestCloudWall) InitializeWetWood();
        }

        ParticleSystem MakeRain(string name, int maximum, float width, float size, float alpha)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false; main.loop = true; main.maxParticles = maximum;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.25f, 1.85f);
            main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(size*.65f,size*1.5f);
            main.startColor = new Color(.8f, .86f, .92f, alpha);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, .5f, width);
            var speed = system.velocityOverLifetime;
            speed.enabled = true; speed.space = ParticleSystemSimulationSpace.World;
            speed.x = velocity.x; speed.y = velocity.y; speed.z = velocity.z;
            var emission = system.emission; emission.rateOverTime = 0;
            var fade = system.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .07f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = rain; renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .018f; renderer.lengthScale = 12;
            renderer.cameraVelocityScale = 0; renderer.freeformStretching = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            system.Play(false);
            system.Emit(maximum);
            system.Clear(false);
            return system;
        }

        void LateUpdate()
        {
            if (rain == null) return;
            using var marker = UpdateMarker.Auto();
            PhysicsQueries = HeightQueries = 0;
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            var sea = OceanSurface.Instance;
            var storm = StormVolumeController.Instance;
            if (viewer == null || sea == null || storm == null || !storm.Ready || storm.IsMenuPreview || storm != owner)
            {
                ResetWeather();
                return;
            }
            downpour = storm.TestCloudWall;
            Instance = this;
            UpdateWetWood();
            rain.SetFloat("_RainDownpour", downpour ? 1 : 0);
            contactRain.SetFloat("_RainDownpour", downpour ? 1 : 0);
            rain.SetVector("_RainWeather", new Vector4(storm.CurrentCenter.x, storm.CurrentCenter.z, storm.CurrentRadius,
                downpour ? 4 : storm.WeatherTransitionWidth));
            if (downpour && !preparedDownpour)
            {
                if (wetMesh == null) InitializeWetWood();
                var warmup = Resources.Load<ShaderVariantCollection>("Storm/RainWarmup");
                if (warmup != null) warmup.WarmUp();
                GameAudio.PrepareStormRain();
                var preparedEmission = near.emission; preparedEmission.rateOverTime = 4300;
                preparedEmission = far.emission; preparedEmission.rateOverTime = 900;
                near.transform.position = viewer.transform.position + Vector3.up * 22;
                far.transform.position = viewer.transform.position + Vector3.up * 28;
                near.Simulate(1.85f, false, true, false); near.Play(false);
                far.Simulate(1.85f, false, true, false); far.Play(false);
                previousEye = viewer.transform.position;
                preparedDownpour = true;
            }
            Vector3 eye = viewer.transform.position;
            bool teleported = (eye - previousEye).sqrMagnitude > 400;
            if (teleported) { near.Clear(); far.Clear(); submergedAt = exposureAt = 0; }
            previousEye = eye;
            if (Time.time >= submergedAt)
            {
                submergedAt = Time.time + .12f;
                Submerged = eye.y < WaterHeight(eye, sea) - .08f;
            }
            var medium = Shader.GetGlobalVector("_BoatAttack_CameraWater");
            if (medium.w > .5f && medium.x > .5f) Submerged = true;
            if (Submerged)
            {
                if (downpour) GameAudio.RainAmbience(0, false);
                ResetWeather();
                return;
            }
            float nearby = Mathf.Max(AmountAt(eye, storm), AmountAt(eye + new Vector3(50, 0, 0), storm));
            nearby = Mathf.Max(nearby, AmountAt(eye - new Vector3(50, 0, 0), storm));
            nearby = Mathf.Max(nearby, AmountAt(eye + new Vector3(0, 0, 50), storm));
            nearby = Mathf.Max(nearby, AmountAt(eye - new Vector3(0, 0, 50), storm));
            Intensity = Mathf.Lerp(Intensity, nearby, 1 - Mathf.Exp(-Time.deltaTime * 5));
            cameraWet = Mathf.Lerp(cameraWet, AmountAt(eye, storm), 1 - Mathf.Exp(-Time.deltaTime * 5));
            var emission = near.emission; emission.rateOverTime = downpour ? 4300 : 1150 * Intensity;
            emission = far.emission; emission.rateOverTime = downpour ? 900 : 210 * Intensity;
            near.transform.position = eye + Vector3.up * 22;
            far.transform.position = eye + Vector3.up * 28;
            rain.SetFloat("_RainFade", Intensity);
            rain.SetFloat("_SeaLevel", sea.SeaLevel);
            UpdateShelter(eye, sea, teleported);
            if (Time.time >= exposureAt)
            {
                exposureAt = Time.time + .15f;
                Exposed = !Cast(eye + Vector3.up * .1f, -velocity.normalized, 180, out _);
            }
            UpdateLens(eye, storm);
            if (downpour) GameAudio.RainAmbience(cameraWet, Exposed);
            impactTokens = Mathf.Min(2, impactTokens + Time.deltaTime * (downpour ? 48 : 24) * Intensity);
            if (impactTokens >= 1 && PhysicsQueries < 4 && HeightQueries < (downpour ? 6 : 4))
            {
                impactTokens--;
                EmitContact(eye, sea, storm);
            }
            UpdateImpacts(sea);
        }

        float WaterHeight(Vector3 point, OceanSurface sea)
        {
            HeightQueries++;
            return sea.Height(point);
        }

        bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
        {
            PhysicsQueries++;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            closest = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.distance >= nearest) continue;
                closest = hit; nearest = hit.distance;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        void UpdateShelter(Vector3 eye, OceanSurface sea, bool reset)
        {
            var origin = new Vector2((Mathf.Floor(eye.x / Cell) - Grid / 2) * Cell, (Mathf.Floor(eye.z / Cell) - Grid / 2) * Cell);
            int dx = Mathf.RoundToInt((origin.x - fieldOrigin.x) / Cell), dz = Mathf.RoundToInt((origin.y - fieldOrigin.y) / Cell);
            if (dx != 0 || dz != 0 || reset)
            {
                for (int z = 0; z < Grid; z++) for (int x = 0; x < Grid; x++)
                {
                    int oldX = x + dx, oldZ = z + dz;
                    shifted[z * Grid + x] = !reset && oldX >= 0 && oldX < Grid && oldZ >= 0 && oldZ < Grid ? field[oldZ * Grid + oldX] : 100000;
                }
                System.Array.Copy(shifted, field, field.Length);
                fieldOrigin = origin;
            }
            bool changed = dx != 0 || dz != 0 || reset;
            if (Intensity > .001f)
                for (int i = 0; i < (downpour ? 1 : 2) && HeightQueries < 3 && PhysicsQueries < 3; i++)
                {
                    int index = cursor++ % field.Length;
                    var point = new Vector3(fieldOrigin.x + (index % Grid + .5f) * Cell, eye.y, fieldOrigin.y + (index / Grid + .5f) * Cell);
                    float water = WaterHeight(point, sea);
                    var start = point; start.y = Mathf.Max(eye.y + 45, sea.SeaLevel + 180);
                    field[index] = Cast(start, Vector3.down, Mathf.Max(220, start.y - water + 2), out var hit) ? Mathf.Max(water, hit.point.y) : water;
                    changed = true;
                }
            if (changed) { heightTexture.SetPixelData(field, 0); heightTexture.Apply(false, false); }
            rain.SetVector("_ShelterField", new Vector4(fieldOrigin.x, fieldOrigin.y, FieldSize, FieldSize));
        }

        void UpdateLens(Vector3 eye, StormVolumeController storm)
        {
            float wet = Exposed ? AmountAt(eye, storm) : 0;
            float facing = Mathf.Lerp(.35f, 1, Mathf.Clamp01(Vector3.Dot(viewer.transform.forward, -velocity.normalized)));
            lensTokens = Mathf.Min(1, lensTokens + Time.deltaTime * wet * facing * (downpour ? 7 : 2.2f));
            if (lensTokens < 1 || SessionController.MenuOpen) return;
            lensTokens--;
            int index = nextLens++ % (downpour ? lens.Length : 12);
            float x = .05f + Next01() * .9f, y = .15f + Next01() * .75f;
            if (Mathf.Abs(x - .5f) < .11f && Mathf.Abs(y - .5f) < .11f) x += x < .5f ? -.16f : .16f;
            lens[index] = new Vector4(x, y, .008f + Next01() * (downpour ? .017f : .01f), .45f + Next01() * .5f);
            lensSeed[index] = Next01();
            lensStarted[index] = Time.time;
        }

        public bool ApplyScreen(Material material, Camera camera)
        {
            if (viewer == null || camera != viewer || Submerged || SessionController.MenuOpen) return false;
            int count = 0;
            for (int i = 0; i < lens.Length; i++)
            {
                float age = Time.time - lensStarted[i];
                float lifetime = downpour ? 4.8f : 3.8f;
                if (age < 0 || age > lifetime) continue;
                var drop = lens[i];
                drop.y -= age * age * (downpour ? .018f : .011f);
                drop.x += downpour ? Mathf.Sin(age * 1.3f + lensSeed[i] * 6.28f) * age * .0015f : 0;
                drop.w *= Mathf.SmoothStep(0, 1, age / .12f) * (1 - Mathf.SmoothStep(1.1f, lifetime, age));
                visibleMotion[count] = new Vector4(age, lensSeed[i], 0, 0);
                visibleLens[count++] = drop;
            }
            var storm = StormVolumeController.Instance;
            var sea = OceanSurface.Instance;
            bool active = storm != null && storm.Ready && sea != null;
            material.SetVector("_RainWeather", active ? new Vector4(storm.CurrentCenter.x, storm.CurrentCenter.z, storm.CurrentRadius, downpour ? 4 : storm.WeatherTransitionWidth) : Vector4.zero);
            material.SetVectorArray("_RainLens", visibleLens);
            material.SetVectorArray("_RainLensMotion", visibleMotion);
            material.SetInteger("_RainLensCount", count);
            material.SetVector("_RainDownpour", downpour ? new Vector4(1, cameraWet, .035f, .9f) : Vector4.zero);
            material.SetFloat("_RainSeaLevel", sea != null ? sea.SeaLevel : 0);
            bool nearby = active && Vector2.Distance(new Vector2(camera.transform.position.x, camera.transform.position.z),
                new Vector2(storm.CurrentCenter.x, storm.CurrentCenter.z)) > storm.CurrentRadius - 750;
            float height = camera.transform.position.y - (storm != null ? storm.WaterLevel : 0);
            bool insideCloud = active && height >= 0 && height < storm.StormHeight && Mathf.Abs(Vector2.Distance(new Vector2(camera.transform.position.x, camera.transform.position.z),new Vector2(storm.CurrentCenter.x,storm.CurrentCenter.z))-storm.CurrentRadius) < storm.EffectiveInnerThickness + storm.OuterThickness;
            return nearby || insideCloud || count > 0;
        }

        void ResetWeather()
        {
            Intensity = cameraWet = 0; Exposed = false; lensTokens = impactTokens = 0;
            near.Clear(); far.Clear();
            var emission = near.emission; emission.rateOverTime = 0;
            emission = far.emission; emission.rateOverTime = 0;
            if (Submerged) for (int i = 0; i < lens.Length; i++) lensStarted[i] = -100;
            ClearImpacts();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (near != null) Destroy(near.gameObject);
            if (far != null) Destroy(far.gameObject);
            if (rain != null) Destroy(rain);
            if (contactRain != null) Destroy(contactRain);
            if (heightTexture != null) Destroy(heightTexture);
            DisposeImpacts();
            DisposeWetWood();
        }
    }
}
