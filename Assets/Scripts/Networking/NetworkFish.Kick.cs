using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkFish
    {
        bool kicked;
        Vector3 kickVelocity;
        float kickSupportHeight;
        Bounds kickBounds;
        readonly RaycastHit[] kickHits = new RaycastHit[32];

        public bool Kick(Vector3 incoming)
        {
            if (!IsServerInitialized || !Available || MonkeyCarried || !float.IsFinite(incoming.sqrMagnitude)) return false;
            var loose = GetComponent<NetworkLooseCannonball>();
            if (loose != null)
            {
                if (loose.IsHeld) return false;
                var ball = GetComponent<Cannonball>();
                if (ball == null || ball.Loaded || ball.Held) return false;
                var ship = ball.PlatformBody != null ? ball.PlatformBody.GetComponent<ShipController>() : null;
                Vector3 inherited = ship != null ? ship.CannonPointVelocity(transform.position) : Vector3.zero;
                ball.Release(false);
                ball.Body.linearVelocity = incoming + inherited;
                ball.Body.WakeUp();
                return true;
            }
            kickBounds = LootPlacement.VisualBounds(this);
            resolvedPlatform = SupportingShip;
            kickSupportHeight = resolvedPlatform != null ? resolvedPlatform.transform.InverseTransformPoint(transform.position).y : 0f;
            kickVelocity = resolvedPlatform != null ? resolvedPlatform.transform.InverseTransformDirection(incoming) : incoming;
            airborne = false;
            kicked = true;
            return true;
        }

        void SimulateKick(float dt)
        {
            if (!kicked) return;
            if (!Available || MonkeyCarried) { kicked = false; return; }
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / .02f));
            float step = dt / steps;
            var extents = Vector3.Scale(kickBounds.extents, transform.lossyScale);
            extents = Vector3.Max(extents * .96f, Vector3.one * .015f);
            for (int i = 0; i < steps; i++)
            {
                var support = resolvedPlatform != null ? resolvedPlatform.transform : null;
                kickVelocity += (support != null ? support.InverseTransformDirection(Physics.gravity) : Physics.gravity) * step;
                Vector3 velocity = support != null ? support.TransformDirection(kickVelocity) : kickVelocity;
                Vector3 delta = velocity * step;
                float distance = delta.magnitude;
                if (distance < .00001f) continue;
                int count = Physics.BoxCastNonAlloc(transform.TransformPoint(kickBounds.center), extents, delta / distance,
                    kickHits, transform.rotation, distance + .005f, ~0, QueryTriggerInteraction.Ignore);
                RaycastHit nearest = default;
                float travel = distance + .005f;
                for (int j = 0; j < count; j++)
                {
                    var hit = kickHits[j];
                    if (hit.collider == null || hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.normal.sqrMagnitude < .1f) continue;
                    if (hit.distance < travel) { nearest = hit; travel = hit.distance; }
                }
                float moved = Mathf.Min(distance, Mathf.Max(0f, travel - .005f));
                Vector3 point = transform.position + delta.normalized * moved;
                if (nearest.collider != null)
                {
                    velocity = Vector3.ProjectOnPlane(velocity, nearest.normal);
                    var remainder = Vector3.ProjectOnPlane(delta.normalized * (distance - moved), nearest.normal);
                    if (remainder.sqrMagnitude > .000001f)
                    {
                        float slideDistance = remainder.magnitude;
                        int slideCount = Physics.BoxCastNonAlloc(point + transform.TransformVector(kickBounds.center) + nearest.normal * .006f,
                            extents, remainder / slideDistance, kickHits, transform.rotation, slideDistance, ~0, QueryTriggerInteraction.Ignore);
                        for (int j = 0; j < slideCount; j++)
                        {
                            var hit = kickHits[j];
                            if (hit.collider == null || hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.normal.sqrMagnitude < .1f) continue;
                            slideDistance = Mathf.Min(slideDistance, Mathf.Max(0f, hit.distance - .005f));
                        }
                        point += remainder.normalized * slideDistance;
                    }
                    if (nearest.normal.y > .5f)
                    {
                        velocity *= Mathf.Exp(-7f * step);
                        var floorShip = nearest.collider.GetComponentInParent<NetworkShip>();
                        if (resolvedPlatform != floorShip && floorShip != null) kickSupportHeight = floorShip.transform.InverseTransformPoint(point).y;
                        resolvedPlatform = floorShip;
                        support = floorShip != null ? floorShip.transform : null;
                        if (velocity.sqrMagnitude < .04f)
                        {
                            Place(floorShip != null ? floorShip.NetworkObject : null, point, transform.rotation);
                            kicked = false; nextFlop = Time.time + Random.Range(3f, 6f);
                            return;
                        }
                    }
                }
                if (support != null && support.InverseTransformPoint(point).y < kickSupportHeight - .25f)
                {
                    velocity += resolvedPlatform.Motor.CannonPointVelocity(point);
                    resolvedPlatform = null; support = null;
                }
                var rotation = transform.rotation;
                Place(resolvedPlatform != null ? resolvedPlatform.NetworkObject : null, point, rotation);
                kickVelocity = support != null ? support.InverseTransformDirection(velocity) : velocity;
                var ocean = OceanSurface.Instance;
                if (support == null && ocean != null && point.y < ocean.Height(point) - .3f)
                {
                    kicked = false;
                    if (LivingFish) EnterWater(velocity);
                    else ServerManager.Despawn(NetworkObject);
                    return;
                }
            }
        }
    }
}
