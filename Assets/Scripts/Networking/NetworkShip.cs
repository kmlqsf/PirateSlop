using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
namespace PirateSlop.Networking
{
    public sealed class NetworkShip : NetworkBehaviour
    {
        public readonly SyncVar<int> ParticipantId = new();
        public ShipController Motor { get; private set; }
        public HelmInteraction Helm { get; private set; }
        public Rigidbody Body { get; private set; }
        public float CollisionRadius { get; private set; }
        void Awake()
        {
            Motor = GetComponent<ShipController>(); Motor.Networked = true;
            Helm = GetComponentInChildren<HelmInteraction>(); Helm.Networked = true;
            Body = GetComponent<Rigidbody>(); Body.interpolation = RigidbodyInterpolation.Interpolate;
            float radius = 1f;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                var extent = collider.bounds.extents;
                radius = Mathf.Max(radius, Mathf.Max(extent.x, extent.z) * .85f);
            }
            CollisionRadius = radius;
        }
        public bool AcceptsDriver(NetworkPlayer player) => player != null && player.Ship == this;
    }
}
