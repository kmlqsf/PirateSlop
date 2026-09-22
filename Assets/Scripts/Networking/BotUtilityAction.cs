using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotUtilityAction : IBotAction
    {
        readonly NetworkPlayer player, target;
        readonly InventoryItem item;
        readonly int slot;
        readonly NetworkWeapon weapon;
        readonly NetworkFishing fishing;
        readonly NetworkEquipment equipment;
        int previous, attempts;
        float started, next;
        bool used;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status { get; private set; }
        public string Failure { get; private set; } = "Нет";
        public static int Slot(NetworkPlayer player, InventoryItem item)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            for (int i = 0; i < 6; i++) if (inventory.ItemAt(i) == item) return i;
            return -1;
        }
        public BotUtilityAction(NetworkPlayer player, InventoryItem item, int slot, NetworkPlayer target = null)
        {
            this.player = player; this.item = item; this.slot = slot; this.target = target;
            weapon = player.GetComponent<NetworkWeapon>(); fishing = player.GetComponent<NetworkFishing>(); equipment = player.GetComponent<NetworkEquipment>();
        }
        public bool Begin(out string reason)
        {
            previous = player.GetComponent<PlayerInventory>().SelectedSlot; started = Time.time;
            Status = item == InventoryItem.Rod ? "Рыбалка" : item == InventoryItem.BombParrot ? "Управление попугаем" : "Зацеп противника крюком";
            reason = Status;
            if (!weapon.SelectServerSlot(slot)) { Finish(false, "Нет предмета"); return false; }
            return true;
        }
        public PlayerCommand Tick(float delta)
        {
            var command = new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            if (player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing || player.Ship == null ||
                player.Passenger.Ship != player.Ship.Body || Time.time - started > (item == InventoryItem.Rod ? 25f : 8f))
            { Finish(false, "Задача прервана состоянием или сроком"); return command; }
            if (Time.time < next) return command;
            next = Time.time + .2f;
            if (item == InventoryItem.Rod)
            {
                if (player.BotVision.Visible) { Finish(false, "Рыбалка прервана угрозой"); return command; }
                if (used && !fishing.IsFishing) { Finish(true, "Рыбалка завершена"); return command; }
                var direction = Quaternion.Euler(0, attempts++ * 45f, 0) * player.Ship.transform.forward;
                used |= fishing.BotFish(direction);
                if (!used && attempts >= 8) Finish(false, "Нет свободной дуги заброса с этой позиции");
            }
            else
            {
                var drone = player.Motor.ActiveParrot;
                if (item == InventoryItem.BombParrot && used)
                {
                    if (drone == null) { Finish(true, "Полёт попугая завершён"); return command; }
                    var aim = Vector3.up;
                    if (BotCombatMemory.Enemy(player, target) && player.GetComponent<NetworkHolyGrenadeHands>()?.BotBlinded != true &&
                        (!FirearmTrace.Cast(drone.gameObject, drone.transform.position, target.transform.position + Vector3.up, out var hit) || hit.collider.GetComponentInParent<NetworkPlayer>() == target))
                        aim = (target.transform.position + Vector3.up - drone.transform.position).normalized;
                    drone.BotSteer(player, aim); return command;
                }
                if (!BotCombatMemory.CanSee(player, target)) { Finish(false, "Противник скрыт"); return command; }
                var direction = (target.transform.position + Vector3.up - BotCombatMemory.Eye(player)).normalized;
                command.Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                if (Time.time - started < .8f) return command;
                if (!used)
                {
                    if (SessionController.Instance.BotAllyInShotSegment(BotCombatMemory.Eye(player), target.transform.position + Vector3.up, .8f, .5f, player, true))
                    { Finish(false, "Союзник на пути предмета"); return command; }
                    if (item == InventoryItem.BombParrot) used = equipment.TryBotUtility(direction);
                    else { weapon.ThrowGrapple(direction, Vector3.up * 1.5f); used = true; }
                    if (!used) Finish(false, "Сервер отклонил применение");
                }
                if (item == InventoryItem.GrapplingHook && (Time.time - started > 3f || Vector3.Distance(player.transform.position, target.transform.position) < 2.5f || weapon.GrappleActive))
                    Finish(true, "Крюк освобождён; возврат к бою");
            }
            SessionController.Instance.RecordBotDecision(player.BotNumber, Status, "Реальный предмет и штатное серверное действие", Status,
                "На своей палубе", player.BotVision.Status, player.BotAssignment, Failure, player.BotCandidates);
            return command;
        }
        void Finish(bool success, string reason)
        {
            if (item == InventoryItem.Rod) fishing.CancelBotFishing();
            if (item == InventoryItem.GrapplingHook) weapon.ReleaseGrapple();
            if (item == InventoryItem.BombParrot && player.Motor.ActiveParrot != null) player.Motor.ActiveParrot.BotSteer(player, Vector3.up);
            weapon.SelectServerSlot(previous); State = success ? BotActionState.Succeeded : BotActionState.Failed;
            Status = reason; Failure = success ? "Нет" : reason;
        }
        public void Cancel(string reason) { Finish(false, reason); State = BotActionState.Cancelled; }
    }
}
