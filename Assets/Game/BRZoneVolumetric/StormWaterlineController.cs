using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop.World;

namespace PirateSlop
{
    [DefaultExecutionOrder(310)]
    public sealed class StormWaterlineController : MonoBehaviour
    {
        public Material WaterlineMaterial;
        public Material SprayMaterial;
        public float WaterY;
        [Range(20, 40)] public float FoamWidth = 38;
        [Range(2, 20)] public float MistHeight = 5.5f;
        [Range(0, 1)] public float BaseOpacity = .64f;
        [Min(.001f)] public float NoiseScale = .019f;
        [Min(0)] public float NoiseSpeed = 1;
        [Min(20)] public float NearSprayDistance = 260;
        [Range(8, 24)] public int ActiveSprayCount = 12;
        const int Segments = 512;
        const int Rows = 17;
        Mesh mesh;
        MeshRenderer ring;
        Vector3[] vertices;
        readonly ParticleSystem[] spray = new ParticleSystem[24];
        readonly ParticleSystemRenderer[] sprayRenderers = new ParticleSystemRenderer[24];
        readonly float[] nextBurst = new float[24];
        readonly uint[] burstSequence = new uint[24];
        MaterialPropertyBlock properties;
        float nextMeshUpdate;
        Camera viewer;
        bool visible;

        void Awake()
        {
            var surface = new GameObject("StormWaterlineRing");
            surface.transform.SetParent(transform, false);
            ring = surface.AddComponent<MeshRenderer>();
            ring.sharedMaterial = WaterlineMaterial;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            mesh = new Mesh { name = "StormWaterlineRuntime" };
            mesh.MarkDynamic();
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            vertices = new Vector3[(Segments + 1) * Rows];
            var uv = new Vector2[vertices.Length];
            var kinds = new Vector2[vertices.Length];
            var indices = new int[Segments * 14 * 6];
            int index = 0;
            for (int s = 0; s <= Segments; s++)
                for (int layer = 0; layer < 3; layer++)
                {
                    int rows = layer == 0 ? 9 : 4;
                    int start = layer == 0 ? 0 : 9 + (layer - 1) * 4;
                    for (int row = 0; row < rows; row++)
                    {
                        int v = s * Rows + start + row;
                        uv[v] = new Vector2(s / (float)Segments, row / (float)(rows - 1));
                        kinds[v] = new Vector2(layer == 0 ? 0 : 1, layer);
                        if (s == Segments || row == rows - 1) continue;
                        indices[index++] = v; indices[index++] = v + Rows; indices[index++] = v + 1;
                        indices[index++] = v + 1; indices[index++] = v + Rows; indices[index++] = v + Rows + 1;
                    }
                }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.uv2 = kinds;
            mesh.triangles = indices;
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < spray.Length; i++) CreateSpray(i);
            SetVisible(false);
        }

        void CreateSpray(int i)
        {
            var go = new GameObject("StormSpray_" + i.ToString("00"));
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = (uint)(179 + i * 3571);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            bool mist = i % 3 == 0;
            main.startLifetime = new ParticleSystem.MinMaxCurve(mist ? 1.15f : .7f, mist ? 2.15f : 1.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(mist ? 1.2f : 5, mist ? 2.5f : 10);
            main.startSize = new ParticleSystem.MinMaxCurve(mist ? .65f : .14f, mist ? 1.35f : .48f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = mist ? .08f : 1f;
            main.startColor = mist ? new ParticleSystem.MinMaxGradient(new Color(.53f,.64f,.69f,.17f),new Color(.69f,.77f,.79f,.3f)) : new ParticleSystem.MinMaxGradient(new Color(.74f,.82f,.85f,.55f),new Color(.92f,.94f,.93f,.9f));
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var emission = system.emission;
            emission.rateOverTime = 0;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = mist ? 12 : 22;
            shape.radius = mist ? 3 : 2;
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(.12f, mist ? .8f : .35f);
            noise.frequency = .2f;
            noise.scrollSpeed = .7f + i * .019f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .75f, 1, mist ? 1.6f : .6f));
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.67f,.75f,.79f), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .06f), new GradientAlphaKey(.6f, .6f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SprayMaterial;
            renderer.renderMode = mist ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.2f;
            renderer.velocityScale = .022f;
            renderer.cameraVelocityScale = 0;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = .028f;
            spray[i] = system;
            sprayRenderers[i] = renderer;
            nextBurst[i] = Time.time + i * .31f;
            burstSequence[i] = (uint)(31 + i * 7919);
        }

        void LateUpdate()
        {
            var zone = StormZone.Instance;
            if (zone == null || zone.Radius <= 0 || WaterlineMaterial == null || SprayMaterial == null)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);
            Vector3 center = zone.Center;
            float radius = zone.Radius;
            var ocean = OceanSurface.Instance;
            center.y = ocean != null ? ocean.SeaLevel : WaterY;
            transform.position = center;
            properties.SetVector("_WaterlineSettings", new Vector4(BaseOpacity, NoiseScale, NoiseSpeed, MistHeight));
            properties.SetVector("_WaterlineCenter", new Vector4(center.x, center.y, center.z, radius));
            ring.SetPropertyBlock(properties);
            if (Time.time >= nextMeshUpdate)
            {
                nextMeshUpdate = Time.time + .08f;
                UpdateRing(center, radius, ocean);
            }
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            float distance = viewer != null ? Mathf.Abs(Vector2.Distance(new Vector2(viewer.transform.position.x, viewer.transform.position.z), new Vector2(center.x, center.z)) - radius) : float.MaxValue;
            if (distance >= NearSprayDistance || viewer == null) { StopSpray(); return; }
            float lod = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(60, NearSprayDistance, distance));
            float angle = Mathf.Atan2(viewer.transform.position.z - center.z, viewer.transform.position.x - center.x);
            int count = Mathf.Clamp(ActiveSprayCount, 8, 24);
            float arc = Mathf.Min(240, radius * 2.5f);
            for (int i = 0; i < spray.Length; i++)
            {
                var system = spray[i];
                if (i >= count) { if (system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); continue; }
                bool mist = i % 3 == 0;
                float a = angle + ((i + .5f + Mathf.Sin(i * 7.13f) * .32f) / count - .5f) * arc / radius;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 tangent = new Vector3(-radial.z, 0, radial.x);
                Vector3 position = center + radial * (radius + Mathf.Sin(i * 7.13f) * 5);
                position.y = (ocean != null ? ocean.Height(position) : center.y) + .12f;
                if ((system.transform.position - position).sqrMagnitude > 2500) system.Clear();
                float gust = Mathf.PerlinNoise(position.x * .006f + Time.time * .09f, position.z * .006f + i * .17f);
                system.transform.SetPositionAndRotation(position, Quaternion.LookRotation((Vector3.up * (mist ? .14f : 1) + tangent * .65f - radial * .3f).normalized));
                var velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                Vector3 wind = tangent * (5 + i % 4 + gust * 4) - radial * (1.5f + gust * 2.5f);
                velocity.x = wind.x;
                velocity.z = wind.z;
                sprayRenderers[i].SetPropertyBlock(properties);
                float cluster = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.24f,.68f,Mathf.PerlinNoise(position.x*.008f+Time.time*.035f,position.z*.008f)));
                var emission = system.emission;
                emission.rateOverTime = lod * cluster * (mist ? 2.2f : 5.5f);
                if (!system.isPlaying) system.Play();
                if (Time.time >= nextBurst[i])
                {
                    float variation = Random01(ref burstSequence[i]);
                    nextBurst[i] = Time.time + .65f + variation * 2.4f;
                    bool tall = !mist && variation > .84f && cluster > .45f;
                    int burstCount = Mathf.FloorToInt(lod * (.3f + cluster * .7f) * (tall ? 20 : mist ? 7 : 14));
                    for (int p = 0; p < burstCount; p++)
                    {
                        float spread = Random01(ref burstSequence[i]);
                        float speed = Random01(ref burstSequence[i]);
                        Vector3 source = position + tangent * ((spread - .5f) * 7) + radial * ((Random01(ref burstSequence[i]) - .5f) * 3);
                        source.y = (ocean != null ? ocean.Height(source) : center.y) + .12f;
                        var burst = new ParticleSystem.EmitParams
                        {
                            position = source,
                            applyShapeToPosition = false,
                            velocity = tangent * (2 + spread * 4) - radial * (1 + speed * 2) + Vector3.up * (tall ? 13 + speed * 4 : mist ? .6f + speed : 4 + speed * 6),
                            startSize = mist ? .7f + speed * .65f : .16f + speed * .29f,
                            startLifetime = tall ? 2.8f + speed * .5f : mist ? 1.7f + speed * .4f : .85f + speed * .7f
                        };
                        system.Emit(burst, 1);
                    }
                }
            }
        }

        static float Random01(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) * (1f / 16777216f);
        }

        void UpdateRing(Vector3 center, float radius, OceanSurface ocean)
        {
            for (int s = 0; s <= Segments; s++)
            {
                float angle = s * (Mathf.PI * 2 / Segments);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-radial.z, 0, radial.x);
                Vector3 boundary = center + radial * radius;
                float patch = Mathf.PerlinNoise(boundary.x * .006f + Time.time * .023f, boundary.z * .006f - Time.time * .015f);
                float curl = Mathf.PerlinNoise(boundary.x * .021f - Time.time * .055f, boundary.z * .021f + Time.time * .038f);
                for (int layer = 0; layer < 3; layer++)
                {
                    int rows = layer == 0 ? 9 : 4;
                    int start = layer == 0 ? 0 : 9 + (layer - 1) * 4;
                    for (int row = 0; row < rows; row++)
                    {
                        float u = row / (float)(rows - 1);
                        float offset = (patch - .5f) * 9 + (layer == 0 ? (u - .5f) * FoamWidth : (layer == 1 ? -.12f : .13f) * FoamWidth - u * FoamWidth * .18f);
                        Vector3 p = center + radial * Mathf.Max(1, radius + offset);
                        if (layer != 0) p += tangent * (u * u * (2 + curl * 5));
                        float water = ocean != null ? ocean.Height(p) : center.y;
                        p.y = water + .16f + (layer == 0 ? 0 : u * MistHeight * (.42f + patch * .63f + curl * .2f));
                        vertices[s * Rows + start + row] = p - center;
                    }
                }
            }
            mesh.vertices = vertices;
            mesh.bounds = new Bounds(new Vector3(0, MistHeight * .5f, 0), new Vector3((radius + FoamWidth) * 2, MistHeight * 3 + 16, (radius + FoamWidth) * 2));
        }

        void SetVisible(bool value)
        {
            if (ring != null) ring.enabled = value;
            if (!value && visible) StopSpray();
            visible = value;
        }
        void StopSpray()
        {
            foreach (var system in spray) if (system != null && system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable() { SetVisible(false); StopSpray(); }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
