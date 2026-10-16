using System;
using System.Security.Cryptography;
using System.Text;
using PirateSlop.Networking;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.Performance
{
    [Serializable]
    public sealed class BenchmarkWaypoint
    {
        public float time;
        public Vector3 position, target;
    }

    [Serializable]
    public sealed class BenchmarkSegment
    {
        public string name, anchor;
        public float duration;
        public BenchmarkWaypoint[] waypoints;
    }

    [Serializable]
    public sealed class BenchmarkProfile
    {
        public string version;
        public string scenario;
        public bool IsShipIsolation => scenario == "ship-isolation";
        public bool IsIsolation => scenario == "fleet-isolation" || IsShipIsolation;
        public string[] IsolationStages => IsShipIsolation ? PerformanceIsolation.ShipStages : PerformanceIsolation.Stages;
        public bool IsFleet => scenario == "fleet" || IsIsolation;
        public float warmup = 30, fov = 80, stormRadius = 3000;
        public int ships = 4, players = 12;
        public Vector3 fleetOffset = new(-250, 0, 240);
        public float fleetYaw = 270, fleetSpacing = 65, fleetSail = .2f;
        public BenchmarkSegment[] segments;

        public float Duration { get { float value = 0; foreach (var s in segments) value += s.duration; return value; } }

        public void Validate()
        {
            if (!float.IsFinite(fleetOffset.sqrMagnitude) || !float.IsFinite(fleetYaw) || !float.IsFinite(fleetSpacing) || fleetSpacing < (IsFleet ? 40 : 60) || !float.IsFinite(fleetSail) || fleetSail < 0 || fleetSail > .35f) throw new InvalidOperationException("Invalid benchmark fleet profile.");
            bool populationValid = IsFleet ? (ships == 1 || ships == 2 || ships == 4 || ships == 10) && players == ships * 3 : ships == 4 && players == 12;
            if (string.IsNullOrWhiteSpace(version) || !populationValid || !float.IsFinite(warmup) || warmup < (IsIsolation ? 20 : 30) || !float.IsFinite(fov) || fov < 40 || fov > 100 || !float.IsFinite(stormRadius) || stormRadius <= 0 || segments == null || segments.Length != (IsIsolation ? IsolationStages.Length : IsFleet ? 4 : 6)) throw new InvalidOperationException("Invalid benchmark profile.");
            foreach (var s in segments)
            {
                if (s == null || string.IsNullOrEmpty(s.name) || !float.IsFinite(s.duration) || s.duration <= 0 || s.anchor != "ship" && s.anchor != "world" && s.anchor != "whirlpool" && s.anchor != "storm" && !(IsFleet && s.anchor == "fleet") || s.waypoints == null || s.waypoints.Length < 2) throw new InvalidOperationException("Invalid benchmark segment.");
                float last = -1;
                foreach (var w in s.waypoints)
                {
                    if (w.time <= last || !float.IsFinite(w.time) || !float.IsFinite(w.position.sqrMagnitude) || !float.IsFinite(w.target.sqrMagnitude) || (w.position - w.target).sqrMagnitude < .01f) throw new InvalidOperationException("Invalid benchmark waypoint.");
                    last = w.time;
                }
                if (s.waypoints[0].time != 0 || Mathf.Abs(last - s.duration) > .001f) throw new InvalidOperationException("Benchmark waypoints must cover each segment.");
            }
            if (Mathf.Abs(Duration - (IsIsolation ? IsolationStages.Length * 6 : IsFleet ? 120 : 240)) > .001f) throw new InvalidOperationException("Unexpected benchmark duration.");
        }
    }

    [DefaultExecutionOrder(900)]
    public sealed class PerformanceBenchmarkController : MonoBehaviour
    {
        public static bool Running => SessionController.Instance != null && SessionController.Instance.PerformanceBenchmarkActive;
        public static bool HideTestLabels => Running;
        public double EnvironmentTime => readyAt > 0 ? Time.realtimeSinceStartupAsDouble - readyAt : 0;
        public bool MotionEnabled => capture != null && capture.Recording;
        public float FleetSail => profile.fleetSail;
        public void FleetSpawn(Vector3 origin, int slot, out Vector3 position, out float yaw)
        {
            yaw = profile.fleetYaw;
            position = origin + profile.fleetOffset + Quaternion.Euler(0, yaw, 0) * Vector3.left * (profile.fleetSpacing * slot);
        }
        public bool ActiveRun { get; private set; }
        public string Progress { get; private set; }
        SessionController session;
        PerformanceCapture capture;
        BenchmarkProfile profile;
        PerformanceMetadata metadata;
        PerformanceIsolation isolation;
        Camera view;
        NetworkShip primary;
        string[] segmentNames;
        double startedAt, readyAt, recordingAt;
        int activeSegment;
        int frozenWidth, frozenHeight, frozenVSync, frozenLimit;
        float frozenScale;
        int frozenQuality;
        string frozenCloudQuality;
        double nextProgress;
        GUIStyle progressStyle;
        static int processRuns;

        public void Begin(SessionController owner, BenchmarkProfile route, string source)
        {
            session = owner; profile = route;
            capture = GetComponent<PerformanceCapture>();
            if (capture == null) capture = gameObject.AddComponent<PerformanceCapture>();
            metadata = CreateMetadata(route.IsIsolation ? "isolation" : "benchmark");
            metadata.routeVersion = profile.version;
            using (var hash = SHA256.Create()) metadata.routeHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "");
            metadata.expectedShips = profile.ships; metadata.expectedPlayers = profile.players;
            metadata.warmupSeconds = profile.warmup; metadata.requestedSeconds = profile.Duration;
            metadata.warmState = processRuns++ == 0 ? "first-benchmark-in-process; asset caches may already be warm" : "repeat-benchmark-in-process";
            segmentNames = new string[profile.segments.Length];
            for (int i = 0; i < segmentNames.Length; i++) segmentNames[i] = profile.segments[i].name;
            capture.Prepare(metadata);
            startedAt = Time.realtimeSinceStartupAsDouble; readyAt = recordingAt = 0;
            activeSegment = 0; ActiveRun = true; Progress = "Подготовка тестовой карты…";
        }

        public static PerformanceMetadata CreateMetadata(string mode)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return new PerformanceMetadata
            {
                utc = DateTime.UtcNow.ToString("O"), mode = mode, applicationVersion = Application.version + " / " + Application.buildGUID,
                unityVersion = Application.unityVersion, buildType = Application.isEditor ? "Editor" : Debug.isDebugBuild ? "Development" : "Release",
                editor = Application.isEditor, profilerEnabled = UnityEngine.Profiling.Profiler.enabled, cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), ramMB = SystemInfo.systemMemorySize, vramMB = SystemInfo.graphicsMemorySize,
                width = Screen.width, height = Screen.height, vSync = QualitySettings.vSyncCount, fpsLimit = Application.targetFrameRate,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], renderScale = pipeline != null ? pipeline.renderScale : 1,
                pipeline = pipeline != null ? pipeline.name : "N/A", telemetryVisible = GameTelemetry.Visible, role = "local-host / synthetic participants; no remote connections",
                environmentSignature = EnvironmentSignature(Camera.main), hullSweepFastPath = ShipController.HullSweepFastPathEnabled,
                fleetScenario = mode == "benchmark" ? SessionController.Instance != null && SessionController.Instance.BenchmarkFleetShipsActive > 0 ? "fleet-scaling-v1;ships=" + SessionController.Instance.BenchmarkFleetShipsActive : SessionController.Instance != null && SessionController.Instance.BenchmarkWhirlpoolStressActive ? "whirlpool-convergence-stress" : "parallel-open-water-v1" : "user-controlled",
                renderExperiment = mode == "benchmark" ? PerformanceRenderOverrides.Signature : "",
                shipBodyPoseFastPath = PirateSlop.Ships.ShipBodyPose.Enabled, worldVisualBudget = WorldVisualBudget.Enabled,
                playerGuiLayoutBudget = PlayerGuiBudget.Enabled, cpuOptimizationVersion = "cpu-work-v1"
            };
        }

        static ScriptableRendererData RendererData(Camera camera)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null || camera == null) return null;
            var selected = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            var data = pipeline.rendererDataList;
            for (int i = 0; i < data.Length; i++) if (data[i] != null && pipeline.GetRenderer(i) == selected) return data[i];
            return null;
        }

        public static string EnvironmentSignature(Camera camera)
        {
            var text = new StringBuilder(512);
            var renderer = RendererData(camera);
            text.Append("Renderer=" + (renderer != null ? renderer.name : "N/A"));
            if (renderer != null)
                foreach (var feature in renderer.rendererFeatures)
                    if (feature != null) text.Append("; " + feature.GetType().Name + ":" + feature.name + "=" + feature.isActive);
            var sky = TestSkyDayNight.Active;
            if (sky != null && sky.Environment != null && sky.Environment.profile.TryGet<VolumetricClouds>(out var clouds))
                text.Append(FormattableString.Invariant($"; SkyClouds={sky.Environment.enabled}/{clouds.active}/{clouds.state.value}; CloudSteps={clouds.numPrimarySteps.value}/{clouds.numLightSteps.value}; CloudDensity={clouds.densityMultiplier.value}; CloudShadows={clouds.shadows.value}; DayTarget={sky.TargetBlend}"));
            var storm = StormVolumeController.Instance;
            if (storm != null)
            {
                text.Append(FormattableString.Invariant($"; Storm={storm.isActiveAndEnabled}/{storm.EnableStormVolume}; TestSmoke={storm.TestCloudWall}; Freeze={storm.FreezeZoneValues}; ManualRadius={storm.ManualRadius}; StormSteps={storm.CloudSettings?.numPrimarySteps.value}/{storm.CloudSettings?.numLightSteps.value}; Wind={storm.WindSpeed}"));
                var weather = storm.GetComponent<StormWeatherController>();
                if (weather != null) text.Append(FormattableString.Invariant($"; Lightning={weather.isActiveAndEnabled}/{weather.LightningInterval}/{weather.BoltChance}/{weather.LightningIntensity}"));
            }
            var rain = StormRainController.Instance;
            text.Append("; Rain=" + (rain != null && rain.isActiveAndEnabled && rain.RainMaterial != null && rain.ImpactMaterial != null));
            text.Append("; CloudQuality=" + PerformanceRenderOverrides.QualitySignature);
            if (!string.IsNullOrEmpty(PerformanceRenderOverrides.Signature)) text.Append("; RenderExperiment=" + PerformanceRenderOverrides.Signature);
            return text.ToString();
        }

        string WeatherFailure()
        {
            var sky = TestSkyDayNight.Active;
            var storm = StormVolumeController.Instance;
            if (StormZone.Instance == null || !StormZone.Instance.Paused || storm == null || !storm.Ready || !storm.TestCloudWall || storm.FreezeZoneValues) return "Expected paused storm and test smoke are missing or overridden.";
            if (Mathf.Abs(StormZone.Instance.Radius - profile.stormRadius) > 1) return "Storm radius differs from benchmark profile.";
            if (sky == null || !sky.isActiveAndEnabled || sky.Environment == null || !sky.Environment.enabled || !sky.Environment.profile.TryGet<VolumetricClouds>(out var clouds) || !clouds.active || !clouds.state.value) return "Expected sky clouds are disabled.";
            if (GraphicsSettings.currentRenderPipeline != sky.Pipeline) return "Expected test sky pipeline is not selected.";
            var rain = StormRainController.Instance;
            var lightning = storm.GetComponent<StormWeatherController>();
            if (rain == null || !rain.isActiveAndEnabled || rain.RainMaterial == null || rain.ImpactMaterial == null || lightning == null || !lightning.isActiveAndEnabled || lightning.BoltMaterial == null || lightning.BoltChance <= 0 || lightning.LightningIntensity <= 0) return "Expected rain or lightning is missing or disabled.";
            var renderer = RendererData(view);
            bool cloudPass = false, smokePass = false, rainPass = false, underwaterPass = false;
            if (renderer != null)
                foreach (var feature in renderer.rendererFeatures)
                {
                    if (feature == null || !feature.isActive) continue;
                    cloudPass |= feature is VolumetricCloudsURP;
                    smokePass |= feature is StormVolumeRendererFeature;
                    rainPass |= feature is StormRainRendererFeature;
                    underwaterPass |= feature is UnderwaterRendererFeature;
                }
            return cloudPass && smokePass && rainPass && underwaterPass ? null : "Expected clouds, smoke, rain or underwater renderer feature is disabled.";
        }

        void LateUpdate()
        {
            if (!ActiveRun) return;
            if (!string.IsNullOrEmpty(session.BenchmarkFailure)) { Finish("INVALID", session.BenchmarkFailure); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Finish("CANCELLED", "Cancelled by user."); return; }
            if (capture.Errors > 0) { Finish("INVALID", "Errors occurred during benchmark setup or capture."); return; }
            if (capture.Exhausted) { Finish("INVALID", "Capture capacity exceeded."); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            if (readyAt == 0)
            {
                if (now - startedAt > 180) { Finish("INVALID", "World, participants or weather did not become ready in 180 seconds."); return; }
                if (!session.BenchmarkReady || Camera.main == null || TestSkyDayNight.Active == null || StormVolumeController.Instance == null || !StormVolumeController.Instance.Ready) return;
                view = Camera.main; capture.View = view;
                foreach (var ship in NetworkShip.ActiveShips)
                    if (ship != null && ship.TeamId.Value == 1) { primary = ship; break; }
                if (primary == null) { Finish("INVALID", "Primary ship is missing."); return; }
                if (!session.BenchmarkEventsReady) return;
                var world = ProceduralWorld.Instance;
                metadata.mapChecksum = world.Checksum;
                foreach (var p in world.Layout.Points) if (p.Tag == EnvironmentTestGallery.Marker) metadata.mapRevision = p.Id;
                TestSkyDayNight.Active.TargetBlend = 0;
                if (OceanSurface.Instance.HeightSource is BoatAttackOcean ocean)
                {
                    ocean.ApplyWaveControls(1, 1, 1, OceanSurface.Instance.WaveTime, 0);
                    metadata.waveStrength = ocean.WaveStrength; metadata.waveSteepness = ocean.WaveSteepness; metadata.waveSpeed = ocean.WaveSpeed;
                }
                session.ConfigureBenchmarkStorm(profile.stormRadius);
                session.ApplyBenchmarkFramePacing();
                if (profile.IsIsolation)
                {
                    isolation = new PerformanceIsolation(view, GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset, profile.IsShipIsolation);
                    metadata.telemetryVisible = GameTelemetry.Visible;
                    metadata.fleetScenario = (profile.IsShipIsolation ? "stationary-ship-isolation-v1;ships=" : "stationary-fleet-isolation-v1;ships=") + profile.ships;
                }
                readyAt = now;
                view.fieldOfView = profile.fov;
                PerformanceCapture.Event("WorldReady", primary);
            }
            if (view == null) { Finish("INVALID", "Benchmark camera is missing."); return; }
            if (primary == null) { Finish("INVALID", "Primary benchmark ship is missing."); return; }
            if (primary.IsSinking) { Finish("INVALID", "Primary benchmark ship started sinking."); return; }
            if (!session.BenchmarkReady) { Finish("INVALID", "Benchmark population changed: " + session.LoadTestPopulationStatus); return; }
            if (!session.BenchmarkWhirlpoolStressActive)
                foreach (var ship in NetworkShip.ActiveShips)
                {
                    if (ship == null || ship.TeamId.Value <= 0) continue;
                    var ocean = OceanSurface.Instance;
                    if (ocean != null && ocean.WhirlpoolDepth > 0 && Vector2.Distance(new Vector2(ship.transform.position.x, ship.transform.position.z), new Vector2(ocean.WhirlpoolCenter.x, ocean.WhirlpoolCenter.z)) < ocean.WhirlpoolRadius)
                    { Finish("INVALID", "Open-water fleet entered whirlpool; team " + ship.TeamId.Value); return; }
                    var flood = ship.GetComponent<PirateSlop.ShipFlooding>();
                    if (flood != null && flood.Level > .01f)
                    { Finish("INVALID", "Open-water fleet unexpectedly flooded; team " + ship.TeamId.Value); return; }
                }
            if (recordingAt == 0)
            {
                double remaining = profile.warmup - (now - readyAt);
                Progress = "Прогрев · " + Mathf.CeilToInt((float)Math.Max(0, remaining)) + $" с · {profile.ships} кораблей / {profile.players} персонажей";
                Position(0, 0);
                if (remaining > 0) return;
                session.ApplyBenchmarkFramePacing();
                string weatherFailure = WeatherFailure();
                if (weatherFailure != null) { Finish("INVALID", weatherFailure); return; }
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                metadata.pipeline = pipeline != null ? pipeline.name : "N/A"; metadata.renderScale = pipeline != null ? pipeline.renderScale : 1;
                metadata.fov = view.fieldOfView; metadata.tickRate = session.Config.TickRate; metadata.stormRadius = profile.stormRadius;
                metadata.nearClip = view.nearClipPlane; metadata.farClip = view.farClipPlane;
                metadata.antialiasing = view.GetUniversalAdditionalCameraData().antialiasing.ToString();
                metadata.environmentSignature = EnvironmentSignature(view);
                metadata.profilerEnabled = UnityEngine.Profiling.Profiler.enabled;
                frozenWidth = Screen.width; frozenHeight = Screen.height; frozenVSync = QualitySettings.vSyncCount; frozenLimit = Application.targetFrameRate;
                frozenScale = metadata.renderScale; frozenQuality = QualitySettings.GetQualityLevel();
                metadata.width = frozenWidth; metadata.height = frozenHeight; metadata.vSync = frozenVSync; metadata.fpsLimit = frozenLimit;
                metadata.quality = QualitySettings.names[frozenQuality];
                frozenCloudQuality = PerformanceRenderOverrides.QualitySignature;
                capture.StartRecording(); recordingAt = now;
            }
            var currentPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (WorldVisualBudget.Enabled != metadata.worldVisualBudget) { Finish("INVALID", "World visual budget changed during capture."); return; }
            if (PlayerGuiBudget.Enabled != metadata.playerGuiLayoutBudget) { Finish("INVALID", "Player GUI layout budget changed during capture."); return; }
            if (ShipController.HullSweepFastPathEnabled != metadata.hullSweepFastPath) { Finish("INVALID", "Hull collision optimization changed during capture."); return; }
            if (PirateSlop.Ships.ShipBodyPose.Enabled != metadata.shipBodyPoseFastPath) { Finish("INVALID", "Ship body pose optimization changed during capture."); return; }
            if ((metadata.renderExperiment ?? "") != PerformanceRenderOverrides.Signature) { Finish("INVALID", "Render experiment changed during capture."); return; }
            if (frozenCloudQuality != PerformanceRenderOverrides.QualitySignature) { Finish("INVALID", "Cloud quality changed during capture."); return; }
            if (!Application.isEditor && !Application.isFocused) { Finish("INVALID", "Player window lost focus during capture."); return; }
            if (Screen.width != frozenWidth || Screen.height != frozenHeight || QualitySettings.vSyncCount != frozenVSync || Application.targetFrameRate != frozenLimit || QualitySettings.GetQualityLevel() != frozenQuality || currentPipeline == null || Mathf.Abs(currentPipeline.renderScale - (isolation != null ? isolation.ExpectedScale : frozenScale)) > .0001f || Mathf.Abs(view.fieldOfView - profile.fov) > .001f)
            { Finish("INVALID", "Graphics settings changed during capture."); return; }
            double elapsed = now - recordingAt;
            if (elapsed >= profile.Duration)
            {
                isolation?.Restore();
                string weatherFailure = WeatherFailure();
                if (weatherFailure != null) { Finish("INVALID", weatherFailure); return; }
                if (EnvironmentSignature(view) != metadata.environmentSignature) { Finish("INVALID", "Weather or renderer features changed during capture."); return; }
                if (!profile.IsFleet && capture.SubmergedSeconds < 10) { Finish("INVALID", "Underwater segment did not record at least 10 submerged seconds."); return; }
                Finish("COMPLETE", ""); return;
            }
            float local = (float)elapsed; int index = 0;
            while (index < profile.segments.Length - 1 && local >= profile.segments[index].duration) local -= profile.segments[index++].duration;
            if (index != activeSegment) { activeSegment = index; PerformanceCapture.Event("Segment:" + segmentNames[index]); }
            isolation?.Apply(index);
            capture.Segment = index;
            Position(index, local);
            if (now >= nextProgress) { nextProgress = now + .5; Progress = segmentNames[index] + " · " + ((int)elapsed) + $" / {profile.Duration:0} с · Escape — отмена"; }
        }

        void Position(int index, float time)
        {
            var segment = profile.segments[index]; var points = segment.waypoints;
            int at = 0; while (at < points.Length - 2 && time > points[at + 1].time) at++;
            float t = Mathf.InverseLerp(points[at].time, points[at + 1].time, time);
            Vector3 position = Vector3.Lerp(points[at].position, points[at + 1].position, t);
            Vector3 target = Vector3.Lerp(points[at].target, points[at + 1].target, t);
            Vector3 anchor = Vector3.up * OceanSurface.Instance.SeaLevel;
            if (segment.anchor == "ship" || segment.anchor == "fleet")
            {
                if (segment.anchor == "fleet")
                {
                    var center = Vector3.left * ((profile.ships - 1) * profile.fleetSpacing * .5f);
                    float fit = Mathf.Max(1, (profile.ships - 1) * profile.fleetSpacing / 120f);
                    position = center + new Vector3(position.x * fit, position.y, position.z * fit);
                    target += center;
                    var yaw = Quaternion.Euler(0, primary.transform.eulerAngles.y, 0);
                    position = primary.transform.position + yaw * position;
                    target = primary.transform.position + yaw * target;
                }
                else { position = primary.transform.TransformPoint(position); target = primary.transform.TransformPoint(target); }
            }
            else
            {
                if (segment.anchor == "whirlpool") anchor = EnvironmentTestGallery.WhirlpoolCenter(ProceduralWorld.Instance.Layout);
                else if (segment.anchor == "storm") anchor = StormVolumeController.Instance.CurrentCenter + new Vector3(profile.stormRadius, OceanSurface.Instance.SeaLevel - StormVolumeController.Instance.CurrentCenter.y, 0);
                position += anchor; target += anchor;
            }
            view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
        }

        public void Finish(string status, string reason)
        {
            if (!ActiveRun) return;
            ActiveRun = false;
            isolation?.Dispose(); isolation = null;
            capture.Finish(status, reason, segmentNames);
            session.EndPerformanceBenchmark();
        }

        void OnDisable() { isolation?.Dispose(); isolation = null; }

        void OnGUI()
        {
            if (!ActiveRun || Event.current.type != EventType.Repaint) return;
            progressStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUI.Box(new Rect(12, 12, Mathf.Min(700, Screen.width - 24), 38), Progress, progressStyle);
        }
    }
}
