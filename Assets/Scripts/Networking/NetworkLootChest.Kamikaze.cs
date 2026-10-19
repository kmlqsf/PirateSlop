using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> kamikazeSupport = new();
        public NetworkKamikazeEvent KamikazeEvent => kamikazeSupport.Value != null ? kamikazeSupport.Value.GetComponent<NetworkKamikazeEvent>() : null;
        public bool InKamikazeEvent => kamikazeSupport.Value != null;

        public void PlaceInKamikazeEvent(NetworkKamikazeEvent owner)
        {
            kind.Value = SeaLootKind.Kamikaze;
            phase.Value = SeaLootState.Ready;
            kamikazeSupport.Value = owner.NetworkObject;
            eventPoint.Value = owner.transform.position;
            anchor.Value = owner.ChestSocket.position;
            facing.Value = owner.ChestSocket.rotation;
        }

        bool UpdateKamikazeChest()
        {
            if (!InKamikazeEvent) return false;
            var owner = KamikazeEvent;
            if (owner == null || owner.ChestSocket == null) return false;
            transform.SetPositionAndRotation(owner.ChestSocket.position, owner.ChestSocket.rotation);
            return true;
        }
    }
}
