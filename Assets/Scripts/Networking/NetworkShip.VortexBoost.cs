using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        public const float VortexBoostDuration = 10f, VortexSpeedMultiplier = 5f, VortexBoostRampSeconds = .35f;
        public readonly SyncVar<bool> VortexBoostActive = new();
        float vortexBoostUntil;
        public void ApplyVortexBoost()
        {
            if (!IsServerInitialized || !IsSpawned || IsSinking) return;
            VortexBoostActive.Value = true;
            vortexBoostUntil = Time.time + VortexBoostDuration;
        }
        void TickVortexBoost()
        {
            if (!VortexBoostActive.Value || Time.time < vortexBoostUntil) return;
            VortexBoostActive.Value = false;
            Motor.FinishVortexBoost();
        }
    }
}

