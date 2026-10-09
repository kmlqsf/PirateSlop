using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
namespace PirateSlop.Networking
{
    public sealed class NetworkHealth : NetworkBehaviour
    {
        readonly SyncVar<float> maximumHealth = new(100f);
        readonly SyncVar<float> health = new(-1f);
        readonly SyncVar<bool> burning = new();
        readonly SyncVar<bool> developerInvulnerable = new();
        readonly SyncVar<uint> frozenUntil = new();
        float burnUntil, nextBurnDamage, wetUntil;
        GameObject fireAttacker;
        ShipFireVfx fireVisual;
        public bool IsBurning => burning.Value;
        public bool DeveloperInvulnerable => developerInvulnerable.Value;
        CombatHealth target;
        public float FrozenSeconds => frozenUntil.Value == 0 || TimeManager == null ? 0f : Mathf.Max(0, unchecked((int)(frozenUntil.Value - TimeManager.Tick))) * (float)TimeManager.TickDelta;
        public bool IsFrozen => FrozenSeconds > 0f;
        void Awake()
        {
            target = GetComponent<CombatHealth>();
            health.OnChange += HealthChanged;
            frozenUntil.OnChange += FreezeChanged;
            maximumHealth.OnChange += (_, next, asServer) => { if (!asServer && !IsServerInitialized) { target.MaxHealth = next; if (health.Value >= 0) target.ApplySnapshot(health.Value, false); } };
        }
        public override void OnStartServer() { base.OnStartServer(); PublishMaximum(target.MaxHealth); Publish(target.Current); }
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized) { target.MaxHealth = maximumHealth.Value; if (health.Value >= 0) target.ApplySnapshot(health.Value, false); }
            if (IsFrozen) GetComponent<AdvancedPlayerController>()?.BeginFreeze();
        }
        public void Freeze(float seconds = 1f)
        {
            if (!IsServerInitialized || target == null || target.IsDead || !float.IsFinite(seconds) || seconds <= 0f) return;
            frozenUntil.Value = TimeManager.Tick + (uint)Mathf.CeilToInt(seconds / (float)TimeManager.TickDelta);
            GetComponent<AdvancedPlayerController>()?.BeginFreeze();
        }
        void FreezeChanged(uint previous, uint next, bool asServer)
        {
            if (IsFrozen) GetComponent<AdvancedPlayerController>()?.BeginFreeze();
        }
        public void PublishMaximum(float value) { if (IsServerInitialized) maximumHealth.Value = value; }
        public void Publish(float value) { if (IsServerInitialized) health.Value = value; }
        public void SetDeveloperInvulnerable(bool enabled)
        {
            if (IsServerInitialized) developerInvulnerable.Value = enabled;
        }

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
            if (IsServerInitialized && frozenUntil.Value != 0 && (!IsFrozen || target.IsDead)) frozenUntil.Value = 0;
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
            if (IsServerInitialized) frozenUntil.Value = 0;
            if (IsServerInitialized) burning.Value = false;
            if (IsServerInitialized) developerInvulnerable.Value = false;
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
        public void DamageFeedback(float amount, bool dead, bool drowning = false)
        {
            if (IsServerInitialized && Owner != null && Owner.IsActive) DamageFeedbackTargetRpc(Owner, amount, dead, drowning);
        }
        public void ConfirmHit()
        {
            if (IsServerInitialized && Owner != null && Owner.IsActive) HitFeedbackTargetRpc(Owner);
        }
        [TargetRpc]
        void DamageFeedbackTargetRpc(FishNet.Connection.NetworkConnection connection, float amount, bool dead, bool drowning)
            => GetComponent<PirateSlop.DamageFeedback>()?.ReceiveDamage(amount, dead, drowning);
        [TargetRpc]
        void HitFeedbackTargetRpc(FishNet.Connection.NetworkConnection connection)
            => GetComponent<PirateSlop.DamageFeedback>()?.ConfirmHit();
        public void Respawn(Vector3 position, float yaw, float healthFraction = 1f)
        {
            if (!IsServerInitialized) return;
            Extinguish();
            frozenUntil.Value = 0;
            RespawnObserversRpc(position, yaw, healthFraction);
            Publish(target.Current);
        }
        [ObserversRpc(RunLocally = true)]
        void RespawnObserversRpc(Vector3 position, float yaw, float healthFraction) => target.Respawn(position, yaw, healthFraction);
        void HealthChanged(float previous, float next, bool asServer)
        {
            if (!asServer && !IsServerInitialized && next >= 0) target.ApplySnapshot(next, false);
        }
    }
}
