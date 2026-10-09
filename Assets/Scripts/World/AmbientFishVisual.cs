using UnityEngine;

namespace PirateSlop.World
{
    public sealed class AmbientFishVisual : MonoBehaviour
    {
        public int Species;
        public Transform[] TailBones;
        public SkinnedMeshRenderer Body;
        public float Length;
        public float BodyRadius;

        public void Swim(float phase, float speed, float turn)
        {
            if (TailBones == null) return;
            float power = Mathf.Clamp(speed, .55f, 1.35f);
            turn = Mathf.Clamp(turn, -8f, 8f);
            for (int i = 1; i < TailBones.Length; i++)
            {
                float wave = Mathf.Sin(phase * Mathf.PI * 2f - i * .6f);
                float amplitude = (Species == 1 ? 2f : Species == 2 ? 1.7f : 2.2f) + i * 1.05f;
                TailBones[i].localRotation = Quaternion.Euler(0f, wave * amplitude * power + turn * .25f, 0f);
            }
        }
    }
}
