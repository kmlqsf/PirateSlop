using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
        public void PushFromCannonImpact(Vector3 velocity)
        {
            if (!IsServerInitialized || !IsSpawned || motor == null || motor.IsDead || motor.IsDowned || Eliminated.Value || !float.IsFinite(velocity.sqrMagnitude)) return;
            KickPushObserversRpc(Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up), 2.5f));
        }

        public void KnockDown(Vector3 velocity, float duration)
        {
            if (!IsServerInitialized || !IsSpawned || motor == null || motor.IsDead || motor.IsDowned || Eliminated.Value) return;
            motor.ApplyKnockdown(velocity, duration);
            KnockdownObserversRpc(velocity, duration);
        }

        [ObserversRpc]
        void KnockdownObserversRpc(Vector3 velocity, float duration)
        {
            if (!IsServerInitialized && motor != null && !motor.IsDead) motor.ApplyKnockdown(velocity, duration);
        }
    }
}
