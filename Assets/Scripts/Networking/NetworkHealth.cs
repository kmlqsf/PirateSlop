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
            if (!IsServerInitialized && health.Value >= 0) target.ApplySnapshot(health.Value, false);
        }
        public void Publish(float value) { if (IsServerInitialized) health.Value = value; }
        public void LaunchCorpse(Vector3 position, Vector3 velocity, Vector3 spin)
        {
            if (IsServerInitialized) CorpseObserversRpc(position, velocity, spin);
        }
        [ObserversRpc(RunLocally = true)]
        void CorpseObserversRpc(Vector3 position, Vector3 velocity, Vector3 spin)
        {
            if (IsClientInitialized) DeathRagdoll.Spawn(transform, position, velocity, spin);
        }
        public void DamageFeedback(float amount, bool dead)
        {
            if (IsServerInitialized && Owner != null && Owner.IsActive) DamageFeedbackTargetRpc(Owner, amount, dead);
        }
        public void ConfirmHit()
        {
            if (IsServerInitialized && Owner != null && Owner.IsActive) HitFeedbackTargetRpc(Owner);
        }
        [TargetRpc]
        void DamageFeedbackTargetRpc(FishNet.Connection.NetworkConnection connection, float amount, bool dead)
            => GetComponent<PirateSlop.DamageFeedback>()?.ReceiveDamage(amount, dead);
        [TargetRpc]
        void HitFeedbackTargetRpc(FishNet.Connection.NetworkConnection connection)
            => GetComponent<PirateSlop.DamageFeedback>()?.ConfirmHit();
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
            if (!asServer && !IsServerInitialized && next >= 0) target.ApplySnapshot(next, false);
        }
    }
}
