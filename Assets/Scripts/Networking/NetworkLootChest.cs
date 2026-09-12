using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest : NetworkBehaviour
    {
        public LootCatalog Catalog;
        public Transform Lid;
        readonly SyncVar<bool> opened = new();
        readonly SyncList<InventoryItem> contents = new();
        public int SlotCount => contents.Count;
        public InventoryItem ItemAt(int slot) => slot >= 0 && slot < contents.Count ? contents[slot] : InventoryItem.None;
        public string Hint(PlayerInventory inventory) => Kind == SeaLootKind.Raft && !Available && inventory.GetComponent<AdvancedPlayerController>().IsSwimming ? "E — забраться на плот" : OceanHint;
        public void Fill(ref MapRandom random)
        {
            contents.Clear(); opened.Value = false;
            int count = LootCatalog.Count(Catalog.ItemsPerChest, ref random);
            for (int i = 0; i < count; i++)
                if (Catalog.Roll(ref random, out var item)) contents.Add(item);
        }
        public void Open()
        {
            if (IsServerInitialized && IsSpawned && Available) opened.Value = true;
        }
        public void Take(NetworkWeapon player, int slot)
        {
            if (!IsServerInitialized || !IsSpawned || !opened.Value || !Available || player == null || !player.CanHandleLoot(this)) return;
            var item = ItemAt(slot);
            if (item != InventoryItem.None && player.AddItem(item)) contents[slot] = InventoryItem.None;
            for (int i = 0; i < contents.Count; i++) if (contents[i] != InventoryItem.None) return;
            ServerManager.Despawn(NetworkObject);
        }
        void LateUpdate()
        {
            UpdateOceanLoot();
            if (Lid != null) Lid.localRotation = Quaternion.Euler(opened.Value ? 105f : 0f, 0f, 0f);
        }
    }
}
