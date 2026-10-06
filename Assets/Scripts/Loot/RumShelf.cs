using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class RumShelf : MonoBehaviour
    {
        public GameObject[] Bottles;
        NetworkShip ship;
        int shown = -1;
        readonly System.Collections.Generic.List<GameObject> upgradeBottles = new();
        void Awake() { ship = GetComponentInParent<NetworkShip>(); }
        public string Hint => $"Ром: {ship.RumCount}/{Mathf.Max(NetworkShip.RumCapacity, ship.RumCount)} • E — поставить выбранный ром";
        void LateUpdate()
        {
            if (ship == null || shown == ship.RumCount) return;
            shown = ship.RumCount;
            int extra = Mathf.Max(0, shown - Bottles.Length);
            if (Bottles.Length > 0 && Bottles[Bottles.Length - 1] != null)
                while (upgradeBottles.Count < extra)
                {
                    var source = Bottles[Bottles.Length - 1];
                    var bottle = Instantiate(source, source.transform.parent);
                    bottle.name = "UpgradeRumBottle";
                    bottle.transform.localPosition += Vector3.right * (.15f * (upgradeBottles.Count + 1));
                    foreach (var shape in bottle.GetComponentsInChildren<Collider>()) shape.enabled = false;
                    upgradeBottles.Add(bottle);
                }
            for (int i = 0; i < upgradeBottles.Count; i++) upgradeBottles[i].SetActive(i < extra);
            for (int i = 0; i < Bottles.Length; i++) if (Bottles[i] != null) Bottles[i].SetActive(i < shown);
        }
    }
}
