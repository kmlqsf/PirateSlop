using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        public const int RumCapacity = 12;
        readonly SyncVar<int> rum = new(3);
        public int RumCount => rum.Value;
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
