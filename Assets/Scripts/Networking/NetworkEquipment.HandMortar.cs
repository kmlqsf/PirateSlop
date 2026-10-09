using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkEquipment
    {
        public HandMortarSettings MortarSettings;
        public NetworkHandMortarBall MortarBallPrefab;
        public bool HandMortar => Item == InventoryItem.HandMortar;
        public float MortarShotAge => Time.time - mortarShotAt;
        float mortarShotAt = -10;
        Vector3 mortarEyeOffset;

        void UseHandMortar(byte request, Vector3 forward, Vector3 eyeOffset)
        {
            if (MortarSettings == null || MortarBallPrefab == null) return;
            int slot = inventory.SelectedSlot;
            if (request == 1)
            {
                if (ammunition[slot] == 0) BeginAction(1, MortarSettings.ReloadSeconds * GetComponent<NetworkPlayer>().ReloadUpgradeMultiplier, slot);
                return;
            }
            if (request != 0) return;
            if (ammunition[slot] == 0) { if (Owner != null && Owner.IsActive) EmptyTargetRpc(Owner); return; }
            if (!MortarLaunch(forward, eyeOffset, out _, out _)) return;
            direction.Value = forward.normalized;
            mortarEyeOffset = eyeOffset;
            ammunition[slot] = 0;
            rounds.Value = 0;
            nextShot = Time.time + MortarSettings.IgnitionSeconds + MortarSettings.ShotInterval;
            BeginAction(5, MortarSettings.IgnitionSeconds, slot);
            MortarIgnitionObserversRpc();
        }

        bool MortarLaunch(Vector3 forward, Vector3 eyeOffset, out Vector3 point, out Vector3 velocity)
        {
            point = velocity = Vector3.zero;
            var eye = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.5f);
            if (float.IsFinite(eyeOffset.sqrMagnitude) && eyeOffset.sqrMagnitude < 4f && !FirearmTrace.Cast(gameObject, eye, transform.position + eyeOffset, out _)) eye = transform.position + eyeOffset;
            point = eye + Quaternion.LookRotation(forward) * MortarSettings.LaunchOffset;
            if (FirearmTrace.Cast(gameObject, eye, point, out _)) return false;
            velocity = forward * MortarSettings.LaunchSpeed;
            var ship = GetComponent<ShipDeckPassenger>()?.Ship;
            if (ship != null) velocity += ship.GetComponent<ShipController>().CannonPointVelocity(point);
            return true;
        }

        void LaunchHandMortar()
        {
            if (!HandMortar || MortarSettings == null || MortarBallPrefab == null || !MortarLaunch(direction.Value, mortarEyeOffset, out var point, out var velocity)) return;
            var ball = Instantiate(MortarBallPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ball.gameObject, gameObject.scene);
            ball.Launch(GetComponent<NetworkPlayer>(), velocity, MortarSettings);
            ServerManager.Spawn(ball.NetworkObject);
            if (MortarSettings.ShooterKnockback > 0 || MortarSettings.ShooterLift > 0)
            {
                var recoilDirection = Vector3.ProjectOnPlane(direction.Value, Vector3.up);
                if (recoilDirection.sqrMagnitude < .001f) recoilDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                GetComponent<NetworkPlayer>().KnockDown(-recoilDirection.normalized * MortarSettings.ShooterKnockback + Vector3.up * MortarSettings.ShooterLift, MortarSettings.ShooterKnockdownSeconds);
            }
            recoilAt = Time.time;
            MortarFireObserversRpc(point, direction.Value);
        }

        [ObserversRpc(RunLocally = true)]
        void MortarIgnitionObserversRpc() => mortarShotAt = Time.time;

        [ObserversRpc(RunLocally = true)]
        void MortarFireObserversRpc(Vector3 point, Vector3 forward)
        {
            recoilAt = Time.time;
            var root = IsOwner && !motor.IsThirdPerson ? view : world;
            var visual = root != null ? root.GetComponentInChildren<HandMortarVisual>() : null;
            if (HandMortar && Active && visual != null) point = visual.Muzzle.position;
            FirearmVfx.Fire(point, forward, 1.3f);
            GameAudio.Play(SoundCue.Cannon, point);
        }
    }
}
