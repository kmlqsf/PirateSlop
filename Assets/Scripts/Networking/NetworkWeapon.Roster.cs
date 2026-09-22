using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void TransferInventoryTo(NetworkWeapon target)
        {
            if (!IsServerInitialized || target == null || !target.IsServerInitialized || target == this)
                throw new System.InvalidOperationException("Inventory replacement requires two server players.");
            target.cannonSlots.Value = cannonSlots.Value;
            target.sabreSlots.Value = sabreSlots.Value;
            target.pistolSlots.Value = pistolSlots.Value;
            target.rodSlots.Value = rodSlots.Value;
            target.malletSlots.Value = malletSlots.Value;
            CopyRosterItems(fishCounts, target.fishCounts);
            CopyRosterItems(ballCounts, target.ballCounts);
            CopyRosterItems(ballItems, target.ballItems);
            CopyRosterItems(plankCounts, target.plankCounts);
            CopyRosterItems(rumCounts, target.rumCounts);
            CopyRosterItems(equipmentItems, target.equipmentItems);
            target.loaded.Value = weapon.Loaded;
            target.reloading.Value = false;
            target.weapon.SetState(weapon.Loaded, false);
            GetComponent<NetworkEquipment>().TransferAmmunitionTo(target.GetComponent<NetworkEquipment>());
            target.ApplyInventory();
            cannonSlots.Value = sabreSlots.Value = pistolSlots.Value = rodSlots.Value = malletSlots.Value = 0;
            for (int i = 0; i < fishCounts.Count; i++) fishCounts[i] = 0;
            for (int i = 0; i < ballCounts.Count; i++) ballCounts[i] = 0;
            for (int i = 0; i < plankCounts.Count; i++) plankCounts[i] = 0;
            for (int i = 0; i < rumCounts.Count; i++) rumCounts[i] = 0;
            for (int i = 0; i < equipmentItems.Count; i++) equipmentItems[i] = InventoryItem.None;
            ApplyInventory();
        }

        static void CopyRosterItems<T>(SyncList<T> source, SyncList<T> target)
        {
            target.Clear();
            for (int i = 0; i < source.Count; i++) target.Add(source[i]);
        }
    }
}
