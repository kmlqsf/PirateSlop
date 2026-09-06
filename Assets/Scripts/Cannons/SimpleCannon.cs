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
        Cannonball supply;
        Rigidbody supplyPlatform;
        Vector3 supplyPosition;
        Quaternion supplyRotation;
        void Start()
        {
            if (supply != null) return;
            var ship = GetComponentInParent<ShipController>();
            if (ship != null) InitializeSupply(ship.GetComponentInChildren<Cannonball>(true));
        }
        public void InitializeSupply(Cannonball ball)
        {
            if (supply != null || ball == null) return;
            supply = ball; supplyPlatform = GetComponentInParent<Rigidbody>();
            supplyPosition = supplyPlatform.transform.InverseTransformPoint(ball.transform.position);
            supplyRotation = Quaternion.Inverse(supplyPlatform.rotation) * ball.transform.rotation;
        }
        public void ResetSupply()
        {
            loaded = null;
            if (supply == null) return;
            supply.Loaded = supply.Held = false;
            supply.Body.isKinematic = true;
            supply.transform.SetParent(supplyPlatform.transform, false);
            supply.transform.localPosition = supplyPosition; supply.transform.localRotation = supplyRotation;
            supply.GetComponent<Collider>().enabled = true;
            supply.AttachToPlatform(supplyPlatform); supply.gameObject.SetActive(true);
        }
        public void SpawnShot(Vector3 position, Vector3 velocity, bool authoritative)
        {
            if (supply == null) return;
            var shot = Instantiate(supply, position, Quaternion.identity);
            shot.name = "FiredCannonball"; shot.Network = null; shot.Loaded = shot.Held = false;
            shot.gameObject.SetActive(true); shot.Release();
            shot.GetComponent<Collider>().enabled = authoritative;
            shot.Body.linearVelocity = velocity;
            Destroy(shot.gameObject, 20f);
        }
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
            InitializeSupply(loaded);
            var ship = GetComponentInParent<Rigidbody>();
            Vector3 inherited = ship != null ? ship.GetPointVelocity(Muzzle.position) : Vector3.zero;
            Vector3 position = Muzzle.position + Muzzle.forward * .35f;
            Vector3 velocity = Muzzle.forward * LaunchSpeed + inherited;
            SpawnShot(position, velocity, true);
            ResetSupply();
            if (Network != null && Network.IsServerInitialized) Network.NotifyFired(position, velocity);
        }
    }
}
