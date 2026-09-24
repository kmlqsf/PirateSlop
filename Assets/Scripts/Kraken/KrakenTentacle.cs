using System;
using UnityEngine;

namespace PirateSlop
{
    [SelectionBase]
    public sealed class KrakenTentacle : MonoBehaviour, IWeaponTarget
    {
        [SerializeField] TentacleAnimator animator;
        [SerializeField] float maxHealth = 150f;

        float currentHealth = 150f;

        public TentacleAnimator Animator => animator;
        public Vector3 TipPosition => animator != null ? animator.TipPosition : transform.position + Vector3.up * 10f;
        public bool IsAttacking => animator != null && animator.IsAttacking;
        public float CurrentHealth => currentHealth;

        void Awake()
        {
            if (animator == null) animator = GetComponent<TentacleAnimator>();
            currentHealth = maxHealth;
        }

        public void Attack(Vector3 targetPosition, Action onImpact)
        {
            if (animator != null)
                animator.TriggerAttack(targetPosition, onImpact);
        }

        public void ReceiveWeaponHit(float damage, GameObject attacker) => TakeDamage(damage, attacker);

        public void TakeDamage(float damage, GameObject attacker = null)
        {
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            Vector3 hitPoint = TipPosition;
            GameAudio.Play(SoundCue.BulletFlesh, hitPoint, 1f);
            CombatVfx.Impact(hitPoint, Vector3.up, false, true);

            if (currentHealth <= 0f)
            {
                // Submerge tentacle on defeat
                gameObject.SetActive(false);
            }
        }
    }
}
