using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> platformingSupport = new();
        readonly SyncVar<bool> platformingLinked = new();
        readonly SyncVar<double> platformingDropAt = new(-1);
        readonly SyncVar<Vector3> platformingDropFrom = new();
        readonly SyncVar<Quaternion> platformingDropFacing = new(Quaternion.identity);
        uint platformingTick;
        float platformingFrameAt;
        double PlatformingClock
        {
            get
            {
                if (TimeManager == null) return Time.timeAsDouble;
                uint tick = TimeManager.Tick;
                if (tick != platformingTick) { platformingTick = tick; platformingFrameAt = Time.unscaledTime; }
                return TimeManager.TicksToTime(tick) + Mathf.Min(Time.unscaledTime - platformingFrameAt, (float)TimeManager.TickDelta);
            }
        }
        public bool OnPlatforming => platformingLinked.Value;
        public bool PlatformingFalling => platformingDropAt.Value >= 0;
        public string PlatformingHint => PlatformingFalling ? "Сундук освобождён — заберите его в воде" : "Сундук закован в цепи · E у рычага — снять замок";
        public void PlaceOnPlatforming(NetworkPlatformingEvent owner)
        {
            kind.Value = SeaLootKind.Platforming;
            phase.Value = SeaLootState.Locked;
            platformingLinked.Value = true;
            platformingDropAt.Value = -1;
            platformingSupport.Value = owner.NetworkObject;
            eventPoint.Value = owner.transform.position;
        }

        public void ReleasePlatforming()
        {
            if (!IsServerInitialized || !IsSpawned || !OnPlatforming || PlatformingFalling) return;
            platformingDropFrom.Value = transform.position;
            platformingDropFacing.Value = transform.rotation;
            platformingDropAt.Value = PlatformingClock;
            opened.Value = true;
        }

        bool UpdatePlatformingChest()
        {
            if (!IsSpawned || !OnPlatforming) return false;
            if (!PlatformingFalling)
            {
                var owner = platformingSupport.Value != null ? platformingSupport.Value.GetComponent<NetworkPlatformingEvent>() : null;
                if (owner != null && owner.ChestSocket != null) transform.SetPositionAndRotation(owner.ChestSocket.position, owner.ChestSocket.rotation);
                return true;
            }
            float age = Mathf.Max(0, (float)(PlatformingClock - platformingDropAt.Value));
            var point = platformingDropFrom.Value + Vector3.down * (4.905f * age * age);
            float water = OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) - .12f : eventPoint.Value.y - .12f;
            bool arrived = point.y <= water;
            point.y = Mathf.Max(point.y, water);
            transform.SetPositionAndRotation(point, platformingDropFacing.Value * Quaternion.Euler(Mathf.Sin(age * 3) * Mathf.Min(age, 1) * 7, age * 8, 0));
            if (arrived && IsServerInitialized)
            {
                anchor.Value = point; eventPoint.Value = point; facing.Value = transform.rotation;
                phase.Value = SeaLootState.Ready;
                platformingLinked.Value = false; platformingSupport.Value = null;
            }
            return true;
        }
    }
}
