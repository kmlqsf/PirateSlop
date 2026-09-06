using UnityEngine;
namespace PirateSlop
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Cannonball : MonoBehaviour
    {
        public bool Loaded { get; set; }
        public bool Held { get; set; }
        public PirateSlop.Networking.NetworkCannon Network { get; set; }
        public Rigidbody Body => GetComponent<Rigidbody>();
        public Rigidbody PlatformBody { get; set; }
        Vector3 lastPlatformPosition;
        Quaternion lastPlatformRotation;
        Vector3 deckLocalPosition;
        Quaternion deckLocalRotation;
        public void AttachToPlatform(Rigidbody platform)
        {
            PlatformBody = platform;
            if (platform != null && platform != Body) { deckLocalPosition = platform.transform.InverseTransformPoint(transform.position); deckLocalRotation = Quaternion.Inverse(platform.rotation) * transform.rotation; }
            else PlatformBody = null;
        }
        void FixedUpdate()
        {
            if (Held || Loaded || PlatformBody == null || !Body.isKinematic) return;
            Body.position = PlatformBody.transform.TransformPoint(deckLocalPosition);
            Body.rotation = PlatformBody.rotation * deckLocalRotation;
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
