using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    [CreateAssetMenu(menuName = "PirateSlop/Inventory Icons")]
    public sealed class InventoryIcons : ScriptableObject
    {
        public Texture2D[] Icons;
        public static string ItemName(InventoryItem item) => item switch
        {
            InventoryItem.Fish => "Рыба",
            InventoryItem.Pistol => "Пистолет",
            InventoryItem.Rod => "Удочка",
            InventoryItem.Cannon => "Пушка",
            InventoryItem.Cannonball => "Ядро",
            InventoryItem.FireCannonball => "Огненное",
            InventoryItem.IceCannonball => "Ледяное",
            InventoryItem.PushCannonball => "Толчковое",
            InventoryItem.BoomerangCannonball => "Бумеранг",
            InventoryItem.Mallet => "Киянка",
            InventoryItem.Plank => "Доска",
            InventoryItem.Sabre => "Сабля",
            _ => ""
        };
        public bool DrawSlot(Rect rect, InventoryItem item, int count, string key, bool button)
        {
            bool clicked = button && GUI.Button(rect, GUIContent.none);
            if (!button) GUI.Box(rect, GUIContent.none);
            int index = CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item;
            Color oldColor = GUI.color;
            GUI.color *= CannonAmmo.Color(item);
            if (index >= 0 && Icons != null && index < Icons.Length && Icons[index] != null)
                GUI.DrawTexture(new Rect(rect.x + 8, rect.y + 5, rect.width - 16, rect.height - 24), Icons[index], ScaleMode.ScaleToFit, true);
            GUI.color = oldColor;
            GUI.Label(new Rect(rect.x + 4, rect.y + 1, 20, 20), key);
            if (count > 1) GUI.Label(new Rect(rect.xMax - 27, rect.y + 2, 27, 20), count.ToString());
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 10 };
            GUI.Label(new Rect(rect.x + 2, rect.yMax - 21, rect.width - 4, 20), ItemName(item), style);
            return clicked;
        }
    }
}
