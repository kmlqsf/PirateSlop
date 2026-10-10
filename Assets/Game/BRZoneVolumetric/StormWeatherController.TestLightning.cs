using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop.World;

namespace PirateSlop
{
    public sealed partial class StormWeatherController
    {
        const int TestLightningGroups = 12;
        static readonly int TestTimingId = Shader.PropertyToID("_StormTestLightningTiming");
        static readonly int TestBandId = Shader.PropertyToID("_WeatherTestBand");
        static readonly int TestFlashesId = Shader.PropertyToID("_StormSmokeFlashVolumes");
        static readonly int TestStartsId = Shader.PropertyToID("_StormSmokeFlashChannelStarts");
        static readonly int TestEndsId = Shader.PropertyToID("_StormSmokeFlashChannelEnds");
        readonly Vector4[] testFlashes = new Vector4[TestLightningGroups];
        readonly Vector4[] testLightStarts = new Vector4[TestLightningGroups * 6], testLightEnds = new Vector4[TestLightningGroups * 6];
        readonly float[] testStarted = new float[TestLightningGroups], testThunderAt = new float[TestLightningGroups];
        readonly Vector3[] testPositions = new Vector3[TestLightningGroups];
        readonly bool[] testShow = new bool[TestLightningGroups];
        readonly LineRenderer[][] testGroups = new LineRenderer[TestLightningGroups][];
        readonly Vector3[][][] testPaths = new Vector3[TestLightningGroups][][];
        Material testBoltMaterial;
        bool testPrepared;
        float nextTestBurst;
        int nextTestGroup;
        float nextBoundaryBurst;
        int nextBoundaryGroup = 6;
        uint boundaryRandom = 194731;

        void PrepareTestLightning()
        {
            var template = Resources.Load<Material>("Storm/TestLightning");
            if (template != null) testBoltMaterial = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
            foreach (var group in groups) foreach (var line in group) line.enabled = false;
            for (int group = 0; group < TestLightningGroups; group++)
            {
                testGroups[group] = new LineRenderer[5];
                testPaths[group] = new Vector3[5][];
                for (int branch = 0; branch < 5; branch++)
                {
                    testPaths[group][branch] = new Vector3[branch == 0 ? 33 : 13];
                    var go = new GameObject("TestStormLightning_" + group + "_" + branch) { layer = 1 };
                    go.transform.SetParent(transform, false);
                    var line = go.AddComponent<LineRenderer>();
                    line.sharedMaterial = testBoltMaterial;
                    line.useWorldSpace = true;
                    line.alignment = LineAlignment.View;
                    line.textureMode = LineTextureMode.Stretch;
                    line.numCornerVertices = 3;
                    line.numCapVertices = 3;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    line.positionCount = testPaths[group][branch].Length;
                    line.SetPositions(testPaths[group][branch]);
                    line.enabled = false;
                    testGroups[group][branch] = line;
                }
            }
            testPrepared = true;
            ClearTestLightning();
            GameAudio.PrepareTestStormThunder();
            nextTestBurst = Clock + .45f;
            nextBoundaryBurst = Clock + .25f;
        }

        void UpdateTestLightning(StormVolumeController storm)
        {
            if (!testPrepared) PrepareTestLightning();
            if (testBoltMaterial != null) storm.ApplyTestCloudField(testBoltMaterial);
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (viewer == null) { ClearLightning(); return; }
            var center = new Vector3(storm.CurrentCenter.x, storm.WaterLevel, storm.CurrentCenter.z);
            float mapRadius = ProceduralWorld.Instance.Layout.Radius;
            float floor = Shader.GetGlobalFloat("_PirateStormTestCloudBase");
            float top = Shader.GetGlobalVector("_PirateStormTestClouds").z;
            properties.SetVector("_WeatherCenter", new Vector4(center.x, center.y, center.z, storm.CurrentRadius));
            properties.SetVector(TestBandId, new Vector4(32, mapRadius, floor, top));
            UpdateCullDistance(storm);
            if (Clock >= nextTestBurst)
            {
                nextTestBurst = Clock + .75f + Next01() * .3f;
                Vector3 previous = Vector3.zero;
                for (int slot = 0; slot < 3; slot++)
                {
                    Vector3 target = PickTestLightningTarget(center, storm.CurrentRadius, mapRadius, slot);
                    for (int retry = 0; slot > 0 && retry < 3 && Vector3.SqrMagnitude(target - previous) < 24 * 24; retry++)
                        target = PickTestLightningTarget(center, storm.CurrentRadius, mapRadius, slot);
                    BeginTestLightning(center, target, floor, top, nextTestGroup + slot);
                    previous = target;
                }
                nextTestGroup = nextTestGroup == 0 ? 3 : 0;
            }
            if (Clock >= nextBoundaryBurst)
            {
                nextBoundaryBurst = Clock + .6f + BoundaryNext01() * .15f;
                for (int slot = 0; slot < 3; slot++)
                {
                    Vector3 target = PickBoundaryLightningTarget(center, storm.CurrentRadius, mapRadius, slot);
                    uint savedRandom = randomState;
                    randomState = boundaryRandom;
                    BeginTestLightning(center, target, floor, top, nextBoundaryGroup + slot);
                    boundaryRandom = randomState;
                    randomState = savedRandom;
                }
                nextBoundaryGroup = nextBoundaryGroup == 6 ? 9 : 6;
            }
            Vector4 primary = Vector4.zero, secondary = Vector4.zero;
            var timing = new Vector4(-10, -10, 0, 0);
            for (int group = 0; group < TestLightningGroups; group++)
            {
                float age = Clock - testStarted[group];
                bool active = age >= 0 && age < 1.1f;
                var position = testPositions[group];
                float pulse = active ? TestLightningPulse(age) : 0;
                bool near = Vector3.SqrMagnitude(viewer.transform.position - position) < 260 * 260;
                for (int branch = 0; branch < 5; branch++)
                {
                    var line = testGroups[group][branch];
                    line.enabled = active && age < .7f && near && pulse > .015f && testShow[group] && testBoltMaterial != null;
                    if (!line.enabled) continue;
                    line.SetPropertyBlock(properties);
                    var color = new Color(.57f, .19f, 1f, Mathf.Clamp01(pulse * LightningIntensity * (branch == 0 ? 1.8f : 1.1f)));
                    line.startColor = color;
                    line.endColor = new Color(color.r, color.g, color.b, color.a * .75f);
                }
                var flash = new Vector4(position.x, position.y, position.z, active ? LightningIntensity : 0);
                testFlashes[group] = flash;
                for (int channel = 0; channel < 6; channel++)
                {
                    var path = testPaths[group][channel < 2 ? 0 : channel - 1];
                    var start = path[channel < 2 ? channel * 16 : 0];
                    var end = path[channel < 2 ? (channel + 1) * 16 : path.Length - 1];
                    float localAge = age - channel * .014f;
                    float energy = active && localAge >= 0 ? TestLightningPulse(localAge) + .35f * Mathf.Exp(-localAge * 4.5f) : 0;
                    testLightStarts[group * 6 + channel] = new Vector4(start.x, start.y, start.z, energy * (channel < 2 ? 1 : .65f));
                    testLightEnds[group * 6 + channel] = new Vector4(end.x, end.y, end.z, 0);
                }
                if (group == 0) { primary = flash; timing.x = age; timing.z = pulse; }
                else if (group == 1) { secondary = flash; timing.y = age; timing.w = pulse; }
                if (testThunderAt[group] >= 0 && Clock >= testThunderAt[group])
                {
                    testThunderAt[group] = -1;
                    GameAudio.TestStormThunder(position, viewer.transform.position);
                }
            }
            Shader.SetGlobalVector(LightningId, primary);
            Shader.SetGlobalVector(SecondaryLightningId, secondary);
            Shader.SetGlobalVector(TestTimingId, timing);
            Shader.SetGlobalVectorArray(TestFlashesId, testFlashes);
            Shader.SetGlobalVectorArray(TestStartsId, testLightStarts);
            Shader.SetGlobalVectorArray(TestEndsId, testLightEnds);
            Shader.SetGlobalColor(LightningColorId, new Color(.42f, .12f, .8f, 1));
        }

        float BoundaryNext01()
        {
            boundaryRandom = boundaryRandom * 1664525u + 1013904223u;
            return (boundaryRandom & 0x00ffffffu) / 16777216f;
        }

        Vector3 PickBoundaryLightningTarget(Vector3 center, float radius, float mapRadius, int slot)
        {
            float screenX = (slot + .32f + BoundaryNext01() * .36f) / 3f;
            var ray = viewer.ViewportPointToRay(new Vector3(screenX, .56f, 0));
            var direction = new Vector2(ray.direction.x, ray.direction.z).normalized;
            var origin = new Vector2(ray.origin.x, ray.origin.z);
            var zone = new Vector2(center.x, center.z);
            float boundaryRadius = Mathf.Max(1, radius - 2 + BoundaryNext01() * 6);
            if (direction.sqrMagnitude > .1f && TestCircleSpan(origin, direction, zone, boundaryRadius, out var span))
            {
                float distance = span.x > 0 ? span.x : span.y;
                var target = origin + direction * distance;
                if (target.sqrMagnitude < (mapRadius - 8) * (mapRadius - 8))
                    return new Vector3(target.x, center.y, target.y);
            }
            float angle = BoundaryNext01() * Mathf.PI * 2;
            var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var fallback = zone + radial * boundaryRadius;
            fallback = Vector2.ClampMagnitude(fallback, Mathf.Max(1, mapRadius - 8));
            return new Vector3(fallback.x, center.y, fallback.y);
        }

        static bool TestCircleSpan(Vector2 origin, Vector2 direction, Vector2 center, float radius, out Vector2 span)
        {
            var from = origin - center;
            float b = Vector2.Dot(from, direction);
            float discriminant = b * b - from.sqrMagnitude + radius * radius;
            span = Vector2.zero;
            if (discriminant < 0) return false;
            float root = Mathf.Sqrt(discriminant);
            span = new Vector2(-b - root, -b + root);
            return span.y > 0;
        }

        Vector3 PickTestLightningTarget(Vector3 center, float radius, float mapRadius, int slot)
        {
            var zone = new Vector2(center.x, center.z);
            if (slot < 2)
            {
                float screenX = (slot == 0 ? .18f : .82f) + (Next01() - .5f) * .12f;
                var ray = viewer.ViewportPointToRay(new Vector3(screenX, .56f, 0));
                var direction = new Vector2(ray.direction.x, ray.direction.z).normalized;
                var origin = new Vector2(ray.origin.x, ray.origin.z);
                if (direction.sqrMagnitude > .1f && TestCircleSpan(origin, direction, Vector2.zero, Mathf.Max(1, mapRadius - 8), out var outer))
                {
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        float begin = Mathf.Max(0, outer.x), end = outer.y;
                        float clearRadius = Mathf.Max(0, radius + (attempt == 0 ? 8 : -12));
                        if (TestCircleSpan(origin, direction, zone, clearRadius, out var hole))
                        {
                            if (hole.x <= begin && hole.y > begin) begin = hole.y;
                            else if (hole.x > begin) end = Mathf.Min(end, hole.x);
                        }
                        if (end - begin < 8) continue;
                        float width = end - begin;
                        float margin = Mathf.Min(12, width * .2f);
                        float reach = Mathf.Min(150, width - margin * 2);
                        float distance = begin + margin + Next01() * reach;
                        var target = origin + direction * distance;
                        return new Vector3(target.x, center.y, target.y);
                    }
                }
            }
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = Next01() * Mathf.PI * 2;
                float distance = Mathf.Sqrt(Next01()) * Mathf.Max(1, mapRadius - 12);
                var target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if ((target - zone).sqrMagnitude > (radius + 8) * (radius + 8))
                    return new Vector3(target.x, center.y, target.y);
            }
            float fallbackAngle = Next01() * Mathf.PI * 2;
            var radial = new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle));
            float b = Vector2.Dot(zone, radial);
            float reachToEdge = -b + Mathf.Sqrt(Mathf.Max(0, b * b + mapRadius * mapRadius - zone.sqrMagnitude));
            float maximum = Mathf.Max(0, reachToEdge - 8);
            float minimum = Mathf.Min(Mathf.Max(0, radius + 8), maximum);
            float selected = Mathf.Sqrt(Mathf.Lerp(minimum * minimum, maximum * maximum, Next01()));
            var fallback = zone + radial * selected;
            return new Vector3(fallback.x, center.y, fallback.y);
        }

        static float TestLightningPulse(float age)
        {
            return .7f * Mathf.Exp(-age * 7) + .65f * Mathf.Exp(-Mathf.Pow((age - .13f) * 20, 2))
                + .4f * Mathf.Exp(-Mathf.Pow((age - .34f) * 16, 2));
        }

        void BeginTestLightning(Vector3 center, Vector3 target, float floor, float top, int group)
        {
            var radial = (target - center).normalized;
            var tangent = new Vector3(-radial.z, 0, radial.x);
            float angle = Next01() * Mathf.PI * 2;
            float start = Mathf.Lerp(floor, top, .64f + Next01() * .18f);
            float end = Mathf.Lerp(floor, top, .11f + Next01() * .21f);
            float drift = (Next01() - .5f) * 9;
            var main = testPaths[group][0];
            for (int i = 0; i < main.Length; i++)
            {
                float u = i / (float)(main.Length - 1);
                float side = drift * u + Mathf.Sin(u * 11 + angle) * 1.4f + (Next01() - .5f) * 1.3f;
                main[i] = target + radial * ((Next01() - .5f) * 1.6f) + tangent * side
                    + Vector3.up * (Mathf.Lerp(start, end, u) - target.y);
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
                var line = testGroups[group][branch];
                line.SetPositions(testPaths[group][branch]);
                line.startWidth = branch == 0 ? .18f : .095f;
                line.endWidth = branch == 0 ? .1f : .035f;
            }
            testStarted[group] = Clock;
            testPositions[group] = main[12];
            testShow[group] = Next01() < BoltChance;
            float distance = Vector3.Distance(viewer.transform.position, testPositions[group]);
            testThunderAt[group] = distance < 240 ? Clock + Mathf.Max(.035f, distance / 343) : -1;
        }

        void ClearTestLightning()
        {
            if (!testPrepared) return;
            for (int group = 0; group < TestLightningGroups; group++)
            {
                testStarted[group] = -10;
                testThunderAt[group] = -1;
                testFlashes[group] = Vector4.zero;
                if (testGroups[group] != null) foreach (var line in testGroups[group]) line.enabled = false;
            }
            if (StormVolumeController.Instance == owner || StormVolumeController.Instance == null)
                Shader.SetGlobalVectorArray(TestFlashesId, testFlashes);
        }

        void OnDestroy()
        {
            if (testBoltMaterial != null) Destroy(testBoltMaterial);
        }
    }
}
