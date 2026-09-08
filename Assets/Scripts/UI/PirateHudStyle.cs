using UnityEngine;

namespace PirateSlop
{
    public static class PirateHudStyle
    {
        public static readonly Color Gold = new(.83f, .68f, .39f);
        public static readonly Color Ink = new(.035f, .085f, .105f, .94f);
        public static readonly Color Paper = new(.94f, .91f, .81f);
        public static readonly Color Muted = new(.58f, .68f, .68f);
        static GUIStyle text, title;
        static Texture2D wash, stroke;
        static void Initialize()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            title = new GUIStyle(text) { fontSize = 24, fontStyle = FontStyle.Bold };
            wash = MakeBrush(false); stroke = MakeBrush(true);
        }
        static Texture2D MakeBrush(bool narrow)
        {
            const int w = 256, h = 64;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f), v = y / (h - 1f);
                float noise = Mathf.PerlinNoise(u * 37, v * 7);
                float edge = Mathf.Pow(Mathf.Sin(u * Mathf.PI), narrow ? .25f : .7f);
                float bend = Mathf.Sin(u * 15) * .035f;
                float distance = Mathf.Abs(v - .5f + bend) * 2;
                float alpha = narrow ? Mathf.Clamp01((edge * .8f - distance + (noise - .5f) * .25f) * 12) : Mathf.Pow(Mathf.Clamp01(1 - distance), 1.5f) * edge;
                pixels[y * w + x] = new Color(1, 1, 1, alpha * (narrow ? .8f + noise * .2f : .8f));
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }
        public static void Brush(Rect rect, Color color, bool thin = false)
        {
            Initialize(); var old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, thin ? stroke : wash); GUI.color = old;
        }
        public static void Diamond(Vector2 center, float size, Color color)
        {
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45, center);
            Fill(new Rect(center.x - size / 2, center.y - size / 2, size, size), color);
            GUI.matrix = matrix;
        }
        public static void Fill(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        public static void Label(Rect rect, string value, Color color, bool large = false, TextAnchor align = TextAnchor.MiddleCenter)
        {
            Initialize();
            var style = large ? title : text;
            style.alignment = align;
            style.normal.textColor = new Color(.015f,.025f,.025f,.85f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 2, rect.width, rect.height), value, style);
            style.normal.textColor = color;
            GUI.Label(rect, value, style);
        }
        public static void Panel(Rect rect, string value = "")
        {
            Brush(new Rect(rect.x - 18, rect.y - 10, rect.width + 36, rect.height + 20), Ink);
            if (!string.IsNullOrEmpty(value)) Label(new Rect(rect.x + 10, rect.y + 3, rect.width - 20, rect.height - 6), value, Paper);
        }
        public static void Bar(Rect rect, float fraction, Color color)
        {
            Brush(rect, new Color(0, 0, 0, .65f), true);
            Brush(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color, true);
        }
        public static bool Button(Rect rect, string value)
        {
            bool hover = rect.Contains(Event.current.mousePosition) && GUI.enabled;
            Panel(rect);
            if (hover) Brush(rect, new Color(Gold.r, Gold.g, Gold.b, .25f));
            Label(rect, value, GUI.enabled ? Gold : Muted);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }
    }
}
