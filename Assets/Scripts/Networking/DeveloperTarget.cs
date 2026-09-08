using UnityEngine;
using FishNet.Object;
namespace PirateSlop.Networking {
    public sealed class DeveloperTarget : NetworkBehaviour
    {
        readonly FishNet.Object.Synchronizing.SyncVar<NetworkObject> platform = new();
        readonly FishNet.Object.Synchronizing.SyncVar<Vector3> point = new();
        public void Place(NetworkShip ship, Vector3 position)
        { platform.Value = ship != null ? ship.NetworkObject : null; point.Value = ship != null ? ship.transform.InverseTransformPoint(position) : position; }
        void LateUpdate() { if (IsSpawned) transform.position = platform.Value != null ? platform.Value.transform.TransformPoint(point.Value) : point.Value; }
    }
}

