using System.Collections.Generic;
using System.Text;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct MatchTeamResult
    {
        public int Team, Place, ShipsSunk, Hits, Chests;
        public float Survived;
        public string Crew;
    }

    public struct MatchResultMessage : IBroadcast
    {
        public int WinnerTeam;
        public float Duration;
        public MatchTeamResult[] Teams;
    }

    public sealed partial class SessionController
    {
        sealed class MatchTeamRecord
        {
            public int ShipsSunk, Hits, Chests, LastHitBy;
            public float LastHitAt = float.NegativeInfinity, SankAt = -1f;
            public string Crew = "";
        }

        const float SinkCreditWindow = 90f;
        readonly Dictionary<int, MatchTeamRecord> matchTeams = new();
        readonly List<int> matchAliveTeams = new();
        bool matchEnded;
        float matchStartedAt = -1f;
        MatchResultMessage? matchResult;
        int localMatchTeam;

        public bool MatchResultVisible => matchResult.HasValue;

        float MatchClock => (float)manager.TimeManager.TicksToTime(manager.TimeManager.Tick);
        bool MatchRulesActive => manager != null && manager.ServerManager.Started && !TutorialActive && !EnvironmentTestActive && !LoadTestActive && !PerformanceBenchmarkActive;

        MatchTeamRecord MatchTeam(int team)
        {
            if (!matchTeams.TryGetValue(team, out var record)) matchTeams[team] = record = new MatchTeamRecord();
            return record;
        }

        public void RecordShipHit(NetworkShip target, GameObject attacker, bool countHit = true)
        {
            if (matchEnded || !MatchRulesActive || target == null || target.IsSinking || attacker == null) return;
            var shooter = attacker.GetComponent<NetworkPlayer>();
            if (shooter == null) return;
            int from = shooter.TeamId.Value, to = target.TeamId.Value;
            if (from <= 0 || to <= 0 || from == to) return;
            if (countHit) MatchTeam(from).Hits++;
            var victim = MatchTeam(to);
            victim.LastHitBy = from;
            victim.LastHitAt = MatchClock;
        }

        public void RecordChestOpened(NetworkWeapon opener)
        {
            if (matchEnded || !MatchRulesActive || opener == null) return;
            var player = opener.GetComponent<NetworkPlayer>();
            if (player != null && player.TeamId.Value > 0) MatchTeam(player.TeamId.Value).Chests++;
        }

        string CrewNames(int team)
        {
            var names = new StringBuilder();
            foreach (var player in players.Values)
            {
                if (player == null || player.TeamId.Value != team) continue;
                if (names.Length > 0) names.Append(", ");
                names.Append(player.DisplayName);
            }
            return names.ToString();
        }

        void TrackMatchTeams()
        {
            if (matchStartedAt < 0f) matchStartedAt = MatchClock;
            foreach (var ship in NetworkShip.ActiveShips)
                if (ship != null && ship.IsSpawned && ship.TeamId.Value > 0 && !matchTeams.ContainsKey(ship.TeamId.Value))
                    MatchTeam(ship.TeamId.Value).Crew = CrewNames(ship.TeamId.Value);
        }

        void RecordTeamEliminated(NetworkShip ship)
        {
            if (matchEnded || !MatchRulesActive) return;
            var record = MatchTeam(ship.TeamId.Value);
            record.SankAt = (float)manager.TimeManager.TicksToTime(ship.SinkingTick);
            string crew = CrewNames(ship.TeamId.Value);
            if (crew.Length > 0) record.Crew = crew;
            if (record.LastHitBy > 0 && record.SankAt - record.LastHitAt <= SinkCreditWindow) MatchTeam(record.LastHitBy).ShipsSunk++;
        }

        void TickMatchEnd()
        {
            if (matchEnded || !MatchRulesActive || !stormRunning) return;
            TrackMatchTeams();
            if (matchTeams.Count < 2) return;
            matchAliveTeams.Clear();
            foreach (var entry in matchTeams)
            {
                if (entry.Value.SankAt >= 0f) continue;
                bool afloat = false;
                foreach (var ship in NetworkShip.ActiveShips)
                    if (ship != null && ship.IsSpawned && ship.TeamId.Value == entry.Key) { afloat = true; break; }
                if (afloat) matchAliveTeams.Add(entry.Key);
                else entry.Value.SankAt = MatchClock;
            }
            if (matchAliveTeams.Count > 1) return;
            EndMatch(matchAliveTeams.Count == 1 ? matchAliveTeams[0] : 0);
        }

        void EndMatch(int survivor)
        {
            matchEnded = true;
            float now = MatchClock;
            var order = new List<int>(matchTeams.Keys);
            order.Sort((a, b) =>
            {
                float sa = a == survivor ? float.PositiveInfinity : matchTeams[a].SankAt;
                float sb = b == survivor ? float.PositiveInfinity : matchTeams[b].SankAt;
                return sb.CompareTo(sa);
            });
            var teams = new MatchTeamResult[order.Count];
            int place = 0;
            float previous = float.NaN;
            for (int i = 0; i < order.Count; i++)
            {
                int team = order[i];
                var record = matchTeams[team];
                float sank = team == survivor ? float.PositiveInfinity : record.SankAt;
                if (sank != previous) place = i + 1;
                previous = sank;
                if (team == survivor)
                {
                    string crew = CrewNames(team);
                    if (crew.Length > 0) record.Crew = crew;
                }
                teams[i] = new MatchTeamResult
                {
                    Team = team, Place = place, ShipsSunk = record.ShipsSunk, Hits = record.Hits, Chests = record.Chests,
                    Survived = Mathf.Max(0f, (team == survivor ? now : record.SankAt) - matchStartedAt), Crew = record.Crew
                };
            }
            int winner = teams.Length > 1 && teams[1].Place == 1 ? 0 : teams[0].Team;
            matchResult = new MatchResultMessage { WinnerTeam = winner, Duration = Mathf.Max(0f, now - matchStartedAt), Teams = teams };
            Debug.Log($"MATCH_END winner={winner} teams={teams.Length} duration={now - matchStartedAt:0}");
            manager.ServerManager.Broadcast(matchResult.Value);
        }

        void SendMatchResult(NetworkConnection connection)
        {
            if (matchEnded && matchResult.HasValue) manager.ServerManager.Broadcast(connection, matchResult.Value);
        }

        void ReceiveMatchResult(MatchResultMessage message, Channel channel)
        {
            if (message.Teams == null || message.Teams.Length == 0) return;
            localMatchTeam = 0;
            foreach (var player in NetworkPlayer.Active)
                if (player != null && player.IsOwner && !player.IsBot.Value) { localMatchTeam = player.TeamId.Value; break; }
            matchResult = message;
        }

        void ResetMatch()
        {
            matchTeams.Clear();
            matchEnded = false;
            matchStartedAt = -1f;
            matchResult = null;
            localMatchTeam = 0;
        }

        static string MatchTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        void DrawMatchResult()
        {
            if (!matchResult.HasValue || dedicated || Automated) return;
            if (Cursor.lockState != CursorLockMode.None) AdvancedPlayerController.SetCursor(false);
            var result = matchResult.Value;
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.height / 900f, Screen.width / 1280f), width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            PirateHudStyle.Fill(new Rect(0, 0, width, height), new Color(0f, .02f, .03f, .6f));
            float panelWidth = Mathf.Min(980f, width - 80f), x = (width - panelWidth) * .5f;
            int rows = Mathf.Min(result.Teams.Length, 10);
            float panelHeight = 230f + rows * 38f, y = Mathf.Max(30f, (height - panelHeight) * .5f);
            PirateHudStyle.Panel(new Rect(x, y, panelWidth, panelHeight));
            MatchTeamResult own = default;
            foreach (var team in result.Teams) if (team.Team == localMatchTeam) own = team;
            string headline = result.WinnerTeam == 0 ? L("Ничья") : own.Team == result.WinnerTeam ? L("Победа!") : own.Place > 0 ? L("Ваше место: ") + own.Place : L("Матч окончен");
            PirateHudStyle.Label(new Rect(x, y + 6, panelWidth, 56), headline, PirateHudStyle.Gold, true);
            var winnerResult = result.Teams[0];
            string winnerLine = result.WinnerTeam == 0 ? L("Последние корабли затонули одновременно") : L("Победил экипаж ") + winnerResult.Team + (string.IsNullOrEmpty(winnerResult.Crew) ? "" : ": " + winnerResult.Crew);
            PirateHudStyle.Label(new Rect(x, y + 62, panelWidth, 30), winnerLine + L("   ·   длительность ") + MatchTime(result.Duration), PirateHudStyle.Paper);
            float rowY = y + 108f;
            float[] columns = { 0f, .08f, .52f, .64f, .76f, .88f };
            string[] headers = { L("Место"), L("Экипаж"), L("Потоплено"), L("Попаданий"), L("Сундуков"), L("Время") };
            for (int c = 0; c < headers.Length; c++)
                PirateHudStyle.Label(new Rect(x + 16 + columns[c] * (panelWidth - 32), rowY, (panelWidth - 32) * ((c + 1 < columns.Length ? columns[c + 1] : 1f) - columns[c]), 30), headers[c], PirateHudStyle.Muted, false, c == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            rowY += 34f;
            for (int i = 0; i < rows; i++)
            {
                var team = result.Teams[i];
                if (team.Team == localMatchTeam) PirateHudStyle.Fill(new Rect(x + 6, rowY, panelWidth - 12, 34), new Color(PirateHudStyle.Gold.r, PirateHudStyle.Gold.g, PirateHudStyle.Gold.b, .16f));
                string crew = L("Экипаж ") + team.Team + (string.IsNullOrEmpty(team.Crew) ? "" : ": " + team.Crew);
                string[] cells = { team.Place.ToString(), crew, team.ShipsSunk.ToString(), team.Hits.ToString(), team.Chests.ToString(), MatchTime(team.Survived) };
                Color color = team.Place == 1 ? PirateHudStyle.Gold : PirateHudStyle.Paper;
                for (int c = 0; c < cells.Length; c++)
                    PirateHudStyle.Label(new Rect(x + 16 + columns[c] * (panelWidth - 32), rowY, (panelWidth - 32) * ((c + 1 < columns.Length ? columns[c + 1] : 1f) - columns[c]), 34), cells[c], color, false, c == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                rowY += 38f;
            }
            bool host = manager != null && manager.ServerManager.Started && !dedicated;
            if (host) PirateHudStyle.Label(new Rect(x, rowY + 4, panelWidth, 24), L("Вы хост: выход вернёт всех игроков в лобби"), PirateHudStyle.Muted);
            if (PirateHudStyle.Button(new Rect(x + (panelWidth - 260f) * .5f, y + panelHeight - 58f, 260f, 46f), L("В меню"))) Disconnect();
            GUI.matrix = oldMatrix;
        }
    }
}
