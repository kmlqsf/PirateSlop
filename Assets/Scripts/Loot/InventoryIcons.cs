using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    [CreateAssetMenu(menuName = "PirateSlop/Inventory Icons")]
    public sealed class InventoryIcons : ScriptableObject
    {
        public Texture2D[] Icons;
        Material hudMaterial;
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
            InventoryItem.Rum => "Ром",
            _ => ""
        };
        public bool DrawSlot(Rect rect, InventoryItem item, int count, string key, bool button)
        {
            bool selected = GUI.color != Color.white;
            Color oldColor = GUI.color;
            GUI.color = Color.white;
            bool hover = button && rect.Contains(Event.current.mousePosition);
            bool clicked = button && GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (selected || hover) PirateHudStyle.Brush(new Rect(rect.x - 8, rect.y - 10, rect.width + 16, rect.height + 10), new Color(.06f,.13f,.13f,.8f));
            int index = CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item;
            GUI.color = CannonAmmo.IsBall(item) ? CannonAmmo.Color(item) : PirateHudStyle.Paper;
            if (index >= 0 && Icons != null && index < Icons.Length && Icons[index] != null)
            {
                float inset = selected || hover ? 0 : 5;
                var target = new Rect(rect.x + inset, rect.y - 9 + inset, rect.width - inset * 2, rect.height - 17 - inset * 2);
                var icon = Icons[index];
                float scale = Mathf.Min(target.width / icon.width, target.height / icon.height);
                target = new Rect(target.center.x - icon.width * scale / 2, target.center.y - icon.height * scale / 2, icon.width * scale, icon.height * scale);
                if (hudMaterial == null)
                {
                    var shader = Resources.Load<Shader>("HudIcon");
                    if (shader != null) hudMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                if (Event.current.type == EventType.Repaint && hudMaterial != null)
                    Graphics.DrawTexture(target, icon, new Rect(0, 0, 1, 1), 0, 0, 0, 0, GUI.color, hudMaterial);
                else if (hudMaterial == null) GUI.DrawTexture(target, icon, ScaleMode.ScaleToFit, true);
            }
            else if (item == InventoryItem.Rum)
            {
                PirateHudStyle.Brush(new Rect(rect.center.x - 13, rect.y + 6, 26, 32), new Color(.3f,.65f,.42f), true);
                PirateHudStyle.Brush(new Rect(rect.center.x - 5, rect.y - 8, 10, 18), PirateHudStyle.Gold, true);
                PirateHudStyle.Brush(new Rect(rect.center.x - 9, rect.y + 16, 18, 12), PirateHudStyle.Paper, true);
            }
            else if (item == InventoryItem.Sabre)
            {
                var matrix = GUI.matrix;
                var center = new Vector2(rect.center.x, rect.y + 23);
                GUIUtility.RotateAroundPivot(35, center);
                PirateHudStyle.Brush(new Rect(center.x - 5, center.y - 29, 10, 48), PirateHudStyle.Paper, true);
                PirateHudStyle.Brush(new Rect(center.x - 16, center.y + 12, 32, 7), PirateHudStyle.Gold, true);
                PirateHudStyle.Brush(new Rect(center.x - 4, center.y + 16, 8, 15), PirateHudStyle.Gold, true);
                GUI.matrix = matrix;
            }
            GUI.color = Color.white;
            if (selected) PirateHudStyle.Brush(new Rect(rect.x + 13, rect.yMax - 15, rect.width - 26, 7), PirateHudStyle.Gold, true);
            PirateHudStyle.Label(new Rect(rect.center.x - 10, rect.yMax - 9, 20, 20), key, selected ? PirateHudStyle.Paper : PirateHudStyle.Muted);
            if (count > 1) PirateHudStyle.Label(new Rect(rect.xMax - 25, rect.y + 28, 27, 22), count.ToString(), PirateHudStyle.Paper);
            if (button) PirateHudStyle.Label(new Rect(rect.x - 4, rect.yMax - 19, rect.width + 8, 24), ItemName(item), PirateHudStyle.Paper);
            GUI.color = oldColor;
            return clicked;
        }
        void OnDisable() { if (hudMaterial != null) DestroyImmediate(hudMaterial); }
    }
}
