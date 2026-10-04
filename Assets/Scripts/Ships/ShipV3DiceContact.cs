using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed class ShipV3DiceContact : MonoBehaviour
    {
        public ShipV3Features Ship;
        public int Slot;

        void OnCollisionEnter(Collision collision)
        {
            if (Ship == null || !Ship.IsServerInitialized || collision.contactCount == 0) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < .08f) return;
            bool die = collision.collider.GetComponent<ShipV3DiceContact>() != null;
            Ship.PlayDiceSound(Slot, die ? SoundCue.DiceImpact : SoundCue.DiceSlide,
                collision.GetContact(0).point, Mathf.Clamp(speed * .8f, .18f, 1f));
        }

        void OnCollisionStay(Collision collision)
        {
            if (Ship == null || !Ship.IsServerInitialized || collision.contactCount == 0 ||
                collision.collider.GetComponent<ShipV3DiceContact>() != null) return;
            var contact = collision.GetContact(0);
            float speed = Vector3.ProjectOnPlane(collision.relativeVelocity, contact.normal).magnitude;
            if (speed > .08f) Ship.PlayDiceSound(Slot, SoundCue.DiceSlide, contact.point, Mathf.Clamp01(speed));
        }
    }
}
