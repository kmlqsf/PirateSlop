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
        readonly List<int> eliminatedBotKeys = new();
        public bool TeamEliminated(int team) => eliminatedTeams.Contains(team);
        void TickCrewElimination()
        {
            if (manager == null || !manager.ServerManager.Started || Time.time < nextCrewCheck) return;
            nextCrewCheck = Time.time + .5f;
            for (int shipIndex = NetworkShip.ActiveShips.Count - 1; shipIndex >= 0; shipIndex--)
            {
                var ship = NetworkShip.ActiveShips[shipIndex];
                if (ship == null || !ship.IsSpawned || ship.IsSinking || ship.TeamId.Value <= 0) continue;
                bool hasCrew = false, hasSurvivor = false;
                foreach (var member in players.Values)
                {
                    if (member == null || member.HomeShipId.Value != ship.ParticipantId.Value) continue;
                    hasCrew = true;
                    if (!member.Motor.IsDead || member.HasPendingSeaPact) { hasSurvivor = true; break; }
                }
                if (!hasCrew || hasSurvivor) continue;
                eliminatedTeams.Add(ship.TeamId.Value);
                foreach (var member in players.Values)
                    if (member != null && member.HomeShipId.Value == ship.ParticipantId.Value) member.Eliminated.Value = true;
                ship.BeginSinking();
                eliminatedBotKeys.Clear();
                foreach (var entry in players)
                    if (entry.Value != null && entry.Value.IsBot.Value && entry.Value.TeamId.Value == ship.TeamId.Value) eliminatedBotKeys.Add(entry.Key);
                foreach (int key in eliminatedBotKeys)
                {
                    var member = players[key];
                    member.ReleaseServerInteractions();
                    EndBotDiagnostics(member.BotNumber, "Экипаж выбыл из матча");
                    manager.ServerManager.Despawn(member.NetworkObject);
                    players.Remove(key); slots.Remove(key);
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
            EndLoadTest();
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
                if (clustered && !FindNearbySpawn(ref position, ref yaw))
                {
                    if (!LoadTestActive) continue;
                    position = spawns[i].Position;
                    yaw = spawns[i].Yaw;
                }
                if (!world.CanSail(position, yaw)) continue;
                if (!ShipSpawnClear(position, 52f)) continue;
                ship = Instantiate(ShipPrefab, position, Quaternion.Euler(0, yaw, 0)).GetComponent<NetworkShip>();
                ship.ParticipantId.Value = nextParticipant++;
                ship.TeamId.Value = team;
                manager.ServerManager.Spawn(ship.NetworkObject);
                if (EnvironmentTestActive) StartCoroutine(SpawnTestUpgradeChest(ship));
                slot = i;
                return true;
            }
            return false;
        }

        bool ShipSpawnClear(Vector3 position, float clearance)
        {
            float distanceSquared = clearance * clearance;
            foreach (var existing in NetworkShip.ActiveShips)
                if (existing != null && (existing.transform.position - position).sqrMagnitude < distanceSquared) return false;
            return true;
        }

    }
}
