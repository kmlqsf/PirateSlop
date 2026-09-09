using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class RumShelf : MonoBehaviour
    {
        public GameObject[] Bottles;
        NetworkShip ship;
        int shown = -1;
        void Awake() { ship = GetComponentInParent<NetworkShip>(); }
        public string Hint => $"Ром: {ship.RumCount}/{NetworkShip.RumCapacity} • E — поставить выбранный ром";
        void LateUpdate()
        {
            if (ship == null || shown == ship.RumCount) return;
            shown = ship.RumCount;
            for (int i = 0; i < Bottles.Length; i++) if (Bottles[i] != null) Bottles[i].SetActive(i < shown);
        }
    }
}
