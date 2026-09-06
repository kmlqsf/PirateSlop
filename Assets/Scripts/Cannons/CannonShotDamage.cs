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
            var health = collision.collider.GetComponentInParent<CombatHealth>();
            if (health != null && health.IsShip) health.Damage(120f);
            Destroy(gameObject);
        }
    }
}
