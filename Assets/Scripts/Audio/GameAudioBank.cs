using UnityEngine;

namespace PirateSlop
{
    public enum SoundCue { Pistol, Cannon, Knife, Reload, ReloadReady, DryFire, Footstep, Jump, Land, Slide, Hurt, Death, Respawn, Wheel, Sail, Barrel, Load, Pickup, Place, Select, Impact, ShipHit, ShipDeath, ShipCollision, Splash, Creak, FishingCast, FishingBite, FishingReel, FishingCatch, FishDrop, FishEat, FishingEscape, HitConfirm, Musket, DoubleBarrel, BulletWood, BulletMetal, BulletStone, BulletFlesh }

    [CreateAssetMenu(menuName = "PirateSlop/Audio Bank")]
    public sealed class GameAudioBank : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public SoundCue Cue;
            public AudioClip[] Clips;
            [Range(0f, 1f)] public float Volume = .5f;
            public float Distance = 25f;
        }
        public Entry[] Entries;
        public AudioClip Ocean, Wind;
        [Range(0f, 1f)] public float Master = .8f, Effects = .8f, Ambience = .25f, Interface = .45f;
    }
}
