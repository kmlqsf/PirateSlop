using UnityEngine;

namespace PirateSlop.Performance
{
    public static class WorldVisualBudget
    {
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => Enabled = true;

        public static bool Due(Renderer visual, ref float next)
        {
            if (!Enabled || !Application.isPlaying || visual == null || visual.isVisible) return true;
            if (Time.unscaledTime < next) return false;
            next = Time.unscaledTime + .2f;
            return true;
        }

        public static bool Due(Renderer[] visuals, ref float next)
        {
            if (!Enabled || !Application.isPlaying || visuals == null || visuals.Length == 0) return true;
            foreach (var visual in visuals) if (visual != null && visual.isVisible) return true;
            if (Time.unscaledTime < next) return false;
            next = Time.unscaledTime + .2f;
            return true;
        }
    }
}
