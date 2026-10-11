using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        readonly SyncVar<NetworkObject> balanceEvent = new();
        readonly SyncVar<int> balanceStoneIndex = new(-1);
        public NetworkBalanceEvent BalanceEvent => balanceEvent.Value != null ? balanceEvent.Value.GetComponent<NetworkBalanceEvent>() : null;
        public int BalanceStoneIndex => balanceStoneIndex.Value;
        public bool CarryingBalanceStone => BalanceEvent != null && BalanceStoneIndex >= 0;
        public void SetBalanceStone(NetworkBalanceEvent target, int index)
        {
            if (!IsServerInitialized) return;
            balanceStoneIndex.Value = index; balanceEvent.Value = target != null ? target.NetworkObject : null;
        }
        public void TakeBalanceStone(NetworkObject target, int index) => TakeBalanceStoneServerRpc(target,index);
        public void DepositBalanceStone() => DepositBalanceStoneServerRpc();
        public void DropBalanceStone() => DropBalanceStoneServerRpc();
        [ServerRpc] void TakeBalanceStoneServerRpc(NetworkObject target,int index) => target?.GetComponent<NetworkBalanceEvent>()?.Take(this,index);
        [ServerRpc]
        void DepositBalanceStoneServerRpc()
        {
            var balance = BalanceEvent;
            string reason = balance != null ? balance.DepositBlockReason(this) : "Сначала подберите камень";
            if (reason == null) balance.Deposit(this);
            else NotifyBalanceStone(reason);
        }
        [ServerRpc] void DropBalanceStoneServerRpc() => BalanceEvent?.Drop(this);
        public void NotifyBalanceStone(string message)
        {
            if (IsServerInitialized && Owner != null && Owner.IsActive) BalanceStoneMessageTargetRpc(Owner, message);
        }
        [TargetRpc]
        void BalanceStoneMessageTargetRpc(NetworkConnection connection, string message) => GetComponent<BalanceStonePlayer>()?.Notify(message);
    }
}
