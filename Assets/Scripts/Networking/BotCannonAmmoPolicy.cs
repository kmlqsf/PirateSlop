using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        internal bool BotBoardingAdvantage(NetworkShip own, NetworkShip target)
        {
            if (own == null || target == null) return false;
            int allies = 0, enemies = 0;
            foreach (var member in players.Values)
            {
                if (member == null || member.Motor.IsDead || member.Eliminated.Value) continue;
                if (member.Ship == own) allies++;
                if (member.Ship == target) enemies++;
            }
            return allies > enemies && allies >= 2;
        }

        internal bool BotAllyInShotSegment(Vector3 from, Vector3 to, float radius, float future, NetworkPlayer observer, bool ignoreObserver = false)
        {
            var line = to - from;
            foreach (var ally in players.Values)
            {
                if (ally == null || ignoreObserver && ally == observer || ally.Motor.IsDead || ally.TeamId.Value != observer.TeamId.Value) continue;
                var point = ally.transform.position + Vector3.up;
                if (ally.Ship != null && ally.Passenger.Ship == ally.Ship.Body)
                    point += ally.Ship.Motor.CannonPointVelocity(ally.transform.position) * future;
                var closest = from + line * Mathf.Clamp01(Vector3.Dot(point - from, line) / Mathf.Max(.001f, line.sqrMagnitude));
                if ((closest - point).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }
        internal bool BotAllyNear(Vector3 point, float radius, NetworkPlayer observer)
        {
            foreach (var ally in players.Values)
                if (ally != null && !ally.Motor.IsDead && ally.TeamId.Value == observer.TeamId.Value &&
                    (ally.transform.position - point).sqrMagnitude < radius * radius) return true;
            return false;
        }
    }
    public static class BotCannonAmmoPolicy
    {
        public static bool Supported(InventoryItem ammo) => ammo == InventoryItem.Cannonball || ammo == InventoryItem.FireCannonball ||
            ammo == InventoryItem.IceCannonball || ammo == InventoryItem.PushCannonball || ammo == InventoryItem.BoomerangCannonball || ammo == InventoryItem.BoardingHook;
        public static float Score(InventoryItem ammo, float distance)
        {
            if (ammo == InventoryItem.Cannonball) return 10f;
            if (ammo == InventoryItem.BoardingHook) return distance >= 25f && distance <= 50f ? 22f : -500f;
            if (ammo == InventoryItem.BoomerangCannonball) return distance >= 30f && distance <= 55f ? 25f : 1f;
            if (ammo == InventoryItem.IceCannonball) return distance >= 90f ? 35f : 12f;
            if (ammo == InventoryItem.PushCannonball) return distance < 75f ? 40f : 8f;
            if (ammo == InventoryItem.FireCannonball) return distance >= 70f && distance <= 120f ? 30f : 5f;
            return -1000f;
        }
        public static float SafetyRadius(InventoryItem ammo) => ammo == InventoryItem.FireCannonball ? CannonAmmo.MortarBlastRadius(ammo) + 3f :
            ammo == InventoryItem.PushCannonball ? 20f : 3f;
        public static string Purpose(InventoryItem ammo) => ammo == InventoryItem.FireCannonball ? "поджог" :
            ammo == InventoryItem.IceCannonball ? "заморозка" : ammo == InventoryItem.PushCannonball ? "отталкивание" :
            ammo == InventoryItem.BoomerangCannonball ? "возвращающееся ядро" : ammo == InventoryItem.BoardingHook ? "абордажный трос" : "урон корпусу";
    }
}
