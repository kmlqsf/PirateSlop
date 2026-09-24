using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using PirateSlop.Harpoon;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        HarpoonShipMount harpoonMount;
        public HarpoonShipMount HarpoonMount => harpoonMount != null ? harpoonMount : (harpoonMount = GetComponentInChildren<HarpoonShipMount>());

        public void RequestHarpoonControl(int index) => HarpoonControlServerRpc(index, true);
        public void ReleaseHarpoonControl(int index) => HarpoonControlServerRpc(index, false);

        [ServerRpc(RequireOwnership = false)]
        void HarpoonControlServerRpc(int index, bool take, NetworkConnection sender = null)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun == null) return;
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (player == null || player.Motor.IsDead) return;

            if (take)
            {
                if (gun.IsBroken || !gun.StructurallyAvailable || (gun.Operator != null && gun.Operator != player.Motor))
                    return;
                gun.TakeControl(player.Motor);
                HarpoonOccupantObserversRpc(index, player.NetworkObject);
            }
            else
            {
                if (gun.Operator == player.Motor)
                {
                    gun.ReleaseControl();
                    HarpoonOccupantObserversRpc(index, null);
                }
            }
        }

        [ObserversRpc(RunLocally = false)]
        void HarpoonOccupantObserversRpc(int index, NetworkObject playerObj)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun == null) return;
            var player = playerObj != null ? playerObj.GetComponent<AdvancedPlayerController>() : null;
            gun.SetRemoteOperator(player);
        }

        public void HarpoonAim(int index, float yaw, float pitch) => HarpoonAimServerRpc(index, yaw, pitch);

        [ServerRpc(RequireOwnership = false)]
        void HarpoonAimServerRpc(int index, float yaw, float pitch, NetworkConnection sender = null)
        {
            HarpoonAimObserversRpc(index, yaw, pitch);
        }

        [ObserversRpc(RunLocally = false)]
        void HarpoonAimObserversRpc(int index, float yaw, float pitch)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null) gun.SetRemoteAim(yaw, pitch);
        }

        public void HarpoonFire(int index, Vector3 spawnPos, Vector3 velocity) => HarpoonFireServerRpc(index, spawnPos, velocity);

        [ServerRpc(RequireOwnership = false)]
        void HarpoonFireServerRpc(int index, Vector3 spawnPos, Vector3 velocity, NetworkConnection sender = null)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun == null || gun.IsBroken) return;
            gun.LaunchFromNetwork(spawnPos, velocity);
            HarpoonFireObserversRpc(index, spawnPos, velocity);
        }

        [ObserversRpc(RunLocally = false)]
        void HarpoonFireObserversRpc(int index, Vector3 spawnPos, Vector3 velocity)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null) gun.LaunchFromNetwork(spawnPos, velocity);
        }

        public void HarpoonAttach(int index, bool isShip, NetworkObject targetNetObj, Vector3 localHitPos, Quaternion localHitRot, float cableLength)
        {
            if (!IsServerInitialized) return;
            HarpoonAttachObserversRpc(index, isShip, targetNetObj, localHitPos, localHitRot, cableLength);
        }

        [ObserversRpc(RunLocally = false)]
        void HarpoonAttachObserversRpc(int index, bool isShip, NetworkObject targetNetObj, Vector3 localHitPos, Quaternion localHitRot, float cableLength)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null && gun.ActiveProjectile != null)
            {
                gun.ActiveProjectile.AttachFromNetwork(isShip, targetNetObj, localHitPos, localHitRot, cableLength);
            }
        }

        public void HarpoonDetach(int index) => HarpoonDetachServerRpc(index);

        [ServerRpc(RequireOwnership = false)]
        void HarpoonDetachServerRpc(int index, NetworkConnection sender = null)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null && gun.ActiveProjectile != null)
            {
                gun.ActiveProjectile.DetachAndRewind();
            }
            HarpoonDetachObserversRpc(index);
        }

        [ObserversRpc(RunLocally = false)]
        void HarpoonDetachObserversRpc(int index)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null && gun.ActiveProjectile != null)
            {
                gun.ActiveProjectile.DetachAndRewind();
            }
        }

        public void HarpoonWinch(int index, float delta) => HarpoonWinchServerRpc(index, delta);

        [ServerRpc(RequireOwnership = false)]
        void HarpoonWinchServerRpc(int index, float delta, NetworkConnection sender = null)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null && gun.ActiveProjectile != null)
            {
                float newLen = gun.ActiveProjectile.CurrentCableLength - delta;
                gun.ActiveProjectile.SetCableLength(newLen);
                HarpoonWinchObserversRpc(index, newLen, delta * 70f);
            }
        }

        [ObserversRpc(RunLocally = false)]
        void HarpoonWinchObserversRpc(int index, float newLen, float drumAngle)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null)
            {
                if (gun.ActiveProjectile != null) gun.ActiveProjectile.SetCableLength(newLen);
                gun.RotateWinchDrum(drumAngle);
                GameAudio.Play(SoundCue.BulletMetal, gun.transform.position, 0.4f);
            }
        }

        public void PublishHarpoonState(int index, float health, bool broken)
        {
            if (IsServerInitialized)
                HarpoonStateObserversRpc(index, health, broken);
        }

        [ObserversRpc]
        void HarpoonStateObserversRpc(int index, float health, bool broken)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null) gun.ApplyNetworkState(health, broken);
        }

        public void HarpoonRepairStrike(int index, float health, bool broken, int strikes, Vector3 strikePoint)
        {
            if (IsServerInitialized)
                HarpoonRepairObserversRpc(index, health, broken, strikes, strikePoint);
        }

        [ObserversRpc]
        void HarpoonRepairObserversRpc(int index, float health, bool broken, int strikes, Vector3 strikePoint)
        {
            var gun = HarpoonMount != null ? HarpoonMount.GetGun(index) : null;
            if (gun != null) gun.ApplyRepairNetworkState(health, broken, strikes, strikePoint);
        }
    }
}
