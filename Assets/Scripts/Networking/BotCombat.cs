using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotCombatMemory
    {
        readonly NetworkPlayer[] nearest = new NetworkPlayer[3];
        public NetworkPlayer Target { get; private set; }
        public Vector3 LastPosition { get; private set; }
        public float LastSeen { get; private set; } = -100f;
        public float AcquiredAt { get; private set; }
        public bool Visible { get; private set; }
        public string Status => Target == null ? "Противник не обнаружен" :
            Visible ? $"Виден противник {Target.ParticipantId.Value}; наблюдение {Time.time - AcquiredAt:F1} с" :
            $"Цель потеряна; последняя позиция {LastPosition:F1}, {Time.time - LastSeen:F1} с назад";

        public static bool Enemy(NetworkPlayer observer, NetworkPlayer target) => target != null && target != observer && target.IsSpawned &&
            !target.Motor.IsDead && target.TeamId.Value != observer.TeamId.Value;
        public static Vector3 Eye(NetworkPlayer player) => player.transform.position + Vector3.up * (player.Motor.IsCrouched ? .75f : 1.65f);
        public static bool CanSee(NetworkPlayer observer, NetworkPlayer target)
        {
            if (observer.GetComponent<NetworkHolyGrenadeHands>()?.BotBlinded == true) return false;
            if (!Enemy(observer, target)) return false;
            var origin = Eye(observer);
            var point = target.transform.position + Vector3.up * (target.Motor.IsCrouched ? .65f : 1.1f);
            if ((point - origin).sqrMagnitude > 45f * 45f) return false;
            if (FirearmTrace.Cast(observer.gameObject, origin, point, out var hit))
                return hit.collider.GetComponentInParent<NetworkPlayer>() == target;
            return true;
        }
        public void Observe(NetworkPlayer observer, IEnumerable<NetworkPlayer> candidates, bool alert = false)
        {
            Visible = false;
            for (int i = 0; i < nearest.Length; i++) nearest[i] = null;
            foreach (var candidate in candidates)
            {
                if (!Enemy(observer, candidate)) continue;
                var offset = candidate.transform.position - observer.transform.position;
                float distance = offset.sqrMagnitude;
                if (distance > 45f * 45f || !alert && distance > 8f * 8f && Vector3.Dot(observer.transform.forward, offset.normalized) < -.3f) continue;
                for (int i = 0; i < nearest.Length; i++)
                    if (nearest[i] == null || distance < (nearest[i].transform.position - observer.transform.position).sqrMagnitude)
                    {
                        for (int j = nearest.Length - 1; j > i; j--) nearest[j] = nearest[j - 1];
                        nearest[i] = candidate; break;
                    }
            }
            foreach (var candidate in nearest)
            {
                if (candidate == null || !CanSee(observer, candidate)) continue;
                if (Target != candidate || Time.time - LastSeen > 2f) AcquiredAt = Time.time;
                Target = candidate; LastPosition = candidate.transform.position; LastSeen = Time.time; Visible = true; return;
            }
            if (!Enemy(observer, Target) || Time.time - LastSeen > 5f) Target = null;
        }
    }

    public sealed class BotPersonalCombatAction : IBotAction
    {
        readonly NetworkPlayer player;
        readonly NetworkPlayer target;
        readonly NetworkWeapon network;
        readonly BotPersonalWeapons weapons;
        readonly PlayerInventory inventory;
        readonly BotCombatPosition position;
        int previousSlot;
        float readyAt, nextDecision, lastSeen, nextReport, started;
        Vector3 aim;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status { get; private set; } = "Оценка противника";
        public string Failure { get; private set; } = "Нет";
        public BotPersonalCombatAction(NetworkPlayer player, NetworkPlayer target)
        {
            this.player = player; this.target = target;
            network = player.GetComponent<NetworkWeapon>(); weapons = new BotPersonalWeapons(player); inventory = player.GetComponent<PlayerInventory>();
            position = new BotCombatPosition(player);
        }
        int Slot(bool sabre)
        {
            if (!sabre) return weapons.RangedSlot(Vector3.Distance(player.transform.position, aim));
            for (int i = 0; i < 6; i++) if (inventory.HasSabre(i)) return i;
            return -1;
        }
        public bool Begin(out string reason)
        {
            previousSlot = inventory.SelectedSlot;
            if (target != null) aim = target.transform.position + Vector3.up * 1.1f;
            reason = "Нет доступного оружия или видимой вражеской цели";
            if (!player.IsServerInitialized || !BotCombatMemory.CanSee(player, target) || Slot(false) < 0 && Slot(true) < 0)
            { Finish(false, reason); return false; }
            started = lastSeen = Time.time; readyAt = Time.time + .2f;
            aim = target.transform.position + Vector3.up * 1.1f;
            reason = "Реакция на видимого противника"; return true;
        }
        bool ClearForAllies(Vector3 end, bool melee)
        {
            var start = BotCombatMemory.Eye(player);
            float spread = 0f;
            if (!melee)
            {
                var definition = weapons.Selected;
                if (definition == null) return false;
                end = start + (end - start).normalized * definition.Ballistics.Range;
                spread = definition.AimSpread + definition.MovingSpread * .3f;
            }
            return SessionController.Instance.BotPersonalShotSafe(player, start, end, melee, spread);
        }

        public PlayerCommand Tick(float delta)
        {
            var command = new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            if (!BotCombatMemory.Enemy(player, target) || player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing ||
                player.Motor.IsKnockedBack || player.Motor.LocomotionLocked || player.Ship == null || player.Passenger.Ship != player.Ship.Body)
            { Finish(true, "Бой закончен или состояние персонажа изменилось"); return command; }
            if (Time.time - started > 20f) { Finish(true, "Повторная оценка задач экипажа"); return command; }
            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + .2f;
                if (!BotCombatMemory.CanSee(player, target))
                {
                    Status = "Цель скрылась; не стреляет";
                    if (!weapons.Reloading) position.Plan(aim, target, false, false);
                    if (Time.time - lastSeen > 5f) Finish(true, "Цель потеряна");
                }
                else
                {
                    lastSeen = Time.time;
                    aim = target.transform.position + Vector3.up * (target.Motor.IsCrouched ? .65f : 1.1f);
                    float distance = Vector3.Distance(player.transform.position, target.transform.position);
                    bool melee = distance < 2.1f && Slot(true) >= 0;
                    bool close = Slot(true) >= 0 && (distance < 2.1f || target.Passenger.Ship == player.Ship.Body && (Slot(false) < 0 || distance < 6f));

                    int slot = Slot(melee);
                    if (slot < 0 && close) slot = Slot(true);
                    if (slot < 0) { Finish(false, "Нет подходящего оружия на этой дистанции"); return command; }
                    if (inventory.SelectedSlot != slot)
                    {
                        network.SelectServerSlot(slot);
                        readyAt = Time.time + Mathf.Max(.5f, weapons.Selected != null ? weapons.Selected.AimSeconds : 0f);
                    }
                    position.Plan(aim, target, close, !close && (!weapons.Loaded || weapons.Reloading));
                    var direction = (aim - BotCombatMemory.Eye(player)).normalized;
                    float angle = Vector3.Angle(Vector3.ProjectOnPlane(direction, Vector3.up), player.transform.forward);
                    if (!ClearForAllies(aim, melee)) Status = "Союзник на линии атаки; ожидание";
                    else if (Time.time < readyAt || angle > 8f) Status = "Разворот и прицеливание";
                    else if (!melee && !inventory.SabreSelected && !weapons.Loaded)
                    {
                        weapons.Act(true, false, direction, Vector3.up * 1.65f); Status = "Перезарядка: " + weapons.Name;
                    }
                    else if (position.Moving || inventory.SabreSelected && !melee) Status = "Занимает позицию перед атакой";
                    else if (!melee && weapons.Selected != null && distance > weapons.Selected.Ballistics.Range) Status = "Цель вне дальности оружия";
                    else
                    {
                        float error = Mathf.Sin(Time.time * 2.3f + player.BotNumber) * .8f;
                        var shotDirection = Quaternion.Euler(error * .4f, error, 0) * direction;
                        var end = BotCombatMemory.Eye(player) + shotDirection * Mathf.Min(distance + .5f, 45f);
                        if (ClearForAllies(end, melee) && (!FirearmTrace.Cast(player.gameObject, BotCombatMemory.Eye(player), end, out var hit) ||
                            hit.collider.GetComponentInParent<NetworkPlayer>() == target))
                        {
                            if (weapons.Act(false, melee, shotDirection, Vector3.up * (player.Motor.IsCrouched ? .75f : 1.65f)))
                            { Status = melee ? "Удар саблей" : "Выстрел: " + weapons.Name; readyAt = Time.time + (melee ? 1f : .8f); }
                        }
                        else Status = "Линия атаки перекрыта";
                    }
                }
            }
            if (State != BotActionState.Running) return command;
            var offset = aim - BotCombatMemory.Eye(player);
            command.Yaw = Mathf.MoveTowardsAngle(command.Yaw, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, 140f * delta);
            command.Pitch = Mathf.Clamp(-Mathf.Atan2(offset.y, new Vector2(offset.x, offset.z).magnitude) * Mathf.Rad2Deg, -85f, 85f);
            position.Move(ref command);
            Report(); return command;
        }
        void Finish(bool success, string reason)
        {
            State = success ? BotActionState.Succeeded : BotActionState.Failed;
            Failure = success ? "Нет" : reason; Status = reason;
            position.Clear(); network.SelectServerSlot(previousSlot); Report(true);
        }
        public void Cancel(string reason) { if (State == BotActionState.Running) { Finish(false, reason); State = BotActionState.Cancelled; } }
        void Report(bool force = false)
        {
            if (!force && Time.time < nextReport) return;
            nextReport = Time.time + .5f;
            SessionController.Instance.RecordBotDecision(player.BotNumber, "Личный бой", "Видимый противник; проверка линии атаки", Status,
                position.Status, player.BotVision.Status, player.BotAssignment, Failure, player.BotCandidates);
        }
    }
}
