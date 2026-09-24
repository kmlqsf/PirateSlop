using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(320)]
    public sealed class StormWeatherController : MonoBehaviour
    {
        public Material BoltMaterial;
        [Min(2)] public float LightningInterval = 4;
        [Range(0, 1)] public float BoltChance = .15f;
        [Range(0, 3)] public float LightningIntensity = .65f;
        static readonly int LightningId = Shader.PropertyToID("_StormLightning");
        static readonly int LightningColorId = Shader.PropertyToID("_StormLightningColor");
        readonly LineRenderer[] bolts = new LineRenderer[3];
        readonly Vector3[] boltPath = new Vector3[33];
        readonly Vector3[] branchPath = new Vector3[13];
        MaterialPropertyBlock properties;
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
            nextLightning = Time.time + Mathf.Max(2, LightningInterval) * (.7f + Next01() * .85f);
            Shader.SetGlobalVector(LightningId, Vector4.zero);
            Shader.SetGlobalColor(LightningColorId, new Color(.6f, .69f, .82f, 1));
        }

        void LateUpdate()
        {
            var storm = StormVolumeController.Instance;
            if (storm == null || !storm.Ready)
            {
                ClearLightning();
                return;
            }
            var center = new Vector3(storm.CurrentCenter.x, storm.WaterLevel, storm.CurrentCenter.z);
            properties.SetVector("_WeatherCenter", new Vector4(center.x, center.y, center.z, storm.CurrentRadius));
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (Time.time >= nextLightning)
            {
                nextLightning = Time.time + Mathf.Max(2, LightningInterval) * (.7f + Next01() * .85f);
                BeginLightning(storm, center);
            }
            float age = (Time.time - flashStarted) / Mathf.Max(.01f, flashDuration);
            float envelope = age < 1 ? Mathf.Exp(-age * 8) + .16f * Mathf.Exp(-Mathf.Pow((age - .24f) * 22, 2)) : 0;
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
            flashDuration = .3f + Next01() * .2f;
            showBolts = Next01() < BoltChance;
        }

        void ClearLightning()
        {
            Shader.SetGlobalVector(LightningId, Vector4.zero);
            foreach (var bolt in bolts) if (bolt != null) bolt.enabled = false;
        }

        void OnDisable()
        {
            ClearLightning();
        }

    }
}
