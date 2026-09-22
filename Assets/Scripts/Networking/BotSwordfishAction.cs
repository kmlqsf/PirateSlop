using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotSwordfishAction : IBotAction
    {
        readonly NetworkPlayer player, target;
        readonly NetworkWeapon weapon;
        readonly PlayerInventory inventory;
        readonly RaycastHit[] hits = new RaycastHit[24];
        readonly int slot;
        int previous;
        float ready, deadline, nextPlan, observedAt;
        Vector3 observed, velocity, direction;
        bool safe;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status { get; private set; } = "Подготовка броска рыбы-меча";
        public string Failure { get; private set; } = "Нет";
        public static int Slot(NetworkPlayer player)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            for (int i = 0; i < 6; i++) if (inventory.EquipmentAt(i) == InventoryItem.Swordfish) return i;
            return -1;
        }
        public BotSwordfishAction(NetworkPlayer player, NetworkPlayer target, int slot)
        {
            this.player = player; this.target = target; this.slot = slot;
            weapon = player.GetComponent<NetworkWeapon>(); inventory = player.GetComponent<PlayerInventory>();
        }
        public bool Begin(out string reason)
        {
            previous = inventory.SelectedSlot; ready = Time.time + .8f; deadline = Time.time + 4f;
            observedAt = Time.time;
            reason = Status;
            if (!BotCombatMemory.CanSee(player, target) || inventory.EquipmentAt(slot) != InventoryItem.Swordfish || !weapon.SelectServerSlot(slot))
            { Finish(false, "Нет видимой цели или рыбы-меча"); reason = Failure; return false; }
            observed = target.transform.position + Vector3.up;
            return true;
        }
        bool Plan()
        {
            var origin = player.transform.position + Vector3.up * 1.5f;
            var inherited = player.Ship.Motor.CannonPointVelocity(origin);
            float best = float.PositiveInfinity, flight = 0;
            for (int i = 2; i <= 26; i++)
            {
                float t = i * .05f;
                var required = (observed + velocity * t - origin - Physics.gravity * (.5f * t * (t + Time.fixedDeltaTime))) /
                    (t + .6f / NetworkFishProjectile.SwordfishSpeed) - inherited * t / (t + .6f / NetworkFishProjectile.SwordfishSpeed);
                float error = Mathf.Abs(required.magnitude - NetworkFishProjectile.SwordfishSpeed);
                if (error >= best) continue;
                best = error; flight = t; direction = required.normalized;
            }
            if (best > 1f) return false;
            var start = origin + direction * .6f;
            if (FirearmTrace.Cast(player.gameObject, origin, start, out _)) return false;
            var launch = direction * NetworkFishProjectile.SwordfishSpeed + inherited;
            var previousPoint = start;
            int steps = Mathf.CeilToInt(flight / .05f);
            for (int i = 1; i <= steps; i++)
            {
                float t = flight * i / steps;
                var next = start + launch * t + Physics.gravity * (.5f * t * (t + Time.fixedDeltaTime));
                if (SessionController.Instance.BotAllyInShotSegment(previousPoint, next, .8f, t, player, t < .3f)) return false;
                var delta = next - previousPoint;
                int count = Physics.SphereCastNonAlloc(previousPoint, .2f, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Collide);
                if (count == hits.Length) return false;
                bool targetHit = false;
                for (int j = 0; j < count; j++)
                {
                    var hit = hits[j];
                    if (!PlayerHitbox.IsTarget(hit.collider) || hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris") ||
                        t < .3f && hit.transform.IsChildOf(player.transform)) continue;
                    if (hit.collider.GetComponentInParent<NetworkPlayer>() == target) targetHit = true;
                    else return false;
                }
                if (targetHit) return true;
                if (OceanSurface.Instance != null && next.y <= OceanSurface.Instance.Height(next)) return false;
                previousPoint = next;
            }
            return false;
        }
        public PlayerCommand Tick(float delta)
        {
            var command = new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            if (player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing || player.Motor.IsKnockedBack || player.Motor.LocomotionLocked ||
                player.Ship == null || player.Passenger.Ship != player.Ship.Body || !BotCombatMemory.CanSee(player, target) || Time.time > deadline)
            { Finish(false, "Бросок отменён: цель скрылась, истёк срок или изменилось состояние"); return command; }
            if (Time.time >= nextPlan)
            {
                nextPlan = Time.time + .25f;
                var point = target.transform.position + Vector3.up;
                float elapsed = Time.time - observedAt;
                velocity = elapsed > .05f ? Vector3.ClampMagnitude((point - observed) / elapsed, 12f) : Vector3.zero;
                observed = point; observedAt = Time.time;
                safe = Plan();
                Status = safe ? "Прицеливается рыбой-мечом" : "Нет безопасного прямого попадания; бросок запрещён";
                Report();
            }
            command.Yaw = Mathf.MoveTowardsAngle(command.Yaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 140f * delta);
            command.Pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (safe && Time.time >= ready && Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, command.Yaw)) < .1f &&
                Vector3.Angle(Vector3.ProjectOnPlane(direction, Vector3.up), player.transform.forward) < 5f && Plan())
                Finish(weapon.ThrowFish(InventoryItem.Swordfish, direction), "Бросок рыбы-меча");
            return command;
        }
        void Finish(bool success, string reason)
        {
            State = success ? BotActionState.Succeeded : BotActionState.Failed;
            Status = reason; Failure = success ? "Нет" : reason; weapon.SelectServerSlot(previous); Report();
        }
        public void Cancel(string reason) { if (State == BotActionState.Running) { Finish(false, reason); State = BotActionState.Cancelled; } }
        void Report() => SessionController.Instance.RecordBotDecision(player.BotNumber, "Рыба-меч", "Видимая цель; проверка дуги и союзников",
            Status, "Бросок со своей палубы", player.BotVision.Status, player.BotAssignment, Failure, player.BotCandidates);
    }
}
