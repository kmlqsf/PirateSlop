#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkFish
    {
        bool upperPhysics;
        Rigidbody upperBody;

        public void BeginUpperPhysics(Vector3 velocity)
        {
            if (!IsServerInitialized) return;
            upperBody = GetComponent<Rigidbody>();
            if (upperBody == null) return;
            platform.Value = null; platformId.Value = 0;
            kicked = false; airborne = false; upperPhysics = true;
            upperBody.isKinematic = false; upperBody.useGravity = true;
            upperBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            upperBody.constraints = RigidbodyConstraints.None;
            upperBody.linearVelocity = velocity;
            upperBody.angularDamping = 1f;
            upperBody.linearDamping = .12f;
            upperBody.maxDepenetrationVelocity = 2f;
            upperBody.WakeUp();
        }

        bool TickUpperPhysics()
        {
            if (!upperPhysics || upperBody == null) return false;
            worldPosition.Value = upperBody.position;
            position.Value = upperBody.position;
            rotation.Value = upperBody.rotation;
            return true;
        }
    }
}
#endif
