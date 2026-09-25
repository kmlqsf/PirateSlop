using UnityEngine;

namespace PirateSlop
{
    public sealed class WhaleChest : MonoBehaviour
    {
        public Transform Lid;
        public WhaleLootPoint Owner;

        bool opened;
        float openProgress;

        public bool IsOpened => opened;
        public bool CanOpen => Owner != null && Owner.RemainingHarpoons == 0 && !opened;

        public void Interact(AdvancedPlayerController player)
        {
            if (opened) return;

            if (Owner != null && Owner.RemainingHarpoons > 0)
            {
                GameAudio.Play(SoundCue.DryFire, transform.position);
                return;
            }

            opened = true;
            GameAudio.Play(SoundCue.ChestOpen, transform.position);

            if (Owner != null)
            {
                Owner.OnChestOpened(player);
            }
        }

        void Update()
        {
            if (opened && openProgress < 1f)
            {
                openProgress = Mathf.MoveTowards(openProgress, 1f, Time.deltaTime * 2.2f);
                float angle = Mathf.Lerp(0f, 105f, Mathf.SmoothStep(0f, 1f, openProgress));
                if (Lid != null)
                {
                    Lid.localRotation = Quaternion.Euler(angle, 0f, 0f);
                }
            }
        }
    }
}
