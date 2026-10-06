using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-5)]
    public sealed class NetworkFogBottle : NetworkBehaviour, IWeaponTarget
    {
        public const float ThrowSpeed = 18f;
        public NetworkFogCloud CloudPrefab;
        readonly SyncVar<bool> flying = new();
        public bool Flying => flying.Value;
        NetworkFish pickup;
        NetworkPlayer source;
        Vector3 velocity;
        float expires;
        bool broken;
        void Awake() => pickup = GetComponent<NetworkFish>();
        public override void OnStartServer() { base.OnStartServer(); broken = false; }
        public bool TryBreakFromWeapon(Vector3 point) => Break(point, false);
        public void ReceiveWeaponHit(float damage, GameObject attacker)
        {
            if (damage > 0) TryBreakFromWeapon(transform.position);
        }
        public void Launch(NetworkPlayer thrower, Vector3 speed)
        {
            source = thrower;
            velocity = speed;
            expires = Time.time + 15f;
            flying.Value = true;
            pickup.Place(null, transform.position, transform.rotation);
        }
        void FixedUpdate()
        {
            if (!IsServerInitialized || !Flying) return;
            if (Time.time >= expires) { ServerManager.Despawn(NetworkObject); return; }
            float remaining = Time.fixedDeltaTime;
            while (remaining > .00001f)
            {
                float dt = Mathf.Min(.02f, remaining);
                velocity += Physics.gravity * dt;
                Vector3 delta = velocity * dt;
                float distance = delta.magnitude;
                RaycastHit nearest = default;
                foreach (var hit in Physics.SphereCastAll(transform.position, .12f, delta.normalized, distance, ~0, QueryTriggerInteraction.Collide))
                {
                    if (!PlayerHitbox.IsTarget(hit.collider) || hit.transform.IsChildOf(transform)) continue;
                    if (source != null && hit.transform.IsChildOf(source.transform)) continue;
                    if (hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris")) continue;
                    if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
                }
                Vector3 point = transform.position + delta;
                var ocean = OceanSurface.Instance;
                if (ocean != null && point.y <= ocean.Height(point))
                {
                    float low = 0f, high = 1f;
                    for (int i = 0; i < 8; i++)
                    {
                        float t = (low + high) * .5f;
                        Vector3 sample = transform.position + delta * t;
                        if (sample.y > ocean.Height(sample)) low = t; else high = t;
                    }
                    if (nearest.collider == null || nearest.distance > delta.magnitude * high)
                    {
                        point = transform.position + delta * high;
                        point.y = ocean.Height(point);
                        Break(point, true);
                        return;
                    }
                }
                if (nearest.collider != null)
                {
                    Break(nearest.point, false);
                    return;
                }
                pickup.Place(null, point, transform.rotation * Quaternion.Euler(450f * dt, 100f * dt, 0f));
                remaining -= dt;
            }
        }
        bool Break(Vector3 point, bool water)
        {
            if (!IsServerInitialized || !IsSpawned || broken || CloudPrefab == null) return false;
            broken = true;
            var cloud = Instantiate(CloudPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cloud.gameObject, gameObject.scene);
            cloud.Initialize();
            ServerManager.Spawn(cloud.NetworkObject);
            BreakObserversRpc(point, water, velocity);
            ServerManager.Despawn(NetworkObject);
            return true;
        }
        [ObserversRpc(RunLocally = true)]
        void BreakObserversRpc(Vector3 point, bool water, Vector3 incoming)
        {
            BottleBreakVfx.Present(point, water, incoming, gameObject);
            GameAudio.Play(SoundCue.FogRelease, point);
        }
    }
}

