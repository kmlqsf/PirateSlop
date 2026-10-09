using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-5)]
    public sealed class NetworkVortexBottle : NetworkBehaviour, IWeaponTarget
    {
        public const float ThrowSpeed = 18f;
        readonly SyncVar<bool> flying = new();
        public bool Flying => flying.Value;
        NetworkFish pickup;
        NetworkPlayer source;
        Vector3 velocity;
        float expires;
        bool broken;
        void Awake() => pickup = GetComponent<NetworkFish>();
        public override void OnStartServer() { base.OnStartServer(); broken = false; }
        public bool TryBreakFromWeapon(Vector3 point) => Break(point, false, pickup.SupportingShip);
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
                Vector3 point = transform.position;
                if (NetworkFogBottle.Advance(source != null ? source.transform : null, transform, ref point, ref velocity, dt, out bool water, out var collider))
                {
                    Break(point, water, collider != null ? collider.GetComponentInParent<NetworkShip>() : null);
                    return;
                }
                pickup.Place(null, point, transform.rotation * Quaternion.Euler(450f * dt, 100f * dt, 0f));
                remaining -= dt;
            }
        }
        bool Break(Vector3 point, bool water, NetworkShip ship = null)
        {
            if (!IsServerInitialized || !IsSpawned || broken) return false;
            broken = true;
            if (!water && ship != null) ship.ApplyVortexBoost();
            BreakObserversRpc(point, water, velocity);
            ServerManager.Despawn(NetworkObject);
            return true;
        }
        [ObserversRpc(RunLocally = true)]
        void BreakObserversRpc(Vector3 point, bool water, Vector3 incoming)
        {
            BottleBreakVfx.Present(point, water, incoming, gameObject);
        }
    }
}
