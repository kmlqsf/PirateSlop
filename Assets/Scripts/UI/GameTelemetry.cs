using System.Collections.Generic;
using System.Text;
using FishNet.Managing;
using PirateSlop.Networking;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace PirateSlop
{
    [DisallowMultipleComponent]
    public sealed class GameTelemetry : MonoBehaviour
    {
        const string Preference = "ShowTelemetry";
        const float Interval = .5f;
        static GameTelemetry instance;
        public static bool Visible => PlayerPrefs.GetInt(Preference, 0) == 1;
        readonly StringBuilder report = new(1024);
        readonly string[] drawNames =
        {
            "Standard Draw Calls Count", "Standard Indirect Draw Calls Count",
            "Standard Instanced Draw Calls Count", "SRP Batcher Draw Calls Count",
            "BRG Draw Calls Count", "BRG Indirect Draw Calls Count",
            "Null Geometry Draw Calls Count", "Null Geometry Indirect Draw Calls Count"
        };
        ProfilerRecorder cpu, render, gpu, triangles, setPass, gc;
        ProfilerRecorder[] draws;
        NetworkManager manager;
        GUIStyle style;
        string text = "ТЕЛЕМЕТРИЯ · сбор данных…";
        bool collecting;
        int frames, lines;
        float elapsed, worstFrame;

        public static void SetVisible(bool value)
        {
            PlayerPrefs.SetInt(Preference, value ? 1 : 0);
            PlayerPrefs.Save();
            if (instance != null) instance.Collect(value);
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            manager = GetComponent<NetworkManager>();
        }

        void OnEnable() => Collect(Visible);
        void OnDisable() => Collect(false);
        void OnDestroy() { if (instance == this) instance = null; }

        void Collect(bool value)
        {
            if (collecting == value) return;
            collecting = value;
            elapsed = worstFrame = 0;
            frames = 0;
            if (!value)
            {
                cpu.Dispose(); render.Dispose(); gpu.Dispose();
                triangles.Dispose(); setPass.Dispose(); gc.Dispose();
                if (draws != null) foreach (var recorder in draws) recorder.Dispose();
                draws = null;
                return;
            }
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            cpu = Record(handles, ProfilerCategory.Render, "CPU Main Thread Frame Time");
            render = Record(handles, ProfilerCategory.Render, "CPU Render Thread Frame Time");
            gpu = Record(handles, ProfilerCategory.Render, "GPU Frame Time");
            triangles = Record(handles, ProfilerCategory.Render, "Triangles Count");
            setPass = Record(handles, ProfilerCategory.Render, "SetPass Calls Count");
            gc = Record(handles, ProfilerCategory.Memory, "GC Allocated In Frame");
            draws = new ProfilerRecorder[drawNames.Length];
            for (int i = 0; i < draws.Length; i++) draws[i] = Record(handles, ProfilerCategory.Render, drawNames[i]);
            text = "ТЕЛЕМЕТРИЯ · сбор данных…";
            lines = 1;
        }

        static ProfilerRecorder Record(List<ProfilerRecorderHandle> handles, ProfilerCategory category, string name)
        {
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name == name && description.Category == category)
                    return ProfilerRecorder.StartNew(category, name, 1);
            }
            return default;
        }

        void Update()
        {
            if (!collecting) return;
            float delta = Time.unscaledDeltaTime;
            if (delta <= 0) return;
            frames++;
            elapsed += delta;
            worstFrame = Mathf.Max(worstFrame, delta);
            if (elapsed < Interval) return;
            RefreshReport();
            frames = 0;
            elapsed = worstFrame = 0;
        }

        static string Counter(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue.ToString("N0") : "—";
        static string Milliseconds(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 && recorder.LastValue > 0 ? (recorder.LastValue / 1000000d).ToString("F1") : "—";
        static double MB(long bytes) => bytes / (1024d * 1024d);

        void RefreshReport()
        {
            report.Clear();
            report.AppendLine(Application.isEditor ? "ТЕЛЕМЕТРИЯ · Unity Editor" : "ТЕЛЕМЕТРИЯ");
            report.AppendLine($"FPS {frames / elapsed:F0}   ·   кадр {elapsed * 1000 / frames:F1} мс");
            report.AppendLine($"Пик кадра за 0.5 с: {worstFrame * 1000:F1} мс");
            report.AppendLine($"CPU {Milliseconds(cpu)} · Render {Milliseconds(render)} · GPU {Milliseconds(gpu)} мс");
            long drawCount = 0;
            bool hasDraws = false;
            foreach (var recorder in draws)
                if (recorder.Valid && recorder.Count > 0) { drawCount += recorder.LastValue; hasDraws = true; }
            report.AppendLine($"Draw calls {(hasDraws ? drawCount.ToString("N0") : "—")} · SetPass {Counter(setPass)}");
            report.AppendLine($"Треугольники: {Counter(triangles)}");
            report.AppendLine($"Unity память: {MB(Profiler.GetTotalAllocatedMemoryLong()):F0} МБ");
            report.AppendLine($"Резерв: {MB(Profiler.GetTotalReservedMemoryLong()):F0} · GC: {MB(Profiler.GetMonoUsedSizeLong()):F0} МБ");
            report.AppendLine($"GC выделено / кадр: {Counter(gc)} байт");
            bool server = manager != null && manager.IsServerStarted;
            bool client = manager != null && manager.IsClientStarted;
            string role = server ? (client ? "Хост" : "Сервер") : client ? "Клиент" : "Нет сессии";
            string ping = client && !server && manager.TimeManager.RoundTripTime > 0 ? $"{manager.TimeManager.RoundTripTime} мс" : server && client ? "локально" : "—";
            report.AppendLine($"Сеть: {role} · ping (RTT): {ping}");
            if (server || client) report.AppendLine($"Тики: {manager.TimeManager.TickRate} Гц · #{manager.TimeManager.LocalTick}");
            int bots = 0;
            NetworkPlayer local = null;
            foreach (var player in NetworkPlayer.Active)
            {
                if (player == null) continue;
                if (player.IsBot.Value) bots++;
                if (player.IsOwner && !player.IsBot.Value) local = player;
            }
            report.AppendLine($"Загружено: игроков {NetworkPlayer.Active.Count} (ботов {bots})");
            int chests = server ? NetworkLootChest.ServerChests.Count : NetworkLootChest.ClientChests.Count;
            report.AppendLine($"Кораблей {NetworkShip.ActiveShips.Count} · сундуков {chests}");
            if (local != null)
            {
                var position = local.transform.position;
                report.AppendLine($"XYZ: {position.x:F0} / {position.y:F0} / {position.z:F0} м");
                foreach (var ship in NetworkShip.ActiveShips)
                    if (ship != null && ship.ParticipantId.Value == local.HomeShipId.Value && ship.Motor != null)
                    {
                        report.AppendLine($"Корабль: {ship.Motor.Speed:F1} / {ship.Motor.MaxSpeed:F1} м/с");
                        break;
                    }
            }
            string limit = Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "без лимита";
            report.Append($"{Screen.width}×{Screen.height} · VSync {QualitySettings.vSyncCount} · FPS {limit}");
            text = report.ToString();
            lines = 1;
            foreach (char character in text) if (character == '\n') lines++;
        }

        void OnGUI()
        {
            if (!collecting || Event.current.type != EventType.Repaint) return;
            style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = false,
                padding = new RectOffset(), normal = { textColor = PirateHudStyle.Paper }
            };
            var matrix = GUI.matrix;
            var color = GUI.color;
            int depth = GUI.depth;
            float scale = Mathf.Min(1f, Screen.height / 720f, Screen.width / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.color = Color.white;
            GUI.depth = -100;
            float top = SessionController.MenuOpen ? 16f : 116f / scale;
            var panel = new Rect(Screen.width / scale - 358, top, 342, lines * 18 + 20);
            PirateHudStyle.Fill(panel, new Color(.015f, .035f, .04f, .86f));
            PirateHudStyle.Fill(new Rect(panel.x, panel.y, panel.width, 2), PirateHudStyle.Gold);
            GUI.Label(new Rect(panel.x + 10, panel.y + 10, panel.width - 20, panel.height - 20), text, style);
            GUI.depth = depth;
            GUI.color = color;
            GUI.matrix = matrix;
        }
    }
}
