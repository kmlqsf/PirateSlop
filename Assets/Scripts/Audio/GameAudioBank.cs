using UnityEngine;

namespace PirateSlop
{
    public enum SoundCue { Pistol, Cannon, Knife, Reload, ReloadReady, DryFire, Footstep, Jump, Land, Slide, Hurt, Death, Respawn, Wheel, Sail, Barrel, Load, Pickup, Place, Select, Impact, ShipHit, ShipDeath, ShipCollision, Splash, Creak, FishingCast, FishingBite, FishingReel, FishingCatch, FishDrop, FishEat, FishingEscape, HitConfirm, Musket, DoubleBarrel, BulletWood, BulletMetal, BulletStone, BulletFlesh, HookThrow, HookTension, HookRelease, FootstepWood, FootstepWoodRun, FootstepStone, ChestOpen, ChestClose, SwordEquip, SwordSheathe, BottleOpen, BottleClose, WaterSplash, PufferThrow, PufferBurst, SwordfishThrow, SwordfishStick, ShipBell, PufferWarning, UnderwaterBubbles, AirWarning, HolyFlash, WheelReverseRope, WheelIdleLeather, CannonFuse, CannonballDispense, CannonballRoll, CannonballDrop, FireCannonballHeld, IceCannonballHeld, PushCannonballHeld, LockpickStart, LockpickMove, LockpickTurn, LockpickJam, LockpickBreak, LockpickSuccess, DiceSlide, DiceImpact, DiceCup, FlameLight, FlameExtinguish, SabreWood, BottleBreak, BarricadeBreak, SwimStroke, DiveEntry, KnockdownBody, WaterRunSteps, AirJump, LowHealth, DrownDamage, WineDrink, FirearmDraw, FirearmAim, BulletPassby, BulletNearbyImpact, SabreFlesh, HookHitFlesh, FogRelease, VortexLoop, ParrotLaunch, ParrotWings, ParrotExplosion, KrakenAppear, KrakenThreat, KrakenSlam, WhirlpoolNear, UISlider, UpgradeAward, UpgradeHover, UpgradeLegendary, UpgradeEpic, UpgradeRare, CoinSave, PactRevive, GhostWave, MatchDefeat }

    [CreateAssetMenu(menuName = "PirateSlop/Audio Bank")]
    public sealed class GameAudioBank : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public SoundCue Cue;
            public AudioClip[] Clips;
            public AudioClip[] DistantClips;
            public float DistantStart = 25f, DistantEnd = 65f;
            [Range(0f, 1f)] public float DistantVolume = .65f;
            [Range(0f, 1f)] public float Volume = .5f;
            public float Distance = 25f;
        }
        public Entry[] Entries;
        public AudioClip Ocean, Wind, DeckCreaks, Storm;
        public AudioClip[] StormThunder;
        public AudioClip MainMenuMusic, SailingMusic, CombatMusic;
        [Range(0f, 1f)] public float Master = .8f, Effects = .8f, Ambience = .25f, Interface = .45f, Music = .2f;
        [Range(0f, 2f)] public float OceanLevel = .7f, WindLevel = 1f, DeckLevel = 1f, StormLevel = 1.26f, ThunderLevel = .63f, RainLevel = 1f;
        [Range(0f, 2f)] public float UnderwaterLevel = 1f, FloodingLevel = 1f;
        [Range(0f, 2f)] public float MainMenuMusicLevel = 1f, SailingMusicLevel = 1f, CombatMusicLevel = 1f;
    }
}


