using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public sealed class KrakenEncounter : MonoBehaviour
    {
        [SerializeField] GameObject tentaclePrefab;
        [SerializeField] float chaseSpeed = 5.5f;
        [SerializeField] float escapeDistance = 20f;
        [SerializeField] float sinkSpeed = 6f;
        [SerializeField] KrakenAttackSystem attackSystem;

        static readonly Vector3[] formationOffsets =
        {
            new(-13f, 0f, 9f),
            new(-14f, 0f, -9f),
            new(13f, 0f, 13f),
            new(15f, 0f, 0f),
            new(13f, 0f, -13f)
        };

        Transform targetShip;
        readonly List<KrakenTentacle> tentacles = new();
        bool isSinking;
        float sinkTimer;

        public void Initialize(Transform ship)
        {
            targetShip = ship;
            transform.position = ship.position;

            if (attackSystem == null) attackSystem = GetComponent<KrakenAttackSystem>();
            if (attackSystem == null) attackSystem = gameObject.AddComponent<KrakenAttackSystem>();

            SpawnTentacles();
            attackSystem.Initialize(targetShip, tentacles);
        }

        void SpawnTentacles()
        {
            if (tentaclePrefab == null)
                tentaclePrefab = Resources.Load<GameObject>("KrakenTentacle");

            for (int i = 0; i < formationOffsets.Length; i++)
            {
                Vector3 worldPos = targetShip.TransformPoint(formationOffsets[i]);
                worldPos.y = OceanSurface.Instance != null ? OceanSurface.Instance.Height(worldPos) : 0f;

                Vector3 dirToShip = targetShip.position - worldPos;
                dirToShip.y = 0f;
                Quaternion rotation = dirToShip.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(dirToShip.normalized, Vector3.up)
                    : Quaternion.identity;

                GameObject instance = tentaclePrefab != null
                    ? Instantiate(tentaclePrefab, worldPos, rotation)
                    : new GameObject("KrakenTentacle_" + i);

                float scale = Random.Range(0.8f, 1.2f);
                instance.transform.localScale = Vector3.one * scale;

                var tentacle = instance.GetComponent<KrakenTentacle>();
                if (tentacle == null) tentacle = instance.AddComponent<KrakenTentacle>();

                tentacles.Add(tentacle);
            }
        }

        public void ExecuteTentacleAttack(int index, Vector3 targetPos)
        {
            if (index >= 0 && index < tentacles.Count && tentacles[index] != null)
            {
                tentacles[index].Attack(targetPos, null);
            }
        }

        public void TriggerSinkLocal()
        {
            isSinking = true;
        }

        void Update()
        {
            if (isSinking)
            {
                UpdateSinking();
                return;
            }

            if (targetShip == null)
            {
                isSinking = true;
                return;
            }

            var netShip = targetShip.GetComponent<PirateSlop.Networking.NetworkShip>();
            if (netShip == null || netShip.IsServerInitialized)
            {
                float distanceToShip = Vector3.Distance(transform.position, targetShip.position);
                if (distanceToShip > escapeDistance)
                {
                    if (netShip != null) netShip.KrakenSink();
                    isSinking = true;
                    return;
                }
            }

            UpdateChase();
        }

        void UpdateChase()
        {
            transform.position = Vector3.MoveTowards(transform.position, targetShip.position, chaseSpeed * Time.deltaTime);

            for (int i = 0; i < tentacles.Count; i++)
            {
                var tentacle = tentacles[i];
                if (tentacle == null) continue;

                Vector3 targetPos = targetShip.TransformPoint(formationOffsets[i]);
                targetPos.y = OceanSurface.Instance != null ? OceanSurface.Instance.Height(targetPos) : 0f;

                tentacle.transform.position = Vector3.MoveTowards(tentacle.transform.position, targetPos, chaseSpeed * Time.deltaTime);

                if (!tentacle.IsAttacking)
                {
                    Vector3 toShip = targetShip.position - tentacle.transform.position;
                    toShip.y = 0f;
                    if (toShip.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(toShip.normalized, Vector3.up);
                        tentacle.transform.rotation = Quaternion.Slerp(tentacle.transform.rotation, targetRot, Time.deltaTime * 4f);
                    }
                }
            }
        }

        void UpdateSinking()
        {
            sinkTimer += Time.deltaTime;

            for (int i = 0; i < tentacles.Count; i++)
            {
                var tentacle = tentacles[i];
                if (tentacle == null) continue;

                tentacle.transform.position = Vector3.MoveTowards(
                    tentacle.transform.position,
                    tentacle.transform.position + Vector3.down * 50f,
                    sinkSpeed * Time.deltaTime
                );
            }

            if (sinkTimer >= 4f)
            {
                for (int i = 0; i < tentacles.Count; i++)
                {
                    if (tentacles[i] != null)
                        Destroy(tentacles[i].gameObject);
                }
                if (targetShip != null)
                {
                    var mgr = targetShip.GetComponent<KrakenEncounterManager>();
                    mgr?.ClearEncounter();
                }
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < tentacles.Count; i++)
            {
                if (tentacles[i] != null)
                    Destroy(tentacles[i].gameObject);
            }
            if (targetShip != null)
            {
                var mgr = targetShip.GetComponent<KrakenEncounterManager>();
                mgr?.ClearEncounter();
            }
        }
    }
}
