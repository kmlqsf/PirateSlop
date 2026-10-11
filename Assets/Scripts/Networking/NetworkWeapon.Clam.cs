using FishNet.Object;
using FishNet.Connection;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void PressClamLever(NetworkObject target, int side) => PressClamLeverServerRpc(target, side);
        [ServerRpc]
        void PressClamLeverServerRpc(NetworkObject target, int side)
        {
            var clam = target != null ? target.GetComponent<NetworkClamEvent>() : null;
            if (clam == null) return;
            string reason = clam.BlockReason(this, side);
            if (GetComponent<AdvancedPlayerController>().IsSwimming) clam.Press(this, side);
            else if (reason == null) clam.Press(this, side);
            else if (Owner != null && Owner.IsActive) ClamMessageTargetRpc(Owner, reason);
        }
        public void ClamInput(NetworkObject target, int side, bool held, float delta, uint attempt, bool begin = false) => ClamInputServerRpc(target, side, held, delta, attempt, begin);
        [ServerRpc]
        void ClamInputServerRpc(NetworkObject target, int side, bool held, float delta, uint attempt, bool begin)
        {
            if (!float.IsFinite(delta)) return;
            var clam = target != null ? target.GetComponent<NetworkClamEvent>() : null;
            string reason = "Механизм больше недоступен";
            bool accepted = clam != null && clam.InputLever(this, side, held, delta, begin, out reason);
            if (held && (begin || !accepted) && Owner != null && Owner.IsActive) ClamGripResultTargetRpc(Owner, target, side, attempt, accepted, reason);
        }
        [TargetRpc]
        void ClamGripResultTargetRpc(NetworkConnection connection, NetworkObject target, int side, uint attempt, bool accepted, string reason) => GetComponent<ClamPlayerInteraction>()?.GripResult(target, side, attempt, accepted, reason);
        [TargetRpc]
        void ClamMessageTargetRpc(NetworkConnection connection, string reason) => GetComponent<ClamPlayerInteraction>()?.Notify(reason);
    }
}
