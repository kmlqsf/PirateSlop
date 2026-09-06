using UnityEngine;
namespace PirateSlop
{
    public sealed class CannonShotDamage : MonoBehaviour
    {
        bool spent;
        void OnCollisionEnter(Collision collision)
        {
            if (spent) return;
            spent = true;
            var contact = collision.GetContact(0);
            var network = collision.collider.GetComponentInParent<PirateSlop.Networking.NetworkShip>();
            if (network != null) network.ImpactVfx(contact.point, contact.normal);
            else CombatVfx.Impact(contact.point, contact.normal, true);
            var health = collision.collider.GetComponentInParent<CombatHealth>();
            if (health != null && health.IsShip) health.Damage(120f);
            Destroy(gameObject);
        }
    }
}
