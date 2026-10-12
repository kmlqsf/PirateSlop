using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> towSupport = new();
        public bool InTow => towSupport.Value != null;
        public NetworkTowedCargo TowedCargo => towSupport.Value != null ? towSupport.Value.GetComponent<NetworkTowedCargo>() : null;
        public void PlaceInTow(NetworkTowedCargo cargo)
        {
            kind.Value = SeaLootKind.SternCargo; phase.Value = SeaLootState.Ready;
            towSupport.Value = cargo.NetworkObject; eventPoint.Value = cargo.transform.position;
        }
        public void ReleaseTowChest() { towSupport.Value = null; }
        public void PlaceTowRewardOnDeck(NetworkShip ship, UnityEngine.Vector3 point, UnityEngine.Quaternion rotation)
        {
            ReleaseTowChest(); PlaceOnDeck(ship, point, rotation); kind.Value = SeaLootKind.SternCargo;
        }
        public void ConfigureFloatingTowReward()
        {
            kind.Value = SeaLootKind.SternCargo; phase.Value = SeaLootState.Ready;
            eventPoint.Value = anchor.Value = transform.position; facing.Value = transform.rotation;
        }
        bool UpdateTowChest()
        {
            if (!InTow) return false;
            var cargo = towSupport.Value.GetComponent<NetworkTowedCargo>();
            if (cargo != null && cargo.ChestSocket != null) transform.SetPositionAndRotation(cargo.ChestSocket.position, cargo.ChestSocket.rotation);
            return true;
        }
    }
}
