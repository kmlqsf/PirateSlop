using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FishNet.Managing;
using PirateSlop.Networking;
using PirateSlop.World;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace PirateSlop.Performance
{
    public static class PerformanceCounters
    {
        public static readonly string[] DrawNames = { "Standard Draw Calls Count", "Standard Indirect Draw Calls Count", "Standard Instanced Draw Calls Count", "SRP Batcher Draw Calls Count", "BRG Draw Calls Count", "BRG Indirect Draw Calls Count", "Null Geometry Draw Calls Count", "Null Geometry Indirect Draw Calls Count" };

        public static ProfilerRecorder Record(List<ProfilerRecorderHandle> handles, ProfilerCategory category, string name)
        {
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name == name && description.Category == category) return ProfilerRecorder.StartNew(category, name, 1);
            }
            return default;
        }

        public static long Value(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue : -1;
    }

    [Serializable]
    public sealed class PerformanceMetadata
    {
        public string utc, mode, applicationVersion, unityVersion, buildType, cpu, gpu, graphicsApi, quality, pipeline, mapRevision, routeVersion, routeHash, mapChecksum, role, warmState, environmentSignature;
        public string timingNote = "Profiler timings refer to completed frames; FrameTiming fallback may be delayed. CPU frame timings may include waits. Missing values are -1/N/A. Marker times sum invocations in a frame and may overlap. Ships includes event vessels; crewShips counts team-owned vessels. Effects covers FirearmVfx particles and active leased VfxPool objects; memory/debris/effects/crew counts refresh at 2 Hz. Primary physics steps are FixedUpdate steps; secondary steps count CorpsePhysics simulations. CPU/GPU are not added.";
        public int width, height, vSync, fpsLimit, ramMB, vramMB, tickRate, expectedShips, expectedPlayers;
        public float renderScale, fov, nearClip, farClip, warmupSeconds, requestedSeconds, waveStrength, waveSteepness, waveSpeed, stormRadius;
        public string antialiasing, fleetScenario, renderExperiment;
        public string instrumentationVersion;
        public string cpuOptimizationVersion;
        public string renderOptimizationVersion, benchmarkHudVersion;
        public bool benchmarkHudVisible;
        public bool profilerEnabled, editor, telemetryVisible, hullSweepFastPath;
        public bool shipBodyPoseFastPath, worldVisualBudget, playerGuiLayoutBudget, shipCompactLod, tentacleBurstCurve, cloudBoundsFastPath,
            shipVisibilityPhases, waterAsyncDiscovery;
    }

    [Serializable]
    public struct PerformanceFrame
    {
        public int unityFrame, segment, ships, crewShips, players, debris, effects, physicsSteps, secondaryPhysicsSteps, networkSteps;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
        public int upperPhysicalDebris, upperCosmeticDebris;
#endif
        public int frustumCrewShips, frustumPlayers;
        public double time;
        public float frameMs, cpuMs, renderMs, gpuMs, x, y, z, qx, qy, qz, qw, stormRadius, stormDistance, rainIntensity;
        public bool cameraAvailable, stormReady, rainReady, submerged;
        public long draws, setPass, triangles, gcBytes, usedBytes, reservedBytes, monoBytes;
        public int gc0, gc1, gc2;
    }

    public struct PerformanceLiveSnapshot
    {
        public PerformanceFrame latest;
        public int frames, over1667, over3333;
        public double fps, p99Ms, maxMs, cpuMs, renderMs, gpuMs;
    }

    [Serializable]
    public struct PerformanceShipSample
    {
        public double time;
        public int unityFrame, segment, objectId, teamId, participantId;
        public Vector3 position, whirlpoolCenter;
        public Quaternion rotation;
        public float floodLevel, waterHeight, whirlpoolDistance, whirlpoolRadius, whirlpoolDepth;
        public bool spawned, sinking, oceanAvailable, insideWhirlpool;
    }

    [Serializable]
    public struct PerformanceEvent
    {
        public int unityFrame;
        public double time;
        public string kind, target;
        public float x, y, z, value;
    }

    [Serializable]
    public sealed class PerformanceSummary
    {
        public string segment;
        public int frames, over833, over1667, over3333, over50, over100, gcMeasuredFrames;
        public double durationSeconds, averageFps, low1Fps, meanMs, p50Ms, p95Ms, p99Ms, p999Ms, maxMs;
        public double cpuMeanMs = -1, renderMeanMs = -1, gpuMeanMs = -1;
        public long gcBytes = -1;
    }

    [Serializable]
    public sealed class PerformanceSecond
    {
        public int second, segment, firstFrameIndex, lastFrameIndex, worstFrameIndex;
        public PerformanceSummary summary;
    }

    [Serializable]
    public sealed class PerformanceReport
    {
        public int schemaVersion = 3;
        public string status, reason;
        public PerformanceMetadata metadata;
        public PerformanceSummary total;
        public PerformanceCombatSummary combat;
        public PerformanceSummary[] segments;
        public PerformanceSecond[] seconds;
        public string secondsLayout = "Frames grouped by floor(time) and segment, using frame start time; a frame crossing a second boundary stays in its starting bucket. Frame indices reference frames[]. Camera quaternion and weather state describe the completed view, not an image or proof of visible objects.";
        public string[] markerNames;
        public string markerLayout = "markerMilliseconds[frameIndex * markerNames.Length + markerIndex]; -1 means unavailable";
        public float[] markerMilliseconds;
        public string[] cpuStageNames;
        public float[] cpuStageMilliseconds;
        public string cpuStageLayout = "cpuStageMilliseconds[frameIndex * cpuStageNames.Length + stageIndex]; -1 unavailable. Stopwatch wall-clock on main thread, including scheduler pauses and native waits. Loop.* measures top-level PlayerLoop phases; Code.* scopes are inclusive and overlap phases/other scopes. FixedUpdate repeats accumulate. Do not add these values to CPU/GPU or to one another. Probes are installed only during capture setup/recording and removed on release.";
        public PerformanceFrame[] frames;
        public PerformanceEvent[] events;
        public PerformanceShipSample[] shipSamples;
        public int droppedShipSamples;
        public string shipSamplesLayout = "Live ship state sampled once per second and on capture finish, with its own Unity frame/time; not an image or exact completed-frame state. objectId is the FishNet ID local to this session; IDs may be reused after despawn. Includes event vessels (teamId <= 0). Missing ocean/flood values are -1. Sampling never changes gameplay. No screenshot readback or PNG encoding during measurement.";
        public int errors, warnings, droppedEvents;
        public long captureBufferBytes;
    }

    public static class PerformanceStatistics
    {
        public static PerformanceSummary Calculate(PerformanceFrame[] frames, int segment, string name)
        {
            var values = new List<double>();
            var result = new PerformanceSummary { segment = name };
            double cpu = 0, render = 0, gpu = 0;
            int cpuCount = 0, renderCount = 0, gpuCount = 0;
            foreach (var frame in frames)
            {
                if (segment >= 0 && frame.segment != segment || !float.IsFinite(frame.frameMs) || frame.frameMs <= 0) continue;
                values.Add(frame.frameMs);
                result.durationSeconds += frame.frameMs / 1000d;
                if (frame.gcBytes >= 0) { if (result.gcBytes < 0) result.gcBytes = 0; result.gcBytes += frame.gcBytes; result.gcMeasuredFrames++; }
                if (frame.cpuMs >= 0) { cpu += frame.cpuMs; cpuCount++; }
                if (frame.renderMs >= 0) { render += frame.renderMs; renderCount++; }
                if (frame.gpuMs >= 0) { gpu += frame.gpuMs; gpuCount++; }
                if (frame.frameMs > 1000d / 120d) result.over833++;
                if (frame.frameMs > 1000d / 60d) result.over1667++;
                if (frame.frameMs > 1000d / 30d) result.over3333++;
                if (frame.frameMs > 50) result.over50++;
                if (frame.frameMs > 100) result.over100++;
            }
            result.frames = values.Count;
            if (values.Count == 0) return result;
            values.Sort();
            result.averageFps = values.Count / result.durationSeconds;
            result.meanMs = result.durationSeconds * 1000 / values.Count;
            result.p50Ms = Percentile(values, .5); result.p95Ms = Percentile(values, .95);
            result.p99Ms = Percentile(values, .99); result.p999Ms = Percentile(values, .999);
            result.maxMs = values[values.Count - 1];
            int slow = Math.Max(1, (int)Math.Ceiling(values.Count * .01));
            double slowTotal = 0;
            for (int i = values.Count - slow; i < values.Count; i++) slowTotal += values[i];
            result.low1Fps = 1000 * slow / slowTotal;
            result.cpuMeanMs = cpuCount > 0 ? cpu / cpuCount : -1;
            result.renderMeanMs = renderCount > 0 ? render / renderCount : -1;
            result.gpuMeanMs = gpuCount > 0 ? gpu / gpuCount : -1;
            return result;
        }

        static double Percentile(List<double> values, double fraction)
        {
            double index = (values.Count - 1) * fraction;
            int low = (int)index, high = Math.Min(low + 1, values.Count - 1);
            return values[low] + (values[high] - values[low]) * (index - low);
        }

        static string Time(double value) => value < 0 ? "N/A" : value.ToString("F3", CultureInfo.InvariantCulture);
        static string Bytes(long value) => value < 0 ? "N/A" : value.ToString(CultureInfo.InvariantCulture);

        static void CpuText(StringBuilder text, PerformanceReport report)
        {
            if (report.cpuStageNames == null || report.cpuStageMilliseconds == null) return;
            text.AppendLine(report.cpuStageLayout);
            text.AppendLine("CPU wall-clock: segment | stage | mean ms | max ms | measured frames");
            for (int segment = -1; segment < report.segments.Length; segment++)
                for (int stage = 0; stage < report.cpuStageNames.Length; stage++)
                {
                    double sum = 0, maximum = 0; int count = 0;
                    for (int i = 0; i < report.frames.Length; i++)
                    {
                        if (segment >= 0 && report.frames[i].segment != segment) continue;
                        int at = i * report.cpuStageNames.Length + stage;
                        if (at >= report.cpuStageMilliseconds.Length || report.cpuStageMilliseconds[at] < 0) continue;
                        double ms = report.cpuStageMilliseconds[at]; sum += ms; maximum = Math.Max(maximum, ms); count++;
                    }
                    text.AppendLine($"{(segment < 0 ? "TOTAL" : Segment(report, segment))} | {report.cpuStageNames[stage]} | {Time(count > 0 ? sum / count : -1)} | {Time(count > 0 ? maximum : -1)} | {count}");
                }
        }

        public static PerformanceSecond[] Timeline(PerformanceFrame[] frames, string[] names)
        {
            var result = new List<PerformanceSecond>();
            for (int first = 0; first < frames.Length;)
            {
                int second = (int)Math.Floor(frames[first].time), segment = frames[first].segment;
                int end = first + 1, worst = first;
                while (end < frames.Length && (int)Math.Floor(frames[end].time) == second && frames[end].segment == segment)
                {
                    if (frames[end].frameMs > frames[worst].frameMs) worst = end;
                    end++;
                }
                var bucket = new PerformanceFrame[end - first];
                Array.Copy(frames, first, bucket, 0, bucket.Length);
                string name = segment >= 0 && segment < names.Length ? names[segment] : "Unknown segment " + segment;
                result.Add(new PerformanceSecond { second = second, segment = segment, firstFrameIndex = first, lastFrameIndex = end - 1, worstFrameIndex = worst, summary = Calculate(bucket, -1, name) });
                first = end;
            }
            return result.ToArray();
        }

        static string Segment(PerformanceReport report, int index) => index >= 0 && index < report.segments.Length ? report.segments[index].segment : "Unknown segment " + index;

        static string View(PerformanceFrame frame) => !frame.cameraAvailable ? "N/A" : FormattableString.Invariant($"({frame.x:F1},{frame.y:F1},{frame.z:F1}) q({frame.qx:F3},{frame.qy:F3},{frame.qz:F3},{frame.qw:F3})");

        static string Weather(PerformanceFrame frame) => (frame.stormReady ? FormattableString.Invariant($"R={frame.stormRadius:F1}m distance={frame.stormDistance:F1}m (positive=safe)") : "storm=N/A") + (frame.rainReady ? FormattableString.Invariant($" rain={frame.rainIntensity:F3} submerged={frame.submerged}") : " rain=N/A");

        public static string Text(PerformanceReport report)
        {
            var text = new StringBuilder(4096);
            text.AppendLine("PirateSlop performance report v" + report.schemaVersion);
            text.AppendLine(report.status + " " + report.reason);
            var m = report.metadata;
            text.AppendLine(m.utc + " | " + m.mode + " | " + m.buildType + " | " + m.warmState);
            text.AppendLine(m.cpu + " | " + m.gpu + " | " + m.graphicsApi);
            text.AppendLine($"{m.width}x{m.height} | {m.quality} | scale {m.renderScale} | FOV {m.fov} | VSync {m.vSync} | FPS limit {m.fpsLimit}");
            text.AppendLine($"Route {m.routeVersion} / {m.routeHash} | map {m.mapRevision} | {m.role} | tick {m.tickRate}");
            text.AppendLine("Environment: " + m.environmentSignature);
            text.AppendLine("Instrumentation: " + m.instrumentationVersion);
            if (report.combat != null)
            {
                var combat = report.combat;
                text.AppendLine($"Combat attrition: ships lost={combat.shipsLost}, min afloat={combat.minAliveShips}; sinking times are scenario seconds (-1 means survived).");
                text.AppendLine("COMBAT: compare matched camera/phase segments, not TOTAL against stationary isolation.");
                text.AppendLine("Combat schedule: " + combat.schedule + " | " + combat.limitations);
                text.AppendLine($"Actual cannon shots/hits: {combat.cannonShots}/{combat.cannonImpacts}; pistol shots/visual hits: {combat.firearmShots}/{combat.firearmImpacts}; player damage/deaths: {combat.playerDamageEvents}/{combat.deaths}; min alive: {combat.minAlivePlayers}; POV fallbacks: {combat.cameraFallbacks}");
                for (int i = 0; i < 6; i++) text.AppendLine($"Combat ship {i}: shots={combat.shotsByShip[i]}, hits received={combat.hitsByShip[i]}, travelled={combat.distanceByShip[i]:F1}m, sinking at={combat.firstSinkingSeconds[i]:F2}s");
            }
            text.AppendLine("Benchmark HUD: " + m.benchmarkHudVisible + " / " + (m.benchmarkHudVersion ?? "none"));
            text.AppendLine("Hull sweep fast path: " + m.hullSweepFastPath);
            text.AppendLine("Ship body pose fast path: " + m.shipBodyPoseFastPath);
            text.AppendLine("World visual budget: " + m.worldVisualBudget);
            text.AppendLine("Player GUI layout budget: " + m.playerGuiLayoutBudget);
            text.AppendLine("CPU optimization package: " + (m.cpuOptimizationVersion ?? "baseline"));
            text.AppendLine("Render optimization package: " + (m.renderOptimizationVersion ?? "baseline"));
            text.AppendLine("Cloud bounds fast path: " + m.cloudBoundsFastPath);
            text.AppendLine("Tentacle Burst curve: " + m.tentacleBurstCurve);
            text.AppendLine("Ship visibility phases: " + m.shipVisibilityPhases);
            text.AppendLine("Water async discovery: " + m.waterAsyncDiscovery);
            text.AppendLine("Compact ship LOD: " + m.shipCompactLod);
            if (m.mode == "isolation") text.AppendLine("DIAGNOSTIC ISOLATION: total FPS mixes disabled systems and is not gameplay FPS. Analyze stages separately, excluding the first 2 seconds of each stage.");
            text.AppendLine("Fleet scenario: " + (m.fleetScenario ?? "N/A (older report)"));
            text.AppendLine("Render experiment: " + (string.IsNullOrEmpty(m.renderExperiment) ? "reference" : m.renderExperiment));
            text.AppendLine($"Expected {m.expectedShips} ships / {m.expectedPlayers} players | warmup {m.warmupSeconds}s | buffer {report.captureBufferBytes / 1048576d:F1} MB");
            text.AppendLine("Segment | frames | seconds | avg FPS | 1% low | P50 / P95 / P99 / P99.9 / max ms | CPU / Render / GPU mean ms");
            void Add(PerformanceSummary s) => text.AppendLine($"{s.segment} | {s.frames} | {s.durationSeconds:F2} | {s.averageFps:F2} | {s.low1Fps:F2} | {Time(s.p50Ms)} / {Time(s.p95Ms)} / {Time(s.p99Ms)} / {Time(s.p999Ms)} / {Time(s.maxMs)} | {Time(s.cpuMeanMs)} / {Time(s.renderMeanMs)} / {Time(s.gpuMeanMs)}");
            Add(report.total);
            foreach (var segment in report.segments) Add(segment);
            text.AppendLine($"Frames >8.33/16.67/33.33/50/100ms: {report.total.over833}/{report.total.over1667}/{report.total.over3333}/{report.total.over50}/{report.total.over100}");
            text.AppendLine($"GC allocated: {Bytes(report.total.gcBytes)} bytes ({report.total.gcMeasuredFrames}/{report.total.frames} measured frames) | errors {report.errors} | warnings {report.warnings} | dropped events {report.droppedEvents}");
            text.AppendLine(m.timingNote);
            CpuText(text, report);
            text.AppendLine(report.secondsLayout);
            text.AppendLine("Timeline: second | segment | avg FPS | P99 / max ms | CPU / Render / GPU mean ms | GC bytes | worst Unity frame / time | camera | weather at worst frame");
            foreach (var row in report.seconds ?? Array.Empty<PerformanceSecond>())
            {
                var s = row.summary;
                var f = report.frames[row.worstFrameIndex];
                text.AppendLine($"{row.second} | {s.segment} | {s.averageFps:F2} | {Time(s.p99Ms)}/{Time(s.maxMs)} | {Time(s.cpuMeanMs)}/{Time(s.renderMeanMs)}/{Time(s.gpuMeanMs)} | {Bytes(s.gcBytes)} | {f.unityFrame}/{f.time:F3} | {View(f)} | {Weather(f)}");
            }
            var worst = new int[report.frames.Length];
            for (int i = 0; i < worst.Length; i++) worst[i] = i;
            Array.Sort(worst, (a, b) => report.frames[b].frameMs.CompareTo(report.frames[a].frameMs));
            text.AppendLine("Longest frames: Unity frame | segment | time | frame/CPU/Render/GPU ms | GC bytes / collections | physics/secondary/network steps | camera | weather | recorded markers >=0.05ms (may overlap)");
            for (int i = 0; i < Math.Min(20, worst.Length); i++)
            {
                var f = report.frames[worst[i]];
                text.Append($"{f.unityFrame} | {Segment(report, f.segment)} | {f.time:F3} | {Time(f.frameMs)}/{Time(f.cpuMs)}/{Time(f.renderMs)}/{Time(f.gpuMs)} | {Bytes(f.gcBytes)}/{f.gc0},{f.gc1},{f.gc2} | {f.physicsSteps}/{f.secondaryPhysicsSteps}/{f.networkSteps} | {View(f)} | {Weather(f)}");
                if (report.markerNames != null && report.markerMilliseconds != null)
                    for (int marker = 0; marker < report.markerNames.Length; marker++)
                    {
                        int at = worst[i] * report.markerNames.Length + marker;
                        if (at < report.markerMilliseconds.Length && report.markerMilliseconds[at] >= .05f) text.Append(" | " + report.markerNames[marker] + "=" + Time(report.markerMilliseconds[at]));
                    }
                if (report.cpuStageNames != null && report.cpuStageMilliseconds != null)
                    for (int stage = 0; stage < report.cpuStageNames.Length; stage++)
                    {
                        int at = worst[i] * report.cpuStageNames.Length + stage;
                        if (at < report.cpuStageMilliseconds.Length && report.cpuStageMilliseconds[at] >= .05f) text.Append(" | " + report.cpuStageNames[stage] + "=" + Time(report.cpuStageMilliseconds[at]));
                    }
                text.AppendLine();
            }
            text.AppendLine("Events: time | Unity frame | kind | target | value | XYZ (if supplied by event)");
            foreach (var e in report.events) text.AppendLine($"{e.time:F3} | {e.unityFrame} | {e.kind} | {e.target} | {e.value} | ({e.x:F2},{e.y:F2},{e.z:F2})");
            text.AppendLine(report.shipSamplesLayout);
            text.AppendLine("Ship state: time | Unity frame | segment | object/team/participant | XYZ | quaternion | flood | sinking/spawned | whirlpool distance/radius/depth | inside | water height | whirlpool center | ocean available");
            foreach (var s in report.shipSamples ?? Array.Empty<PerformanceShipSample>())
                text.AppendLine(FormattableString.Invariant($"{s.time:F3} | {s.unityFrame} | {Segment(report, s.segment)} | {s.objectId}/{s.teamId}/{s.participantId} | ({s.position.x:F2},{s.position.y:F2},{s.position.z:F2}) | ({s.rotation.x:F3},{s.rotation.y:F3},{s.rotation.z:F3},{s.rotation.w:F3}) | {s.floodLevel:F3} | {s.sinking}/{s.spawned} | {s.whirlpoolDistance:F2}/{s.whirlpoolRadius:F2}/{s.whirlpoolDepth:F2} | {s.insideWhirlpool} | {s.waterHeight:F2} | ({s.whirlpoolCenter.x:F2},{s.whirlpoolCenter.y:F2},{s.whirlpoolCenter.z:F2}) | {s.oceanAvailable}"));
            text.AppendLine("Dropped ship samples: " + report.droppedShipSamples);
            return text.ToString();
        }

        public static string Compare(PerformanceReport before, PerformanceReport after)
        {
            if (before == null || after == null || before.schemaVersion < 2 || before.schemaVersion > 3 || after.schemaVersion < 2 || after.schemaVersion > 3 || before.metadata == null || after.metadata == null || before.total == null || after.total == null || before.segments == null || after.segments == null) throw new InvalidDataException("Report schema v2/v3 is required.");
            var mismatches = new List<string>();
            if (before.schemaVersion != after.schemaVersion) mismatches.Add("Report schema changed: repeat baseline with the same instrumentation.");
            if (before.status != "COMPLETE" || after.status != "COMPLETE") mismatches.Add("Only COMPLETE reports are accepted.");
            if (before.metadata.mode != "benchmark" && before.metadata.mode != "isolation" && before.metadata.mode != "combat" || after.metadata.mode != "benchmark" && after.metadata.mode != "isolation" && after.metadata.mode != "combat") mismatches.Add("Only automated benchmark/isolation/combat reports are baselines; manual recordings and functionality probes are excluded.");
            string[] conditions = { "mode", "routeVersion", "routeHash", "mapRevision", "mapChecksum", "buildType", "unityVersion", "cpu", "gpu", "graphicsApi", "width", "height", "quality", "pipeline", "renderScale", "fov", "nearClip", "farClip", "antialiasing", "vSync", "fpsLimit", "tickRate", "role", "expectedShips", "expectedPlayers", "warmupSeconds", "requestedSeconds", "warmState", "waveStrength", "waveSteepness", "waveSpeed", "stormRadius", "telemetryVisible", "profilerEnabled", "environmentSignature", "fleetScenario", "renderExperiment", "benchmarkHudVisible", "benchmarkHudVersion" };
            if ((before.metadata.instrumentationVersion ?? "") != (after.metadata.instrumentationVersion ?? "")) mismatches.Add("instrumentationVersion: " + before.metadata.instrumentationVersion + " -> " + after.metadata.instrumentationVersion);
            foreach (string key in conditions)
            {
                var field = typeof(PerformanceMetadata).GetField(key);
                object a = field.GetValue(before.metadata), b = field.GetValue(after.metadata);
                if (field.FieldType == typeof(string)) { a = a ?? ""; b = b ?? ""; }
                if (!Equals(a, b)) mismatches.Add(key + ": " + a + " -> " + b);
            }
            if (before.segments.Length != after.segments.Length) mismatches.Add("Segment counts differ.");
            else for (int i = 0; i < before.segments.Length; i++) if (before.segments[i].segment != after.segments[i].segment) mismatches.Add("Segment names/order differ.");
            if (before.combat != null || after.combat != null)
            {
                var a = before.combat; var b = after.combat;
                if (a == null || b == null) mismatches.Add("Combat workload is missing in one report.");
                else
                {
                    if (a.schedule != b.schedule) mismatches.Add("Combat control schedules differ.");
                    if (a.shipsLost != b.shipsLost || a.minAliveShips != b.minAliveShips) mismatches.Add("Combat ship losses differ.");
                    if (a.firstSinkingSeconds == null || b.firstSinkingSeconds == null || a.firstSinkingSeconds.Length != 6 || b.firstSinkingSeconds.Length != 6) mismatches.Add("Combat sinking timeline missing.");
                    else for (int i = 0; i < 6; i++)
                        if ((a.firstSinkingSeconds[i] < 0) != (b.firstSinkingSeconds[i] < 0) || Math.Abs(a.firstSinkingSeconds[i] - b.firstSinkingSeconds[i]) > 5) mismatches.Add("Combat sinking timing differs materially for ship " + i);
                    if (Math.Abs(a.cannonShots - b.cannonShots) > Math.Max(2, a.cannonShots * .05) || Math.Abs(a.firearmShots - b.firearmShots) > Math.Max(3, a.firearmShots * .05)) mismatches.Add("Actual combat shot workload differs by more than 5%.");
                    if (Math.Abs(a.cannonImpacts - b.cannonImpacts) > Math.Max(2, a.cannonImpacts * .2)) mismatches.Add("Actual cannon impact workload differs by more than 20%.");
                    if (Math.Abs(a.deaths - b.deaths) > 3 || Math.Abs(a.minAlivePlayers - b.minAlivePlayers) > 3 || a.cameraFallbacks != b.cameraFallbacks) mismatches.Add("Combat casualties or POV fallbacks differ materially.");
                }
            }
            var output = new StringBuilder(2048);
            output.AppendLine(mismatches.Count == 0 ? "COMPARABLE" : "NOT COMPARABLE — do not claim a performance improvement");
            foreach (var mismatch in mismatches) output.AppendLine(mismatch);
            output.AppendLine("Hull sweep fast path (intentional optimization A/B): " + before.metadata.hullSweepFastPath + " -> " + after.metadata.hullSweepFastPath);
            output.AppendLine("Ship body pose fast path (intentional optimization A/B): " + before.metadata.shipBodyPoseFastPath + " -> " + after.metadata.shipBodyPoseFastPath);
            output.AppendLine("World visual budget (intentional optimization A/B): " + before.metadata.worldVisualBudget + " -> " + after.metadata.worldVisualBudget);
            output.AppendLine("Player GUI layout budget (intentional optimization A/B): " + before.metadata.playerGuiLayoutBudget + " -> " + after.metadata.playerGuiLayoutBudget);
            output.AppendLine("CPU optimization package: " + (before.metadata.cpuOptimizationVersion ?? "baseline") + " -> " + (after.metadata.cpuOptimizationVersion ?? "baseline"));
            output.AppendLine("Cloud bounds fast path: " + before.metadata.cloudBoundsFastPath + " -> " + after.metadata.cloudBoundsFastPath);
            output.AppendLine("Tentacle Burst curve: " + before.metadata.tentacleBurstCurve + " -> " + after.metadata.tentacleBurstCurve);
            output.AppendLine("Ship visibility phases: " + before.metadata.shipVisibilityPhases + " -> " + after.metadata.shipVisibilityPhases);
            output.AppendLine("Water async discovery: " + before.metadata.waterAsyncDiscovery + " -> " + after.metadata.waterAsyncDiscovery);
            output.AppendLine("Compact ship LOD (intentional optimization A/B): " + before.metadata.shipCompactLod + " -> " + after.metadata.shipCompactLod);
            output.AppendLine("Segment | avg FPS before/after | 1% low before/after | mean ms change | P99 before/after");
            void Add(PerformanceSummary a, PerformanceSummary b)
            {
                string delta = a.meanMs > 0 ? ((b.meanMs / a.meanMs - 1) * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%" : "N/A";
                output.AppendLine($"{a.segment} | {a.averageFps:F2}/{b.averageFps:F2} | {a.low1Fps:F2}/{b.low1Fps:F2} | {delta} | {a.p99Ms:F3}/{b.p99Ms:F3}");
            }
            Add(before.total, after.total);
            for (int i = 0; i < Math.Min(before.segments.Length, after.segments.Length); i++) Add(before.segments[i], after.segments[i]);
            output.AppendLine("Negative ms change is faster. Compare three runs per stage; changes within run-to-run variation are inconclusive.");
            return output.ToString();
        }
    }

    [DefaultExecutionOrder(-500)]
    public sealed class PerformanceCapture : MonoBehaviour
    {
        const int Capacity = 262144, EventCapacity = 8192;
        public static PerformanceCapture Active { get; private set; }
        public static string LastReportPath { get; private set; }
        public static string LastResult { get; private set; } = "Отчётов пока нет";
        public static bool Saving { get; private set; }
        public bool Recording { get; private set; }
        public double SubmergedSeconds { get; private set; }
        public bool Exhausted => count >= Capacity;
        public int Segment;
        public Camera View;
        public int Errors { get; private set; }
        public PerformanceCombatScenario CombatScenario { get; set; }
        readonly string[] markerNames = { "Ships.VisualRig", "Ships.RopeTubes", "Ships.Attachments", "Ships.PhysicsPublish", "Ships.RenderBatchVisibility", "Performance.CannonImpact", "Performance.ShipDamage", "Performance.ShipPublish", "Performance.RenderRebuild", "Performance.CollisionBatch", "Performance.FirearmImpact", "Performance.PlayerDamage", "Performance.Ragdoll", "Performance.CorpsePhysics", "Storm rain update", "LoadTest.ScriptedMotion" };
        PerformanceFrame[] frames;
        float[] markerValues;
        float[] cpuStageValues;
        readonly float[] liveScratch = new float[2048];
        readonly List<PerformanceEvent> events = new(EventCapacity);
        readonly List<PerformanceShipSample> shipSamples = new(16384);
        double nextShipSample;
        int droppedShipSamples;
        ProfilerRecorder cpu, render, gpu, drawsTotal, triangles, setPass, gc;
        ProfilerRecorder[] draws, markers;
        readonly FrameTiming[] timings = new FrameTiming[1];
        readonly Plane[] frustumPlanes = new Plane[6];
        int cachedFrustumShips = -1, cachedFrustumPlayers = -1;
        ulong lastTimingTimestamp;
        NetworkManager manager;
        PerformanceMetadata metadata;
        int count, warnings, droppedEvents, fixedSteps, secondarySteps, networkSteps, cachedDebris, cachedEffects, cachedCrewShips;
        int lastGC0, lastGC1, lastGC2;
        long cachedUsed, cachedReserved, cachedMono;
        double origin, nextCounts;
        sealed class SavedReport { public string Path, Result; }
        Task<SavedReport> savingTask;

        public static void Event(string kind, UnityEngine.Object target = null, Vector3 point = default, float value = 0)
        {
            var capture = Active;
            if (capture == null) return;
            if (capture.Recording) capture.CombatScenario?.Observe(kind, target);
            if (capture.events.Count >= EventCapacity) { capture.droppedEvents++; return; }
            capture.events.Add(new PerformanceEvent { time = Time.realtimeSinceStartupAsDouble - capture.origin, unityFrame = Time.frameCount, kind = kind, target = target != null ? target.name : "", x = point.x, y = point.y, z = point.z, value = value });
        }

        public static void SecondaryPhysicsStep() { if (Active != null && Active.Recording) Active.secondarySteps++; }

        public void Prepare(PerformanceMetadata value)
        {
            if (Active != null) throw new InvalidOperationException("A performance capture is already active.");
            metadata = value; CombatScenario = null; count = Errors = warnings = droppedEvents = 0; SubmergedSeconds = 0; events.Clear();
            shipSamples.Clear(); droppedShipSamples = 0; nextShipSample = 0;
            frames = new PerformanceFrame[Capacity]; markerValues = new float[Capacity * markerNames.Length];
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            cpu = PerformanceCounters.Record(handles, ProfilerCategory.Render, "CPU Main Thread Frame Time");
            render = PerformanceCounters.Record(handles, ProfilerCategory.Render, "CPU Render Thread Frame Time");
            gpu = PerformanceCounters.Record(handles, ProfilerCategory.Render, "GPU Frame Time");
            drawsTotal = PerformanceCounters.Record(handles, ProfilerCategory.Render, "Draw Calls Count");
            triangles = PerformanceCounters.Record(handles, ProfilerCategory.Render, "Triangles Count");
            setPass = PerformanceCounters.Record(handles, ProfilerCategory.Render, "SetPass Calls Count");
            gc = PerformanceCounters.Record(handles, ProfilerCategory.Memory, "GC Allocated In Frame");
            draws = new ProfilerRecorder[PerformanceCounters.DrawNames.Length];
            for (int i = 0; i < draws.Length; i++) draws[i] = PerformanceCounters.Record(handles, ProfilerCategory.Render, PerformanceCounters.DrawNames[i]);
            markers = new ProfilerRecorder[markerNames.Length];
            for (int i = 0; i < markers.Length; i++) markers[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, markerNames[i], 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
            manager = GetComponent<NetworkManager>();
            if (manager != null) manager.TimeManager.OnPostTick += NetworkStep;
            origin = Time.realtimeSinceStartupAsDouble; Active = this;
            Application.logMessageReceived += Log;
            try
            {
                PerformanceCpuTimings.Start();
                metadata.instrumentationVersion = PerformanceCpuTimings.Version;
                cpuStageValues = new float[Capacity * PerformanceCpuTimings.Names.Length];
            }
            catch { Release(); throw; }
        }

        public void StartRecording()
        {
            double newOrigin = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < events.Count; i++) { var e = events[i]; e.time -= newOrigin - origin; events[i] = e; }
            origin = newOrigin;
            count = fixedSteps = secondarySteps = networkSteps = 0;
            lastGC0 = GC.CollectionCount(0); lastGC1 = GC.CollectionCount(1); lastGC2 = GC.CollectionCount(2);
            nextCounts = 0; Recording = true;
            Event("RecordingStarted");
        }

        void FixedUpdate() { if (Recording) fixedSteps++; }
        void NetworkStep() { if (Recording) networkSteps++; }

        void SampleShips(bool force = false)
        {
            if (!Recording) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!force && now < nextShipSample) return;
            using var cpuTiming = PerformanceCpuTimings.Measure(PerformanceCpuSection.ShipStateSampling);
            nextShipSample = now + 1;
            var ocean = OceanSurface.Instance;
            foreach (var ship in NetworkShip.ActiveShips)
            {
                if (ship == null) continue;
                if (shipSamples.Count >= 16384) { droppedShipSamples++; continue; }
                var position = ship.transform.position;
                var flood = ship.GetComponent<PirateSlop.ShipFlooding>();
                var sample = new PerformanceShipSample
                {
                    time = now - origin, unityFrame = Time.frameCount, segment = Segment,
                    objectId = ship.NetworkObject != null ? ship.NetworkObject.ObjectId : -1, teamId = ship.TeamId.Value, participantId = ship.ParticipantId.Value,
                    position = position, rotation = ship.transform.rotation, spawned = ship.IsSpawned, sinking = ship.IsSinking,
                    floodLevel = flood != null ? flood.Level : -1, oceanAvailable = ocean != null,
                    waterHeight = -1, whirlpoolDistance = -1, whirlpoolRadius = -1, whirlpoolDepth = -1
                };
                if (ocean != null)
                {
                    sample.waterHeight = ocean.Height(position);
                    sample.whirlpoolCenter = ocean.WhirlpoolCenter;
                    sample.whirlpoolRadius = ocean.WhirlpoolRadius; sample.whirlpoolDepth = ocean.WhirlpoolDepth;
                    sample.whirlpoolDistance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(ocean.WhirlpoolCenter.x, ocean.WhirlpoolCenter.z));
                    sample.insideWhirlpool = ocean.WhirlpoolDepth > 0 && sample.whirlpoolDistance < ocean.WhirlpoolRadius;
                }
                shipSamples.Add(sample);
            }
        }

        public void CompareRecentReports()
        {
            if (Active != null || Saving) return;
            string directory = Path.Combine(Application.persistentDataPath, "PerformanceReports");
            Saving = true; LastResult = "Сравнение отчётов…";
            savingTask = Task.Run(() =>
            {
                if (!Directory.Exists(directory)) throw new InvalidDataException("Reports folder is empty.");
                string[] files = Directory.GetFiles(directory, "*.json"); Array.Sort(files, StringComparer.Ordinal);
                PerformanceReport before = null, after = null; string beforePath = null, afterPath = null;
                for (int i = files.Length - 1; i >= 0; i--)
                {
                    var candidate = JsonUtility.FromJson<PerformanceReport>(File.ReadAllText(files[i], Encoding.UTF8));
                    if (candidate == null || candidate.status != "COMPLETE" || candidate.metadata == null || candidate.metadata.mode != "benchmark" && candidate.metadata.mode != "isolation" && candidate.metadata.mode != "combat") continue;
                    if (after != null && (candidate.metadata.mode != after.metadata.mode || candidate.metadata.routeVersion != after.metadata.routeVersion)) continue;
                    if (after == null) { after = candidate; afterPath = files[i]; }
                    else { before = candidate; beforePath = files[i]; break; }
                }
                if (before == null || after == null) throw new InvalidDataException("Two completed benchmark reports are required.");
                string result = PerformanceStatistics.Compare(before, after);
                string path = Path.Combine(directory, "comparison-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".txt");
                File.WriteAllText(path, "Baseline: " + Path.GetFileName(beforePath) + "\nCandidate: " + Path.GetFileName(afterPath) + "\n" + result, new UTF8Encoding(false));
                return new SavedReport { Path = path, Result = result.StartsWith("COMPARABLE", StringComparison.Ordinal) ? "Сравнение сохранено: " + Path.GetFileName(path) : "Условия отличаются. Сравнение: " + Path.GetFileName(path) };
            });
        }

        void Update()
        {
            using var timing = PerformanceCpuTimings.Measure(PerformanceCpuSection.Capture);
            if (savingTask != null && savingTask.IsCompleted)
            {
                Saving = false;
                if (savingTask.IsFaulted) LastResult = "Ошибка сохранения: " + savingTask.Exception.GetBaseException().Message;
                else { LastReportPath = savingTask.Result.Path; LastResult = savingTask.Result.Result; }
                savingTask = null;
            }
            if (Active != this) return;
            FrameTimingManager.CaptureFrameTimings();
            if (!Recording || count >= Capacity) return;
            if (!metadata.profilerEnabled && Profiler.enabled)
            {
                metadata.profilerEnabled = true;
                Event("ProfilerObservedDuringCapture");
            }
            SampleShips();
            var delta = Time.unscaledDeltaTime;
            if (!float.IsFinite(delta) || delta <= 0) return;
            double time = Time.realtimeSinceStartupAsDouble - origin - delta;
            if (time < -.001) return;
            if (Time.realtimeSinceStartupAsDouble >= nextCounts)
            {
                nextCounts = Time.realtimeSinceStartupAsDouble + .5;
                cachedUsed = Profiler.GetTotalAllocatedMemoryLong(); cachedReserved = Profiler.GetTotalReservedMemoryLong(); cachedMono = Profiler.GetMonoUsedSizeLong();
                cachedDebris = 0;
                cachedCrewShips = 0;
                foreach (var ship in NetworkShip.ActiveShips)
                    if (ship != null) { if (ship.TeamId.Value > 0 && ship.IsSpawned) cachedCrewShips++; if (ship.TryGetComponent<ShipDebrisPool>(out var pool)) cachedDebris += pool.ActiveCount; }
                cachedEffects = FirearmVfx.ActiveCount + VfxPool.ActiveCount;
                cachedFrustumShips = cachedFrustumPlayers = -1;
                if (View != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(View, frustumPlanes);
                    cachedFrustumShips = cachedFrustumPlayers = 0;
                    foreach (var ship in NetworkShip.ActiveShips)
                    {
                        if (ship == null || !ship.IsSpawned || ship.TeamId.Value <= 0) continue;
                        var half = ship.HullHalfExtents;
                        var right = ship.transform.right * half.x;
                        var forward = ship.transform.forward * half.y;
                        var size = new Vector3((Mathf.Abs(right.x) + Mathf.Abs(forward.x)) * 2, 45, (Mathf.Abs(right.z) + Mathf.Abs(forward.z)) * 2);
                        if (GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(ship.transform.position + Vector3.up * 18, size))) cachedFrustumShips++;
                    }
                    foreach (var player in NetworkPlayer.Active)
                        if (player != null && GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(player.transform.position + Vector3.up, new Vector3(2, 2, 2)))) cachedFrustumPlayers++;
                }
            }
            var frame = new PerformanceFrame { unityFrame = Time.frameCount - 1, segment = Segment, time = Math.Max(0, time), frameMs = delta * 1000, cpuMs = Ms(cpu), renderMs = Ms(render), gpuMs = Ms(gpu), draws = PerformanceCounters.Value(drawsTotal), setPass = PerformanceCounters.Value(setPass), triangles = PerformanceCounters.Value(triangles), gcBytes = PerformanceCounters.Value(gc), usedBytes = cachedUsed, reservedBytes = cachedReserved, monoBytes = cachedMono, ships = NetworkShip.ActiveShips.Count, crewShips = cachedCrewShips, players = NetworkPlayer.Active.Count, debris = cachedDebris, effects = cachedEffects, physicsSteps = fixedSteps, secondaryPhysicsSteps = secondarySteps, networkSteps = networkSteps };
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            frame.upperPhysicalDebris = PirateSlop.Experimental.UpperDebrisPresentation.ActivePhysicalCount;
            foreach (var upper in PirateSlop.Experimental.UpperDestructionV2.Active)
                if (upper != null && upper.isActiveAndEnabled && upper.TryGetComponent<PirateSlop.Experimental.UpperDebrisPresentation>(out var debris)) frame.upperCosmeticDebris += debris.ActiveCosmeticCount;
#endif
            frame.frustumCrewShips = cachedFrustumShips; frame.frustumPlayers = cachedFrustumPlayers;
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].frameStartTimestamp != lastTimingTimestamp)
            {
                lastTimingTimestamp = timings[0].frameStartTimestamp;
                if (frame.cpuMs < 0 && timings[0].cpuMainThreadFrameTime > 0) frame.cpuMs = (float)timings[0].cpuMainThreadFrameTime;
                if (frame.renderMs < 0 && timings[0].cpuRenderThreadFrameTime > 0) frame.renderMs = (float)timings[0].cpuRenderThreadFrameTime;
                if (frame.gpuMs < 0 && timings[0].gpuFrameTime > 0) frame.gpuMs = (float)timings[0].gpuFrameTime;
            }
            if (frame.draws < 0)
            {
                long sum = 0; bool available = false;
                foreach (var recorder in draws) { long v = PerformanceCounters.Value(recorder); if (v >= 0) { sum += v; available = true; } }
                if (available) frame.draws = sum;
            }
            if (View != null)
            {
                frame.cameraAvailable = true;
                var p = View.transform.position; frame.x = p.x; frame.y = p.y; frame.z = p.z;
                var q = View.transform.rotation; frame.qx = q.x; frame.qy = q.y; frame.qz = q.z; frame.qw = q.w;
                var storm = StormVolumeController.Instance;
                frame.stormReady = storm != null && storm.Ready;
                if (frame.stormReady) { frame.stormRadius = storm.CurrentRadius; frame.stormDistance = storm.CurrentRadius - Vector2.Distance(new Vector2(p.x, p.z), new Vector2(storm.CurrentCenter.x, storm.CurrentCenter.z)); }
                var rain = StormRainController.Instance;
                frame.rainReady = rain != null && rain.isActiveAndEnabled && rain.RendersCamera(View);
                if (frame.rainReady) { frame.rainIntensity = rain.Intensity; frame.submerged = rain.Submerged; }
            }
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            frame.gc0 = gc0 - lastGC0; frame.gc1 = gc1 - lastGC1; frame.gc2 = gc2 - lastGC2;
            lastGC0 = gc0; lastGC1 = gc1; lastGC2 = gc2;
            fixedSteps = secondarySteps = networkSteps = 0;
            if (frame.segment == 5 && frame.rainReady && frame.submerged) SubmergedSeconds += delta;
            frames[count] = frame;
            PerformanceCpuTimings.CopyFrame(frame.unityFrame, cpuStageValues, count * PerformanceCpuTimings.Names.Length);
            for (int i = 0; i < markers.Length; i++) markerValues[count * markers.Length + i] = Ms(markers[i], false);
            count++;
        }

        static float Ms(ProfilerRecorder recorder, bool positive = true)
        {
            long value = PerformanceCounters.Value(recorder);
            return value < 0 || positive && value == 0 ? -1 : value / 1000000f;
        }

        public PerformanceLiveSnapshot ReadLiveSnapshot()
        {
            var result = new PerformanceLiveSnapshot { cpuMs = -1, renderMs = -1, gpuMs = -1, p99Ms = -1 };
            if (!Recording || count == 0)
            {
                result.latest = new PerformanceFrame { cpuMs = Ms(cpu), renderMs = Ms(render), gpuMs = Ms(gpu), draws = PerformanceCounters.Value(drawsTotal), setPass = PerformanceCounters.Value(setPass), triangles = PerformanceCounters.Value(triangles), gcBytes = PerformanceCounters.Value(gc), usedBytes = Profiler.GetTotalAllocatedMemoryLong(), crewShips = NetworkShip.ActiveShips.Count, players = NetworkPlayer.Active.Count };
                result.cpuMs = result.latest.cpuMs; result.renderMs = result.latest.renderMs; result.gpuMs = result.latest.gpuMs;
                return result;
            }
            result.latest = frames[count - 1];
            double total = 0, cpuTotal = 0, renderTotal = 0, gpuTotal = 0;
            int cpuCount = 0, renderCount = 0, gpuCount = 0;
            for (int i = count - 1; i >= 0 && result.frames < liveScratch.Length; i--)
            {
                var frame = frames[i];
                if (frame.segment != result.latest.segment || result.latest.time - frame.time >= 1) break;
                if (!float.IsFinite(frame.frameMs) || frame.frameMs <= 0) continue;
                liveScratch[result.frames++] = frame.frameMs;
                total += frame.frameMs;
                result.maxMs = Math.Max(result.maxMs, frame.frameMs);
                if (frame.frameMs > 1000d / 60) result.over1667++;
                if (frame.frameMs > 1000d / 30) result.over3333++;
                if (frame.cpuMs >= 0) { cpuTotal += frame.cpuMs; cpuCount++; }
                if (frame.renderMs >= 0) { renderTotal += frame.renderMs; renderCount++; }
                if (frame.gpuMs >= 0) { gpuTotal += frame.gpuMs; gpuCount++; }
            }
            result.fps = total > 0 ? result.frames * 1000d / total : 0;
            result.cpuMs = cpuCount > 0 ? cpuTotal / cpuCount : -1;
            result.renderMs = renderCount > 0 ? renderTotal / renderCount : -1;
            result.gpuMs = gpuCount > 0 ? gpuTotal / gpuCount : -1;
            if (result.frames >= 20)
            {
                Array.Sort(liveScratch, 0, result.frames);
                double rank = (result.frames - 1) * .99;
                int lo = (int)rank, hi = Math.Min(lo + 1, result.frames - 1);
                result.p99Ms = liveScratch[lo] + (liveScratch[hi] - liveScratch[lo]) * (rank - lo);
            }
            return result;
        }

        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors++;
            else if (type == LogType.Warning) warnings++;
            else return;
            if (events.Count >= EventCapacity) { droppedEvents++; return; }
            Event(type.ToString());
            if (events.Count > 0) { int i = events.Count - 1; var e = events[i]; e.target = message.Length > 1200 ? message.Substring(0, 1200) : message; events[i] = e; }
        }

        public void Finish(string status, string reason, string[] segmentNames)
        {
            if (Active != this) return;
            SampleShips(true);
            Event("RecordingStopped"); Recording = false;
            var recorded = new PerformanceFrame[count]; Array.Copy(frames, recorded, count);
            var recordedMarkers = new float[count * markerNames.Length]; Array.Copy(markerValues, recordedMarkers, recordedMarkers.Length);
            var recordedCpu = new float[count * PerformanceCpuTimings.Names.Length]; Array.Copy(cpuStageValues, recordedCpu, recordedCpu.Length);
            var report = new PerformanceReport { combat = CombatScenario?.Summary, status = status, reason = reason, metadata = metadata, markerNames = markerNames, markerMilliseconds = recordedMarkers, cpuStageNames = PerformanceCpuTimings.Names, cpuStageMilliseconds = recordedCpu, frames = recorded, events = events.ToArray(), errors = Errors, warnings = warnings, droppedEvents = droppedEvents, captureBufferBytes = (long)Capacity * System.Runtime.InteropServices.Marshal.SizeOf<PerformanceFrame>() + ((long)markerValues.Length + cpuStageValues.Length) * sizeof(float) };
            if (status == "COMPLETE" && Errors > 0) { report.status = "INVALID"; report.reason = "Errors occurred during the capture."; }
            report.shipSamples = shipSamples.ToArray(); report.droppedShipSamples = droppedShipSamples;
            report.captureBufferBytes += (long)shipSamples.Capacity * System.Runtime.InteropServices.Marshal.SizeOf<PerformanceShipSample>();
            string directory = Path.Combine(Application.persistentDataPath, "PerformanceReports");
            string basename = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + metadata.mode;
            Release(); Saving = true; LastResult = "Сохранение отчёта…";
            savingTask = Task.Run(() =>
            {
                report.total = PerformanceStatistics.Calculate(recorded, -1, "TOTAL");
                report.segments = new PerformanceSummary[segmentNames.Length];
                for (int i = 0; i < segmentNames.Length; i++) report.segments[i] = PerformanceStatistics.Calculate(recorded, i, segmentNames[i]);
                report.seconds = PerformanceStatistics.Timeline(recorded, segmentNames);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, basename + ".json");
                string json = JsonUtility.ToJson(report, false);
                string temporary = path + ".tmp"; File.WriteAllText(temporary, json, new UTF8Encoding(false)); File.Move(temporary, path);
                File.WriteAllText(Path.Combine(directory, basename + ".txt"), PerformanceStatistics.Text(report), new UTF8Encoding(false));
                return new SavedReport { Path = path, Result = report.status + (metadata.mode == "isolation" ? " · Разбор нагрузки: результаты по участкам\n" : " · FPS " + report.total.averageFps.ToString("F1", CultureInfo.InvariantCulture) + " · P99 " + report.total.p99Ms.ToString("F2", CultureInfo.InvariantCulture) + " мс\n") + (string.IsNullOrEmpty(report.reason) ? Path.GetFileName(path) : report.reason) };
            });
        }

        void Release()
        {
            Application.logMessageReceived -= Log;
            if (manager != null) manager.TimeManager.OnPostTick -= NetworkStep;
            cpu.Dispose(); render.Dispose(); gpu.Dispose(); drawsTotal.Dispose(); triangles.Dispose(); setPass.Dispose(); gc.Dispose();
            if (draws != null) foreach (var r in draws) r.Dispose();
            if (markers != null) foreach (var r in markers) r.Dispose();
            PerformanceCpuTimings.Stop();
            frames = null; markerValues = null; cpuStageValues = null;
            if (Active == this) Active = null;
            Recording = false;
            CombatScenario = null;
        }

        void OnApplicationQuit()
        {
            if (Active == this) Finish("CANCELLED", "Application is exiting.", new[] { "Interrupted capture" });
            if (savingTask != null) { try { savingTask.Wait(2000); } catch { } }
        }

        void OnDestroy() { if (Active == this) Release(); }
    }
}
