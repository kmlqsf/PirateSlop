using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        readonly Dictionary<int, BotDebugJournal> botJournals = new();
        static readonly Unity.Profiling.ProfilerMarker botDiagnosticMarker = new("Bots.Diagnostics");
        public BotPathScheduler BotPaths { get; } = new();
        float nextBotDiagnosticSample;
        public bool RemoteBotDiagnosticsAllowed { get; private set; }

        public void RegisterBotDiagnostics(NetworkPlayer bot)
        {
            if (bot.IsServerInitialized && bot.IsBot.Value && !botJournals.ContainsKey(bot.BotNumber))
                botJournals.Add(bot.BotNumber, new BotDebugJournal(bot));
        }

        public void RecordBotEvent(int number, string message)
        {
            if (manager != null && manager.ServerManager.Started && botJournals.TryGetValue(number, out var journal)) journal.Add(message);
        }

        public void EndBotDiagnostics(int number, string reason)
        {
            if (botJournals.TryGetValue(number, out var journal)) journal.Remove(reason);
        }

        public string AssignBotHelmAction(int number, bool cancel)
        {
            if (manager == null || !manager.ServerManager.Started || !botJournals.TryGetValue(number, out var journal) ||
                journal.Row.Removed || journal.Player == null) return "Бот недоступен";
            return journal.Player.AssignHelmAction(cancel);
        }

        public void RecordBotDecision(int number, string goal, string reason, string action, string navigation, string perception, string crew, string failure, string candidates)
        {
            if (manager != null && manager.ServerManager.Started && botJournals.TryGetValue(number, out var journal))
                journal.Decision(goal, reason, action, navigation, perception, crew, failure, candidates);
        }

        void TickBotDiagnostics()
        {
            if (manager == null || !manager.ServerManager.Started || Time.unscaledTime < nextBotDiagnosticSample) return;
            using var sample = botDiagnosticMarker.Auto();
            nextBotDiagnosticSample = Time.unscaledTime + .5f;
            foreach (var journal in botJournals.Values) journal.Sample();
        }

        public BotDebugRow[] GetBotDebugRows()
        {
            var rows = new BotDebugRow[botJournals.Count];
            int i = 0;
            foreach (var journal in botJournals.Values) rows[i++] = journal.Row;
            System.Array.Sort(rows, (a, b) => a.Number.CompareTo(b.Number));
            return rows;
        }

        public BotDebugSnapshot GetBotDebugSnapshot(int number, int revision)
            => botJournals.TryGetValue(number, out var journal) ? journal.Read(revision) : default;
    }
}
