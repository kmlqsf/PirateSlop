using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed class ShipV3RenderBudget : MonoBehaviour
    {
        static readonly List<ShipV3RenderBudget> active = new();
        static Light shadowLight;
        static float nextShadowSelection;
        Light[] lights;
        Camera view;
        public float DistanceSquared { get; private set; }
        public float VisualInterval => DistanceSquared < 6400f ? 0f : DistanceSquared < 40000f ? .1f : .25f;
        public bool LocalLightsVisible => DistanceSquared < 10000f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { active.Clear(); shadowLight = null; nextShadowSelection = 0f; }

        void Awake() => lights = GetComponentsInChildren<Light>(true);
        void OnEnable() => active.Add(this);
        void OnDisable()
        {
            active.Remove(this);
            if (shadowLight != null && shadowLight.transform.IsChildOf(transform))
            {
                shadowLight.shadows = LightShadows.None;
                shadowLight = null;
            }
        }
        void LateUpdate()
        {
            if (Time.unscaledTime < nextShadowSelection) return;
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            if (view == null) return;
            nextShadowSelection = Time.unscaledTime + .2f;
            Light nearest = null;
            float best = 64f;
            foreach (var budget in active)
            {
                budget.DistanceSquared = (budget.transform.position - view.transform.position).sqrMagnitude;
                foreach (var lamp in budget.lights)
                {
                    if (lamp == null || !lamp.isActiveAndEnabled) continue;
                    float score = (lamp.transform.position - view.transform.position).sqrMagnitude;
                    if (score < best) { best = score; nearest = lamp; }
                }
            }
            if (shadowLight == nearest) return;
            if (shadowLight != null) shadowLight.shadows = LightShadows.None;
            shadowLight = nearest;
            if (shadowLight != null) shadowLight.shadows = LightShadows.Soft;
        }
    }
}
