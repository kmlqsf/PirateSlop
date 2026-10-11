using UnityEngine;
using PirateSlop.Networking;
namespace PirateSlop
{
    public sealed class WhaleChest : MonoBehaviour
    {
        public Transform Lid;
        public WhaleLootPoint Owner;
        public bool IsOpened => TryGetComponent<NetworkLootChest>(out var chest) && chest.Opened;
        public bool CanOpen => Owner != null && Owner.ChestAvailable && !IsOpened;
    }
}

