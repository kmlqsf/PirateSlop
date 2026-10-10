using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        public const int RumCapacity = 12;
        readonly SyncVar<int> rum = new(3);
        public int RumCount => rum.Value;
        int rescueRings;
        public bool RingForRescue(int team)
        {
            if (!IsServerInitialized || !IsSpawned || IsSinking || team != TeamId.Value) return false;
            CombatHealth waiting = null;
            foreach (var member in NetworkPlayer.Active)
                if (member != null && member.Ship == this && member.TeamId.Value == team && !member.Eliminated.Value && member.GetComponent<CombatHealth>() is { IsDead: true } health)
                { waiting = health; break; }
            if (waiting == null || RumCount <= 0) { rescueRings = 0; return false; }
            if (++rescueRings < 3) return false;
            rescueRings = 0;
            return waiting.RespawnFromBell(this);
        }
        public bool ConsumeRespawnRum()
        {
            if (!IsServerInitialized || !IsSpawned || IsSinking || rum.Value <= 0) return false;
            rum.Value--;
            return true;
        }
        public bool AddUpgradeRum()
        {
            if (!IsServerInitialized || !IsSpawned || IsSinking) return false;
            rum.Value++;
            return true;
        }
        public int StoreRum(int amount)
        {
            if (!IsServerInitialized || !IsSpawned || IsSinking || amount <= 0) return 0;
            int added = UnityEngine.Mathf.Clamp(amount, 0, UnityEngine.Mathf.Max(0, RumCapacity - rum.Value));
            rum.Value += added;
            return added;
        }
    }
}
