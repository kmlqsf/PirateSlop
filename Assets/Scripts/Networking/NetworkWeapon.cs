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
        readonly SyncVar<bool> hasPistol = new(true), hasRod = new(true);
        readonly SyncList<int> fishCounts = new();
        public NetworkFish[] DropPrefabs;
        PlayerInventory inventory;
        PirateWeapon weapon;
        void Awake() { weapon = GetComponent<PirateWeapon>(); inventory = GetComponent<PlayerInventory>(); }
        public override void OnStartServer()
        {
            base.OnStartServer();
            if (fishCounts.Count == 0) for (int i = 0; i < 6; i++) fishCounts.Add(0);
            ApplyInventory();
        }
        void ApplyInventory()
        {
            inventory.SetContents(cannonSlots.Value); inventory.HasPistol = hasPistol.Value; inventory.HasRod = hasRod.Value;
            for (int i = 0; i < 6; i++) inventory.SetFishCount(i, i < fishCounts.Count ? fishCounts[i] : 0);
        }
        public bool CanAddItem(InventoryItem item)
        {
            if (!IsServerInitialized) return false;
            if (item == InventoryItem.Pistol) return !hasPistol.Value;
            if (item == InventoryItem.Rod) return !hasRod.Value;
            if (item == InventoryItem.Fish)
                for (int i = 2; i < fishCounts.Count; i++) if (fishCounts[i] > 0 && fishCounts[i] < 20) return true;
            return inventory.EmptySlot() >= 0;
        }
        public bool AddItem(InventoryItem item)
        {
            if (!CanAddItem(item)) return false;
            if (item == InventoryItem.Pistol) hasPistol.Value = true;
            else if (item == InventoryItem.Rod) hasRod.Value = true;
            else
            {
                int slot = inventory.EmptySlot();
                if (item == InventoryItem.Fish)
                {
                    for (int i = 2; i < fishCounts.Count; i++) if (fishCounts[i] > 0 && fishCounts[i] < 20) { slot = i; break; }
                    fishCounts[slot]++;
                }
                else cannonSlots.Value |= 1 << slot;
            }
            ApplyInventory(); return true;
        }
        public bool EatSelectedFish(float healing)
        {
            if (!IsServerInitialized) return false;
            int slot = selectedSlot.Value;
            var health = GetComponent<CombatHealth>();
            if (slot >= fishCounts.Count || fishCounts[slot] <= 0 || health.IsDead) return false;
            fishCounts[slot]--; ApplyInventory(); health.Heal(healing); return true;
        }
        public void DropSelected() => DropSelectedServerRpc();
        [ServerRpc]
        void DropSelectedServerRpc()
        {
            var motor = GetComponent<AdvancedPlayerController>();
            if (motor.IsDead || motor.IsSwimming || motor.IsClimbing || motor.LocomotionLocked || GetComponent<CannonHands>().HasHeldBall || inventory.Fishing.CarryingCatch || inventory.Fishing.IsEating) return;
            int slot = selectedSlot.Value;
            InventoryItem item;
            if (slot == 0 && hasPistol.Value) item = InventoryItem.Pistol;
            else if (slot == 1 && hasRod.Value) item = InventoryItem.Rod;
            else if (inventory.HasCannon(slot)) item = InventoryItem.Cannon;
            else if (slot < fishCounts.Count && fishCounts[slot] > 0) item = InventoryItem.Fish;
            else return;
            if (DropPrefabs == null || (int)item >= DropPrefabs.Length || DropPrefabs[(int)item] == null) return;
            Vector3 origin = transform.position + Vector3.up + transform.forward * .8f;
            RaycastHit floor = default; float distance = 6f;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > .5f && hit.distance < distance) { floor = hit; distance = hit.distance; }
            if (floor.collider == null) return;
            var prefab = DropPrefabs[(int)item];
            var orientation = Quaternion.FromToRotation(Vector3.up, floor.normal) * Quaternion.Euler(0, transform.eulerAngles.y, item == InventoryItem.Fish ? 90 : 0);
            var shape = prefab.GetComponent<BoxCollider>();
            float height = item == InventoryItem.Fish ? .12f : shape.size.y * .5f - shape.center.y + .02f;
            Vector3 point = floor.point + floor.normal * height;
            var dropped = Instantiate(prefab, point, orientation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dropped.gameObject, gameObject.scene);
            var support = floor.collider.GetComponentInParent<NetworkShip>();
            dropped.Place(support != null ? support.NetworkObject : null, point, orientation);
            ServerManager.Spawn(dropped.NetworkObject);
            if (item == InventoryItem.Pistol) hasPistol.Value = false;
            else if (item == InventoryItem.Rod) hasRod.Value = false;
            else if (item == InventoryItem.Cannon) cannonSlots.Value &= ~(1 << slot);
            else fishCounts[slot]--;
            ApplyInventory(); DropSoundObserversRpc(point);
        }
        [ObserversRpc(RunLocally = true)] void DropSoundObserversRpc(Vector3 point) => GameAudio.Play(SoundCue.Place, point);
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
        public void PlaceCannon(NetworkObject ship, int slot, Vector3 localPosition, Quaternion rotation) => PlaceCannonServerRpc(ship, slot, localPosition, rotation);
        [ServerRpc]
        void PlaceCannonServerRpc(NetworkObject ship, int slot, Vector3 localPosition, Quaternion rotation)
        {
            if (inventory == null || ship == null || selectedSlot.Value != slot || !inventory.HasCannon(slot)) return;
            var cannon = ship.GetComponent<NetworkCannon>();
            if (cannon == null || !PlayerInventory.CanPlace(cannon.Crate, localPosition, rotation, GetComponent<AdvancedPlayerController>())) return;
            cannon.Place(localPosition, rotation);
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
                ApplyInventory();
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
