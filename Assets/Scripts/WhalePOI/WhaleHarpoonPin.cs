using UnityEngine;
using PirateSlop.World;
namespace PirateSlop
{
    public sealed class WhaleHarpoonPin : MonoBehaviour
    {
        public WhaleLootPoint Owner;
        public int Index;
        public Transform Grip;
        public Transform RopeEye;
        public Vector3 LocalOutward;
        Vector3 restPosition;
        Quaternion restRotation;
        float rock, pull;
        bool removed, splashed;
        Collider hitbox;
        public const float ReachDistance = 6f;
        public bool IsRemoved => Owner != null && Owner.Work(Index).Removed;
        public float Progress => Owner == null ? 0 : Owner.Work(Index).Strokes / 12f + Owner.Work(Index).Pull * .5f;
        public Vector3 Outward => Owner.transform.TransformDirection(LocalOutward).normalized;
        public Vector3 EmbeddedPoint => Owner.transform.TransformPoint(restPosition);
        public Vector3 GripPoint => Grip != null ? Grip.position : transform.position;
        public Vector3 RopePoint => RopeEye != null ? RopeEye.position : GripPoint;
        public float ShaftDistance(Vector3 origin)
        {
            var start=transform.TransformPoint(Vector3.forward*.75f);
            var shaft=transform.TransformPoint(Vector3.forward*2.6f)-start;
            float along=shaft.sqrMagnitude>.0001f ? Mathf.Clamp01(Vector3.Dot(origin-start,shaft)/shaft.sqrMagnitude) : 0;
            return Vector3.Distance(origin,start+shaft*along);
        }
        public bool Aim(Ray ray, float distance, out float range)
        {
            var start = transform.TransformPoint(Vector3.forward * .75f);
            var shaft = transform.TransformPoint(Vector3.forward * 2.55f) - start;
            var offset = ray.origin - start;
            float projection = Vector3.Dot(ray.direction, shaft);
            float denominator = shaft.sqrMagnitude - projection * projection;
            float along = denominator > .0001f ? Mathf.Clamp01((Vector3.Dot(shaft, offset) - projection * Vector3.Dot(ray.direction, offset)) / denominator) : Mathf.Clamp01(Vector3.Dot(shaft,offset)/Mathf.Max(.0001f,shaft.sqrMagnitude));
            var point = start + shaft * along;
            range = Vector3.Dot(point - ray.origin, ray.direction);
            return range >= 0 && range <= distance && (point - ray.GetPoint(range)).sqrMagnitude <= .38f * .38f;
        }
        public static Vector3 ReachOrigin(Networking.NetworkWeapon actor)
        {
            var motor = actor.GetComponent<AdvancedPlayerController>();
            return actor.transform.position + Vector3.up * Mathf.Lerp(1.65f, .75f, motor.CrouchBlend);
        }
        public bool CanReach(Networking.NetworkWeapon actor) => CanReach(actor, out _);
        public bool CanReach(Networking.NetworkWeapon actor, out string reason)
        {
            reason = "Подойди ближе к гарпуну";
            if (actor == null || Owner == null) return false;
            var origin = ReachOrigin(actor);
            var support = actor.GetComponent<ShipDeckPassenger>()?.Support;
            bool onWhale = support != null && support.GetComponent<WhaleLootPoint>() == Owner || Vector3.Dot(origin-EmbeddedPoint,Owner.transform.up)>.25f;
            for (int i = 0; i < 5; i++)
            {
                var target = transform.TransformPoint(Vector3.forward * (1f + i * .38f));
                var delta = target - origin;
                if (delta.sqrMagnitude > ReachDistance * ReachDistance) continue;
                reason = "Гарпун закрыт препятствием — обойди его";
                if (delta.sqrMagnitude < .01f) { reason = null; return true; }
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(actor.transform) || hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null) continue;
                    if (onWhale && hit.collider.GetComponentInParent<WhaleLootPoint>() == Owner) continue;
                    if (hit.distance < delta.magnitude - .12f) { blocked = true; break; }
                }
                if (!blocked) { reason = null; return true; }
            }
            return false;
        }
        void Awake() { restPosition = transform.localPosition; restRotation = transform.localRotation; hitbox = GetComponent<Collider>(); }
        void LateUpdate()
        {
            if (Owner == null || IsRemoved)
            {
                if (!removed && IsRemoved) { removed = true; hitbox.enabled = false; GameAudio.Play(SoundCue.BulletFlesh, transform.position); }
                float surface = OceanSurface.Instance != null ? OceanSurface.Instance.Height(transform.position) : Owner != null ? Owner.SeaLevel : 0;
                if (Owner != null && removed && !splashed && transform.position.y <= surface + .25f) { splashed = true; GameAudio.Play(SoundCue.WaterSplash, transform.position); }
                return;
            }
            var s = Owner.Work(Index);
            rock = Mathf.Lerp(rock, s.Actor != null ? s.Rock : 0, 1 - Mathf.Exp(-16 * Time.deltaTime));
            pull = Mathf.Lerp(pull, s.Pull, 1 - Mathf.Exp(-12 * Time.deltaTime));
            transform.localPosition = restPosition + LocalOutward * (pull * 1.15f + s.Strokes * .018f);
            transform.localRotation = Quaternion.AngleAxis(rock * (8 + s.Strokes * 1.3f), Vector3.up) * restRotation;
        }
    }
}

