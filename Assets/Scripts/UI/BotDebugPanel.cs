using System;
using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(-110)]
    public sealed class BotDebugPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static int closedFrame = -1;
        public static bool ConsumedInput => IsOpen || closedFrame == Time.frameCount;
        NetworkPlayer player;
        BotDebugRow[] rows = Array.Empty<BotDebugRow>();
        BotDebugSnapshot snapshot;
        string[] history = Array.Empty<string>();
        string search = "", error = "";
        string actionResult = "";
        int selected, revision = -1;
        float nextRequest, receivedAt = -1;
        bool ownsPanel, previousCursorLocked;
        Vector2 listScroll, detailScroll;
        GUIStyle wrapped;

        bool Spectator => player == null && SessionController.Instance != null && SessionController.Instance.ObserverActive;
        void Awake() => player = GetComponent<NetworkPlayer>();

        void Update()
        {
            if ((!Spectator && (player == null || !player.IsOwner || !player.IsSpawned)) || !DeveloperMenu.Available) { if (ownsPanel) Close(); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame && !DeveloperMenu.IsOpen)
            {
                if (ownsPanel) Close();
                else if (!PlayerInventory.LootWindowOpen)
                {
                    previousCursorLocked = Cursor.lockState == CursorLockMode.Locked;
                    IsOpen = ownsPanel = true;
                    revision = -1; nextRequest = 0; receivedAt = -1;
                    AdvancedPlayerController.SetCursor(false);
                }
            }
            if (!ownsPanel) return;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (Time.unscaledTime >= nextRequest)
            {
                nextRequest = Time.unscaledTime + .5f;
                if (Spectator)
                {
                    var session = SessionController.Instance;
                    Receive("", session.GetBotDebugRows(), session.GetBotDebugSnapshot(selected, revision));
                }
                else player.RequestBotDebug(selected, revision);
            }
        }

        void Close()
        {
            if (!ownsPanel) return;
            IsOpen = ownsPanel = false;
            closedFrame = Time.frameCount;
            AdvancedPlayerController.SetCursor(previousCursorLocked && (Spectator || player != null && player.IsSpawned));
        }

        void OnDisable() => Close();
        public void ReportAction(string message) => actionResult = message;

        public void Receive(string failure, BotDebugRow[] roster, BotDebugSnapshot state)
        {
            if (!ownsPanel) return;
            error = failure;
            rows = roster ?? Array.Empty<BotDebugRow>();
            receivedAt = Time.unscaledTime;
            if (!string.IsNullOrEmpty(error)) { snapshot = default; history = Array.Empty<string>(); revision = -1; return; }
            if (state.Number != selected) return;
            snapshot = state;
            if (state.History != null && state.History.Length > 0) history = state.History;
            revision = state.Revision;
        }

        void OnGUI()
        {
            if (!ownsPanel || !Spectator && (player == null || !player.IsOwner)) return;
            wrapped ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            var previousDepth = GUI.depth;
            GUI.depth = -100;
            GUILayout.BeginArea(new Rect(16, 16, Mathf.Max(280, Screen.width - 32), Mathf.Max(220, Screen.height - 32)), "Боты · F10 / Esc", GUI.skin.window);
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Поиск по номеру", GUILayout.Width(130));
            search = GUILayout.TextField(search, 20, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрыть", GUILayout.Width(100))) Close();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error, wrapped);
            else if (receivedAt < 0) GUILayout.Label("Ожидание ответа сервера…");
            else if (Time.unscaledTime - receivedAt > 2) GUILayout.Label("Данные устарели: сервер не отвечает.");
            GUILayout.BeginHorizontal();
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Width(Mathf.Min(270, Screen.width * .35f)));
            string filter = search.Trim().Replace("БОТ", "").Replace("бот", "");
            foreach (var row in rows)
            {
                if (filter.Length > 0 && !row.Number.ToString().Contains(filter)) continue;
                if (GUILayout.Button($"{(selected == row.Number ? "► " : "")}БОТ{row.Number} · экипаж {row.Team}\n{row.State}", GUILayout.Height(48)))
                {
                    selected = row.Number; revision = -1; snapshot = default;
                    actionResult = "";
                    history = Array.Empty<string>(); detailScroll = Vector2.zero;
                }
            }
            if (rows.Length == 0 && receivedAt >= 0 && string.IsNullOrEmpty(error)) GUILayout.Label("В этой сессии нет ботов.");
            GUILayout.EndScrollView();
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            if (selected == 0) GUILayout.Label("Выберите бота по номеру над его головой.", wrapped);
            else if (snapshot.Number != selected) GUILayout.Label($"БОТ{selected} · ожидание данных…");
            else
            {
                GUILayout.Label($"БОТ{selected} · позиция {snapshot.Position:F1}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Занять штурвал")) RequestHelm(false);
                if (GUILayout.Button("Отменить задачу")) RequestHelm(true);
                GUILayout.EndHorizontal();
                if (actionResult.Length > 0) GUILayout.Label(actionResult, wrapped);
                Field("Цель", snapshot.Goal); Field("Причина", snapshot.Reason);
                Field("Действие / этап", snapshot.Action); Field("Навигация / застревание", snapshot.Navigation);
                Field("Восприятие / память", snapshot.Perception); Field("Экипаж / резервирования", snapshot.Crew);
                Field("Ошибки / восстановление", snapshot.Failure); Field("Оценки вариантов", snapshot.Candidates);
                GUILayout.Space(12);
                GUILayout.Label("Последние события (новые сверху; время с начала работы сцены)");
                for (int i = history.Length - 1; i >= 0; i--) GUILayout.Label(history[i], wrapped);
            }
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.depth = previousDepth;
        }

        void RequestHelm(bool cancel)
        {
            if (Spectator) ReportAction(SessionController.Instance.AssignBotHelmAction(selected, cancel));
            else player.RequestBotHelmAction(selected, cancel);
        }

        void Field(string title, string value) => GUILayout.Label(title + ": " + value, wrapped);
    }
}
