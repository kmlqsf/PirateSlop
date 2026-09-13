using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class CannonRangeHud
    {
        SimpleCannon previous;
        float nextSample, range;
        bool valid, outbound;

        public void Update(SimpleCannon cannon)
        {
            if (cannon == null || cannon.IsMortar || cannon.Muzzle == null) { previous = null; valid = false; return; }
            if (previous == cannon && Time.unscaledTime < nextSample) return;
            previous = cannon;
            nextSample = Time.unscaledTime + .05f;
            var ocean = OceanSurface.Instance;
            valid = ocean != null;
            if (!valid) return;
            Vector3 launch = cannon.ShotPosition, position = launch, velocity = cannon.ShotVelocity;
            outbound = cannon.LoadedAmmo == InventoryItem.BoomerangCannonball;
            float dt = Mathf.Max(.005f, Time.fixedDeltaTime);
            float radius = cannon.ProjectileRadius;
            float duration = outbound ? 2f : 20f;
            valid = false;
            for (float time = 0f; time < duration; time += dt)
            {
                float step = Mathf.Min(dt, duration - time);
                Vector3 nextVelocity = outbound ? velocity : CannonShotDamage.StepVelocity(velocity, step);
                Vector3 next = position + (velocity + nextVelocity) * (.5f * step);
                if (next.y - radius <= ocean.Height(next))
                {
                    float low = 0f, high = 1f;
                    for (int i = 0; i < 8; i++)
                    {
                        float middle = (low + high) * .5f;
                        Vector3 sample = Vector3.Lerp(position, next, middle);
                        if (sample.y - radius > ocean.Height(sample)) low = middle; else high = middle;
                    }
                    position = Vector3.Lerp(position, next, high);
                    valid = true;
                    break;
                }
                position = next;
                velocity = nextVelocity;
            }
            valid |= outbound;
            range = Vector3.ProjectOnPlane(position - launch, Vector3.up).magnitude;
        }

        public void Draw(SimpleCannon cannon)
        {
            if (cannon == null || cannon.IsMortar) return;
            float scale = Mathf.Clamp(Screen.height / 1080f, .7f, 1.25f);
            float x = Screen.width * .5f + 190f * scale;
            float y = Screen.height * .5f;
            float height = 240f * scale;
            PirateHudStyle.Brush(new Rect(x - 28f * scale, y - height * .5f - 65f * scale, 190f * scale, height + 145f * scale), PirateHudStyle.Ink);
            PirateHudStyle.Label(new Rect(x - 40f * scale, y - height * .5f - 47f * scale, 210f * scale, 30f * scale), outbound ? "ДО РАЗВОРОТА" : "ДАЛЬНОСТЬ ДО ВОДЫ", PirateHudStyle.Gold);
            if (!valid)
            {
                PirateHudStyle.Label(new Rect(x, y - 20f, 140f * scale, 40f), "—", PirateHudStyle.Paper, true);
                return;
            }
            float increment = range >= 1000f ? 100f : range >= 300f ? 25f : 10f;
            float pixelsPerMeter = height / (increment * 6f);
            int first = Mathf.Max(0, Mathf.FloorToInt(range / increment) - 3);
            int last = Mathf.CeilToInt(range / increment) + 3;
            PirateHudStyle.Brush(new Rect(x, y - height * .5f, 3f * scale, height), PirateHudStyle.Gold, true);
            for (int i = first; i <= last; i++)
            {
                float distance = i * increment;
                float offset = (range - distance) * pixelsPerMeter;
                if (Mathf.Abs(offset) >= height * .5f) continue;
                float fade = Mathf.Clamp01((height * .5f - Mathf.Abs(offset)) / (35f * scale));
                Color tint = PirateHudStyle.Paper;
                tint.a = fade;
                PirateHudStyle.Fill(new Rect(x, y + offset, 17f * scale, scale), tint);
                if (Mathf.Abs(offset) > 20f * scale)
                    PirateHudStyle.Label(new Rect(x + 22f * scale, y + offset - 11f * scale, 78f * scale, 22f * scale), distance.ToString("0"), tint);
            }
            PirateHudStyle.Diamond(new Vector2(x, y), 9f * scale, PirateHudStyle.Gold);
            PirateHudStyle.Brush(new Rect(x + 12f * scale, y - 22f * scale, 140f * scale, 44f * scale), PirateHudStyle.Ink);
            PirateHudStyle.Label(new Rect(x + 16f * scale, y - 21f * scale, 126f * scale, 42f * scale), $"{range:0} м", PirateHudStyle.Gold, true);
            PirateHudStyle.Label(new Rect(x - 25f * scale, y + height * .5f + 10f * scale, 180f * scale, 25f * scale), $"Наклон {cannon.Elevation:+0.0;-0.0;0.0}°", PirateHudStyle.Paper);
        }
    }
}
