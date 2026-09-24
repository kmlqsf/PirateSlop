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
            var netShip = GetComponent<PirateSlop.Networking.NetworkShip>();
            if (netShip != null && !netShip.IsServerInitialized) return;

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
            var netShip = GetComponent<PirateSlop.Networking.NetworkShip>();
            if (netShip != null && netShip.IsServerInitialized)
            {
                netShip.TriggerKrakenFromNetwork();
            }
            else if (netShip == null)
            {
                StartEncounterLocal();
            }
        }

        public void StartEncounterLocal()
        {
            if (activeEncounter != null) return;

            if (encounterPrefab == null)
                encounterPrefab = Resources.Load<GameObject>("KrakenEncounter");

            GameObject encounterGo = encounterPrefab != null
                ? Instantiate(encounterPrefab, transform.position, Quaternion.identity)
                : new GameObject("KrakenEncounter");

            activeEncounter = encounterGo.GetComponent<KrakenEncounter>();
            if (activeEncounter == null) activeEncounter = encounterGo.AddComponent<KrakenEncounter>();

            activeEncounter.Initialize(transform);
        }

        public void ClearEncounter()
        {
            activeEncounter = null;
        }
    }
}
