using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        void AssignArtillery(BotCrew crew)
        {
            if (crew.Ship.IsSinking || crew.EscapeZone || Time.time < crew.NextArtilleryPlan) return;
            crew.NextArtilleryPlan = Time.time + .15f;
            var network = crew.Ship.GetComponent<NetworkCannon>();
            if (network == null || network.Crate == null || network.Crate.Cannons.Count == 0) return;
            for (int scan = 0; scan < Mathf.Min(8, network.Crate.Cannons.Count); scan++)
            {
                int index = crew.ArtilleryCursor++ % network.Crate.Cannons.Count;
                var cannon = network.Crate.Cannons[index];
                if (cannon == null || crew.Reserved.Contains(70000 + cannon.Index)) continue;
                if (network.HasBoarding(cannon.Index) && Time.time - cannon.LastHumanControlTime >= Config.BotMotion.HumanStationGrace)
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
                    if (candidate == null || candidate.IsSinking || candidate.TeamId.Value == crew.Ship.TeamId.Value) continue;
                    var delta = candidate.transform.position - cannon.transform.position;
                    float distance = delta.sqrMagnitude;
                    if (distance > 160f * 160f) continue;
                    float score = distance;
                    if (crew.LastDamagedBy == candidate) score *= 0.4f;
                    if (score >= best) continue;
                    var local = cannon.transform.InverseTransformDirection(delta);
                    if (Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) > cannon.MaxTraverse + 10f) continue;
                    if (++probes > 3) continue;
                    if (!BotCannonStation.Visible(worker, candidate)) continue;
                    target = candidate; best = score;
                }
                if (target == null) continue;
                crew.DangerUntil = Time.time + 3f;
                worker.BotCandidates = $"Пушка {cannon.Index + 1}; видимый корабль команды {target.TeamId.Value}; дальность {Mathf.Sqrt(best):F0} м";
                if (worker.BotTaskRunning) worker.PreemptBotTask("Боевой пост: вести огонь по вражескому кораблю");
                worker.AssignBotStation(key, new BotCannonStation(cannon, crew.Ship, target), "Видимый вражеский корабль в секторе пушки; проверка упреждения и дуги");
                if (!worker.BotTaskRunning) worker.FinishBotTask();
                if (worker.BotTaskRunning) crew.Reserved.Add(key);
            }
        }

        internal bool BotPersonalShotSafe(NetworkPlayer player, Vector3 start, Vector3 end, bool melee, float spread)
        {
            var delta = end - start;
            float length = Mathf.Min(delta.magnitude, melee ? 2f : 12f);
            var direction = delta.normalized;
            foreach (var ally in players.Values)
            {
                if (ally == null || ally == player || ally.Motor.IsDead || ally.TeamId.Value != player.TeamId.Value) continue;
                var point = ally.transform.position + Vector3.up;
                float along = Vector3.Dot(point - start, direction);
                if (along < 0f || along > length) continue;
                float radius = melee ? 1.0f : Mathf.Min(.5f + Mathf.Tan(Mathf.Min(spread, 3.5f) * Mathf.Deg2Rad) * along, 1.2f);
                if ((point - start - direction * along).sqrMagnitude < radius * radius) return false;
            }
            return true;
        }

        bool CloseShipContact(NetworkShip ship, NetworkShip other)
        {
            if (other == null || other == ship || other.IsSinking || other.TeamId.Value == ship.TeamId.Value) return false;
            float range = ship.CollisionRadius + other.CollisionRadius + 12f;
            return Vector3.ProjectOnPlane(other.transform.position - ship.transform.position, Vector3.up).sqrMagnitude <= range * range;
        }

        bool CannonCoversContact(BotCrew crew, NetworkShip target)
        {
            var network = crew.Ship.GetComponent<NetworkCannon>();
            if (network == null || network.Crate == null) return false;
            foreach (var cannon in network.Crate.Cannons)
            {
                if (cannon == null || !cannon.gameObject.activeInHierarchy || cannon.IsLoaded && !BotCannonAmmoPolicy.Supported(cannon.LoadedAmmo)) continue;
                if (network.HasBoarding(cannon.Index))
                { if (BotBoardingAdvantage(crew.Ship, target)) return true; continue; }
                bool blocked = false;
                foreach (var member in crew.Members)
                    if (member != null && (member.BotStationDeferred(70000 + cannon.Index) || member.BotTaskRunning && member.BotTaskKey == 70000 + cannon.Index && member.BotArtilleryBlocked)) blocked = true;
                if (blocked) continue;
                var delta = target.transform.position + Vector3.up * 3f - cannon.transform.position;
                if (delta.sqrMagnitude < 15f * 15f || delta.sqrMagnitude > 160f * 160f) continue;
                var local = cannon.transform.InverseTransformDirection(delta);
                if (Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) <= cannon.MaxTraverse) return true;
            }
            return false;
        }

        void ObserveBotCombat(BotCrew crew)
        {
            bool alert = false;
            crew.ContactCombat = false;
            foreach (var ship in NetworkShip.ActiveShips)
                if (CloseShipContact(crew.Ship, ship)) { alert = true; if (!CannonCoversContact(crew, ship)) crew.ContactCombat = true; }
                
            NetworkPlayer boarder = null;
            Vector3 boarderPos = default;
            
            for (int i = 0; i < crew.Members.Count; i++)
            {
                crew.CombatObserverCursor %= crew.Members.Count;
                var observer = crew.Members[crew.CombatObserverCursor++];
                if (observer == null || !observer.IsBot.Value || observer.Motor.IsDead) continue;
                observer.BotVision.Observe(observer, players.Values, alert);
                if (observer.BotVision.Visible)
                {
                    crew.DangerUntil = Time.time + 3f;
                    if (observer.BotVision.Target != null && observer.BotVision.Target.Passenger.Ship == crew.Ship.Body)
                    {
                        boarder = observer.BotVision.Target;
                        boarderPos = observer.BotVision.LastPosition;
                    }
                }
                if (observer.BotVision.Visible && observer.BotTaskKey == 52000)
                    observer.PreemptBotTask("Рыбалка прервана: замечен противник");
            }
            
            if (boarder != null)
            {
                foreach (var member in crew.Members)
                {
                    if (member == null || !member.IsBot.Value || member.Motor.IsDead) continue;
                    member.BotVision.ReceiveCallout(boarder, boarderPos);
                }
            }
        }
        void AssignPersonalCombat(BotCrew crew)
        {
            if (crew.Ship.IsSinking) return;
            var assignedTargets = new HashSet<NetworkPlayer>();
            foreach (var m in crew.Members)
                if (m != null && m.BotTaskRunning && (m.BotTaskKey == 60000 || m.BotTaskKey == 60001) && m.BotVision.Target != null)
                    assignedTargets.Add(m.BotVision.Target);

            foreach (var player in crew.Members)
            {
                if (player == null || !player.IsBot.Value || player.BotNeedsShipWait) continue;
                var sight = player.BotVision;
                if (!sight.Visible || Time.time - sight.LastSeen > 2f) continue;
                NetworkPlayer targetEnemy = sight.Target;
                if (!BotCombatMemory.Enemy(player, targetEnemy)) continue;
                if (assignedTargets.Contains(targetEnemy))
                {
                    float closestAlt = float.PositiveInfinity;
                    foreach (var other in players.Values)
                    {
                        if (other == null || other.Motor.IsDead || other.TeamId.Value == player.TeamId.Value) continue;
                        if (other == targetEnemy || !BotCombatMemory.CanSee(player, other)) continue;
                        float d = (other.transform.position - player.transform.position).sqrMagnitude;
                        if (d < closestAlt) { closestAlt = d; targetEnemy = other; }
                    }
                }
                assignedTargets.Add(targetEnemy);
                bool contactFight = CloseShipContact(crew.Ship, targetEnemy.Ship) && !CannonCoversContact(crew, targetEnemy.Ship);
                float separation = (sight.LastPosition - player.transform.position).sqrMagnitude;
                bool boarder = targetEnemy.Passenger.Ship == crew.Ship.Body;
                if (!boarder && !contactFight && separation > 12f * 12f) continue;
                bool protectedWork = player.BotHasManualTask || player.BotTaskKey == -1 || player.BotTaskKey == 53000 || player.BotTaskKey == 51000 ||
                    player.BotTaskKey >= 100000 || player.BotTaskKey >= 0 && player.BotTaskKey < 1000 && (crew.UrgentSails || crew.Pilot.AvoidingCollision);
                bool fighting = player.BotTaskKey == 60000 || player.BotTaskKey == 60001;
                if (player.BotTaskRunning && !fighting && !protectedWork && (contactFight || separation < 10f * 10f) && player.BotCanPlanReplacement(60000))
                {
                    if (player.BotTaskKey >= 70000 && player.BotTaskKey < 80000) player.DeferBotStation(player.BotTaskKey, 5f);
                    player.PreemptBotTask("Тесный контакт: пушки не достают, атаковать экипаж личным оружием");
                }
                if (!player.BotCanReceiveStation(60000)) continue;
                player.BotCandidates = $"Цель {targetEnemy.ParticipantId.Value}: видна лично; задержка реакции и проверка союзников";
                int throwingSlot = BotSwordfishAction.Slot(player);
                int areaSlot = BotAreaThrowAction.Slot(player);
                int parrotSlot = BotUtilityAction.Slot(player, InventoryItem.BombParrot);
                int hookSlot = BotUtilityAction.Slot(player, InventoryItem.GrapplingHook);
                float distance = Vector3.Distance(player.transform.position, sight.LastPosition);
                bool throwing = throwingSlot >= 0 && distance >= 5f && distance <= 30f && Time.time >= player.NextBotThrow && player.BotCanReceiveStation(60001);
                bool areaThrow = !throwing && areaSlot >= 0 && distance >= 8f && distance <= 30f && Time.time >= player.NextBotThrow && player.BotCanReceiveStation(60001);
                bool utility = !throwing && !areaThrow && Time.time >= player.NextBotThrow && player.BotCanReceiveStation(60001) &&
                    (parrotSlot >= 0 && distance > 12f && distance < 45f || hookSlot >= 0 && distance > 4f && distance < 20f && targetEnemy.Passenger.Ship == crew.Ship.Body);
                IBotAction combat = utility ? new BotUtilityAction(player, parrotSlot >= 0 && distance > 12f ? InventoryItem.BombParrot : InventoryItem.GrapplingHook,
                    parrotSlot >= 0 && distance > 12f ? parrotSlot : hookSlot, targetEnemy) : areaThrow ? new BotAreaThrowAction(player, targetEnemy, areaSlot) : throwing ?
                    new BotSwordfishAction(player, targetEnemy, throwingSlot) : new BotPersonalCombatAction(player, targetEnemy);
                if (throwing || areaThrow || utility) player.NextBotThrow = Time.time + 20f;
                player.AssignBotJob(throwing || areaThrow || utility ? 60001 : 60000, combat);
                if (!player.BotTaskRunning) player.FinishBotTask();
            }
        }
    }
}
