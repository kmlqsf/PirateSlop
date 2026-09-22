using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotBoomerangShot
    {
        readonly RaycastHit[] hits = new RaycastHit[32];
        public static bool Solve(SimpleCannon cannon, NetworkShip ship, Vector3 observed, Vector3 velocity, out Vector3 direction, out float time)
        {
            direction = default; time = 0;
            float speed = cannon.LaunchSpeed * 1.15f;
            var inherited = ship.Motor.CannonPointVelocity(cannon.Muzzle.position);
            var offset = observed + velocity * cannon.FuseSeconds - cannon.ShotPosition - inherited * cannon.FuseSeconds;
            var relative = velocity - inherited;
            float a = relative.sqrMagnitude - speed * speed, b = 2f * Vector3.Dot(offset, relative), c = offset.sqrMagnitude;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0) return false;
            if (Mathf.Abs(a) < .001f) { if (Mathf.Abs(b) < .001f) return false; time = -c / b; }
            else
            {
                float root = Mathf.Sqrt(discriminant);
                float first = (-b - root) / (2f * a), second = (-b + root) / (2f * a);
                time = first > 0 && second > 0 ? Mathf.Min(first, second) : Mathf.Max(first, second);
            }
            if (time <= 0 || time >= CannonShotDamage.DefaultBoomerangOutboundTime - .1f) return false;
            direction = (offset / time + relative).normalized;
            var local = cannon.transform.InverseTransformDirection(direction);
            float elevation = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            return elevation >= cannon.MinElevation && elevation <= cannon.MaxElevation &&
                Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) <= cannon.MaxTraverse;
        }
        bool Segment(NetworkPlayer player, NetworkShip ship, NetworkShip target, Vector3 from, Vector3 to, float radius, float future, out RaycastHit impact)
        {
            impact = default;
            if (SessionController.Instance.BotAllyInShotSegment(from, to, radius + 1f, future, player)) return false;
            var delta = to - from;
            int count = Physics.SphereCastNonAlloc(from, radius + .25f, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Collide);
            if (count == hits.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(ship.transform) || !PlayerHitbox.IsTarget(hit.collider) ||
                    hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris") || hit.collider.GetComponentInParent<CannonShotDamage>() != null) continue;
                if (hit.collider.GetComponentInParent<NetworkShip>() != target) return false;
                if (hit.distance < nearest) { nearest = hit.distance; impact = hit; }
            }
            if (OceanSurface.Instance != null && to.y - radius <= OceanSurface.Instance.Height(to)) return false;
            foreach (var other in NetworkShip.ActiveShips)
            {
                if (other == null || other == ship || other.TeamId.Value != ship.TeamId.Value) continue;
                var predicted = other.transform.position + other.Motor.CannonPointVelocity(other.transform.position) * future;
                var line = to - from;
                var closest = from + line * Mathf.Clamp01(Vector3.Dot(predicted - from, line) / Mathf.Max(.001f, line.sqrMagnitude));
                if (Vector3.Distance(closest, predicted) < other.CollisionRadius + radius + 3f) return false;
            }
            return true;
        }
        bool ReturnSafe(NetworkPlayer player, SimpleCannon cannon, NetworkShip ship, NetworkShip target, Vector3 origin, Vector3 inherited,
            Vector3 start, Vector3 normal, Vector3 right, float outbound)
        {
            var control = start + normal * 8f + Vector3.up * CannonShotDamage.DefaultBoomerangHeight;
            var previous = start;
            float duration = CannonShotDamage.DefaultBoomerangDuration * .5f;
            for (int step = 1; step <= 30; step++)
            {
                float elapsed = duration * step / 30f;
                var destination = origin + inherited * (outbound + elapsed);
                var next = CannonShotDamage.ReturnCurve(start, control, destination, right, CannonShotDamage.DefaultBoomerangWidth,
                    CannonShotDamage.DefaultBoomerangHeight, step / 30f);
                if (!Segment(player, ship, target, previous, next, cannon.ProjectileRadius,
                    cannon.FuseSeconds + outbound + elapsed, out _)) return false;
                previous = next;
            }
            return true;
        }
        public bool Clear(NetworkPlayer player, SimpleCannon cannon, NetworkShip ship, NetworkShip target, Vector3 direction)
        {
            var inherited = ship.Motor.CannonPointVelocity(cannon.Muzzle.position);
            var origin = cannon.ShotPosition + inherited * cannon.FuseSeconds;
            var speed = direction * (cannon.LaunchSpeed * 1.15f) + inherited;
            var right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(speed, Vector3.up).normalized);
            var previous = origin;
            bool checkedImpact = false;
            float outbound = CannonShotDamage.DefaultBoomerangOutboundTime;
            for (int step = 1; step <= 20; step++)
            {
                float elapsed = outbound * step / 20f;
                var next = origin + speed * elapsed;
                if (!Segment(player, ship, target, previous, next, cannon.ProjectileRadius, cannon.FuseSeconds + elapsed, out var hit)) return false;
                if (!checkedImpact && hit.collider != null)
                {
                    checkedImpact = true;
                    if (!ReturnSafe(player, cannon, ship, target, origin, inherited, hit.point + hit.normal * (cannon.ProjectileRadius + .05f),
                        hit.normal, right, elapsed)) return false;
                }
                previous = next;
            }
            return ReturnSafe(player, cannon, ship, target, origin, inherited, previous, speed.normalized, right, outbound);
        }
    }
}
