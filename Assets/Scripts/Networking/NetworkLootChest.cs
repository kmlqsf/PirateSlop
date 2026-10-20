using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(10)]
    public sealed partial class NetworkLootChest : NetworkBehaviour
    {
        public static readonly System.Collections.Generic.List<NetworkLootChest> ServerChests = new();
        public static readonly System.Collections.Generic.List<NetworkLootChest> ClientChests = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetClientChests() => ClientChests.Clear();
        public override void OnStartClient()
        {
            base.OnStartClient();
            WaterImpactBody.Ensure(gameObject);
            if (!ClientChests.Contains(this)) ClientChests.Add(this);
            lidProgress = opened.Value ? 1f : 0f;
            lidOpened = opened.Value;
            ApplyLidRotation();
        }
        public override void OnStartServer() { base.OnStartServer(); WaterImpactBody.Ensure(gameObject); ServerChests.Add(this); }
        public LootCatalog Catalog;
        public Transform Lid;
        [Min(.05f)] public float LidOpenSeconds = 1.1f;
        public float LidOpenAngle = 105f;
        Quaternion lidClosedRotation;
        float lidProgress;
        float appliedLidAngle = float.NaN;
        bool lidOpened;
        AudioSource lidSound;
        void Awake() { if (Lid != null) lidClosedRotation = Lid.localRotation; }
        readonly SyncVar<bool> opened = new();
        readonly SyncVar<bool> lootHintClaimed = new();
        bool upgradeRewardEligible, upgradeRewardClaimed;
        public bool Opened => opened.Value;
        readonly SyncList<ChestLootStack> contents = new();
        public int SlotCount => contents.Count;
        public InventoryItem ItemAt(int slot) => slot >= 0 && slot < contents.Count && contents[slot].Count > 0 ? contents[slot].Item : InventoryItem.None;
        public int CountAt(int slot) => slot >= 0 && slot < contents.Count ? contents[slot].Count : 0;
        public string Hint(PlayerInventory inventory) => InSeagullWreck ? SeagullChestHint(inventory) : Kind == SeaLootKind.Raft && !Available && inventory.GetComponent<AdvancedPlayerController>().IsSwimming ? "E — забраться на плот" : OceanHint;
        public void Fill(ref MapRandom random)
        {
            var rolled = ChestLootTable.RollContents(ref random, MainOceanEvents.RewardItemTypes(transform.position));
            contents.Clear(); opened.Value = false; lootHintClaimed.Value = false;
            foreach (var stack in rolled) contents.Add(stack);
            upgradeRewardEligible = true;
        }
        public void Open(NetworkWeapon opener = null)
        {
            if (!IsServerInitialized || !IsSpawned || !Available) return;
            if (contents.Count == 0) { if (!PreserveCapturedBuoys()) ServerManager.Despawn(NetworkObject); return; }
            opened.Value = true;
            lootHintClaimed.Value = true;
            if (!upgradeRewardEligible || upgradeRewardClaimed || opener == null) return;
            var player = opener.GetComponent<NetworkPlayer>();
            if (player == null || player.TeamId.Value <= 0) return;
            bool hasLoot = false;
            for (int i = 0; i < contents.Count; i++) if (contents[i].Count > 0) { hasLoot = true; break; }
            if (!hasLoot) return;
            upgradeRewardClaimed = true;
            if (player.HasUpgrade(UpgradeEffect.FishSupply)) contents.Add(new ChestLootStack { Item = InventoryItem.Fish, Count = RoguelikeTuning.Current.fishSupplyCount });
            SessionController.Instance?.AwardChestUpgrade(player);
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
            if (!PreserveCapturedBuoys()) ServerManager.Despawn(NetworkObject);
        }
        void LateUpdate()
        {
            UpdateOceanLoot();
            if (Lid == null) return;
            if (opened.Value != lidOpened)
            {
                lidOpened = opened.Value;
                if (lidOpened && IsClientInitialized) GameAudio.Attached(ref lidSound, SoundCue.ChestLidCreak, Lid);
            }
            float next = Mathf.MoveTowards(lidProgress, lidOpened ? 1f : 0f, Time.deltaTime / Mathf.Max(.05f, LidOpenSeconds));
            if (next != lidProgress || appliedLidAngle != LidOpenAngle) { lidProgress = next; ApplyLidRotation(); }
        }
        void ApplyLidRotation()
        {
            if (Lid != null)
            {
                Lid.localRotation = lidClosedRotation * Quaternion.Euler(LidOpenAngle * Mathf.SmoothStep(0f, 1f, lidProgress), 0f, 0f);
                appliedLidAngle = LidOpenAngle;
            }
        }
    }
}
