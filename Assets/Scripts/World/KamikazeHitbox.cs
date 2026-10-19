using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class KamikazeHitbox : MonoBehaviour, IWeaponTarget
    {
        public KamikazeUnit Unit;
        public bool Bomb;

        public void ReceiveWeaponHit(float damage, GameObject attacker)
        {
            if (Unit != null && Unit.Event != null) Unit.ReceiveHit(Bomb ? 2f : damage / Mathf.Max(1f, Unit.Event.BodyHitDamage), attacker);
        }

        public bool HitCannon(CannonShotDamage shot)
        {
            if (Unit == null || shot.Ammo == InventoryItem.BoardingHook) return false;
            if (shot.Authoritative) Unit.ReceiveHit(2f, shot.Attacker);
            return true;
        }
    }
}
