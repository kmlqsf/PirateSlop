using System.Collections.Generic;
using System.Linq;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        public const int CrewSize = 3;
        bool fillWithBots;
        int nextTeam = 1, botPopulation, teamPopulation;
        float nextCrewCheck;
        readonly HashSet<int> eliminatedTeams = new();
        public bool TeamEliminated(int team) => eliminatedTeams.Contains(team);
        void TickCrewElimination()
        {
            if (manager == null || !manager.ServerManager.Started || Time.time < nextCrewCheck) return;
            nextCrewCheck = Time.time + .5f;
            foreach (var ship in NetworkShip.ActiveShips.ToArray())
            {
                if (ship == null || !ship.IsSpawned || ship.IsSinking || ship.TeamId.Value <= 0) continue;
                var crew = players.Values.Where(p => p != null && p.HomeShipId.Value == ship.ParticipantId.Value).ToArray();
                if (crew.Length == 0 || crew.Any(p => !p.Motor.IsDead)) continue;
                eliminatedTeams.Add(ship.TeamId.Value);
                foreach (var member in crew) member.Eliminated.Value = true;
                ship.BeginSinking();
                foreach (var entry in players.Where(e => e.Value != null && e.Value.IsBot.Value && e.Value.TeamId.Value == ship.TeamId.Value).ToArray())
                {
                    entry.Value.ReleaseServerInteractions();
                    EndBotDiagnostics(entry.Value.BotNumber, "Экипаж выбыл из матча");
                    manager.ServerManager.Despawn(entry.Value.NetworkObject);
                    players.Remove(entry.Key); slots.Remove(entry.Key);
                }
                BroadcastPopulation();
            }
        }
        readonly Dictionary<int, int> crewTeams = new();
        int HumanTeam(int crew)
        {
            if (crew > 0 && crewTeams.TryGetValue(crew, out int existing))
            {
                if (TeamEliminated(existing)) return 0;
                if (players.Values.Any(p => p != null && p.TeamId.Value == existing))
                    return players.Values.Count(p => p != null && !p.IsBot.Value && p.TeamId.Value == existing) < CrewSize ? existing : 0;
                crewTeams.Remove(crew);
            }
            var available = players.Values.Where(p => p != null && p.Ship != null && !TeamEliminated(p.TeamId.Value))
                .GroupBy(p => p.TeamId.Value)
                .Where(g => g.Count(p => !p.IsBot.Value) < CrewSize && (crew <= 0 || (g.All(p => p.IsBot.Value) && !crewTeams.ContainsValue(g.Key))))
                .OrderByDescending(g => g.Count(p => !p.IsBot.Value)).FirstOrDefault();
            int team;
            if (available != null) team = available.Key;
            else
            {
                if (players.Values.Where(p => p != null && !TeamEliminated(p.TeamId.Value)).Select(p => p.TeamId.Value).Distinct().Count() + eliminatedTeams.Count >= MaxPlayers / CrewSize) return 0;
                team = nextTeam++;
            }
            if (crew > 0) crewTeams[crew] = team;
            return team;
        }

        Vector3 CrewSpawn(NetworkShip ship, NetworkPlayer replacing = null)
        {
            Vector3 best = ship.transform.TransformPoint(Config.PlayerLocalSpawn);
            float bestDistance = -1f;
            for (int i = 0; i < CrewSize; i++)
            {
                Vector3 candidate = ship.transform.TransformPoint(Config.PlayerLocalSpawn + Vector3.right * ((i - 1) * 1.4f));
                float distance = players.Values.Where(p => p != null && p != replacing && p.Ship == ship)
                    .Select(p => (p.transform.position - candidate).sqrMagnitude).DefaultIfEmpty(100f).Min();
                if (distance > bestDistance) { best = candidate; bestDistance = distance; }
            }
            return best;
        }

        void ResetRoster()
        {
            botJournals.Clear();
            BotPaths.Clear();
            botCrews.Clear(); botCrewCursor = 0; nextBotCrewTick = nextBotCrewRoster = 0;
            nextBotDiagnosticSample = 0;
            botRosterPolicy = null;
            eliminatedTeams.Clear(); nextCrewCheck = 0f;

            crewTeams.Clear();
            nextTeam = 1; nextBotKey = -1;
            botPopulation = teamPopulation = 0;

            stormRunning = false;
        }

        bool TrySpawnShip(int team, bool clustered, out NetworkShip ship, out int slot)
        {
            ship = null; slot = -1;
            var world = ProceduralWorld.Instance;
            var spawns = world.Points("ship_spawn").ToArray();
            for (int i = 0; i < spawns.Length; i++)
            {
                if (slots.ContainsValue(i)) continue;
                Vector3 position = spawns[i].Position;
                float yaw = spawns[i].Yaw;
                if (clustered && !FindNearbySpawn(ref position, ref yaw)) continue;
                if (!world.CanSail(position, yaw)) continue;
                if (NetworkShip.ActiveShips.Any(s => s != null && Vector3.Distance(s.transform.position, position) < 52f)) continue;
                ship = Instantiate(ShipPrefab, position, Quaternion.Euler(0, yaw, 0)).GetComponent<NetworkShip>();
                ship.ParticipantId.Value = nextParticipant++;
                ship.TeamId.Value = team;
                manager.ServerManager.Spawn(ship.NetworkObject);
                slot = i;
                return true;
            }
            return false;
        }

    }
}
