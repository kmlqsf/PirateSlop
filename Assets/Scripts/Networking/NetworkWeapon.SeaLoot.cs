using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        readonly SyncVar<NetworkObject> workingLoot = new();
        readonly SyncVar<NetworkObject> carriedLoot = new();
        public NetworkLootChest WorkingLoot => workingLoot.Value != null ? workingLoot.Value.GetComponent<NetworkLootChest>() : null;
        public NetworkLootChest CarriedLoot => carriedLoot.Value != null ? carriedLoot.Value.GetComponent<NetworkLootChest>() : null;
        public bool LootHandsBusy => WorkingLoot != null || CarriedLoot != null;
        public bool LootWorkLocked => WorkingLoot != null && WorkingLoot.Kind == SeaLootKind.Raft;

        public void SetLootWork(NetworkLootChest chest)
        {
            if (IsServerInitialized) workingLoot.Value = chest != null ? chest.NetworkObject : null;
        }

        public void SetCarriedLoot(NetworkLootChest chest)
        {
            if (IsServerInitialized) carriedLoot.Value = chest != null ? chest.NetworkObject : null;
        }

        public void CancelLootWork()
        {
            if (IsServerInitialized && WorkingLoot != null) WorkingLoot.StopWork();
        }

        public bool CanHandleLoot(NetworkLootChest chest, bool continuing = false)
        {
            var motor = GetComponent<AdvancedPlayerController>();
            if (!IsServerInitialized || !IsSpawned || chest == null || !chest.IsSpawned || motor.IsDead || motor.IsKnockedBack || motor.IsClimbing || CarriedLoot != null) return false;
            if (!continuing && (motor.LocomotionLocked || WorkingLoot != null)) return false;
            if (GetComponent<CannonHands>().HasHeldBall) return false;
            var fishing = inventory.Fishing;
            if (fishing != null && (fishing.HasFish || fishing.IsFishing || fishing.IsEating)) return false;
            return CanReach(chest.transform.position + Vector3.up * .4f, chest.transform);
        }

        public void StartLootWork(NetworkObject target) => StartLootWorkServerRpc(target);
        [ServerRpc]
        void StartLootWorkServerRpc(NetworkObject target)
        {
            var chest = target != null ? target.GetComponent<NetworkLootChest>() : null;
            if (!CanHandleLoot(chest)) return;
            if (chest.Kind == SeaLootKind.Raft && GetComponent<AdvancedPlayerController>().IsSwimming) chest.BoardRaft(this);
            else chest.StartWork(this);
        }

        public void LootInput(bool active, int key = -1, int round = 0) => LootInputServerRpc(active, key, round);
        [ServerRpc]
        void LootInputServerRpc(bool active, int key, int round)
        {
            var chest = WorkingLoot;
            if (chest == null) return;
            if (!active) { chest.StopWork(); return; }
            chest.WorkInput(this, key, round);
        }

        public void CarryLoot(NetworkObject target) => CarryLootServerRpc(target);
        [ServerRpc]
        void CarryLootServerRpc(NetworkObject target)
        {
            var chest = target != null ? target.GetComponent<NetworkLootChest>() : null;
            if (CanHandleLoot(chest) && chest.Available) chest.Carry(this);
        }

        public void DropLoot() => DropLootServerRpc();
        [ServerRpc]
        void DropLootServerRpc()
        {
            if (CarriedLoot != null) CarriedLoot.Drop();
        }
    }
}
