using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> fortSupport = new();
        public NetworkCannonBreachEvent Fort => fortSupport.Value != null ? fortSupport.Value.GetComponent<NetworkCannonBreachEvent>() : null;
        public bool InFort => fortSupport.Value != null;
        public void PlaceInFort(NetworkCannonBreachEvent owner)
        {
            kind.Value = SeaLootKind.CannonBreach; phase.Value = SeaLootState.Ready;
            fortSupport.Value = owner.NetworkObject; eventPoint.Value = owner.transform.position;
        }
        bool UpdateFortChest()
        {
            if (!InFort) return false;
            var owner = Fort;
            if (owner != null && owner.ChestSocket != null) transform.SetPositionAndRotation(owner.ChestSocket.position, owner.ChestSocket.rotation);
            return true;
        }
        string FortHint => Fort != null && Fort.ChestAvailable ? "E — открыть · F — нести сундук" : $"Пушечный пролом · разбейте любой вход: {Fort?.HitsToBreach ?? 6} попаданий из пушки";
    }
}
