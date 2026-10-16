using System;
using System.Collections;
using System.IO;
using PirateSlop.Performance;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        public bool PerformanceBenchmarkActive { get; private set; }
        public bool BenchmarkWhirlpoolStressActive { get; private set; }
        public int BenchmarkFleetShipsActive { get; private set; }
        int benchmarkFleetShipsSelected;
        bool benchmarkWhirlpoolStressSelected;
        bool benchmarkIsolationSelected, benchmarkShipIsolationSelected;
        int diagnosticFleetShips = 4;
        bool benchmarkUncappedRequested, benchmarkPacingApplied;
        int benchmarkPreviousFrameLimit, benchmarkPreviousVSync;
        public bool BenchmarkReady => PerformanceBenchmarkActive && playing && manager != null && manager.ServerManager.Started && ProceduralWorld.Instance != null && ProceduralWorld.Instance.Ready && EnvironmentTestActive && LoadTestPopulationReady;
        public string BenchmarkFailure => error;
        PerformanceBenchmarkController performanceBenchmark;
        int benchmarkPreviousCapacity;
        bool benchmarkPreviousObserver;
        UnityEngine.Random.State benchmarkPreviousRandom;
        public float BenchmarkSimulationTime => performanceBenchmark != null ? (float)performanceBenchmark.EnvironmentTime : 0;
        public bool BenchmarkMotionEnabled => performanceBenchmark != null && performanceBenchmark.MotionEnabled;
        public float BenchmarkSail => performanceBenchmark != null ? performanceBenchmark.FleetSail : 0;

        IEnumerator RunPerformanceCommandLine(string[] args)
        {
            yield return new WaitForSecondsRealtime(2);
            benchmarkWhirlpoolStressSelected = Has(args, "-performanceStress");
            benchmarkShipIsolationSelected = Has(args, "-performanceShipIsolation");
            benchmarkIsolationSelected = benchmarkShipIsolationSelected || Has(args, "-performanceIsolation");
            try { PerformanceRenderOverrides.Configure(Value(args, "-performanceStormScale"), Value(args, "-performanceSkyScale")); }
            catch (Exception ex) { Debug.LogError(ex.Message); if (Has(args, "-performanceQuit")) Application.Quit(2); yield break; }
            benchmarkUncappedRequested = Has(args, "-performanceUncapped");
            ShipController.HullSweepFastPathEnabled = !Has(args, "-performanceLegacyHull");
            PirateSlop.Ships.ShipBodyPose.Enabled = false;
            WorldVisualBudget.Enabled = !Has(args, "-performanceLegacyWorldVisuals");
            string fleetArgument = Value(args, "-performanceFleetShips") ?? (benchmarkIsolationSelected ? "4" : null);
            if (fleetArgument != null)
            {
                if (!int.TryParse(fleetArgument, out int fleetShips) || fleetShips != 1 && fleetShips != 2 && fleetShips != 4 && fleetShips != 10)
                { Debug.LogError("performanceFleetShips must be 1, 2, 4 or 10."); if (Has(args, "-performanceQuit")) Application.Quit(2); yield break; }
                BeginFleetPerformanceBenchmark(fleetShips);
            }
            else BeginPerformanceBenchmark();
            while (PerformanceBenchmarkActive || PerformanceCapture.Saving) yield return null;
            Debug.Log("PERFORMANCE_AUTORUN " + PerformanceCapture.LastResult + " path=" + PerformanceCapture.LastReportPath);
            if (Has(args, "-performanceQuit")) Application.Quit(PerformanceCapture.LastResult.StartsWith("COMPLETE", StringComparison.Ordinal) ? 0 : 2);
        }

        public bool BenchmarkEventsReady
        {
            get
            {
                bool capture = false, raft = false, sunken = false, shark = false;
                foreach (var chest in NetworkLootChest.ServerChests)
                {
                    if (chest == null || !chest.IsSpawned) continue;
                    capture |= chest.Kind == SeaLootKind.Capture; raft |= chest.Kind == SeaLootKind.Raft;
                    sunken |= chest.Kind == SeaLootKind.Sunken; shark |= chest.Kind == SeaLootKind.Shark;
                }
                return capture && raft && sunken && shark && NetworkSkullEvent.ServerEvents.Count > 0 && WhaleLootPoint.Active.Count > 0 && NetworkClamEvent.ServerEvents.Count > 0 && NetworkBalanceEvent.ServerEvents.Count > 0 && NetworkRiggingEvent.ServerEvents.Count > 0 && NetworkSternCargoEvent.Active.Count > 0 && NetworkCannonBreachEvent.ServerEvents.Count > 0 && NetworkSeagullEvent.ServerEvents.Count > 0 && NetworkPlatformingEvent.ServerEvents.Count > 0;
            }
        }

        public void ApplyBenchmarkFramePacing()
        {
            if (!benchmarkUncappedRequested) return;
            if (!benchmarkPacingApplied) { benchmarkPreviousFrameLimit = Application.targetFrameRate; benchmarkPreviousVSync = QualitySettings.vSyncCount; }
            benchmarkPacingApplied = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        }

        public void BeginFleetPerformanceBenchmark(int ships = 4)
        {
            if (ships != 1 && ships != 2 && ships != 4 && ships != 10) throw new ArgumentOutOfRangeException(nameof(ships));
            benchmarkFleetShipsSelected = ships;
            try { BeginPerformanceBenchmark(); }
            finally { benchmarkFleetShipsSelected = 0; }
        }

        public void BeginPerformanceBenchmark()
        {
            if (SessionBusy || PerformanceCapture.Active != null || PerformanceCapture.Saving) return;
            var source = Resources.Load<TextAsset>(benchmarkFleetShipsSelected > 0 ? "Performance/FleetBenchmarkRoute" : "Performance/BenchmarkRoute");
            BenchmarkProfile profile;
            try
            {
                if (source == null) throw new InvalidOperationException("Benchmark route is missing.");
                profile = JsonUtility.FromJson<BenchmarkProfile>(source.text);
                if (benchmarkFleetShipsSelected > 0) { profile.ships = benchmarkFleetShipsSelected; profile.players = profile.ships * CrewSize; profile.version += "-n" + profile.ships; }
                if (benchmarkIsolationSelected)
                {
                    if (benchmarkWhirlpoolStressSelected || benchmarkFleetShipsSelected == 0) throw new InvalidOperationException("Isolation requires an open-water fleet.");
                    profile.version = (benchmarkShipIsolationSelected ? "ship-isolation-v1-n" : "fleet-isolation-v1-n") + profile.ships;
                    profile.scenario = benchmarkShipIsolationSelected ? "ship-isolation" : "fleet-isolation"; profile.warmup = 20; profile.fleetSail = 0;
                    var point = profile.segments[0].waypoints[0];
                    profile.segments = new BenchmarkSegment[profile.IsolationStages.Length];
                    for (int i = 0; i < profile.segments.Length; i++)
                        profile.segments[i] = new BenchmarkSegment { name = profile.IsolationStages[i], anchor = "fleet", duration = 6,
                            waypoints = new[] { new BenchmarkWaypoint { time = 0, position = point.position, target = point.target }, new BenchmarkWaypoint { time = 6, position = point.position, target = point.target } } };
                }
                profile.Validate();
            }
            catch (Exception ex) { SetError(ex.Message); return; }
            string routeSource = source.text;
            BenchmarkFleetShipsActive = benchmarkFleetShipsSelected;
            if (BenchmarkFleetShipsActive > 0 || profile.IsIsolation) routeSource = JsonUtility.ToJson(profile);
            BenchmarkWhirlpoolStressActive = benchmarkFleetShipsSelected == 0 && benchmarkWhirlpoolStressSelected;
            if (BenchmarkWhirlpoolStressActive)
            {
                profile.version += "-whirlpool-stress";
                routeSource = JsonUtility.ToJson(profile);
            }
            benchmarkPreviousCapacity = MaxPlayers; benchmarkPreviousRandom = UnityEngine.Random.state;
            benchmarkPreviousObserver = observerSelected;
            previousLoadTestBots = fillWithBots; previousLoadTestSeed = seedInput;
            PerformanceBenchmarkActive = true; LoadTestActive = true; LoadTestMoving = true;
            LoadTestPopulationReady = false; loadTestFillFinished = false;
            reportedLoadTestPlayers = reportedLoadTestShips = -1; nextLoadTestMotion = 0;
            MaxPlayers = profile.players; fillWithBots = true; observerSelected = true; chooseObserver = false; seedInput = "41719";
            UnityEngine.Random.InitState(41719);
            performanceBenchmark = GetComponent<PerformanceBenchmarkController>();
            if (performanceBenchmark == null) performanceBenchmark = gameObject.AddComponent<PerformanceBenchmarkController>();
            try { performanceBenchmark.Begin(this, profile, routeSource); Begin(true, "127.0.0.1:" + Config.Port, true); }
            catch (Exception ex) { performanceBenchmark.Finish("INVALID", ex.Message); if (PerformanceBenchmarkActive) EndPerformanceBenchmark(); SetError(ex.Message); }
        }

        public void ConfigureBenchmarkStorm(float radius)
        {
            if (!PerformanceBenchmarkActive || !stormRunning) return;
            float progress = Mathf.Clamp01((stormRadius - radius) / Mathf.Max(1, stormRadius - StormZone.FinalRadius));
            stormPaused = true; stormPausedAt = stormStarted + progress * stormDuration;
            ReceiveStorm(CurrentStorm(), FishNet.Transporting.Channel.Reliable);
        }

        public void EndPerformanceBenchmark()
        {
            if (!PerformanceBenchmarkActive) return;
            Disconnect();
            if (ProceduralWorld.Instance != null) ProceduralWorld.Instance.Clear();
            PerformanceBenchmarkActive = false; benchmarkIsolationSelected = benchmarkShipIsolationSelected = false; MaxPlayers = benchmarkPreviousCapacity;
            BenchmarkWhirlpoolStressActive = false;
            BenchmarkFleetShipsActive = 0;
            PirateSlop.Ships.ShipBodyPose.Enabled = false;
            WorldVisualBudget.Enabled = true;
            if (benchmarkPacingApplied) { QualitySettings.vSyncCount = benchmarkPreviousVSync; Application.targetFrameRate = benchmarkPreviousFrameLimit; }
            benchmarkPacingApplied = benchmarkUncappedRequested = false;
            PerformanceRenderOverrides.Set(-1, -1);
            UnityEngine.Random.state = benchmarkPreviousRandom;
            observerSelected = benchmarkPreviousObserver; chooseObserver = false; menuPage = 10; error = "";
        }

        public void TogglePerformanceRecording()
        {
            if (PerformanceBenchmarkActive || PerformanceCapture.Saving) return;
            var capture = GetComponent<PerformanceCapture>();
            if (PerformanceCapture.Active != null)
            {
                PerformanceCapture.Active.Finish("COMPLETE", "Manual gameplay recording; not comparable to the automated route.", new[] { "Manual gameplay" });
                return;
            }
            if (!playing || manager == null || !manager.ClientManager.Started) return;
            if (capture == null) capture = gameObject.AddComponent<PerformanceCapture>();
            var metadata = PerformanceBenchmarkController.CreateMetadata("manual");
            metadata.role = manager.ServerManager.Started ? "host" : "client";
            metadata.tickRate = Config.TickRate; metadata.fov = Camera.main != null ? Camera.main.fieldOfView : 0;
            metadata.nearClip = Camera.main != null ? Camera.main.nearClipPlane : 0; metadata.farClip = Camera.main != null ? Camera.main.farClipPlane : 0;
            metadata.expectedShips = NetworkShip.ActiveShips.Count; metadata.expectedPlayers = NetworkPlayer.Active.Count;
            metadata.mapChecksum = ProceduralWorld.Instance != null ? ProceduralWorld.Instance.Checksum : "N/A";
            metadata.routeVersion = "manual-v1"; metadata.warmState = "user-controlled";
            capture.Prepare(metadata); capture.View = Camera.main; capture.Segment = 0; capture.StartRecording();
        }

        void DrawFleetPerformanceMenu(float x, float y, float width)
        {
            MenuText(new Rect(x, y, width, 35), "ДИАГНОСТИКА ФЛОТА");
            bool previous = GUI.enabled;
            GUI.enabled = previous && !SessionBusy && !PerformanceCapture.Saving;
            MenuText(new Rect(x + 32, y + 50, width - 40, 24), "Корабли / персонажи");
            float part = width / 3;
            if (MenuAction(x, y + 86, part, (diagnosticFleetShips == 1 ? "● " : "") + "1 / 3")) diagnosticFleetShips = 1;
            if (MenuAction(x + part, y + 86, part, (diagnosticFleetShips == 4 ? "● " : "") + "4 / 12")) diagnosticFleetShips = 4;
            if (MenuAction(x + part * 2, y + 86, part, (diagnosticFleetShips == 10 ? "● " : "") + "10 / 30")) diagnosticFleetShips = 10;
            if (MenuAction(x, y + 156, width, "Тест флота · 120 с")) BeginFleetPerformanceBenchmark(diagnosticFleetShips);
            if (MenuAction(x, y + 224, width, "Разбор нагрузки · 60 с"))
            {
                benchmarkIsolationSelected = true;
                try { BeginFleetPerformanceBenchmark(diagnosticFleetShips); }
                finally { benchmarkIsolationSelected = false; }
            }
            GUI.enabled = previous;
            if (MenuAction(x, y + 292, width, "Разбор корабля · 66 с"))
            {
                benchmarkIsolationSelected = benchmarkShipIsolationSelected = true;
                try { BeginFleetPerformanceBenchmark(diagnosticFleetShips); }
                finally { benchmarkIsolationSelected = benchmarkShipIsolationSelected = false; }
            }
            MenuText(new Rect(x + 32, y + 350, width - 40, 60), "Разбор: 20 с подготовки, затем поочерёдное\nотключение отдельных групп нагрузки.\nОтчёт содержит результаты каждого участка.", true);
            if (MenuAction(x, y + 420, width, "Назад")) menuPage = 10;
        }

        void DrawPerformanceMenu(float x, float y, float width)
        {
            MenuText(new Rect(x, y, width, 35), "ДИАГНОСТИКА");
            if (!playing)
            {
                bool previous = GUI.enabled;
                GUI.enabled = previous && !SessionBusy && !PerformanceCapture.Saving;
                if (MenuAction(x, y + 60, width, "Тест производительности", true)) BeginPerformanceBenchmark();
                GUI.enabled = previous;
                GUI.enabled = previous && !SessionBusy && !PerformanceCapture.Saving;
                if (MenuAction(x, y + 120, width, "Тесты флота")) menuPage = 11;
                GUI.enabled = previous && !SessionBusy && PerformanceCapture.Active == null && !PerformanceCapture.Saving;
                ShipController.HullSweepFastPathEnabled = GUI.Toggle(new Rect(x + 32, y + 190, width - 40, 22), ShipController.HullSweepFastPathEnabled, "Быстрая проверка пути корпуса");
                benchmarkWhirlpoolStressSelected = GUI.Toggle(new Rect(x + 32, y + 214, width - 40, 22), benchmarkWhirlpoolStressSelected, "Сближение флота в водовороте (стресс)");
                GUI.enabled = previous;
            }
            else
            {
                if (MenuAction(x, y + 60, width, PerformanceCapture.Active != null ? "Остановить запись" : "Запись игры и попаданий", true)) TogglePerformanceRecording();
                MenuText(new Rect(x + 32, y + 116, width - 40, 74), "Продолжите игру и выполните серию попаданий.\nДля сохранения вернитесь в это меню.", true);
            }
            MenuText(new Rect(x + 32, y + 242, width - 40, 65), PerformanceCapture.LastResult, true);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && PerformanceCapture.Active == null && !PerformanceCapture.Saving;
            if (MenuAction(x, y + 320, width, "Сравнить последние два теста"))
            {
                var capture = GetComponent<PerformanceCapture>();
                if (capture == null) capture = gameObject.AddComponent<PerformanceCapture>();
                capture.CompareRecentReports();
            }
            GUI.enabled = enabled;
            if (MenuAction(x, y + 380, width, "Открыть папку отчётов"))
            {
                var directory = Path.Combine(Application.persistentDataPath, "PerformanceReports"); Directory.CreateDirectory(directory);
                Application.OpenURL(new Uri(directory).AbsoluteUri);
            }
            if (MenuAction(x, y + 445, width, "Назад")) menuPage = 0;
        }
    }
}
