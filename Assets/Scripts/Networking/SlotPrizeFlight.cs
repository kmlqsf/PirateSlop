using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class SlotPrizeFlight : MonoBehaviour
    {
        public bool Flying { get; private set; }
        NetworkFish item;
        NetworkShip ship;
        Vector3 start, finish;
        Quaternion facing;
        float began, seconds;
        public void Begin(NetworkFish prize, NetworkShip support, Vector3 from, Vector3 to, Quaternion rotation, float duration)
        {
            item = prize; ship = support; start = support.transform.InverseTransformPoint(from);
            finish = support.transform.InverseTransformPoint(to); facing = Quaternion.Inverse(support.transform.rotation) * rotation;
            began = Time.time; seconds = Mathf.Max(.1f, duration); Flying = true;
        }
        void Update()
        {
            if (!Flying || item == null || !item.IsSpawned || !item.IsServerInitialized) return;
            if (ship == null || !ship.IsSpawned) { Flying = false; Destroy(this); return; }
            float t = Mathf.Clamp01((Time.time - began) / seconds);
            var local = Vector3.Lerp(start, finish, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .22f);
            item.Place(ship.NetworkObject, ship.transform.TransformPoint(local), ship.transform.rotation * facing);
            if (t >= 1f) { Flying = false; Destroy(this); }
        }
    }
}
