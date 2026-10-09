using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkHandMortarBall : NetworkBehaviour
    {
        readonly SyncVar<Vector3> point = new();
        readonly SyncVar<float> fuseFraction = new(1f);
        public float RemainingFuseFraction => fuseFraction.Value;
        NetworkPlayer shooter;
        HandMortarSettings settings;
        Vector3 velocity, restingLocal;
        Collider restingSurface;
        NetworkShip support;
        float age;
        bool spent, waterEntered;

        public void Launch(NetworkPlayer source, Vector3 speed, HandMortarSettings configuration)
        {
            shooter = source;
            velocity = speed;
            settings = configuration;
            point.Value = transform.position;
            fuseFraction.Value = 1f;
            waterEntered = ProjectileWaterFlight.IsSubmerged(transform.position, settings.BallRadius);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized) transform.position = point.Value;
        }

        void Update()
        {
            if (IsSpawned && !IsServerInitialized) transform.position = Vector3.Lerp(transform.position, point.Value, 1f - Mathf.Exp(-Time.deltaTime * 25f));
        }

        void FixedUpdate()
        {
            if (!IsServerInitialized || spent || settings == null) return;
            age += Time.fixedDeltaTime;
            fuseFraction.Value = Mathf.Clamp01(1f - Mathf.Floor(age * 10f) / (settings.FuseSeconds * 10f));
            if (settings.ExplodeOnWaterEntry && ProjectileWaterFlight.IsSubmerged(transform.position, settings.BallRadius)) { Explode(null); return; }
            if (restingSurface != null && restingSurface.enabled && restingSurface.gameObject.activeInHierarchy)
            {
                if (support != null) { transform.position = support.transform.TransformPoint(restingLocal); velocity = support.GetComponent<ShipController>().CannonPointVelocity(transform.position); }
            }
            else
            {
                restingSurface = null;
                support = null;
                float remaining = Time.fixedDeltaTime;
                for (int contact = 0; contact < 6 && remaining > .0001f && !spent; contact++) Move(ref remaining);
            }
            point.Value = transform.position;
            if (!spent && age >= settings.FuseSeconds) Explode(null);
        }

        void Move(ref float remaining)
        {
            Vector3 start = transform.position;
            Vector3 next = velocity;
            Vector3 delta;
            if (ProjectileWaterFlight.IsSubmerged(start, settings.BallRadius)) delta = ProjectileWaterFlight.Step(ref next, remaining, ProjectileWaterFlight.CannonDrag);
            else { next += Physics.gravity * remaining; delta = (velocity + next) * (.5f * remaining); }
            if (delta.sqrMagnitude < .00000001f) { remaining = 0; return; }
            RaycastHit nearest = default;
            float distance = delta.magnitude;
            var hits = MortarTrajectory.CastHits(start, delta, settings.BallRadius, out int count);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!PlayerHitbox.IsTarget(hit.collider) || hit.collider.gameObject.layer == 4 || hit.collider.gameObject.scene != gameObject.scene) continue;
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<NetworkHandMortarBall>() != null || age < settings.OwnerGraceSeconds && shooter != null && hit.transform.IsChildOf(shooter.transform)) continue;
                if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
            }
            if (!waterEntered && WaterImpactPhysics.Cross(OceanSurface.Instance, start, start + delta, settings.BallRadius, out var waterPoint, out float waterFraction) && (nearest.collider == null || delta.magnitude * waterFraction < nearest.distance))
            {
                velocity = Vector3.Lerp(velocity, next, waterFraction);
                transform.position = start + delta * waterFraction - Vector3.up * .001f;
                waterEntered = true;
                WaterObserversRpc(waterPoint, velocity);
                if (settings.ExplodeOnWaterEntry) { Explode(null); remaining = 0; return; }
                remaining *= 1f - waterFraction;
                return;
            }
            if (nearest.collider == null) { transform.position += delta; velocity = next; remaining = 0; return; }
            float fraction = Mathf.Clamp01(nearest.distance / delta.magnitude);
            Vector3 normal = nearest.normal.sqrMagnitude > .5f ? nearest.normal.normalized : -delta.normalized;
            transform.position = start + delta * fraction + normal * .004f;
            velocity = Vector3.Lerp(velocity, next, fraction);
            var health = nearest.collider.GetComponentInParent<CombatHealth>();
            if (settings.ExplodeOnLivingHit && health != null && health.GetComponent<NetworkPlayer>() != null && !health.IsDead) { Explode(health); remaining = 0; return; }
            var ship = nearest.collider.GetComponentInParent<NetworkShip>();
            Vector3 platformVelocity = ship != null ? ship.GetComponent<ShipController>().CannonPointVelocity(transform.position) : Vector3.zero;
            Vector3 relative = velocity - platformVelocity;
            float impactSpeed = Mathf.Max(0, -Vector3.Dot(relative, normal));
            Vector3 perpendicular = Vector3.Project(relative, normal);
            relative = (relative - perpendicular) * settings.TangentialRetention - perpendicular * settings.Restitution;
            velocity = relative + platformVelocity;
            if (impactSpeed > 1f) BounceObserversRpc(transform.position);
            if (normal.y > .55f && relative.sqrMagnitude < 2.25f)
            {
                restingSurface = nearest.collider;
                support = ship;
                restingLocal = ship != null ? ship.transform.InverseTransformPoint(transform.position) : transform.position;
                velocity = Vector3.zero;
                remaining = 0;
                return;
            }
            remaining *= 1f - Mathf.Max(fraction, .05f);
        }

        void Explode(CombatHealth direct)
        {
            if (spent) return;
            spent = true;
            var damaged = new HashSet<CombatHealth>();
            if (direct != null && direct.GetComponent<NetworkPlayer>() != null) { damaged.Add(direct); direct.Damage(settings.DirectDamage, shooter != null ? shooter.gameObject : null); }
            foreach (var hit in Physics.OverlapSphere(transform.position, settings.BlastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.gameObject.scene != gameObject.scene || hit.transform.IsChildOf(transform)) continue;
                var health = hit.GetComponentInParent<CombatHealth>();
                if (health != null && health.GetComponent<NetworkPlayer>() != null && damaged.Add(health))
                {
                    float distance = Vector3.Distance(transform.position, hit.bounds.ClosestPoint(transform.position));
                    if (ClearBlast(hit, hit.bounds.center)) health.Damage(settings.BlastDamage * (settings.BlastFalloff ? Mathf.Clamp01(1f - distance / settings.BlastRadius) : 1f), shooter != null ? shooter.gameObject : null);
                }
            }
            ExplosionObserversRpc(transform.position);
            ServerManager.Despawn(NetworkObject);
        }

        bool ClearBlast(Collider target, Vector3 destination)
        {
            Vector3 origin = transform.position;
            Vector3 delta = destination - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.gameObject.layer == 4) continue;
                var health = target.GetComponentInParent<CombatHealth>();
                if (hit.collider == target || health != null && hit.transform.IsChildOf(health.transform)) continue;
                return false;
            }
            return true;
        }

        [ObserversRpc(RunLocally = true)] void BounceObserversRpc(Vector3 position) => GameAudio.Play(SoundCue.Impact, position);
        [ObserversRpc(RunLocally = true)] void ExplosionObserversRpc(Vector3 position) { CombatVfx.Impact(position, Vector3.up, true); GameAudio.Play(SoundCue.Cannon, position); }
        [ObserversRpc(RunLocally = true)] void WaterObserversRpc(Vector3 position, Vector3 speed) => WaterImpactPhysics.Report(position, speed, 3f, .09f, WaterImpactKind.Projectile, gameObject);
    }
}
