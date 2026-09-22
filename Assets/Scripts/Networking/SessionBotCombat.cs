using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        void AssignArtillery(BotCrew crew)
        {
            if (crew.Ship.IsSinking || crew.EscapeZone || Time.time < crew.NextArtilleryPlan) return;
            crew.NextArtilleryPlan = Time.time + .15f;
            foreach (var member in crew.Members)
                if (member != null && member.BotTaskRunning && member.BotTaskKey >= 70000 && member.BotTaskKey < 80000) return;
            var network = crew.Ship.GetComponent<NetworkCannon>();
            if (network == null || network.Crate == null || network.Crate.Cannons.Count == 0) return;
            for (int scan = 0; scan < Mathf.Min(8, network.Crate.Cannons.Count); scan++)
            {
                int index = crew.ArtilleryCursor++ % network.Crate.Cannons.Count;
                var cannon = network.Crate.Cannons[index];
                if (cannon != null && network.HasBoarding(cannon.Index) && Time.time - cannon.LastHumanControlTime >= Config.BotMotion.HumanStationGrace)
                {
                    var handler = MaintenanceWorker(crew, 70000 + cannon.Index, cannon.transform.position, false);
                    if (handler != null) handler.AssignBotStation(70000 + cannon.Index, new BotBoardingStation(cannon), "Кратко подтянуть связанный корабль и освободить трос");
                    continue;
                }
                if (cannon == null || !cannon.gameObject.activeInHierarchy || !cannon.IsLoaded || cannon.IsLoading || cannon.IsIgnited ||
                    !BotCannonAmmoPolicy.Supported(cannon.LoadedAmmo) || cannon.Operator != null || cannon.RemoteOccupied || network.HasBoarding(cannon.Index) ||
                    Time.time - cannon.LastHumanControlTime < Config.BotMotion.HumanStationGrace) continue;
                int key = 70000 + cannon.Index;
                var worker = MaintenanceWorker(crew, key, cannon.transform.position, false);
                if (worker == null) continue;
                NetworkShip target = null;
                float best = float.PositiveInfinity;
                int probes = 0;
                foreach (var candidate in NetworkShip.ActiveShips)
                {
                    if (crew.Pilot.CombatTarget != null && candidate != crew.Pilot.CombatTarget) continue;
                    if (candidate == null || candidate.IsSinking || candidate.TeamId.Value == crew.Ship.TeamId.Value) continue;
                    var delta = candidate.transform.position - cannon.transform.position;
                    float distance = delta.sqrMagnitude;
                    if (distance > 160f * 160f || distance < 15f * 15f || distance >= best) continue;
                    var local = cannon.transform.InverseTransformDirection(delta);
                    if (Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) > cannon.MaxTraverse + 10f) continue;
                    if (++probes > 3) break;
                    if (!BotCannonStation.Visible(worker, candidate)) continue;
                    target = candidate; best = distance;
                }
                if (target == null) continue;
                crew.DangerUntil = Time.time + 3f;
                worker.BotCandidates = $"Пушка {cannon.Index + 1}; видимый корабль команды {target.TeamId.Value}; дальность {Mathf.Sqrt(best):F0} м";
                worker.AssignBotStation(key, new BotCannonStation(cannon, crew.Ship, target), "Видимый вражеский корабль в секторе пушки; проверка упреждения и дуги");
                if (!worker.BotTaskRunning) worker.FinishBotTask();
                if (worker.BotTaskRunning) return;
            }
        }

        void ObserveBotCombat(BotCrew crew)
        {
            for (int i = 0; i < crew.Members.Count; i++)
            {
                crew.CombatObserverCursor %= crew.Members.Count;
                var observer = crew.Members[crew.CombatObserverCursor++];
                if (observer == null || !observer.IsBot.Value || observer.Motor.IsDead) continue;
                observer.BotVision.Observe(observer, players.Values);
                if (observer.BotVision.Visible) crew.DangerUntil = Time.time + 3f;
                if (observer.BotVision.Visible && observer.BotTaskKey == 52000)
                    observer.PreemptBotTask("Рыбалка прервана: замечен противник");
            }
        }
        void AssignPersonalCombat(BotCrew crew)
        {
            if (crew.Ship.IsSinking) return;
            foreach (var player in crew.Members)
            {
                if (player == null || !player.IsBot.Value || player.BotNeedsShipWait) continue;
                var sight = player.BotVision;
                if (!sight.Visible || Time.time - sight.LastSeen > 2f || !BotCombatMemory.Enemy(player, sight.Target)) continue;
                if (sight.Target.Passenger.Ship != crew.Ship.Body && (sight.LastPosition - player.transform.position).sqrMagnitude > 12f * 12f) continue;
                if (player.BotTaskRunning && player.BotTaskKey >= 1000 && player.BotTaskKey != 60000 && player.BotTaskKey != 60001 &&
                    (sight.LastPosition - player.transform.position).sqrMagnitude < 10f * 10f)
                { player.PreemptBotTask("Противник рядом; палубная работа отложена"); }
                if (!player.BotCanReceiveStation(60000)) continue;
                player.BotCandidates = $"Цель {sight.Target.ParticipantId.Value}: видна лично; задержка реакции и проверка союзников";
                int throwingSlot = BotSwordfishAction.Slot(player);
                int areaSlot = BotAreaThrowAction.Slot(player);
                int parrotSlot = BotUtilityAction.Slot(player, InventoryItem.BombParrot);
                int hookSlot = BotUtilityAction.Slot(player, InventoryItem.GrapplingHook);
                float distance = Vector3.Distance(player.transform.position, sight.LastPosition);
                bool throwing = throwingSlot >= 0 && distance >= 5f && distance <= 30f && Time.time >= player.NextBotThrow && player.BotCanReceiveStation(60001);
                bool areaThrow = !throwing && areaSlot >= 0 && distance >= 8f && distance <= 30f && Time.time >= player.NextBotThrow && player.BotCanReceiveStation(60001);
                bool utility = !throwing && !areaThrow && Time.time >= player.NextBotThrow && player.BotCanReceiveStation(60001) &&
                    (parrotSlot >= 0 && distance > 12f && distance < 45f || hookSlot >= 0 && distance > 4f && distance < 20f && sight.Target.Passenger.Ship == crew.Ship.Body);
                IBotAction combat = utility ? new BotUtilityAction(player, parrotSlot >= 0 && distance > 12f ? InventoryItem.BombParrot : InventoryItem.GrapplingHook,
                    parrotSlot >= 0 && distance > 12f ? parrotSlot : hookSlot, sight.Target) : areaThrow ? new BotAreaThrowAction(player, sight.Target, areaSlot) : throwing ?
                    new BotSwordfishAction(player, sight.Target, throwingSlot) : new BotPersonalCombatAction(player, sight.Target);
                if (throwing || areaThrow || utility) player.NextBotThrow = Time.time + 20f;
                player.AssignBotJob(throwing || areaThrow || utility ? 60001 : 60000, combat);
                if (!player.BotTaskRunning) player.FinishBotTask();
            }
        }
    }
}
