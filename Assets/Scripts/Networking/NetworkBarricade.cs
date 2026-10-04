using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(85)]
    public sealed class NetworkBarricade : NetworkBehaviour
    {
        public GameObject Fragments;
        readonly SyncVar<NetworkObject> support = new();
        readonly SyncVar<int> shipId = new();
        readonly SyncVar<Vector3> localPosition = new();
        readonly SyncVar<Quaternion> localRotation = new(Quaternion.identity);
        NetworkShip resolved;
        bool removed;
        public bool Available => IsSpawned && !removed;
        public NetworkShip SupportingShip
        {
            get
            {
                if (support.Value != null) resolved = support.Value.GetComponent<NetworkShip>();
                if (resolved == null)
                    foreach (var ship in NetworkShip.ActiveShips)
                        if (ship != null && ship.ParticipantId.Value == shipId.Value) { resolved = ship; break; }
                return resolved;
            }
        }
        public void Place(NetworkShip ship, Vector3 point, Quaternion rotation)
        {
            support.Value = ship.NetworkObject;
            shipId.Value = ship.ParticipantId.Value;
            localPosition.Value = point;
            localRotation.Value = rotation;
            resolved = ship;
            FollowShip();
        }
        public override void OnStartServer() { base.OnStartServer(); removed = false; }
        void LateUpdate()
        {
            if (!IsSpawned) return;
            FollowShip();
            if (IsServerInitialized && SupportingShip == null) ServerManager.Despawn(NetworkObject);
        }
        void FollowShip()
        {
            var ship = SupportingShip;
            if (ship != null) transform.SetPositionAndRotation(ship.transform.TransformPoint(localPosition.Value), ship.transform.rotation * localRotation.Value);
        }
        public bool Collect(NetworkWeapon player)
        {
            if (!IsServerInitialized || !Available || player == null || !player.CanAddItem(InventoryItem.Barricade)) return false;
            if (!player.AddItem(InventoryItem.Barricade)) return false;
            removed = true;
            SoundObserversRpc(SoundCue.Pickup, transform.position);
            ServerManager.Despawn(NetworkObject);
            return true;
        }
        public bool Hit(CannonShotDamage shot, Vector3 point)
        {
            if (!IsServerInitialized || !Available || shot == null || !shot.Authoritative || shot.MortarShot || shot.Ammo != InventoryItem.Cannonball) return false;
            removed = true;
            BreakObserversRpc(transform.position, transform.rotation, shot.Velocity.normalized, Random.Range(1, int.MaxValue));
            ServerManager.Despawn(NetworkObject);
            return true;
        }
        [ObserversRpc(RunLocally = true)]
        void BreakObserversRpc(Vector3 position, Quaternion rotation, Vector3 direction, int seed)
        {
            GameAudio.Play(SoundCue.BarricadeBreak, position);
            BarricadeBreakAnimation.Play(Fragments, position, rotation, direction, seed);
        }
        [ObserversRpc(RunLocally = true)]
        void SoundObserversRpc(SoundCue cue, Vector3 point) => GameAudio.Play(cue, point);
    }
}
