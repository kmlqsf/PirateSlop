using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-5)]
    public sealed class NetworkHolyGrenade : NetworkBehaviour
    {
        public const float Step = .02f;
        public float FullFlashRadius = 3f;
        public float FlashRadius = 15f;
        readonly SyncVar<bool> armed = new();
        readonly SyncVar<bool> flying = new();
        readonly SyncVar<float> fuseRemaining = new();
        public bool Busy => armed.Value || flying.Value;
        NetworkHolyGrenadeHands source;
        NetworkFish pickup;
        HolyGrenadeFuse fuseVisual;
        Vector3 velocity;
        float deadline, elapsed;
        void Awake() { pickup = GetComponent<NetworkFish>(); fuseVisual = GetComponentInChildren<HolyGrenadeFuse>(); }
        public void Launch(NetworkHolyGrenadeHands thrower, Vector3 speed, float fuse)
        {
            source = thrower;
            velocity = speed;
            flying.Value = true;
            armed.Value = fuse >= 0;
            deadline = Time.time + Mathf.Max(0, fuse);
            fuseRemaining.Value = Mathf.Max(0, fuse);
            pickup.Place(null, transform.position, transform.rotation);
        }
        void Update()
        {
            if (fuseVisual != null) fuseVisual.SetBurn(armed.Value, fuseRemaining.Value / 3f);
            if (!IsServerInitialized || !armed.Value) return;
            fuseRemaining.Value = Mathf.Max(0, deadline - Time.time);
            if (Time.time < deadline) return;
            Explode(source, transform.position, this);
            if (source == null) SoundObserversRpc(transform.position);
            ServerManager.Despawn(NetworkObject);
        }
        void FixedUpdate()
        {
            if (!IsServerInitialized || !flying.Value) return;
            elapsed += Time.fixedDeltaTime;
            if (!armed.Value && elapsed > 30f) { ServerManager.Despawn(NetworkObject); return; }
            float dt = Time.fixedDeltaTime;
            while (dt > .00001f)
            {
                float step = Mathf.Min(Step, dt);
                Vector3 point = transform.position;
                bool hit = Advance(source != null ? source.transform : null, transform, ref point, ref velocity, step, out var support);
                pickup.Place(hit && support != null ? support.NetworkObject : null, point, transform.rotation);
                if (hit) { flying.Value = false; break; }
                dt -= step;
            }
        }
        public static bool Advance(Transform owner, Transform self, ref Vector3 point, ref Vector3 speed, float dt, out NetworkShip support)
        {
            support = null;
            Vector3 delta = speed * dt + Physics.gravity * (.5f * dt * dt);
            speed += Physics.gravity * dt;
            float distance = delta.magnitude;
            RaycastHit nearest = default;
            if (distance > .00001f)
                foreach (var hit in Physics.SphereCastAll(point, .13f, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if ((owner != null && hit.transform.IsChildOf(owner)) || (self != null && hit.transform.IsChildOf(self)) || hit.collider.GetComponentInParent<NetworkFish>() != null) continue;
                    if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
                }
            if (nearest.collider != null)
            {
                point += delta.normalized * Mathf.Max(0, nearest.distance - .005f);
                speed = Vector3.zero;
                support = nearest.collider.GetComponentInParent<NetworkShip>();
                return true;
            }
            point += delta;
            var ocean = OceanSurface.Instance;
            if (ocean != null && point.y < ocean.Height(point))
            {
                point.y = ocean.Height(point);
                speed = Vector3.zero;
                return true;
            }
            return false;
        }
        public static void Explode(NetworkHolyGrenadeHands thrower, Vector3 point, NetworkHolyGrenade settings)
        {
            if (settings == null) return;
            foreach (var player in FindObjectsByType<NetworkHolyGrenadeHands>(FindObjectsSortMode.None))
            {
                if (!player.IsServerInitialized || (player.gameObject.scene != settings.gameObject.scene && thrower == null)) continue;
                if (thrower != null && player.gameObject.scene != thrower.gameObject.scene) continue;
                var motor = player.GetComponent<AdvancedPlayerController>();
                if (motor.IsDead) continue;
                float distance = Vector3.Distance(player.transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.5f), point);
                if (distance >= settings.FlashRadius) continue;
                float strength = 1f - Mathf.InverseLerp(settings.FullFlashRadius, settings.FlashRadius, distance);
                player.Flash(strength * strength);
            }
            if (thrower != null) thrower.BurstSound(point);
        }
        [ObserversRpc(RunLocally = true)]
        void SoundObserversRpc(Vector3 point)
        {
            GameAudio.Play(SoundCue.HolyFlash, point);
            HolyGrenadeFuse.Burst(point, fuseVisual);
        }
    }
}
