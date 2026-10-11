using FishNet.Object;
using FishNet.Object.Synchronizing;
namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> whaleSupport = new();
        readonly SyncVar<bool> whaleAttached = new();
        public WhaleLootPoint Whale => whaleSupport.Value != null ? whaleSupport.Value.GetComponent<WhaleLootPoint>() : null;
        public bool OnWhale => whaleAttached.Value;
        public void PlaceOnWhale(WhaleLootPoint whale) { whaleSupport.Value = whale.NetworkObject; whaleAttached.Value = true; }
        bool UpdateWhaleChest()
        {
            if (!OnWhale) return false;
            var whale = Whale;
            if (whale != null && whale.ChestSocket != null) transform.SetPositionAndRotation(whale.ChestSocket.position, whale.ChestSocket.rotation);
            return true;
        }
        string WhaleHint => Whale == null ? "Раненый кит" : Whale.ChestAvailable ? "E — открыть сундук" : Whale.State == WhalePOIState.Diving ? "Кит погружается!" : $"Сними по одному гарпуну из каждой связки: {Whale.FreedPairs}/3";
    }
}

