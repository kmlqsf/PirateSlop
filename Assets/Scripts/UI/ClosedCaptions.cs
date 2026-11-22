using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public sealed class ClosedCaptions : MonoBehaviour
    {
        struct Caption { public SoundCue Cue; public string Text; public Vector3 Position; public bool Positional; public float Until; }

        const float Lifetime = 2.6f;
        const int MaxVisible = 5;
        static ClosedCaptions instance;
        static readonly Dictionary<SoundCue, string> texts = new()
        {
            { SoundCue.Cannon, "Пушечный выстрел" },
            { SoundCue.ShipHit, "Попадание по кораблю" },
            { SoundCue.ShipDeath, "Корабль тонет" },
            { SoundCue.ShipCollision, "Столкновение кораблей" },
            { SoundCue.Pistol, "Выстрел" },
            { SoundCue.Musket, "Выстрел из мушкета" },
            { SoundCue.DoubleBarrel, "Выстрел дробовика" },
            { SoundCue.BulletPassby, "Пуля пролетела рядом" },
            { SoundCue.CannonFuse, "Горит фитиль" },
            { SoundCue.ShipBell, "Звон колокола" },
            { SoundCue.KrakenAppear, "Появился кракен" },
            { SoundCue.KrakenThreat, "Рёв кракена" },
            { SoundCue.KrakenSlam, "Удар щупальца" },
            { SoundCue.WhirlpoolNear, "Рёв водоворота" },
            { SoundCue.SharkChase, "Акулы рядом" },
            { SoundCue.SharkBite, "Укус акулы" },
            { SoundCue.PufferWarning, "Фугу раздувается" },
            { SoundCue.AirWarning, "Кончается воздух" },
            { SoundCue.LowHealth, "Мало здоровья" },
            { SoundCue.HolyFlash, "Взрыв гранаты" },
            { SoundCue.ParrotExplosion, "Взрыв попугая" },
            { SoundCue.FireStaffImpact, "Огненный удар" },
            { SoundCue.IceStaffImpact, "Ледяной удар" },
            { SoundCue.LightningStaffImpact, "Удар молнии" },
            { SoundCue.CaptureStart, "Начался захват" },
            { SoundCue.CaptureContested, "Захват оспаривается" },
            { SoundCue.CaptureVictory, "Захват завершён" },
            { SoundCue.SkullGrowl, "Рык черепа" },
            { SoundCue.BarricadeBreak, "Баррикада сломана" },
            { SoundCue.Splash, "Всплеск" }
        };
        static readonly Dictionary<SoundCue, float> lastShown = new();
        readonly List<Caption> active = new();
        GUIStyle style;

        public static void Report(SoundCue cue, Vector3 position, bool ui, float audibleDistance)
        {
            if (!texts.TryGetValue(cue, out var text)) return;
            float now = Time.unscaledTime;
            if (lastShown.TryGetValue(cue, out float last) && now - last < 1.2f) return;
            var ear = SpatialAudioTone.FindListener();
            if (!ui && ear != null && audibleDistance > 0f && Vector3.Distance(ear.transform.position, position) > audibleDistance) return;
            lastShown[cue] = now;
            if (instance == null)
            {
                var host = new GameObject("ClosedCaptions") { hideFlags = HideFlags.DontSave };
                DontDestroyOnLoad(host);
                instance = host.AddComponent<ClosedCaptions>();
            }
            var list = instance.active;
            for (int i = list.Count - 1; i >= 0; i--) if (list[i].Cue == cue) list.RemoveAt(i);
            list.Add(new Caption { Cue = cue, Text = text, Position = position, Positional = !ui, Until = now + Lifetime });
            if (list.Count > MaxVisible) list.RemoveAt(0);
        }

        static string Direction(Vector3 position)
        {
            var view = Camera.main;
            if (view == null) return "";
            var local = view.transform.InverseTransformPoint(position);
            if (new Vector2(local.x, local.z).sqrMagnitude < 4f) return "";
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (angle > -30f && angle < 30f) return " ↑";
            if (angle >= 30f && angle < 150f) return " →";
            if (angle <= -30f && angle > -150f) return " ←";
            return " ↓";
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !GameSettings.Captions) return;
            float now = Time.unscaledTime;
            active.RemoveAll(caption => caption.Until <= now);
            if (active.Count == 0) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(16, Screen.height / 48), alignment = TextAnchor.MiddleCenter, wordWrap = false };
            float lineHeight = style.fontSize * 1.6f;
            float y = Screen.height * .78f - active.Count * lineHeight;
            int depth = GUI.depth; GUI.depth = -90;
            for (int i = 0; i < active.Count; i++)
            {
                var caption = active[i];
                string text = "[" + Localization.T(caption.Text) + (caption.Positional ? Direction(caption.Position) : "") + "]";
                var size = style.CalcSize(new GUIContent(text));
                var rect = new Rect((Screen.width - size.x) * .5f - 12f, y + i * lineHeight, size.x + 24f, lineHeight - 4f);
                float alpha = Mathf.Clamp01((caption.Until - now) / .4f);
                PirateHudStyle.Fill(rect, new Color(0f, 0f, 0f, .62f * alpha));
                style.normal.textColor = new Color(1f, 1f, 1f, alpha);
                GUI.Label(rect, text, style);
            }
            GUI.depth = depth;
        }
    }
}
