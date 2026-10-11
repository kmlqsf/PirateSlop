using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> balanceSupport = new();
        public NetworkBalanceEvent Balance => balanceSupport.Value != null ? balanceSupport.Value.GetComponent<NetworkBalanceEvent>() : null;
        public bool OnBalance => balanceSupport.Value != null;
        public void PlaceOnBalance(NetworkBalanceEvent owner)
        {
            kind.Value = SeaLootKind.Balance; phase.Value = SeaLootState.Ready;
            balanceSupport.Value = owner.NetworkObject; eventPoint.Value = owner.transform.position;
        }
        bool UpdateBalanceChest()
        {
            if (!OnBalance) return false;
            var owner = Balance;
            if (owner != null) transform.SetPositionAndRotation(owner.ChestSocket.position,owner.ChestSocket.rotation);
            return true;
        }
        string BalanceHint => Balance != null && Balance.ChestAvailable ? "E — открыть · F — нести сундук" : $"Весы: {Balance?.Deposited ?? 0}/5 камней";
    }
}
