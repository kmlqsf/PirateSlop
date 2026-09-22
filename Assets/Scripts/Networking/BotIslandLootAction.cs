using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotIslandLootAction : IBotAction, IBotOutsideWork
    {
        enum Phase { Plan, Depart, Outbound, Loot, Breathe, Return, Board }
        readonly NetworkPlayer player;
        readonly NetworkShip ship;
        readonly NetworkLootChest chest;
        readonly NetworkFish pickup;
        bool TargetAvailable => chest != null ? chest.BotLootCandidate : pickup != null && pickup.Available;
        readonly NetworkWeapon weapon;
        readonly BotShoreRoute route;
        readonly Vector3 destination;
        ShipLadder ladder;
        IBotAction travel;
        Phase phase, afterBreathing;
        float breathingStarted;
        bool wineTried, drinkingWine;
        int winePrevious;
        float wineDeadline;
        Vector3 anchor, plannedReturn;
        float started, progressAt, nextProbe, nextLoot, nextReport, nextReplan;
        int taken, replans, observedLockRound = -1;
        float lockAnswerAt;
        bool SeaChest => chest != null && chest.Kind != SeaLootKind.None;
        bool edgeClear = true;
        readonly bool returnOnly;
        public static BotIslandLootAction ReturnToShip(NetworkPlayer player) => new(player, null);
        public BotActionState State { get; private set; } = BotActionState.Running;
        public bool RequiresShipWait => State == BotActionState.Running;
        public string Status { get; private set; } = "Подготовка островной вылазки";
        public string Failure { get; private set; } = "Нет";
        PlayerCommand Idle => new() { Yaw = player.transform.eulerAngles.y, Rise = player.Motor.IsSwimming };
        Vector3 ReturnPoint
        {
            get
            {
                var point = ladder.transform.position + ladder.transform.forward * 2.3f;
                point.y = OceanSurface.Instance.Height(point) - (player.GetComponent<NetworkEquipment>().WaterRunning ? 0f : 1.25f);
                return point;
            }
        }
        public BotIslandLootAction(NetworkPlayer player, FishNet.Object.NetworkObject target)
        {
            this.player = player; chest = target != null ? target.GetComponent<NetworkLootChest>() : null;
            pickup = target != null ? target.GetComponent<NetworkFish>() : null; ship = player.Ship;
            weapon = player.GetComponent<NetworkWeapon>(); route = new BotShoreRoute(player);
            returnOnly = target == null;
            destination = returnOnly ? player.transform.position : chest != null ? chest.BotLootPoint : target.transform.position;
        }
        public bool Begin(out string reason)
        {
            reason = "Нет подходящей боковой лестницы для островной вылазки";
            if (!player.IsServerInitialized || !player.IsBot.Value || ship == null || !returnOnly && !TargetAvailable || OceanSurface.Instance == null)
                return End(false, reason);
            float side = Mathf.Sign(ship.transform.InverseTransformPoint(destination).x);
            foreach (var candidate in ShipLadder.Active)
                if (candidate != null && candidate.BoardingAccess && !candidate.RopeClimb && candidate.Body == ship.Body &&
                    Mathf.Sign(ship.transform.InverseTransformPoint(candidate.transform.position).x) == side) { ladder = candidate; break; }
            if (ladder == null) return End(false, reason);
            started = progressAt = Time.time; anchor = player.transform.position;
            if (returnOnly) { BeginReturn("Возвращение после прерванной вылазки"); reason = "Вернуться к своему кораблю"; return true; }
            route.Begin(ReturnPoint, destination, !SeaChest); phase = Phase.Plan;
            reason = "Проверить путь до замеченного островного лута до выхода за борт";
            Status = reason; return true;
        }
        public PlayerCommand Tick(float delta)
        {
            if (State != BotActionState.Running) return Idle;
            if (ship == null || ship.IsSinking || player.Motor.IsDead || OceanSurface.Instance == null)
            { End(false, "Корабль потерян или бот погиб"); return Idle; }
            if (ladder == null || !ladder.isActiveAndEnabled) { End(false, "Лестница возвращения недоступна"); return Idle; }
            if (player.BotRecallOutside && (phase == Phase.Plan || phase == Phase.Depart))
            {
                if (!player.Motor.IsSwimming && !player.Motor.IsClimbing && player.Passenger.Ship == ship.Body)
                { End(false, "Вылазка отменена: срочная опасность"); return Idle; }
                travel?.Cancel("Вылазка отозвана");
                BeginReturn("Срочный отзыв на корабль");
            }
            if ((player.transform.position - anchor).sqrMagnitude > .25f)
            { anchor = player.transform.position; progressAt = Time.time; }
            if ((phase is Phase.Outbound or Phase.Loot or Phase.Breathe) && (phase != Phase.Breathe && Time.time - started > 150f ||
                player.BotRecallOutside || !TargetAvailable || player.BotVision.Visible ||
                new Vector2(destination.x, destination.z).magnitude > SessionController.Instance.SafeRadius(60f) - 15f ||
                Vector3.Distance(player.transform.position, ship.transform.position) > 140f))
                BeginReturn("Время, безопасная зона или состояние цели требуют возвращения");
            if ((phase is Phase.Outbound or Phase.Loot) && player.Motor.IsSwimming && player.Motor.Breath < 8f)
            {
                if (weapon.WorkingLoot == chest) weapon.CancelLootWork();
                afterBreathing = phase; phase = Phase.Breathe; breathingStarted = Time.time;
                SessionController.Instance.RecordBotEvent(player.BotNumber, "Всплыть за воздухом, затем продолжить вылазку");
            }
            if (phase == Phase.Breathe)
            {
                Status = "Всплывает и восстанавливает воздух перед продолжением работы";
                progressAt = Time.time;
                Report();
                if (player.Motor.BreathFraction < .98f)
                    return new PlayerCommand { Yaw = player.transform.eulerAngles.y, Rise = player.Motor.IsSwimming };
                started += Time.time - breathingStarted;
                phase = afterBreathing;
                if (phase == Phase.Outbound) route.Begin(player.transform.position, destination, !SeaChest);
            }
            Report();
            if (phase == Phase.Plan)
            {
                if (route.Searching) return Idle;
                if (route.Failed) { End(false, "До островного лута нет проверенного маршрута"); return Idle; }
                route.Clear();
                travel = BotHullWaterRepairAction.Depart(player, destination);
                if (!travel.Begin(out var reason)) { End(false, reason); return Idle; }
                phase = Phase.Depart;
            }
            if (phase == Phase.Depart || phase == Phase.Board)
            {
                Status = phase == Phase.Depart ? "Выход за борт для островной вылазки: " + travel.Status : "Возвращение на корабль: " + travel.Status;
                var command = travel.Tick(delta);
                if (travel.State == BotActionState.Running) return command;
                if (phase == Phase.Board) { End(travel.State == BotActionState.Succeeded && (taken > 0 || returnOnly), travel.Failure == "Нет" ? Failure : travel.Failure); return command; }
                if (travel.State != BotActionState.Succeeded || !player.Motor.IsSwimming)
                { End(false, travel.Failure == "Нет" ? "Выход за борт не завершён" : travel.Failure); return command; }
                phase = Phase.Outbound; route.Begin(player.transform.position, destination, !SeaChest); progressAt = Time.time;
                return command;
            }
            if (phase == Phase.Loot)
            {
                if (SeaChest && (!chest.Available || !weapon.CanHandleLoot(chest))) return WorkSeaChest();
                Status = pickup != null ? "Подбирает островной предмет" : "Осматривает сундук и забирает подходящие предметы";
                if (Time.time < nextLoot) return Idle;
                nextLoot = Time.time + .4f;
                if (pickup != null)
                {
                    if (player.GetComponent<NetworkFishing>().TryPickup(pickup.NetworkObject)) taken++;
                    BeginReturn(taken > 0 ? "Подобран островной предмет" : "Предмет недоступен или инвентарь заполнен"); return Idle;
                }
                if (chest == null || !chest.Available || !weapon.TryUseChest(chest.NetworkObject)) { BeginReturn("Сундук недоступен или его забрал другой участник"); return Idle; }
                int slot = -1, best = -1;
                for (int i = 0; i < chest.SlotCount; i++)
                {
                    var item = chest.ItemAt(i);
                    if (item == InventoryItem.None || !weapon.CanAddItem(item)) continue;
                    int score = item == InventoryItem.Rum ? (ship.RumCount < 3 ? 20 : 8) : item == InventoryItem.Cannon ? 5 : item == InventoryItem.Mallet ? 4 : item == InventoryItem.Plank ? 3 : CannonAmmo.IsBall(item) ? 2 : 1;
                    if (score > best) { best = score; slot = i; }
                }
                if (slot < 0) { BeginReturn(taken > 0 ? "Лут собран либо инвентарь заполнен" : "Нет предметов, помещающихся в инвентарь"); return Idle; }
                if (weapon.TryUseChest(chest.NetworkObject, slot))
                {
                    taken++;
                    SessionController.Instance.RecordBotEvent(player.BotNumber, $"Подобран предмет из сундука; всего {taken}");
                }
                else BeginReturn("Сервер отклонил подбор; возвращение с уже собранными предметами");
                return Idle;
            }
            if (phase == Phase.Return && (ReturnPoint - plannedReturn).sqrMagnitude > 16f && Time.time >= nextReplan)
            { plannedReturn = ReturnPoint; route.Begin(player.transform.position, plannedReturn); nextReplan = Time.time + 3f; }
            Status = phase == Phase.Return ? "Возвращается с острова к кораблю" : "Плывёт к берегу и идёт к сундуку";
            if (route.Searching) { progressAt += Mathf.Clamp(delta, 0f, .2f); Status += "; поиск пути"; return Idle; }
            if (route.Failed || Time.time - progressAt > 12f)
            {
                if (phase != Phase.Return) BeginReturn("Островной путь недоступен или перекрыт");
                else if (Time.time >= nextReplan)
                {
                    replans++;
                    route.Begin(player.transform.position, ReturnPoint); progressAt = Time.time; nextReplan = Time.time + 10f;
                }
                return Idle;
            }
            if (!route.Ready) return Idle;
            if (route.Advance(player.transform.position))
            {
                if (phase == Phase.Outbound) { phase = Phase.Loot; return Idle; }
                travel = new BotHullWaterRepairAction(player, null, -1);
                if (!travel.Begin(out var reason)) { End(false, reason); return Idle; }
                phase = Phase.Board; return Idle;
            }
            var target = route.Waypoint;
            if (phase == Phase.Return && !player.Motor.IsSwimming && !player.Motor.IsClimbing &&
                Vector3.Distance(player.transform.position, ReturnPoint) > 25f)
            {
                var equipment = player.GetComponent<NetworkEquipment>();
                if (!wineTried && !equipment.WaterRunning)
                {
                    wineTried = true;
                    int wine = BotUtilityAction.Slot(player, InventoryItem.Wine);
                    if (wine >= 0)
                    {
                        winePrevious = player.GetComponent<PlayerInventory>().SelectedSlot;
                        weapon.SelectServerSlot(wine);
                        drinkingWine = equipment.TryBotUtility(player.transform.forward); wineDeadline = Time.time + 4f;
                        if (!drinkingWine) weapon.SelectServerSlot(winePrevious);
                    }
                }
                if (drinkingWine)
                {
                    if (equipment.IsBusy && Time.time < wineDeadline) { Status = "Пьёт вино для возвращения по воде"; progressAt = Time.time; return Idle; }
                    weapon.SelectServerSlot(winePrevious); drinkingWine = false;
                    route.Begin(player.transform.position, ReturnPoint); progressAt = Time.time; return Idle;
                }
            }
            if (Time.time >= nextProbe)
            {
                nextProbe = Time.time + .2f;
                var probe = Vector3.MoveTowards(player.transform.position, target, 1f);
                edgeClear = route.Sample(player.transform.position, out var foot) && route.Sample(probe, out var ground) && route.Edge(foot, ground, true);
                if (!edgeClear && Time.time - progressAt > 3f && Time.time >= nextReplan)
                { route.Begin(player.transform.position, phase == Phase.Return ? ReturnPoint : destination, phase != Phase.Return && !SeaChest); nextReplan = Time.time + 3f; }
            }
            if (!edgeClear) { Status += "; проход занят"; return Idle; }
            var offset = target - player.transform.position;
            return new PlayerCommand {
                Yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, Move = Vector2.up,
                Jump = !player.Motor.IsSwimming && player.Motor.IsGrounded && offset.y > .3f,
                Rise = player.Motor.IsSwimming && offset.y > .2f,
                Crouch = player.Motor.IsSwimming && offset.y < -.4f
            };
        }
        PlayerCommand WorkSeaChest()
        {
            Status = chest.BotLootRising ? "Ждёт всплытия добычи" : chest.Kind == SeaLootKind.Raft ? "Взламывает ящик на плоту" : "Освобождает подводные крепления";
            if (chest.BotLootRising) return Idle;
            if (weapon.WorkingLoot == chest)
            {
                if (Time.time >= nextLoot)
                {
                    nextLoot = Time.time + .2f;
                    int answer = -1;
                    if (chest.Kind == SeaLootKind.Raft)
                    {
                        if (observedLockRound != chest.LockRound)
                        { observedLockRound = chest.LockRound; lockAnswerAt = Time.time + .85f; }
                        if (Time.time >= lockAnswerAt) answer = chest.LockKey;
                    }
                    chest.WorkInput(weapon, answer, chest.LockRound);
                }
                return new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            }
            var point = chest.WorkPoint(player.transform.position + Vector3.up);
            if (weapon.CanHandleLoot(chest))
            {
                if (!chest.Available && chest.Kind == SeaLootKind.Raft && player.Motor.IsSwimming)
                { chest.BoardRaft(weapon); return Idle; }
                if (!chest.Available) chest.StartWork(weapon);
                if (weapon.WorkingLoot == chest || chest.Available) return Idle;
            }
            var goal = point - Vector3.up;
            if (chest.Kind != SeaLootKind.Sunken || chest.Available)
                goal.y = OceanSurface.Instance.Height(goal) - 1.25f;
            var delta = goal - player.transform.position;
            return new PlayerCommand {
                Yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg,
                Move = Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude > 1f ? Vector2.up : Vector2.zero,
                Rise = player.Motor.IsSwimming && delta.y > .15f,
                Crouch = player.Motor.IsSwimming && delta.y < -.15f,
                Jump = player.Motor.IsGrounded && delta.y > .3f
            };
        }

        void BeginReturn(string reason)
        {
            if (weapon.WorkingLoot == chest) weapon.CancelLootWork();
            Failure = taken > 0 ? "Нет" : reason; phase = Phase.Return;
            plannedReturn = ReturnPoint; route.Begin(player.transform.position, plannedReturn); progressAt = Time.time; replans = 0;
            SessionController.Instance.RecordBotEvent(player.BotNumber, reason + "; возвращение на корабль");
        }
        bool End(bool success, string reason)
        {
            if (weapon.WorkingLoot == chest) weapon.CancelLootWork();
            if (drinkingWine) { weapon.SelectServerSlot(winePrevious); drinkingWine = false; }
            State = success ? BotActionState.Succeeded : BotActionState.Failed;
            Failure = success ? "Нет" : reason; Status = success ? $"Вылазка завершена; собрано {taken}" : "Вылазка прервана";
            route.Clear(); travel?.Cancel(reason); Report(true); return success;
        }
        public void Cancel(string reason)
        {
            if (State != BotActionState.Running) return;
            End(false, reason); State = BotActionState.Cancelled;
        }
        void Report(bool force = false)
        {
            if (!force && Time.time < nextReport) return;
            nextReport = Time.time + .5f;
            SessionController.Instance.RecordBotDecision(player.BotNumber, "Островной лут и возвращение", "Один сборщик; экипаж ждёт у острова", Status,
                $"Собрано {taken}; вылазка {Time.time - started:F0} с; перестроений {replans}",
                "Видимый обычный сундук или предмет; общий бюджет пути; настоящий инвентарь", player.BotAssignment, Failure, player.BotCandidates);
        }
    }
}
