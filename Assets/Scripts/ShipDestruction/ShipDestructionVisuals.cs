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
            pool = GetComponent<ShipDebrisPool>();
            pool?.Initialize(value);
        }
        public void Present(ShipDestructionEvent impact)
        {
            if (owner == null) return;
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
