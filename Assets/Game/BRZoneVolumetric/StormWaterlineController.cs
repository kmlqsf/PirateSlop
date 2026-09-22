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
        [Range(20, 40)] public float FoamWidth = 36;
        [Range(2, 20)] public float MistHeight = 4;
        [Range(0, 1)] public float BaseOpacity = .52f;
        [Min(.001f)] public float NoiseScale = .024f;
        [Min(0)] public float NoiseSpeed = 1;
        [Min(20)] public float NearSprayDistance = 260;
        [Range(8, 24)] public int ActiveSprayCount = 12;
        const int Segments = 512;
        const int Rows = 15;
        Mesh mesh;
        MeshRenderer ring;
        Vector3[] vertices;
        readonly ParticleSystem[] spray = new ParticleSystem[24];
        readonly float[] nextBurst = new float[24];
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
            var indices = new int[Segments * 3 * 4 * 6];
            int index = 0;
            for (int s = 0; s <= Segments; s++)
                for (int layer = 0; layer < 3; layer++)
                    for (int row = 0; row < 5; row++)
                    {
                        int v = s * Rows + layer * 5 + row;
                        uv[v] = new Vector2(s / (float)Segments, row / 4f);
                        kinds[v] = new Vector2(layer == 0 ? 0 : 1, layer);
                        if (s == Segments || row == 4) continue;
                        indices[index++] = v; indices[index++] = v + Rows; indices[index++] = v + 1;
                        indices[index++] = v + 1; indices[index++] = v + Rows; indices[index++] = v + Rows + 1;
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(mist ? 1.2f : .65f, mist ? 2.1f : 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(mist ? 1 : 3, mist ? 2 : 6);
            main.startSize = new ParticleSystem.MinMaxCurve(mist ? .5f : .12f, mist ? 1.1f : .4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = mist ? .12f : 1f;
            main.startColor = mist ? new ParticleSystem.MinMaxGradient(new Color(.48f,.6f,.65f,.14f),new Color(.63f,.72f,.75f,.26f)) : new ParticleSystem.MinMaxGradient(new Color(.7f,.78f,.8f,.5f),new Color(.84f,.88f,.89f,.8f));
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var emission = system.emission;
            emission.rateOverTime = 0;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18;
            shape.radius = 2.5f;
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(.15f, .5f);
            noise.frequency = .16f;
            noise.scrollSpeed = .6f + i * .013f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .7f, 1, 1.25f));
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.57f,.69f,.76f), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(.45f, .6f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SprayMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = .035f;
            spray[i] = system;
            nextBurst[i] = Time.time + i * .31f;
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
                float a = angle + ((i + .5f + Mathf.Sin(i * 7.13f) * .32f) / count - .5f) * arc / radius;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 tangent = new Vector3(-radial.z, 0, radial.x);
                Vector3 position = center + radial * (radius + Mathf.Sin(i * 7.13f) * 5);
                position.y = (ocean != null ? ocean.Height(position) : center.y) + .2f;
                if ((system.transform.position - position).sqrMagnitude > 2500) system.Clear();
                system.transform.SetPositionAndRotation(position, Quaternion.LookRotation((Vector3.up * (i % 3 == 0 ? .15f : 1) + tangent * 1.2f - radial * .35f).normalized));
                var velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = tangent.x * (6 + i % 5) - radial.x * (1 + i % 3);
                velocity.z = tangent.z * (6 + i % 5) - radial.z * (1 + i % 3);
                var emission = system.emission;
                float cluster = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f,.7f,Mathf.PerlinNoise(position.x*.008f+Time.time*.035f,position.z*.008f)));
                emission.rateOverTime = lod * cluster * (i % 3 == 0 ? 1 : 3);
                if (!system.isPlaying) system.Play();
                if (Time.time >= nextBurst[i])
                {
                    float variation = Mathf.PerlinNoise(i * 3.71f, Time.time * .31f);
                    nextBurst[i] = Time.time + .7f + variation * 2.8f;
                    bool tall = i % 3 != 0 && variation > .68f;
                    var burst = new ParticleSystem.EmitParams { velocity = tangent * (5 + i % 5) - radial * 2 + Vector3.up * (tall ? 14 : i % 3 == 0 ? 1 : 5), startSize = i % 3 == 0 ? .85f : tall ? .28f : .2f, startLifetime = tall ? 2.7f : i % 3 == 0 ? 1.8f : 1.1f };
                    system.Emit(burst, Mathf.FloorToInt(lod * (.3f + cluster*.7f) * (tall ? 14 : 10)));
                }
            }
        }

        void UpdateRing(Vector3 center, float radius, OceanSurface ocean)
        {
            for (int s = 0; s <= Segments; s++)
            {
                float angle = s * (Mathf.PI * 2 / Segments);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                float patch = Mathf.PerlinNoise(center.x * .006f + radial.x * radius * .006f + Time.time * .02f, center.z * .006f + radial.z * radius * .006f);
                for (int layer = 0; layer < 3; layer++)
                    for (int row = 0; row < 5; row++)
                    {
                        float u = row / 4f;
                        float offset = (patch-.5f)*6 + (layer == 0 ? (u - .5f) * FoamWidth : (layer == 1 ? -.14f : .14f) * FoamWidth - u * FoamWidth * .22f);
                        Vector3 p = center + radial * Mathf.Max(1, radius + offset);
                        float water = ocean != null ? ocean.Height(p) : center.y;
                        p.y = water + .2f + (layer == 0 ? 0 : u * MistHeight * (.3f + patch * .9f));
                        vertices[s * Rows + layer * 5 + row] = p - center;
                    }
            }
            mesh.vertices = vertices;
            mesh.bounds = new Bounds(new Vector3(0, MistHeight * .5f, 0), new Vector3((radius + FoamWidth) * 2, MistHeight * 3 + 10, (radius + FoamWidth) * 2));
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
