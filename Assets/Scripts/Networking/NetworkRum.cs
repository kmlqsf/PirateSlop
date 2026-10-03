using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void DepositRum(NetworkObject ship) => DepositRumServerRpc(ship);
        public void DepositRum(NetworkObject ship, int slot, Vector3 point) => DepositAimedRumServerRpc(ship, slot, point);
        [ServerRpc]
        void DepositRumServerRpc(NetworkObject target) => TryDepositRum(target);
        [ServerRpc]
        void DepositAimedRumServerRpc(NetworkObject target, int slot, Vector3 point)
        {
            if (float.IsFinite(point.sqrMagnitude)) TryDepositRum(target, slot, point);
        }
        public bool TryDepositRum(NetworkObject target, int requestedSlot = -1, Vector3? aimedPoint = null)
        {
            if (!IsServerInitialized || target == null || !CanHandleBall() || GetComponent<CannonHands>().HasHeldBall) return false;
            var ship = target.GetComponent<NetworkShip>();
            if (ship == null) return false;
            var shelf = ship.GetComponentInChildren<RumShelf>();
            if (shelf == null) return false;
            bool reachable = false;
            Vector3 eye = transform.position + Vector3.up * 1.4f;
            foreach (var collider in shelf.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Vector3 point = aimedPoint ?? collider.ClosestPoint(eye);
                if (aimedPoint.HasValue && Vector3.Distance(collider.ClosestPoint(point), point) > .08f) continue;
                if (CanReach(point, shelf.transform)) { reachable = true; break; }
            }
            if (!reachable) return false;
            int slot = requestedSlot >= 0 ? requestedSlot : selectedSlot.Value;
            if (slot < 0 || slot >= rumCounts.Count || rumCounts[slot] <= 0) return false;
            int added = ship.StoreRum(rumCounts[slot]);
            rumCounts[slot] -= added;
            ApplyInventory();
            if (added > 0) DropSoundObserversRpc(shelf.transform.position);
            return added > 0;
        }
    }
}
