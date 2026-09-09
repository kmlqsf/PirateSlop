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
        public Vector3 Start, End, Normal;
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
            foreach (var hit in Physics.RaycastAll(origin, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (shooter != null && hit.transform.IsChildOf(shooter.transform)) continue;
                if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
            }
            return nearest.collider != null;
        }

        public static FirearmShot Resolve(GameObject shooter, Vector3 eye, Vector3 muzzle, Vector3 direction, float range, out RaycastHit hit)
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
            var ocean = OceanSurface.Instance;
            if (ocean != null)
            {
                Vector3 delta = shot.End - shot.Start;
                int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .5f));
                float previous = 0f;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector3 point = shot.Start + delta * t;
                    if (point.y <= ocean.Height(point))
                    {
                        float low = previous, high = t;
                        for (int j = 0; j < 8; j++)
                        {
                            float middle = (low + high) * .5f;
                            Vector3 sample = shot.Start + delta * middle;
                            if (sample.y > ocean.Height(sample)) low = middle; else high = middle;
                        }
                        shot.End = shot.Start + delta * high;
                        shot.Normal = Vector3.up; shot.Hit = false; shot.Water = true;
                        hit = default;
                        break;
                    }
                    previous = t;
                }
            }
            if(hit.collider!=null && !shot.Water) BulletSurface.Describe(hit,ref shot);
            return shot;
        }
    }
}
