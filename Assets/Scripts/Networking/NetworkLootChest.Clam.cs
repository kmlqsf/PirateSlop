using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> clamSupport = new();
        readonly SyncVar<bool> inClam = new();
        public NetworkClamEvent Clam => clamSupport.Value != null ? clamSupport.Value.GetComponent<NetworkClamEvent>() : null;
        public bool InClam => inClam.Value;
        public void PlaceInClam(NetworkClamEvent clam)
        {
            kind.Value = SeaLootKind.Clam; phase.Value = SeaLootState.Ready;
            clamSupport.Value = clam.NetworkObject; inClam.Value = true;
            eventPoint.Value = clam.transform.position;
        }
        bool UpdateClamChest()
        {
            if (!InClam) return false;
            var clam = Clam;
            if (clam != null && clam.ChestSocket != null) transform.SetPositionAndRotation(clam.ChestSocket.position, clam.ChestSocket.rotation);
            return true;
        }
        string ClamHint => Clam == null ? "Раковина с сундуком" : Clam.ChestAvailable ? "E — открыть · F — нести сундук"
            : $"Нажмите два рычага вместе: {Clam.SuccessfulPairs}/{Clam.RequiredPairs} · окно {Clam.PairWindow:F1} с";
    }
}
