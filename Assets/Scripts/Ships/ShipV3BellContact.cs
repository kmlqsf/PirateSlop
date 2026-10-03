using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed class ShipV3BellContact : MonoBehaviour
    {
        public ShipV3Features Ship;
        public Rigidbody Clapper;
        void OnCollisionEnter(Collision collision)
        {
            if (collision.rigidbody == Clapper && collision.contactCount > 0)
                Ship.ClapperContact(collision.relativeVelocity.magnitude, collision.GetContact(0).point);
        }
    }
}
