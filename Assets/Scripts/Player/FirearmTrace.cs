using UnityEngine;

namespace PirateSlop
{
    [System.Serializable]
    public sealed class FirearmSettings
    {
        public float Range = 100f;
        public float NearDamage = 45f, FarDamage = 25f, NearHeadDamage = 70f, FarHeadDamage = 40f;
        public float FalloffStart = 15f, FalloffEnd = 35f;
        public float ShotInterval = .25f, ReloadDuration = 3f;
    }

    public struct FirearmShot
    {
        public Vector3 Start, End, Normal, WaterVelocity;
        public int ProjectileId;
        public bool Hit, Water;
        public BulletSurfaceKind Surface;
        public bool LeaveMark;
        public FishNet.Object.NetworkObject Anchor;
        public int ShipId;
        public Vector3 LocalEnd, LocalNormal;
    }

    public static class FirearmTrace
    {
        public static bool Cast(GameObject shooter, Vector3 origin, Vector3 end, out RaycastHit nearest)
        {
            nearest = default;
            Vector3 delta = end - origin;
            float distance = delta.magnitude;
            if (distance < .0001f) return false;
            foreach (var hit in Physics.RaycastAll(origin, delta / distance, distance, ~0, QueryTriggerInteraction.Collide))
            {
                if (!PlayerHitbox.IsTarget(hit.collider) || hit.collider.gameObject.layer == LayerMask.NameToLayer("Water")) continue;
                if (shooter != null && hit.transform.IsChildOf(shooter.transform)) continue;
                if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
            }
            return nearest.collider != null;
        }

        public static FirearmShot Resolve(GameObject shooter, Vector3 eye, Vector3 muzzle, Vector3 direction, float range, out RaycastHit hit, float speed = 450f)
        {
            direction.Normalize();
            Vector3 target = eye + direction * Mathf.Max(1f, range);
            bool cameraHit = Cast(shooter, eye, target, out hit);
            if (cameraHit) target = hit.point;
            Vector3 start = muzzle;
            if (Cast(shooter, eye, muzzle, out var obstruction))
            {
                start = eye;
                hit = obstruction;
                target = obstruction.point;
                cameraHit = true;
            }
            else if (Cast(shooter, muzzle, target + direction * .002f, out var muzzleHit))
            {
                hit = muzzleHit;
                target = muzzleHit.point;
                cameraHit = true;
            }
            var shot = new FirearmShot { Start = start, End = target, Normal = cameraHit ? hit.normal : -direction, Hit = cameraHit };
            Vector3 flightDirection = (target - start).sqrMagnitude > .000001f ? (target - start).normalized : direction;
            if (ProjectileWaterFlight.IsSubmerged(start))
            {
                shot.End = start;
                shot.Normal = Vector3.up;
                shot.Hit = false;
                shot.Water = true;
                hit = default;
            }
            else if (WaterImpactPhysics.Cross(OceanSurface.Instance, start, target, 0f, out var waterPoint, out _))
            {
                shot.End = waterPoint;
                shot.Normal = Vector3.up;
                shot.Hit = false;
                shot.Water = true;
                hit = default;
            }
            if (shot.Water) shot.WaterVelocity = flightDirection * Mathf.Max(50f, speed);
            else if (hit.collider != null) BulletSurface.Describe(hit, ref shot);
            return shot;
        }

    }
}
