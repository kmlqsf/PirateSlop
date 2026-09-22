using System.Linq;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        IBotRosterPolicy botRosterPolicy;
        int nextBotKey = -1;

        void InitializeBotRoster()
        {
            if (!manager.ServerManager.Started || botRosterPolicy == null) return;
            int requested = botRosterPolicy.TakeInitialCount(MaxPlayers, players.Count);
            int created = 0;
            for (; created < requested; created++)
            {
                var crew = players.Where(p => p.Value != null && p.Value.Ship != null && !TeamEliminated(p.Value.TeamId.Value))
                    .GroupBy(p => p.Value.TeamId.Value).FirstOrDefault(g => g.Count() < CrewSize);
                NetworkShip ship;
                int slot, team;
                if (crew != null)
                {
                    var member = crew.First();
                    team = crew.Key;
                    ship = member.Value.Ship;
                    slot = slots[member.Key];
                }
                else
                {
                    team = nextTeam++;
                    if (!TrySpawnShip(team, false, out ship, out slot)) break;
                }
                var bot = Instantiate(PlayerPrefab, CrewSpawn(ship), ship.transform.rotation).GetComponent<NetworkPlayer>();
                bot.ParticipantId.Value = nextParticipant++;
                bot.IsBot.Value = true;
                bot.TeamId.Value = team;
                bot.ShipObject.Value = ship.NetworkObject;
                bot.HomeShipId.Value = ship.ParticipantId.Value;
                bot.name = "Bot_" + bot.BotNumber;
                int key = nextBotKey--;
                players.Add(key, bot);
                slots.Add(key, slot);
                manager.ServerManager.Spawn(bot.NetworkObject);
            }
            if (created > 0 && !stormRunning) StartStorm();
            if (requested > 0) BroadcastPopulation();
            if (created < requested) Debug.LogWarning($"BOT_INITIAL_FILL requested={requested} created={created}; no automatic retry");
        }

        NetworkPlayer FindReplacementBot(int team)
        {
            if (botRosterPolicy == null || !botRosterPolicy.ReplaceOnHumanJoin) return null;
            return players.Values.Where(p => p != null && p.IsSpawned && p.IsBot.Value && !p.Eliminated.Value &&
                    !TeamEliminated(p.TeamId.Value) && p.Ship != null && !p.Ship.IsSinking)
                .OrderByDescending(p => p.TeamId.Value == team)
                .ThenBy(p => p.Motor.IsDead)
                .ThenBy(p => p.BotNumber).FirstOrDefault();
        }

        void RetireReplacedBot(NetworkPlayer bot)
        {
            EndBotDiagnostics(bot.BotNumber, "Заменён подключившимся человеком");
            var entry = players.First(p => p.Value == bot);
            var ship = bot.Ship;
            bot.ReleaseServerInteractions();
            manager.ServerManager.Despawn(bot.NetworkObject);
            players.Remove(entry.Key);
            slots.Remove(entry.Key);
            if (ship != null && !players.Values.Any(p => p != null && p.Ship == ship))
            {
                foreach (var other in players.Values)
                    if (other != null && other.Passenger.Ship == ship.Body) other.ReturnHome();
                if (ship.IsSpawned) manager.ServerManager.Despawn(ship.NetworkObject);
            }
        }
    }
}
