using FishNet.Object;
using FishNet.Connection;
namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public WhaleHarpoonPin WhalePin => WhaleLootPoint.WorkingPin(NetworkObject);
        public void WhaleInput(NetworkObject target, int index, bool held, float rock, float pull, uint attempt, bool begin = false) => WhaleInputServerRpc(target, index, held, rock, pull, attempt, begin);
        [ServerRpc]
        void WhaleInputServerRpc(NetworkObject target, int index, bool held, float rock, float pull, uint attempt, bool begin)
        {
            if (!float.IsFinite(rock) || !float.IsFinite(pull)) return;
            var whale = target != null ? target.GetComponent<WhaleLootPoint>() : null;
            string reason = "Гарпун больше недоступен";
            bool accepted = whale != null && whale.InputHarpoon(this, index, held, rock, pull, out reason);
            if (held && (begin || !accepted) && Owner != null && Owner.IsActive) WhaleGripResultTargetRpc(Owner, target, index, attempt, accepted, reason);
        }
        [TargetRpc]
        void WhaleGripResultTargetRpc(NetworkConnection connection, NetworkObject target, int index, uint attempt, bool accepted, string reason) => GetComponent<WhalePlayerInteraction>()?.GripResult(target, index, attempt, accepted, reason);
    }
}

