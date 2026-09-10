using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        bool BotCanWork => IsServerInitialized && GetComponent<NetworkPlayer>().IsBot.Value && CanHandleBall();
        public bool BotTakeKit(NetworkCannon cannon)
        {
            if (!BotCanWork || cannon == null || cannon.Crate == null || !CanReach(cannon.Crate.Kit.transform.position, cannon.Crate.Kit.transform)) return false;
            int slot = inventory.EmptySlot();
            if (slot < 0 || !cannon.TakeKit()) return false;
            cannonSlots.Value |= 1 << slot; ApplyInventory(); return true;
        }
        public bool BotPlaceCannon(NetworkCannon cannon, Vector3 position, Quaternion rotation)
        {
            if (!BotCanWork || cannon == null || !PlayerInventory.CanPlace(cannon.Crate, position, rotation, GetComponent<AdvancedPlayerController>())) return false;
            var root = cannon.transform;
            foreach (var hit in Physics.OverlapBox(root.TransformPoint(position) + root.up * .65f, new Vector3(.85f, .5f, 1.5f), root.rotation * rotation, ~0, QueryTriggerInteraction.Ignore))
                if (hit.GetComponentInParent<AdvancedPlayerController>() == null) return false;
            for (int slot = 0; slot < 6; slot++)
            {
                if (!inventory.HasCannon(slot)) continue;
                cannon.Place(position, rotation); cannonSlots.Value &= ~(1 << slot); ApplyInventory(); return true;
            }
            return false;
        }
        public bool BotLoadCannon(SimpleCannon cannon)
        {
            if (!BotCanWork || cannon == null || cannon.Network == null) return false;
            for (int slot = 0; slot < ballCounts.Count; slot++)
            {
                if (ballCounts[slot] <= 0 || ballItems[slot] == InventoryItem.BoardingHook || ballItems[slot] == InventoryItem.BoomerangCannonball) continue;
                if (!cannon.Network.LoadInventoryBall(this, cannon.Index, ballItems[slot])) return false;
                ballCounts[slot]--; ApplyInventory(); return true;
            }
            return false;
        }
        public bool BotSupplyBalls(NetworkCannon cannon)
        {
            if (!BotCanWork || cannon == null || cannon.Crate == null || !CanReach(cannon.Crate.Supply.transform.position, cannon.Crate.Supply.transform)) return false;
            for (int i = 0; i < 3; i++) if (!AddItem(InventoryItem.Cannonball)) return i > 0;
            return true;
        }
        public bool BotTakeLoose(NetworkFish item)
        {
            if (!BotCanWork || item == null || !item.Available || !CanReach(item.transform.position, item.transform) || !CanAddItem(item.CurrentItem)) return false;
            if (!AddItem(item.CurrentItem)) return false;
            return item.Take();
        }
        public bool BotLoot(NetworkLootChest chest)
        {
            if (!BotCanWork || chest == null || !chest.IsSpawned || !CanReach(chest.transform.position + Vector3.up * .4f, chest.transform)) return false;
            chest.Open();
            for (int slot = 0; slot < chest.SlotCount; slot++)
            {
                var item = chest.ItemAt(slot);
                if (item == InventoryItem.None || !CanAddItem(item)) continue;
                chest.Take(this, slot); return chest.ItemAt(slot) == InventoryItem.None;
            }
            return false;
        }
        public bool BotUnload(NetworkShip ship)
        {
            if (!BotCanWork || ship == null || ship.IsSinking || GetComponent<ShipDeckPassenger>().Ship != ship.Body) return false;
            var shelf = ship.GetComponentInChildren<RumShelf>();
            if (shelf != null && Vector3.Distance(transform.position, shelf.transform.position) > 3f) return false;
            bool stored = false;
            for (int slot = 0; slot < 6; slot++)
            {
                if (rumCounts[slot] > 0) { int count = ship.StoreRum(rumCounts[slot]); rumCounts[slot] -= count; stored |= count > 0; }
            }
            ApplyInventory();
            int selected = selectedSlot.Value;
            for (int slot = 0; slot < 6; slot++)
            {
                var item = inventory.ItemAt(slot);
                if (item == InventoryItem.None || item == InventoryItem.Pistol || item == InventoryItem.Rod || item == InventoryItem.Sabre || item == InventoryItem.Cannon || CannonAmmo.IsBall(item)) continue;
                selectedSlot.Value = slot;
                for (int count = 0; count < 20 && inventory.ItemAt(slot) != InventoryItem.None; count++) DropSelectedAuthority();
            }
            selectedSlot.Value = selected;
            return stored;
        }
    }
}
