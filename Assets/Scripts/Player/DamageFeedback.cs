using UnityEngine;

namespace PirateSlop
{
    public sealed class DamageFeedback : MonoBehaviour
    {
        AdvancedPlayerController motor;
        PirateSlop.Networking.NetworkPlayer player;
        Texture2D vignette;
        float flash, nextHurtSound, nextHitSound;
        bool Local => player != null ? player.IsOwner : motor != null && motor.InputActive;

        void Awake() { motor = GetComponent<AdvancedPlayerController>(); player = GetComponent<PirateSlop.Networking.NetworkPlayer>(); }

        public void ReceiveDamage(float amount, bool dead)
        {
            if (!Local) return;
            flash = Mathf.Clamp01(flash + Mathf.Lerp(.35f, 1f, Mathf.Clamp01(amount / 50f)));
            if (dead || Time.unscaledTime >= nextHurtSound)
            {
                nextHurtSound = Time.unscaledTime + .15f;
                GameAudio.Play(dead ? SoundCue.Death : SoundCue.Hurt, transform.position, 1.5f, true);
            }
        }

        public void ConfirmHit()
        {
            if (!Local || Time.unscaledTime < nextHitSound) return;
            nextHitSound = Time.unscaledTime + .06f;
            GameAudio.Play(SoundCue.HitConfirm, transform.position, 1f, true);
        }

        void Update() { flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.8f); }

        void OnGUI()
        {
            if (!Local || flash <= 0f || Event.current.type != EventType.Repaint) return;
            if (vignette == null)
            {
                const int size = 64;
                vignette = new Texture2D(size, size, TextureFormat.RGBA32, false);
                vignette.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float edge = Mathf.Max(Mathf.Abs(x / (size - 1f) * 2f - 1f), Mathf.Abs(y / (size - 1f) * 2f - 1f));
                        pixels[y * size + x] = new Color(.85f, .015f, .01f, .06f + .7f * Mathf.Pow(edge, 3f));
                    }
                vignette.SetPixels(pixels); vignette.Apply(false, true);
            }
            Color previous = GUI.color;
            int depth = GUI.depth;
            GUI.depth = -100;
            GUI.color = new Color(1f, 1f, 1f, flash);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignette);
            GUI.color = previous; GUI.depth = depth;
        }

        void OnDestroy() { if (vignette != null) Destroy(vignette); }
    }
}
