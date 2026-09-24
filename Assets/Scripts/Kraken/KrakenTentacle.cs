using System;
using UnityEngine;

namespace PirateSlop
{
    [SelectionBase]
    public sealed class KrakenTentacle : MonoBehaviour
    {
        [SerializeField] TentacleAnimator animator;

        public TentacleAnimator Animator => animator;
        public Vector3 TipPosition => animator != null ? animator.TipPosition : transform.position + Vector3.up * 10f;
        public bool IsAttacking => animator != null && animator.IsAttacking;

        void Awake()
        {
            if (animator == null) animator = GetComponent<TentacleAnimator>();
        }

        public void Attack(Vector3 targetPosition, Action onImpact)
        {
            if (animator != null)
                animator.TriggerAttack(targetPosition, onImpact);
        }
    }
}
