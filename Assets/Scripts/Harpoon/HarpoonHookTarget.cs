using UnityEngine;

namespace PirateSlop.Harpoon
{
    public class HarpoonHookTarget : BoardingHookTarget
    {
        public HarpoonProjectile Projectile;
        int strikes;

        public override void Strike(GameObject attacker)
        {
            if (Projectile == null || !Projectile.IsAttached) return;
            strikes++;
            GameAudio.Play(SoundCue.BulletMetal, transform.position, 0.7f);
            if (strikes >= 2)
            {
                Projectile.DetachAndRewind();
            }
        }
    }
}
