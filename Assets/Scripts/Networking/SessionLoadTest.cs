using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        public bool LoadTestOnStart;
        public bool LoadTestActive { get; private set; }
        public bool LoadTestMoving { get; private set; } = true;
        float nextLoadTestMotion;
        bool previousLoadTestBots;
        string previousLoadTestSeed;
        bool loadTestFillFinished;
        int reportedLoadTestPlayers = -1, reportedLoadTestShips = -1;
        GUIStyle loadTestWarningStyle;
        public bool LoadTestPopulationReady { get; private set; }
        public string LoadTestPopulationStatus { get; private set; } = "Подготовка состава";
        static readonly Unity.Profiling.ProfilerMarker loadTestMarker = new("LoadTest.ScriptedMotion");

        public void BeginLoadTest()
        {
            if (SessionBusy) return;
            previousLoadTestBots = fillWithBots;
            previousLoadTestSeed = seedInput;
            LoadTestActive = true;
            LoadTestMoving = true;
            loadTestFillFinished = false;
            LoadTestPopulationReady = false;
            LoadTestPopulationStatus = "Подготовка состава";
            reportedLoadTestPlayers = reportedLoadTestShips = -1;
            nextLoadTestMotion = 0f;
            fillWithBots = true;
            observerSelected = false;
            seedInput = "41719";
            Begin(true, "127.0.0.1:" + Config.Port);
        }

        void EndLoadTest()
        {
            if (!LoadTestActive) return;
            LoadTestActive = false;
            fillWithBots = previousLoadTestBots;
            seedInput = previousLoadTestSeed;
        }

        void TickLoadTest()
        {
            if (!LoadTestActive || manager == null || !manager.ServerManager.Started) return;
            if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame) ToggleLoadTestMotion();
            if (Time.unscaledTime < nextLoadTestMotion) return;
            using var sample = loadTestMarker.Auto();
            nextLoadTestMotion = Time.unscaledTime + .2f;
            if (loadTestFillFinished) CheckLoadTestPopulation();
            foreach (var ship in NetworkShip.ActiveShips)
            {
                if (ship == null || !ship.IsServerInitialized || ship.IsSinking) continue;
                bool scripted = true;
                foreach (var player in NetworkPlayer.Active)
                    if (player != null && !player.IsBot.Value && player.Ship == ship) { scripted = false; break; }
                if (!scripted) continue;
                float phase = (float)manager.TimeManager.Tick * (float)manager.TimeManager.TickDelta + ship.TeamId.Value;
                ship.GetComponent<SailSystem>().SetDeploy(LoadTestMoving ? .35f + .1f * Mathf.Sin(phase * .15f) : 0f);
                if (ship.Helm.Driver == null) ship.Helm.Restore(LoadTestMoving ? Mathf.Sin(phase * .07f) * .08f : 0f, false, null);
            }
        }

        public void ToggleLoadTestMotion()
        {
            LoadTestMoving = !LoadTestMoving;
            nextLoadTestMotion = 0f;
        }

        void CheckLoadTestPopulation()
        {
            int ships = 0;
            foreach (var ship in NetworkShip.ActiveShips)
                if (ship != null && ship.IsSpawned && ship.IsServerInitialized && !ship.IsSinking) ships++;
            if (reportedLoadTestPlayers == players.Count && reportedLoadTestShips == ships) return;
            reportedLoadTestPlayers = players.Count;
            reportedLoadTestShips = ships;
            int expectedShips = MaxPlayers / CrewSize;
            LoadTestPopulationReady = players.Count == MaxPlayers && ships == expectedShips;
            LoadTestPopulationStatus = $"{(LoadTestPopulationReady ? "Состав готов" : "НЕПОЛНЫЙ ТЕСТ")}: персонажи {players.Count}/{MaxPlayers} · корабли {ships}/{expectedShips}";
            if (LoadTestPopulationReady) Debug.Log("LOAD_TEST_READY " + LoadTestPopulationStatus);
            else Debug.LogWarning("LOAD_TEST_INCOMPLETE " + LoadTestPopulationStatus);
        }

        void DrawLoadTestWarning()
        {
            if (!LoadTestActive || !loadTestFillFinished || LoadTestPopulationReady || !playing) return;
            loadTestWarningStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 15, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, .65f, .35f) } };
            GUI.Box(new Rect(16f, Screen.height - 88f, Mathf.Min(760f, Screen.width - 32f), 64f), LoadTestPopulationStatus + "\nЭтот состав не подходит для замера полного матча.", loadTestWarningStyle);
        }
    }
}
