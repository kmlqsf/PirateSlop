using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void DepositRum(NetworkObject ship) => DepositRumServerRpc(ship);
        [ServerRpc]
        void DepositRumServerRpc(NetworkObject target)
        {
            if (target == null || !CanHandleBall() || GetComponent<CannonHands>().HasHeldBall) return;
            var ship = target.GetComponent<NetworkShip>();
            if (ship == null) return;
            var shelf = ship.GetComponentInChildren<RumShelf>();
            if (shelf == null) return;
            bool reachable = false;
            Vector3 eye = transform.position + Vector3.up * 1.4f;
            foreach (var collider in shelf.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger && CanReach(collider.ClosestPoint(eye), shelf.transform)) { reachable = true; break; }
            if (!reachable) return;
            int slot = selectedSlot.Value;
            if (slot < 0 || slot >= rumCounts.Count || rumCounts[slot] <= 0) return;
            int added = ship.StoreRum(rumCounts[slot]);
            rumCounts[slot] -= added;
            ApplyInventory();
            if (added > 0) DropSoundObserversRpc(shelf.transform.position);
        }
    }
}
