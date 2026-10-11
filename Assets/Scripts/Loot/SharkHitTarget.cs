using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class SharkHitTarget : MonoBehaviour
    {
        public NetworkLootChest Chest;
        public int Index;
        public void ReceiveFirearmShot(GameObject shooter) => Chest?.ShootShark(Index, shooter);
    }
}
