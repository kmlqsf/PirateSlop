#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public sealed class UpperDroppedWeapon : NetworkBehaviour
    {
        public Transform Barrel, Traverse;
        public Vector3[] GroundPoints = System.Array.Empty<Vector3>();
        readonly SyncVar<float> elevation = new(3f), traverse = new();
        Quaternion barrelRest, traverseRest;
        bool physical;
        PirateSlop.Networking.NetworkShip supportingShip;
        Rigidbody body;
        float contactAt = -10f, resting;
        uint supportRevision;
        readonly RaycastHit[] groundHits = new RaycastHit[12];
        public static int ActivePhysicalCount { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCount() => ActivePhysicalCount = 0;
        public void BeginFall(Vector3 velocity, PirateSlop.Networking.NetworkShip owner)
        {
            supportingShip = owner;
            GetComponent<PirateSlop.Networking.NetworkFish>().BeginUpperPhysics(velocity);
            body = GetComponent<Rigidbody>();
            if (body == null) return;
            body.angularVelocity = Vector3.zero;
            resting = 0f;
            if (!physical) { physical = true; ActivePhysicalCount++; }
        }
        void OnCollisionStay(Collision collision)
        {
            if (!physical || supportingShip == null || collision.collider.GetComponentInParent<PirateSlop.Networking.NetworkShip>() != supportingShip) return;
            for (int i = 0; i < collision.contactCount; i++)
                if (Vector3.Dot(collision.GetContact(i).normal, supportingShip.transform.up) > .4f) { contactAt = Time.time; break; }
        }
        void FixedUpdate()
        {
            if (!IsServerInitialized || supportingShip == null || !IsSpawned) return;
            var damage = supportingShip.GetComponent<ShipDestruction>();
            if (!physical)
            {
                if (damage == null || supportRevision == damage.Revision) return;
                supportRevision = damage.Revision;
                foreach (var point in GroundPoints)
                {
                    var origin = transform.TransformPoint(point) + supportingShip.transform.up * .12f;
                    var upper = supportingShip.GetComponent<UpperDestructionV2>();
                    if (upper != null)
                    {
                        if (!upper.BlocksRay(origin, origin - supportingShip.transform.up * .35f)) { BeginFall(supportingShip.Motor.CannonPointVelocity(transform.position), supportingShip); break; }
                        continue;
                    }
                    int count = Physics.RaycastNonAlloc(origin, -supportingShip.transform.up, groundHits, .35f, ~0, QueryTriggerInteraction.Ignore);
                    bool supported = false;
                    for (int i = 0; i < count; i++)
                        if (groundHits[i].collider.GetComponentInParent<PirateSlop.Networking.NetworkShip>() == supportingShip) { supported = true; break; }
                    if (!supported) { BeginFall(supportingShip.Motor.CannonPointVelocity(transform.position), supportingShip); break; }
                }
                return;
            }
            var relative = body.linearVelocity - supportingShip.Motor.CannonPointVelocity(body.position);
            if (Time.time - contactAt < .1f && relative.sqrMagnitude < .0225f && body.angularVelocity.sqrMagnitude < .0225f) resting += Time.fixedDeltaTime;
            else resting = 0f;
            if (resting < .75f) return;
            GetComponent<PirateSlop.Networking.NetworkFish>().Place(supportingShip.NetworkObject, body.position, body.rotation);
            physical = false; ActivePhysicalCount = Mathf.Max(0, ActivePhysicalCount - 1);
            if (damage != null) supportRevision = damage.Revision;
        }
        void OnDisable() { if (physical) { physical = false; ActivePhysicalCount = Mathf.Max(0, ActivePhysicalCount - 1); } }
        void Awake()
        {
            if (Barrel != null) barrelRest = Barrel.localRotation;
            if (Traverse != null) traverseRest = Traverse.localRotation;
        }
        public void SetPose(float pitch, float yaw) { elevation.Value = pitch; traverse.Value = yaw; }
        public override void OnStartClient()
        {
            if (Barrel != null) Barrel.localRotation = Quaternion.AngleAxis(3f - elevation.Value, Vector3.right) * barrelRest;
            if (Traverse != null) Traverse.localRotation = Quaternion.AngleAxis(traverse.Value, Vector3.up) * traverseRest;
        }
    }
}
#endif
