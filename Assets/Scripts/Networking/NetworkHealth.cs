using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
namespace PirateSlop.Networking
{
    public sealed class NetworkHealth : NetworkBehaviour
    {
        readonly SyncVar<float> health = new(-1f);
        CombatHealth target;
        void Awake() { target = GetComponent<CombatHealth>(); health.OnChange += HealthChanged; }
        public override void OnStartServer() { base.OnStartServer(); Publish(target.Current); }
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized && health.Value >= 0) target.ApplySnapshot(health.Value);
        }
        public void Publish(float value) { if (IsServerInitialized) health.Value = value; }
        public void Respawn(Vector3 position, float yaw)
        {
            if (!IsServerInitialized) return;
            RespawnObserversRpc(position, yaw);
            Publish(target.Current);
        }
        [ObserversRpc(RunLocally = true)]
        void RespawnObserversRpc(Vector3 position, float yaw) => target.Respawn(position, yaw);
        void HealthChanged(float previous, float next, bool asServer)
        {
            if (!asServer && !IsServerInitialized && next >= 0) target.ApplySnapshot(next);
        }
    }
}
