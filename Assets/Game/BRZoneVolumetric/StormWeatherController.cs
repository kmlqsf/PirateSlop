using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

namespace PirateSlop
{
    [DefaultExecutionOrder(320)]
    public sealed partial class StormWeatherController : MonoBehaviour
    {
        public Material BoltMaterial;
        [Min(.2f)] public float LightningInterval = 4;
        [Range(0, 1)] public float BoltChance = .85f;
        [Range(0, 3)] public float LightningIntensity = .65f;
        static readonly int LightningId = Shader.PropertyToID("_StormLightning");
        static readonly int LightningColorId = Shader.PropertyToID("_StormLightningColor");
        static readonly int VisibilityId = Shader.PropertyToID("_LightningVisibilityDistance");
        static readonly int MenuPreviewId = Shader.PropertyToID("_WeatherMenuPreview");
        LineRenderer[] bolts;
        readonly LineRenderer[][] groups = new LineRenderer[2][];
        readonly float[] groupStart = { -10, -10 };
        readonly float[] groupDuration = new float[2];
        readonly Vector3[] groupPosition = new Vector3[2];
        readonly bool[] groupShow = new bool[2];
        int nextGroup;
        static readonly int SecondaryLightningId = Shader.PropertyToID("_StormLightningSecondary");
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
        StormVolumeController owner;
        int menuSector = -1;
        Camera cullCamera;
        float previousCullDistance;
        float appliedCullDistance;
        float nextCullCheck;
        int renderingLayer = -1;
        float Clock => owner != null && owner.IsMenuPreview ? Time.unscaledTime : Time.time;

        float Next01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0x00FFFFFFu) / 16777216f;
        }

        void Awake()
        {
            owner = GetComponent<StormVolumeController>();
            properties = new MaterialPropertyBlock();
            for (int group = 0; group < groups.Length; group++)
            {
                groups[group] = new LineRenderer[5];
                for (int i = 0; i < groups[group].Length; i++)
                {
                var go = new GameObject("StormLightning_" + group + "_" + i) { layer = gameObject.layer };
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = BoltMaterial;
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.numCornerVertices = 3;
                line.numCapVertices = 3;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                groups[group][i] = line;
                }
            }
            bolts = groups[0];
            ScheduleLightning();
        }

        void LateUpdate()
        {
            var storm = owner;
            if (storm == null || StormVolumeController.Instance != storm || !storm.Ready)
            {
                ClearLightning();
                return;
            }
            if (storm.TestCloudWall) { UpdateTestLightning(storm); return; }
            Shader.SetGlobalVector("_StormTestLightningTiming", new Vector4(-10,-10,0,0));
            Shader.SetGlobalColor(LightningColorId, new Color(.6f, .69f, .82f, 1));
            var center = new Vector3(storm.CurrentCenter.x, storm.WaterLevel, storm.CurrentCenter.z);
            properties.SetVector("_WeatherCenter", new Vector4(center.x, center.y, center.z, storm.CurrentRadius));
            properties.SetFloat(VisibilityId, storm.IsMenuPreview ? Mathf.Max(6000, storm.CurrentRadius * 1.7f) : 6000);
            properties.SetFloat(MenuPreviewId, storm.IsMenuPreview ? 1f : 0f);
            if (storm.IsMenuPreview) viewer = storm.PreviewCamera;
            else if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            int layer = storm.IsMenuPreview ? gameObject.layer : 1;
            if (layer != renderingLayer)
            {
                foreach (var group in groups) foreach (var line in group) line.gameObject.layer = layer;
                renderingLayer = layer;
            }
            UpdateCullDistance(storm);
            if (Clock >= nextLightning)
            {
                ScheduleLightning();
                int index = storm.IsMenuPreview ? 0 : nextGroup;
                nextGroup = 1 - index;
                bolts = groups[index];
                BeginLightning(storm, center);
                groupStart[index] = flashStarted;
                groupDuration[index] = flashDuration;
                groupPosition[index] = flashPosition;
                groupShow[index] = showBolts;
            }
            Vector4 primary = Vector4.zero, secondary = Vector4.zero;
            for (int group = 0; group < groups.Length; group++)
            {
                float age = (Clock - groupStart[group]) / Mathf.Max(.01f, groupDuration[group]);
                float envelope = age >= 0 && age < 1 ? Mathf.Exp(-age * 5) + .7f * Mathf.Exp(-Mathf.Pow((age - .24f) * 28, 2)) + .35f * Mathf.Exp(-Mathf.Pow((age - .48f) * 30, 2)) : 0;
                float strength = Mathf.Clamp01(envelope) * LightningIntensity;
                if (storm.IsMenuPreview && group != 0) strength = 0;
                var position = groupPosition[group];
                var flash = new Vector4(position.x, position.y, position.z, strength);
                if (group == 0) primary = flash; else secondary = flash;
                for (int i = 0; i < groups[group].Length; i++)
                {
                    var line = groups[group][i];
                    line.enabled = groupShow[group] && strength > .008f && BoltMaterial != null;
                    if (!line.enabled) continue;
                    line.SetPropertyBlock(properties);
                    var color = new Color(.58f, .76f, 1f, Mathf.Clamp01(strength * (i == 0 ? 1.5f : .9f)));
                    line.startColor = color;
                    line.endColor = new Color(color.r, color.g, color.b, color.a * (storm.IsMenuPreview ? .55f : .8f));
                }
            }
            Shader.SetGlobalVector(LightningId, primary);
            Shader.SetGlobalVector(SecondaryLightningId, secondary);
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
                if (storm.IsMenuPreview || Next01() < .9f)
                {
                    menuSector = (menuSector + 1 + (Next01() < .5f ? 0 : 1)) % 3;
                    float screenX = (menuSector + .15f + Next01() * .7f) / 3f;
                    var ray = viewer.ViewportPointToRay(new Vector3(screenX, .55f, 0));
                    forward = new Vector2(ray.direction.x, ray.direction.z).normalized;
                }
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
                angle = Mathf.Atan2(target.y, target.x);
                if (!storm.IsMenuPreview) angle += (Next01() - .5f) * Mathf.Min(.12f, 120 / radius);
            }
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 tangent = new Vector3(-radial.z, 0, radial.x);
            float startHeight = storm.StormHeight * (.5f + Next01() * .3f);
            float endHeight = Next01() < .3f ? 1 : storm.StormHeight * (.07f + Next01() * .15f);
            if (storm.IsMenuPreview)
            {
                startHeight = storm.StormHeight * (.4f + Next01() * .18f);
                endHeight = Next01() < .25f ? 1 : storm.StormHeight * (.04f + Next01() * .10f);
            }
            float offset = outside ? Mathf.Min(storm.OuterThickness * .68f, cameraClearance) : -storm.EffectiveInnerThickness * .85f;
            float boltRadius = Mathf.Max(radius * .22f, radius + offset);
            float side = Next01() < .5f ? -1 : 1;
            for (int i = 0; i < boltPath.Length; i++)
            {
                float u = i / (float)(boltPath.Length - 1);
                float height = Mathf.Lerp(startHeight, endHeight, u);
                float drift = Mathf.Sin(u * 6.2f + angle) * 9 + side * u * 17;
                if(!storm.IsMenuPreview) drift=Mathf.Sin(u*4.6f+angle)*24+Mathf.Sin(u*13.1f+angle*1.7f)*9+side*u*12;
                float jagged = (Next01() - .5f) * 10;
                if (storm.IsMenuPreview) { drift *= 24; jagged *= 32; }
                else { drift *= 2f; jagged *= 2.5f; }
                boltPath[i] = center + radial * (boltRadius - storm.EffectiveInwardOffset * height / storm.StormHeight * .7f + (Next01() - .5f) * 3) + tangent * (drift + jagged) + Vector3.up * height;
            }
            bolts[0].positionCount = boltPath.Length;
            bolts[0].SetPositions(boltPath);
            float width = Mathf.Clamp(storm.StormHeight * .009f, 1.2f, 2.8f);
            if (storm.IsMenuPreview) width = Mathf.Clamp(storm.CurrentRadius * .0012f, 4.5f, 32f);
            else width = Mathf.Clamp((viewer != null ? Vector3.Distance(viewer.transform.position, boltPath[12]) : storm.CurrentRadius) * .006f, .7f, 24f);
            if(!storm.IsMenuPreview)width*=1.35f;
            bolts[0].startWidth = width;
            bolts[0].endWidth = width * .3f;
            for (int branch = 1; branch < bolts.Length; branch++)
            {
                int root = 8 + branch * 5;
                if(!storm.IsMenuPreview)root=Mathf.Clamp(5+branch*5+(int)(Next01()*4),4,boltPath.Length-4);
                Vector3 origin = boltPath[root];
                float direction = branch % 2 == 1 ? 1 : -1;
                float length = 18f + Next01() * 30f;
                if (storm.IsMenuPreview) length *= 14;
                else length *= 6f;
                for (int i = 0; i < branchPath.Length; i++)
                {
                    float u = i / (float)(branchPath.Length - 1);
                    branchPath[i] = origin + tangent * (direction * u * length + (Next01() - .5f) * 5 * u) - Vector3.up * u * length * .8f + radial * (Next01() - .5f) * 4 * u;
                }
                bolts[branch].positionCount = branchPath.Length;
                bolts[branch].SetPositions(branchPath);
                bolts[branch].startWidth = width * (storm.IsMenuPreview ? .5f : .65f);
                bolts[branch].endWidth = width * .08f;
            }
            flashPosition = boltPath[12];
            flashStarted = Clock;
            flashDuration = .38f + Next01() * .38f;
            if (storm.IsMenuPreview) flashDuration = .45f + Next01() * .1f;
            showBolts = Next01() < BoltChance;
            if (viewer != null && !storm.IsMenuPreview)
            {
                float distance = Vector3.Distance(viewer.transform.position, flashPosition);
                StartCoroutine(PlayThunderAfterDelay(flashPosition, Mathf.Clamp(distance / 343f, .08f, 4f)));
            }
        }

        IEnumerator PlayThunderAfterDelay(Vector3 position, float delay)
        {
            yield return new WaitForSeconds(delay);
            var storm = StormVolumeController.Instance;
            if (viewer != null && storm != null && storm.Ready)
                GameAudio.StormThunder(position, GameAudio.StormProximity(storm, viewer.transform.position));
        }

        void ClearLightning()
        {
            ReleaseCullDistance();
            ClearTestLightning();
            if (StormVolumeController.Instance == owner || StormVolumeController.Instance == null)
            {
                Shader.SetGlobalVector(LightningId, Vector4.zero);
                Shader.SetGlobalVector(SecondaryLightningId, Vector4.zero);
                Shader.SetGlobalVector("_StormTestLightningTiming", new Vector4(-10,-10,0,0));
            }
            foreach (var group in groups) if (group != null) foreach (var bolt in group) if (bolt != null) bolt.enabled = false;
        }

        void UpdateCullDistance(StormVolumeController storm)
        {
            if (storm.IsMenuPreview || viewer == null)
            {
                ReleaseCullDistance();
                return;
            }
            if (cullCamera != viewer)
            {
                ReleaseCullDistance();
                cullCamera = viewer;
                previousCullDistance = viewer.layerCullDistances[1];
                appliedCullDistance = -1f;
                nextCullCheck = 0f;
            }
            float wanted = Mathf.Max(previousCullDistance, viewer.farClipPlane);
            if (Mathf.Approximately(wanted, appliedCullDistance) && Clock < nextCullCheck) return;
            nextCullCheck = Clock + 1f;
            var distances = viewer.layerCullDistances;
            if (appliedCullDistance >= 0f && !Mathf.Approximately(distances[1], appliedCullDistance))
                previousCullDistance = distances[1];
            wanted = Mathf.Max(previousCullDistance, viewer.farClipPlane);
            if (!Mathf.Approximately(distances[1], wanted))
            {
                distances[1] = wanted;
                viewer.layerCullDistances = distances;
            }
            appliedCullDistance = wanted;
        }

        void ReleaseCullDistance()
        {
            if (cullCamera != null && appliedCullDistance >= 0f)
            {
                var distances = cullCamera.layerCullDistances;
                if (Mathf.Approximately(distances[1], appliedCullDistance))
                {
                    distances[1] = previousCullDistance;
                    cullCamera.layerCullDistances = distances;
                }
            }
            cullCamera = null;
            appliedCullDistance = -1f;
        }

        void ScheduleLightning()
        {
            float delay = owner != null && owner.IsMenuPreview
                ? Mathf.Max(.2f, LightningInterval) * (.7f + Next01() * .6f)
                : Mathf.Max(.65f, LightningInterval) * (.65f + Next01() * .75f);
            nextLightning = Clock + delay;
        }

        void OnEnable()
        {
            flashStarted = -10;
            showBolts = false;
            for (int i = 0; i < groupStart.Length; i++) { groupStart[i] = -10; groupShow[i] = false; }
            ScheduleLightning();
            if (owner != null && owner.IsMenuPreview) nextLightning = Clock + .2f + Next01() * .2f;
            if (StormVolumeController.Instance == owner)
            {
                Shader.SetGlobalVector(LightningId, Vector4.zero);
                Shader.SetGlobalVector(SecondaryLightningId, Vector4.zero);
                Shader.SetGlobalColor(LightningColorId, new Color(.6f, .69f, .82f, 1));
            }
        }

        void OnDisable()
        {
            ReleaseCullDistance();
            StopAllCoroutines();
            ClearLightning();
        }

    }
}
