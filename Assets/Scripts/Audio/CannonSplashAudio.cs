using UnityEngine;

namespace PirateSlop
{
    public sealed class CannonSplashAudio : MonoBehaviour
    {
        public float WaterHeight;
        void Update()
        {
            if (OceanSurface.Instance != null) WaterHeight = OceanSurface.Instance.Height(transform.position);
            if (transform.position.y > WaterHeight) return;
            GameAudio.Play(SoundCue.Splash, new Vector3(transform.position.x, WaterHeight, transform.position.z));
            Destroy(gameObject);
        }
    }
}
