using UnityEngine;

namespace PirateSlop
{
    public sealed class HolyGrenadeFuse : MonoBehaviour
    {
        public Transform Wick;
        public Transform Ember;
        Vector3 wickScale;
        void Awake() { if (Wick != null) wickScale = Wick.localScale; }
        public void SetBurn(bool burning, float remaining)
        {
            if (Wick == null || Ember == null) return;
            if (wickScale == Vector3.zero) wickScale = Wick.localScale;
            float length = burning ? Mathf.Max(.05f, Mathf.Clamp01(remaining)) : 1f;
            Wick.localScale = new Vector3(wickScale.x, wickScale.y * length, wickScale.z);
            Ember.gameObject.SetActive(burning);
            Ember.position = Wick.TransformPoint(new Vector3(-.012f, .09f, 0));
            Ember.localScale = Vector3.one * (.014f + .006f * Mathf.Abs(Mathf.Sin(Time.time * 43f)));
        }
        public static void Burst(Vector3 point, HolyGrenadeFuse model)
        {
            if (model == null || model.Ember == null) return;
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "HolyGrenadeFlash";
            Destroy(flash.GetComponent<Collider>());
            flash.transform.position = point;
            flash.transform.localScale = Vector3.one * .7f;
            flash.GetComponent<Renderer>().sharedMaterial = model.Ember.GetComponent<Renderer>().sharedMaterial;
            var light = flash.AddComponent<Light>();
            light.color = Color.white;
            light.range = 7f;
            light.intensity = 8f;
            Destroy(flash, .12f);
        }
    }
}
