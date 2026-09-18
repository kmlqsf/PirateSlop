using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    [CreateAssetMenu(menuName = "PirateSlop/Inventory Icons")]
    public sealed class InventoryIcons : ScriptableObject
    {
        public Texture2D[] Icons;
        public Texture2D ChestIcon;
        public Texture2D HotbarBase, HotbarSelected;
        Material hudMaterial;
        GUIStyle hotbarNumber;
        public void DrawHotbarSlot(Rect rect, InventoryItem item, int count, string key, bool selected)
        {
            Color previous = GUI.color;
            float uiScale = rect.width / 56f;
            GUI.color = Color.white;
            var background = selected && HotbarSelected != null ? HotbarSelected : HotbarBase;
            if (background != null) GUI.DrawTexture(rect, background, ScaleMode.StretchToFill, true);
            else PirateHudStyle.Fill(rect, new Color(.094f, .106f, .114f, .72f));
            int index = (int)item;
            if (index >= 0 && Icons != null && index < Icons.Length && Icons[index] != null)
            {
                var icon = Icons[index];
                float occupancy = item == InventoryItem.Rod || item == InventoryItem.Sabre || item == InventoryItem.Swordfish || item == InventoryItem.Musket ? .82f :
                    CannonAmmo.IsBall(item) || item == InventoryItem.Pufferfish || item == InventoryItem.HolyGrenade ? .75f : .79f;
                float size = rect.width * occupancy;
                float scale = Mathf.Min(size / icon.width, size / icon.height);
                var target = new Rect(rect.center.x - icon.width * scale * .5f, rect.center.y - 2 * uiScale - icon.height * scale * .5f, icon.width * scale, icon.height * scale);
                if (hudMaterial == null)
                {
                    var shader = Resources.Load<Shader>("HudIcon");
                    if (shader != null) hudMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                if (Event.current.type == EventType.Repaint && hudMaterial != null)
                {
                    hudMaterial.SetFloat("_Edge", 1f);
                    Graphics.DrawTexture(target, icon, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, hudMaterial);
                    hudMaterial.SetFloat("_Edge", 0f);
                }
                else if (hudMaterial == null) GUI.DrawTexture(target, icon, ScaleMode.ScaleToFit, true);
            }
            hotbarNumber ??= new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Normal, padding = new RectOffset(), alignment = TextAnchor.LowerLeft };
            hotbarNumber.fontSize = Mathf.RoundToInt(11 * uiScale);
            hotbarNumber.normal.textColor = selected ? new Color(.9f, .88f, .83f, .95f) : new Color(.85f, .85f, .82f, .8f);
            GUI.Label(new Rect(rect.x + 5 * uiScale, rect.yMax - 16 * uiScale, 20 * uiScale, 13 * uiScale), key, hotbarNumber);
            if (count > 1 || CannonAmmo.IsBall(item))
            {
                hotbarNumber.alignment = TextAnchor.LowerRight;
                hotbarNumber.normal.textColor = new Color(.9f, .88f, .83f, .9f);
                GUI.Label(new Rect(rect.xMax - 28 * uiScale, rect.yMax - 16 * uiScale, 23 * uiScale, 13 * uiScale), count.ToString(), hotbarNumber);
                hotbarNumber.alignment = TextAnchor.LowerLeft;
            }
            GUI.color = previous;
        }
        public static string ItemName(InventoryItem item) => item switch
        {
            InventoryItem.Fish => "Рыба",
            InventoryItem.Pufferfish => "Иглобрюх",
            InventoryItem.Swordfish => "Рыба-меч",
            InventoryItem.Wine => "Jesus Whine",
            InventoryItem.Musket => "Мушкет",
            InventoryItem.DoubleBarrel => "Двустволка",
            InventoryItem.BombParrot => "Попугай",
            InventoryItem.HolyGrenade => "Святая граната",
            InventoryItem.GrapplingHook => "Крюк-кошка",
            InventoryItem.BoardingHook => "Абордажный крюк",
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
            int index = (int)item;
            GUI.color = Color.white;
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
            else if (item == InventoryItem.GrapplingHook || item == InventoryItem.BoardingHook)
            {
                Color tint = item == InventoryItem.BoardingHook ? PirateHudStyle.Gold : PirateHudStyle.Paper;
                Vector2 center = new Vector2(rect.center.x, rect.y + 19);
                PirateHudStyle.Brush(new Rect(center.x - 3, center.y - 21, 6, 37), tint, true);
                PirateHudStyle.Brush(new Rect(center.x - 11, center.y - 16, 22, 5), tint, true);
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(40, center + Vector2.up * 14);
                PirateHudStyle.Brush(new Rect(center.x - 22, center.y + 11, 25, 6), tint, true);
                GUI.matrix = matrix;
                GUIUtility.RotateAroundPivot(-40, center + Vector2.up * 14);
                PirateHudStyle.Brush(new Rect(center.x - 3, center.y + 11, 25, 6), tint, true);
                GUI.matrix = matrix;
            }
            else if (item == InventoryItem.HolyGrenade)
            {
                PirateHudStyle.Brush(new Rect(rect.center.x - 18, rect.y + 8, 36, 32), PirateHudStyle.Paper, true);
                PirateHudStyle.Brush(new Rect(rect.center.x - 3, rect.y - 9, 6, 26), PirateHudStyle.Gold, true);
                PirateHudStyle.Brush(new Rect(rect.center.x - 11, rect.y - 3, 22, 6), PirateHudStyle.Gold, true);
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
