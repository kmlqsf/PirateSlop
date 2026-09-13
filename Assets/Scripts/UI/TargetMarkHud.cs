using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public static class TargetMarkHud
    {
        public static void Draw(NetworkPlayer player, Camera view)
        {
            if (player == null || !player.IsOwner || view == null || !view.enabled || SessionController.MenuOpen) return;
            float scale = Mathf.Clamp(Screen.height / 1080f, .7f, 1.25f);
            float width = Mathf.Min(640f * scale, Screen.width - 32f);
            float center = Screen.width * .5f;
            float heading = Mathf.Repeat(view.transform.eulerAngles.y, 360f);
            float elapsed = Time.unscaledTime - player.TargetMarksReceivedAt;
            var marks = player.TargetMarks;
            for (int i = 0; i < marks.Length; i++)
            {
                var mark = marks[i];
                float remaining = mark.SecondsLeft - elapsed;
                if (remaining <= 0f) continue;
                Color tint = PirateHudStyle.Gold;
                tint.a = Mathf.Clamp01(remaining / 5f);
                Vector3 direction = mark.Position - view.transform.position;
                float bearing = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float angle = Mathf.DeltaAngle(heading, bearing);
                float x = center + Mathf.Clamp(angle / 60f, -1f, 1f) * width * .5f;
                int row = 0;
                for (int j = 0; j < i; j++)
                {
                    if (marks[j].SecondsLeft <= elapsed) continue;
                    Vector3 other = marks[j].Position - view.transform.position;
                    float otherAngle = Mathf.DeltaAngle(heading, Mathf.Atan2(other.x, other.z) * Mathf.Rad2Deg);
                    float otherX = center + Mathf.Clamp(otherAngle / 60f, -1f, 1f) * width * .5f;
                    if (Mathf.Abs(otherX - x) < 95f * scale) row++;
                }
                float y = (105f + row * 26f) * scale;
                PirateHudStyle.Diamond(new Vector2(x, 58f * scale), 8f * scale, tint);
                string label = $"{mark.Id} · {mark.Distance:0} м";
                if (Mathf.Abs(angle) > 60f) label = angle < 0f ? "‹ " + label : label + " ›";
                PirateHudStyle.Brush(new Rect(x - 52f * scale, y - 5f, 104f * scale, 30f * scale), PirateHudStyle.Ink);
                PirateHudStyle.Label(new Rect(x - 55f * scale, y, 110f * scale, 22f * scale), label, tint);

                Vector3 screen = view.WorldToViewportPoint(mark.Position);
                if (screen.z <= 0f || screen.x <= 0f || screen.x >= 1f || screen.y <= 0f || screen.y >= 1f) continue;
                Vector2 point = new(screen.x * Screen.width, (1f - screen.y) * Screen.height);
                if (ShipSpyglassView.IsViewing && Vector2.Distance(point, new Vector2(center, Screen.height * .5f)) > Screen.height * .44f) continue;
                PirateHudStyle.Diamond(point, 13f * scale, tint);
                PirateHudStyle.Diamond(point, 7f * scale, PirateHudStyle.Ink);
                PirateHudStyle.Brush(new Rect(point.x - 70f * scale, point.y + 12f * scale, 140f * scale, 53f * scale), PirateHudStyle.Ink);
                PirateHudStyle.Label(new Rect(point.x - 65f * scale, point.y + 15f * scale, 130f * scale, 24f * scale), $"{mark.Id} · {mark.Distance:0} м", tint);
                PirateHudStyle.Label(new Rect(point.x - 50f * scale, point.y + 37f * scale, 100f * scale, 21f * scale), $"{Mathf.CeilToInt(remaining)} с", PirateHudStyle.Paper);
            }
        }
    }
}
