using UnityEngine;

namespace PirateSlop
{
    public sealed class PistolBullet : MonoBehaviour
    {
        static readonly PistolBullet[] pool=new PistolBullet[96];
        static int next;
        public int Generation { get; private set; }
        bool authoritative;
        public static PistolBullet Spawn(Vector3 start,FirearmShot shot,Material material,float width,bool detailed,float speed,bool confirmed=true)
        {
            if(!Application.isPlaying || Application.isBatchMode) return null;
            int index=next++%pool.Length;
            if(pool[index]==null) pool[index]=new GameObject("FirearmTracerPool").AddComponent<PistolBullet>();
            var bullet=pool[index];
            if(bullet.gameObject.activeSelf && bullet.tracer!=null && !bullet.impacted && bullet.authoritative) FirearmImpact.Present(bullet.shot,false);
            bullet.Initialize(start,shot,material,width,detailed,speed);
            bullet.authoritative=confirmed;
            return bullet;
        }
        public void Confirm(FirearmShot result)
        {
            shot=result;authoritative=true;
            if(age>=duration) { FirearmImpact.Present(shot,impactEffects);impacted=true; }
        }
        FirearmShot shot;
        Vector3 start;
        LineRenderer tracer;
        LineRenderer glow;
        Material material;
        float age, duration;
        bool impacted;
        bool impactEffects;
        float width;

        public void Initialize(Vector3 visibleStart, FirearmShot result, Material template, float thickness=.022f, bool showImpact=true, float speed=450)
        {
            age=0;impacted=false;authoritative=true;Generation++;gameObject.SetActive(true);
            start = visibleStart; shot = result;
            duration = Mathf.Clamp(Vector3.Distance(start, shot.End) / Mathf.Max(50,speed), .025f, .45f);
            impactEffects=showImpact; width=thickness;
            material = Resources.Load<Material>("FirearmGlow");
            if(material==null) material=template;
            if(tracer==null) tracer = gameObject.AddComponent<LineRenderer>();
            tracer.sharedMaterial = material;
            tracer.useWorldSpace = true; tracer.positionCount = 2;
            tracer.startWidth = width*.2f; tracer.endWidth = width;
            tracer.startColor=new Color(.7f,.5f,.2f,0);tracer.endColor=new Color(1.6f,1.35f,.8f,.85f);
            tracer.numCapVertices = 2;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            if(glow==null) { var halo=new GameObject("TracerGlow");halo.transform.SetParent(transform,false);glow=halo.AddComponent<LineRenderer>(); }
            glow.sharedMaterial=material;glow.useWorldSpace=true;glow.positionCount=2;glow.startWidth=width;glow.endWidth=width*3;
            glow.startColor=new Color(1,.35f,.05f,0);glow.endColor=new Color(1,.6f,.18f,.08f);
            glow.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;glow.receiveShadows=false;
            Draw(.01f);
        }

        void Draw(float time)
        {
            FirearmImpact.ResolvePoint(ref shot);
            float head = Mathf.Clamp01(time / duration);
            float tail = Mathf.Clamp01((time-Mathf.Min(.009f,duration*.45f))/duration);
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
            if(age>duration+.1f) gameObject.SetActive(false);
            if (impacted || age < duration) return;
            impacted = true;
            if(authoritative) FirearmImpact.Present(shot,impactEffects);
        }

    }
}
