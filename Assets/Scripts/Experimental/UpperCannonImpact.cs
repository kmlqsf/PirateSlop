#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using PirateSlop.Networking;
using PirateSlop.Ships;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public struct UpperImpactShape
    {
        public Vector3 Point, Normal, Direction;
        public bool Radial, Metal;
        public const float Length = 5f, Radius = 2.5f;
        public static UpperImpactShape Create(Vector3 point, Vector3 normal, Vector3 velocity, Vector3 up, bool metal)
        {
            var direction = velocity.sqrMagnitude > .001f ? velocity.normalized : -normal;
            bool grazing = Mathf.Abs(Vector3.Dot(direction, normal)) < .342f;
            bool radial = !grazing && Vector3.Dot(normal, up) > .65f && Vector3.Dot(direction, up) < -.2f;
            if (grazing)
            {
                var tangent = Vector3.ProjectOnPlane(direction, normal);
                if (tangent.sqrMagnitude > .001f) direction = tangent.normalized;
            }
            return new UpperImpactShape { Point = point, Normal = normal, Direction = direction, Radial = radial, Metal = metal };
        }
        public float Fraction(Vector3 target)
        {
            var delta = target - Point;
            float distance = delta.magnitude;
            if (Radial) return Mathf.Clamp01(1f - distance / Radius);
            float forward = Vector3.Dot(delta, Direction);
            if (forward < -.15f || forward > Length || distance > Length) return 0f;
            if (distance > .2f && forward / distance < .819152f) return 0f;
            return Mathf.Clamp01(1f - distance / Length);
        }
        public Vector3 Origin => Point + Normal * .035f;
    }

    public static class UpperCannonImpact
    {
        static readonly RaycastHit[] obstacles = new RaycastHit[128];
        static readonly Collider[] blastHits = new Collider[512];
        static readonly HashSet<NetworkShip> blastShips = new();
        static readonly HashSet<KrakenTentacle> blastTentacles = new();
        public static bool Resolve(CannonShotDamage shot, Collider collider, Vector3 point, Vector3 normal)
        {
            if (shot.Ammo == InventoryItem.BoardingHook || !CannonAmmo.IsBall(shot.Ammo)) return false;
            var targetShip = collider != null ? collider.GetComponentInParent<NetworkShip>() : null;
            var upper = targetShip != null ? targetShip.GetComponent<UpperDestructionV2>() : null;
            var sourceUpper = shot.Source != null ? shot.Source.GetComponent<UpperDestructionV2>() : null;
            var direct = collider != null ? collider.GetComponentInParent<CombatHealth>() : null;
            if (upper == null && sourceUpper == null || targetShip == null && direct == null) return false;
            if (!shot.Authoritative) return true;
            bool weapon = upper != null && upper.HitWeapon(collider, point, normal, shot.Velocity, shot.Ammo, shot.Attacker);
            var surface = collider != null ? ShipV3CollisionBatch.ResolveSection(collider, point) : null;
            bool protectedMast = upper != null && surface != null && upper.IsMain(surface.SectionId);
            var shape = UpperImpactShape.Create(point, normal, shot.Velocity, targetShip != null ? targetShip.transform.up : Vector3.up, weapon || protectedMast);
            if (targetShip != null)
            {
                float radius = shot.MortarShot || shot.Ammo == InventoryItem.FireCannonball ? CannonAmmo.MortarBlastRadius(shot.Ammo) : 0f;
                if (!weapon) targetShip.GetComponent<ShipDestruction>()?.Damage(collider, point, normal, shot.Velocity, shot.Ammo, shot.Attacker, radius);
                targetShip.Motor.ApplyCannonImpulse(point, shot.Velocity.normalized * Mathf.Clamp(shot.Velocity.magnitude / 40f, .5f, 1.5f), 1.5f);
                if (shot.Ammo == InventoryItem.FireCannonball) targetShip.Ignite(point, shot.Attacker, shot.MortarShot ? CannonAmmo.MortarBlastRadius(shot.Ammo) : shot.StandardBlastRadius, collider, normal);
                if (shot.Ammo == InventoryItem.IceCannonball) targetShip.FreezeFromShot(point);
                if (shot.Ammo == InventoryItem.PushCannonball) targetShip.Motor.ApplyPushImpulse(point, shot.Velocity);
                if (upper != null) upper.PublishImpact(shape);
                else targetShip.ImpactVfx(point, normal);
            }
            if (shot.MortarShot || shot.Ammo == InventoryItem.FireCannonball) ApplyBlast(shot, collider, targetShip, point, normal);
            foreach (var health in CombatHealth.Active)
            {
                if (health == null || health.IsDead) continue;
                bool hitDirectly = health == direct;
                Vector3 target = health.transform.position + Vector3.up * .9f;
                float fraction = hitDirectly ? 1f : targetShip != null ? shape.Fraction(target) : 0f;
                if (fraction <= 0f || !hitDirectly && !Visible(shape.Origin, target, health)) continue;
                health.Damage(hitDirectly ? 60f : 30f * fraction, shot.Attacker);
                shot.PushUpperImpact(health, point);
                if (shot.Ammo == InventoryItem.FireCannonball) health.GetComponent<NetworkHealth>()?.Ignite(shot.Attacker);
                if (shot.Ammo == InventoryItem.IceCannonball) health.GetComponent<NetworkHealth>()?.Extinguish();
            }
            if (direct != null && targetShip == null && sourceUpper != null) sourceUpper.PublishPeopleImpact(point, normal);
            return true;
        }

        static void ApplyBlast(CannonShotDamage shot, Collider directCollider, NetworkShip directShip, Vector3 point, Vector3 normal)
        {
            blastShips.Clear(); blastTentacles.Clear();
            if (directShip != null) blastShips.Add(directShip);
            float radius = shot.Ammo == InventoryItem.FireCannonball ? shot.StandardBlastRadius : CannonAmmo.MortarBlastRadius(shot.Ammo);
            int count = Physics.OverlapSphereNonAlloc(point, radius, blastHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = blastHits[i];
                var tentacle = hit.GetComponentInParent<KrakenTentacle>();
                if (tentacle != null && blastTentacles.Add(tentacle)) tentacle.TakeDamage(shot.Ammo == InventoryItem.Cannonball ? shot.StandardBlastDamage : shot.PlayerDamage, shot.Attacker);
                var ship = hit.GetComponentInParent<NetworkShip>();
                if (ship == null || !blastShips.Add(ship)) continue;
                if (shot.Source != null && ship.transform == shot.Source && !SessionController.FriendlyFire) continue;
                ship.GetComponent<ShipDestruction>()?.Damage(hit, point, normal, shot.Velocity, shot.Ammo, shot.Attacker, CannonAmmo.MortarBlastRadius(shot.Ammo));
                ship.Motor.ApplyCannonImpulse(point, shot.Velocity.normalized, 1.5f);
                if (shot.Ammo == InventoryItem.FireCannonball) ship.Ignite(point, shot.Attacker, radius, hit, normal);
                if (shot.Ammo == InventoryItem.IceCannonball) ship.FreezeFromShot(point);
                if (shot.Ammo == InventoryItem.PushCannonball) ship.Motor.ApplyPushImpulse(point, shot.Velocity);
            }
        }

        public static bool Visible(Vector3 start, Vector3 end, CombatHealth target = null)
            => ClearDistance(start, end) >= Vector3.Distance(start, end) - .025f;

        public static float ClearDistance(Vector3 start, Vector3 end)
        {
            float nearest = Vector3.Distance(start, end);
            foreach (var upper in UpperDestructionV2.Active)
                if (upper != null && upper.isActiveAndEnabled) nearest = Mathf.Min(nearest, upper.BlockDistance(start, end));
            var delta = end - start;
            float distance = delta.magnitude;
            if (distance < .025f) return nearest;
            int count = Physics.RaycastNonAlloc(start, delta / distance, obstacles, distance - .025f, ~0, QueryTriggerInteraction.Ignore);
            if (count == obstacles.Length) return 0f;
            for (int i = 0; i < count; i++)
            {
                var hit = obstacles[i].collider;
                if (hit == null || hit.GetComponentInParent<CombatHealth>() != null || hit.GetComponentInParent<Cannonball>() != null || hit.gameObject.layer == LayerMask.NameToLayer("ShipDebris") || hit.gameObject.layer == LayerMask.NameToLayer("Water")) continue;
                var owner = hit.GetComponentInParent<ShipDestruction>();
                if (owner != null && owner.Upper != null && (hit.GetComponent<ShipV3CollisionBatch>() != null || owner.SectionFor(hit) != null)) continue;
                nearest = Mathf.Min(nearest, obstacles[i].distance);
            }
            return nearest;
        }
    }

    public sealed partial class UpperDestructionV2
    {
        uint impactSeed;
        public void PublishPeopleImpact(Vector3 point, Vector3 normal)
        {
            if (IsServerInitialized) PeopleImpactObserversRpc(point, normal);
        }
        [FishNet.Object.ObserversRpc]
        void PeopleImpactObserversRpc(Vector3 point, Vector3 normal) => CombatVfx.Impact(point, normal, true);

        public void PublishImpact(UpperImpactShape shape)
        {
            if (!IsServerInitialized) return;
            ImpactObserversRpc(transform.InverseTransformPoint(shape.Point), transform.InverseTransformDirection(shape.Normal), transform.InverseTransformDirection(shape.Direction), shape.Radial, shape.Metal, ++impactSeed);
        }
        [FishNet.Object.ObserversRpc]
        void ImpactObserversRpc(Vector3 point, Vector3 normal, Vector3 direction, bool radial, bool metal, uint seed)
        {
            presentation?.EmitImpact(new UpperImpactShape { Point = transform.TransformPoint(point), Normal = transform.TransformDirection(normal), Direction = transform.TransformDirection(direction), Radial = radial, Metal = metal }, seed);
            GameAudio.Play(SoundCue.Impact, transform.TransformPoint(point));
        }
    }
}
#endif
