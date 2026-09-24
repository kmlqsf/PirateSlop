using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class SharkSwarmVisual : MonoBehaviour
    {
        const int SharkCount = 3;
        const float PatrolRadius = 6.5f;
        const float BaitRadius = 3f;
        const float PatrolSpeed = 4f;
        const float BaitSpeed = 7f;
        const float DepthOffset = 0.75f;

        NetworkLootChest chest;
        GameObject[] sharks;
        Animator[] animators;
        float[] angles;
        bool dispersed;
        float disperseY;
        float nextBaitSplash;

        int attackingIndex = -1;
        float attackTimer;
        Vector3 attackTarget;

        public void Initialize(NetworkLootChest owner)
        {
            chest = owner;
            var prefab = Resources.Load<GameObject>("SharkVisual");
            if (prefab == null) return;

            sharks = new GameObject[SharkCount];
            animators = new Animator[SharkCount];
            angles = new float[SharkCount];

            Vector3 center = chest.EventPoint;
            for (int i = 0; i < SharkCount; i++)
            {
                angles[i] = i * (Mathf.PI * 2f / SharkCount);
                Vector3 pos = center + new Vector3(Mathf.Cos(angles[i]) * PatrolRadius, 0f, Mathf.Sin(angles[i]) * PatrolRadius);
                var ocean = OceanSurface.Instance;
                pos.y = (ocean != null ? ocean.Height(pos) : center.y) - DepthOffset;

                Vector3 forward = new Vector3(-Mathf.Sin(angles[i]), 0f, Mathf.Cos(angles[i]));
                var shark = Instantiate(prefab, pos, Quaternion.LookRotation(forward), transform);
                shark.name = $"Shark_{i + 1}";
                sharks[i] = shark;
                animators[i] = shark.GetComponent<Animator>();
            }
        }

        public void AttackLunge(Vector3 targetPoint)
        {
            if (sharks == null || sharks.Length == 0) return;
            int nearest = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < sharks.Length; i++)
            {
                if (sharks[i] == null) continue;
                float d = (sharks[i].transform.position - targetPoint).sqrMagnitude;
                if (d < minDist) { minDist = d; nearest = i; }
            }
            attackingIndex = nearest;
            attackTimer = 1.0f;
            attackTarget = targetPoint;
            if (animators[nearest] != null)
            {
                animators[nearest].SetTrigger("Bite");
                animators[nearest].SetBool("Biting", true);
            }
        }

        void Update()
        {
            if (chest == null || sharks == null) return;

            if (!dispersed && (chest.Opened || chest.Carrier != null))
            {
                dispersed = true;
                disperseY = 0f;
            }

            if (dispersed)
            {
                disperseY += Time.deltaTime * 3f;
                for (int i = 0; i < sharks.Length; i++)
                {
                    if (sharks[i] == null) continue;
                    sharks[i].transform.position += (sharks[i].transform.forward * (PatrolSpeed * 1.5f) + Vector3.down * 2.5f) * Time.deltaTime;
                    if (disperseY > 20f)
                    {
                        Destroy(sharks[i]);
                        sharks[i] = null;
                    }
                }
                return;
            }

            bool distracted = chest.SharksDistracted;
            Vector3 targetCenter = distracted ? chest.SharkBaitPoint : chest.EventPoint;
            float radius = distracted ? BaitRadius : PatrolRadius;
            float speed = distracted ? BaitSpeed : PatrolSpeed;
            var ocean = OceanSurface.Instance;

            if (distracted && Time.time >= nextBaitSplash)
            {
                nextBaitSplash = Time.time + 0.8f;
                CombatVfx.Splash(chest.SharkBaitPoint);
            }

            if (attackTimer > 0f)
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0.3f && attackingIndex >= 0 && animators[attackingIndex] != null && !distracted)
                    animators[attackingIndex].SetBool("Biting", false);
                if (attackTimer <= 0f)
                    attackingIndex = -1;
            }

            for (int i = 0; i < sharks.Length; i++)
            {
                if (sharks[i] == null) continue;

                bool isAttacking = (i == attackingIndex && attackTimer > 0.25f);
                if (animators[i] != null)
                {
                    if (!isAttacking && attackTimer <= 0.3f)
                        animators[i].SetBool("Biting", distracted);
                    animators[i].speed = isAttacking ? 1.8f : (distracted ? 1.5f : 1.0f);
                }

                Vector3 targetPos;
                Quaternion targetRot;
                float blendPos;
                float blendRot;

                if (isAttacking)
                {
                    targetPos = attackTarget;
                    float waterY = ocean != null ? ocean.Height(targetPos) : targetCenter.y;
                    targetPos.y = waterY - DepthOffset;

                    Vector3 toTarget = attackTarget - sharks[i].transform.position;
                    toTarget.y = 0f;
                    targetRot = toTarget.sqrMagnitude > 0.05f ? Quaternion.LookRotation(toTarget.normalized, Vector3.up) : sharks[i].transform.rotation;
                    blendPos = 1f - Mathf.Exp(-14f * Time.deltaTime);
                    blendRot = 1f - Mathf.Exp(-16f * Time.deltaTime);
                }
                else
                {
                    angles[i] += (speed / radius) * Time.deltaTime;
                    targetPos = targetCenter + new Vector3(Mathf.Cos(angles[i]) * radius, 0f, Mathf.Sin(angles[i]) * radius);
                    float waterY = ocean != null ? ocean.Height(targetPos) : targetCenter.y;
                    targetPos.y = waterY - DepthOffset;

                    Vector3 forward = new Vector3(-Mathf.Sin(angles[i]), 0f, Mathf.Cos(angles[i]));
                    if (ocean != null)
                    {
                        float forwardY = ocean.Height(targetPos + forward * 0.8f) - ocean.Height(targetPos - forward * 0.8f);
                        forward.y = forwardY * 0.5f;
                    }

                    targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
                    blendPos = 1f - Mathf.Exp(-8f * Time.deltaTime);
                    blendRot = 1f - Mathf.Exp(-10f * Time.deltaTime);
                }

                sharks[i].transform.position = Vector3.Lerp(sharks[i].transform.position, targetPos, blendPos);
                sharks[i].transform.rotation = Quaternion.Slerp(sharks[i].transform.rotation, targetRot, blendRot);
            }
        }
    }
}
