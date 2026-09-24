using UnityEngine;

namespace PirateSlop
{
    public sealed class KrakenEncounterManager : MonoBehaviour
    {
        [SerializeField] GameObject encounterPrefab;
        [SerializeField] float checkInterval = 1f;
        [SerializeField] float stationaryRadius = 20f;
        [SerializeField] float stationaryDuration = 300f;

        Vector3 anchorPosition;
        float stationaryTimer;
        float nextCheckTime;
        KrakenEncounter activeEncounter;

        public KrakenEncounter ActiveEncounter => activeEncounter;

        void Start()
        {
            anchorPosition = transform.position;
            nextCheckTime = Time.time + checkInterval;
        }

        void Update()
        {
            if (Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + checkInterval;

            if (activeEncounter != null)
            {
                anchorPosition = transform.position;
                stationaryTimer = 0f;
                return;
            }

            float dist = Vector3.Distance(transform.position, anchorPosition);
            if (dist > stationaryRadius)
            {
                anchorPosition = transform.position;
                stationaryTimer = 0f;
            }
            else
            {
                stationaryTimer += checkInterval;
                if (stationaryTimer >= stationaryDuration)
                {
                    stationaryTimer = 0f;
                    TriggerEncounter();
                }
            }
        }

        public void TriggerEncounter()
        {
            if (activeEncounter != null) return;

            GameObject encounterGo = encounterPrefab != null
                ? Instantiate(encounterPrefab, transform.position, Quaternion.identity)
                : new GameObject("KrakenEncounter");

            activeEncounter = encounterGo.GetComponent<KrakenEncounter>();
            if (activeEncounter == null) activeEncounter = encounterGo.AddComponent<KrakenEncounter>();

            activeEncounter.Initialize(transform);
        }
    }
}
