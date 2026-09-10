using System.Collections.Generic;
using System.Linq;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        public const int CrewSize = 3;
        bool fillWithBots, sessionBots;
        int nextTeam = 1, nextBotKey = -1, botPopulation, teamPopulation;
        float nextBotFill;
        readonly Dictionary<int, int> crewTeams = new();
        WorldRoutePlanner botRoutes;
        public WorldRoutePlanner BotRoutes => botRoutes ??= new WorldRoutePlanner(ProceduralWorld.Instance.Layout,
            ProceduralWorld.Instance.Profile.RouteGridSize, ProceduralWorld.Instance.Profile.RouteClearance);

        int HumanTeam(int crew)
        {
            if (crew > 0 && crewTeams.TryGetValue(crew, out int existing))
            {
                if (players.Values.Any(p => p != null && p.TeamId.Value == existing))
                    return players.Values.Count(p => p != null && !p.IsBot.Value && p.TeamId.Value == existing) < CrewSize ? existing : 0;
                crewTeams.Remove(crew);
            }
            var available = players.Values.Where(p => p != null && p.Ship != null)
                .GroupBy(p => p.TeamId.Value)
                .Where(g => g.Count(p => !p.IsBot.Value) < CrewSize && (crew <= 0 || (g.All(p => p.IsBot.Value) && !crewTeams.ContainsValue(g.Key))))
                .OrderByDescending(g => g.Count(p => !p.IsBot.Value)).FirstOrDefault();
            int team;
            if (available != null) team = available.Key;
            else
            {
                if (players.Values.Where(p => p != null).Select(p => p.TeamId.Value).Distinct().Count() >= MaxPlayers / CrewSize) return 0;
                team = nextTeam++;
            }
            if (crew > 0) crewTeams[crew] = team;
            return team;
        }

        public bool IsBotHelmsman(NetworkPlayer bot)
        {
            var ship = bot.Ship;
            if (ship == null) return false;
            ship.Helm.ValidateGrip();
            if (ship.Helm.IsControlling) return ship.Helm.IsControlledBy(bot.Motor);
            return players.Values.Where(p => p != null && p.IsBot.Value && p.Ship == ship && !p.Motor.IsDead && !p.Motor.IsSwimming && !p.Motor.IsKnockedBack)
                .OrderBy(p => p.ParticipantId.Value).FirstOrDefault() == bot;
        }

        Vector3 CrewSpawn(NetworkShip ship)
        {
            Vector3 best = ship.transform.TransformPoint(Config.PlayerLocalSpawn);
            float bestDistance = -1f;
            for (int i = 0; i < CrewSize; i++)
            {
                Vector3 candidate = ship.transform.TransformPoint(Config.PlayerLocalSpawn + Vector3.right * ((i - 1) * 1.4f));
                float distance = players.Values.Where(p => p != null && p.Ship == ship)
                    .Select(p => (p.transform.position - candidate).sqrMagnitude).DefaultIfEmpty(100f).Min();
                if (distance > bestDistance) { best = candidate; bestDistance = distance; }
            }
            return best;
        }

        void ResetBots()
        {
            botRoutes = null;
            crewTeams.Clear();
            nextTeam = 1; nextBotKey = -1;
            botPopulation = teamPopulation = 0;
            nextBotFill = 0;
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

        void TickBots()
        {
            if (!sessionBots || manager == null || !manager.ServerManager.Started || Time.time < nextBotFill) return;
            if (!dedicated && !playing) return;
            nextBotFill = Time.time + .5f;
            int pending = manager.ServerManager.Clients.Values.Count(c => c.IsAuthenticated && !players.ContainsKey(c.ClientId));
            int target = Mathf.Max(0, MaxPlayers - pending);
            while (players.Count > target && RemoveOneBot()) { }
            if (players.Count >= target) return;
            var crew = players.Where(p => p.Value != null && p.Value.Ship != null).GroupBy(p => p.Value.TeamId.Value)
                .Where(g => g.Count() < CrewSize).OrderByDescending(g => g.Any(p => !p.Value.IsBot.Value)).FirstOrDefault();
            NetworkShip ship;
            int slot, team;
            if (crew != null)
            {
                var member = crew.First();
                team = crew.Key; ship = member.Value.Ship; slot = slots[member.Key];
            }
            else
            {
                if (players.Values.Where(p => p != null).Select(p => p.TeamId.Value).Distinct().Count() >= MaxPlayers / CrewSize) return;
                team = nextTeam++;
                if (!TrySpawnShip(team, false, out ship, out slot)) return;
            }
            var player = Instantiate(PlayerPrefab, CrewSpawn(ship), ship.transform.rotation).GetComponent<NetworkPlayer>();
            player.ParticipantId.Value = nextParticipant++;
            player.IsBot.Value = true;
            player.TeamId.Value = team;
            player.ShipObject.Value = ship.NetworkObject;
            player.HomeShipId.Value = ship.ParticipantId.Value;
            player.name = "BotCaptain_" + player.ParticipantId.Value;
            int key = nextBotKey--;
            players.Add(key, player); slots.Add(key, slot);
            manager.ServerManager.Spawn(player.NetworkObject);
            if (!stormRunning) StartStorm();
            BroadcastPopulation();
        }

        bool RemoveOneBot(int team = 0)
        {
            foreach (var entry in players)
            {
                var bot = entry.Value;
                if (bot == null || !bot.IsBot.Value || (team > 0 && bot.TeamId.Value != team)) continue;
                var ship = bot.Ship;
                if (ship != null && ship.Helm.IsControlledBy(bot.Motor)) ship.Helm.ReleaseControl();
                if (ship != null && !players.Values.Any(p => p != null && p != bot && p.Ship == ship))
                {
                    ship.Helm.ReleaseControl();
                    foreach (var other in players.Values)
                        if (other != null && other != bot && other.Passenger.Ship == ship.Body) other.ReturnHome();
                    if (ship.IsSpawned) manager.ServerManager.Despawn(ship.NetworkObject);
                }
                if (bot.IsSpawned) manager.ServerManager.Despawn(bot.NetworkObject);
                players.Remove(entry.Key); slots.Remove(entry.Key);
                BroadcastPopulation();
                return true;
            }
            return false;
        }
    }
}
