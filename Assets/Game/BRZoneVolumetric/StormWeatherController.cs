using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop.World;

namespace PirateSlop
{
    [DefaultExecutionOrder(320)]
    public sealed class StormWeatherController : MonoBehaviour
    {
        public Material RainMaterial;
        public Material BoltMaterial;
        [Range(0, 1)] public float RainOpacity = .34f;
        [Min(1)] public float RainSpeed = 24;
        [Range(.2f, 1)] public float RainHeightFraction = .72f;
        [Min(2)] public float LightningInterval = 8;
        [Range(0, 1)] public float BoltChance = .7f;
        [Range(0, 3)] public float LightningIntensity = .85f;
        const int Segments = 256;
        const int Rows = 7;
        const int Layers = 2;
        static readonly int LightningId = Shader.PropertyToID("_StormLightning");
        static readonly int LightningColorId = Shader.PropertyToID("_StormLightningColor");
        readonly LineRenderer[] bolts = new LineRenderer[3];
        readonly Vector3[] boltPath = new Vector3[33];
        readonly Vector3[] branchPath = new Vector3[13];
        Mesh rainMesh;
        MeshRenderer rain;
        Vector3[] vertices;
        Vector2[] coordinates;
        MaterialPropertyBlock properties;
        float nextMeshUpdate;
        float nextLightning;
        float flashStarted = -10;
        float flashDuration;
        Vector3 flashPosition;
        bool showBolts;
        uint randomState = 0x83A4F129u;
        Camera viewer;

        float Next01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0x00FFFFFFu) / 16777216f;
        }

        void Awake()
        {
            var curtains = new GameObject("StormRainCurtains");
            curtains.transform.SetParent(transform, false);
            rain = curtains.AddComponent<MeshRenderer>();
            rain.sharedMaterial = RainMaterial;
            rain.shadowCastingMode = ShadowCastingMode.Off;
            rain.receiveShadows = false;
            rainMesh = new Mesh { name = "StormRainRuntime" };
            rainMesh.MarkDynamic();
            curtains.AddComponent<MeshFilter>().sharedMesh = rainMesh;
            vertices = new Vector3[(Segments + 1) * Rows * Layers];
            coordinates = new Vector2[vertices.Length];
            var kinds = new Vector2[vertices.Length];
            var indices = new int[Segments * (Rows - 1) * Layers * 6];
            int index = 0;
            for (int s = 0; s <= Segments; s++)
                for (int layer = 0; layer < Layers; layer++)
                    for (int row = 0; row < Rows; row++)
                    {
                        int v = s * Rows * Layers + layer * Rows + row;
                        kinds[v] = new Vector2(layer, row / (float)(Rows - 1));
                        if (s == Segments || row == Rows - 1) continue;
                        indices[index++] = v; indices[index++] = v + Rows * Layers; indices[index++] = v + 1;
                        indices[index++] = v + 1; indices[index++] = v + Rows * Layers; indices[index++] = v + Rows * Layers + 1;
                    }
            rainMesh.vertices = vertices;
            rainMesh.uv = coordinates;
            rainMesh.uv2 = kinds;
            rainMesh.triangles = indices;
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < bolts.Length; i++)
            {
                var go = new GameObject("StormLightning_" + i);
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = BoltMaterial;
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.numCornerVertices = 1;
                line.numCapVertices = 1;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                bolts[i] = line;
            }
            nextLightning = Time.time + 2.5f + Next01() * 2.5f;
            Shader.SetGlobalVector(LightningId, Vector4.zero);
            Shader.SetGlobalColor(LightningColorId, new Color(.6f, .69f, .82f, 1));
        }

        void LateUpdate()
        {
            var storm = StormVolumeController.Instance;
            if (storm == null || !storm.Ready || RainMaterial == null)
            {
                rain.enabled = false;
                ClearLightning();
                return;
            }
            rain.enabled = true;
            var center = new Vector3(storm.CurrentCenter.x, storm.WaterLevel, storm.CurrentCenter.z);
            rain.transform.position = center;
            properties.SetVector("_WeatherCenter", new Vector4(center.x, center.y, center.z, storm.CurrentRadius));
            properties.SetVector("_RainSettings", new Vector4(RainOpacity, RainSpeed, storm.StormHeight * RainHeightFraction, storm.EffectiveInnerThickness));
            rain.SetPropertyBlock(properties);
            if (Time.time >= nextMeshUpdate)
            {
                nextMeshUpdate = Time.time + .12f;
                UpdateCurtains(storm, center);
            }
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (Time.time >= nextLightning)
            {
                nextLightning = Time.time + Mathf.Max(2, LightningInterval) * (.65f + Next01() * .85f);
                BeginLightning(storm, center);
            }
            float age = (Time.time - flashStarted) / Mathf.Max(.01f, flashDuration);
            float envelope = age < 1 ? Mathf.Exp(-age * 6) + .42f * Mathf.Exp(-Mathf.Pow((age - .31f) * 18, 2)) : 0;
            float strength = Mathf.Clamp01(envelope) * LightningIntensity;
            Shader.SetGlobalVector(LightningId, new Vector4(flashPosition.x, flashPosition.y, flashPosition.z, strength));
            for (int i = 0; i < bolts.Length; i++)
            {
                var line = bolts[i];
                line.SetPropertyBlock(properties);
                line.enabled = showBolts && strength > .008f && BoltMaterial != null;
                var color = new Color(.77f, .83f, .92f, Mathf.Clamp01(strength * (i == 0 ? 1 : .65f)));
                line.startColor = color;
                line.endColor = new Color(color.r, color.g, color.b, color.a * .55f);
            }
        }

        void UpdateCurtains(StormVolumeController storm, Vector3 center)
        {
            float radius = storm.CurrentRadius;
            float height = storm.StormHeight * RainHeightFraction;
            var ocean = OceanSurface.Instance;
            for (int s = 0; s <= Segments; s++)
            {
                float angle = s * Mathf.PI * 2 / Segments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var tangent = new Vector3(-radial.z, 0, radial.x);
                var anchor = center + radial * radius;
                float patch = Mathf.PerlinNoise(anchor.x * .009f + Time.time * .009f, anchor.z * .009f - Time.time * .006f);
                for (int layer = 0; layer < Layers; layer++)
                {
                    float offset = layer == 0 ? -storm.EffectiveInnerThickness * .72f : storm.OuterThickness * .52f;
                    Vector3 foot = center + radial * Mathf.Max(radius * .28f, radius + offset);
                    float water = ocean != null ? ocean.Height(foot) : center.y;
                    for (int row = 0; row < Rows; row++)
                    {
                        float v = row / (float)(Rows - 1);
                        float y = v * height * (.68f + patch * .45f);
                        Vector3 position = foot - radial * (storm.EffectiveInwardOffset * v * .65f) - tangent * (y * .14f);
                        position.y = water + .15f + y;
                        int vertex = s * Rows * Layers + layer * Rows + row;
                        vertices[vertex] = position - center;
                        coordinates[vertex] = new Vector2(angle * radius, y);
                    }
                }
            }
            rainMesh.vertices = vertices;
            rainMesh.uv = coordinates;
            float extent = radius + storm.OuterThickness + height * .2f;
            rainMesh.bounds = new Bounds(new Vector3(0, height * .5f, 0), new Vector3(extent * 2, height + 30, extent * 2));
        }

        void BeginLightning(StormVolumeController storm, Vector3 center)
        {
            float radius = storm.CurrentRadius;
            float angle = Next01() * Mathf.PI * 2;
            bool outside = false;
            float cameraClearance = storm.OuterThickness;
            if (viewer != null)
            {
                Vector2 from = new Vector2(viewer.transform.position.x - center.x, viewer.transform.position.z - center.z);
                Vector2 forward = new Vector2(viewer.transform.forward.x, viewer.transform.forward.z).normalized;
                outside = from.magnitude > radius;
                cameraClearance = Mathf.Max(0, from.magnitude - radius - 8);
                float b = Vector2.Dot(from, forward);
                float discriminant = b * b - (from.sqrMagnitude - radius * radius);
                Vector2 target = from;
                if (discriminant >= 0 && forward.sqrMagnitude > .1f)
                {
                    float near = -b - Mathf.Sqrt(discriminant);
                    float far = -b + Mathf.Sqrt(discriminant);
                    float hit = near > 0 ? near : far;
                    if (hit > 0) target = from + forward * hit;
                }
                angle = Mathf.Atan2(target.y, target.x) + (Next01() - .5f) * Mathf.Min(1.8f, 460 / radius);
            }
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 tangent = new Vector3(-radial.z, 0, radial.x);
            float startHeight = storm.StormHeight * (.5f + Next01() * .3f);
            float endHeight = Next01() < .3f ? 1 : storm.StormHeight * (.07f + Next01() * .15f);
            float offset = outside ? Mathf.Min(storm.OuterThickness * .68f, cameraClearance) : -storm.EffectiveInnerThickness * .85f;
            float boltRadius = Mathf.Max(radius * .22f, radius + offset);
            float side = Next01() < .5f ? -1 : 1;
            for (int i = 0; i < boltPath.Length; i++)
            {
                float u = i / (float)(boltPath.Length - 1);
                float height = Mathf.Lerp(startHeight, endHeight, u);
                float drift = Mathf.Sin(u * 6.2f + angle) * 9 + side * u * 17;
                float jagged = (Next01() - .5f) * 10;
                boltPath[i] = center + radial * (boltRadius - storm.EffectiveInwardOffset * height / storm.StormHeight * .7f + (Next01() - .5f) * 3) + tangent * (drift + jagged) + Vector3.up * height;
            }
            bolts[0].positionCount = boltPath.Length;
            bolts[0].SetPositions(boltPath);
            float width = Mathf.Clamp(storm.StormHeight * .004f, .45f, 1.2f);
            bolts[0].startWidth = width;
            bolts[0].endWidth = width * .3f;
            for (int branch = 1; branch < bolts.Length; branch++)
            {
                int root = 8 + branch * 5;
                Vector3 origin = boltPath[root];
                float direction = branch == 1 ? 1 : -1;
                for (int i = 0; i < branchPath.Length; i++)
                {
                    float u = i / (float)(branchPath.Length - 1);
                    branchPath[i] = origin + tangent * (direction * u * 23 + (Next01() - .5f) * 5 * u) - Vector3.up * u * 26 + radial * (Next01() - .5f) * 4 * u;
                }
                bolts[branch].positionCount = branchPath.Length;
                bolts[branch].SetPositions(branchPath);
                bolts[branch].startWidth = width * .5f;
                bolts[branch].endWidth = width * .08f;
            }
            flashPosition = boltPath[12];
            flashStarted = Time.time;
            flashDuration = .48f + Next01() * .25f;
            showBolts = Next01() < BoltChance;
        }

        void ClearLightning()
        {
            Shader.SetGlobalVector(LightningId, Vector4.zero);
            foreach (var bolt in bolts) if (bolt != null) bolt.enabled = false;
        }

        void OnDisable()
        {
            if (rain != null) rain.enabled = false;
            ClearLightning();
        }

        void OnDestroy()
        {
            if (rainMesh != null) Destroy(rainMesh);
        }
    }
}
