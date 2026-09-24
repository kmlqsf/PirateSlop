using UnityEngine;

namespace PirateSlop
{
    public sealed class PlayerHitbox : MonoBehaviour
    {
        CharacterController body;
        CombatHealth health;
        CapsuleCollider shape;
        void Awake()
        {
            body = GetComponentInParent<CharacterController>();
            health = GetComponentInParent<CombatHealth>();
            shape = GetComponent<CapsuleCollider>();
        }
        void LateUpdate()
        {
            if (body == null || health == null) return;
            shape.enabled = !health.IsDead;
            shape.center = body.center;
            shape.radius = body.radius * 1.65f;
            shape.height = Mathf.Max(body.height + .2f, shape.radius * 2f);
        }
        public static bool IsTarget(Collider collider) => !collider.isTrigger || collider.GetComponent<PlayerHitbox>() != null || collider.GetComponentInParent<KrakenTentacle>() != null;
    }
}
