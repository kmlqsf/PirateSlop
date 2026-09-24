using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        sealed class BotCrew
        {
            public NetworkShip Ship;
            public SailSystem Sails;
            public readonly List<NetworkPlayer> Members = new();
            public readonly BotSeaPilot Pilot = new();
            public readonly HashSet<int> Reserved = new();
            public bool Human;
            public float Updated;
            public int CaptainNumber;
            public bool Steering;
            public float DangerUntil;
            public float CombatUntil;
            public bool IsInCombat => Time.time < CombatUntil;
            public NetworkShip LastDamagedBy;
            public void TriggerCombatAlert(NetworkShip attacker = null)
            {
                CombatUntil = Time.time + 10f;
                DangerUntil = Time.time + 10f;
                if (attacker != null) LastDamagedBy = attacker;
                foreach (var member in Members)
                {
                    if (member == null || !member.IsBot.Value) continue;
                    if (member.BotTaskKey == 800000 || member.BotTaskKey == 52000)
                        member.PreemptBotTask("Боевая тревога: отмена мирных действий");
                }
            }
            public string RevivalStatus = "";
            public int RescueRepairSection = -1;
            public int NavigationRepairSection = -1;
            public float NextRescuePlan;
            public bool FloodEmergency, ZoneThreat, EscapeZone, CriticalFlood, ContactCombat;
            public bool UrgentSails => Sails != null && Pilot.PlannedSails <= 0f && Sails.EffectiveDeploy > .05f;
            public string PriorityReason;
            public float NextAmmoPlan;
            public int CannonSiteCursor;
            public float NextCannonPlan;
            public float NextMaintenancePlan;
            public int RepairCursor, RepairFragmentCursor, AmmoCursor;
            public int LootCursor, LootSearchCursor;
            public int CombatObserverCursor;
            public float NextArtilleryPlan;
            public int ArtilleryCursor;
            public float NextLootPlan, NextLootSearch, LootMissionUntil;
            public FishNet.Object.NetworkObject LootMission;
            public Vector3 LootAnchorage;
            public readonly Dictionary<int, float> LootRetry = new();
            public bool SailWorkReady => Time.time - Updated < 2f &&
                (Pilot.PlannedSails <= 0f || Steering);
            public float SailTarget => Time.time - Updated < 2f ? Pilot.PlannedSails : 0f;
            public float RudderTarget => Time.time - Updated < 2f ? Pilot.Rudder : 0f;
        }

        readonly List<BotCrew> botCrews = new();
        public void NotifyCombatDamage(NetworkShip damagedShip, GameObject attacker)
        {
            if (damagedShip == null) return;
            NetworkShip attackerShip = attacker != null ? attacker.GetComponentInParent<NetworkShip>() : null;
            foreach (var crew in botCrews)
            {
                if (crew.Ship == damagedShip) crew.TriggerCombatAlert(attackerShip);
                if (attackerShip != null && crew.Ship == attackerShip) crew.TriggerCombatAlert(damagedShip);
            }
        }
        static readonly Unity.Profiling.ProfilerMarker botCrewMarker = new("Bots.Crews");
        float nextBotCrewTick, nextBotCrewRoster;
        int botCrewCursor;

        void TickBotTasks()
        {
            if (manager == null || !manager.ServerManager.Started || Time.time < nextBotCrewTick) return;
            using var sample = botCrewMarker.Auto();
            nextBotCrewTick = Time.time;
            if (Time.time >= nextBotCrewRoster)
            {
                nextBotCrewRoster = Time.time + .5f;
                foreach (var crew in botCrews) { crew.Members.Clear(); crew.Human = false; }
                foreach (var player in players.Values)
                {
                    if (player == null || player.Ship == null) continue;
                    BotCrew crew = null;
                    foreach (var existing in botCrews) if (existing.Ship == player.Ship) { crew = existing; break; }
                    if (crew == null)
                    {
                        crew = new BotCrew { Ship = player.Ship, Sails = player.Ship.GetComponent<SailSystem>() };
                        botCrews.Add(crew);
                    }
                    crew.Members.Add(player);
                    if (!player.IsBot.Value) crew.Human = true;
                }
                botCrews.RemoveAll(c => c.Ship == null || c.Members.Count == 0);
            }
            if (botCrews.Count == 0) return;
            int crewBudget = Mathf.Clamp(Mathf.CeilToInt(botCrews.Count * Time.unscaledDeltaTime / .05f), 1, 4);
            for (int i = 0; i < Mathf.Min(crewBudget, botCrews.Count); i++)
            {
                botCrewCursor %= botCrews.Count;
                TickBotCrew(botCrews[botCrewCursor++]);
            }
        }

        void TickBotCrew(BotCrew crew)
        {
            var ship = crew.Ship;
            if (ship == null) return;
            crew.Updated = Time.time;
            crew.Reserved.Clear();
            int available = 0;
            bool manualHelm = false, outsideWork = false;
            foreach (var player in crew.Members)
            {
                if (player == null || !player.IsBot.Value) continue;
                player.FinishBotTask();
                player.RecoverBotFromWater();
                outsideWork |= player.BotNeedsShipWait;
                if (ship.IsSinking || crew.Human && player.BotTaskKey < 1000)
                    player.PreemptBotTask("Командование у человека или корабль потерян");
                if (!player.Motor.IsDead && !player.Motor.IsSwimming && player.Passenger.Ship == ship.Body) available++;
                if (player.BotTaskRunning && player.BotTaskKey != -2) crew.Reserved.Add(player.BotTaskKey);
                if (player.BotTaskRunning && player.BotHasManualTask) manualHelm = true;
            }
            var flooding = ship.GetComponent<ShipFlooding>();
            crew.FloodEmergency = flooding != null && (flooding.OpenImpactCount > 0 || flooding.Level > .25f);
            if (flooding != null)
                foreach (var breach in flooding.Breaches) if (breach.Area > 0f) { crew.FloodEmergency = true; break; }
            crew.CriticalFlood = flooding != null && flooding.Level >= .50f;
            float radius = new Vector2(ship.transform.position.x, ship.transform.position.z).magnitude;
            crew.ZoneThreat = radius > Mathf.Max(10f, SafeRadius(30f) - 40f);
            crew.EscapeZone = crew.ZoneThreat && (!crew.FloodEmergency || flooding == null || flooding.Level < .65f);
            crew.PriorityReason = crew.EscapeZone ? "Выживание: выход из зоны" : crew.FloodEmergency ? "Выживание: устранить течь" : "Возрождение, защита и снабжение";
            crew.Pilot.EscapeZone = crew.EscapeZone;
            foreach (var member in crew.Members)
            {
                if (member == null || !member.IsBot.Value) continue;
                member.BotRecallOutside = crew.EscapeZone || (crew.FloodEmergency || Time.time < crew.DangerUntil) && member.BotTaskKey == 800000;
                if (crew.EscapeZone && member.BotTaskRunning && !member.BotNeedsShipWait && member.BotTaskKey >= 1000 &&
                    member.BotTaskKey != 53000 && member.BotTaskKey != 51000 && member.BotTaskKey < 100000 &&
                    (!member.BotVision.Visible || (member.BotVision.LastPosition - member.transform.position).sqrMagnitude > 36f))
                    member.PreemptBotTask("Выход из зоны: освободить экипаж для руля и парусов");
            }
            bool canCruise = (!crew.FloodEmergency || crew.EscapeZone) && !crew.Human && !manualHelm && available >= 2 && !ship.IsSinking && ship.Helm != null && ship.Helm.StructurallyAvailable;
            ObserveBotCombat(crew);
            if (!canCruise)
                foreach (var player in crew.Members)
                    if (player != null && player.BotTaskKey == -1) player.PreemptBotTask("Автономное плавание приостановлено");
            bool steering = false;
            NetworkPlayer observer = null;
            crew.CaptainNumber = 0;
            foreach (var player in crew.Members)
            {
                if (observer == null && player != null && player.IsBot.Value && !player.Motor.IsDead && player.Passenger.Ship == ship.Body) observer = player;
                if (player != null && player.IsBot.Value && player.BotTaskKey == -1 && player.BotTaskRunning)
                {
                    observer = player;
                    crew.CaptainNumber = player.BotNumber;
                    if (ship.Helm != null && ship.Helm.IsControlledBy(player.Motor)) steering = true;
                }
            }
            crew.Steering = steering;
            PlanLootDestination(crew, outsideWork);
            if (crew.EscapeZone && canCruise && (!outsideWork || radius > SafeRadius(5f) - 20f)) crew.Pilot.Tick(ship, Config.BotMotion, steering, observer);
            else if (crew.FloodEmergency) crew.Pilot.Stop("Активная течь: остановиться и спасать корабль");
            else if (outsideWork) crew.Pilot.Stop("Моряк выполняет задачу вне корабля; убрать паруса и дождаться возвращения");
            else if (canCruise) crew.Pilot.Tick(ship, Config.BotMotion, steering, observer);
            else crew.Pilot.Stop(crew.Human ? "Командование человека; палубные работы разрешены" :
                manualHelm ? "Штурвал управляется командой F10" : "Недостаточно экипажа для одновременной работы с рулём и парусами");
            if (crew.Pilot.CombatTarget != null) crew.DangerUntil = Time.time + 2f;
            if (Time.time < crew.DangerUntil)
                foreach (var member in crew.Members)
                    if (member != null && member.BotTaskKey == 52000) member.PreemptBotTask("Рыбалка прервана: корабль вступает в бой");
            if (Time.time >= crew.NextRescuePlan)
            {
                crew.NextRescuePlan = Time.time + .5f;
                int previousRepair = crew.RescueRepairSection;
                crew.RescueRepairSection = FindRescueRepair(crew);
                int previousNavigationRepair = crew.NavigationRepairSection;
                crew.NavigationRepairSection = FindNavigationRepair(crew);
                if (previousNavigationRepair >= 0 && ship.Helm != null && ship.Helm.StructurallyAvailable)
                    foreach (var member in crew.Members) if (member != null) member.AllowBotStationRetry(-1);
                if (previousRepair >= 0 && crew.RescueRepairSection < 0)
                    foreach (var member in crew.Members) if (member != null) member.AllowBotStationRetry(53000);
            }
            if (crew.CriticalFlood || crew.RescueRepairSection >= 0) AssignMaintenance(crew, true);
            AssignConsumables(crew);
            AssignRevival(crew);
            AssignMaintenance(crew, true);
            if (!ship.IsSinking && flooding != null && flooding.Level > .30f && (crew.RescueRepairSection < 0 && flooding.OpenImpactCount == 0))
            {
                foreach (var member in crew.Members)
                {
                    if (member == null || !member.IsBot.Value || member.Motor.IsDead || member.BotNeedsShipWait || member.BotTaskKey == -1) continue;
                    if (!member.BotCanReceiveStation(55000)) continue;
                    member.AssignBotJob(55000, new BotBucketAction(member));
                    break;
                }
            }
            var armament = ship.GetComponent<NetworkCannon>();
            bool needsFirstCannon = armament != null && armament.Crate != null && armament.Crate.Cannons.Count == 0;
            if (canCruise && crew.Pilot.AvoidingCollision && !crew.Reserved.Contains(-1) && !ship.Helm.IsControlling)
                foreach (var member in crew.Members)
                    if (member != null && member.BotTaskRunning && member.BotCanPlanReplacement(-1) &&
                        (member.BotTaskKey == 52000 || member.BotTaskKey >= 1000 && member.BotTaskKey < 50000))
                    { member.PreemptBotTask("Предотвратить столкновение: немедленно занять штурвал"); break; }
            if (canCruise && ship.Helm != null && !crew.Reserved.Contains(-1) && !ship.Helm.IsControlling &&
                Time.time - ship.Helm.LastHumanControlTime >= Mathf.Max(5f, Config.BotMotion.HumanStationGrace))
                AssignNearest(crew, -1, new BotHelmStation(ship.Helm, () => crew.RudderTarget), "Нужен рулевой автономного экипажа");
            if (needsFirstCannon) AssignCannonWork(crew);
            AssignArtillery(crew);
            AssignMaintenance(crew);
            if (!crew.Human && !manualHelm && !ship.IsSinking && crew.Sails != null)
            {
                bool sailWorker = false;
                foreach (var member in crew.Members)
                    if (member != null && member.BotTaskRunning && member.BotTaskKey >= 0 && member.BotTaskKey < 1000) sailWorker = true;
                if (!sailWorker && crew.Pilot.PlannedSails <= 0f && crew.Sails.EffectiveDeploy > .05f &&
                    (!needsFirstCannon || crew.Pilot.AvoidingCollision))
                    foreach (var member in crew.Members)
                        if (member != null && member.BotTaskRunning && !member.BotNeedsShipWait &&
                            (member.BotTaskKey == 52000 || member.BotTaskKey >= 1000 && member.BotTaskKey < 50000))
                        { member.PreemptBotTask("Убрать паруса для безопасной остановки"); break; }
                for (int i = 0; i < crew.Sails.RopeCount; i++)
                {
                    if (crew.Reserved.Contains(i) || Time.time - crew.Sails.LastHumanControlTime(i) < Mathf.Max(5f, Config.BotMotion.HumanStationGrace) ||
                        Mathf.Abs(crew.Sails.Tension(i) - crew.Pilot.PlannedSails) < .06f) continue;
                    var station = new BotSailStation(crew.Sails, i, () => crew.SailTarget, () => crew.SailWorkReady);
                    if (!station.Available || station.Busy) continue;
                    AssignNearest(crew, i, station, crew.Pilot.Reason);
                    if (crew.Pilot.CombatTarget != null && !crew.Pilot.AvoidingCollision && crew.Reserved.Contains(i)) break;
                }
            }
            if (!crew.Human && !manualHelm && !ship.IsSinking)
            {
                bool wantsFullStop = outsideWork || crew.CriticalFlood ||
                    (crew.Pilot.LootDestination.HasValue &&
                     new Vector2(ship.transform.position.x - crew.Pilot.LootDestination.Value.x, ship.transform.position.z - crew.Pilot.LootDestination.Value.z).sqrMagnitude < 22f * 22f &&
                     crew.Pilot.PlannedSails <= 0f);

                if (wantsFullStop && !ship.AnchorDropped && Mathf.Abs(ship.Motor.Speed) < 3.5f)
                {
                    if (!crew.Reserved.Contains(54000))
                        AssignNearest(crew, 54000, new BotCapstanDropStation(ship), "Полная остановка корабля: сбросить якорь");
                }
                else if (!wantsFullStop && (ship.AnchorDropped || ship.AnchorRaiseProgress < 0.99f))
                {
                    if (!crew.Reserved.Contains(54001))
                        AssignNearest(crew, 54001, new BotCapstanRaiseStation(ship, 0), "Сняться с якоря для продолжения плавания");
                    if (available >= 3 && !crew.Reserved.Contains(54002))
                        AssignNearest(crew, 54002, new BotCapstanRaiseStation(ship, 1), "Помощь в подъёме якоря");
                }
            }
            AssignPersonalCombat(crew);
            AssignIslandLoot(crew);
            AssignCannonWork(crew);
            if (!crew.ZoneThreat && !crew.FloodEmergency && !crew.IsInCombat && crew.Pilot.CombatTarget == null && Time.time >= crew.DangerUntil && !ship.IsSinking)
                foreach (var member in crew.Members)
                {
                    if (member == null || !member.BotCanReceiveStation(52000) || Time.time < member.NextBotFishing || member.BotVision.Visible ||
                        member.GetComponent<PlayerInventory>().EmptySlot() < 0) continue;
                    int rod = BotUtilityAction.Slot(member, InventoryItem.Rod);
                    if (rod < 0) continue;
                    member.NextBotFishing = Time.time + 5f;
                    var ladders = ship.GetComponentsInChildren<ShipLadder>();
                    for (int i = 0; i < ladders.Length; i++)
                    {
                        var ladder = ladders[(member.BotNumber + i) % ladders.Length];
                        if (!ladder.isActiveAndEnabled || !ladder.BoardingAccess || ladder.RopeClimb) continue;
                        member.AssignBotStation(52000, new BotFishingStation(ladder, rod), "Свободный моряк ловит рыбу у борта корабля"); break;
                    }
                    break;
                }
            foreach (var player in crew.Members)
            {
                if (player == null || !player.IsBot.Value) continue;
                bool changed = player.lastAssignmentTaskKey != player.BotTaskKey
                    || player.lastAssignmentCaptain != crew.CaptainNumber
                    || player.lastAssignmentTeam != player.TeamId.Value
                    || !ReferenceEquals(player.lastAssignmentPriority, crew.PriorityReason)
                    || !ReferenceEquals(player.lastAssignmentPilotReason, crew.Pilot.Reason)
                    || !string.Equals(player.lastAssignmentRevival, crew.RevivalStatus, System.StringComparison.Ordinal);
                if (changed)
                {
                    player.lastAssignmentTaskKey = player.BotTaskKey;
                    player.lastAssignmentCaptain = crew.CaptainNumber;
                    player.lastAssignmentTeam = player.TeamId.Value;
                    player.lastAssignmentPriority = crew.PriorityReason;
                    player.lastAssignmentPilotReason = crew.Pilot.Reason;
                    player.lastAssignmentRevival = crew.RevivalStatus;
                    string taskName = player.BotTaskKey == -1 ? "рулевой" : player.BotTaskKey == 50000 ? "лечение рыбой" : player.BotTaskKey == 51000 ? "доставка рома" : player.BotTaskKey == 52000 ? "рыбалка" : player.BotTaskKey == 53000 ? "колокол: возрождение" : player.BotTaskKey == 54000 ? "сброс якоря" : player.BotTaskKey == 54001 || player.BotTaskKey == 54002 ? "подъём якоря" : player.BotTaskKey == 60001 ? "метательный предмет" : player.BotTaskKey == 60000 ? "личный бой" : player.BotTaskKey >= 70000 && player.BotTaskKey < 80000 ? "артиллерист" : player.BotTaskKey == 900000 ? "возвращение" : player.BotTaskKey == 800000 ? "островная вылазка" : player.BotTaskKey >= 100000 ? "ремонт" : player.BotTaskKey >= 2000 ? "снабжение пушки" : player.BotTaskKey >= 1000 ? "оснащение корабля" : player.BotTaskKey >= 0 ? crew.Sails.RopeName(player.BotTaskKey) : "свободен";
                    player.BotAssignment = $"Экипаж {player.TeamId.Value}; капитан БОТ{crew.CaptainNumber}; {taskName}; {crew.PriorityReason}; {crew.Pilot.Reason}; {crew.RevivalStatus}";
                }
                if (!player.BotTaskRunning)
                    RecordBotDecision(player.BotNumber, "Ожидание задачи", crew.Pilot.Reason, player.BotStatus,
                        player.BotCanReceiveTask ? "Готов к назначению" : player.BotTaskBlockReason,
                        player.BotVision.Status, player.BotAssignment,
                        player.BotLastFailure, player.BotCandidates);
            }
        }

        int FindRescueRepair(BotCrew crew)
        {
            bool waiting = false;
            foreach (var member in crew.Members)
                if (member != null && member.Motor.IsDead && !member.Eliminated.Value) waiting = true;
            if (!waiting) return -1;
            var bell = crew.Ship.transform.Find("CrewBell");
            var destruction = crew.Ship.GetComponent<ShipDestruction>();
            if (bell == null || destruction == null) return -1;
            if (crew.RescueRepairSection >= 0 && crew.RescueRepairSection < destruction.Sections.Length &&
                destruction.Sections[crew.RescueRepairSection] != null && destruction.Sections[crew.RescueRepairSection].RemovedFragments != 0)
                foreach (var member in crew.Members)
                    if (member != null && member.BotTaskRunning && member.BotTaskKey == 100000 + crew.RescueRepairSection) return crew.RescueRepairSection;
            bool blocked = !bell.gameObject.activeInHierarchy;
            foreach (var member in crew.Members)
                if (member != null && member.IsBot.Value && member.BotStationDeferred(53000) &&
                    (member.BotLastFailure.Contains("Путь") || member.BotLastFailure.Contains("позиции") || member.BotLastFailure.Contains("Застревание"))) blocked = true;
            if (!blocked) return -1;
            int selected = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < destruction.Sections.Length; i++)
            {
                var section = destruction.Sections[i];
                if (section == null || section.RemovedFragments == 0) continue;
                var type = destruction.Definition(section.SectionId).Type;
                if (type != ShipSectionType.Deck && type != ShipSectionType.Stairs && type != ShipSectionType.Fitting && type != ShipSectionType.Capstan) continue;
                for (int fragment = 0; fragment < Mathf.Min(64, section.RepairCount); fragment++)
                {
                    if ((section.RemovedFragments & (1UL << fragment)) == 0) continue;
                    var anchor = section.RepairTransform(fragment);
                    if (anchor == null) continue;
                    var point = anchor.TransformPoint(section.RepairBounds(fragment).center);
                    foreach (var member in crew.Members)
                    {
                        if (member == null || !member.IsBot.Value || member.Motor.IsDead || member.Passenger.Ship != crew.Ship.Body) continue;
                        var from = member.transform.position;
                        var offset = bell.position - from;
                        var nearest = from + offset * Mathf.Clamp01(Vector3.Dot(point - from, offset) / Mathf.Max(.01f, offset.sqrMagnitude));
                        float distance = (point - nearest).sqrMagnitude;
                        if (distance > 25f || distance >= best) continue;
                        best = distance; selected = i;
                    }
                }
            }
            return selected;
        }

        void AssignRevival(BotCrew crew)
        {
            crew.RevivalStatus = "";
            NetworkPlayer casualty = null;
            foreach (var member in crew.Members)
                if (member != null && member.Motor.IsDead && !member.Eliminated.Value) { casualty = member; break; }
            if (casualty == null) return;
            crew.RevivalStatus = "Товарищ ждёт колокола";
            if (crew.Ship.IsSinking) { crew.RevivalStatus += ": корабль тонет"; return; }
            if (crew.Ship.RumCount <= 0) { crew.RevivalStatus += ": на корабле нет рома"; return; }
            if (crew.RescueRepairSection >= 0 && crew.Reserved.Contains(100000 + crew.RescueRepairSection))
            { crew.RevivalStatus += ": восстанавливается проход или опора колокола"; return; }
            if (crew.Reserved.Contains(53000)) { crew.RevivalStatus += ": спасатель назначен"; return; }
            var root = crew.Ship.transform.Find("CrewBell");
            if (root == null || !root.gameObject.activeInHierarchy) { crew.RevivalStatus += ": колокол недоступен"; return; }
            var bell = root.GetComponent<CrewBellMotion>();
            if (bell == null) bell = root.gameObject.AddComponent<CrewBellMotion>();
            if (bell.Holder != null) { crew.RevivalStatus += ": колокол занят"; return; }
            NetworkPlayer worker = null;
            float best = float.PositiveInfinity;
            foreach (var member in crew.Members)
            {
                if (member == null || !member.IsBot.Value || member.Motor.IsDead || member.Motor.IsSwimming || member.Motor.IsClimbing ||
                    member.Motor.IsKnockedBack || member.Passenger.Ship != crew.Ship.Body || member.BotNeedsShipWait || crew.CriticalFlood && member.BotTaskRunning && member.BotTaskKey >= 100000) continue;
                if (member.BotTaskKey == -1 && (Mathf.Abs(crew.Ship.Motor.Speed) > .1f || crew.Pilot.AvoidingCollision || !crew.Ship.AnchorDropped)) continue;
                if (member.BotVision.Visible && member.BotVision.Target != null &&
                    member.BotVision.Target.Passenger.Ship == crew.Ship.Body &&
                    (member.BotVision.LastPosition - member.transform.position).sqrMagnitude < 36f) continue;
                var bellHandler = member.GetComponent<NetworkCrewBell>();
                if (bellHandler == null || !bellHandler.BotReadyToRing) continue;
                float distance = (member.transform.position - bell.GripPoint).sqrMagnitude;
                if (member.BotTaskRunning && member.BotTaskKey == -1) distance += 10000f;
                if (distance >= best) continue;
                worker = member; best = distance;
            }
            if (worker == null) { crew.RevivalStatus += ": нет доступного спасателя или повтор подхода отложен"; return; }
            if (worker.BotTaskRunning) worker.PreemptBotTask("Товарищ ждёт возрождения: направиться к колоколу");
            if (!worker.BotCanReceiveStation(53000)) return;
            worker.AssignBotStation(53000, new BotBellStation(crew.Ship, casualty, bell), "Вернуть погибшего товарища за один ром");
            if (worker.BotTaskRunning) { crew.Reserved.Add(53000); crew.RevivalStatus += $": направлен БОТ{worker.BotNumber}"; }
            else { worker.FinishBotTask(); crew.RevivalStatus += ": " + worker.BotLastFailure; }
        }

        void PlanLootDestination(BotCrew crew, bool outsideWork)
        {
            if (crew.Human || crew.Ship.IsSinking || crew.ZoneThreat || crew.FloodEmergency || outsideWork)
            { crew.Pilot.LootDestination = null; return; }
            if (crew.LootMission != null)
            {
                var chest = crew.LootMission.GetComponent<NetworkLootChest>();
                if (chest == null || !chest.BotLootCandidate || Time.time > crew.LootMissionUntil ||
                    new Vector2(chest.transform.position.x, chest.transform.position.z).magnitude > SafeRadius(120f) - 40f)
                {
                    crew.LootRetry[crew.LootMission.ObjectId] = Time.time + 90f;
                    crew.LootMission = null; crew.Pilot.LootDestination = null;
                }
                else { crew.Pilot.LootDestination = crew.LootAnchorage; return; }
            }
            if (Time.time < crew.NextLootSearch) return;
            crew.NextLootSearch = Time.time + .5f;
            bool room = false;
            foreach (var member in crew.Members)
                if (member != null && member.IsBot.Value && !member.Motor.IsDead && (member.GetComponent<PlayerInventory>().EmptySlot() >= 0 || member.GetComponent<NetworkWeapon>().CanAddItem(InventoryItem.Rum))) room = true;
            if (!room) return;
            var world = PirateSlop.World.ProceduralWorld.Instance;
            var chests = NetworkLootChest.ServerChests;
            if (world == null || !world.Ready || chests.Count == 0) return;
            float bestLootScore = float.PositiveInfinity;
            for (int i = 0; i < Mathf.Min(16, chests.Count); i++)
            {
                crew.LootSearchCursor %= chests.Count;
                var chest = chests[crew.LootSearchCursor++];
                if (chest == null || !chest.BotLootCandidate || chest.GetComponentInParent<NetworkShip>() != null ||
                    crew.LootRetry.TryGetValue(chest.ObjectId, out float retry) && Time.time < retry) continue;
                var point = chest.BotLootPoint;
                if (OceanSurface.Instance == null || chest.Kind == SeaLootKind.None && point.y < OceanSurface.Instance.SeaLevel + .1f ||
                    (point - crew.Ship.transform.position).sqrMagnitude > 450f * 450f ||
                    new Vector2(point.x, point.z).magnitude > SafeRadius(120f) - 40f) continue;
                var offset = Vector3.ProjectOnPlane(point - crew.Ship.transform.position, Vector3.up);
                float along = Vector3.Dot(offset, crew.Ship.transform.forward);
                float detour = (offset - crew.Ship.transform.forward * Mathf.Clamp(along, 0f, 450f)).magnitude;
                float score = offset.magnitude + detour * 2f;
                if (score >= bestLootScore) continue;
                var towardShip = -offset.normalized;
                float standOff = chest.Kind == SeaLootKind.Capture ? Mathf.Max(5f, chest.Catalog.CaptureRadius * .45f) : chest.Kind == SeaLootKind.None ? 65f : 35f;
                var anchorage = point + towardShip * standOff;
                anchorage.y = crew.Ship.transform.position.y;
                float heading = Mathf.Atan2(-towardShip.x, -towardShip.z) * Mathf.Rad2Deg;
                bool clear = true;
                for (int step = 1; step <= 6; step++)
                    if (!world.CanSail(Vector3.Lerp(crew.Ship.transform.position, anchorage, step / 6f), heading)) { clear = false; break; }
                if (!clear) continue;
                crew.LootMission = chest.NetworkObject;
                crew.LootMissionUntil = Time.time + 120f;
                crew.LootAnchorage = anchorage;
                crew.Pilot.LootDestination = anchorage;
                bestLootScore = score;
            }
        }

        void AssignIslandLoot(BotCrew crew)
        {
            if (crew.Human || crew.Ship.IsSinking || crew.ZoneThreat || crew.FloodEmergency || crew.Pilot.CombatTarget != null || crew.IsInCombat || Time.time < crew.DangerUntil || Time.time < crew.NextLootPlan) return;
            crew.NextLootPlan = Time.time + .5f;
            foreach (var member in crew.Members)
                if (member != null && (member.BotNeedsShipWait || member.BotVision.Visible && Time.time - member.BotVision.LastSeen < 3f || member.BotTaskRunning && (member.BotTaskKey >= 100000 || member.BotTaskKey >= 60000 && member.BotTaskKey < 80000 || member.BotHasManualTask))) return;
            NetworkPlayer worker = null;
            foreach (var member in crew.Members)
                if (member != null && member.IsBot.Value && (member.BotCanReceiveWork(800000) || member.BotCanPlanReplacement(800000) && member.BotTaskKey >= 1000 && member.BotTaskKey < 2000) && (member.GetComponent<PlayerInventory>().EmptySlot() >= 0 || member.GetComponent<NetworkWeapon>().CanAddItem(InventoryItem.Rum)))
                { worker = member; break; }
            if (worker == null) return;
            var chests = NetworkLootChest.ServerChests;
            var items = NetworkFish.ServerItems;
            int count = chests.Count + items.Count;
            for (int i = 0; i < Mathf.Min(24, count); i++)
            {
                crew.LootCursor %= count;
                int index = crew.LootCursor++;
                FishNet.Object.NetworkObject target;
                if (i == 0 && crew.LootMission != null) target = crew.LootMission;
                else
                if (index < chests.Count)
                {
                    var chest = chests[index];
                    if (chest == null || !chest.BotLootCandidate || chest.GetComponentInParent<NetworkShip>() != null) continue;
                    target = chest.NetworkObject;
                }
                else
                {
                    var item = items[index - chests.Count];
                    if (item == null || !item.Available || item.OnShip || !worker.GetComponent<NetworkWeapon>().CanAddItem(item.CurrentItem)) continue;
                    target = item.NetworkObject;
                }
                int id = target.ObjectId;
                if (crew.LootRetry.TryGetValue(id, out float retry) && Time.time < retry) continue;
                var targetChest = target.GetComponent<NetworkLootChest>();
                if (targetChest != null && targetChest.BotCapturePending) continue;
                var point = targetChest != null ? targetChest.BotLootPoint : target.transform.position;
                if (OceanSurface.Instance == null || (targetChest == null || targetChest.Kind == SeaLootKind.None) && point.y < OceanSurface.Instance.SeaLevel + .1f ||
                    (point - crew.Ship.transform.position).sqrMagnitude > 100f * 100f ||
                    new Vector2(point.x, point.z).magnitude > SafeRadius(120f) - 30f) continue;
                crew.LootRetry[id] = Time.time + 45f;
                if (crew.LootRetry.Count > 128) crew.LootRetry.Clear();
                worker.BotCandidates = $"Замечен островной лут в {Vector3.Distance(point, worker.transform.position):F0} м; один сборщик БОТ{worker.BotNumber}";
                if (worker.BotTaskRunning) worker.PreemptBotTask("Сбор припасов у ближайшей точки интереса");
                worker.AssignBotJob(800000, new BotIslandLootAction(worker, target));
                if (!worker.BotTaskRunning) worker.FinishBotTask();
                return;
            }
        }

        void AssignCannonWork(BotCrew crew)
        {
            if (crew.Ship.IsSinking || crew.ZoneThreat || crew.FloodEmergency || Time.time < crew.NextCannonPlan) return;
            crew.NextCannonPlan = Time.time + .25f;
            foreach (var member in crew.Members)
                if (member != null && member.BotTaskRunning && member.BotTaskKey >= 1000 && member.BotTaskKey < 2000) return;
            var cannon = crew.Ship.GetComponent<NetworkCannon>();
            var hints = Config.BotMotion.CannonSites;
            if (cannon == null || cannon.Crate == null || hints == null || hints.Length == 0) return;
            for (int probe = 0; probe < Mathf.Min(6, hints.Length); probe++)
            {
                int index = crew.CannonSiteCursor++ % hints.Length;
                int key = 1000 + index;
                NetworkPlayer selected = null;
                float best = float.PositiveInfinity;
                foreach (var player in crew.Members)
                {
                    if (player == null || !player.IsBot.Value) continue;
                    bool firstGun = cannon.Crate.Cannons.Count == 0;
                    if (!player.BotCanReceiveWork(key) && !((firstGun || player.GetComponent<PlayerInventory>().CannonSlots != 0) && player.BotCanPlanReplacement(key) &&
                        player.BotTaskKey >= 0 && player.BotTaskKey < 50000 &&
                        (player.BotTaskKey >= 1000 || !crew.UrgentSails && !crew.Pilot.AvoidingCollision) && !player.BotNeedsShipWait &&
                        (!player.BotVision.Visible || (player.BotVision.LastPosition - player.transform.position).sqrMagnitude > 144f))) continue;
                    var inventory = player.GetComponent<PlayerInventory>();
                    bool carrying = inventory.CannonSlots != 0;
                    if (!carrying && (!cannon.Crate.KitAvailable || inventory.EmptySlot() < 0)) continue;
                    float score = carrying ? -1f : Vector3.Distance(player.transform.position, cannon.Crate.Kit.transform.position);
                    if (score >= best) continue;
                    best = score; selected = player;
                }
                if (selected == null) continue;
                if (!BotCannonPlacement.Find(crew.Ship, hints[index], out var site))
                {
                    selected.BotCandidates = $"Место пушки {index + 1}: занято или нет ровной опоры; проверить следующее";
                    continue;
                }
                selected.BotCandidates = $"Установка пушки: БОТ{selected.BotNumber}, место {index + 1}; комплект и место зарезервированы";
                if (selected.BotTaskRunning) selected.PreemptBotTask("Первая пушка необходима для защиты корабля");
                selected.AssignBotJob(key, new BotInstallCannonAction(selected, cannon, site));
                if (!selected.BotTaskRunning) selected.FinishBotTask();
                if (selected.BotTaskRunning) return;
            }
        }

        NetworkPlayer MaintenanceWorker(BotCrew crew, int key, Vector3 point, bool repair)
        {
            NetworkPlayer selected = null;
            float best = float.PositiveInfinity;
            foreach (var player in crew.Members)
            {
                if (player == null || !player.IsBot.Value) continue;
                bool reload = key >= 2000 && key < 50000;
                bool combatWork = reload || key >= 70000 && key < 80000;
                bool replace = combatWork && player.BotTaskRunning && player.BotCanPlanReplacement(key) &&
                    (player.BotTaskKey >= 1000 && player.BotTaskKey < 2000 || crew.Pilot.CombatTarget != null && !crew.UrgentSails && player.BotTaskKey >= 0 && player.BotTaskKey < 1000);
                bool repairPriority = repair && (crew.FloodEmergency || crew.RescueRepairSection >= 0 || key == 100000 + crew.NavigationRepairSection || player.BotTaskKey == 52000 || player.BotTaskKey >= 1000 && player.BotTaskKey < 2000) && player.BotCanPlanReplacement(key) && player.BotTaskRunning &&
                    player.BotTaskKey >= 1000 && player.BotTaskKey < 100000 && player.BotTaskKey != 53000 && player.BotTaskKey != 51000 && !player.BotNeedsShipWait &&
                    (!player.BotVision.Visible || (player.BotVision.LastPosition - player.transform.position).sqrMagnitude > 36f);
                bool restoreNavigation = repair && crew.NavigationRepairSection >= 0 && key == 100000 + crew.NavigationRepairSection &&
                    player.BotCanPlanReplacement(key) && !player.BotNeedsShipWait &&
                    (player.BotTaskKey >= 0 && player.BotTaskKey < 1000 || player.BotTaskKey == -1 && !crew.Ship.Helm.StructurallyAvailable);
                if (player.BotTaskKey == -1 && (Mathf.Abs(crew.Ship.Motor.Speed) > .1f || crew.Pilot.AvoidingCollision || !crew.Ship.AnchorDropped)) continue;
                if (!player.BotCanReceiveWork(key) && !replace && !repairPriority && !restoreNavigation) continue;
                var inventory = player.GetComponent<PlayerInventory>();
                if (repair && BotMaintenanceAction.MalletSlot(inventory) < 0) continue;
                float score = (player.transform.position - point).sqrMagnitude;
                if (!repair && key >= 2000 && key < 60000)
                {
                    bool carrying = inventory.BallCount(PlayerInventory.AmmoSlot) > 0;
                    var ammo = carrying ? inventory.BallItem(PlayerInventory.AmmoSlot) : InventoryItem.Cannonball;
                    if (!BotCannonAmmoPolicy.Supported(ammo) || ammo == InventoryItem.BoardingHook && !BotBoardingAdvantage(crew.Ship, crew.Pilot.CombatTarget)) continue;
                    if (ammo != InventoryItem.Cannonball && crew.Pilot.CombatTarget == null) continue;
                    if (crew.Pilot.CombatTarget != null)
                    {
                        float distance = Vector3.Distance(crew.Ship.transform.position, crew.Pilot.CombatTarget.transform.position);
                        score -= BotCannonAmmoPolicy.Score(ammo, distance) * 100f;
                    }
                    if (carrying) score -= 100f;
                }
                if (score >= best) continue;
                best = score; selected = player;
            }
            return selected;
        }

        int FindNavigationRepair(BotCrew crew)
        {
            var destruction = crew.Ship.GetComponent<ShipDestruction>();
            if (destruction == null) return -1;
            int mast = -1;
            for (int i = 0; i < destruction.Sections.Length; i++)
            {
                var section = destruction.Sections[i];
                if (section == null) continue;
                var type = destruction.Definition(section.SectionId).Type;
                if (type == ShipSectionType.Helm && section.RemovedFragments != 0) return i;
                if (type == ShipSectionType.Capstan && section.RemovedFragments != 0) return i;
                if (mast < 0 && type == ShipSectionType.Mast && destruction.MastRepairPoint(section.SectionId, out _)) mast = i;
            }
            return mast;
        }

        int UrgentBreachSection(BotCrew crew, ShipDestruction destruction)
        {
            var flood = crew.Ship.GetComponent<ShipFlooding>();
            if (flood == null) return -1;
            int selected = -1;
            float largest = -1f;
            foreach (var breach in flood.Breaches)
                for (int i = 0; i < destruction.Sections.Length; i++)
                    if (destruction.Sections[i] != null && destruction.Sections[i].SectionId == breach.SectionId &&
                        destruction.Sections[i].RemovedFragments != 0 && !crew.Reserved.Contains(100000 + i) && breach.Area > largest)
                    { selected = i; largest = breach.Area; }
            return selected;
        }

        void AssignMaintenance(BotCrew crew, bool repairOnly = false)
        {
            if (crew.Ship.IsSinking || Time.time < (repairOnly ? crew.NextMaintenancePlan : crew.NextAmmoPlan)) return;
            if (repairOnly) crew.NextMaintenancePlan = Time.time + .15f;
            else crew.NextAmmoPlan = Time.time + .15f;
            int repairing = 0;
            foreach (var member in crew.Members)
                if (member != null && member.BotTaskRunning && member.BotTaskKey >= 100000 && member.BotTaskKey < 800000) repairing++;
            int repairLimit = crew.FloodEmergency && crew.Sails != null && crew.Sails.EffectiveDeploy < .05f ? 2 : 1;
            var destruction = crew.Ship.GetComponent<ShipDestruction>();
            bool rescueWaiting = false;
            foreach (var member in crew.Members)
                if (member != null && member.Motor.IsDead && !member.Eliminated.Value && crew.Ship.RumCount > 0) rescueWaiting = true;
            if (repairOnly && (!rescueWaiting || crew.Reserved.Contains(53000) || crew.CriticalFlood || crew.RescueRepairSection >= 0) && (!crew.EscapeZone || crew.NavigationRepairSection >= 0) && repairing < repairLimit && destruction != null && destruction.Sections.Length > 0)
            {
                int urgent = UrgentBreachSection(crew, destruction);
                for (int probe = 0; probe < Mathf.Min(12, destruction.Sections.Length); probe++)
                {
                    int index = crew.EscapeZone ? crew.NavigationRepairSection : probe == 0 && urgent >= 0 ? urgent :
                        probe <= 1 && crew.RescueRepairSection >= 0 ? crew.RescueRepairSection :
                        probe <= 2 && crew.NavigationRepairSection >= 0 ? crew.NavigationRepairSection : crew.RepairCursor++ % destruction.Sections.Length;
                    var section = destruction.Sections[index];
                    if (section == null) continue;
                    bool mast = destruction.Definition(section.SectionId).Type == ShipSectionType.Mast;
                    if (!mast && section.RemovedFragments == 0) continue;
                    int key = 100000 + index;
                    if (crew.Reserved.Contains(key)) continue;
                    int fragment = -1;
                    Vector3 point;
                    if (mast)
                    {
                        if (!destruction.MastRepairPoint(section.SectionId, out point)) continue;
                    }
                    else
                    {
                        int count = Mathf.Min(64, section.RepairCount);
                        for (int offset = 0; offset < count; offset++)
                        {
                            int i = (crew.RepairFragmentCursor + offset) % count;
                            if ((section.RemovedFragments & (1UL << i)) != 0) { fragment = i; crew.RepairFragmentCursor = i + 1; break; }
                        }
                        if (fragment < 0) continue;
                        var anchor = section.RepairTransform(fragment);
                        if (anchor == null) continue;
                        point = anchor.TransformPoint(section.RepairBounds(fragment).center);
                    }
                    var worker = MaintenanceWorker(crew, key, point, true);
                    if (worker == null) continue;
                    if (fragment >= 0)
                    {
                        var anchor = section.RepairTransform(fragment);
                        point = anchor.TransformPoint(section.RepairBounds(fragment).ClosestPoint(
                            anchor.InverseTransformPoint(worker.transform.position + Vector3.up * 1.5f)));
                    }
                    else point += Vector3.ProjectOnPlane(worker.transform.position - point, Vector3.up).normalized * .65f;
                    worker.BotCandidates = $"Ремонт секции {section.SectionId}: БОТ{worker.BotNumber}; остальные моряки продолжают свои задачи";
                    if (worker.BotTaskRunning) worker.PreemptBotTask("Ремонт корабля приоритетнее текущей палубной работы");
                    worker.AssignBotJob(key, new BotMaintenanceAction(worker, section, fragment, point));
                    if (worker.BotTaskRunning) { crew.Reserved.Add(key); break; }
                    worker.FinishBotTask();
                }
            }
            if (repairOnly || crew.EscapeZone) return;
            var network = crew.Ship.GetComponent<NetworkCannon>();
            if (network == null || network.Crate == null || network.Crate.Cannons.Count == 0) return;
            for (int index = 0; index < network.Crate.Cannons.Count; index++)
            {
                var cannon = network.Crate.Cannons[index];
                if (cannon == null || !cannon.gameObject.activeInHierarchy || cannon.IsLoaded || cannon.IsLoading ||
                    cannon.IsIgnited || network.HasBoarding(cannon.Index)) continue;
                int key = 2000 + cannon.Index;
                if (crew.Reserved.Contains(key)) continue;
                var worker = MaintenanceWorker(crew, key, cannon.transform.position, false);
                if (worker == null) continue;
                worker.BotCandidates = $"Снабжение пушки {cannon.Index + 1}: БОТ{worker.BotNumber}; зарезервирована только эта пушка";
                if (worker.BotTaskRunning) worker.PreemptBotTask("Перезарядка установленной пушки важнее нового оснащения и рыбалки");
                worker.AssignBotJob(key, new BotMaintenanceAction(worker, cannon));
                if (worker.BotTaskRunning) crew.Reserved.Add(key);
                else worker.FinishBotTask();
            }
        }

        void AssignNearest(BotCrew crew, int key, IBotShipStation station, string reason)
        {
            NetworkPlayer selected = null;
            float best = float.PositiveInfinity;
            string candidates = station.Name + ": ";
            foreach (var player in crew.Members)
            {
                if (player == null || !player.IsBot.Value) continue;
                if (!player.BotCanReceiveTask && player.BotTaskKey != 52000) { candidates += $"БОТ{player.BotNumber}: {player.BotTaskBlockReason}; "; continue; }
                if (!player.BotCanReceiveWork(key)) { candidates += $"БОТ{player.BotNumber}: повтор этой станции отложен; "; continue; }
                float distance = Vector3.Distance(player.transform.position, station.Position);
                candidates += $"БОТ{player.BotNumber} {distance:F1} м; ";
                if (distance < best) { selected = player; best = distance; }
            }
            foreach (var player in crew.Members) if (player != null && player.IsBot.Value) player.BotCandidates = candidates;
            if (selected == null) return;
            selected.AssignBotStation(key, station, reason + $"; ближайший свободный бот ({best:F1} м)");
            if (key == -1 && selected.BotTaskRunning) crew.CaptainNumber = selected.BotNumber;
            if (selected.BotTaskRunning) crew.Reserved.Add(key);
            else selected.FinishBotTask();
        }
    }
}
