using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkLootChest : NetworkBehaviour
    {
        public LootCatalog Catalog;
        public Transform Lid;
        readonly SyncVar<bool> opened = new();
        readonly SyncList<InventoryItem> contents = new();
        public int SlotCount => contents.Count;
        public InventoryItem ItemAt(int slot) => slot >= 0 && slot < contents.Count ? contents[slot] : InventoryItem.None;
        public string Hint(PlayerInventory inventory) => "E — открыть сундук";
        public void Fill(ref MapRandom random)
        {
            contents.Clear(); opened.Value = false;
            int count = LootCatalog.Count(Catalog.ItemsPerChest, ref random);
            for (int i = 0; i < count; i++)
                if (Catalog.Roll(ref random, out var item)) contents.Add(item);
        }
        public void Open()
        {
            if (IsServerInitialized && IsSpawned) opened.Value = true;
        }
        public void Take(NetworkWeapon player, int slot)
        {
            if (!IsServerInitialized || !IsSpawned || !opened.Value) return;
            var item = ItemAt(slot);
            if (item != InventoryItem.None && player.AddItem(item)) contents[slot] = InventoryItem.None;
        }
        void LateUpdate()
        {
            if (Lid != null) Lid.localRotation = Quaternion.Euler(opened.Value ? 105f : 0f, 0f, 0f);
        }
    }
}
