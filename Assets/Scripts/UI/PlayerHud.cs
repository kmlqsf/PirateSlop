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
        void OnGUI()
        {
            if (motor.PlayerCamera == null || !motor.PlayerCamera.enabled || (network != null && !network.IsOwner) || SessionController.MenuOpen) return;
            if (ShipSpyglassView.IsViewing) return;
            float heading = motor.PlayerCamera.transform.eulerAngles.y;
            string[] directions = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
            PirateHudStyle.Label(new Rect(Screen.width / 2f - 70, 20, 140, 28), directions[Mathf.RoundToInt(heading / 45) % 8], PirateHudStyle.Paper);
            PirateHudStyle.Diamond(new Vector2(Screen.width / 2f, 53), 4, PirateHudStyle.Gold);
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
                PirateHudStyle.Label(new Rect(Screen.width / 2f - 180, Screen.height / 2f - 43, 360, 35), "ВЫ ПАЛИ В БОЮ", PirateHudStyle.Gold, true);
                bool hasRum = network == null || (network.Ship != null && network.Ship.RumCount > 0);
                PirateHudStyle.Label(new Rect(Screen.width / 2f - 180, Screen.height / 2f, 360, 25), hasRum ? $"Возвращение через {health.RespawnRemaining:0} с • −1 ром" : "Нет рома — экипаж должен пополнить полку", PirateHudStyle.Paper);
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
