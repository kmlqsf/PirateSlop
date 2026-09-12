using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon : NetworkBehaviour
    {
        readonly SyncVar<bool> loaded = new(true);
        readonly SyncVar<bool> reloading = new(false);
        readonly SyncVar<int> cannonSlots = new(0);
        readonly SyncVar<int> sabreSlots = new(1 << 2);
        readonly SyncVar<int> selectedSlot = new(0);
        readonly SyncVar<int> pistolSlots = new(1), rodSlots = new(2);
        readonly SyncList<int> fishCounts = new();
        readonly SyncList<int> ballCounts = new();
        readonly SyncList<InventoryItem> ballItems = new();
        readonly SyncVar<int> malletSlots = new(0);
        readonly SyncList<int> plankCounts = new();
        readonly SyncList<int> rumCounts = new();
        readonly SyncList<InventoryItem> equipmentItems = new();
        public NetworkFish[] DropPrefabs;
        PlayerInventory inventory;
        PirateWeapon weapon;
        void Awake() { weapon = GetComponent<PirateWeapon>(); inventory = GetComponent<PlayerInventory>(); }
        public override void OnStartServer()
        {
            base.OnStartServer();
            if (fishCounts.Count == 0) for (int i = 0; i < 6; i++) fishCounts.Add(0);
            if (ballItems.Count == 0) for (int i = 0; i < 6; i++) ballItems.Add(InventoryItem.Cannonball);
            if (ballCounts.Count == 0) for (int i = 0; i < 6; i++) ballCounts.Add(0);
            if (plankCounts.Count == 0) for (int i = 0; i < 6; i++) plankCounts.Add(0);
            if (rumCounts.Count == 0) for (int i = 0; i < 6; i++) rumCounts.Add(0);
            if (equipmentItems.Count == 0) for (int i = 0; i < 6; i++) equipmentItems.Add(InventoryItem.None);
            ApplyInventory();
        }
        void ApplyInventory()
        {
            for (int i = 0; i < 6; i++) inventory.SetEquipment(i, i < equipmentItems.Count ? equipmentItems[i] : InventoryItem.None);
            inventory.SabreSlots = sabreSlots.Value;
            for (int i = 0; i < 6; i++) inventory.SetRumCount(i, i < rumCounts.Count ? rumCounts[i] : 0);
            for (int i = 0; i < 6; i++) inventory.SetRepairItems(malletSlots.Value, i, i < plankCounts.Count ? plankCounts[i] : 0);
            inventory.SetContents(cannonSlots.Value); inventory.PistolSlots = pistolSlots.Value; inventory.RodSlots = rodSlots.Value;
            for (int i = 0; i < 6; i++) inventory.SetFishCount(i, i < fishCounts.Count ? fishCounts[i] : 0);
            for (int i = 0; i < 6; i++) inventory.SetBallCount(i, i < ballCounts.Count ? ballCounts[i] : 0, i < ballItems.Count ? ballItems[i] : InventoryItem.Cannonball);
        }
        public bool CanAddItem(InventoryItem item)
        {
            if (!IsServerInitialized || item < InventoryItem.Fish || item > InventoryItem.BoardingHook) return false;
            if (item == InventoryItem.Mallet || item == InventoryItem.Plank) return false;
            if (item == InventoryItem.Rum)
                for (int i = 0; i < rumCounts.Count; i++) if (rumCounts[i] > 0 && rumCounts[i] < 6) return true;
            if (item == InventoryItem.Fish)
                for (int i = 0; i < fishCounts.Count; i++) if (fishCounts[i] > 0 && fishCounts[i] < 20) return true;
            if (CannonAmmo.IsBall(item))
                for (int i = 0; i < ballCounts.Count; i++) if (ballCounts[i] > 0 && ballCounts[i] < 20 && ballItems[i] == item) return true;
            if (item == InventoryItem.Plank)
                for (int i = 0; i < plankCounts.Count; i++) if (plankCounts[i] > 0 && plankCounts[i] < 20) return true;
            return inventory.EmptySlot() >= 0;
        }
        public bool AddItem(InventoryItem item)
        {
            if (!CanAddItem(item)) return false;
            {
                int slot = inventory.EmptySlot();
                if (item >= InventoryItem.Wine && !CannonAmmo.IsBall(item)) { equipmentItems[slot] = item; GetComponent<NetworkEquipment>()?.ResetSlot(slot, item); }
                else if (item == InventoryItem.Pistol) pistolSlots.Value |= 1 << slot;
                else if (item == InventoryItem.Rum)
                {
                    for (int i = 0; i < rumCounts.Count; i++) if (rumCounts[i] > 0 && rumCounts[i] < 6) { slot = i; break; }
                    rumCounts[slot]++;
                }
                else if (item == InventoryItem.Rod) rodSlots.Value |= 1 << slot;
                else if (item == InventoryItem.Fish)
                {
                    for (int i = 0; i < fishCounts.Count; i++) if (fishCounts[i] > 0 && fishCounts[i] < 20) { slot = i; break; }
                    fishCounts[slot]++;
                }
                else if (CannonAmmo.IsBall(item))
                {
                    for (int i = 0; i < ballCounts.Count; i++) if (ballCounts[i] > 0 && ballCounts[i] < 20 && ballItems[i] == item) { slot = i; break; }
                    ballItems[slot] = item; ballCounts[slot]++;
                }
                else if (item == InventoryItem.Plank)
                {
                    for (int i = 0; i < plankCounts.Count; i++) if (plankCounts[i] > 0 && plankCounts[i] < 20) { slot = i; break; }
                    plankCounts[slot]++;
                }
                else if (item == InventoryItem.Mallet) malletSlots.Value |= 1 << slot;
                else if (item == InventoryItem.Sabre) sabreSlots.Value |= 1 << slot;
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
        public bool AddSupplyBalls(InventoryItem item, int count)
        {
            if (!IsServerInitialized || !CannonAmmo.IsBall(item) || count < 1 || count > 20) return false;
            int slot = inventory.EmptySlot();
            for (int i = 0; i < ballCounts.Count; i++)
                if (ballCounts[i] > 0 && ballCounts[i] <= 20 - count && ballItems[i] == item) { slot = i; break; }
            if (slot < 0) return false;
            ballItems[slot] = item; ballCounts[slot] += count;
            selectedSlot.Value = slot; inventory.SetSelection(slot);
            ApplyInventory(); SelectSupplyTargetRpc(Owner, slot);
            return true;
        }
        [TargetRpc]
        void SelectSupplyTargetRpc(FishNet.Connection.NetworkConnection connection, int slot) => inventory.SetSelection(slot);
        public void DropSelected() => DropSelectedServerRpc();
        [ServerRpc]
        void DropSelectedServerRpc()
        {
            DropSelectedAuthority();
        }
        void DropSelectedAuthority()
        {
            var motor = GetComponent<AdvancedPlayerController>();
            if (LootHandsBusy || motor.IsDead || motor.IsSwimming || motor.IsClimbing || motor.LocomotionLocked || GetComponent<CannonHands>().HasHeldBall || inventory.Fishing.CarryingCatch || inventory.Fishing.IsEating) return;
            int slot = selectedSlot.Value;
            InventoryItem item;
            if (inventory.EquipmentAt(slot) != InventoryItem.None) item = inventory.EquipmentAt(slot);
            else if (inventory.PistolAt(slot)) item = InventoryItem.Pistol;
            else if (inventory.RodAt(slot)) item = InventoryItem.Rod;
            else if (inventory.HasCannon(slot)) item = InventoryItem.Cannon;
            else if (inventory.HasMallet(slot)) item = InventoryItem.Mallet;
            else if (inventory.HasSabre(slot)) item = InventoryItem.Sabre;
            else if (inventory.PlankCount(slot) > 0) item = InventoryItem.Plank;
            else if (inventory.RumCount(slot) > 0) item = InventoryItem.Rum;
            else if (inventory.BallCount(slot) > 0) item = inventory.BallItem(slot);
            else if (slot < fishCounts.Count && fishCounts[slot] > 0) item = InventoryItem.Fish;
            else return;
            int prefabIndex = CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item;
            if (DropPrefabs == null || prefabIndex >= DropPrefabs.Length || DropPrefabs[prefabIndex] == null) return;
            Vector3 origin = transform.position + Vector3.up + transform.forward * .8f;
            RaycastHit floor = default; float distance = 6f;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > .5f && hit.distance < distance) { floor = hit; distance = hit.distance; }
            if (floor.collider == null) return;
            var prefab = DropPrefabs[prefabIndex];
            var orientation = Quaternion.FromToRotation(Vector3.up, floor.normal) * Quaternion.Euler(0, transform.eulerAngles.y, item == InventoryItem.Fish ? 90 : 0);
            var shape = prefab.GetComponent<BoxCollider>();
            float height = CannonAmmo.IsBall(item) ? prefab.GetComponent<SphereCollider>().radius + .02f : item == InventoryItem.Fish ? .12f : shape.size.y * .5f - shape.center.y + .02f;
            Vector3 point = CannonAmmo.IsBall(item) ? origin : floor.point + floor.normal * height;
            var dropped = Instantiate(prefab, point, orientation);
            if (CannonAmmo.IsBall(item)) dropped.SetAmmoItem(item);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dropped.gameObject, gameObject.scene);
            var support = floor.collider.GetComponentInParent<NetworkShip>();
            dropped.Place(support != null ? support.NetworkObject : null, point, orientation);
            ServerManager.Spawn(dropped.NetworkObject);
            if (CannonAmmo.IsBall(item) && support != null) dropped.GetComponent<Cannonball>().RollOnPlatform(support.GetComponent<Rigidbody>());
            if (item >= InventoryItem.Wine && !CannonAmmo.IsBall(item)) equipmentItems[slot] = InventoryItem.None;
            else if (item == InventoryItem.Pistol) pistolSlots.Value &= ~(1 << slot);
            else if (item == InventoryItem.Rod) rodSlots.Value &= ~(1 << slot);
            else if (item == InventoryItem.Cannon) cannonSlots.Value &= ~(1 << slot);
            else if (CannonAmmo.IsBall(item)) ballCounts[slot]--;
            else if (item == InventoryItem.Mallet) malletSlots.Value &= ~(1 << slot);
            else if (item == InventoryItem.Sabre) sabreSlots.Value &= ~(1 << slot);
            else if (item == InventoryItem.Plank) plankCounts[slot]--;
            else if (item == InventoryItem.Rum) rumCounts[slot]--;
            else fishCounts[slot]--;
            ApplyInventory(); DropSoundObserversRpc(point);
        }
        [ObserversRpc(RunLocally = true)] void DropSoundObserversRpc(Vector3 point) => GameAudio.Play(SoundCue.Place, point);
        public void SelectSlot(int slot) => SelectSlotServerRpc(slot);
        public bool ConsumeEquipment(int slot, InventoryItem item)
        {
            if (!IsServerInitialized || slot < 0 || slot >= equipmentItems.Count || equipmentItems[slot] != item) return false;
            equipmentItems[slot] = InventoryItem.None; ApplyInventory(); return true;
        }
        [ServerRpc]
        void SelectSlotServerRpc(int slot)
        {
            if (inventory == null || slot < 0 || slot >= 6) return;
            selectedSlot.Value = slot; inventory.SetSelection(slot);
        }
        public void UseChest(NetworkObject target, int slot = -1) => UseChestServerRpc(target, slot);
        [ServerRpc]
        void UseChestServerRpc(NetworkObject target, int slot)
        {
            var motor = GetComponent<AdvancedPlayerController>();
            var fishing = inventory.Fishing;
            if (target == null || LootHandsBusy || motor.IsDead || motor.IsClimbing || motor.LocomotionLocked || GetComponent<CannonHands>().HasHeldBall) return;
            if (fishing != null && (fishing.CarryingCatch || fishing.IsFishing || fishing.IsEating)) return;
            var chest = target.GetComponent<NetworkLootChest>();
            if (chest == null || !chest.IsSpawned || !chest.Available || Vector3.Distance(transform.position, target.transform.position) > 5f) return;
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 delta = target.transform.position + Vector3.up * .4f - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target.transform)) return;
            if (slot < 0) { chest.Open(); OpenChestTargetRpc(Owner, target); }
            else chest.Take(this, slot);
        }
        [TargetRpc]
        void OpenChestTargetRpc(FishNet.Connection.NetworkConnection connection, NetworkObject target)
        {
            if (target != null) inventory.OpenLoot(target.GetComponent<NetworkLootChest>());
        }
        public void StoreBall(NetworkObject ship) => StoreBallServerRpc(ship);
        [ServerRpc]
        void StoreBallServerRpc(NetworkObject ship)
        {
            if (ship == null || !CanHandleBall()) return;
            var cannon = ship.GetComponent<NetworkCannon>();
            if (cannon != null) cannon.StoreBall(this);
        }
        public void LoadBall(NetworkObject ship, int index) => LoadBallServerRpc(ship, index);
        [ServerRpc]
        void LoadBallServerRpc(NetworkObject ship, int index)
        {
            int slot = selectedSlot.Value;
            if (ship == null || !CanHandleBall() || GetComponent<CannonHands>().HasHeldBall || inventory.BallCount(slot) <= 0) return;
            var cannon = ship.GetComponent<NetworkCannon>();
            if (cannon != null && cannon.LoadInventoryBall(this, index, ballItems[slot]))
            { ballCounts[slot]--; ApplyInventory(); }
        }
        bool CanHandleBall()
        {
            var motor = GetComponent<AdvancedPlayerController>();
            var fishing = inventory.Fishing;
            return !LootHandsBusy && !motor.IsDead && !motor.IsSwimming && !motor.IsClimbing && !motor.LocomotionLocked &&
                (fishing == null || (!fishing.CarryingCatch && !fishing.IsFishing && !fishing.IsEating));
        }
        public bool CanReach(Vector3 point, Transform target)
        {
            if (Vector3.Distance(transform.position, point) > 5f) return false;
            Vector3 origin = transform.position + Vector3.up * 1.5f, delta = point - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target) &&
                    hit.collider.GetComponentInParent<AdvancedPlayerController>() == null && hit.collider.GetComponentInParent<Cannonball>() == null) return false;
            return true;
        }
        public bool CanReachCannon(SimpleCannon cannon)
        {
            if (cannon == null || !cannon.gameObject.activeInHierarchy) return false;
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            foreach (var collider in cannon.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger && CanReach(collider.ClosestPoint(origin), cannon.transform)) return true;
            return CanReach(cannon.Muzzle.position, cannon.transform);
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
        public void Request(byte action, Vector3 direction, Vector3 eyeOffset,bool aimed=false,int seed=0) => ActionServerRpc(action,direction,eyeOffset,aimed,seed);
        [ServerRpc] void ActionServerRpc(byte action, Vector3 direction, Vector3 eyeOffset,bool aimed,int seed)
        {
            weapon.TickAuthority();
            bool accepted = weapon.Act(action, direction, eyeOffset,aimed,seed);
            if (action == 0 && !accepted) RejectShotTargetRpc(Owner);
            loaded.Value = weapon.Loaded; reloading.Value = weapon.Reloading;
        }
        [TargetRpc]
        void RejectShotTargetRpc(FishNet.Connection.NetworkConnection connection) => weapon.RejectPredictedShot();
        void Update()
        {
            UpdateShipComparison();
            UpdateGrapple();
            if (inventory != null && IsClientInitialized && !IsServerInitialized)
            {
                ApplyInventory();
                if (!IsOwner && inventory.SelectedSlot != selectedSlot.Value) inventory.SetSelection(selectedSlot.Value);
            }
            if (IsServerInitialized) { weapon.TickAuthority(); loaded.Value = weapon.Loaded; reloading.Value = weapon.Reloading; }
            else if (IsClientInitialized) weapon.SetState(loaded.Value,reloading.Value);
        }
        public void PublishAttack(byte action,Vector3 end) => AttackObserversRpc(action,end);
        public void PublishShot(FirearmShot shot) => ShotObserversRpc(shot);
        [ObserversRpc] void ShotObserversRpc(FirearmShot shot)
        { if(!IsServerInitialized)weapon.ShowShot(shot); }
        [ObserversRpc] void AttackObserversRpc(byte action,Vector3 end) => weapon.ShowAttack(action,end);
    }
}
