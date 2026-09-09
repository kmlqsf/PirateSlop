using UnityEngine;

namespace PirateSlop
{
    public static class FirearmVfx
    {
        public static void Fire(Vector3 point, Vector3 forward, float power=1)
        {
            if(!Application.isPlaying || Application.isBatchMode) return;
            var flash=Resources.Load<Material>("FirearmGlow");
            var smoke=Resources.Load<Material>("CombatParticles");
            Burst(point,forward,flash,new Color(3,1.6f,.45f,1),5,.3f*power,10*power,.075f,8,false);
            Burst(point+forward*.1f,forward,smoke,new Color(.66f,.65f,.6f,.48f),12,.34f*power,2.4f*power,1.1f,18,true);
            Burst(point,forward,flash,new Color(2,.8f,.18f,1),9,.023f*power,12*power,.25f,24,false);
            var go=new GameObject("FirearmMuzzleLight"); go.transform.position=point;
            var light=go.AddComponent<Light>(); light.color=new Color(1,.63f,.28f); light.intensity=6*power; light.range=4*power; light.shadows=LightShadows.None;
            Object.Destroy(go,.065f);
        }
        static void Burst(Vector3 point,Vector3 forward,Material material,Color color,int count,float size,float speed,float life,float angle,bool smoke)
        {
            if(material==null) return;
            var go=new GameObject(smoke?"PowderSmoke":"PowderFlash");go.transform.SetPositionAndRotation(point,Quaternion.LookRotation(forward));
            var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.loop=false;main.playOnAwake=false;main.duration=.1f;main.maxParticles=count;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(life*.65f,life);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.4f,speed);
            main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size);main.startColor=color;main.gravityModifier=smoke ? -.06f : .35f;
            main.stopAction=ParticleSystemStopAction.Destroy;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=angle;shape.radius=.025f;
            var emission=particles.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var fade=particles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(smoke?Color.white:new Color(1,.3f,.06f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.6f,.3f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var growth=particles.sizeOverLifetime;growth.enabled=true;growth.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,smoke?.6f:1,1,smoke?2.4f:.1f));
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            if(!smoke){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.025f;}
            particles.Play();Object.Destroy(go,life+.3f);
        }
    }
}
