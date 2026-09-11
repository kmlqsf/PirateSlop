using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class PlayerHud : MonoBehaviour
    {
        AdvancedPlayerController motor;
        CombatHealth health;
        PlayerInventory inventory;
        PirateWeapon weapon;
        NetworkPlayer network;
        FirearmHandling handling;
        NetworkEquipment equipment;
        float trailingHealth;
        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>(); health = GetComponent<CombatHealth>();
            inventory = GetComponent<PlayerInventory>(); weapon = GetComponent<PirateWeapon>();
            network = GetComponent<NetworkPlayer>(); trailingHealth = health.MaxHealth;
            handling=GetComponent<FirearmHandling>();equipment=GetComponent<NetworkEquipment>();
        }
        void Update() => trailingHealth = Mathf.MoveTowards(trailingHealth, health.Current, Time.deltaTime * 22);
        static readonly string[] CompassDirections = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
        static GUIStyle compassNumber, compassDirection, compassBearing;
        public static void DrawCompass(Camera view)
        {
            if (view == null || !view.enabled || SessionController.MenuOpen) return;
            float scale = Mathf.Clamp(Screen.height / 1080f, .7f, 1.25f);
            float width = Mathf.Min(640f * scale, Screen.width - 32f);
            float center = Screen.width * .5f;
            float top = 12f * scale;
            float heading = Mathf.Repeat(view.transform.eulerAngles.y, 360f);
            if (compassNumber == null)
            {
                compassNumber = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
                compassDirection = new GUIStyle(compassNumber) { fontStyle = FontStyle.Bold };
                compassBearing = new GUIStyle(compassDirection);
            }
            compassNumber.fontSize = Mathf.RoundToInt(12f * scale);
            compassDirection.fontSize = Mathf.RoundToInt(17f * scale);
            compassBearing.fontSize = Mathf.RoundToInt(16f * scale);
            PirateHudStyle.Brush(new Rect(center - width * .5f - 15f, top - 8f, width + 30f, 79f * scale), new Color(.012f, .026f, .031f, .9f));
            PirateHudStyle.Brush(new Rect(center - width * .5f, top + 44f * scale, width, 3f * scale), PirateHudStyle.Gold, true);
            float pixelsPerDegree = width / 120f;
            int first = Mathf.FloorToInt((heading - 60f) / 5f) * 5;
            for (int angle = first; angle <= heading + 60f; angle += 5)
            {
                float offset = (angle - heading) * pixelsPerDegree;
                float fade = Mathf.Clamp01((width * .5f - Mathf.Abs(offset)) / (width * .12f));
                if (fade <= 0f) continue;
                int degrees = (angle % 360 + 360) % 360;
                bool cardinal = degrees % 45 == 0;
                bool numbered = degrees % 15 == 0;
                float x = center + offset;
                Color tint = degrees == 0 ? new Color(.95f, .4f, .3f) : cardinal ? PirateHudStyle.Gold : PirateHudStyle.Paper;
                tint.a = fade;
                float height = (cardinal ? 12f : numbered ? 8f : 4f) * scale;
                PirateHudStyle.Fill(new Rect(x - scale * .5f, top + 44f * scale - height, scale, height), tint);
                if (!numbered) continue;
                compassNumber.normal.textColor = new Color(PirateHudStyle.Paper.r, PirateHudStyle.Paper.g, PirateHudStyle.Paper.b, fade * .85f);
                GUI.Label(new Rect(x - 24f * scale, top + 15f * scale, 48f * scale, 19f * scale), degrees.ToString("000"), compassNumber);
                if (cardinal)
                {
                    compassDirection.normal.textColor = tint;
                    GUI.Label(new Rect(x - 25f * scale, top - 5f * scale, 50f * scale, 23f * scale), CompassDirections[degrees / 45], compassDirection);
                }
            }
            PirateHudStyle.Diamond(new Vector2(center, top + 45f * scale), 7f * scale, PirateHudStyle.Gold);
            var bearing = new Rect(center - 37f * scale, top + 52f * scale, 74f * scale, 25f * scale);
            PirateHudStyle.Brush(new Rect(bearing.x - 10f, bearing.y - 4f, bearing.width + 20f, bearing.height + 8f), PirateHudStyle.Ink);
            compassBearing.normal.textColor = PirateHudStyle.Gold;
            GUI.Label(bearing, (Mathf.RoundToInt(heading) % 360).ToString("000") + "°", compassBearing);
        }
        void OnGUI()
        {
            if (motor.PlayerCamera == null || !motor.PlayerCamera.enabled || (network != null && !network.IsOwner) || SessionController.MenuOpen) return;
            if (ShipSpyglassView.IsViewing) return;
            DrawCompass(motor.PlayerCamera);
            float sideY = Screen.width < 1050 ? Screen.height - 180 : Screen.height - 74;
            var bar = new Rect(65, sideY + 16, 200, 16);
            PirateHudStyle.Brush(new Rect(18, sideY - 24, 285, 105), new Color(.015f,.035f,.04f,.55f));
            PirateHudStyle.Label(new Rect(23, sideY - 3, 38, 45), "♥", health.Current < 30 ? new Color(.95f,.35f,.25f) : PirateHudStyle.Paper, true);
            PirateHudStyle.Bar(bar, trailingHealth / health.MaxHealth, PirateHudStyle.Gold);
            PirateHudStyle.Brush(new Rect(bar.x, bar.y, bar.width * health.Current / health.MaxHealth, bar.height), health.Current < 30 ? new Color(.9f,.3f,.23f) : new Color(.46f,.85f,.65f), true);
            if (health.Current < health.MaxHealth)
                PirateHudStyle.Label(new Rect(65, sideY - 10, 80, 24), $"{health.Current:0}", PirateHudStyle.Paper, false, TextAnchor.MiddleLeft);
            if (motor.BreathFraction < .99f)
            {
                PirateHudStyle.Label(new Rect(30, sideY - 38, 90, 22), "Воздух", PirateHudStyle.Paper);
                PirateHudStyle.Bar(new Rect(115, sideY - 33, 145, 12), motor.BreathFraction, new Color(.55f,.83f,.95f));
            }
            if (health.IsDead)
            {
                PirateHudStyle.Panel(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 55, 400, 100));
                bool eliminated = network != null && network.Eliminated.Value;
                PirateHudStyle.Label(new Rect(Screen.width / 2f - 180, Screen.height / 2f - 43, 360, 35), eliminated ? "ЭКИПАЖ ВЫБЫЛ" : "ВЫ ПАЛИ В БОЮ", PirateHudStyle.Gold, true);
                bool hasRum = network == null || (network.Ship != null && network.Ship.RumCount > 0);
                PirateHudStyle.Label(new Rect(Screen.width / 2f - 180, Screen.height / 2f, 360, 25), eliminated ? "Корабль потерян. Возрождение недоступно." : hasRum ? $"Возвращение через {health.RespawnRemaining:0} с • −1 ром" : "Нет рома — экипаж должен пополнить полку", PirateHudStyle.Paper);
                return;
            }
            if (network != null && network.Ship != null)
                PirateHudStyle.Label(new Rect(24, sideY - 65, 280, 24), $"Ром корабля: {network.Ship.RumCount} возрождений", network.Ship.RumCount > 0 ? PirateHudStyle.Paper : PirateHudStyle.Gold);
            if (inventory.PistolSelected || (equipment!=null && equipment.Active && equipment.Firearm))
            {
                int count=inventory.PistolSelected ? (weapon.Loaded?1:0) : equipment.LoadedRounds;
                bool reload=inventory.PistolSelected ? weapon.Reloading : equipment.IsReloading;
                var ammo = new Rect(Screen.width - 168, sideY - 4, 125, 48);
                PirateHudStyle.Brush(new Rect(ammo.x - 25, ammo.y - 15, 175, 85), new Color(.015f,.035f,.04f,.5f));
                PirateHudStyle.Diamond(new Vector2(ammo.x + 20, ammo.y + 21), 9, count>0 ? PirateHudStyle.Paper : PirateHudStyle.Muted);
                PirateHudStyle.Label(new Rect(ammo.x + 37, ammo.y, 80, 42), count+" / ∞", PirateHudStyle.Paper, true);
                if (count==0 || reload)
                    PirateHudStyle.Label(new Rect(ammo.x - 30, ammo.y - 28, 175, 26), reload ? "Перезарядка…" : "[R]  Зарядить", PirateHudStyle.Paper);
            }
            if (motor.InputActive && !ShipSpyglassView.IsViewing)
            {
                if(equipment!=null && equipment.Scoped) return;
                float x = Screen.width / 2f, y = Screen.height / 2f;
                PirateHudStyle.Fill(new Rect(x - 1, y - 1, 2, 2), PirateHudStyle.Paper);
                if (handling!=null && handling.Available && !handling.Aiming)
                {
                    float radius=handling.ReticleRadius;
                    PirateHudStyle.Fill(new Rect(x-radius-5,y,5,1),PirateHudStyle.Paper);
                    PirateHudStyle.Fill(new Rect(x+radius,y,5,1),PirateHudStyle.Paper);
                    PirateHudStyle.Fill(new Rect(x,y-radius-5,1,5),PirateHudStyle.Paper);
                    PirateHudStyle.Fill(new Rect(x,y+radius,1,5),PirateHudStyle.Paper);
                }
            }
        }
    }
}
