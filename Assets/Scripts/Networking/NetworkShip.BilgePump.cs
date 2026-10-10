using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct ShipPumpSnapshot
    {
        public int Sequence, Holder;
        public float Pull, ReturnFrom, ReleasedAt;
    }
    public sealed partial class NetworkShip
    {
        readonly SyncVar<ShipPumpSnapshot> pumpState = new();
        ShipBilgePump bilgePump;
        float pumpHeartbeat;
        public ShipPumpSnapshot PumpState => pumpState.Value;
        public void PullPump(float delta, bool holding, int sequence) => PullPumpServerRpc(delta, holding, sequence);
        [ServerRpc(RequireOwnership = false)]
        void PullPumpServerRpc(float delta, bool holding, int sequence, NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsActive || SessionController.Instance == null || !float.IsFinite(delta)) return;
            if (bilgePump == null) bilgePump = GetComponentInChildren<ShipBilgePump>();
            if (bilgePump == null) return;
            var current = pumpState.Value;
            int holder = sender.ClientId + 1;
            if (current.Sequence != sequence || current.Holder != 0 && current.Holder != holder) return;
            if (!holding)
            {
                if (current.Holder == holder) ReturnPump(ref current);
                return;
            }
            var player = SessionController.Instance.GetPlayer(sender.ClientId);
            if (!bilgePump.CanReach(player, current.Holder == holder)) return;
            if (current.Holder == 0)
            {
                if (bilgePump.Pull(current) > .005f) return;
                current.Holder = holder;
                current.Pull = current.ReturnFrom = 0f;
                current.ReleasedAt = bilgePump.Clock;
                pumpState.Value = current;
                player.Motor.ShipActivityLocked = true;
                player.Passenger?.Attach(GetComponent<Rigidbody>());
            }
            pumpHeartbeat = Time.time;
        }
        void ReturnPump(ref ShipPumpSnapshot current)
        {
            var player = SessionController.Instance != null ? SessionController.Instance.GetPlayer(current.Holder - 1) : null;
            if (player != null && player.Motor != null) player.Motor.ShipActivityLocked = false;
            float pull = bilgePump != null ? bilgePump.Pull(current) : current.Pull;
            current.Sequence++;
            current.Holder = 0;
            current.ReturnFrom = pull;
            current.ReleasedAt = bilgePump != null ? bilgePump.Clock : 0f;
            current.Pull = 0f;
            pumpState.Value = current;
        }
        void UpdatePump()
        {
            if (!IsServerInitialized) return;
            var current = pumpState.Value;
            if (current.Holder == 0 && current.ReturnFrom <= 0f) return;
            if (bilgePump == null) bilgePump = GetComponentInChildren<ShipBilgePump>();
            if (current.Holder != 0)
            {
                var player = SessionController.Instance != null ? SessionController.Instance.GetPlayer(current.Holder - 1) : null;
                if (bilgePump == null || Time.time - pumpHeartbeat > 1.5f || !bilgePump.CanReach(player, true)) ReturnPump(ref current);
                else GetComponent<ShipFlooding>()?.Pump(Time.deltaTime);
            }
            else if (bilgePump == null || bilgePump.Clock - current.ReleasedAt >= bilgePump.ReturnDuration)
            {
                current.ReturnFrom = 0f;
                pumpState.Value = current;
            }
        }
    }
}
