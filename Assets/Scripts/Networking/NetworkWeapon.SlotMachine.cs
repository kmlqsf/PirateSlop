using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public bool ConsumeSlotFish(int slot)
        {
            if (!IsServerInitialized || !CanHandleBall() || GetComponent<CannonHands>().HasHeldBall ||
                slot != selectedSlot.Value || slot < 0 || slot >= fishCounts.Count || fishCounts[slot] < 1 ||
                inventory.ItemAt(slot) != InventoryItem.Fish) return false;
            fishCounts[slot]--;
            ApplyInventory();
            return true;
        }
    }
}
