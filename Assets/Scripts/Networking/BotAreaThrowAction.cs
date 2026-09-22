using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotAreaThrowAction : IBotAction
    {
        readonly NetworkPlayer player, target;
        readonly NetworkWeapon weapon;
        readonly NetworkHolyGrenadeHands grenade;
        readonly PlayerInventory inventory;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly int slot;
        readonly InventoryItem item;
        int previous, candidate;
        float started, nextPlan, seenAt;
        Vector3 observed, targetVelocity, direction;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status { get; private set; } = "Выбор броска";
        public string Failure { get; private set; } = "Нет";
        string Name => item == InventoryItem.Pufferfish ? "Фугу" : "Святая граната";
        public static int Slot(NetworkPlayer player)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            for (int i = 0; i < 6; i++)
                if (inventory.EquipmentAt(i) == InventoryItem.Pufferfish || inventory.EquipmentAt(i) == InventoryItem.HolyGrenade) return i;
            return -1;
        }
        public BotAreaThrowAction(NetworkPlayer player, NetworkPlayer target, int slot)
        {
            this.player = player; this.target = target; this.slot = slot;
            inventory = player.GetComponent<PlayerInventory>(); weapon = player.GetComponent<NetworkWeapon>();
            grenade = player.GetComponent<NetworkHolyGrenadeHands>(); item = inventory.EquipmentAt(slot);
        }
        public bool Begin(out string reason)
        {
            previous = inventory.SelectedSlot; started = seenAt = Time.time;
            reason = "Оценка места взрыва";
            if (!BotCombatMemory.CanSee(player, target) || !weapon.SelectServerSlot(slot) ||
                item == InventoryItem.HolyGrenade && (grenade == null || grenade.Prefab == null || grenade.Primed))
            { Finish(false, "Предмет или цель недоступны"); reason = Failure; return false; }
            observed = target.transform.position + Vector3.up;
            return true;
        }
        bool Predict(Vector3 forward)
        {
            bool holy = item == InventoryItem.HolyGrenade;
            float duration = holy ? grenade.FuseSeconds : NetworkFishProjectile.PufferFuse;
            if (duration <= 0 || duration > 5f) return false;
            Vector3 point, speed;
            if (holy) grenade.GetLaunch(forward, out point, out speed);
            else
            {
                var eye = player.transform.position + Vector3.up * 1.5f;
                point = eye + forward * .6f;
                if (FirearmTrace.Cast(player.gameObject, eye, point, out _)) return false;
                speed = forward * NetworkFishProjectile.PufferSpeed + player.Ship.Motor.CannonPointVelocity(point);
            }
            int bounces = 0, steps = 0;
            float elapsed = 0;
            while (elapsed < duration)
            {
                if (++steps > 260) return false;
                float dt = Mathf.Min(holy ? NetworkHolyGrenade.Step : Time.fixedDeltaTime, duration - elapsed);
                if (dt < .005f) break;
                var delta = holy ? speed * dt + Physics.gravity * (.5f * dt * dt) : (speed + Physics.gravity * dt) * dt;
                speed += Physics.gravity * dt;
                int count = Physics.SphereCastNonAlloc(point, holy ? .13f : .12f, delta.normalized, hits, delta.magnitude, ~0,
                    holy ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
                if (count == hits.Length) return false;
                RaycastHit nearest = default; float distance = delta.magnitude;
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (hit.transform.IsChildOf(player.transform) && (holy || elapsed < .3f)) continue;
                    if (holy ? hit.collider.GetComponentInParent<NetworkFish>() != null :
                        !PlayerHitbox.IsTarget(hit.collider) || hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris")) continue;
                    if (hit.distance > distance) continue;
                    distance = hit.distance; nearest = hit;
                }
                var next = nearest.collider == null ? point + delta : point + delta.normalized * Mathf.Max(0, distance - .005f);
                if (SessionController.Instance.BotAllyInShotSegment(point, next, .5f, elapsed + dt, player, elapsed < .3f)) return false;
                elapsed += dt;
                if (nearest.collider != null)
                {
                    var support = nearest.collider.GetComponentInParent<NetworkShip>();
                    if (holy)
                    {
                        point = next;
                        if (support != null) point += support.Motor.CannonPointVelocity(point) * (duration - elapsed);
                        elapsed = duration; break;
                    }
                    if (++bounces > 5) return false;
                    point = nearest.point + nearest.normal * .15f;
                    speed = Vector3.Reflect(speed, nearest.normal) * NetworkFishProjectile.PufferBounce;
                }
                else point = next;
                if (OceanSurface.Instance != null && point.y <= OceanSurface.Instance.Height(point))
                {
                    point.y = OceanSurface.Instance.Height(point);
                    if (!holy) duration = elapsed;
                    break;
                }
            }
            float radius = holy ? grenade.Prefab.FlashRadius : NetworkFishProjectile.PufferRadius;
            if (SessionController.Instance.BotAllyInShotSegment(point, point, radius + 2f, duration, player)) return false;
            var predictedTarget = observed + targetVelocity * duration;
            float useful = holy ? Mathf.Min(radius * .65f, grenade.Prefab.FullFlashRadius + 3f) : 2.5f;
            if (Vector3.Distance(point, predictedTarget) > useful) return false;
            if (!holy && FirearmTrace.Cast(player.gameObject, point, predictedTarget, out var block) &&
                block.collider.GetComponentInParent<NetworkPlayer>() != target) return false;
            return true;
        }
        public PlayerCommand Tick(float delta)
        {
            var command = new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            if (player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing || player.Motor.IsKnockedBack || player.Motor.LocomotionLocked ||
                player.Ship == null || player.Passenger.Ship != player.Ship.Body || !BotCombatMemory.CanSee(player, target) || Time.time - started > 6f)
            { Finish(false, "Бросок отменён; нет безопасной возможности"); return command; }
            if (Time.time >= nextPlan)
            {
                nextPlan = Time.time + .5f;
                var point = target.transform.position + Vector3.up;
                targetVelocity = Vector3.ClampMagnitude((point - observed) / Mathf.Max(.1f, Time.time - seenAt), 12f);
                observed = point; seenAt = Time.time;
                var flat = Vector3.ProjectOnPlane(observed - player.transform.position, Vector3.up).normalized;
                float angle = 10f + candidate++ % 6 * 12f;
                direction = flat * Mathf.Cos(angle * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(angle * Mathf.Deg2Rad);
                bool aligned = Vector3.Angle(flat, player.transform.forward) < 6f;
                if (Time.time - started >= .8f && aligned && Predict(direction))
                {
                    bool thrown = item == InventoryItem.HolyGrenade ? grenade.TryBotThrow(direction) : weapon.ThrowFish(item, direction);
                    Finish(thrown, thrown ? "Предмет брошен: " + Name : "Сервер отклонил бросок"); return command;
                }
                Status = "Проверяет дугу, отскоки и союзников у места взрыва"; Report();
            }
            command.Yaw = Mathf.MoveTowardsAngle(command.Yaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 140f * delta);
            command.Pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            return command;
        }
        void Finish(bool success, string reason)
        {
            State = success ? BotActionState.Succeeded : BotActionState.Failed; Status = reason; Failure = success ? "Нет" : reason;
            weapon.SelectServerSlot(previous); Report();
        }
        public void Cancel(string reason) { if (State == BotActionState.Running) { Finish(false, reason); State = BotActionState.Cancelled; } }
        void Report() => SessionController.Instance.RecordBotDecision(player.BotNumber, Name, "Видимая цель; прогноз места взрыва", Status,
            "Бросок со своей палубы", player.BotVision.Status, player.BotAssignment, Failure, player.BotCandidates);
    }
}
