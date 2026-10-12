using FishNet.Object;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void PickupSeagullKey(NetworkObject target) => PickupSeagullKeyServerRpc(target);
        [ServerRpc]
        void PickupSeagullKeyServerRpc(NetworkObject target) => target?.GetComponent<NetworkSeagullEvent>()?.PickupKey(this);
    }
}
