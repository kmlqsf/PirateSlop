using System;
using UnityEngine;

namespace PirateSlop
{
    public sealed class TentacleAnimator : MonoBehaviour
    {
        [SerializeField] SkinnedMeshRenderer meshRenderer;
        [SerializeField] Transform[] bones;
        [SerializeField] float idleFrequency = 1.2f;
        [SerializeField] float idleAmplitude = 10f;
        [SerializeField] float attackDuration = 1.7f;

        Quaternion[] initialLocalRotations;
        float timeOffset;
        bool isAttacking;
        float attackStartTime;
        Vector3 attackTarget;
        Action impactCallback;
        bool impactTriggered;
        Quaternion attackBaseRotation;

        public bool IsAttacking => isAttacking;
        public Vector3 TipPosition => bones != null && bones.Length > 0 && bones[^1] != null ? bones[^1].position : transform.position + Vector3.up * 10f;

        void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if ((bones == null || bones.Length == 0) && meshRenderer != null) bones = meshRenderer.bones;

            if (bones != null && bones.Length > 0)
            {
                initialLocalRotations = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null) initialLocalRotations[i] = bones[i].localRotation;
                }
            }

            timeOffset = UnityEngine.Random.Range(0f, 500f);
        }

        public void TriggerAttack(Vector3 targetPosition, Action onImpact)
        {
            if (isAttacking) return;
            isAttacking = true;
            attackStartTime = Time.time;
            attackTarget = targetPosition;
            impactCallback = onImpact;
            impactTriggered = false;

            Vector3 dir = targetPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                attackBaseRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            else
                attackBaseRotation = transform.rotation;
        }

        void LateUpdate()
        {
            if (bones == null || bones.Length == 0) return;

            if (isAttacking)
            {
                UpdateAttack();
            }
            else
            {
                UpdateIdle();
            }
        }

        void UpdateIdle()
        {
            float t = Time.time * idleFrequency + timeOffset;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;

                float bonePhase = t + i * 0.55f;
                float x = Mathf.Sin(bonePhase) * (idleAmplitude * (1f + i * 0.2f));
                float z = Mathf.Cos(bonePhase * 0.85f) * (idleAmplitude * (1f + i * 0.15f));
                float y = Mathf.Sin(bonePhase * 0.6f) * (idleAmplitude * 0.35f);

                bones[i].localRotation = initialLocalRotations[i] * Quaternion.Euler(x, y, z);
            }
        }

        void UpdateAttack()
        {
            float elapsed = Time.time - attackStartTime;
            float progress = Mathf.Clamp01(elapsed / attackDuration);

            transform.rotation = Quaternion.Slerp(transform.rotation, attackBaseRotation, Time.deltaTime * 6f);

            float strikePitch = 0f;
            if (progress < 0.32f)
            {
                float t = progress / 0.32f;
                strikePitch = Mathf.Lerp(0f, -12f, Mathf.SmoothStep(0f, 1f, t));
            }
            else if (progress < 0.55f)
            {
                float t = (progress - 0.32f) / 0.23f;
                strikePitch = Mathf.Lerp(-12f, 24f, t * t);

                if (!impactTriggered && progress >= 0.52f)
                {
                    impactTriggered = true;
                    impactCallback?.Invoke();
                }
            }
            else
            {
                float t = (progress - 0.55f) / 0.45f;
                strikePitch = Mathf.Lerp(24f, 0f, Mathf.SmoothStep(0f, 1f, t));
            }

            float idleBlend = progress > 0.6f ? (progress - 0.6f) / 0.4f : 0f;
            float idleT = Time.time * idleFrequency + timeOffset;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;

                float bonePhase = idleT + i * 0.55f;
                float ix = Mathf.Sin(bonePhase) * (idleAmplitude * (1f + i * 0.2f)) * idleBlend;
                float iz = Mathf.Cos(bonePhase * 0.85f) * (idleAmplitude * (1f + i * 0.15f)) * idleBlend;

                bones[i].localRotation = initialLocalRotations[i] * Quaternion.Euler(strikePitch + ix, 0f, iz);
            }

            if (progress >= 1f)
            {
                isAttacking = false;
            }
        }
    }
}
