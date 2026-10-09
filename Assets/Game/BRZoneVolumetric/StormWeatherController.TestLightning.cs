using UnityEngine;

namespace PirateSlop
{
    public sealed partial class StormWeatherController
    {
        static readonly int TestTimingId = Shader.PropertyToID("_StormTestLightningTiming");
        static readonly int TestBandId = Shader.PropertyToID("_WeatherTestBand");
        static readonly int TestStartsId = Shader.PropertyToID("_StormTestLightningStarts");
        static readonly int TestEndsId = Shader.PropertyToID("_StormTestLightningEnds");
        readonly Vector4[] testLightStarts = new Vector4[12], testLightEnds = new Vector4[12];
        readonly float[] testThunderAt = { -1, -1 };
        readonly Vector3[] testRadial = new Vector3[2], testCenter = new Vector3[2];
        readonly float[] testRadius = new float[2];
        readonly Vector3[][][] testPaths = new Vector3[2][][];
        Material testBoltMaterial;
        bool testPrepared;

        void PrepareTestLightning()
        {
            testPrepared = true;
            var template = Resources.Load<Material>("Storm/TestLightning");
            if (template != null) testBoltMaterial = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
            for (int group = 0; group < groups.Length; group++)
            {
                testPaths[group] = new Vector3[5][];
                for (int branch = 0; branch < 5; branch++)
                {
                    testPaths[group][branch] = new Vector3[branch == 0 ? 33 : 13];
                    groups[group][branch].sharedMaterial = testBoltMaterial;
                    groups[group][branch].positionCount = testPaths[group][branch].Length;
                    groups[group][branch].SetPositions(testPaths[group][branch]);
                }
            }
            GameAudio.PrepareTestStormThunder();
            nextLightning = Clock + .6f + Next01() * .6f;
        }

        void UpdateTestLightning(StormVolumeController storm)
        {
            if (!testPrepared) PrepareTestLightning();
            if (testBoltMaterial != null) storm.ApplyTestCloudField(testBoltMaterial);
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (viewer == null) { ClearLightning(); return; }
            int layer = 1;
            if (layer != renderingLayer)
            {
                foreach (var group in groups) foreach (var line in group) line.gameObject.layer = layer;
                renderingLayer = layer;
            }
            var center = new Vector3(storm.CurrentCenter.x, storm.WaterLevel, storm.CurrentCenter.z);
            float floor = Shader.GetGlobalFloat("_PirateStormTestCloudBase");
            float top = Shader.GetGlobalVector("_PirateStormTestClouds").z;
            properties.SetVector("_WeatherCenter", new Vector4(center.x, center.y, center.z, storm.CurrentRadius));
            properties.SetVector(TestBandId, new Vector4(2, 18, floor, top));
            UpdateCullDistance(storm);
            if (Clock >= nextLightning)
            {
                nextLightning = Clock + 1.35f + Next01();
                int group = nextGroup;
                nextGroup = 1 - group;
                BeginTestLightning(storm, center, floor, top, group);
            }
            Vector4 primary = Vector4.zero, secondary = Vector4.zero;
            var timing = new Vector4(-10, -10, 0, 0);
            for (int group = 0; group < groups.Length; group++)
            {
                float age = Clock - groupStart[group];
                bool active = age >= 0 && age < 1.1f;
                var movement = center - testCenter[group] + testRadial[group] * (storm.CurrentRadius - testRadius[group]);
                var position = groupPosition[group] + movement;
                float pulse = active ? TestLightningPulse(age) : 0;
                bool near = Vector3.SqrMagnitude(viewer.transform.position - position) < 260 * 260;
                for (int branch = 0; branch < groups[group].Length; branch++)
                {
                    var line = groups[group][branch];
                    line.enabled = active && age < .7f && near && pulse > .015f && groupShow[group] && testBoltMaterial != null;
                    if (!line.enabled) continue;
                    line.SetPropertyBlock(properties);
                    var source = testPaths[group][branch];
                    var work = branch == 0 ? boltPath : branchPath;
                    for (int p = 0; p < source.Length; p++) work[p] = source[p] + movement;
                    line.SetPositions(work);
                    var color = new Color(.57f, .19f, 1f, Mathf.Clamp01(pulse * LightningIntensity * (branch == 0 ? 1.8f : 1.1f)));
                    line.startColor = color; line.endColor = new Color(color.r, color.g, color.b, color.a * .75f);
                }
                var flash = new Vector4(position.x, position.y, position.z, active ? LightningIntensity : 0);
                for (int channel = 0; channel < 6; channel++)
                {
                    var path = testPaths[group][channel < 2 ? 0 : channel - 1];
                    var start = path[channel < 2 ? channel * 16 : 0] + movement;
                    var end = path[channel < 2 ? (channel + 1) * 16 : path.Length - 1] + movement;
                    float localAge = age - channel * .014f;
                    float energy = active && localAge >= 0 ? TestLightningPulse(localAge) + .35f * Mathf.Exp(-localAge * 4.5f) : 0;
                    testLightStarts[group * 6 + channel] = new Vector4(start.x, start.y, start.z, energy * (channel < 2 ? 1 : .65f));
                    testLightEnds[group * 6 + channel] = new Vector4(end.x, end.y, end.z, 0);
                }
                if (group == 0) { primary = flash; timing.x = age; timing.z = pulse; }
                else { secondary = flash; timing.y = age; timing.w = pulse; }
                if (testThunderAt[group] >= 0 && Clock >= testThunderAt[group])
                {
                    testThunderAt[group] = -1;
                    GameAudio.TestStormThunder(position, viewer.transform.position);
                }
            }
            Shader.SetGlobalVector(LightningId, primary);
            Shader.SetGlobalVector(SecondaryLightningId, secondary);
            Shader.SetGlobalVector(TestTimingId, timing);
            Shader.SetGlobalVectorArray(TestStartsId, testLightStarts);
            Shader.SetGlobalVectorArray(TestEndsId, testLightEnds);
            Shader.SetGlobalColor(LightningColorId, new Color(.42f, .12f, .8f, 1));
        }

        static float TestLightningPulse(float age)
        {
            return .7f * Mathf.Exp(-age * 7) + .65f * Mathf.Exp(-Mathf.Pow((age - .13f) * 20, 2))
                + .4f * Mathf.Exp(-Mathf.Pow((age - .34f) * 16, 2));
        }

        void BeginTestLightning(StormVolumeController storm, Vector3 center, float floor, float top, int group)
        {
            var from = new Vector2(viewer.transform.position.x - center.x, viewer.transform.position.z - center.z);
            var forward = new Vector2(viewer.transform.forward.x, viewer.transform.forward.z).normalized;
            float angle = Mathf.Atan2(from.y, from.x);
            float b = Vector2.Dot(from, forward);
            float discriminant = b * b - from.sqrMagnitude + storm.CurrentRadius * storm.CurrentRadius;
            if (discriminant >= 0 && forward.sqrMagnitude > .1f)
            {
                float hit = -b - Mathf.Sqrt(discriminant);
                if (hit <= 0) hit = -b + Mathf.Sqrt(discriminant);
                if (hit > 0) { var target = from + forward * hit; angle = Mathf.Atan2(target.y, target.x); }
            }
            if (from.magnitude > storm.CurrentRadius + 18 && Mathf.Abs(from.magnitude - storm.CurrentRadius) < 240)
                angle = Mathf.Atan2(from.y, from.x);
            float lateral = Mathf.Lerp(16, 110, Mathf.InverseLerp(60, 600, Mathf.Abs(from.magnitude - storm.CurrentRadius)));
            angle += (Next01() - .5f) * Mathf.Min(.22f, lateral / Mathf.Max(1, storm.CurrentRadius));
            var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var tangent = new Vector3(-radial.z, 0, radial.x);
            testRadial[group] = radial; testCenter[group] = center; testRadius[group] = storm.CurrentRadius;
            float offset = 7 + Next01() * 2;
            float start = Mathf.Lerp(floor, top, .64f + Next01() * .18f);
            float end = Mathf.Lerp(floor, top, .11f + Next01() * .21f);
            float drift = (Next01() - .5f) * 9;
            var main = testPaths[group][0];
            for (int i = 0; i < main.Length; i++)
            {
                float u = i / (float)(main.Length - 1);
                float side = drift * u + Mathf.Sin(u * 11 + angle) * 1.4f + (Next01() - .5f) * 1.3f;
                main[i] = center + radial * (storm.CurrentRadius + offset + (Next01() - .5f) * 1.6f)
                    + tangent * side + Vector3.up * (Mathf.Lerp(start, end, u) - center.y);
            }
            for (int branch = 1; branch < 5; branch++)
            {
                var path = testPaths[group][branch];
                var origin = main[Mathf.Clamp(5 + branch * 5 + (int)(Next01() * 4), 4, main.Length - 4)];
                float side = branch % 2 == 0 ? -1 : 1;
                float length = 5 + Next01() * 5;
                for (int i = 0; i < path.Length; i++)
                {
                    float u = i / (float)(path.Length - 1);
                    path[i] = origin + tangent * (side * u * length + (Next01() - .5f) * u * .8f)
                        - Vector3.up * u * length * .32f + radial * (Next01() - .5f) * u;
                    path[i].y = Mathf.Clamp(path[i].y, floor + .5f, top - .5f);
                }
            }
            for (int branch = 0; branch < 5; branch++)
            {
                var line = groups[group][branch];
                line.positionCount = testPaths[group][branch].Length;
                line.SetPositions(testPaths[group][branch]);
                line.startWidth = branch == 0 ? .18f : .095f;
                line.endWidth = branch == 0 ? .1f : .035f;
            }
            groupStart[group] = Clock;
            groupPosition[group] = main[12];
            groupShow[group] = Next01() < BoltChance;
            float distance = Vector3.Distance(viewer.transform.position, groupPosition[group]);
            testThunderAt[group] = distance < 240 ? Clock + Mathf.Max(.035f, distance / 343) : -1;
        }
        void OnDestroy()
        {
            if (testBoltMaterial != null) Destroy(testBoltMaterial);
        }
    }
}
