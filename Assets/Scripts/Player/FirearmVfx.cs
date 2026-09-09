using UnityEngine;

namespace PirateSlop
{
    public static class FirearmVfx
    {
        static readonly ParticleSystem[] bursts=new ParticleSystem[96];
        static int nextBurst;
        public static void Impact(Vector3 point,Vector3 normal,BulletSurfaceKind surface)
        {
            var smoke=Resources.Load<Material>("CombatParticles");
            var glow=Resources.Load<Material>("FirearmGlow");
            Color dust=surface==BulletSurfaceKind.Wood?new Color(.48f,.29f,.12f,.65f):surface==BulletSurfaceKind.Flesh?new Color(.45f,.07f,.035f,.5f):new Color(.55f,.52f,.44f,.6f);
            Burst(point,normal,smoke,dust,5,.12f,1.4f,.45f,42,true);
            if(surface==BulletSurfaceKind.Metal) Burst(point,normal,glow,new Color(2,1.2f,.35f,1),8,.013f,6,.22f,60,false);
            else if(surface!=BulletSurfaceKind.Flesh) Burst(point,normal,smoke,dust,6,.018f,3,.35f,48,false);
        }
        public static void Fire(Vector3 point, Vector3 forward, float power=1)
        {
            if(!Application.isPlaying || Application.isBatchMode) return;
            var flash=Resources.Load<Material>("FirearmGlow");
            var smoke=Resources.Load<Material>("CombatParticles");
            Burst(point,forward,flash,new Color(2,1.3f,.55f,1),3,.22f*power,8*power,.055f,7,false);
            Burst(point+forward*.12f,forward,smoke,new Color(.61f,.6f,.55f,.32f),7,.27f*power,2.1f*power,.75f,15,true);
            Burst(point,forward,flash,new Color(1.5f,.7f,.18f,1),5,.015f*power,10*power,.18f,20,false);
            var go=new GameObject("FirearmMuzzleLight"); go.transform.position=point;
            var light=go.AddComponent<Light>(); light.color=new Color(1,.63f,.28f); light.intensity=6*power; light.range=4*power; light.shadows=LightShadows.None;
            Object.Destroy(go,.065f);
        }
        static void Burst(Vector3 point,Vector3 forward,Material material,Color color,int count,float size,float speed,float life,float angle,bool smoke)
        {
            if(material==null) return;
            int index=nextBurst++%bursts.Length;
            var particles=bursts[index];
            if(particles==null) particles=bursts[index]=new GameObject("FirearmParticlePool").AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var go=particles.gameObject;go.SetActive(true);go.transform.SetPositionAndRotation(point,Quaternion.LookRotation(forward));
            var main=particles.main;main.loop=false;main.playOnAwake=false;main.duration=.1f;main.maxParticles=count;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(life*.65f,life);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.4f,speed);
            main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size);main.startColor=color;main.gravityModifier=smoke ? -.06f : .35f;
            main.stopAction=ParticleSystemStopAction.Disable;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=angle;shape.radius=.025f;
            var emission=particles.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var fade=particles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(smoke?Color.white:new Color(1,.3f,.06f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.6f,.3f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var growth=particles.sizeOverLifetime;growth.enabled=true;growth.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,smoke?.6f:1,1,smoke?2.4f:.1f));
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.renderMode=smoke?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;
            if(!smoke){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.025f;}
            particles.Play();
        }
    }
}
