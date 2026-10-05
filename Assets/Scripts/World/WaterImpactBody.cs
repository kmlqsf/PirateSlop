using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(1030), DisallowMultipleComponent]
    public sealed class WaterImpactBody : MonoBehaviour
    {
        [Min(0f)] public float Mass;
        public WaterImpactKind Kind;
        public Vector3 Velocity { get; private set; }
        Rigidbody body;
        CharacterController character;
        Cannonball ball;
        NetworkFish item;
        Collider[] colliders;
        Camera focus;
        Vector3 previous, motionPosition;
        float previousGap, sampledAt, dryAt, motionAt;
        bool ready, armed, emitted;

        public static WaterImpactBody Ensure(GameObject owner)
        {
            var component = owner.GetComponent<WaterImpactBody>();
            return component != null ? component : owner.AddComponent<WaterImpactBody>();
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            character = GetComponent<CharacterController>();
            ball = GetComponent<Cannonball>();
            item = GetComponent<NetworkFish>();
            colliders = GetComponentsInChildren<Collider>(true);
            if (character != null) { if (Mass <= 0f) Mass = 80f; Kind = WaterImpactKind.Person; }
            else if (Mass <= 0f && GetComponent<NetworkLootChest>() != null) Mass = 45f;
            else if (Mass <= 0f && item != null) Mass = ItemMass(item.CurrentItem);
        }

        static float ItemMass(InventoryItem value)
        {
            if (CannonAmmo.IsBall(value)) return 8f;
            return value switch {
                InventoryItem.Cannon => 160f, InventoryItem.Plank => 4f,
                InventoryItem.Musket or InventoryItem.DoubleBarrel => 4f,
                InventoryItem.Fish => 1f, InventoryItem.Swordfish => 12f,
                InventoryItem.Pufferfish => 2f, InventoryItem.Barricade => 25f,
                InventoryItem.FogBottle or InventoryItem.VortexBottle or InventoryItem.Wine => .7f,
                _ => 1.5f
            };
        }

        void OnEnable() => ResetTracking();
        public void ResetTracking()
        {
            ready = emitted = false;
            armed = false;
            dryAt = -1f;
            Velocity = Vector3.zero;
            motionAt = 0f;
        }

        public bool ConsumeImpact()
        {
            if (emitted && !armed) return false;
            emitted = true;
            armed = false;
            dryAt = -1f;
            return true;
        }

        bool Bounds(out Bounds bounds)
        {
            if (character != null)
            {
                Vector3 scale = character.transform.lossyScale;
                bounds = new Bounds(character.transform.TransformPoint(character.center),
                    new Vector3(character.radius * 2f * Mathf.Abs(scale.x), character.height * Mathf.Abs(scale.y), character.radius * 2f * Mathf.Abs(scale.z)));
                return true;
            }
            bounds = default;
            bool found = false;
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
                if (collider.attachedRigidbody != null && collider.attachedRigidbody != body) continue;
                if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds);
            }
            return found;
        }

        void LateUpdate()
        {
            var ocean = OceanSurface.Instance;
            if (ocean == null) { ready = false; return; }
            if (GetComponent<CannonShotDamage>() != null || (ball != null && (ball.Held || ball.Loaded))) { ResetTracking(); return; }
            if (focus == null || !focus.isActiveAndEnabled) focus = Camera.main;
            if (focus == null || (transform.position - focus.transform.position).sqrMagnitude > 19600f) { ready = false; return; }
            if (!Bounds(out var bounds)) { ready = false; return; }
            Vector3 point = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float gap = point.y - ocean.Height(point);
            float now = Time.time, dt = now - sampledAt;
            if (!ready || dt <= .0001f || dt > .3f || (character != null && Vector3.Distance(previous, point) > 12f))
            {
                previous = motionPosition = point; previousGap = gap; sampledAt = motionAt = now; ready = true;
                armed = gap > .05f; emitted = !armed; dryAt = -1f;
                return;
            }
            if (body != null && !body.isKinematic) Velocity = body.GetPointVelocity(point);
            else if ((point - motionPosition).sqrMagnitude > .000001f)
            {
                Velocity = (point - motionPosition) / Mathf.Max(.001f, now - motionAt);
                motionPosition = point; motionAt = now;
            }
            else if (now - motionAt > .1f) Velocity = Vector3.zero;
            if (gap > .25f)
            {
                if (dryAt < 0f) dryAt = now;
                if (now - dryAt >= .25f) { armed = true; emitted = false; }
            }
            else dryAt = -1f;
            if (armed && WaterImpactPhysics.Cross(ocean, previous, point, 0f, out var contact, out _, previousGap))
            {
                float radius = Mathf.Clamp(Mathf.Sqrt(bounds.extents.x * bounds.extents.z), .025f, 4f);
                float mass = Mass > 0f ? Mass : body != null ? body.mass : 1f;
                WaterImpactPhysics.Report(contact, Velocity, mass, radius, Kind, gameObject);
                armed = false;
            }
            previous = point; previousGap = gap; sampledAt = now;
        }
    }
}
