using UnityEngine;

namespace PirateSlop.World
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SandyIslandFoliageWind : MonoBehaviour
    {
        public float Height;
        public float Strength = .12f;
        public float Phase;
        public float SwayDegrees = .65f;
        Transform visual;
        Quaternion restRotation;
        Vector3 appliedPosition;
        Renderer[] visuals;
        bool visualOnly;
        float nextVisualUpdate;

        void OnEnable()
        {
            visual = transform.Find("CoastalVisual");
            if (visual != null) restRotation = visual.localRotation;
            visuals = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : null;
            visualOnly = visual != null && visual.GetComponentsInChildren<Collider>(true).Length == 0;
            Apply();
        }
        void Start() => Apply();

        void LateUpdate()
        {
            if (transform.position != appliedPosition) Apply();
            if (!Application.isPlaying || visual == null) return;
            if (visualOnly && !PirateSlop.Performance.WorldVisualBudget.Due(visuals, ref nextVisualUpdate)) return;
            float time = (float)(Time.timeAsDouble % 4096);
            float phase = Phase + transform.position.x * .071f + transform.position.z * .053f;
            float gust = Mathf.Sin(time * .83f + phase) + .23f * Mathf.Sin(time * 1.37f + phase * .7f);
            visual.localRotation = Quaternion.Euler(gust * SwayDegrees * .39f, 0, -gust * SwayDegrees * .92f) * restRotation;
        }

        void OnDisable()
        {
            if (visual != null) visual.localRotation = restRotation;
        }

        public void Apply()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            float height = Height;
            if (height <= 0)
                foreach (var renderer in renderers)
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_WindStrength"))
                        height = Mathf.Max(height, renderer.bounds.max.y - transform.position.y);
            var block = new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetVector("_WindRoot", transform.position);
                block.SetFloat("_WindHeight", Mathf.Max(.3f, height));
                block.SetFloat("_WindStrength", Strength);
                block.SetFloat("_WindPhase", Phase);
                renderer.SetPropertyBlock(block);
            }
            appliedPosition = transform.position;
        }
    }
}
