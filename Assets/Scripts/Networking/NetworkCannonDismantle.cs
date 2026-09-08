using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        NetworkCannon dismantleShip;
        int dismantleIndex = -1;
        float dismantleStarted, dismantleLast;
        public void HoldDismantle(NetworkObject ship, int index, bool holding) => DismantleServerRpc(ship, index, holding);
        [ServerRpc]
        void DismantleServerRpc(NetworkObject ship, int index, bool holding)
        {
            var cannonNetwork = ship != null ? ship.GetComponent<NetworkCannon>() : null;
            var cannon = cannonNetwork != null && cannonNetwork.Crate != null && index >= 0 && index < cannonNetwork.Crate.Cannons.Count
                ? cannonNetwork.Crate.Cannons[index] : null;
            bool valid = holding && cannon != null && cannon.gameObject.activeSelf && !cannon.IsIgnited && !cannon.IsLoading &&
                CanHandleBall() && !GetComponent<CannonHands>().HasHeldBall && CanReachCannon(cannon);
            if (!valid || !CanAddItem(InventoryItem.Cannon))
            {
                dismantleShip = null; dismantleIndex = -1;
                DismantleProgressTargetRpc(Owner, 0f, valid ? "Нет свободного места в инвентаре" : null);
                return;
            }
            if (dismantleShip != cannonNetwork || dismantleIndex != index || Time.time - dismantleLast > .5f)
            { dismantleShip = cannonNetwork; dismantleIndex = index; dismantleStarted = Time.time; }
            dismantleLast = Time.time;
            float progress = Mathf.Clamp01((Time.time - dismantleStarted) / 7f);
            if (progress >= 1f)
            {
                bool removed = cannonNetwork.RemoveCannon(this, index);
                dismantleShip = null; dismantleIndex = -1;
                DismantleProgressTargetRpc(Owner, removed ? 1f : 0f, removed ? "Пушка в инвентаре" : "Снятие отменено");
            }
            else DismantleProgressTargetRpc(Owner, progress, null);
        }
        [TargetRpc]
        void DismantleProgressTargetRpc(FishNet.Connection.NetworkConnection connection, float progress, string message)
            => GetComponent<CannonDismantle>()?.Report(progress, message);
    }
}
