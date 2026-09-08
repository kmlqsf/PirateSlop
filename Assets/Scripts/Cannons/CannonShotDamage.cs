using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class CannonShotDamage : MonoBehaviour
    {
        public bool Authoritative;
        public Transform Source;
        public GameObject Attacker;
        public Vector3 Velocity;
        public InventoryItem Ammo = InventoryItem.Cannonball;
        public float Radius = .12f, Drag = .015f;
        public static Vector3 StepVelocity(Vector3 velocity, float dt, float drag = .015f) => (velocity + Physics.gravity * dt) * Mathf.Exp(-drag * dt);
        public float PlayerDamage = 45f, PlayerPushSpeed = 22f;
        public float BoomerangDuration = 6f, BoomerangWidth = 16f, BoomerangHeight = 8f;
        bool spent, returning;
        float age, returnAge;
        Vector3 launchPoint, launchLocal, forward, right, returnStart, returnControl;
        Vector3 launchVelocity;
        public float BoomerangOutboundTime = 2f;
        readonly HashSet<Transform> hitTargets = new();

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
                float u = 1f - t;
                Vector3 target = ReturnPoint;
                return returnStart * (u * u * u) + returnControl * (3f * u * u * t) +
                    (target - right * BoomerangWidth + Vector3.up * BoomerangHeight) * (3f * u * t * t) + target * (t * t * t);
            }
            return launchPoint + launchVelocity * age;
        }

        void FixedUpdate()
        {
            if (spent) return;
            float dt = Time.fixedDeltaTime;
            bool boomerang = Ammo == InventoryItem.BoomerangCannonball;
            Vector3 nextVelocity = StepVelocity(Velocity, dt, Drag);
            Vector3 delta = boomerang ? BoomerangStep(dt) - transform.position : (Velocity + nextVelocity) * (.5f * dt);
            if (boomerang)
            {
                nextVelocity = Velocity = delta / dt;
                transform.Rotate(Vector3.up, 900f * dt, Space.Self);
            }
            RaycastHit nearest = default;
            float distance = delta.magnitude;
            foreach (var hit in Physics.SphereCastAll(transform.position, Radius, delta.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
            {
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

        void BeginReturn(Vector3 point, Vector3 normal)
        {
            returning = true; returnAge = 0f;
            transform.position = returnStart = point;
            returnControl = point + normal * 8f + Vector3.up * BoomerangHeight;
        }

        void Impact(Collider collider, Vector3 point, Vector3 normal)
        {
            var health = collider.GetComponentInParent<CombatHealth>();
            hitTargets.Add(health != null ? health.transform : collider.transform);
            if (Authoritative)
            {
                var ship = collider.GetComponentInParent<ShipController>();
                var network = collider.GetComponentInParent<NetworkShip>();
                if (ship != null)
                {
                    ship.ApplyCannonImpulse(point, Velocity.normalized * Mathf.Clamp(Velocity.magnitude / 40f, .5f, 1.5f), 1.5f);
                    if (Ammo == InventoryItem.Cannonball)
                        foreach (var passenger in FindObjectsByType<ShipDeckPassenger>(FindObjectsSortMode.None))
                            if (passenger.Ship == ship.GetComponent<Rigidbody>())
                                passenger.GetComponent<CombatHealth>()?.Damage(5f, Attacker);
                    if (Ammo == InventoryItem.PushCannonball) ship.ApplyPushImpulse(point, Velocity);
                    if (Ammo == InventoryItem.IceCannonball)
                    {
                        if (network != null) network.FreezeFromShot(); else ship.Freeze(5f);
                    }
                    if (Ammo == InventoryItem.FireCannonball && ship.transform != Source) network?.Ignite(point + normal * .1f, Attacker);
                }
                if (network != null) network.ImpactVfx(point, normal); else CombatVfx.Impact(point, normal, true);
                if (health != null)
                {
                    if (Ammo == InventoryItem.Cannonball)
                        health.GetComponent<AdvancedPlayerController>()?.ApplyKnockback(Vector3.ProjectOnPlane(Velocity, Vector3.up).normalized * PlayerPushSpeed + Vector3.up * 8f);
                    health.Damage(PlayerDamage, Attacker);
                }
            }
            if (Ammo != InventoryItem.BoomerangCannonball) { spent = true; Destroy(gameObject); }
        }
    }
}
