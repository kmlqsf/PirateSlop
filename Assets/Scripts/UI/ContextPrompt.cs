using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(10000)]
    public sealed class ContextPrompt : MonoBehaviour
    {
        static string current;
        static int frame = -1, rank;
        AdvancedPlayerController motor;
        void Awake() => motor = GetComponent<AdvancedPlayerController>();
        public static void Offer(string value, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (frame != Time.frameCount) { frame = Time.frameCount; rank = int.MinValue; current = null; }
            if (priority < rank) return;
            rank = priority; current = value;
        }
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || frame != Time.frameCount || !motor.InputActive || PlayerInventory.LootWindowOpen || string.IsNullOrEmpty(current)) return;
            Draw(current);
        }
        public static void Draw(string text)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            float scale = Mathf.Clamp(Screen.height / 1080f, .65f, 1.25f);
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            string[] rows = text.Replace(" • ", " · ").Split(new[] { " · ", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            float width = 650f, height = rows.Length * 31f + 18f;
            var panel = new Rect(Screen.width / scale * .5f - width * .5f, Screen.height / scale - 190f - height, width, height);
            PirateHudStyle.Brush(panel, PirateHudStyle.Ink);
            PirateHudStyle.Brush(new Rect(panel.x + 35, panel.y, width - 70, 3), PirateHudStyle.Gold, true);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = new Rect(panel.x + 28, panel.y + 10 + i * 31, width - 56, 29);
                int split = rows[i].IndexOf(" — ", System.StringComparison.Ordinal);
                if (split > 0 && split <= 18)
                {
                    float keyWidth = Mathf.Clamp(split * 10 + 20, 52, 190);
                    PirateHudStyle.Fill(new Rect(row.x, row.y + 3, keyWidth, 23), new Color(.35f,.28f,.14f,.7f));
                    PirateHudStyle.Label(new Rect(row.x, row.y, keyWidth, row.height), rows[i].Substring(0, split), PirateHudStyle.Gold);
                    PirateHudStyle.Label(new Rect(row.x + keyWidth + 12, row.y, row.width - keyWidth - 12, row.height), rows[i].Substring(split + 3), PirateHudStyle.Paper, false, TextAnchor.MiddleLeft);
                }
                else PirateHudStyle.Label(row, rows[i], i == 0 ? PirateHudStyle.Gold : PirateHudStyle.Paper);
            }
            GUI.matrix = matrix;
        }
    }
}
