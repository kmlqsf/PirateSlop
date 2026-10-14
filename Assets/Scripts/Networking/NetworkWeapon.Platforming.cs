using FishNet.Object;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void PullPlatformingLever(NetworkObject target) => PullPlatformingLeverServerRpc(target);
        [ServerRpc]
        void PullPlatformingLeverServerRpc(NetworkObject target)
        {
            if (target != null) target.GetComponent<NetworkPlatformingEvent>()?.PullLever(this);
        }
    }
}
