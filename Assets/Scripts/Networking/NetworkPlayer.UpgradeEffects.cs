using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
        readonly SyncVar<ulong> effectMask = new();
        readonly SyncVar<float> pactSeconds = new();
        public float PactSeconds => pactSeconds.Value;
        public bool HasPendingSeaPact => UpgradeState != null && UpgradeState.PendingPact;
        readonly SyncVar<string> effectConfig = new("");
        float baseUpgradeHealth, lastUpgradeDamage, upgradeInvulnerableUntil;
        float bleedRemaining, bleedPerSecond;
        GameObject bleedSource;
        public bool HasUpgrade(UpgradeEffect effect) => (effectMask.Value & (1UL << (int)effect)) != 0;
        public float RepairUpgradeMultiplier => HasUpgrade(UpgradeEffect.FastCarpenterRare) ? RoguelikeTuning.Current.repairRare : HasUpgrade(UpgradeEffect.FastCarpenterCommon) ? RoguelikeTuning.Current.repairCommon : 1f;
        public float FishingUpgradeMultiplier => HasUpgrade(UpgradeEffect.LuckyBaitRare) ? RoguelikeTuning.Current.fishingRare : HasUpgrade(UpgradeEffect.LuckyBaitCommon) ? RoguelikeTuning.Current.fishingCommon : 1f;
        public float SabreUpgradeMultiplier => HasUpgrade(UpgradeEffect.SabreMaster) ? RoguelikeTuning.Current.sabreMultiplier : 1f;
        public float ReloadUpgradeMultiplier => HasUpgrade(UpgradeEffect.QuickHands) ? RoguelikeTuning.Current.reloadMultiplier : 1f;

        void InitializeUpgradeEffects()
        {
            effectConfig.OnChange += (_, next, asServer) => { if (!string.IsNullOrEmpty(next)) RoguelikeTuning.Receive(next); };
            baseUpgradeHealth = GetComponent<CombatHealth>().MaxHealth;
        }

        internal void RestoreUpgradeEffects()
        {
            if (UpgradeState == null) return;
            ulong mask = 0;
            foreach (string id in UpgradeState.Taken)
                if (Enum.TryParse(id, out UpgradeEffect effect)) mask |= 1UL << (int)effect;
            effectMask.Value = mask;
            effectConfig.Value = JsonUtility.ToJson(RoguelikeTuning.Current);
            var health = GetComponent<CombatHealth>();
            health.SetMaximum(baseUpgradeHealth * (HasUpgrade(UpgradeEffect.ToughPirate) ? RoguelikeTuning.Current.maxHealthMultiplier : 1f));
        }

        internal void ApplyChosenUpgrade(UpgradeCard card)
        {
            RestoreUpgradeEffects();
            if (card.id == "RumSupply") UpgradeState.PendingRum = true;
            if (card.id == "ExtraMonkey") UpgradeState.PendingMonkey = true;
            ApplyPendingShipUpgrades();
        }

        void ApplyPendingShipUpgrades()
        {
            if (UpgradeState == null || Ship == null || Ship.IsSinking || !Ship.IsSpawned) return;
            if (UpgradeState.PendingRum && Ship.AddUpgradeRum()) UpgradeState.PendingRum = false;
            if (UpgradeState.PendingMonkey && Ship.AddUpgradeMonkey()) UpgradeState.PendingMonkey = false;
        }

        public float FilterUpgradeDamage(float amount)
        {
            if (!IsServerInitialized) return amount;
            if (Time.time < upgradeInvulnerableUntil) return 0f;
            lastUpgradeDamage = Time.time;
            var health = GetComponent<CombatHealth>();
            if (amount >= health.Current && HasUpgrade(UpgradeEffect.CharmedCoin) && !UpgradeState.CoinUsed)
            {
                UpgradeState.CoinUsed = true;
                upgradeInvulnerableUntil = Time.time + RoguelikeTuning.Current.coinInvulnerability;
                UpgradeAudioObserversRpc(SoundCue.CoinSave, transform.position);
                return Mathf.Max(0f, health.Current - 1f);
            }
            if (amount >= health.Current && HasUpgrade(UpgradeEffect.SeaPact) && !UpgradeState.PactUsed)
            {
                UpgradeState.PactUsed = UpgradeState.PendingPact = true;
                UpgradeState.PactAt = Time.time + RoguelikeTuning.Current.pactDelay;
                UpgradeState.PactPosition = UpgradeState.PactWorldPosition = transform.position;
                pactSeconds.Value = RoguelikeTuning.Current.pactDelay;
                UpgradeState.PactYaw = transform.eulerAngles.y;
                var platform = Passenger != null ? Passenger.Ship : null;
                UpgradeState.PactPlatform = platform != null ? platform.transform : null;
                if (UpgradeState.PactPlatform != null)
                {
                    UpgradeState.PactPosition = UpgradeState.PactPlatform.InverseTransformPoint(transform.position);
                    UpgradeState.PactYaw -= UpgradeState.PactPlatform.eulerAngles.y;
                }
            }
            return amount;
        }

        [ObserversRpc(RunLocally = true)]
        void UpgradeAudioObserversRpc(SoundCue cue, Vector3 point) => GameAudio.Play(cue, point);

        public void StartUpgradeBleed(float hitDamage, GameObject source)
        {
            var attacker = source != null ? source.GetComponent<NetworkPlayer>() : null;
            if (!IsServerInitialized || hitDamage <= 0 || attacker == null || attacker == this || attacker.TeamId.Value == TeamId.Value) return;
            bleedRemaining = RoguelikeTuning.Current.bleedDuration;
            bleedPerSecond = hitDamage * RoguelikeTuning.Current.bleedFraction / bleedRemaining;
            bleedSource = source;
        }

        public bool IsEnemy(NetworkPlayer other) => other != null && other != this && !other.Eliminated.Value && other.TeamId.Value != TeamId.Value && !other.GetComponent<CombatHealth>().IsDead;

        void TickUpgradeEffects()
        {
            if (!IsServerInitialized || !IsSpawned) return;
            ApplyPendingShipUpgrades();
            var health = GetComponent<CombatHealth>();
            var state = UpgradeState;
            if (state != null && state.PendingPact)
            {
                pactSeconds.Value = Mathf.Ceil(Mathf.Max(0, state.PactAt - Time.time) * 10f) / 10f;
                if (state.PactPlatform != null) state.PactWorldPosition = state.PactPlatform.TransformPoint(state.PactPosition);
            }
            if (state != null && state.PendingPact && (!health.IsDead || !health.CanRespawn)) { state.PendingPact = false; pactSeconds.Value = 0f; }
            if (state != null && state.PendingPact && Time.time >= state.PactAt)
            {
                state.PendingPact = false; pactSeconds.Value = 0f;
                if (health.IsDead && !Eliminated.Value)
                {
                    Vector3 point = state.PactPlatform != null ? state.PactPlatform.TransformPoint(state.PactPosition) : state.PactWorldPosition;
                    float yaw = state.PactPlatform != null ? state.PactPlatform.eulerAngles.y + state.PactYaw : state.PactYaw;
                    GetComponent<NetworkHealth>().Respawn(point, yaw, RoguelikeTuning.Current.pactHealth);
                    UpgradeAudioObserversRpc(SoundCue.PactRevive, point);
                }
            }
            if (health.IsDead) { bleedRemaining = 0; return; }
            if (bleedRemaining > 0)
            {
                float dt = Mathf.Min(bleedRemaining, Time.deltaTime);
                bleedRemaining -= dt;
                health.Damage(bleedPerSecond * dt, bleedSource);
            }
            if (HasUpgrade(UpgradeEffect.SecondWind) && !health.IsDead && Time.time - lastUpgradeDamage >= RoguelikeTuning.Current.regenDelay)
                health.Heal(Mathf.Min(health.MaxHealth * RoguelikeTuning.Current.regenPerSecond * Time.deltaTime, health.MaxHealth * RoguelikeTuning.Current.regenLimit - health.Current));
        }

        public void PushByUpgrade(Vector3 velocity)
        {
            if (!IsServerInitialized || motor.IsDead || Eliminated.Value) return;
            motor.ApplyKnockback(velocity);
            UpgradePushObserversRpc(velocity);
        }
        [ObserversRpc]
        void UpgradePushObserversRpc(Vector3 velocity) { if (!IsServerInitialized && !motor.IsDead) motor.ApplyKnockback(velocity); }

        public void HealFromFish(float amount)
        {
            if (!IsServerInitialized) return;
            if (HasUpgrade(UpgradeEffect.HeartyCatch)) amount *= RoguelikeTuning.Current.fishHealingMultiplier;
            var health = GetComponent<CombatHealth>();
            if (health.IsDead) return;
            health.Heal(amount);
            if (!HasUpgrade(UpgradeEffect.PirateFeast) || amount <= 0f) return;
            foreach (var ally in Active)
                if (ally != null && ally != this && ally.IsSpawned && ally.TeamId.Value == TeamId.Value)
                    ally.GetComponent<CombatHealth>().Heal(amount * RoguelikeTuning.Current.feastFraction);
        }
    }
}
