using UnityEngine;

namespace PirateSlop.Harpoon
{
    public class HarpoonShipMount : MonoBehaviour
    {
        [SerializeField] HarpoonGun harpoonPrefab;
        [SerializeField] Transform mountPort;
        [SerializeField] Transform mountStarboard;

        HarpoonGun gunPort;
        HarpoonGun gunStarboard;

        void Awake()
        {
            SpawnHarpoons();
        }

        public void SpawnHarpoons()
        {
            if (harpoonPrefab == null)
            {
                harpoonPrefab = Resources.Load<HarpoonGun>("HarpoonGun");
            }

            if (harpoonPrefab == null) return;

            var destruction = GetComponentInParent<ShipDestruction>();
            ShipDamageSection railingSection = null;
            if (destruction != null)
            {
                foreach (var s in destruction.Sections)
                {
                    if (s != null && s.SectionId == 1019)
                    {
                        railingSection = s;
                        break;
                    }
                }
            }

            if (mountPort != null && gunPort == null)
            {
                gunPort = Instantiate(harpoonPrefab, mountPort.position, mountPort.rotation, mountPort);
                if (railingSection != null) gunPort.SetMountSection(railingSection);
            }
            if (mountStarboard != null && gunStarboard == null)
            {
                gunStarboard = Instantiate(harpoonPrefab, mountStarboard.position, mountStarboard.rotation, mountStarboard);
                if (railingSection != null) gunStarboard.SetMountSection(railingSection);
            }
        }
    }
}
