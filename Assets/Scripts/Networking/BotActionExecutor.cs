using UnityEngine;

namespace PirateSlop.Networking
{
    public enum BotActionState { Running, Succeeded, Failed, Cancelled }

    public interface IBotAction
    {
        string Status { get; }
        string Failure { get; }
        BotActionState State { get; }
        bool Begin(out string reason);
        PlayerCommand Tick(float delta);
        void Cancel(string reason);
    }

    public interface IBotOutsideWork
    {
        bool RequiresShipWait { get; }
    }

    public sealed class BotActionExecutor
    {
        static readonly Unity.Profiling.ProfilerMarker marker = new("Bots.Actions");
        readonly NetworkPlayer player;
        IBotAction action;
        BotYieldAction yielding;
        float nextYield;
        public bool Running => yielding != null && yielding.State == BotActionState.Running || action != null && action.State == BotActionState.Running;
        public bool ArtilleryBlocked => action is BotStationAction station && station.ArtilleryBlocked;
        public bool Succeeded => action != null && action.State == BotActionState.Succeeded;
        public string Failure => action?.Failure ?? "Нет";
        public string Status => yielding != null && yielding.State == BotActionState.Running ? yielding.Status : action != null ? action.Status : "Ожидание задачи";
        public bool RequiresShipWait => Running && action is IBotOutsideWork work && work.RequiresShipWait;
        public BotActionExecutor(NetworkPlayer player) => this.player = player;
        public bool Assign(IBotAction next, out string reason)
        {
            yielding = null;
            action?.Cancel("Назначена новая задача");
            action = next;
            return next.Begin(out reason);
        }
        public PlayerCommand Tick(float delta)
        {
            using var sample = marker.Auto();
            if (yielding != null)
            {
                if (yielding.State == BotActionState.Running) return yielding.Tick(delta);
                yielding = null;
            }
            return action != null && action.State == BotActionState.Running
                ? action.Tick(delta) : new PlayerCommand { Yaw = player.transform.eulerAngles.y };
        }
        public void Cancel(string reason) { yielding = null; action?.Cancel(reason); }
        public bool TryYield(NetworkPlayer requester)
        {
            if (yielding != null || Time.time < nextYield || player.Motor.IsDead || player.Motor.LocomotionLocked ||
                player.Motor.IsSwimming || player.Motor.IsClimbing || player.Ship == null || player.Passenger.Ship != player.Ship.Body) return false;
            bool walking = action != null && action.State == BotActionState.Running;
            if (walking && (action is not BotStationAction station || !station.CanYield || requester.BotNumber > player.BotNumber)) return false;
            nextYield = Time.time + 3f;
            var next = new BotYieldAction(player, requester);
            if (!next.Begin(out _)) return false;
            yielding = next;
            return true;
        }
    }

    public sealed class BotStuckEvidence
    {
        Vector3 anchor;
        bool initialized;
        public float AttemptSeconds { get; private set; }
        public int Retries { get; private set; }
        public void Reset(Vector3 point) { anchor = point; initialized = true; AttemptSeconds = 0; Retries = 0; }
        public void Observe(Vector3 point, float delta, bool attempting)
        {
            if (!initialized || Vector3.Distance(point, anchor) >= .25f) { Reset(point); return; }
            if (attempting) AttemptSeconds += Mathf.Clamp(delta, 0, .2f);
        }
        public void Retried() => Retries++;
        public bool Eligible(float threshold, bool staticObstruction, bool validSupport, bool humanBlocking)
            => Retries >= 2 && AttemptSeconds >= threshold && staticObstruction && validSupport && !humanBlocking;
    }

    public sealed class BotStationAction : IBotAction
    {
        readonly NetworkPlayer player;
        readonly BotMotionSettings settings;
        readonly DeckRoute route;
        readonly BotStuckEvidence evidence = new();
        NetworkShip ship;
        readonly IBotShipStation station;
        readonly string assignmentReason;
        Vector3 goal, lastSafe, sideStep, lastWaypoint;
        readonly System.Collections.Generic.List<Vector3> approaches = new();
        float deadline, nextProbe, nextReport, sideStepUntil, occupiedSeconds, nextOccupiedRetry;
        bool hasSafe, holding, staticBlocked, humanBlocked;
        string failure = "Нет", phase = "Подготовка";
        public string Failure => failure;
        public string Status => phase;
        public BotActionState State { get; private set; } = BotActionState.Running;

        public BotStationAction(NetworkPlayer player, BotMotionSettings settings, IBotShipStation station, string reason)
        {
            this.player = player; this.settings = settings; this.station = station; assignmentReason = reason; route = new DeckRoute(player, settings);
        }

        public bool ArtilleryBlocked => station is BotCannonStation cannon && cannon.CannotEngage;
        public bool CanYield => State == BotActionState.Running && !holding;
        Vector3 Local => ship.transform.InverseTransformPoint(player.transform.position);
        PlayerCommand Idle => new() { Yaw = player.transform.eulerAngles.y };

        public bool Begin(out string reason)
        {
            ship = player.Ship;
            reason = "Бот должен быть живым и находиться на своей палубе";
            if (!player.IsServerInitialized || !player.IsBot.Value || ship == null || ship.IsSinking ||
                player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing || player.Motor.IsKnockedBack ||
                player.Motor.LocomotionLocked || player.Passenger.Ship != ship.Body) return Fail(reason);
            if (!station.Available) { reason = "Станция недоступна"; return Fail(reason); }
            station.Validate();
            if (station.Busy) { reason = "Станция занята; бот не отбирает управление"; return Fail(reason); }
            route.Configure(ship);
            var center = ship.transform.InverseTransformPoint(station.Position - Vector3.up);
            float best = float.PositiveInfinity;
            approaches.Clear();
            float approachRadius = station is IBotApproachRange range ? range.ApproachRadius : 1.7f;
            for (int ring = 1; ring <= Mathf.Clamp(Mathf.CeilToInt(approachRadius / .85f), 2, 4); ring++) for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                var near = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * .85f);
                if (!route.Ground(near, out var point, station is IBotApproachRange ? 3f : 1.5f) || !route.ClearAt(point, true)) continue;
                if (station is IBotApproachConstraint constraint && !constraint.AllowsApproach(ship.transform.TransformPoint(point))) continue;
                if (Vector3.Distance(ship.transform.TransformPoint(point) + Vector3.up, station.Position) > (station is IBotApproachRange ? 3.5f : 2.5f)) continue;
                float distance = Vector3.Distance(Local, point);
                approaches.Add(point);
                if (distance < best) { goal = point; best = distance; }
            }
            if (best == float.PositiveInfinity) { reason = "Нет свободной позиции подхода к станции"; return Fail(reason); }
            deadline = Time.time + Mathf.Max(15f, settings.TaskTimeout);
            evidence.Reset(Local);
            hasSafe = route.Ground(Local, out lastSafe) && route.ClearAt(lastSafe, true, true);
            route.Begin(ship, Local, approaches);
            phase = "Поиск пути";
            reason = "Назначена задача: " + station.Name;
            Report(true);
            return true;
        }

        public PlayerCommand Tick(float delta)
        {
            var command = Idle;
            if (ship == null || ship.IsSinking || !station.Available) { Fail("Корабль или станция недоступны"); return command; }
            var motor = player.Motor;
            if (motor.IsDead || motor.IsSwimming || motor.IsClimbing || motor.IsKnockedBack || player.Passenger.Ship != ship.Body)
            { Cancel("Действие прервано состоянием персонажа или потерей палубы"); return command; }
            station.Validate();
            if (holding)
            {
                if (!station.Owned(player)) { Cancel("Управление перехвачено или потеряно; повторного захвата нет"); return command; }
                station.Work(player, delta);
                if (station.Complete) { State = BotActionState.Succeeded; phase = "Выполнено: " + station.Name; Release(); Report(true); return command; }
                phase = station.Name + ": управление";
                Report(); return command;
            }
            if (motor.LocomotionLocked) { Cancel("Движение заблокировано другим действием"); return command; }
            if (station.Busy) { Cancel("Станцию занял другой участник"); return command; }
            if (Time.time > deadline) { Fail("Истёк срок задачи; телепортация по таймауту запрещена"); return command; }
            if (route.Failed) { Fail("Путь не найден или разрушен; недоступная цель не разрешает телепортацию"); return command; }
            if (route.Searching) { phase = "Поиск пути"; Report(); return command; }
            if (!route.Ready) { Fail("Нет маршрута"); return command; }
            goal = route.Destination;
            if (route.Advance(Local))
            {
                if (!station.Acquire(player)) { Fail("Сервер отклонил захват станции"); return command; }
                holding = true;
                phase = station.Name + ": управление";
                Report(true); return command;
            }
            var target = Time.time < sideStepUntil ? sideStep : route.Waypoint;
            if ((target - lastWaypoint).sqrMagnitude > .01f) { nextProbe = 0; lastWaypoint = target; }
            var direction = target - Local;
            direction.y = 0;
            var probe = Vector3.Lerp(Local, target, Mathf.Min(1f, .5f / Mathf.Max(.001f, direction.magnitude)));
            if (Time.time >= nextProbe)
            {
                nextProbe = Time.time + .15f;
                staticBlocked = !route.Ground(probe, out var grounded) || !route.Edge(Local, grounded);
                humanBlocked = route.PlayerBlocking(Local) || route.PlayerBlocking(probe) || (!staticBlocked && !route.Edge(Local, grounded, true));
                if (route.Ground(Local, out var safe) && route.ClearAt(safe, true, true) &&
                    (!hasSafe || Vector3.Distance(lastSafe, safe) > .3f)) { lastSafe = safe; hasSafe = true; }
            }
            evidence.Observe(Local, delta, !humanBlocked);
            occupiedSeconds = humanBlocked ? occupiedSeconds + delta : 0f;
            if (occupiedSeconds >= .75f && Time.time >= nextOccupiedRetry)
            {
                nextOccupiedRetry = Time.time + 1.5f;
                occupiedSeconds = 0f;
                var blocker = route.BlockingBot(probe) ?? route.BlockingBot(Local);
                bool yielding = blocker != null && blocker.TryYieldBotPassage(player);
                if (!yielding && blocker != null) yielding = player.TryYieldBotPassage(blocker);
                if (!yielding) route.Begin(ship, Local, approaches, true);
                phase = yielding ? "Боты уступают проход друг другу" : "Поиск обхода занятого прохода";
                Report(true);
                return command;
            }
            phase = humanBlocked ? "Ожидание: проход занят персонажем" : staticBlocked ? "Препятствие; восстановление пути" : "Подход: " + station.Name;
            if (!humanBlocked && evidence.Retries < 2 && evidence.AttemptSeconds >= Mathf.Clamp(settings.RetryAfter, .75f, 1.5f) * (evidence.Retries + 1))
            {
                evidence.Retried();
                bool movedAside = false;
                for (int option = 0; evidence.Retries == 1 && option < 3; option++)
                {
                    var escape = option == 2 ? -direction.normalized : new Vector3(direction.z, 0, -direction.x).normalized * (option == 0 ? -1f : 1f);
                    var side = Local + escape * .6f;
                    if (!route.Ground(side, out side) || !route.Edge(Local, side, true)) continue;
                    sideStep = side; sideStepUntil = Time.time + .8f; nextProbe = 0; movedAside = true; break;
                }
                if (!movedAside) route.Begin(ship, Local, approaches, true);
                SessionController.Instance.RecordBotEvent(player.BotNumber, $"Восстановление {evidence.Retries}: {(movedAside ? "попытка отойти в сторону" : "повторный поиск пути")}");
                Report(true); return command;
            }
            if (evidence.AttemptSeconds >= Mathf.Max(6, settings.RecoveryAfter) && evidence.Retries >= 2)
            {
                bool supported = motor.IsGrounded && route.Ground(Local, out _);
                bool obstructed = staticBlocked || !route.ClearAt(Local, false, true);
                if (settings.AllowEmergencyTeleport && Time.time >= player.NextBotRecovery && hasSafe &&
                    evidence.Eligible(Mathf.Max(6, settings.RecoveryAfter), obstructed, supported, humanBlocked) &&
                    route.RecoveryPoint(Local, lastSafe, out var recovery))
                {
                    var from = player.transform.position;
                    var to = ship.transform.TransformPoint(recovery);
                    player.Teleport(new PlayerState { Position = to, Yaw = command.Yaw, Grounded = true });
                    player.Passenger.Attach(ship.Body);
                    player.NextBotRecovery = Time.time + Mathf.Max(10, settings.TeleportCooldown);
                    SessionController.Instance.RecordBotEvent(player.BotNumber, $"Аварийный перенос: {evidence.AttemptSeconds:F1} с попыток, {evidence.Retries} восстановления, препятствие подтверждено; {from:F2} → {to:F2}");
                    evidence.Reset(recovery); route.Begin(ship, recovery, goal); nextProbe = 0;
                }
                else if (!humanBlocked) { Fail("Застревание: строгие условия безопасного переноса не выполнены"); return command; }
            }
            if (!staticBlocked && !humanBlocked && !route.Searching)
            {
                var worldDirection = ship.transform.TransformDirection(direction.normalized);
                float yaw = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;
                command.Yaw = Mathf.MoveTowardsAngle(command.Yaw, yaw, 150f * delta);
                var relative = Quaternion.Inverse(Quaternion.Euler(0, command.Yaw, 0)) * worldDirection;
                command.Move = Vector2.ClampMagnitude(new Vector2(relative.x, relative.z), 1f) * Mathf.Clamp01(direction.magnitude / .35f);
                command.Jump = motor.IsGrounded && target.y - Local.y > player.GetComponent<CharacterController>().stepOffset;
            }
            Report(); return command;
        }

        bool Fail(string reason)
        {
            failure = reason; phase = "Задача не выполнена"; State = BotActionState.Failed;
            Release(); Report(true); return false;
        }

        public void Cancel(string reason)
        {
            if (State != BotActionState.Running) return;
            failure = reason; phase = "Задача отменена"; State = BotActionState.Cancelled;
            Release(); Report(true);
        }

        void Release()
        {
            route.Clear();
            station.Release(player);
            
            holding = false;
        }

        void Report(bool force = false)
        {
            if (!force && Time.time < nextReport) return;
            nextReport = Time.time + .5f;
            SessionController.Instance.RecordBotDecision(player.BotNumber, station.Name, assignmentReason, phase,
                $"Цель на палубе {goal:F1}; точек осталось {route.Remaining}; попытки без перемещения {evidence.AttemptSeconds:F1} с; восстановлений {evidence.Retries}",
                player.BotVision.Status,
                player.BotAssignment, failure, player.BotCandidates);
        }
    }
}
