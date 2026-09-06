using UnityEngine;
using PirateSlop.Networking;
namespace PirateSlop
{
    public sealed class SimpleCannon : MonoBehaviour
    {
        public Transform Muzzle;
        public float LaunchSpeed = 30f;
        public NetworkCannon Network { get; set; }
        public CannonballCrate Crate { get; set; }
        public int Index { get; set; }
        Cannonball loaded;
        Cannonball supply;
        Rigidbody supplyPlatform;
        Vector3 supplyPosition;
        Quaternion supplyRotation;
        float nextFireTime;
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
            var anchor = Crate != null ? Crate.SpawnPoint : ball.transform;
            supplyPosition = supplyPlatform.transform.InverseTransformPoint(anchor.position);
            supplyRotation = Quaternion.Inverse(supplyPlatform.rotation) * anchor.rotation;
        }
        public void ResetSupply()
        {
            loaded = null;
            if (Crate != null) { Crate.ResetSupply(); return; }
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
            if (authoritative)
            {
                shot.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                shot.gameObject.AddComponent<CannonShotDamage>();
                var owner = GetComponentInParent<ShipController>();
                if (owner != null)
                    foreach (var collider in owner.GetComponentsInChildren<Collider>())
                        Physics.IgnoreCollision(shot.GetComponent<Collider>(), collider);
            }
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
            if (Network != null && Network.IsClientInitialized && !Network.IsServerInitialized) { Network.RequestFire(Index); return; }
            if (!IsLoaded || Time.time < nextFireTime) return;
            nextFireTime = Time.time + 6f;
            InitializeSupply(loaded);
            var ship = GetComponentInParent<Rigidbody>();
            Vector3 inherited = ship != null ? ship.GetPointVelocity(Muzzle.position) : Vector3.zero;
            Vector3 position = Muzzle.position + Muzzle.forward * .35f;
            Vector3 velocity = Muzzle.forward * LaunchSpeed + inherited;
            SpawnShot(position, velocity, true);
            ResetSupply();
            if (Network != null && Network.IsServerInitialized) Network.NotifyFired(Index, position, velocity);
        }
    }
}
