using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.World
{
    public sealed class WorldMapDisplay : MonoBehaviour
    {
        bool visible;
        WorldLayout cachedMap;
        Texture2D dot;
        GUIStyle markerStyle;
        Networking.NetworkPlayer localPlayer;
        readonly System.Collections.Generic.List<MapMarker> scenery = new();
        readonly System.Collections.Generic.List<MapMarker> events = new();
        readonly Rect[] labelRects = new Rect[MainOceanEvents.EventCount];
        struct MapMarker
        {
            public Vector3 Position;
            public float Radius, LabelWidth;
            public Color Color;
            public string Name;
        }
        void Update() { if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) visible = !visible; }
        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint || Networking.SessionController.MenuOpen) return;
            var world = GetComponent<ProceduralWorld>();
            if (world == null || !world.Ready) return;
            var map = world.Layout;
            float size = Mathf.Min(1000f, Mathf.Min(Screen.height - 140f, Screen.width - 72f)), x = (Screen.width - size) * .5f, y = (Screen.height - size) * .5f - 5f;
            var old = GUI.color;
            int oldDepth = GUI.depth; GUI.depth = -100;
            GUI.color = Color.white;
            PirateHudStyle.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.01f, .025f, .035f, .72f));
            PirateHudStyle.Fill(new Rect(x - 18, y - 50, size + 36, size + 112), new Color(.025f, .065f, .085f, .99f));
            PirateHudStyle.Brush(new Rect(x - 16, y - 9, size + 32, 3), PirateHudStyle.Gold, true);
            PirateHudStyle.Label(new Rect(x, y - 46, size, 32), "КАРТА ОКЕАНА", PirateHudStyle.Paper, true);
            PirateHudStyle.Label(new Rect(x + size - 136, y - 40, 136, 22), "M · закрыть", PirateHudStyle.Muted);
            GUI.color = new Color(.045f, .15f, .20f); GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
            CacheMarkers(map);
            GUI.color = new Color(.3f, .58f, .65f, .13f);
            for (int i = 1; i < 8; i++)
            {
                float offset = i * size / 8f;
                GUI.DrawTexture(new Rect(x + offset, y, 1, size), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x, y + offset, size, 1), Texture2D.whiteTexture);
            }
            foreach (var marker in scenery)
            {
                var point = Project(marker.Position, map, x, y, size);
                float radius = Mathf.Max(1.8f, marker.Radius / (map.Radius * 2f) * size);
                GUI.color = new Color(.01f, .055f, .07f, .85f);
                GUI.DrawTexture(new Rect(point.x - radius - 1, point.y - radius - 1, radius * 2 + 2, radius * 2 + 2), dot);
                GUI.color = marker.Color;
                GUI.DrawTexture(new Rect(point.x - radius, point.y - radius, radius * 2, radius * 2), dot);
            }
            if (MainOceanMap.IsMain(map))
            {
                float radius = OceanSurface.CentralWhirlpoolRadius / (map.Radius * 2f) * size;
                GUI.color = new Color(.65f, .35f, .65f, .8f);
                GUI.DrawTexture(new Rect(x + size * .5f - radius, y + size * .5f - radius, radius * 2, radius * 2), dot);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + size * .5f - 31, y + size * .5f - 9, 70, 20), "Воронка", markerStyle);
            }
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
                for (float row = 0; row < size; row += 3)
                {
                    float dy = row + 1.5f - size * .5f;
                    float half = Mathf.Sqrt(Mathf.Max(0, r * r - dy * dy));
                    float edge = Mathf.Clamp(size * .5f - half, 0, size * .5f);
                    float h = Mathf.Min(3, size - row);
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
            int labelCount = 0;
            foreach (var marker in events)
            {
                var point = Project(marker.Position, map, x, y, size);
                GUI.color = new Color(1f, .8f, .3f);
                GUI.DrawTexture(new Rect(point.x - 3, point.y - 3, 6, 6), dot);
                GUI.color = new Color(1f, .92f, .68f);
                float width = marker.LabelWidth;
                Rect label = default;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    bool left = (attempt % 2 == 0) == (point.x < x + size * .5f);
                    float dy = attempt < 2 ? -10f : attempt < 4 ? 12f : attempt < 6 ? -32f : 34f;
                    label = new Rect(Mathf.Clamp(left ? point.x - width - 7f : point.x + 7f, x + 2f, x + size - width - 2f), Mathf.Clamp(point.y + dy, y + 2f, y + size - 24f), width, 21f);
                    bool overlaps = false;
                    for (int i = 0; i < labelCount; i++) if (label.Overlaps(labelRects[i])) { overlaps = true; break; }
                    if (!overlaps) break;
                }
                if (labelCount < labelRects.Length) labelRects[labelCount++] = label;
                PirateHudStyle.Fill(label, new Color(.02f, .07f, .09f, .7f));
                GUI.Label(label, marker.Name, markerStyle);
            }
            if (localPlayer == null || !localPlayer.IsOwner)
                foreach (var candidate in Networking.NetworkPlayer.Active)
                    if (candidate != null && candidate.IsOwner && !candidate.IsBot.Value && candidate.Motor != null && candidate.Motor.IsLocal) { localPlayer = candidate; break; }
            var ownShip = localPlayer != null ? localPlayer.Ship : storm != null ? storm.LocalShip : null;
            foreach (var ship in Networking.NetworkShip.ActiveShips)
            {
                if (ship == null || ship.GetComponent<Networking.NetworkSternCargoEvent>() != null) continue;
                var p = ship.transform.position;
                bool own = ship == ownShip;
                bool outside = storm != null && storm.DistanceInside(p) <= 0;
                var point = new Vector2(x + Mathf.Clamp(.5f + p.x / (map.Radius * 2), 0, 1) * size, y + Mathf.Clamp(.5f - p.z / (map.Radius * 2), 0, 1) * size);
                GUI.color = own ? PirateHudStyle.Paper : new Color(.015f, .035f, .045f);
                GUI.DrawTexture(new Rect(point.x - 10, point.y - 10, 20, 20), dot);
                GUI.color = outside ? new Color(1, .32f, .25f) : own ? Color.white : Color.yellow;
                PirateHudStyle.Diamond(point, own ? 11f : 9f, GUI.color);
                var forward = ship.transform.forward;
                Line(point, point + new Vector2(forward.x, -forward.z) * 21, 2);
                if (own) MarkerCaption(point + Vector2.up * 16f, "ВАШ КОРАБЛЬ", PirateHudStyle.Paper, x, y, size);
            }
            if (localPlayer != null)
            {
                var point = Project(localPlayer.transform.position, map, x, y, size);
                point.x = Mathf.Clamp(point.x, x + 8, x + size - 8); point.y = Mathf.Clamp(point.y, y + 8, y + size - 8);
                GUI.color = new Color(.015f, .045f, .055f);
                GUI.DrawTexture(new Rect(point.x - 7, point.y - 7, 14, 14), dot);
                GUI.color = new Color(.3f, 1f, .9f);
                GUI.DrawTexture(new Rect(point.x - 4, point.y - 4, 8, 8), dot);
                var forward = localPlayer.transform.forward;
                var direction = new Vector2(forward.x, -forward.z).normalized;
                var tip = point + direction * 15;
                Line(point, tip, 2);
                Line(tip, tip - direction * 5 + new Vector2(-direction.y, direction.x) * 3, 2);
                Line(tip, tip - direction * 5 - new Vector2(-direction.y, direction.x) * 3, 2);
                MarkerCaption(point - Vector2.up * 27f, "ВЫ", new Color(.3f, 1f, .9f), x, y, size);
            }
            GUI.color = Color.white;
            PirateHudStyle.Label(new Rect(x, y + 3, size, 22), "С", PirateHudStyle.Muted);
            PirateHudStyle.Label(new Rect(x, y + size - 25, size, 22), "Ю", PirateHudStyle.Muted);
            PirateHudStyle.Label(new Rect(x + 3, y + size * .5f - 11, 22, 22), "З", PirateHudStyle.Muted);
            PirateHudStyle.Label(new Rect(x + size - 25, y + size * .5f - 11, 22, 22), "В", PirateHudStyle.Muted);
            PirateHudStyle.Brush(new Rect(x - 16, y + size + 7, size + 32, 2), PirateHudStyle.Gold, true);
            PirateHudStyle.Label(new Rect(x, y + size + 14, size, 20), "Вы — бирюзовый · Корабль — белый · Другие — жёлтый · Шторм — красный", PirateHudStyle.Paper);
            PirateHudStyle.Label(new Rect(x, y + size + 34, size, 20), "Острова — зелёные · Скалы — серые · Ивенты — золотые", PirateHudStyle.Muted);
            GUI.color = old; GUI.depth = oldDepth;
        }
        void MarkerCaption(Vector2 point, string text, Color color, float x, float y, float size)
        {
            var old = GUI.color; GUI.color = Color.white;
            var rect = new Rect(Mathf.Clamp(point.x - 57, x + 2, x + size - 116), Mathf.Clamp(point.y, y + 2, y + size - 23), 114, 21);
            PirateHudStyle.Fill(rect, new Color(.015f, .055f, .07f, .9f));
            PirateHudStyle.Label(rect, text, color);
            GUI.color = old;
        }
        static Vector2 Project(Vector3 position, WorldLayout map, float x, float y, float size) =>
            new Vector2(x + (.5f + position.x / (map.Radius * 2f)) * size, y + (.5f - position.z / (map.Radius * 2f)) * size);

        void CacheMarkers(WorldLayout map)
        {
            markerStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            if (dot == null)
            {
                dot = new Texture2D(24, 24, TextureFormat.RGBA32, false) { name = "WorldMapDot", hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color[24 * 24];
                for (int py = 0; py < 24; py++) for (int px = 0; px < 24; px++)
                    pixels[py * 24 + px] = new Color(1, 1, 1, Mathf.Clamp01(12f - Vector2.Distance(new Vector2(px + .5f, py + .5f), Vector2.one * 12f)));
                dot.SetPixels(pixels); dot.Apply(false, true);
            }
            if (cachedMap == map) return;
            cachedMap = map; scenery.Clear(); events.Clear();
            var catalog = MainOceanMap.IsMain(map) ? Resources.Load<MainOceanScenery>(MainOceanScenery.ResourcePath) : null;
            foreach (var point in map.Points)
            {
                if (point.Tag == MainOceanScenery.Tag && catalog != null)
                {
                    bool island = point.TypeId.EndsWith("Island", System.StringComparison.Ordinal);
                    bool landmark = point.TypeId == "Sea_Lagoon_Cave" || point.TypeId == "SeaArch_Huge_A";
                    scenery.Add(new MapMarker { Position = point.Position, Radius = catalog.Find(point.TypeId).Radius * MainOceanScenery.Scale(point),
                        Color = island ? new Color(.35f, .76f, .5f) : landmark ? new Color(.75f, .66f, .47f) : point.TypeId == "Reef_ShallowField_A" ? new Color(.35f, .65f, .72f) : new Color(.68f, .72f, .75f) });
                }
                else if (point.Tag == MainOceanEvents.Tag)
                {
                    string name = point.Id.Substring("main_event/".Length) + " " + EventName((MainOceanEventKind)point.Rule);
                    events.Add(new MapMarker { Position = point.Position, Name = name, LabelWidth = Mathf.Min(166f, markerStyle.CalcSize(new GUIContent(name)).x + 6f) });
                }
            }
        }

        static string EventName(MainOceanEventKind kind) => kind switch
        {
            MainOceanEventKind.Capture => "Захват", MainOceanEventKind.Raft => "Плот", MainOceanEventKind.Sunken => "Затонувший",
            MainOceanEventKind.Shark => "Акулы", MainOceanEventKind.Skull => "Череп", MainOceanEventKind.Whale => "Кит",
            MainOceanEventKind.Clam => "Раковина", MainOceanEventKind.Balance => "Весы", MainOceanEventKind.Rigging => "Такелаж",
            MainOceanEventKind.SternCargo => "Груз", MainOceanEventKind.CannonBreach => "Форт", MainOceanEventKind.SeagullKey => "Ключ",
            MainOceanEventKind.Platforming => "Платформинг", MainOceanEventKind.Kamikaze => "Камикадзе", _ => "Ивент"
        };

        void OnDestroy() { if (dot != null) Destroy(dot); }
        static void Line(Vector2 a, Vector2 b, float width)
        {
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * .5f, Vector2.Distance(a, b), width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
