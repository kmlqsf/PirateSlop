using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotFishingStation : IBotShipStation
    {
        readonly ShipLadder ladder;
        readonly int slot;
        BotUtilityAction action;
        public BotFishingStation(ShipLadder ladder, int slot) { this.ladder = ladder; this.slot = slot; }
        public string Name => action?.Status ?? "Подойти к борту для рыбалки";
        public Vector3 Position => ladder.transform.TransformPoint(new Vector3(0, ladder.Height + 1f, -2.4f));
        public bool Available => ladder != null && ladder.isActiveAndEnabled;
        public bool Busy => false;
        public bool Complete => action != null && action.State == BotActionState.Succeeded;
        public void Validate() { }
        public bool Owned(NetworkPlayer player) => action != null && action.State != BotActionState.Failed && action.State != BotActionState.Cancelled;
        public bool Acquire(NetworkPlayer player)
        {
            action = new BotUtilityAction(player, InventoryItem.Rod, slot);
            return action.Begin(out _);
        }
        public void Work(NetworkPlayer player, float delta) => action.Tick(delta);
        public void Release(NetworkPlayer player) { if (action != null && action.State == BotActionState.Running) action.Cancel("Рыбалка прервана новой задачей"); }
    }
}
