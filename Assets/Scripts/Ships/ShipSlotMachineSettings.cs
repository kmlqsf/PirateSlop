using UnityEngine;

namespace PirateSlop.Ships
{
    public enum SlotSymbol : byte { Fish, SkillPoint, Mystery, Cannonball, Weapon, Cannon }

    [CreateAssetMenu(menuName = "PirateSlop/Ship Slot Machine")]
    public sealed class ShipSlotMachineSettings : ScriptableObject
    {
        public float[] WinWeights = { 18.33f, .42f, 8.33f, 7.5f, 4.17f, 1.25f };
        [Min(0)] public float LossWeight = 60f;
        [Min(.2f)] public float IntakeSeconds = 1f;
        [Min(1)] public float FirstReelSeconds = 3.8f;
        [Min(.1f)] public float ReelStopGap = .55f;
        [Min(1)] public float PaidTimeout = 30f;
        [Range(0, 1)] public float SpecialFishChance = .3f;
        [Min(.1f)] public float PrizeGap = .3f;
        [Min(.2f)] public float PrizeFlightSeconds = .7f;
        public float SpinSeconds => FirstReelSeconds + ReelStopGap * 2f;
    }
}
