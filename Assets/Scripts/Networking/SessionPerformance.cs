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
        bool benchmarkWhirlpoolStressSelected;
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
            try { PerformanceRenderOverrides.Configure(Value(args, "-performanceStormScale"), Value(args, "-performanceSkyScale")); }
            catch (Exception ex) { Debug.LogError(ex.Message); if (Has(args, "-performanceQuit")) Application.Quit(2); yield break; }
            benchmarkUncappedRequested = Has(args, "-performanceUncapped");
            ShipController.HullSweepFastPathEnabled = !Has(args, "-performanceLegacyHull");
            BeginPerformanceBenchmark();
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

        public void BeginPerformanceBenchmark()
        {
            if (SessionBusy || PerformanceCapture.Active != null || PerformanceCapture.Saving) return;
            var source = Resources.Load<TextAsset>("Performance/BenchmarkRoute");
            BenchmarkProfile profile;
            try { if (source == null) throw new InvalidOperationException("BenchmarkRoute is missing."); profile = JsonUtility.FromJson<BenchmarkProfile>(source.text); profile.Validate(); }
            catch (Exception ex) { SetError(ex.Message); return; }
            string routeSource = source.text;
            BenchmarkWhirlpoolStressActive = benchmarkWhirlpoolStressSelected;
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
            PerformanceBenchmarkActive = false; MaxPlayers = benchmarkPreviousCapacity;
            BenchmarkWhirlpoolStressActive = false;
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

        void DrawPerformanceMenu(float x, float y, float width)
        {
            MenuText(new Rect(x, y, width, 35), "ДИАГНОСТИКА");
            if (!playing)
            {
                bool previous = GUI.enabled;
                GUI.enabled = previous && !SessionBusy && !PerformanceCapture.Saving;
                if (MenuAction(x, y + 60, width, "Тест производительности", true)) BeginPerformanceBenchmark();
                GUI.enabled = previous;
                MenuText(new Rect(x + 32, y + 116, width - 40, 74), "4 корабля · 12 персонажей без AI\n30 с прогрева + 4 минуты пролёта\nТекущие настройки графики", true);
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
