using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.World
{
    public sealed class WorldMapDisplay : MonoBehaviour
    {
        bool visible;
        void Update() { if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) visible = !visible; }
        void OnGUI()
        {
            var world = GetComponent<ProceduralWorld>();
            if (!visible || !world.Ready || Networking.SessionController.MenuOpen) return;
            var map = world.Layout;
            float size = Mathf.Min(560, Screen.height - 100), x = (Screen.width - size) * .5f, y = 45;
            GUI.Box(new Rect(x - 8, y - 28, size + 16, size + 75), "Map · seed " + map.Seed + " · M close");
            var old = GUI.color; GUI.color = new Color(.05f, .19f, .25f); GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
            foreach (var location in map.Locations)
            {
                float radius = Mathf.Max(3, location.Radius / (map.Radius * 2) * size);
                float px = x + (.5f + location.Position.x / (map.Radius * 2)) * size, py = y + (.5f - location.Position.z / (map.Radius * 2)) * size;
                GUI.color = location.Type.Shape == Landform.Reef || location.Type.Shape == Landform.ReefPassage ? Color.cyan : location.Type.Ground;
                GUI.DrawTexture(new Rect(px - radius, py - radius, radius * 2, radius * 2), Texture2D.whiteTexture);
                GUI.color = Color.white; GUI.Label(new Rect(px - 40, py, 110, 20), location.Type.Id);
            }
            var storm = StormZone.Instance;
            if (storm != null)
            {
                float r = storm.Radius / (map.Radius * 2) * size;
                GUI.color = new Color(.48f, .24f, .65f, .55f);
                for (float row = 0; row < size; row += 2)
                {
                    float dy = row + 1 - size * .5f;
                    float half = Mathf.Sqrt(Mathf.Max(0, r * r - dy * dy));
                    float edge = Mathf.Clamp(size * .5f - half, 0, size * .5f);
                    float h = Mathf.Min(2, size - row);
                    GUI.DrawTexture(new Rect(x, y + row, edge, h), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(x + size - edge, y + row, edge, h), Texture2D.whiteTexture);
                }
                GUI.color = new Color(.55f, 1, .94f);
                var center = new Vector2(x + size * .5f, y + size * .5f);
                for (int i = 0; i < 180; i++)
                {
                    float a = i * Mathf.PI * 2 / 180, b = (i + 1) * Mathf.PI * 2 / 180;
                    Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r, 2);
                }
            }
            foreach (var ship in Networking.NetworkShip.ActiveShips)
            {
                if (ship == null) continue;
                var p = ship.transform.position;
                bool own = storm != null && ship == storm.LocalShip;
                bool outside = storm != null && storm.DistanceInside(p) <= 0;
                var point = new Vector2(x + Mathf.Clamp(.5f + p.x / (map.Radius * 2), 0, 1) * size, y + Mathf.Clamp(.5f - p.z / (map.Radius * 2), 0, 1) * size);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(point.x - 6, point.y - 6, 12, 12), Texture2D.whiteTexture);
                GUI.color = outside ? new Color(1, .32f, .25f) : own ? Color.white : Color.yellow;
                GUI.DrawTexture(new Rect(point.x - 4, point.y - 4, 8, 8), Texture2D.whiteTexture);
                var forward = ship.transform.forward;
                Line(point, point + new Vector2(forward.x, -forward.z) * 17, 2);
                if (own) GUI.Label(new Rect(Mathf.Clamp(point.x - 55, x, x + size - 130), Mathf.Clamp(point.y + 9, y, y + size - 22), 130, 22), outside ? "ВАШ КОРАБЛЬ: ШТОРМ" : "ВАШ КОРАБЛЬ: В ЗОНЕ");
            }
            GUI.color = Color.white; GUI.Label(new Rect(x, y + size + 6, size, 40), "Бирюзовое кольцо: безопасная зона • Фиолетовое: шторм\nКрасный: корабль вне зоны • Белый: ваш • Жёлтый: другие"); GUI.color = old;
        }
        static void Line(Vector2 a, Vector2 b, float width)
        {
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * .5f, Vector2.Distance(a, b), width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
