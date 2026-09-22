using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void DepositRum(NetworkObject ship) => DepositRumServerRpc(ship);
        [ServerRpc]
        void DepositRumServerRpc(NetworkObject target) => TryDepositRum(target);
        public bool TryDepositRum(NetworkObject target)
        {
            if (!IsServerInitialized || target == null || !CanHandleBall() || GetComponent<CannonHands>().HasHeldBall) return false;
            var ship = target.GetComponent<NetworkShip>();
            if (ship == null) return false;
            var shelf = ship.GetComponentInChildren<RumShelf>();
            if (shelf == null) return false;
            bool reachable = false;
            Vector3 eye = transform.position + Vector3.up * 1.4f;
            foreach (var collider in shelf.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger && CanReach(collider.ClosestPoint(eye), shelf.transform)) { reachable = true; break; }
            if (!reachable) return false;
            int slot = selectedSlot.Value;
            if (slot < 0 || slot >= rumCounts.Count || rumCounts[slot] <= 0) return false;
            int added = ship.StoreRum(rumCounts[slot]);
            rumCounts[slot] -= added;
            ApplyInventory();
            if (added > 0) DropSoundObserversRpc(shelf.transform.position);
            return added > 0;
        }
    }
}
