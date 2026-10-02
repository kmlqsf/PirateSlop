using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class SkullMouthTarget : MonoBehaviour
    {
        public NetworkSkullEvent Event;

        public bool Hit(CannonShotDamage shot, Vector3 point)
        {
            return Event != null && Event.HitMouth(shot, point);
        }
    }
}
