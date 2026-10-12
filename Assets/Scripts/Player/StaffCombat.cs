using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public static class StaffCombat
    {
        public static void Impact(NetworkPlayer shooter, Vector3 point, StaffSettings settings, float radiusMultiplier = 1f)
        {
            if (shooter == null || !shooter.IsServerInitialized || settings == null) return;
            foreach (var health in CombatHealth.Active)
            {
                if (health == null || health.IsDead || health.gameObject == shooter.gameObject || health.gameObject.scene != shooter.gameObject.scene) continue;
                var player = health.GetComponent<NetworkPlayer>();
                var networkHealth = health.GetComponent<NetworkHealth>();
                if (player == null || networkHealth == null || networkHealth.DeveloperInvulnerable || player.Ship != null && player.Ship.IsSinking) continue;
                if (!SessionController.FriendlyFire && shooter.TeamId.Value > 0 && shooter.TeamId.Value == player.TeamId.Value) continue;
                var body = health.GetComponent<CharacterController>();
                Vector3 nearest = body != null ? body.bounds.ClosestPoint(point) : health.transform.position + Vector3.up;
                if ((nearest - point).sqrMagnitude > settings.BlastRadius * settings.BlastRadius * radiusMultiplier * radiusMultiplier) continue;
                Vector3 center = body != null ? body.bounds.center : nearest;
                if (FirearmTrace.Cast(shooter.gameObject, point, center, out var obstacle) && obstacle.collider.GetComponentInParent<CombatHealth>() != health) continue;
                health.Damage(settings.Damage, shooter.gameObject);
                if (settings.Item == InventoryItem.FireStaff) networkHealth.Ignite(shooter.gameObject, settings.BurnSeconds);
                else if (settings.Item == InventoryItem.IceStaff) networkHealth.Slow(settings.SlowSeconds, settings.SlowMultiplier);
            }
        }
    }
}
