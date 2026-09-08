using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public static class CannonAmmo
    {
        public static bool IsBall(InventoryItem item) => item == InventoryItem.Cannonball ||
            item >= InventoryItem.FireCannonball && item <= InventoryItem.BoomerangCannonball;

        public static Color Color(InventoryItem item) => item switch
        {
            InventoryItem.FireCannonball => new Color(1f, .4f, .12f),
            InventoryItem.IceCannonball => new Color(.3f, .8f, 1f),
            InventoryItem.PushCannonball => new Color(.75f, .4f, 1f),
            _ => UnityEngine.Color.white
        };
    }
}