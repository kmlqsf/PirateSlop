using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
namespace PirateSlop.Networking
{
    public sealed class NetworkHealth : NetworkBehaviour
    {
        readonly SyncVar<float> health = new(-1f);
        readonly SyncVar<bool> burning = new();
        float burnUntil, nextBurnDamage, wetUntil;
        GameObject fireAttacker;
        ShipFireVfx fireVisual;
        public bool IsBurning => burning.Value;
        CombatHealth target;
        void Awake() { target = GetComponent<CombatHealth>(); health.OnChange += HealthChanged; }
        public override void OnStartServer() { base.OnStartServer(); Publish(target.Current); }
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized && health.Value >= 0) target.ApplySnapshot(health.Value, false);
        }
        public void Publish(float value) { if (IsServerInitialized) health.Value = value; }

        public void Ignite(GameObject attacker = null)
        {
            if (!IsServerInitialized || target == null || target.IsDead || burning.Value || Time.time < wetUntil) return;
            var motor = GetComponent<AdvancedPlayerController>();
            if (motor != null && motor.IsSwimming) return;
            burning.Value = true;
            burnUntil = Time.time + Random.Range(5f, 9f);
            nextBurnDamage = Time.time;
            fireAttacker = attacker;
        }

        public void Extinguish()
        {
            if (!IsServerInitialized) return;
            burning.Value = false;
            fireAttacker = null;
            wetUntil = Time.time + 3f;
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServerInitialized && burning.Value)
            {
                var motor = GetComponent<AdvancedPlayerController>();
                bool swimming = motor != null && motor.IsSwimming;
                var ocean = OceanSurface.Instance;
                bool submerged = ocean != null && transform.position.y + .7f < ocean.Height(transform.position);
                if (target.IsDead || swimming || submerged || Time.time >= burnUntil) Extinguish();
                else if (Time.time >= nextBurnDamage)
                {
                    nextBurnDamage = Time.time + .25f;
                    target.Damage(15f * .25f, fireAttacker, true);
                }
            }
            if (!IsClientInitialized || Application.isBatchMode) return;
            if (burning.Value && !target.IsDead && fireVisual == null)
                fireVisual = ShipFireVfx.Create(transform, Vector3.up * .65f, Vector3.up, .45f, true);
            if ((!burning.Value || target.IsDead) && fireVisual != null) { fireVisual.Finish(); fireVisual = null; }
        }

        public override void OnStopNetwork()
        {
            if (fireVisual != null) Destroy(fireVisual.gameObject);
            fireVisual = null;
            fireAttacker = null;
            if (IsServerInitialized) burning.Value = false;
            base.OnStopNetwork();
        }
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
            Extinguish();
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
