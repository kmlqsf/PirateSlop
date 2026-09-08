using UnityEngine;

namespace PirateSlop
{
    public sealed class PistolBullet : MonoBehaviour
    {
        FirearmShot shot;
        Vector3 start;
        LineRenderer tracer;
        Material material;
        float age, duration;
        bool impacted;

        public void Initialize(Vector3 visibleStart, FirearmShot result, Material template)
        {
            start = visibleStart; shot = result;
            duration = Mathf.Clamp(Vector3.Distance(start, shot.End) / 400f, .04f, .18f);
            material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = new Color(1f, .85f, .45f);
            tracer = gameObject.AddComponent<LineRenderer>();
            tracer.sharedMaterial = material;
            tracer.useWorldSpace = true; tracer.positionCount = 2;
            tracer.startWidth = .025f; tracer.endWidth = .045f;
            tracer.numCapVertices = 2;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            Draw(.01f);
            Destroy(gameObject, duration + .1f);
        }

        void Draw(float time)
        {
            float length = Vector3.Distance(start, shot.End);
            float head = Mathf.Clamp01(time / duration);
            float tail = Mathf.Max(0f, head - 3f / Mathf.Max(.01f, length));
            tracer.SetPosition(0, Vector3.Lerp(start, shot.End, tail));
            tracer.SetPosition(1, Vector3.Lerp(start, shot.End, head));
            tracer.widthMultiplier = Mathf.Clamp01(1f - Mathf.Max(0f, time - duration) / .06f);
        }

        void Update()
        {
            if (tracer == null) return;
            age += Time.deltaTime;
            Draw(age);
            if (impacted || age < duration) return;
            impacted = true;
            if (shot.Water) CombatVfx.Splash(shot.End, .25f);
            else if (shot.Hit)
            {
                CombatVfx.Impact(shot.End, shot.Normal, false);
                GameAudio.Play(SoundCue.Impact, shot.End, .5f);
            }
        }

        void OnDestroy() { if (material != null) Destroy(material); }
    }
}