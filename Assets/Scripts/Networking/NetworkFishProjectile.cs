using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-5)]
    public sealed class NetworkFishProjectile : NetworkBehaviour
    {
        public const float SwordfishSpeed = 38f;
        public const float PufferSpeed = 14f, PufferFuse = 2.5f, PufferRadius = 3f, PufferBounce = .4f;
        readonly SyncVar<bool> flying = new();
        readonly SyncVar<float> burstProgress = new();
        public bool Flying => flying.Value;
        NetworkFish pickup;
        NetworkPlayer attacker;
        Vector3 velocity;
        float fuse, started, visibleFlightAt = -1f;
        Transform[] visualParts;
        Vector3[] visualScales;
        SphereCollider recovery;
        AudioSource warning;
        float nextWarning;
        void StopWarning()
        {
            if (warning != null) warning.Stop();
            nextWarning = 0f;
            visibleFlightAt = -1f;
        }
        void OnDisable() => StopWarning();
        public override void OnStopClient()
        {
            StopWarning();
            base.OnStopClient();
        }
        void LateUpdate()
        {
            if (IsClientInitialized && pickup.Item == InventoryItem.Swordfish)
            {
                if (recovery == null)
                {
                    var root = new GameObject("SwordfishRecovery"); root.transform.SetParent(transform, false);
                    root.transform.localPosition = new Vector3(0,0,-.25f);
                    recovery = root.AddComponent<SphereCollider>(); recovery.isTrigger = true; recovery.radius = .45f;
                }
                recovery.enabled = !Flying;
            }
            if (!IsClientInitialized || pickup.Item != InventoryItem.Pufferfish) return;
            if (visualParts == null)
            {
                var roots = new System.Collections.Generic.HashSet<Transform>();
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                {
                    var part = renderer.transform;
                    while (part.parent != null && part.parent != transform) part = part.parent;
                    if (part != transform) roots.Add(part);
                }
                visualParts = new Transform[roots.Count]; roots.CopyTo(visualParts);
                visualScales = new Vector3[visualParts.Length];
                for (int i=0;i<visualParts.Length;i++) visualScales[i]=visualParts[i].localScale;
            }
            if (!Flying) { StopWarning(); return; }
            if (visibleFlightAt < 0f) visibleFlightAt = Time.time;
            float progress = burstProgress.Value;
            if (Time.time >= nextWarning)
            {
                GameAudio.Attached(ref warning, SoundCue.PufferWarning, transform);
                if (warning != null) warning.pitch = Mathf.Lerp(.9f, 1.45f, progress);
                nextWarning = Time.time + Mathf.Lerp(.5f, .12f, progress);
            }
            float pulse = 1f + progress * .35f + Mathf.Sin((Time.time-visibleFlightAt) * Mathf.Lerp(10f,35f,progress)) * progress * .07f;
            for (int i=0;i<visualParts.Length;i++) if(visualParts[i]!=null && visualParts[i]!=transform) visualParts[i].localScale=visualScales[i]*pulse;
        }
        void Awake() => pickup = GetComponent<NetworkFish>();
        public void Launch(NetworkPlayer source, Vector3 direction)
        {
            attacker = source;
            velocity = direction.normalized * (pickup.Item == InventoryItem.Swordfish ? SwordfishSpeed : PufferSpeed);
            var ship = source.GetComponent<ShipDeckPassenger>()?.Ship;
            if (ship != null) velocity += ship.GetComponent<ShipController>().CannonPointVelocity(transform.position);
            started = Time.time;
            fuse = Time.time + (pickup.Item == InventoryItem.Pufferfish ? PufferFuse : 8f);
            flying.Value = true;
            pickup.Place(null, transform.position, transform.rotation);
        }
        void FixedUpdate()
        {
            if (!IsServerInitialized || !Flying) return;
            if (pickup.Item == InventoryItem.Pufferfish) burstProgress.Value = Mathf.Clamp01(Mathf.Floor((Time.time-started)*10f)/25f);
            if (Time.time >= fuse)
            {
                if (pickup.Item == InventoryItem.Pufferfish) Burst();
                else { flying.Value = false; ServerManager.Despawn(NetworkObject); }
                return;
            }
            velocity += Physics.gravity * Time.fixedDeltaTime;
            Vector3 delta = velocity * Time.fixedDeltaTime;
            RaycastHit nearest = default;
            float distance = delta.magnitude;
            foreach (var hit in Physics.SphereCastAll(transform.position, .12f, delta.normalized, distance, ~0, QueryTriggerInteraction.Collide))
            {
                if (!PlayerHitbox.IsTarget(hit.collider) || hit.transform.IsChildOf(transform)) continue;
                if (attacker != null && hit.transform.IsChildOf(attacker.transform) && Time.time - started < .3f) continue;
                if (hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris")) continue;
                if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
            }
            if (nearest.collider != null)
            {
                if (pickup.Item == InventoryItem.Swordfish)
                {
                    var health = nearest.collider.GetComponentInParent<CombatHealth>();
                    if (health != null)
                    {
                        health.Damage(70f, attacker != null ? attacker.gameObject : null);
                        SoundObserversRpc(SoundCue.BulletFlesh, nearest.point, false);
                        flying.Value = false;
                        ServerManager.Despawn(NetworkObject);
                        return;
                    }
                    var ship = nearest.collider.GetComponentInParent<NetworkShip>();
                    pickup.Place(ship != null ? ship.NetworkObject : null, nearest.point - velocity.normalized * .65f, Quaternion.LookRotation(velocity));
                    flying.Value = false;
                    SoundObserversRpc(SoundCue.SwordfishStick, nearest.point, false);
                    return;
                }
                pickup.Place(null, nearest.point + nearest.normal * .15f, transform.rotation);
                velocity = Vector3.Reflect(velocity, nearest.normal) * PufferBounce;
            }
            else pickup.Place(null, transform.position + delta, Quaternion.LookRotation(velocity));
            var ocean = OceanSurface.Instance;
            if (ocean != null && transform.position.y <= ocean.Height(transform.position))
            {
                Vector3 splashPoint = transform.position;
                foreach (var chest in NetworkLootChest.ServerChests)
                {
                    if (chest != null && chest.IsSpawned && chest.Kind == SeaLootKind.Shark)
                    {
                        if (Vector3.Distance(splashPoint, chest.EventPoint) <= 12f)
                            chest.FeedSharks(pickup != null ? pickup.Item : InventoryItem.Fish, splashPoint);
                    }
                }
                if (pickup.Item == InventoryItem.Pufferfish) Burst();
                else { SoundObserversRpc(SoundCue.Splash, transform.position, false); flying.Value = false; ServerManager.Despawn(NetworkObject); }
            }
        }
        void Burst()
        {
            var damaged = new System.Collections.Generic.HashSet<CombatHealth>();
            foreach (var collider in Physics.OverlapSphere(transform.position, PufferRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var health = collider.GetComponentInParent<CombatHealth>();
                if (health == null || !damaged.Add(health)) continue;
                Vector3 end = collider.ClosestPoint(transform.position);
                if (FirearmTrace.Cast(gameObject, transform.position, end, out var hit) && hit.collider.GetComponentInParent<CombatHealth>() != health) continue;
                health.Damage(40f, attacker != null ? attacker.gameObject : null);
            }
            SoundObserversRpc(SoundCue.PufferBurst, transform.position, true);
            flying.Value = false;
            ServerManager.Despawn(NetworkObject);
        }
        [ObserversRpc(RunLocally = true)]
        void SoundObserversRpc(SoundCue cue, Vector3 point, bool burst)
        {
            GameAudio.Play(cue, point);
            if (burst) CombatVfx.Impact(point, Vector3.up, true);
        }
    }
}
