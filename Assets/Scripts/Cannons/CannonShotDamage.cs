using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class CannonShotDamage : MonoBehaviour
    {
        public bool Authoritative;
        public bool MortarShot;
        public Transform Source;
        public int SourceCannonIndex = -1;
        public GameObject Attacker;
        public Vector3 Velocity;
        public InventoryItem Ammo = InventoryItem.Cannonball;
        public float Radius = .12f, Drag = .015f;
        public static Vector3 StepVelocity(Vector3 velocity, float dt, float drag = .015f) => (velocity + Physics.gravity * dt) * Mathf.Exp(-drag * dt);
        public float PlayerDamage = 45f, PlayerPushSpeed = 22f;
        public float StandardBlastRadius = .8f, StandardBlastDamage = 30f;
        public float WorldRestitution = .55f, WorldTangentialRetention = .82f;
        public const float DefaultBoomerangDuration = 6f, DefaultBoomerangWidth = 16f, DefaultBoomerangHeight = 8f, DefaultBoomerangOutboundTime = 2f;
        public float BoomerangDuration = DefaultBoomerangDuration, BoomerangWidth = DefaultBoomerangWidth, BoomerangHeight = DefaultBoomerangHeight;
        bool spent, returning;
        float age, returnAge;
        Vector3 launchPoint, launchLocal, forward, right, returnStart, returnControl;
        Vector3 launchVelocity;
        CannonSmokeTrail smoke;
        UnderwaterProjectileTrail waterTrail;
        bool waterEntered, launchedAboveWater;
        float underwaterAge, lifetimeAge;
        int worldBounces;
        public float BoomerangOutboundTime = DefaultBoomerangOutboundTime;
        public static Vector3 ReturnCurve(Vector3 start, Vector3 control, Vector3 target, Vector3 right, float width, float height, float t)
        {
            float u = 1f - t;
            return start * (u * u * u) + control * (3f * u * u * t) +
                (target - right * width + Vector3.up * height) * (3f * u * t * t) + target * (t * t * t);
        }
        readonly HashSet<Transform> hitTargets = new();
        readonly HashSet<ShipDamageSection> hitSections = new();

        void Start()
        {
            launchPoint = transform.position;
            launchLocal = Source != null ? Source.InverseTransformPoint(launchPoint) : launchPoint;
            forward = Vector3.ProjectOnPlane(Velocity, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            right = Vector3.Cross(Vector3.up, forward);
            launchVelocity = Velocity;
            launchedAboveWater = !ProjectileWaterFlight.IsSubmerged(transform.position, Radius);
            if (launchedAboveWater) smoke = CannonSmokeTrail.Create(transform.position);
        }

        void MoveShot(Vector3 position)
        {
            if (smoke != null) smoke.Segment(transform.position, position);
            if (waterTrail != null) waterTrail.Segment(transform.position, position);
            transform.position = position;
        }

        void OnDestroy()
        {
            if (smoke != null) smoke.Finish();
            if (waterTrail != null) waterTrail.Finish();
        }

        Vector3 ReturnPoint => Source != null ? Source.TransformPoint(launchLocal) : launchPoint;

        Vector3 BoomerangStep(float dt)
        {
            age += dt;
            if (!returning && age >= BoomerangOutboundTime)
                BeginReturn(transform.position, launchVelocity.normalized);
            if (returning)
            {
                returnAge += dt;
                float t = Mathf.Clamp01(returnAge / (BoomerangDuration * .5f));
                Vector3 target = ReturnPoint;
                return ReturnCurve(returnStart, returnControl, target, right, BoomerangWidth, BoomerangHeight, t);
            }
            return launchPoint + launchVelocity * age;
        }

        void FixedUpdate()
        {
            if (spent) return;
            float dt = Time.fixedDeltaTime;
            lifetimeAge += dt;
            if (!waterEntered && lifetimeAge >= 20f) { spent = true; Destroy(gameObject); return; }
            if (waterEntered)
            {
                underwaterAge += dt;
                if (underwaterAge >= ProjectileWaterFlight.CannonLifetime) { spent = true; Destroy(gameObject); return; }
            }
            else if (ProjectileWaterFlight.IsSubmerged(transform.position, Radius)) EnterWater(launchedAboveWater);
            bool boomerang = !waterEntered && !MortarShot && Ammo == InventoryItem.BoomerangCannonball;
            bool explosive = MortarShot || Ammo == InventoryItem.FireCannonball;
            float remaining = dt;
            Vector3 boomerangDelta = boomerang ? BoomerangStep(dt) - transform.position : Vector3.zero;
            for (int contact = 0; contact < 6 && !spent && remaining > .0001f; contact++)
            {
                bool submerged = ProjectileWaterFlight.IsSubmerged(transform.position, Radius);
                Vector3 nextVelocity = Velocity;
                Vector3 delta;
                if (boomerang)
                {
                    delta = boomerangDelta;
                    nextVelocity = delta / remaining;
                    transform.Rotate(Vector3.up, 900f * remaining, Space.Self);
                }
                else if (submerged) delta = ProjectileWaterFlight.Step(ref nextVelocity, remaining, ProjectileWaterFlight.CannonDrag);
                else
                {
                    nextVelocity = StepVelocity(Velocity, remaining, Drag);
                    delta = (Velocity + nextVelocity) * (.5f * remaining);
                }
                RaycastHit nearest = default;
                float distance = delta.magnitude;
                var hits = MortarTrajectory.CastHits(transform.position, delta, Radius, out int count);
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (!PlayerHitbox.IsTarget(hit.collider) || hit.collider.gameObject.layer == LayerMask.NameToLayer("Water")) continue;
                    if (hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris")) continue;
                    if (hit.collider.GetComponentInParent<BoardingWalkSurface>() != null) continue;
                    if (hit.transform.IsChildOf(transform) || (Source != null && hit.transform.IsChildOf(Source)) ||
                        hit.collider.GetComponentInParent<CannonShotDamage>() != null) continue;
                    var section = PirateSlop.Ships.ShipV3CollisionBatch.ResolveSection(hit.collider, hit.point);
                    if (section != null && hitSections.Contains(section)) continue;
                    var health = hit.collider.GetComponentInParent<CombatHealth>();
                    var target = health != null ? health.transform : hit.collider.transform;
                    if (hitTargets.Contains(target)) continue;
                    if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
                }
                if (!waterEntered && WaterImpactPhysics.Cross(OceanSurface.Instance, transform.position, transform.position + delta, Radius, out var waterPoint, out float waterFraction) &&
                    (nearest.collider == null || delta.magnitude * waterFraction < nearest.distance))
                {
                    Vector3 incoming = Vector3.Lerp(Velocity, nextVelocity, waterFraction);
                    MoveShot(Vector3.Lerp(transform.position, transform.position + delta, waterFraction) - Vector3.up * .001f);
                    Velocity = incoming;
                    WaterImpactPhysics.Report(waterPoint, incoming, 8f, Radius, WaterImpactKind.Projectile, gameObject);
                    GameAudio.Play(SoundCue.Splash, waterPoint);
                    EnterWater();
                    boomerang = false;
                    remaining *= 1f - waterFraction;
                    continue;
                }
                if (nearest.collider != null)
                {
                    float fraction = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(nearest.distance / delta.magnitude) : 0f;
                    MoveShot(transform.position + delta * fraction);
                    Velocity = Vector3.Lerp(Velocity, nextVelocity, fraction);
                    if (HitSkullMouth(nearest.collider, nearest.point)) return;
                    if (explosive)
                    {
                        ExplodeArea(nearest.point, nearest.normal, nearest.collider);
                        spent = true;
                        Destroy(gameObject);
                        return;
                    }
                    var barricade = nearest.collider.GetComponentInParent<NetworkBarricade>();
                    if (barricade != null && Ammo == InventoryItem.Cannonball)
                    {
                        if (Authoritative) barricade.Hit(this, nearest.point);
                        spent = true;
                        Destroy(gameObject);
                        return;
                    }
                    if (!boomerang && Ammo != InventoryItem.BoardingHook && IsWorldSurface(nearest.collider))
                    {
                        CombatVfx.Impact(nearest.point, nearest.normal, true);
                        GameAudio.Play(SoundCue.Impact, nearest.point);
                        Vector3 normalVelocity = Vector3.Project(Velocity, nearest.normal);
                        Velocity = (Velocity - normalVelocity) * WorldTangentialRetention - normalVelocity * WorldRestitution;
                        MoveShot(transform.position + nearest.normal * .02f);
                        if (++worldBounces >= 8 || Velocity.sqrMagnitude < 16f)
                        {
                            spent = true;
                            Destroy(gameObject);
                            return;
                        }
                        remaining *= 1f - fraction;
                        continue;
                    }
                    Impact(nearest.collider, nearest.point, nearest.normal);
                    if (boomerang && !returning) BeginReturn(nearest.point + nearest.normal * (Radius + .05f), nearest.normal);
                    else if (Ammo == InventoryItem.BoomerangCannonball) MoveShot(transform.position + delta * (1f - fraction));
                    return;
                }
                MoveShot(transform.position + delta);
                Velocity = nextVelocity;
                break;
            }
            if (boomerang && (returning ? returnAge >= BoomerangDuration * .5f : age >= BoomerangDuration))
            {
                spent = true;
                Destroy(gameObject);
            }
        }
        void EnterWater(bool reportImpact = false)
        {
            if (waterEntered) return;
            if (reportImpact)
            {
                Vector3 point = transform.position;
                if (OceanSurface.Instance != null) point.y = OceanSurface.Instance.Height(point);
                WaterImpactPhysics.Report(point, Velocity, 8f, Radius, WaterImpactKind.Projectile, gameObject);
                GameAudio.Play(SoundCue.Splash, point);
            }
            waterEntered = true;
            underwaterAge = 0f;
            if (smoke != null) { smoke.Finish(); smoke = null; }
            waterTrail = UnderwaterProjectileTrail.Create(transform.position, Radius);
        }


        bool HitSkullMouth(Collider collider, Vector3 point)
        {
            if (collider == null) return false;
            var target = collider.GetComponent<SkullMouthTarget>();
            var altar = collider.GetComponentInParent<NetworkSkullEvent>();
            bool accepted = target != null ? target.Hit(this, point) : altar != null && altar.HitMouth(this, point);
            if (!accepted) return false;
            CombatVfx.Impact(point, -Velocity.normalized, true);
            spent = true;
            Destroy(gameObject);
            return true;
        }

        static bool IsWorldSurface(Collider collider) => !collider.isTrigger &&
            collider.GetComponentInParent<ShipController>() == null && collider.GetComponentInParent<NetworkShip>() == null &&
            collider.GetComponentInParent<CombatHealth>() == null && collider.GetComponentInParent<Harpoon.HarpoonGun>() == null &&
            collider.GetComponentInParent<KrakenTentacle>() == null;

        void ExplodeArea(Vector3 point, Vector3 normal, Collider surface)
        {
            CombatVfx.Impact(point, normal, true);
            if (surface == null) { WaterImpactPhysics.Report(point, Velocity, 8f, Radius, WaterImpactKind.Projectile, gameObject); GameAudio.Play(SoundCue.Splash, point); }
            if (!Authoritative) return;
            if (Ammo == InventoryItem.BoardingHook && surface != null && Source != null)
                Source.GetComponent<NetworkCannon>()?.AttachBoarding(SourceCannonIndex, surface.GetComponentInParent<NetworkShip>(), point, normal);
            var damaged = new HashSet<CombatHealth>();
            var ships = new HashSet<NetworkShip>();
            float blastRadius = Ammo == InventoryItem.FireCannonball ? StandardBlastRadius : CannonAmmo.MortarBlastRadius(Ammo);
            foreach (var hit in Physics.OverlapSphere(point, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Source != null && hit.transform.IsChildOf(Source)) continue;
                var health = hit.GetComponentInParent<CombatHealth>();
                if (health != null && damaged.Add(health))
                {
                    health.Damage(Ammo == InventoryItem.Cannonball ? StandardBlastDamage : PlayerDamage, Attacker);
                    if (Ammo == InventoryItem.FireCannonball) health.GetComponent<NetworkHealth>()?.Ignite(Attacker);
                    if (Ammo == InventoryItem.IceCannonball) health.GetComponent<NetworkHealth>()?.Extinguish();
                    if (Ammo == InventoryItem.Cannonball || Ammo == InventoryItem.PushCannonball)
                    {
                        Vector3 direction = Vector3.ProjectOnPlane(health.transform.position - point, Vector3.up).normalized;
                        health.GetComponent<AdvancedPlayerController>()?.ApplyKnockback(direction * PlayerPushSpeed + Vector3.up * 8f);
                    }
                }
                var harpoon = hit.GetComponentInParent<Harpoon.HarpoonGun>();
                if (harpoon != null) harpoon.TakeDamage(Ammo == InventoryItem.Cannonball ? StandardBlastDamage : PlayerDamage, Attacker);
                var tentacle = hit.GetComponentInParent<KrakenTentacle>();
                if (tentacle != null) tentacle.TakeDamage(Ammo == InventoryItem.Cannonball ? StandardBlastDamage : PlayerDamage, Attacker);
                var ship = hit.GetComponentInParent<NetworkShip>();
                if (ship != null && ships.Add(ship))
                {
                    ship.GetComponent<ShipDestruction>()?.Damage(hit, point, normal, Velocity, Ammo, Attacker, CannonAmmo.MortarBlastRadius(Ammo));
                    ship.Motor.ApplyCannonImpulse(point, Velocity.normalized, 1.5f);
                    if (Ammo == InventoryItem.FireCannonball) ship.Ignite(point, Attacker, blastRadius, hit, normal);
                    if (Ammo == InventoryItem.IceCannonball) ship.FreezeFromShot(point);
                    if (Ammo == InventoryItem.PushCannonball) ship.GetComponent<ShipController>()?.ApplyPushImpulse(point, Velocity);
                }
            }
        }

        void BeginReturn(Vector3 point, Vector3 normal)
        {
            returning = true; returnAge = 0f;
            MoveShot(point);
            returnStart = point;
            returnControl = point + normal * 8f + Vector3.up * BoomerangHeight;
        }

        void Impact(Collider collider, Vector3 point, Vector3 normal)
        {
            if (Ammo == InventoryItem.BoardingHook)
            {
                if (Authoritative && Source != null)
                    Source.GetComponent<NetworkCannon>()?.AttachBoarding(SourceCannonIndex, collider.GetComponentInParent<NetworkShip>(), point, normal);
                spent=true; Destroy(gameObject); return;
            }
            var health = collider.GetComponentInParent<CombatHealth>();
            var damageSection = PirateSlop.Ships.ShipV3CollisionBatch.ResolveSection(collider, point);
            if (damageSection != null) hitSections.Add(damageSection);
            hitTargets.Add(health != null ? health.transform : collider.transform);
            var directHarpoon = collider.GetComponentInParent<Harpoon.HarpoonGun>();
            var directTentacle = collider.GetComponentInParent<KrakenTentacle>();
            if (Authoritative)
            {
                if (directHarpoon != null) directHarpoon.TakeDamage(Ammo == InventoryItem.Cannonball ? 60f : 45f, Attacker);
                if (directTentacle != null) directTentacle.TakeDamage(Ammo == InventoryItem.Cannonball ? 60f : 45f, Attacker);
                var ship = collider.GetComponentInParent<ShipController>();
                var network = collider.GetComponentInParent<NetworkShip>();
                if (network != null) network.GetComponent<ShipDestruction>()?.Damage(collider, point, normal, Velocity, Ammo, Attacker);
                if (ship != null)
                {
                    ship.ApplyCannonImpulse(point, Velocity.normalized * Mathf.Clamp(Velocity.magnitude / 40f, .5f, 1.5f), 1.5f);
                    if (Ammo == InventoryItem.PushCannonball) ship.ApplyPushImpulse(point, Velocity);
                    if (Ammo == InventoryItem.IceCannonball)
                    {
                        if (network != null) network.FreezeFromShot(point); else ship.Freeze(5f);
                    }
                }
                if (network != null) network.ImpactVfx(point, normal); else CombatVfx.Impact(point, normal, true, true);
                if (Ammo == InventoryItem.Cannonball)
                {
                    var damaged = new HashSet<CombatHealth>();
                    foreach (var hit in Physics.OverlapSphere(point, StandardBlastRadius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var target = hit.GetComponentInParent<CombatHealth>();
                        if (target != null && damaged.Add(target)) target.Damage(StandardBlastDamage, Attacker);
                        var h = hit.GetComponentInParent<Harpoon.HarpoonGun>();
                        if (h != null && h != directHarpoon) h.TakeDamage(StandardBlastDamage, Attacker);
                        var t = hit.GetComponentInParent<KrakenTentacle>();
                        if (t != null && t != directTentacle) t.TakeDamage(StandardBlastDamage, Attacker);
                    }
                    if (health != null && damaged.Add(health)) health.Damage(StandardBlastDamage, Attacker);
                }
                if (health != null)
                {
                    if (Ammo == InventoryItem.IceCannonball) health.GetComponent<NetworkHealth>()?.Extinguish();
                    if (Ammo == InventoryItem.Cannonball)
                        health.GetComponent<AdvancedPlayerController>()?.ApplyKnockback(Vector3.ProjectOnPlane(Velocity, Vector3.up).normalized * PlayerPushSpeed + Vector3.up * 8f);
                    if (Ammo != InventoryItem.Cannonball) health.Damage(PlayerDamage, Attacker);
                }
            }
            if (Ammo != InventoryItem.BoomerangCannonball) { spent = true; Destroy(gameObject); }
        }
    }
}
