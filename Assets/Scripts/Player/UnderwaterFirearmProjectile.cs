using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class FirearmDamageBatch
    {
        readonly GameObject shooter;
        readonly FirearmDefinition definition;
        readonly Dictionary<CombatHealth, float> totals = new();
        readonly Dictionary<IWeaponTarget, float> others = new();
        readonly HashSet<PirateSlop.Ships.ShipMonkey> monkeys = new();
        Dictionary<PirateSlop.World.KamikazeUnit, float> kamikazeDamage;
        public FirearmDamageBatch(GameObject owner, FirearmDefinition firearm) { shooter = owner; definition = firearm; }
        public bool ApplyKamikazeHit(RaycastHit hit, float distance, float damageScale = 1f)
        {
            if (hit.collider == null) return false;
            var box = hit.collider.GetComponent<PirateSlop.World.KamikazeHitbox>();
            if (box == null || box.Unit == null || box.Unit.Event == null) return false;
            if (box.Bomb) { box.Unit.ReceiveHit(2f, shooter); return true; }
            kamikazeDamage ??= new Dictionary<PirateSlop.World.KamikazeUnit, float>();
            kamikazeDamage.TryGetValue(box.Unit, out float previous);
            if (definition.Pellets <= 1)
            {
                if (previous == 0 && damageScale > 0) { kamikazeDamage[box.Unit] = 1f; box.Unit.ReceiveHit(1f, shooter); }
                return true;
            }
            var settings = definition.Ballistics;
            float falloff = Mathf.InverseLerp(settings.FalloffStart, settings.FalloffEnd, distance);
            float amount = Mathf.Min(Mathf.Lerp(settings.NearDamage, settings.FarDamage, falloff), Mathf.Max(0f, definition.DamageCap - previous));
            kamikazeDamage[box.Unit] = previous + amount;
            box.Unit.ReceiveHit(amount * damageScale / Mathf.Max(1f, box.Unit.Event.BodyHitDamage), shooter);
            return true;
        }
        public void Apply(RaycastHit hit, float distance)
        {
            if (hit.collider == null || shooter == null) return;
            if (ApplyKamikazeHit(hit, distance)) return;
            var seagull = hit.collider.GetComponentInParent<PirateSlop.World.SeagullVisual>();
            if (seagull != null) { seagull.ReceiveFirearmHit((hit.point - shooter.transform.position).normalized); return; }
            var shark = hit.collider.GetComponentInParent<SharkHitTarget>();
            if (shark != null) { shark.ReceiveFirearmShot(shooter); return; }
            var monkey = hit.collider.GetComponent<PirateSlop.Ships.ShipMonkeyHitbox>();
            if (monkey != null && monkey.Monkey != null)
            {
                if (monkeys.Add(monkey.Monkey)) monkey.Monkey.ReceiveFirearmShot(shooter);
                return;
            }
            var settings = definition.Ballistics;
            float falloff = Mathf.InverseLerp(settings.FalloffStart, settings.FalloffEnd, distance);
            var health = hit.collider.GetComponentInParent<CombatHealth>();
            if (health != null)
            {
                var body = health.GetComponent<CharacterController>();
                bool head = body != null && health.transform.InverseTransformPoint(hit.point).y >= body.center.y + body.height * .5f - .3f;
                float amount = Mathf.Lerp(head ? settings.NearHeadDamage : settings.NearDamage, head ? settings.FarHeadDamage : settings.FarDamage, falloff);
                totals.TryGetValue(health, out float previous);
                amount = Mathf.Min(amount, Mathf.Max(0f, definition.DamageCap - previous));
                totals[health] = previous + amount;
                if (amount > 0f) health.Damage(amount, shooter);
                return;
            }
            foreach (var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                if (component is IWeaponTarget target)
                {
                    others.TryGetValue(target, out float previous);
                    float amount = Mathf.Min(Mathf.Lerp(settings.NearDamage, settings.FarDamage, falloff), Mathf.Max(0f, definition.DamageCap - previous));
                    others[target] = previous + amount;
                    if (amount > 0f) target.ReceiveWeaponHit(amount, shooter);
                    break;
                }
        }
    }

    public sealed class UnderwaterFirearmProjectile : MonoBehaviour
    {
        static int nextId;
        GameObject shooter;
        NetworkWeapon network;
        FirearmDamageBatch damage;
        FirearmShot shot;
        Vector3 position, velocity;
        float delay, age, distance, bulletDiameter;
        public static void Spawn(GameObject owner, FirearmDefinition definition, FirearmDamageBatch batch, ref FirearmShot result)
        {
            result.ProjectileId = ++nextId;
            var projectile = new GameObject("UnderwaterFirearmProjectile").AddComponent<UnderwaterFirearmProjectile>();
            projectile.shooter = owner;
            projectile.network = owner.GetComponent<NetworkWeapon>();
            projectile.damage = batch;
            projectile.bulletDiameter = definition.BulletDiameter;
            projectile.shot = result;
            projectile.position = result.End;
            projectile.velocity = result.WaterVelocity;
            projectile.distance = Vector3.Distance(result.Start, result.End);
            projectile.delay = Mathf.Clamp(projectile.distance / Mathf.Max(50f, definition.TracerSpeed), 0f, .45f);
            projectile.transform.position = result.End;
        }
        void FixedUpdate()
        {
            if (shooter == null || (network != null && !network.IsServerInitialized)) { Destroy(gameObject); return; }
            float dt = Time.fixedDeltaTime;
            if (delay > 0f)
            {
                float wait = Mathf.Min(delay, dt);
                delay -= wait;
                dt -= wait;
                if (dt <= 0f) return;
            }
            dt = Mathf.Min(dt, ProjectileWaterFlight.BulletLifetime - age);
            Vector3 delta = ProjectileWaterFlight.IsSubmerged(position)
                ? ProjectileWaterFlight.Step(ref velocity, dt, ProjectileWaterFlight.BulletDrag) : velocity * dt;
            if (FirearmTrace.Cast(shooter, position, position + delta, out var hit))
            {
                NetworkShip.PunctureSails(position, hit.point, bulletDiameter);
                distance += hit.distance;
                damage.Apply(hit, distance);
                shot.End = hit.point;
                shot.Normal = hit.normal;
                shot.Water = false;
                shot.Hit = true;
                BulletSurface.Describe(hit, ref shot);
                Finish();
                return;
            }
            NetworkShip.PunctureSails(position, position + delta, bulletDiameter);
            position += delta;
            distance += delta.magnitude;
            age += dt;
            transform.position = position;
            if (age >= ProjectileWaterFlight.BulletLifetime)
            {
                shot.End = position;
                shot.Hit = shot.Water = false;
                Finish();
            }
        }
        void Finish()
        {
            PistolBullet.ResolveWaterImpact(shot);
            if (network != null && network.IsServerInitialized) network.PublishWaterImpact(shot);
            Destroy(gameObject);
        }
    }
}
