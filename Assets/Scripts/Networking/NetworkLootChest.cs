using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest : NetworkBehaviour
    {
        public static readonly System.Collections.Generic.List<NetworkLootChest> ServerChests = new();
        public static readonly System.Collections.Generic.List<NetworkLootChest> ClientChests = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetClientChests() => ClientChests.Clear();
        public override void OnStartClient() { base.OnStartClient(); WaterImpactBody.Ensure(gameObject); if (!ClientChests.Contains(this)) ClientChests.Add(this); }
        public override void OnStartServer() { base.OnStartServer(); WaterImpactBody.Ensure(gameObject); ServerChests.Add(this); }
        public LootCatalog Catalog;
        public Transform Lid;
        readonly SyncVar<bool> opened = new();
        public bool Opened => opened.Value;
        readonly SyncList<ChestLootStack> contents = new();
        public int SlotCount => contents.Count;
        public InventoryItem ItemAt(int slot) => slot >= 0 && slot < contents.Count && contents[slot].Count > 0 ? contents[slot].Item : InventoryItem.None;
        public int CountAt(int slot) => slot >= 0 && slot < contents.Count ? contents[slot].Count : 0;
        public string Hint(PlayerInventory inventory) => Kind == SeaLootKind.Raft && !Available && inventory.GetComponent<AdvancedPlayerController>().IsSwimming ? "E — забраться на плот" : OceanHint;
        public void Fill(ref MapRandom random)
        {
            var rolled = ChestLootTable.RollContents(ref random);
            contents.Clear(); opened.Value = false;
            foreach (var stack in rolled) contents.Add(stack);
        }
        public void Open()
        {
            if (!IsServerInitialized || !IsSpawned || !Available) return;
            if (contents.Count == 0) { ServerManager.Despawn(NetworkObject); return; }
            opened.Value = true;
        }
        public void Take(NetworkWeapon player, int slot, bool swap = false, int selected = -1, InventoryItem expected = InventoryItem.None, int expectedCount = 0)
        {
            if (!IsServerInitialized || !IsSpawned || !opened.Value || !Available || player == null || !player.CanHandleLoot(this)) return;
            var item = ItemAt(slot);
            if (item != InventoryItem.None && swap && !player.CanAddItem(item) && !player.PrepareSwap(item, selected, expected, expectedCount)) return;
            if (item == InventoryItem.None) return;
            var stack = contents[slot];
            int remaining = stack.Count;
            while (remaining > 0 && player.AddItem(item)) remaining--;
            if (remaining == stack.Count) return;
            stack.Count = remaining;
            if (remaining == 0) stack.Item = InventoryItem.None;
            contents[slot] = stack;
            for (int i = 0; i < contents.Count; i++) if (contents[i].Count > 0) return;
            ServerManager.Despawn(NetworkObject);
        }
        void LateUpdate()
        {
            UpdateOceanLoot();
            if (Lid != null) Lid.localRotation = Quaternion.Euler(opened.Value ? 105f : 0f, 0f, 0f);
        }
    }
}
