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
            if (sharks == null) return;
            for (int i = 0; i < sharks.Length; i++)
            {
                if (sharks[i] == null) continue;
                if ((sharks[i].transform.position - targetPoint).sqrMagnitude < 36f && animators[i] != null)
                {
                    animators[i].SetTrigger("Bite");
                    animators[i].SetBool("Biting", true);
                }
            }
            CombatVfx.Splash(targetPoint);
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

            var ocean = OceanSurface.Instance;
            bool hasTarget = false;
            Vector3 targetPoint = Vector3.zero;

            if (chest.SharksDistracted)
            {
                hasTarget = true;
                targetPoint = chest.SharkBaitPoint;
            }
            else if (chest.SharkTargetPlayer != null)
            {
                hasTarget = true;
                targetPoint = chest.SharkTargetPlayer.transform.position;
            }
            else
            {
                float aggroDistSqr = 16f * 16f;
                Vector3 center = chest.EventPoint;
                float bestDistSqr = aggroDistSqr;
                foreach (var p in NetworkPlayer.Active)
                {
                    if (p == null || !p.IsSpawned || p.Motor.IsDead || !p.Motor.IsSwimming) continue;
                    Vector3 pPos = p.transform.position;
                    pPos.y = center.y;
                    float sqr = (pPos - center).sqrMagnitude;
                    if (sqr < bestDistSqr)
                    {
                        bestDistSqr = sqr;
                        targetPoint = p.transform.position;
                        hasTarget = true;
                    }
                }
            }

            if (hasTarget)
            {
                if (Time.time >= nextBaitSplash)
                {
                    nextBaitSplash = Time.time + 0.6f;
                    CombatVfx.Splash(targetPoint);
                }

                for (int i = 0; i < sharks.Length; i++)
                {
                    if (sharks[i] == null) continue;

                    angles[i] += 3.5f * Time.deltaTime;
                    Vector3 diff = targetPoint - sharks[i].transform.position;
                    diff.y = 0f;
                    float dist = diff.magnitude;

                    Vector3 targetPos;
                    Quaternion targetRot;

                    if (dist > 2.5f)
                    {
                        Vector3 offset = new Vector3(Mathf.Cos(angles[i]), 0f, Mathf.Sin(angles[i])) * 1.5f;
                        targetPos = targetPoint + offset;
                        float waterY = ocean != null ? ocean.Height(targetPos) : targetPoint.y;
                        targetPos.y = waterY - DepthOffset;

                        targetRot = diff.sqrMagnitude > 0.05f ? Quaternion.LookRotation(diff.normalized, Vector3.up) : sharks[i].transform.rotation;

                        if (animators[i] != null)
                        {
                            animators[i].speed = 1.6f;
                            animators[i].SetBool("Biting", false);
                        }

                        sharks[i].transform.position = Vector3.MoveTowards(sharks[i].transform.position, targetPos, 11f * Time.deltaTime);
                        sharks[i].transform.rotation = Quaternion.Slerp(sharks[i].transform.rotation, targetRot, 1f - Mathf.Exp(-12f * Time.deltaTime));
                    }
                    else
                    {
                        Vector3 circleOffset = new Vector3(Mathf.Cos(angles[i]) * 1.8f, 0f, Mathf.Sin(angles[i]) * 1.8f);
                        targetPos = targetPoint + circleOffset;
                        float waterY = ocean != null ? ocean.Height(targetPos) : targetPoint.y;
                        targetPos.y = waterY - DepthOffset;

                        Vector3 tangent = new Vector3(-Mathf.Sin(angles[i]), 0f, Mathf.Cos(angles[i]));
                        targetRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);

                        if (animators[i] != null)
                        {
                            animators[i].speed = 1.8f;
                            animators[i].SetBool("Biting", true);
                        }

                        sharks[i].transform.position = Vector3.Lerp(sharks[i].transform.position, targetPos, 1f - Mathf.Exp(-10f * Time.deltaTime));
                        sharks[i].transform.rotation = Quaternion.Slerp(sharks[i].transform.rotation, targetRot, 1f - Mathf.Exp(-12f * Time.deltaTime));
                    }
                }
            }
            else
            {
                Vector3 center = chest.EventPoint;
                for (int i = 0; i < sharks.Length; i++)
                {
                    if (sharks[i] == null) continue;

                    if (animators[i] != null)
                    {
                        animators[i].speed = 1.0f;
                        animators[i].SetBool("Biting", false);
                    }

                    angles[i] += (PatrolSpeed / PatrolRadius) * Time.deltaTime;
                    Vector3 patrolPos = center + new Vector3(Mathf.Cos(angles[i]) * PatrolRadius, 0f, Mathf.Sin(angles[i]) * PatrolRadius);
                    float waterY = ocean != null ? ocean.Height(patrolPos) : center.y;
                    patrolPos.y = waterY - DepthOffset;

                    Vector3 toPatrol = patrolPos - sharks[i].transform.position;
                    toPatrol.y = 0f;

                    if (toPatrol.magnitude > 2.0f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(toPatrol.normalized, Vector3.up);
                        sharks[i].transform.position = Vector3.MoveTowards(sharks[i].transform.position, patrolPos, 6.5f * Time.deltaTime);
                        sharks[i].transform.rotation = Quaternion.Slerp(sharks[i].transform.rotation, targetRot, 1f - Mathf.Exp(-6f * Time.deltaTime));
                    }
                    else
                    {
                        Vector3 forward = new Vector3(-Mathf.Sin(angles[i]), 0f, Mathf.Cos(angles[i]));
                        if (ocean != null)
                        {
                            float forwardY = ocean.Height(patrolPos + forward * 0.8f) - ocean.Height(patrolPos - forward * 0.8f);
                            forward.y = forwardY * 0.5f;
                        }
                        Quaternion targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
                        sharks[i].transform.position = Vector3.Lerp(sharks[i].transform.position, patrolPos, 1f - Mathf.Exp(-6f * Time.deltaTime));
                        sharks[i].transform.rotation = Quaternion.Slerp(sharks[i].transform.rotation, targetRot, 1f - Mathf.Exp(-8f * Time.deltaTime));
                    }
                }
            }
        }
    }
}
