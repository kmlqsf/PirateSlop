using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> riggingSupport = new();
        public NetworkRiggingEvent Rigging => riggingSupport.Value != null ? riggingSupport.Value.GetComponent<NetworkRiggingEvent>() : null;
        public bool InRigging => riggingSupport.Value != null;
        public void PlaceInRigging(NetworkRiggingEvent owner)
        {
            kind.Value = SeaLootKind.Rigging; phase.Value = SeaLootState.Ready;
            riggingSupport.Value = owner.NetworkObject; eventPoint.Value = owner.transform.position;
        }
        bool UpdateRiggingChest()
        {
            if (!InRigging) return false;
            var owner = Rigging;
            if (owner != null && owner.ChestSocket != null) transform.SetPositionAndRotation(owner.ChestSocket.position, owner.ChestSocket.rotation);
            return true;
        }
        string RiggingHint => Rigging == null ? "Сундук в такелаже" : Rigging.ChestAvailable ? "E — открыть · F — нести сундук" : "Сундук висит на канате · спустите его тормозом на конце реи";
    }
}
