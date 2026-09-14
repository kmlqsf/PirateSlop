using UnityEngine;

namespace PirateSlop.World
{
    public sealed class UnderwaterLife : MonoBehaviour
    {
        ProceduralWorld world;
        Camera view;
        Transform[] fish;
        Vector3[] schools = new Vector3[4];
        ParticleSystem bubbles, silt;
        GameObject visuals;
        float checkAt;
        float habitatAt;
        bool[] fishClear = new bool[24];
        Vector3 anchor = Vector3.positiveInfinity;
        public void Initialize(ProceduralWorld source) => world=source;
        void Create()
        {
            visuals=new GameObject("UnderwaterAmbience"); visuals.transform.SetParent(transform,false);
            var prefab=Resources.Load<GameObject>("Underwater/Fish");
            fish=new Transform[prefab!=null?24:0];
            for(int i=0;i<fish.Length;i++)
            {
                var go=Instantiate(prefab,visuals.transform);go.name="AmbientFish";go.transform.localScale*=.25f+(i%4)*.04f;fish[i]=go.transform;
            }
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
            if(world==null || !world.Ready)return;
            if(Time.unscaledTime>=checkAt)
            {
                checkAt=Time.unscaledTime+.5f;
                view=null;
                foreach(var player in FindObjectsByType<Networking.NetworkPlayer>(FindObjectsSortMode.None))
                    if(player.IsOwner && player.Motor!=null && !player.Motor.IsDead && player.Motor.PlayerCamera.enabled) {view=player.Motor.PlayerCamera;break;}
            }
            bool underwater=view!=null && OceanSurface.Instance!=null && view.transform.position.y<OceanSurface.Instance.Height(view.transform.position)-.2f;
            if(!underwater) {if(visuals!=null)visuals.SetActive(false);anchor=Vector3.positiveInfinity;return;}
            if(visuals==null)Create();
            if(!visuals.activeSelf){visuals.SetActive(true);bubbles.Clear();silt.Clear();}
            Vector3 eye=view.transform.position;
            if((eye-anchor).sqrMagnitude>900f)
            {
                anchor=eye;
                habitatAt=0f;
                for(int i=0;i<schools.Length;i++)
                {
                    float angle=(i*.5f+.2f)*Mathf.PI;
                    Vector3 center=eye+new Vector3(Mathf.Cos(angle)*18f,-2f,Mathf.Sin(angle)*18f);
                    center.y=Mathf.Clamp(eye.y-2f-i,world.GroundHeight(center)+3f,world.Layout.SeaLevel-3f);
                    schools[i]=center;
                }
            }
            bubbles.transform.position=new Vector3(eye.x,Mathf.Min(eye.y-4f,world.Layout.SeaLevel-8f),eye.z);
            silt.transform.position=new Vector3(eye.x,Mathf.Min(eye.y,world.Layout.SeaLevel-7f),eye.z);
            if(!bubbles.isPlaying)bubbles.Play();if(!silt.isPlaying)silt.Play();
            bool checkHabitat=Time.time>=habitatAt;
            if(checkHabitat)habitatAt=Time.time+.2f;
            for(int i=0;i<fish.Length;i++)
            {
                float t=Time.time*.24f+i*.08f;
                Vector3 center=schools[i/6];
                Vector3 point=center+new Vector3(Mathf.Cos(t)*3f,Mathf.Sin(t*2f+i)*.3f+(i%3)*.22f,Mathf.Sin(t)*3f);
                if(checkHabitat)fishClear[i]=point.y>world.GroundHeight(point)+.6f && point.y<world.Layout.SeaLevel-1f && !Physics.CheckSphere(point,.25f,~0,QueryTriggerInteraction.Ignore);
                bool clear=fishClear[i];
                fish[i].gameObject.SetActive(clear);
                if(clear)fish[i].SetPositionAndRotation(point,Quaternion.LookRotation(new Vector3(-Mathf.Sin(t),0,Mathf.Cos(t)))*Quaternion.Euler(0,90f+Mathf.Sin(Time.time*7f+i)*5f,0));
            }
        }
    }
}
