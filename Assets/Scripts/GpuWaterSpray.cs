using UnityEngine;
using UnityEngine.VFX;

namespace PirateSlop
{
    public sealed class GpuWaterSpray : MonoBehaviour
    {
        static VisualEffectAsset asset;
        static int active;
        VisualEffect effect;
        float stopAt, destroyAt;
        bool stopped;

        public static bool Spawn(Vector3 position, float scale)
        {
            if (!SystemInfo.supportsComputeShaders) return false;
            if (asset == null) asset = Resources.Load<VisualEffectAsset>("CombatVfx/WaterSpray");
            if (asset == null) return false;
            if (active >= 24) return true;
            var root = new GameObject("WaterSpray");
            root.SetActive(false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * Mathf.Clamp(scale, .25f, 4f);
            var spray = root.AddComponent<GpuWaterSpray>();
            spray.effect = root.AddComponent<VisualEffect>();
            spray.effect.visualEffectAsset = asset;
            spray.stopAt = Time.time + .1f;
            spray.destroyAt = Time.time + 1.6f;
            active++;
            root.SetActive(true);
            return true;
        }

        void Update()
        {
            if (!stopped && Time.time >= stopAt) { effect.Stop(); stopped = true; }
            if (Time.time >= destroyAt) Destroy(gameObject);
        }

        void OnDestroy() => active = Mathf.Max(0, active - 1);
    }
}
