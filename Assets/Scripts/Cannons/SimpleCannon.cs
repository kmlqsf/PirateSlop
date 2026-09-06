using UnityEngine;
using PirateSlop.Networking;
namespace PirateSlop
{
    public sealed class SimpleCannon : MonoBehaviour
    {
        public Transform Muzzle;
        public float LaunchSpeed = 30f;
        public NetworkCannon Network { get; set; }
        Cannonball loaded;
        public bool IsLoaded => loaded != null;
        public bool TryLoad(Cannonball ball)
        {
            if (IsLoaded || ball == null || ball.Loaded || Vector3.Distance(ball.transform.position, Muzzle.position) > .4f) return false;
            loaded = ball; ball.Loaded = true; ball.Held = false; ball.GetComponent<Collider>().enabled = true;
            ball.Body.isKinematic = true;
            ball.AttachToPlatform(GetComponentInParent<Rigidbody>());
            ball.transform.SetParent(Muzzle, true);
            ball.transform.localPosition = Vector3.back * .18f;
            ball.gameObject.SetActive(false);
            return true;
        }
        public void Fire()
        {
            if (Network != null && Network.IsClientInitialized && !Network.IsServerInitialized) { Network.RequestFire(); return; }
            if (!IsLoaded) return;
            var ball = loaded; loaded = null;
            var ship = GetComponentInParent<Rigidbody>();
            Vector3 inherited = ship != null ? ship.GetPointVelocity(Muzzle.position) : Vector3.zero;
            ball.transform.position = Muzzle.position + Muzzle.forward * .35f;
            ball.Loaded = false;
            ball.gameObject.SetActive(true);
            ball.Release();
            ball.Body.linearVelocity = Muzzle.forward * LaunchSpeed + inherited;
            ball.AttachToPlatform(null);
            Destroy(ball.gameObject, 20f);
        }
    }
}
