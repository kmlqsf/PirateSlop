using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkWeapon : NetworkBehaviour
    {
        readonly SyncVar<bool> loaded = new(true);
        readonly SyncVar<bool> reloading = new(false);
        readonly SyncVar<int> cannonSlots = new(0);
        readonly SyncVar<int> selectedSlot = new(0);
        PlayerInventory inventory;
        PirateWeapon weapon;
        void Awake() { weapon = GetComponent<PirateWeapon>(); inventory = GetComponent<PlayerInventory>(); }
        public void SelectSlot(int slot) => SelectSlotServerRpc(slot);
        [ServerRpc]
        void SelectSlotServerRpc(int slot)
        {
            if (inventory == null || slot < 0 || slot >= 6) return;
            selectedSlot.Value = slot; inventory.SetSelection(slot);
        }
        public void TakeCannon(NetworkObject ship) => TakeCannonServerRpc(ship);
        [ServerRpc]
        void TakeCannonServerRpc(NetworkObject ship)
        {
            if (inventory == null || ship == null) return;
            var motor = GetComponent<AdvancedPlayerController>();
            var cannon = ship.GetComponent<NetworkCannon>();
            int slot = inventory.EmptySlot();
            if (motor.IsDead || motor.LocomotionLocked || slot < 0 || cannon == null || cannon.Crate == null || Vector3.Distance(transform.position, cannon.Crate.Kit.transform.position) > 6f) return;
            if (!cannon.TakeKit()) return;
            cannonSlots.Value |= 1 << slot;
            inventory.SetContents(cannonSlots.Value);
        }
        public void PlaceCannon(NetworkObject ship, int slot, Vector3 localPosition, float yaw) => PlaceCannonServerRpc(ship, slot, localPosition, yaw);
        [ServerRpc]
        void PlaceCannonServerRpc(NetworkObject ship, int slot, Vector3 localPosition, float yaw)
        {
            if (inventory == null || ship == null || selectedSlot.Value != slot || !inventory.HasCannon(slot)) return;
            var cannon = ship.GetComponent<NetworkCannon>();
            if (cannon == null || !PlayerInventory.CanPlace(cannon.Crate, localPosition, yaw, GetComponent<AdvancedPlayerController>())) return;
            cannon.Place(localPosition, yaw);
            cannonSlots.Value &= ~(1 << slot);
            inventory.SetContents(cannonSlots.Value);
        }
        public void Request(byte action, Vector3 direction, Vector3 eyeOffset) => ActionServerRpc(action,direction,eyeOffset);
        [ServerRpc] void ActionServerRpc(byte action, Vector3 direction, Vector3 eyeOffset)
        { weapon.TickAuthority(); weapon.Act(action,direction,eyeOffset); loaded.Value = weapon.Loaded; reloading.Value = weapon.Reloading; }
        void Update()
        {
            if (inventory != null && IsClientInitialized && !IsServerInitialized)
            {
                inventory.SetContents(cannonSlots.Value);
                if (!IsOwner && inventory.SelectedSlot != selectedSlot.Value) inventory.SetSelection(selectedSlot.Value);
            }
            if (IsServerInitialized) { weapon.TickAuthority(); loaded.Value = weapon.Loaded; reloading.Value = weapon.Reloading; }
            else if (IsClientInitialized) weapon.SetState(loaded.Value,reloading.Value);
        }
        public void PublishAttack(byte action,Vector3 end) => AttackObserversRpc(action,end);
        public void PublishShot(Vector3 origin,Vector3 velocity) => ShotObserversRpc(origin,velocity);
        [ObserversRpc] void ShotObserversRpc(Vector3 origin,Vector3 velocity)
        { if(!IsServerInitialized)weapon.SpawnBullet(origin,velocity,false); }
        [ObserversRpc] void AttackObserversRpc(byte action,Vector3 end) => weapon.ShowAttack(action,end);
    }
}
