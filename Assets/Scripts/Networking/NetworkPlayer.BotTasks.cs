using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
        bool botManualTask;
        internal bool BotHasManualTask => botManualTask;
        public BotCombatMemory BotVision { get; } = new();
        bool botReturnRequired;
        float nextBotTask;
        internal float NextBotThrow;
        internal float NextBotFishing;
        internal float NextBotWaterRepair;
        internal bool BotRecallOutside;
        readonly System.Collections.Generic.Dictionary<int, float> botStationRetry = new();
        public int BotTaskKey { get; private set; } = -2;
        public string BotAssignment { get; internal set; } = "Нет назначения";
        public string BotCandidates { get; internal set; } = "Нет вариантов";
        internal bool BotArtilleryBlocked => botActions != null && botActions.ArtilleryBlocked;
        public bool BotTaskRunning => botActions != null && botActions.Running;
        public bool BotNeedsShipWait => botActions != null && botActions.RequiresShipWait;
        internal void RecoverBotFromWater()
        {
            if (BotNeedsShipWait) botReturnRequired = true;
            if (Motor != null && (Motor.IsDead || Ship != null && Motor.IsGrounded && !Motor.IsSwimming && !Motor.IsClimbing && Passenger.Ship == Ship.Body))
                botReturnRequired = false;
            if (!IsServerInitialized || !IsBot.Value || botActions == null || BotTaskRunning || botManualTask ||
                Motor.IsDead || Ship == null || Ship.IsSinking || Time.time < nextBotTask ||
                (!Motor.IsSwimming && !Motor.IsClimbing && !botReturnRequired)) return;
            BotTaskKey = 900000;
            IBotAction recovery = Motor.IsClimbing || (transform.position - Ship.transform.position).sqrMagnitude < 25f * 25f && Motor.IsSwimming
                ? new BotHullWaterRepairAction(this, null, -1) : BotIslandLootAction.ReturnToShip(this);
            if (!botActions.Assign(recovery, out _)) { BotTaskKey = -2; nextBotTask = Time.time + 5f; }
        }
        public string BotTaskBlockReason
        {
            get
            {
                if (!IsServerInitialized || !IsBot.Value || botActions == null) return "Исполнитель бота не запущен на сервере";
                if (Motor == null) return "Нет контроллера движения";
                if (Motor.IsDead) return "Бот мёртв";
                if (botManualTask) return "Ручная задача F10";
                if (BotTaskRunning) return "Выполняет задачу";
                if (Time.time < nextBotTask) return $"Пауза после задачи: {Mathf.CeilToInt(nextBotTask-Time.time)} с";
                if (Motor.IsSwimming) return "Бот находится в воде";
                if (Motor.IsClimbing) return "Бот находится на лестнице";
                if (Motor.IsKnockedBack) return "Бот отброшен";
                if (Motor.LocomotionLocked) return "Движение заблокировано взаимодействием";
                if (Ship == null) return "Не найден корабль экипажа";
                if (Passenger.Ship != Ship.Body) return "Не обнаружена опора на своей палубе";
                return "";
            }
        }
        public bool BotCanReceiveTask => BotTaskBlockReason.Length == 0;
        public string BotLastFailure => botActions?.Failure ?? "Нет";
        public bool BotCanReceiveStation(int key) => BotCanReceiveTask &&
            (!botStationRetry.TryGetValue(key, out float retry) || Time.time >= retry);

        internal bool BotStationDeferred(int key) => botStationRetry.TryGetValue(key, out float retry) && Time.time < retry;
        internal void DeferBotStation(int key, float seconds) => botStationRetry[key] = Time.time + seconds;
        internal void AllowBotStationRetry(int key) => botStationRetry.Remove(key);

        internal bool TryYieldBotPassage(NetworkPlayer requester) => IsServerInitialized && IsBot.Value && !botManualTask &&
            requester != null && requester.Ship == Ship && botActions != null && botActions.TryYield(requester);

        internal bool BotCanPlanReplacement(int key) => IsServerInitialized && IsBot.Value && botActions != null && !botManualTask &&
            (BotTaskRunning || BotCanReceiveTask) &&
            (!botStationRetry.TryGetValue(key, out float retry) || Time.time >= retry);

        internal bool BotCanReceiveWork(int key) => BotCanReceiveStation(key) ||
            BotTaskRunning && BotTaskKey == 52000 && !Motor.IsDead && !Motor.IsSwimming && !Motor.IsClimbing &&
            Ship != null && Passenger.Ship == Ship.Body &&
            (!botStationRetry.TryGetValue(key, out float retry) || Time.time >= retry);

        internal void AssignBotJob(int key, IBotAction action)
        {
            if (BotTaskKey == 52000 && BotCanReceiveWork(key)) PreemptBotTask("Рыбалка отложена: назначена работа экипажа");
            if (!BotCanReceiveStation(key)) return;
            BotTaskKey = key;
            botActions.Assign(action, out _);
        }

        internal void AssignBotStation(int key, IBotShipStation station, string reason)
        {
            if (BotTaskKey == 52000 && BotCanReceiveWork(key)) PreemptBotTask("Рыбалка отложена: назначена работа экипажа");
            if (!BotCanReceiveStation(key)) return;
            BotTaskKey = key;
            BotAssignment = station.Name;
            botActions.Assign(new BotStationAction(this, SessionController.Instance.Config.BotMotion, station, reason), out _);
        }

        internal void FinishBotTask()
        {
            if (BotTaskRunning) return;
            if (BotTaskKey != -2 && botActions != null && !botActions.Succeeded)
            {
                bool occupied = botActions.Failure.Contains("занят") || botActions.Failure.Contains("Нет свободной позиции");
                botStationRetry[BotTaskKey] = Time.time + (BotTaskKey == 53000 ? 2f : occupied ? 3f : Mathf.Max(2f, SessionController.Instance.Config.BotMotion.FailedTaskRetry));
            }
            if (BotTaskKey != -2 || botManualTask) nextBotTask = Time.time;
            BotTaskKey = -2;
            botManualTask = false;
        }

        internal void PreemptBotTask(string reason)
        {
            if (botManualTask || BotTaskKey == -2) return;
            botActions.Cancel(reason);
            BotTaskKey = -2;
            nextBotTask = Time.time;
        }

        internal void CancelAutomaticBotTask(string reason)
        {
            if (botManualTask || BotTaskKey == -2) return;
            botActions.Cancel(reason);
            FinishBotTask();
        }
    }
}
