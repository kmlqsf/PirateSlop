using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-5)]
    public sealed class NetworkVortexBottle : NetworkBehaviour
    {
        public const float ThrowSpeed = 18f;
        readonly SyncVar<bool> flying = new();
        public bool Flying => flying.Value;
        NetworkFish pickup;
        NetworkPlayer source;
        Vector3 velocity;
        float expires;
        void Awake() => pickup = GetComponent<NetworkFish>();
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
                if (nearest.collider != null)
                {
                    var ship = nearest.collider.GetComponentInParent<NetworkShip>();
                    if (ship != null) ship.ApplyVortexBoost();
                    Break(nearest.point, false);
                    return;
                }
                Vector3 point = transform.position + delta;
                var ocean = OceanSurface.Instance;
                if (ocean != null && point.y <= ocean.Height(point))
                {
                    point.y = ocean.Height(point);
                    Break(point, true);
                    return;
                }
                pickup.Place(null, point, transform.rotation * Quaternion.Euler(450f * dt, 100f * dt, 0f));
                remaining -= dt;
            }
        }
        void Break(Vector3 point, bool water)
        {
            BreakObserversRpc(point, water);
            ServerManager.Despawn(NetworkObject);
        }
        [ObserversRpc(RunLocally = true)]
        void BreakObserversRpc(Vector3 point, bool water)
        {
            GameAudio.Play(water ? SoundCue.WaterSplash : SoundCue.BottleClose, point);
            CombatVfx.Splash(point, .5f);
        }
    }
}
