using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class FortGateTarget : MonoBehaviour
    {
        public NetworkCannonBreachEvent Owner;
        public int GateIndex;
        public bool Hit(CannonShotDamage shot, Vector3 point) => Owner != null && Owner.Hit(GateIndex, shot, point);
    }
}
