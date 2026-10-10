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
        bool testMotion;

        public void ConfigureForTest(bool enabled)
        {
            testMotion = enabled;
            if (enabled && Species == 0 && TailBones != null && TailBones.Length > 0)
                TailBones[0].localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        public void Swim(float phase, float speed, float turn)
        {
            if (TailBones == null) return;
            float power = Mathf.Clamp(speed, .55f, 1.35f);
            turn = Mathf.Clamp(turn, -8f, 8f);
            for (int i = 1; i < TailBones.Length; i++)
            {
                float wave = Mathf.Sin(phase * Mathf.PI * 2f - i * (testMotion ? .38f : .6f));
                float amplitude = testMotion
                    ? (Species == 1 ? 3f : Species == 2 ? 4f : 5f) + i * 1.8f
                    : (Species == 1 ? 2f : Species == 2 ? 1.7f : 2.2f) + i * 1.05f;
                float angle = wave * amplitude * power + turn * .25f;
                TailBones[i].localRotation = testMotion && Species == 0
                    ? Quaternion.AngleAxis(angle, Vector3.right)
                    : Quaternion.Euler(0f, angle, 0f);
            }
        }
    }
}
