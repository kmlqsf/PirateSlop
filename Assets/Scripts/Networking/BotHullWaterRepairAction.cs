using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotHullWaterRepairAction : IBotAction, IBotOutsideWork
    {
        enum Phase { Wait, Approach, Jump, SwimOut, SwimAlong, Repair, Breathe, ReturnOut, ReturnAlong, Board }
        readonly NetworkPlayer player;
        readonly NetworkShip ship;
        ShipDamageSection section;
        int fragment;
        Vector3? departureTarget;
        public static BotHullWaterRepairAction Depart(NetworkPlayer player, Vector3 towards)
            => new BotHullWaterRepairAction(player, null, -1) { departureTarget = towards };
        readonly NetworkWeapon weapon;
        readonly NetworkHullRepair repair;
        readonly RaycastHit[] hits = new RaycastHit[24];
        static readonly float[] AvoidAngles = { 45f, -45f, 90f, -90f, 135f, -135f };
        ShipLadder ladder;
        BotStationAction approach;
        Phase phase, afterBreathing;
        float breathingStarted;
        Vector3 localPoint, anchor;
        float side, corridor, phaseAt, started, nextStrike, nextProbe, nextReport, progressAt, nextJump;
        Vector3 swimDirection;
        int previousSlot;
        bool repaired;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public bool RequiresShipWait => State == BotActionState.Running;
        public string Failure { get; private set; } = "Нет";
        public string Status { get; private set; } = "Подготовка ремонта с воды";
        bool Returning => phase >= Phase.ReturnOut;
        bool Complete => section == null ? !departureTarget.HasValue : (section.RemovedFragments & (1UL << fragment)) == 0;
        Vector3 Local => ship.transform.InverseTransformPoint(player.transform.position);
        Vector3 LadderLocal => ship.transform.InverseTransformPoint(ladder.transform.position);
        PlayerCommand Idle => new() { Yaw = player.transform.eulerAngles.y };

        public BotHullWaterRepairAction(NetworkPlayer player, ShipDamageSection section, int fragment)
        {
            this.player = player; ship = player.Ship; this.section = section; this.fragment = fragment;
            weapon = player.GetComponent<NetworkWeapon>(); repair = player.GetComponent<NetworkHullRepair>();
        }
        public bool Begin(out string reason)
        {
            previousSlot = player.GetComponent<PlayerInventory>().SelectedSlot;
            reason = "Нет доступной боковой лестницы для выхода и возвращения";
            if (!player.IsServerInitialized || !player.IsBot.Value || ship == null || ship.IsSinking || player.Motor.IsDead ||
                OceanSurface.Instance == null || section != null && (fragment < 0 || fragment >= section.RepairCount))
                return End(false, reason);
            var target = departureTarget ?? player.transform.position;
            if (section != null)
            {
                var source = section.RepairTransform(fragment);
                if (source == null) return End(false, "У повреждения нет доступной геометрии");
                target = source.TransformPoint(section.RepairBounds(fragment).center);
            }
            float targetSide = Mathf.Sign(ship.transform.InverseTransformPoint(target).x);
            float best = float.PositiveInfinity;
            foreach (var candidate in ShipLadder.Active)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.BoardingAccess || candidate.RopeClimb || candidate.Body != ship.Body) continue;
                var local = ship.transform.InverseTransformPoint(candidate.transform.position);
                if (Mathf.Sign(local.x) != targetSide) continue;
                float distance = (candidate.transform.position - target).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; ladder = candidate;
            }
            if (ladder == null) return End(false, reason);
            side = Mathf.Sign(LadderLocal.x);
            corridor = Mathf.Abs(LadderLocal.x) + 2.3f;
            if (section != null)
            {
                var source = section.RepairTransform(fragment);
                var outside = ship.transform.InverseTransformPoint(target);
                outside.x = side * (corridor + 5f);
                target = source.TransformPoint(section.RepairBounds(fragment).ClosestPoint(source.InverseTransformPoint(ship.transform.TransformPoint(outside))));
                localPoint = ship.transform.InverseTransformPoint(target);
                corridor = Mathf.Max(corridor, Mathf.Abs(localPoint.x) + 2f);
            }
            started = Time.time;
            SetPhase(player.Motor.IsClimbing ? Phase.Board : departureTarget.HasValue ? Phase.Wait : section == null || player.Motor.IsSwimming ? Phase.ReturnOut : Phase.Wait);
            reason = "Ремонт с воды с возвращением по " + ladder.name;
            Report(); return true;
        }
        void SetPhase(Phase next)
        {
            phase = next; phaseAt = progressAt = Time.time; anchor = Local; nextProbe = 0;
            Status = next switch {
                Phase.Wait => "Ждёт остановки корабля перед выходом за борт",
                Phase.Approach => "Подход к боковой лестнице для выхода за борт",
                Phase.Jump => "Прыжок за борт у боковой лестницы",
                Phase.SwimOut => "Отплывает от борта",
                Phase.SwimAlong => "Плывёт вдоль корпуса к повреждению",
                Phase.Repair => "Ремонт корпуса с воды",
                Phase.Breathe => "Всплывает за воздухом; ремонт продолжится после вдоха",
                Phase.ReturnOut => "Возвращение: всплывает и отходит от корпуса",
                Phase.ReturnAlong => "Возвращение к боковой лестнице",
                _ => "Подъём по боковой лестнице на палубу"
            };
            SessionController.Instance.RecordBotEvent(player.BotNumber, Status);
        }
        public PlayerCommand Tick(float delta)
        {
            if (State != BotActionState.Running) return Idle;
            if (ship == null || ship.IsSinking || player.Motor.IsDead) { End(false, "Корабль потерян или бот погиб"); return Idle; }
            if (ladder == null || !ladder.isActiveAndEnabled) { End(false, "Боковая лестница недоступна; требуется другой путь возвращения"); return Idle; }
            if (player.BotRecallOutside && !Returning)
            {
                if (phase < Phase.Jump) { approach?.Cancel("Отзыв на корабль"); End(false, "Выход за борт отменён: срочная опасность"); return Idle; }
                Return("Срочный отзыв: кораблю необходимо уходить от зоны");
            }
            if (Vector3.Distance(anchor, Local) > .35f) { anchor = Local; progressAt = Time.time; }
            if (phase == Phase.Repair && Complete && section != null) TryNextRepair();
            var sails = ship.GetComponent<SailSystem>();
            bool activeMovement = (sails != null && sails.EffectiveDeploy > .1f && (ship.Capstan == null || !ship.Capstan.IsAnchored)) || Mathf.Abs(ship.Motor.Speed) > 5f;
            if (!Returning && phase >= Phase.Jump && (phase != Phase.Breathe && Time.time - started > 90f || Complete || activeMovement))
                Return("Работы завершены либо требуется возвращение: время или активное движение корабля");
            if (Returning && !player.Motor.IsSwimming && !player.Motor.IsClimbing && player.Motor.IsGrounded && player.Passenger.Ship == ship.Body)
            { End(repaired || section == null, repaired ? "Нет" : Failure); return Idle; }
            if (!Returning && phase >= Phase.Jump && phase != Phase.Breathe && player.Motor.IsSwimming && player.Motor.Breath < 8f)
            {
                afterBreathing = phase; breathingStarted = Time.time; SetPhase(Phase.Breathe);
            }
            if (phase == Phase.Breathe)
            {
                Report();
                progressAt = Time.time;
                if (player.Motor.BreathFraction < .98f)
                {
                    var surface = Local; surface.x = side * corridor;
                    return Swim(Surface(surface));
                }
                started += Time.time - breathingStarted;
                SetPhase(afterBreathing);
            }
            Report();
            if (phase == Phase.Wait)
            {
                if (Complete) { End(true, "Нет"); return Idle; }
                if (Mathf.Abs(ship.Motor.Speed) > .75f || sails != null && sails.EffectiveDeploy > .03f)
                {
                    if (Time.time - phaseAt > 60f) End(false, "Корабль не остановился; выход за борт отменён");
                    return Idle;
                }
                approach = new BotStationAction(player, SessionController.Instance.Config.BotMotion, new ExitStation(this), "Подойти к проверенной боковой лестнице перед выходом за борт");
                if (!approach.Begin(out var reason)) { End(false, reason); return Idle; }
                SetPhase(Phase.Approach);
            }
            if (phase == Phase.Approach)
            {
                if (Mathf.Abs(ship.Motor.Speed) > 1f) { approach.Cancel("Корабль начал движение"); End(false, "Выход за борт отменён: корабль начал движение"); return Idle; }
                var command = approach.Tick(delta);
                if (approach.State == BotActionState.Succeeded) SetPhase(Phase.Jump);
                else if (approach.State != BotActionState.Running) End(false, approach.Failure);
                return command;
            }
            if (phase == Phase.Jump)
            {
                if (player.Motor.IsSwimming) { SetPhase(Phase.SwimOut); return Idle; }
                if (Time.time - phaseAt > 8f) { Return("Не удалось выйти за борт; возвращение на палубу"); return Idle; }
                var command = Move(ladder.transform.forward);
                if (Time.time >= nextJump && (player.Motor.IsGrounded || player.Motor.IsClimbing)) { command.Jump = true; nextJump = Time.time + 1.2f; }
                return command;
            }
            if (!Returning && Time.time - progressAt > 12f && phase != Phase.Repair) Return("Проход в воде заблокирован; ремонт отложен");
            if (Returning && Time.time - phaseAt > 40f) { End(false, "Возвращение по лестнице задержалось; повторить подход"); return Idle; }
            if (phase == Phase.SwimOut || phase == Phase.ReturnOut)
            {
                var target = Local; target.x = side * corridor;
                var world = Surface(target);
                if (Vector3.Distance(player.transform.position, world) < .7f)
                {
                    if (departureTarget.HasValue && phase == Phase.SwimOut)
                    {
                        End(true, "Нет"); Status = "Выход за борт завершён"; return Idle;
                    }
                    SetPhase(Returning ? Phase.ReturnAlong : Phase.SwimAlong);
                }
                return Swim(world);
            }
            if (phase == Phase.SwimAlong || phase == Phase.ReturnAlong)
            {
                var target = new Vector3(side * corridor, Local.y, Returning ? LadderLocal.z : localPoint.z);
                var world = Surface(target);
                if (Vector3.Distance(player.transform.position, world) < .7f) SetPhase(Returning ? Phase.Board : Phase.Repair);
                return Swim(world);
            }
            if (phase == Phase.Repair)
            {
                if (Complete) { Return("Нет"); return Idle; }
                var point = ship.transform.TransformPoint(localPoint);
                var target = point + ship.transform.right * side * 1.4f - Vector3.up * 1.2f;
                target.y = Mathf.Min(target.y, OceanSurface.Instance.Height(target) - 1.25f);
                var command = Swim(target);
                if (Time.time >= nextStrike)
                {
                    nextStrike = Time.time + .55f;
                    int slot = BotMaintenanceAction.MalletSlot(player.GetComponent<PlayerInventory>());
                    if (slot < 0) { Return("Молоток утрачен"); return command; }
                    if (weapon.SelectServerSlot(slot) && repair.TryRepair(ship.NetworkObject, section.SectionId, fragment, localPoint)) progressAt = Time.time;
                    if (Complete)
                    {
                        TryNextRepair();
                        if (Complete) Return("Нет");
                    }
                    else if (Time.time - progressAt > 10f) Return("Нет доступа к повреждению с воды; возвращение");
                }
                return command;
            }
            var facing = -ladder.transform.forward;
            if (player.Motor.IsClimbing)
            {
                var command = Move(facing); command.Move = Vector2.up; return command;
            }
            var atLadder = ladder.transform.InverseTransformPoint(player.transform.position);
            if (atLadder.y >= ladder.Height - .2f && atLadder.z < -.5f)
                return Move(facing);
            var bottom = ladder.transform.TransformPoint(new Vector3(0, 0, 1.1f));
            bottom.y = OceanSurface.Instance.Height(bottom) - (player.GetComponent<NetworkEquipment>().WaterRunning ? 0f : 1.25f);
            var boarding = Swim(bottom);
            if (ladder.Contains(player.transform.position, false)) { boarding = Move(facing); boarding.Use = true; boarding.Rise = true; }
            return boarding;
        }
        void TryNextRepair()
        {
            float best = 144f;
            int nextFragment = -1;
            ShipDamageSection nextSection = null;
            Vector3 point = default;
            var destruction = ship.GetComponent<ShipDestruction>();
            if (destruction != null)
            {
                foreach (var candidateSection in destruction.Sections)
                {
                    if (candidateSection == null || candidateSection.RemovedFragments == 0) continue;
                    if (destruction.Definition(candidateSection.SectionId).Type != ShipSectionType.Hull) continue;
                    int count = Mathf.Min(64, candidateSection.RepairCount);
                    for (int i = 0; i < count; i++)
                    {
                        if ((candidateSection.RemovedFragments & (1UL << i)) == 0) continue;
                        var source = candidateSection.RepairTransform(i);
                        if (source == null) continue;
                        var world = source.TransformPoint(candidateSection.RepairBounds(i).ClosestPoint(source.InverseTransformPoint(player.transform.position + Vector3.up * 1.2f)));
                        var local = ship.transform.InverseTransformPoint(world);
                        float distance = (local - localPoint).sqrMagnitude;
                        if (Mathf.Sign(local.x) != side || distance >= best) continue;
                        nextFragment = i; nextSection = candidateSection; best = distance; point = local;
                    }
                }
            }
            if (nextFragment < 0 || nextSection == null) return;
            section = nextSection; fragment = nextFragment; localPoint = point; progressAt = Time.time;
            repaired = false;
            SessionController.Instance.RecordBotEvent(player.BotNumber, "Пробоина закрыта; продолжает ремонт соседнего повреждения с воды");
        }

        Vector3 Surface(Vector3 local)
        {
            var world = ship.transform.TransformPoint(local); world.y = OceanSurface.Instance.Height(world) - (player.GetComponent<NetworkEquipment>().WaterRunning ? 0f : 1.25f); return world;
        }
        void Return(string reason)
        {
            repaired = Complete;
            if (!repaired) Failure = reason;
            weapon.SelectServerSlot(previousSlot);
            SetPhase(player.Motor.IsClimbing ? Phase.Board : Phase.ReturnOut);
        }
        PlayerCommand Move(Vector3 direction)
        {
            direction.y = 0;
            return new PlayerCommand { Yaw = direction.sqrMagnitude > .001f ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : player.transform.eulerAngles.y,
                Move = direction.sqrMagnitude > .001f ? Vector2.up : Vector2.zero };
        }
        bool Clear(Vector3 direction)
        {
            var origin = player.transform.position + Vector3.up * .9f;
            int count = Physics.SphereCastNonAlloc(origin, .4f, direction, hits, .9f, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (!hits[i].transform.IsChildOf(player.transform)) return false;
            return true;
        }
        PlayerCommand Swim(Vector3 target)
        {
            var delta = target - player.transform.position;
            if (Time.time >= nextProbe)
            {
                nextProbe = Time.time + .2f;
                swimDirection = Vector3.ProjectOnPlane(delta, Vector3.up).normalized;
                if (swimDirection.sqrMagnitude > .01f && !Clear(swimDirection))
                {
                    var desired = swimDirection; swimDirection = Vector3.zero;
                    foreach (float angle in AvoidAngles)
                    {
                        var option = Quaternion.Euler(0, angle, 0) * desired;
                        if (!Clear(option)) continue;
                        swimDirection = option; break;
                    }
                }
            }
            var command = Move(delta.sqrMagnitude > .16f ? swimDirection : Vector3.zero);
            command.Move *= Mathf.Clamp01(new Vector2(delta.x, delta.z).magnitude * 1.5f);
            command.Rise = delta.y > .2f || player.Motor.Breath < 8f;
            command.Crouch = !command.Rise && delta.y < -.25f;
            return command;
        }
        bool End(bool success, string reason)
        {
            State = success ? BotActionState.Succeeded : BotActionState.Failed;
            Failure = success ? "Нет" : reason; Status = success ? departureTarget.HasValue ? "Выход за борт завершён" : "Вернулся на корабль; задача завершена" : "Задача за бортом прервана";
            approach?.Cancel(reason); weapon.SelectServerSlot(previousSlot); Report(true); return success;
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
            SessionController.Instance.RecordBotDecision(player.BotNumber, departureTarget.HasValue ? "Выход за борт" : section == null ? "Возвращение на корабль" : "Ремонт с воды и возвращение",
                "Физическое перемещение и боковая лестница", Status,
                $"Лестница: {(ladder != null ? ladder.name : "нет")}; воздух {player.Motor.Breath:F1}; стадия {Time.time - phaseAt:F1} с",
                "Проверки препятствий, воздуха и движения корабля", player.BotAssignment, Failure, player.BotCandidates);
        }
        sealed class ExitStation : IBotShipStation, IBotApproachConstraint
        {
            readonly BotHullWaterRepairAction job;
            bool reached;
            public ExitStation(BotHullWaterRepairAction job) => this.job = job;
            public string Name => "Выход к боковой лестнице";
            public Vector3 Position => job.ladder.transform.TransformPoint(new Vector3(0, job.ladder.Height + 1f, -.7f));
            public bool Available => job.ladder != null && job.ladder.isActiveAndEnabled;
            public bool Busy => false;
            public bool Complete => reached;
            public bool AllowsApproach(Vector3 feet) => job.ladder.transform.InverseTransformPoint(feet).z < -.4f;
            public void Validate() { }
            public bool Owned(NetworkPlayer player) => reached;
            public bool Acquire(NetworkPlayer player) { reached = true; return true; }
            public void Work(NetworkPlayer player, float delta) { }
            public void Release(NetworkPlayer player) { }
        }
    }
}
