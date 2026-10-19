#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public sealed class UpperWeaponVisual : MonoBehaviour
    {
        Transform pivot, visual;
        Vector3 axis;
        readonly List<MeshRenderer> sources = new();
        readonly List<MeshRenderer> displays = new();
        readonly List<bool> originalVisibility = new();
        bool showing;
        bool wasDisabled;
        float hitAt = -100f;
        uint revision;

        public void Initialize(Transform source, Vector3 rotationAxis)
        {
            pivot = source; axis = rotationAxis.normalized;
            visual = new GameObject("DisabledWeaponVisual").transform;
            visual.SetParent(transform, false);
            visual.SetPositionAndRotation(pivot.position, pivot.rotation);
            foreach (var renderer in pivot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<Cannonball>() != null) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                sources.Add(renderer); originalVisibility.Add(renderer.forceRenderingOff);
                var copy = new GameObject(renderer.name);
                copy.transform.SetParent(visual, false);
                copy.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                copy.transform.localScale = new Vector3(renderer.transform.lossyScale.x / visual.lossyScale.x, renderer.transform.lossyScale.y / visual.lossyScale.y, renderer.transform.lossyScale.z / visual.lossyScale.z);
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var display = copy.AddComponent<MeshRenderer>(); display.sharedMaterials = renderer.sharedMaterials;
                display.shadowCastingMode = renderer.shadowCastingMode; display.receiveShadows = renderer.receiveShadows;
                displays.Add(display);
            }
            visual.gameObject.SetActive(false);
        }

        public void Hit(uint value)
        {
            if (revision == value) return;
            revision = value; hitAt = Time.time;
        }

        public Vector3 ProjectPoint(Vector3 point) => showing && pivot != null && visual != null ? visual.TransformPoint(pivot.InverseTransformPoint(point)) : point;

        public void Present(double until, double clock, bool broken)
        {
            bool disabled = until > clock;
            bool show = disabled || broken;
            if (show != showing)
            {
                showing = show;
                for (int i = 0; i < sources.Count; i++) if (sources[i] != null) sources[i].forceRenderingOff = show || originalVisibility[i];
                visual.gameObject.SetActive(show);
            }
            if (wasDisabled && !disabled && !broken) GameAudio.Play(SoundCue.ReloadReady, transform.position, .75f);
            wasDisabled = disabled;
            if (!show || pivot == null) return;
            for (int i = 0; i < sources.Count; i++) displays[i].enabled = sources[i] != null && sources[i].enabled && sources[i].gameObject.activeInHierarchy;
            float age = Mathf.Max(0f, 15f - (float)(until - clock));
            float remaining = Mathf.Max(0f, (float)(until - clock));
            float sag = broken ? 32f : 18f * Mathf.SmoothStep(0f, 1f, age / .3f) * Mathf.SmoothStep(0f, 1f, remaining / .6f);
            float attempt = broken ? 0f : Mathf.Pow(Mathf.Max(0f, Mathf.Sin(age * Mathf.PI)), 8f) * 5f;
            float hitAge = Time.time - hitAt;
            float jerk = hitAge < .5f ? Mathf.Sin(hitAge * 45f) * Mathf.Exp(-hitAge * 9f) * 8f : 0f;
            visual.SetPositionAndRotation(pivot.position, pivot.rotation * Quaternion.AngleAxis(sag - attempt + jerk, axis));
        }

        void OnDestroy()
        {
            for (int i = 0; i < sources.Count; i++) if (sources[i] != null) sources[i].forceRenderingOff = originalVisibility[i];
            if (visual != null) Destroy(visual.gameObject);
        }
    }
}
#endif
