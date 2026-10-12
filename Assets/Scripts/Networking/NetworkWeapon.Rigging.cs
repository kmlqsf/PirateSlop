using FishNet.Object;
using FishNet.Connection;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void RiggingInput(NetworkObject target, bool held, uint attempt, bool begin = false) => RiggingInputServerRpc(target, held, attempt, begin);
        [ServerRpc]
        void RiggingInputServerRpc(NetworkObject target, bool held, uint attempt, bool begin)
        {
            var rigging = target != null ? target.GetComponent<NetworkRiggingEvent>() : null;
            string reason = "Тормоз недоступен";
            bool accepted = rigging != null && rigging.InputBrake(this, held, out reason);
            if (held && (begin || !accepted) && Owner != null && Owner.IsActive) RiggingResultTargetRpc(Owner, target, attempt, accepted, reason);
        }
        [TargetRpc]
        void RiggingResultTargetRpc(NetworkConnection connection, NetworkObject target, uint attempt, bool accepted, string reason)
            => GetComponent<RiggingPlayerInteraction>()?.GripResult(target, attempt, accepted, reason);
    }
}
