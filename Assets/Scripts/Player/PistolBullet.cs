using UnityEngine;

namespace PirateSlop
{
    public sealed class PistolBullet : MonoBehaviour
    {
        FirearmShot shot;
        Vector3 start;
        LineRenderer tracer;
        LineRenderer glow;
        Material material;
        float age, duration;
        bool impacted;
        bool impactEffects;
        float width;

        public void Initialize(Vector3 visibleStart, FirearmShot result, Material template, float thickness=.045f, bool showImpact=true)
        {
            start = visibleStart; shot = result;
            duration = Mathf.Clamp(Vector3.Distance(start, shot.End) / 320f, .055f, .35f);
            impactEffects=showImpact; width=thickness;
            material = Resources.Load<Material>("FirearmGlow");
            if(material==null) material=template;
            tracer = gameObject.AddComponent<LineRenderer>();
            tracer.sharedMaterial = material;
            tracer.useWorldSpace = true; tracer.positionCount = 2;
            tracer.startWidth = width*.2f; tracer.endWidth = width;
            tracer.startColor=new Color(1,.4f,.08f,0);tracer.endColor=new Color(3,2.4f,1.4f,1);
            tracer.numCapVertices = 2;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            var halo=new GameObject("TracerGlow");halo.transform.SetParent(transform,false);glow=halo.AddComponent<LineRenderer>();
            glow.sharedMaterial=material;glow.useWorldSpace=true;glow.positionCount=2;glow.startWidth=width;glow.endWidth=width*3;
            glow.startColor=new Color(1,.35f,.05f,0);glow.endColor=new Color(1,.6f,.18f,.22f);
            glow.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;glow.receiveShadows=false;
            Draw(.01f);
            Destroy(gameObject, duration + .1f);
        }

        void Draw(float time)
        {
            float length = Vector3.Distance(start, shot.End);
            float head = Mathf.Clamp01(time / duration);
            float tail = Mathf.Clamp01((time-Mathf.Min(.024f,duration*.6f))/duration);
            tracer.SetPosition(0, Vector3.Lerp(start, shot.End, tail));
            tracer.SetPosition(1, Vector3.Lerp(start, shot.End, head));
            tracer.widthMultiplier = Mathf.Clamp01(1f - Mathf.Max(0f, time - duration) / .06f);
            glow.SetPosition(0,tracer.GetPosition(0));glow.SetPosition(1,tracer.GetPosition(1));glow.widthMultiplier=tracer.widthMultiplier;
        }

        void Update()
        {
            if (tracer == null) return;
            age += Time.deltaTime;
            Draw(age);
            if (impacted || age < duration) return;
            impacted = true;
            if(!impactEffects) return;
            if (shot.Water) CombatVfx.Splash(shot.End, .25f);
            else if (shot.Hit)
            {
                CombatVfx.Impact(shot.End, shot.Normal, false);
                GameAudio.Play(SoundCue.Impact, shot.End, .5f);
            }
        }

    }
}
