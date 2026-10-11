using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        float floatVelocity;
        SeaLootState lastPhase;
        bool phaseKnown;

        void UpdateObjectivePresentation()
        {
            if (phaseKnown && lastPhase == SeaLootState.Rising && phase.Value == SeaLootState.Ready && IsClientInitialized)
            {
                GameAudio.Play(SoundCue.WaterSplash, transform.position);
                CombatVfx.Splash(transform.position);
            }
            if (phaseKnown && lastPhase == SeaLootState.Locked && phase.Value == SeaLootState.Rising && IsClientInitialized)
            {
                transform.position = anchor.Value;
                GameAudio.Play(SoundCue.UnderwaterBubbles, transform.position);
            }
            phaseKnown = true;
            lastPhase = phase.Value;
            if (Kind == SeaLootKind.Capture) UpdateCaptureStation();
            if (Kind == SeaLootKind.Sunken)
            {
                UpdateSunkenRope();
                UpdateSunkenGulls();
            }
        }

        void FloatChest(Vector3 point)
        {
            var ocean = OceanSurface.Instance;
            float water = ocean != null ? ocean.Height(point) : eventPoint.Value.y;
            float t = ocean != null ? ocean.WaveTime : Time.time;
            float target = water - .12f + Mathf.Sin(t * 1.7f + eventPoint.Value.x) * .035f;
            point.y = Mathf.SmoothDamp(transform.position.y, target, ref floatVelocity, .32f, 10f, Time.deltaTime);
            Vector3 normal = Vector3.up;
            if (ocean != null)
            {
                float x = ocean.Height(point - Vector3.right * .6f) - ocean.Height(point + Vector3.right * .6f);
                float z = ocean.Height(point - Vector3.forward * .6f) - ocean.Height(point + Vector3.forward * .6f);
                normal = new Vector3(x, 1.2f, z).normalized;
            }
            var rotation = Quaternion.FromToRotation(Vector3.up, normal) * facing.Value * Quaternion.Euler(Mathf.Sin(t * 1.2f) * 2f, 0, Mathf.Cos(t * 1.5f) * 2f);
            transform.SetPositionAndRotation(point, Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-4f * Time.deltaTime)));
        }
    }
}
