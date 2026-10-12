using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void SternCargoInput(NetworkObject target, bool held, Vector2 motion, Vector3 grabLocal, byte stage, uint attempt, bool begin = false) => SternCargoInputServerRpc(target, held, motion, grabLocal, stage, attempt, begin);
        [ServerRpc]
        void SternCargoInputServerRpc(NetworkObject target, bool held, Vector2 motion, Vector3 grabLocal, byte stage, uint attempt, bool begin)
        {
            var sternCargo = target != null ? target.GetComponent<NetworkTowedCargo>() : null;
            string reason = "Кормовой канат недоступен";
            bool accepted = sternCargo != null && sternCargo.InputRope(this, held, motion, grabLocal, stage, attempt, begin, out reason);
            if (held && (begin || !accepted) && Owner != null && Owner.IsActive) SternCargoResultTargetRpc(Owner, target, attempt, accepted, reason);
        }
        [TargetRpc]
        void SternCargoResultTargetRpc(NetworkConnection connection, NetworkObject target, uint attempt, bool accepted, string reason)
            => GetComponent<SternCargoPlayerInteraction>()?.GripResult(target, attempt, accepted, reason);
    }
}
