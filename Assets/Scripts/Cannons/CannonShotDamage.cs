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
        public const float DefaultBoomerangDuration = 6f, DefaultBoomerangWidth = 16f, DefaultBoomerangHeight = 8f, DefaultBoomerangOutboundTime = 2f;
        public float BoomerangDuration = DefaultBoomerangDuration, BoomerangWidth = DefaultBoomerangWidth, BoomerangHeight = DefaultBoomerangHeight;
        bool spent, returning;
        float age, returnAge;
        Vector3 launchPoint, launchLocal, forward, right, returnStart, returnControl;
        Vector3 launchVelocity;
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
            bool boomerang = !MortarShot && Ammo == InventoryItem.BoomerangCannonball;
            Vector3 nextVelocity = StepVelocity(Velocity, dt, Drag);
            Vector3 delta = boomerang ? BoomerangStep(dt) - transform.position : (Velocity + nextVelocity) * (.5f * dt);
            if (MortarShot || Ammo == InventoryItem.FireCannonball)
            {
                if (MortarTrajectory.Trace(transform.position, delta, Radius, Source, out var point, out var normal, out var collider))
                {
                    ExplodeArea(point, normal, collider);
                    spent = true;
                    Destroy(gameObject);
                }
                else { transform.position += delta; Velocity = nextVelocity; }
                return;
            }
            if (boomerang)
            {
                nextVelocity = Velocity = delta / dt;
                transform.Rotate(Vector3.up, 900f * dt, Space.Self);
            }
            RaycastHit nearest = default;
            float distance = delta.magnitude;
            foreach (var hit in Physics.SphereCastAll(transform.position, Radius, delta.normalized, distance, ~0, QueryTriggerInteraction.Collide))
            {
                if (!PlayerHitbox.IsTarget(hit.collider)) continue;
                if (hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris")) continue;
                var section = hit.collider.GetComponentInParent<ShipDamageSection>();
                if (section != null && hitSections.Contains(section)) continue;
                if (hit.transform.IsChildOf(transform) || (Source != null && hit.transform.IsChildOf(Source)) ||
                    hit.collider.GetComponentInParent<CannonShotDamage>() != null) continue;
                var health = hit.collider.GetComponentInParent<CombatHealth>();
                var target = health != null ? health.transform : hit.collider.transform;
                if (hitTargets.Contains(target)) continue;
                if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
            }
            var ocean = OceanSurface.Instance;
            if (ocean != null && (transform.position + delta).y - Radius <= ocean.Height(transform.position + delta))
            {
                float low = 0f, high = 1f;
                for (int i = 0; i < 8; i++)
                {
                    float t = (low + high) * .5f;
                    var sample = transform.position + delta * t;
                    if (sample.y - Radius > ocean.Height(sample)) low = t; else high = t;
                }
                if (nearest.collider == null || delta.magnitude * high < nearest.distance)
                {
                    var point = transform.position + delta * high;
                    point.y = ocean.Height(point);
                    CombatVfx.Splash(point); GameAudio.Play(SoundCue.Splash, point);
                    if (boomerang && !returning) BeginReturn(point + Vector3.up * (Radius + .05f), Vector3.up);
                    else { spent = true; Destroy(gameObject); }
                    return;
                }
            }
            if (nearest.collider != null)
            {
                Impact(nearest.collider, nearest.point, nearest.normal);
                if (boomerang && !returning) BeginReturn(nearest.point + nearest.normal * (Radius + .05f), nearest.normal);
                else if (boomerang) transform.position += delta;
                return;
            }
            transform.position += delta; Velocity = nextVelocity;
            if (boomerang && (returning ? returnAge >= BoomerangDuration * .5f : age >= BoomerangDuration))
            {
                spent = true;
                Destroy(gameObject);
            }
        }

        void ExplodeArea(Vector3 point, Vector3 normal, Collider surface)
        {
            CombatVfx.Impact(point, normal, true);
            if (surface == null) { CombatVfx.Splash(point); GameAudio.Play(SoundCue.Splash, point); }
            if (!Authoritative) return;
            if (Ammo == InventoryItem.BoardingHook && surface != null && Source != null)
                Source.GetComponent<NetworkCannon>()?.AttachBoarding(SourceCannonIndex, surface.GetComponentInParent<NetworkShip>(), point, normal);
            var damaged = new HashSet<CombatHealth>();
            var ships = new HashSet<NetworkShip>();
            foreach (var hit in Physics.OverlapSphere(point, CannonAmmo.MortarBlastRadius(Ammo), ~0, QueryTriggerInteraction.Ignore))
            {
                if (Source != null && hit.transform.IsChildOf(Source)) continue;
                var health = hit.GetComponentInParent<CombatHealth>();
                if (health != null && damaged.Add(health))
                {
                    health.Damage(Ammo == InventoryItem.Cannonball ? StandardBlastDamage : PlayerDamage, Attacker);
                    if (Ammo == InventoryItem.Cannonball || Ammo == InventoryItem.PushCannonball)
                    {
                        Vector3 direction = Vector3.ProjectOnPlane(health.transform.position - point, Vector3.up).normalized;
                        health.GetComponent<AdvancedPlayerController>()?.ApplyKnockback(direction * PlayerPushSpeed + Vector3.up * 8f);
                    }
                }
                var ship = hit.GetComponentInParent<NetworkShip>();
                if (ship != null && ships.Add(ship))
                {
                    ship.GetComponent<ShipDestruction>()?.Damage(hit, point, normal, Velocity, Ammo, Attacker, CannonAmmo.MortarBlastRadius(Ammo));
                    ship.Motor.ApplyCannonImpulse(point, Velocity.normalized, 1.5f);
                    if (Ammo == InventoryItem.FireCannonball) ship.Ignite(point, Attacker, CannonAmmo.MortarBlastRadius(Ammo));
                    if (Ammo == InventoryItem.IceCannonball) ship.FreezeFromShot();
                    if (Ammo == InventoryItem.PushCannonball) ship.GetComponent<ShipController>()?.ApplyPushImpulse(point, Velocity);
                }
            }
        }

        void BeginReturn(Vector3 point, Vector3 normal)
        {
            returning = true; returnAge = 0f;
            transform.position = returnStart = point;
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
            var damageSection = collider.GetComponentInParent<ShipDamageSection>();
            if (damageSection != null) hitSections.Add(damageSection);
            hitTargets.Add(health != null ? health.transform : collider.transform);
            if (Authoritative)
            {
                var ship = collider.GetComponentInParent<ShipController>();
                var network = collider.GetComponentInParent<NetworkShip>();
                if (network != null) network.GetComponent<ShipDestruction>()?.Damage(collider, point, normal, Velocity, Ammo, Attacker);
                if (ship != null)
                {
                    ship.ApplyCannonImpulse(point, Velocity.normalized * Mathf.Clamp(Velocity.magnitude / 40f, .5f, 1.5f), 1.5f);
                    if (Ammo == InventoryItem.PushCannonball) ship.ApplyPushImpulse(point, Velocity);
                    if (Ammo == InventoryItem.IceCannonball)
                    {
                        if (network != null) network.FreezeFromShot(); else ship.Freeze(5f);
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
                    }
                    if (health != null && damaged.Add(health)) health.Damage(StandardBlastDamage, Attacker);
                }
                if (health != null)
                {
                    if (Ammo == InventoryItem.Cannonball)
                        health.GetComponent<AdvancedPlayerController>()?.ApplyKnockback(Vector3.ProjectOnPlane(Velocity, Vector3.up).normalized * PlayerPushSpeed + Vector3.up * 8f);
                    if (Ammo != InventoryItem.Cannonball) health.Damage(PlayerDamage, Attacker);
                }
            }
            if (Ammo != InventoryItem.BoomerangCannonball) { spent = true; Destroy(gameObject); }
        }
    }
}
