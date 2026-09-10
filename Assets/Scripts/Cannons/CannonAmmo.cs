using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public static class CannonAmmo
    {
        public static bool IsBall(InventoryItem item) => item == InventoryItem.Cannonball || item == InventoryItem.BoardingHook ||
            item >= InventoryItem.FireCannonball && item <= InventoryItem.BoomerangCannonball;

        public static float MortarBlastRadius(InventoryItem item) => item switch
        {
            InventoryItem.Cannonball => 2f,
            InventoryItem.BoardingHook => 3f,
            InventoryItem.BoomerangCannonball => 4f,
            InventoryItem.IceCannonball => 5f,
            InventoryItem.FireCannonball => 6f,
            InventoryItem.PushCannonball => 7f,
            _ => 2f
        };

        public static Color Color(InventoryItem item) => item switch
        {
            InventoryItem.FireCannonball => new Color(1f, .4f, .12f),
            InventoryItem.IceCannonball => new Color(.3f, .8f, 1f),
            InventoryItem.PushCannonball => new Color(.75f, .4f, 1f),
            _ => UnityEngine.Color.white
        };
    }
}
