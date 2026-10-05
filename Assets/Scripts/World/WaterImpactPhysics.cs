using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public enum WaterImpactKind { Object, Person, Projectile }

    public struct WaterImpactEvent
    {
        public Vector3 Position, Normal, WaterVelocity, Tangent;
        public float Mass, Radius, NormalSpeed, Energy, Strength, Lift, Drift, Life;
        public int Drops;
        public WaterImpactKind Kind;
    }

    [DefaultExecutionOrder(1040), DisallowMultipleComponent]
    public sealed class WaterImpactPhysics : MonoBehaviour
    {
        public static WaterImpactPhysics Instance { get; private set; }
        public int ImpactCount { get; private set; }
        public WaterImpactEvent LastImpact { get; private set; }
        readonly Collider[] nearby = new Collider[512];
        Camera focus;
        float scanAt;
        int scanSector;

        public static WaterImpactPhysics Ensure(GameObject owner)
        {
            if (Instance != null && Instance.isActiveAndEnabled) return Instance;
            var component = owner.GetComponent<WaterImpactPhysics>();
            return component != null ? component : owner.AddComponent<WaterImpactPhysics>();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Instance = this;
            scanAt = 0f;
            WaterBowSpray.Ensure(gameObject);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Time.time < scanAt || OceanSurface.Instance == null) return;
            scanAt = Time.time + .25f;
            if (focus == null || !focus.isActiveAndEnabled) focus = Camera.main;
            if (focus == null) return;
            int sector = scanSector++ % 4;
            Vector3 center = focus.transform.position + new Vector3((sector & 1) == 0 ? -45f : 45f, 0f, (sector & 2) == 0 ? -45f : 45f);
            int count = Physics.OverlapSphereNonAlloc(center, 70f, nearby, ~0, QueryTriggerInteraction.Ignore);
            var found = count < nearby.Length ? nearby : Physics.OverlapSphere(center, 70f, ~0, QueryTriggerInteraction.Ignore);
            if (found != nearby) count = found.Length;
            for (int i = 0; i < count; i++)
            {
                var collider = found[i];
                if (collider == null || collider.GetComponentInParent<CannonShotDamage>() != null) continue;
                var body = collider.attachedRigidbody;
                var motor = collider.GetComponentInParent<AdvancedPlayerController>();
                var item = collider.GetComponentInParent<NetworkFish>();
                var chest = collider.GetComponentInParent<NetworkLootChest>();
                var creature = collider.GetComponentInParent<CombatHealth>();
                Transform owner = motor != null ? motor.transform : item != null ? item.transform : chest != null ? chest.transform : body != null ? body.transform : creature != null ? creature.transform : null;
                if (owner == null || owner.GetComponent<ShipController>() != null) continue;
                if (motor == null && item == null && chest == null && (body == null || body.isKinematic) && owner.GetComponentInParent<ShipController>() != null) continue;
                WaterImpactBody.Ensure(owner.gameObject);
            }
        }

        public static Vector3 SurfaceNormal(OceanSurface ocean, Vector3 point)
        {
            const float step = .25f;
            float x = (ocean.Height(point + Vector3.right * step) - ocean.Height(point - Vector3.right * step)) / (step * 2f);
            float z = (ocean.Height(point + Vector3.forward * step) - ocean.Height(point - Vector3.forward * step)) / (step * 2f);
            return new Vector3(-x, 1f, -z).normalized;
        }

        public static Vector3 SurfaceVelocity(OceanSurface ocean, Vector3 point)
        {
            if (ocean.HeightSource is BoatAttackOcean boat)
            {
                boat.SampleFoamSurface(point, out _, out var velocity, out _);
                return velocity;
            }
            return Vector3.zero;
        }

        public static bool Cross(OceanSurface ocean, Vector3 start, Vector3 end, float radius, out Vector3 point, out float fraction, float previousGap = float.NaN)
        {
            point = end;
            fraction = 1f;
            if (ocean == null || !float.IsFinite((end - start).sqrMagnitude)) return false;
            float gap = float.IsNaN(previousGap) ? start.y - radius - ocean.Height(start) : previousGap;
            if (gap <= 0f) return false;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(start, end) / 1f), 1, 128);
            float low = 0f;
            for (int i = 1; i <= steps; i++)
            {
                float high = i / (float)steps;
                Vector3 sample = Vector3.Lerp(start, end, high);
                if (sample.y - radius > ocean.Height(sample)) { low = high; continue; }
                for (int j = 0; j < 9; j++)
                {
                    float middle = (low + high) * .5f;
                    sample = Vector3.Lerp(start, end, middle);
                    if (sample.y - radius > ocean.Height(sample)) low = middle; else high = middle;
                }
                fraction = high;
                point = Vector3.Lerp(start, end, high);
                point.y = ocean.Height(point);
                return true;
            }
            return false;
        }

        public static WaterImpactEvent Solve(Vector3 position, Vector3 velocity, Vector3 waterVelocity, Vector3 normal, float mass, float radius, WaterImpactKind kind)
        {
            normal = normal.sqrMagnitude > .001f ? normal.normalized : Vector3.up;
            mass = Mathf.Clamp(mass, .01f, 10000f);
            radius = Mathf.Clamp(radius, .025f, 4f);
            Vector3 relative = velocity - waterVelocity;
            float closing = Mathf.Max(0f, -Vector3.Dot(relative, normal));
            Vector3 tangent = Vector3.ProjectOnPlane(relative, normal);
            float area = Mathf.PI * radius * radius;
            float coupledMass = Mathf.Min(mass, 1000f * area * Mathf.Min(radius * 2f, .65f));
            float energy = .5f * coupledMass * (closing * closing + tangent.sqrMagnitude * .12f);
            float intensity = Mathf.Clamp(Mathf.Sqrt(closing / 6f) * Mathf.Pow(mass / 20f, .12f), 0f, 1.8f);
            int drops = closing >= .65f ? Mathf.Clamp(Mathf.RoundToInt((10f + 12f * Mathf.Sqrt(area) + 16f * Mathf.Log10(1f + energy / 20f)) * Mathf.Min(1.4f, intensity)), 3, 150) : 0;
            float lift = Mathf.Clamp(1.2f + Mathf.Sqrt(closing) * 1.2f, 1.5f, 7f);
            float drift = kind == WaterImpactKind.Person ? Mathf.Min(tangent.magnitude * .04f, .6f) : Mathf.Min(tangent.magnitude * .16f, 7f);
            return new WaterImpactEvent {
                Position = position, Normal = normal, WaterVelocity = waterVelocity, Tangent = tangent,
                Mass = mass, Radius = radius, NormalSpeed = closing, Energy = energy, Strength = intensity,
                Lift = lift, Drift = drift, Life = Mathf.Clamp(.6f + lift * .14f, .7f, 1.6f), Drops = drops, Kind = kind
            };
        }

        public static bool Report(Vector3 point, Vector3 incomingVelocity, float mass, float radius, WaterImpactKind kind = WaterImpactKind.Object, GameObject owner = null)
        {
            var ocean = OceanSurface.Instance;
            if (!Application.isPlaying || ocean == null || !float.IsFinite(point.sqrMagnitude) || !float.IsFinite(incomingVelocity.sqrMagnitude)) return false;
            var instance = Ensure(ocean.gameObject);
            point.y = ocean.Height(point);
            var impact = Solve(point, incomingVelocity, SurfaceVelocity(ocean, point), SurfaceNormal(ocean, point), mass, radius, kind);
            if (impact.NormalSpeed < .1f) return false;
            if (owner != null && !WaterImpactBody.Ensure(owner).ConsumeImpact()) return false;
            instance.LastImpact = impact;
            instance.ImpactCount++;
            WaterBowSpray.Ensure(instance.gameObject)?.EmitWaterImpact(impact);
            WaterShipFoam.Instance?.EmitSurfaceImpact(impact);
            return true;
        }
    }
}
