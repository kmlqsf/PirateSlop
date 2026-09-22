using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotEatFishAction : IBotAction
    {
        readonly NetworkPlayer player;
        readonly NetworkFishing fishing;
        readonly NetworkWeapon weapon;
        readonly CombatHealth health;
        readonly int slot;
        int previous;
        float started, initialHealth;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status { get; private set; } = "Ест рыбу";
        public string Failure { get; private set; } = "Нет";
        public BotEatFishAction(NetworkPlayer player, int slot)
        {
            this.player = player; this.slot = slot;
            fishing = player.GetComponent<NetworkFishing>(); weapon = player.GetComponent<NetworkWeapon>();
            health = player.GetComponent<CombatHealth>();
        }
        public bool Begin(out string reason)
        {
            previous = player.GetComponent<PlayerInventory>().SelectedSlot;
            started = Time.time; initialHealth = health.Current;
            reason = "Восстановить здоровье рыбой из инвентаря";
            if (!weapon.SelectServerSlot(slot) || !fishing.TryEat())
            { Finish(false, "Не удалось начать лечение"); reason = Failure; return false; }
            Report(); return true;
        }
        public PlayerCommand Tick(float delta)
        {
            if (health.IsDead || health.Current < initialHealth || player.Motor.IsSwimming || player.Motor.IsClimbing ||
                player.BotVision.Visible || Time.time - started > 5f)
                Finish(false, "Лечение прервано угрозой или состоянием персонажа");
            else if (!fishing.IsEating) Finish(health.Current > initialHealth, health.Current > initialHealth ? "Рыба съедена" : "Лечение прервано");
            return new PlayerCommand { Yaw = player.transform.eulerAngles.y };
        }
        void Finish(bool success, string reason)
        {
            fishing.CancelEating(); weapon.SelectServerSlot(previous);
            State = success ? BotActionState.Succeeded : BotActionState.Failed;
            Status = reason; Failure = success ? "Нет" : reason; Report();
        }
        public void Cancel(string reason) { if (State == BotActionState.Running) { Finish(false, reason); State = BotActionState.Cancelled; } }
        void Report() => SessionController.Instance.RecordBotDecision(player.BotNumber, "Лечение", "Недостаток здоровья; есть рыба", Status,
            "Остаётся на палубе", player.BotVision.Status, player.BotAssignment, Failure, player.BotCandidates);
    }

    public sealed class BotRumStation : IBotShipStation
    {
        readonly NetworkShip ship;
        readonly RumShelf shelf;
        readonly int slot;
        int previous;
        bool done;
        public BotRumStation(NetworkShip ship, RumShelf shelf, int slot) { this.ship = ship; this.shelf = shelf; this.slot = slot; }
        public string Name => "Доставка рома на полку";
        public Vector3 Position => shelf.transform.position;
        public bool Available => ship != null && !ship.IsSinking && shelf != null && shelf.gameObject.activeInHierarchy;
        public bool Busy => false;
        public bool Complete => done || ship.RumCount >= NetworkShip.RumCapacity;
        public void Validate() { }
        public bool Owned(NetworkPlayer player) => done;
        public bool Acquire(NetworkPlayer player)
        {
            var weapon = player.GetComponent<NetworkWeapon>();
            previous = player.GetComponent<PlayerInventory>().SelectedSlot;
            done = weapon.SelectServerSlot(slot) && weapon.TryDepositRum(ship.NetworkObject);
            weapon.SelectServerSlot(previous); return done;
        }
        public void Work(NetworkPlayer player, float delta) { }
        public void Release(NetworkPlayer player) { }
    }

    public sealed partial class SessionController
    {
        void AssignConsumables(BotCrew crew)
        {
            bool rescueRum = false;
            foreach (var member in crew.Members)
                if (member != null && member.Motor.IsDead && !member.Eliminated.Value && crew.Ship.RumCount <= 0) rescueRum = true;
            bool rumBusy = false;
            foreach (var member in crew.Members)
                if (member != null && member.BotTaskRunning && member.BotTaskKey == 51000) rumBusy = true;
            foreach (var player in crew.Members)
            {
                if (player == null || !player.IsBot.Value || player.BotNeedsShipWait || (!player.BotCanReceiveWork(51000) && !(rescueRum && player.BotCanPlanReplacement(51000))) ||
                    crew.FloodEmergency && player.BotTaskRunning && player.BotTaskKey >= 100000) continue;
                if (!rescueRum && (player.BotVision.Visible || Time.time - player.BotVision.LastSeen < 5f)) continue;
                var inventory = player.GetComponent<PlayerInventory>();
                var health = player.GetComponent<CombatHealth>();
                var fishing = player.GetComponent<NetworkFishing>();
                int fish = -1, rum = -1;
                for (int i = 0; i < 6; i++)
                {
                    if (inventory.FishCount(i) > 0) fish = i;
                    if (inventory.RumCount(i) > 0) rum = i;
                }
                if (!rescueRum && fish >= 0 && fishing != null && health.MaxHealth - health.Current >= fishing.HealAmount && player.BotCanReceiveStation(50000))
                {
                    player.AssignBotJob(50000, new BotEatFishAction(player, fish));
                    if (!player.BotTaskRunning) player.FinishBotTask();
                    continue;
                }
                if (rumBusy || rum < 0 || !rescueRum && crew.Pilot.CombatTarget != null || crew.Ship.RumCount >= NetworkShip.RumCapacity ) continue;
                var shelf = crew.Ship.GetComponentInChildren<RumShelf>();
                if (shelf == null) continue;
                if (player.BotTaskRunning) player.PreemptBotTask("Доставить ром для немедленного возрождения товарища");
                if (!player.BotCanReceiveStation(51000)) continue;
                player.AssignBotStation(51000, new BotRumStation(crew.Ship, shelf, rum), "Пополнить корабельный запас реальным ромом из инвентаря");
                rumBusy = player.BotTaskRunning;
                if (!rumBusy) player.FinishBotTask();
            }
        }
    }
}
