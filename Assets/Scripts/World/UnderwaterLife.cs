using UnityEngine;

namespace PirateSlop.World
{
    public sealed class UnderwaterLife : MonoBehaviour
    {
        ProceduralWorld world;
        Camera view;
        AmbientFishPopulation fish;
        ParticleSystem bubbles, silt;
        GameObject visuals;
        float checkAt;
        public void Initialize(ProceduralWorld source) => world=source;
        void Create()
        {
            visuals=new GameObject("UnderwaterAmbience"); visuals.transform.SetParent(transform,false);
            bubbles=Particles("Bubbles",Resources.Load<Material>("Underwater/Bubbles"),true);
            silt=Particles("SuspendedParticles",Resources.Load<Material>("Underwater/Silt"),false);
        }
        ParticleSystem Particles(string name,Material material,bool rising)
        {
            var go=new GameObject(name);go.transform.SetParent(visuals.transform,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=rising?100:280;main.startLifetime=rising?6f:12f;main.startSpeed=0f;
            main.startSize=new ParticleSystem.MinMaxCurve(rising?.025f:.012f,rising?.075f:.035f);
            main.startColor=rising?new Color(.6f,.85f,.9f,.5f):new Color(.65f,.8f,.7f,.3f);
            var emission=ps.emission;emission.rateOverTime=rising?12f:22f;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=rising?new Vector3(9,2,9):new Vector3(24,12,24);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=.06f;velocity.y=rising?.9f:.015f;velocity.z=.04f;
            var fade=ps.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.15f),new GradientAlphaKey(.6f,.75f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
        void Update()
        {
            if(world==null || !world.Ready){fish?.Update(null);return;}
            if(Time.unscaledTime>=checkAt)
            {
                checkAt=Time.unscaledTime+.5f;
                view=null;
                foreach(var player in Networking.NetworkPlayer.Active)
                    if(player.IsOwner && player.Motor!=null && !player.Motor.IsDead && player.Motor.PlayerCamera!=null && player.Motor.PlayerCamera.enabled) {view=player.Motor.PlayerCamera;break;}
            }
            if(fish==null)fish=new AmbientFishPopulation(world,transform);
            fish.Update(view);
            bool underwater=view!=null && OceanSurface.Instance!=null && view.transform.position.y<OceanSurface.Instance.Height(view.transform.position)-.2f;
            if(!underwater) {if(visuals!=null)visuals.SetActive(false);return;}
            if(visuals==null)Create();
            if(!visuals.activeSelf){visuals.SetActive(true);bubbles.Clear();silt.Clear();}
            Vector3 eye=view.transform.position;
            bubbles.transform.position=new Vector3(eye.x,Mathf.Min(eye.y-4f,world.Layout.SeaLevel-8f),eye.z);
            silt.transform.position=new Vector3(eye.x,Mathf.Min(eye.y,world.Layout.SeaLevel-7f),eye.z);
            if(!bubbles.isPlaying)bubbles.Play();if(!silt.isPlaying)silt.Play();
        }
        void OnDisable() => fish?.Update(null);
    }
}
