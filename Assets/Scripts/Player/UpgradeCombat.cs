using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public static class UpgradeCombat
    {
        public static void AfterHit(CombatHealth target, float previousHealth, GameObject attacker, bool firearm, Vector3 direction)
        {
            var source = attacker != null ? attacker.GetComponent<NetworkPlayer>() : null;
            var victim = target.GetComponent<NetworkPlayer>();
            if (source == null || !source.IsServerInitialized || victim == null || target.Current >= previousHealth || source.TeamId.Value == victim.TeamId.Value) return;
            if (source.HasUpgrade(UpgradeEffect.Bloodletter)) victim.StartUpgradeBleed(previousHealth - target.Current, attacker);
            if (firearm && source.HasUpgrade(UpgradeEffect.PushingBullets) && !target.IsDead)
                victim.PushByUpgrade(Vector3.ProjectOnPlane(direction, Vector3.up).normalized * RoguelikeTuning.Current.bulletPushSpeed);
        }
        public static void Ricochet(CombatHealth first, float amount, GameObject attacker, Vector3 origin)
        {
            var source = attacker.GetComponent<NetworkPlayer>();
            if (source == null || !source.IsServerInitialized || !source.HasUpgrade(UpgradeEffect.Ricochet) || amount <= 0) return;
            CombatHealth nearest = null;
            float best = RoguelikeTuning.Current.ricochetRange * RoguelikeTuning.Current.ricochetRange;
            Vector3 end = default;
            foreach (var candidate in CombatHealth.Active)
            {
                if (candidate == null || candidate == first || candidate.IsDead || !source.IsEnemy(candidate.GetComponent<NetworkPlayer>())) continue;
                Vector3 point = candidate.transform.position + Vector3.up * 1.1f, delta = point - origin;
                if (delta.sqrMagnitude > best) continue;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(attacker.transform) && hit.collider.GetComponentInParent<CombatHealth>() != first && hit.collider.GetComponentInParent<CombatHealth>() != candidate)
                    { blocked = true; break; }
                if (blocked) continue;
                nearest = candidate; best = delta.sqrMagnitude; end = point;
            }
            if (nearest == null) return;
            float before = nearest.Current;
            nearest.Damage(amount * RoguelikeTuning.Current.ricochetDamage, attacker);
            AfterHit(nearest, before, attacker, true, end - origin);
            attacker.GetComponent<NetworkWeapon>()?.PublishUpgradeTrace(new FirearmShot { Start = origin, End = end, Normal = (origin - end).normalized, Hit = true, Surface = BulletSurfaceKind.Flesh, LeaveMark = false });
        }
    }
}
