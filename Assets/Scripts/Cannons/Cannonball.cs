using UnityEngine;
namespace PirateSlop
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Cannonball : MonoBehaviour
    {
        public bool Loaded { get; set; }
        public Rigidbody Body => GetComponent<Rigidbody>();
        public Rigidbody PlatformBody { get; set; }
        Vector3 lastPlatformPosition;
        Quaternion lastPlatformRotation;
        Vector3 deckLocalPosition;
        public void AttachToPlatform(Rigidbody platform)
        {
            PlatformBody = platform;
            if (platform != null) { lastPlatformPosition = platform.position; lastPlatformRotation = platform.rotation; deckLocalPosition = platform.transform.InverseTransformPoint(Body.position); }
        }
        void FixedUpdate()
        {
            if (Loaded || PlatformBody == null || !Body.isKinematic) return;
            var local = deckLocalPosition;
            local.x = Mathf.Clamp(local.x, -3.25f, 3.25f);
            local.z = Mathf.Clamp(local.z, -13f, 13f);
            local.y = Mathf.Max(local.y, 5.45f);
            Body.position = PlatformBody.transform.TransformPoint(local);
            Body.rotation = PlatformBody.rotation * Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            lastPlatformPosition = PlatformBody.position; lastPlatformRotation = PlatformBody.rotation;
        }
        public void Release()
        {
            transform.SetParent(null, true);
            AttachToPlatform(null);
            Body.isKinematic = false;
            Body.useGravity = true;
        }
    }
}
