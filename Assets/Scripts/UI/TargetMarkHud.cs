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
                Color tint = mark.Seagull ? mark.CrewColor switch { 1 => new Color(1f, .65f, .2f), 2 => new Color(.95f, .4f, 1f), _ => new Color(.3f, 1f, .9f) } : PirateHudStyle.Gold;
                tint.a = Mathf.Clamp01(remaining / 5f);
                var position = mark.TrackedPosition;
                float distance = mark.Seagull ? Vector3.Distance(view.transform.position, position) : mark.Distance;
                Vector3 direction = position - view.transform.position;
                float bearing = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float angle = Mathf.DeltaAngle(heading, bearing);
                float x = center + Mathf.Clamp(angle / 60f, -1f, 1f) * width * .5f;
                int row = 0;
                for (int j = 0; j < i; j++)
                {
                    if (marks[j].SecondsLeft <= elapsed) continue;
                    Vector3 other = marks[j].TrackedPosition - view.transform.position;
                    float otherAngle = Mathf.DeltaAngle(heading, Mathf.Atan2(other.x, other.z) * Mathf.Rad2Deg);
                    float otherX = center + Mathf.Clamp(otherAngle / 60f, -1f, 1f) * width * .5f;
                    if (Mathf.Abs(otherX - x) < 95f * scale) row++;
                }
                float y = (105f + row * 26f) * scale;
                PirateHudStyle.Diamond(new Vector2(x, 58f * scale), 8f * scale, tint);
                string targetName = mark.Seagull ? "Чайка" : mark.Id.ToString();
                string label = $"{targetName} · {distance:0} м";
                if (Mathf.Abs(angle) > 60f) label = angle < 0f ? "‹ " + label : label + " ›";
                PirateHudStyle.Brush(new Rect(x - 52f * scale, y - 5f, 104f * scale, 30f * scale), PirateHudStyle.Ink);
                PirateHudStyle.Label(new Rect(x - 55f * scale, y, 110f * scale, 22f * scale), label, tint);

                Vector3 screen = view.WorldToViewportPoint(position);
                if (screen.z <= 0f || screen.x <= 0f || screen.x >= 1f || screen.y <= 0f || screen.y >= 1f) continue;
                Vector2 point = new(screen.x * Screen.width, (1f - screen.y) * Screen.height);
                if (ShipSpyglassView.IsViewing && Vector2.Distance(point, new Vector2(center, Screen.height * .5f)) > Screen.height * .44f) continue;
                int overlap = 0;
                if (mark.Seagull)
                    for (int j = 0; j < i; j++)
                        if (marks[j].Seagull && marks[j].SecondsLeft > elapsed && (marks[j].TrackedPosition - position).sqrMagnitude < 1f) overlap++;
                var badge = point + (mark.Seagull ? new Vector2((mark.CrewColor - 1) * 20f * scale, -28f * scale) : Vector2.zero);
                PirateHudStyle.Diamond(badge, 13f * scale, tint);
                PirateHudStyle.Diamond(badge, 7f * scale, PirateHudStyle.Ink);
                if (mark.Seagull)
                {
                    float radius = (20f + overlap * 5f) * scale, length = 8f * scale, line = 2f * scale;
                    for (int side = -1; side <= 1; side += 2) for (int top = -1; top <= 1; top += 2)
                    {
                        float left = point.x + side * radius, edge = point.y + top * radius;
                        PirateHudStyle.Fill(new Rect(left - (side > 0 ? length : 0), edge, length, line), tint);
                        PirateHudStyle.Fill(new Rect(left, edge - (top > 0 ? length : 0), line, length), tint);
                    }
                }
                point.y += overlap * 54f * scale;
                PirateHudStyle.Brush(new Rect(point.x - 70f * scale, point.y + 12f * scale, 140f * scale, 53f * scale), PirateHudStyle.Ink);
                PirateHudStyle.Label(new Rect(point.x - 65f * scale, point.y + 15f * scale, 130f * scale, 24f * scale), $"{targetName} · {distance:0} м", tint);
                PirateHudStyle.Label(new Rect(point.x - 50f * scale, point.y + 37f * scale, 100f * scale, 21f * scale), $"{Mathf.CeilToInt(remaining)} с", PirateHudStyle.Paper);
            }
        }
    }
}
