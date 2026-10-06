using UnityEngine;

namespace PirateSlop
{
    public static class ProjectileWaterFlight
    {
        public const float BulletLifetime = 3f, CannonLifetime = 6f, HarpoonLifetime = 4.5f;
        public const float BulletDrag = .35f, CannonDrag = .08f, HarpoonDrag = .1f;
        public static bool IsSubmerged(Vector3 point, float radius = 0f)
        {
            var ocean = OceanSurface.Instance;
            return ocean != null && point.y - radius <= ocean.Height(point);
        }
        public static Vector3 StepVelocity(Vector3 velocity, float dt, float drag)
        {
            velocity += Physics.gravity * (.18f * dt);
            return velocity / (1f + drag * velocity.magnitude * dt);
        }
        public static Vector3 Step(ref Vector3 velocity, float dt, float drag)
        {
            Vector3 middle = StepVelocity(velocity, dt * .5f, drag);
            velocity = StepVelocity(middle, dt * .5f, drag);
            return middle * dt;
        }
    }
}
