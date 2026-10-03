using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed class ShipV3InteractionTarget : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<ShipV3InteractionTarget> active = new();
        public static System.Collections.Generic.IReadOnlyList<ShipV3InteractionTarget> Active => active;
        public ShipV3Features Ship;
        public ShipV3TargetKind Kind;
        public int Index;
        void OnEnable() { active.Add(this); }
        void OnDisable() { active.Remove(this); }
    }
}
