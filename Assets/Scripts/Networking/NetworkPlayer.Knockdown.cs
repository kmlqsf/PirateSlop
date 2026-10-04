using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
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
