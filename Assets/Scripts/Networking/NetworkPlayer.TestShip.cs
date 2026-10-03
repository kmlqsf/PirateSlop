using FishNet.Object;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
        [ServerRpc]
        public void TeleportToTestShip()
        {
            if (motor == null || motor.IsDead || IsBot.Value) return;
            foreach (var target in FindObjectsByType<ShipV3Features>(FindObjectsSortMode.None))
            {
                if (!target.IsSpawned || target.RespawnPoint == null || target.gameObject.scene != gameObject.scene) continue;
                TestShipTeleportObserversRpc(target.RespawnPoint.position, target.transform.eulerAngles.y, target.NetworkObject);
                return;
            }
        }

        [ObserversRpc(RunLocally = true)]
        void TestShipTeleportObserversRpc(Vector3 position, float yaw, NetworkObject platform)
        {
            var active = ActiveShip;
            if (active != null)
            {
                if (active.Helm != null && active.Helm.IsControlledBy(motor)) active.Helm.ReleaseControl();
                active.GetComponent<SailSystem>()?.ReleasePlayer(motor);
                if (IsOwner) active.Capstan?.CancelPush();
            }
            motor.ShipActivityLocked = false;
            motor.SailPullLocked = false;
            motor.BellPullLocked = false;
            Teleport(new PlayerState { Position = position, Yaw = yaw });
            passenger.Attach(platform != null ? platform.GetComponent<Rigidbody>() : null);
        }
    }
}
