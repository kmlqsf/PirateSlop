using UnityEngine;

namespace PirateSlop
{
    public sealed class ShipDestructionVisuals : MonoBehaviour
    {
        ShipDestruction owner;
        ShipDebrisPool pool;
        public void Initialize(ShipDestruction value)
        {
            owner = value;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (owner.GetComponent<PirateSlop.Experimental.UpperDestructionV2>() != null) return;
#endif
            pool = GetComponent<ShipDebrisPool>();
            pool?.Initialize(value);
        }
        public void Present(ShipDestructionEvent impact)
        {
            if (owner == null) return;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (owner.Upper != null) { owner.Upper.Present(impact); return; }
#endif
            var section = owner.Section(impact.SectionId);
            var point = transform.TransformPoint(impact.LocalPoint);
            if (impact.Reason == ShipDamageReason.Hit) pool?.SpawnSplinters(impact);
            if (impact.Previous != impact.Current || impact.DetachedFragments != 0) GameAudio.Play(owner.Profile.BreakSound, point);
            if (section != null && (impact.DetachedFragments != 0 || section.Fragments.Length == 0 && impact.Current == ShipSectionState.Destroyed && impact.Previous != impact.Current))
            {
                pool?.Spawn(section, impact);
                GameAudio.Play(owner.Profile.FallSound, section.transform.position);
            }
        }
        public void Clear() => pool?.Clear();
    }
}
