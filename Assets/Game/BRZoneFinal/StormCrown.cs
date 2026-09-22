using UnityEngine;
using UnityEngine.SceneManagement;
using PirateSlop.World;

namespace PirateSlop
{
    [DefaultExecutionOrder(200)]
    public sealed class StormCrown : MonoBehaviour
    {
        public Material ShellMaterial, WaterlineMaterial, CloudMaterial, VaporMaterial, SprayMaterial;
        public ParticleSystem CloudPrefab;
        public float Height=160, BaseDensity=.72f, BroadScale=.009f, MediumScale=.025f, FlowSpeed=1.2f;
        public AnimationCurve InwardProfile=new AnimationCurve(new Keyframe(0,0),new Keyframe(.125f,2),new Keyframe(.25f,8),new Keyframe(.4375f,20),new Keyframe(.625f,40),new Keyframe(.8125f,65),new Keyframe(1,95));
        [Range(0,1)] public float CloudBreakupDensity=.55f, WaterSprayIntensity=.65f;
        [Range(25,90)] public float SectorHalfAngle=65;
        public bool DebugGizmos=true;
        public int Seed=7919;
        public int ActiveParticles { get; private set; }
        public float CurrentRadius { get; private set; }
        const int Segments=256, Rows=32, Slots=42;
        Mesh shell, waterline;
        Vector3[] vertices, foamVertices;
        MeshRenderer shellRenderer;
        MaterialPropertyBlock block;
        ParticleSystem[] clouds=new ParticleSystem[Slots], vapor=new ParticleSystem[Slots], spray=new ParticleSystem[Slots];
        int[] keys=new int[Slots];
        Vector3 center;
        float direction;
        Camera observer;
        float oldFar;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { SceneManager.sceneLoaded-=Loaded; SceneManager.sceneLoaded+=Loaded; }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="NetworkOcean" || FindFirstObjectByType<StormCrown>()!=null) return;
            var asset=Resources.Load<GameObject>("StormCrown"); if(asset!=null) Instantiate(asset);
        }
        void Awake()
        {
            block=new MaterialPropertyBlock();
            shell=Build("StormCrownShell",Rows,ShellMaterial,out vertices,out shellRenderer);
            waterline=Build("StormWaterline",1,WaterlineMaterial,out foamVertices,out _);
            for(int i=0;i<Slots;i++)
            {
                keys[i]=int.MinValue;
                clouds[i]=CreateCloud("Breakup",CloudMaterial,5,65,.17f);
                vapor[i]=CreateCloud("SeaVapor",VaporMaterial,6,24,.35f);
                spray[i]=CreateSpray();
            }
        }
        Mesh Build(string name,int rows,Material material,out Vector3[] points,out MeshRenderer renderer)
        {
            points=new Vector3[(Segments+1)*(rows+1)]; var uv=new Vector2[points.Length]; var tris=new int[Segments*rows*6];
            for(int i=0;i<=Segments;i++) for(int j=0;j<=rows;j++)
            {
                int k=i*(rows+1)+j; uv[k]=new Vector2(i/(float)Segments,j/(float)rows);
                if(i==Segments||j==rows)continue; int t=(i*rows+j)*6,n=k+rows+1;
                tris[t]=k;tris[t+1]=k+1;tris[t+2]=n;tris[t+3]=n;tris[t+4]=k+1;tris[t+5]=n+1;
            }
            var mesh=new Mesh{name=name,vertices=points,uv=uv,triangles=tris};mesh.MarkDynamic();
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return mesh;
        }
        ParticleSystem CreateCloud(string name,Material material,int count,float size,float rate)
        {
            var ps=Instantiate(CloudPrefab,transform);ps.name=name;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.localScale=Vector3.one;
            var m=ps.main;m.playOnAwake=false;m.simulationSpace=ParticleSystemSimulationSpace.Local;m.startLifetime=24;m.startSpeed=0;m.maxParticles=count;m.startSize=new ParticleSystem.MinMaxCurve(size*.65f,size*1.4f);m.startRotation=new ParticleSystem.MinMaxCurve(-.6f,.6f);
            var e=ps.emission;e.rateOverTime=rate;
            var shape=ps.shape;shape.scale=new Vector3(size*.5f,size*.15f,5);
            var color=ps.colorOverLifetime;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.6f,.2f),new GradientAlphaKey(.6f,.75f),new GradientAlphaKey(0,1)});color.color=g;
            var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=material;r.maxParticleSize=3;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;ps.gameObject.SetActive(false);return ps;
        }
        ParticleSystem CreateSpray()
        {
            var go=new GameObject("SeaSpray");go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var m=ps.main;m.playOnAwake=false;m.simulationSpace=ParticleSystemSimulationSpace.Local;m.startLifetime=new ParticleSystem.MinMaxCurve(.7f,1.4f);m.startSpeed=new ParticleSystem.MinMaxCurve(4,10);m.startSize=new ParticleSystem.MinMaxCurve(.18f,.55f);m.startColor=new Color(.85f,.92f,.94f,.5f);m.gravityModifier=1;m.maxParticles=28;
            var e=ps.emission;e.rateOverTime=12;var s=ps.shape;s.shapeType=ParticleSystemShapeType.Cone;s.angle=30;s.radius=4;
            var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=SprayMaterial;r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=2;r.velocityScale=.06f;
            ps.transform.localRotation=Quaternion.Euler(-90,0,0);go.SetActive(false);return ps;
        }
        public float Inset(float fraction,float radius)=>Mathf.Min(Mathf.Max(0,InwardProfile.Evaluate(fraction)),radius*.7f);
        static float Water(Vector3 p)=>OceanSurface.Instance!=null?OceanSurface.Instance.Height(p):0;
        void LateUpdate()
        {
            var zone=StormZone.Instance;var camera=Camera.main;if(zone==null||camera==null)return;
            var old=FindFirstObjectByType<BRZoneVisual>();if(old!=null&&old.gameObject.activeSelf)old.gameObject.SetActive(false);
            var arc=FindFirstObjectByType<StormCloudArcController>();if(arc!=null&&arc.gameObject.activeSelf)arc.gameObject.SetActive(false);
            center=zone.Center;CurrentRadius=zone.Radius;transform.position=center;
            if(observer!=camera){if(observer!=null)observer.farClipPlane=oldFar;observer=camera;oldFar=camera.farClipPlane;}camera.farClipPlane=Mathf.Max(oldFar,CurrentRadius*2.3f+Height);
            for(int i=0;i<=Segments;i++)
            {
                float a=i*Mathf.PI*2/Segments;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));float sea=Water(center+radial*CurrentRadius)-center.y;
                for(int j=0;j<=Rows;j++){float h=j/(float)Rows;vertices[i*(Rows+1)+j]=radial*(CurrentRadius-Inset(h,CurrentRadius))+Vector3.up*(sea+h*Height);}
                for(int j=0;j<2;j++){var p=radial*(CurrentRadius+(j-.5f)*8);p.y=Water(center+p)-center.y+.55f;foamVertices[i*2+j]=p;}
            }
            shell.vertices=vertices;shell.RecalculateBounds();waterline.vertices=foamVertices;waterline.RecalculateBounds();
            block.SetFloat("_Density",BaseDensity);block.SetFloat("_BroadScale",BroadScale);block.SetFloat("_MediumScale",MediumScale);block.SetFloat("_FlowSpeed",FlowSpeed);shellRenderer.SetPropertyBlock(block);
            Vector3 offset=camera.transform.position-center;if(offset.x*offset.x+offset.z*offset.z>1)direction=Mathf.Atan2(offset.z,offset.x)*Mathf.Rad2Deg;else direction=Mathf.Atan2(camera.transform.forward.z,camera.transform.forward.x)*Mathf.Rad2Deg;
            int middle=Mathf.RoundToInt(direction/4);ActiveParticles=0;
            for(int slotIndex=0;slotIndex<Slots;slotIndex++)
            {
                int cell=middle+slotIndex-Slots/2;int i=(cell%Slots+Slots)%Slots;int key=(cell%90+90)%90;float jitter=Random01(key),angle=(cell*4+(jitter-.5f)*3)*Mathf.Deg2Rad;
                var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));float h=Mathf.Lerp(.15f,.96f,Random01(key+139));
                Vector3 p=center+radial*(CurrentRadius-Inset(h,CurrentRadius)+(jitter-.5f)*22);p.y=Water(p)+h*Height;
                bool visible=Mathf.Abs(Mathf.DeltaAngle(direction,cell*4))<SectorHalfAngle;
                bool cloud=visible&&jitter<CloudBreakupDensity;
                Set(clouds[i],cloud,key,ref keys[i]);clouds[i].transform.position=p;
                float waterAngle=direction*Mathf.Deg2Rad+(slotIndex-Slots/2)*7/Mathf.Max(1,CurrentRadius);
                p=center+new Vector3(Mathf.Cos(waterAngle),0,Mathf.Sin(waterAngle))*CurrentRadius;p.y=Water(p)+2;
                bool near=Vector3.Distance(camera.transform.position,p)<650;
                SetSimple(vapor[i],near);vapor[i].transform.position=p;
                SetSimple(spray[i],near&&Vector3.Distance(camera.transform.position,p)<220&&WaterSprayIntensity>0);
                spray[i].transform.position=p-Vector3.up;var emission=spray[i].emission;emission.rateOverTime=18*WaterSprayIntensity*(.5f+jitter);
                ActiveParticles+=clouds[i].particleCount+vapor[i].particleCount+spray[i].particleCount;
            }
        }
        float Random01(int key){uint x=unchecked((uint)(key*747796405+Seed));x=(x^(x>>16))*2246822519u;return (x&65535)/65535f;}
        void Set(ParticleSystem ps,bool on,int key,ref int previous)
        {
            if(previous!=key){ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(key+Seed+1);previous=key;}
            if(on&&!ps.gameObject.activeSelf){ps.gameObject.SetActive(true);ps.Simulate(26,true,true,true);ps.Play();}else if(!on)ps.gameObject.SetActive(false);
        }
        void SetSimple(ParticleSystem ps,bool on){if(on&&!ps.gameObject.activeSelf){ps.gameObject.SetActive(true);ps.Play();}else if(!on)ps.gameObject.SetActive(false);}
        void OnDestroy(){if(observer!=null)observer.farClipPlane=oldFar;if(shell!=null)Destroy(shell);if(waterline!=null)Destroy(waterline);}
        void OnDrawGizmos()
        {
            if(!DebugGizmos||CurrentRadius<=0)return;
            foreach(float h in new[]{0f,.5f,1f}){Gizmos.color=h==0?Color.yellow:Color.cyan;for(int i=0;i<128;i++){float a=i*Mathf.PI/64,b=(i+1)*Mathf.PI/64,r=CurrentRadius-Inset(h,CurrentRadius);Gizmos.DrawLine(center+new Vector3(Mathf.Cos(a)*r,h*Height,Mathf.Sin(a)*r),center+new Vector3(Mathf.Cos(b)*r,h*Height,Mathf.Sin(b)*r));}}
            Gizmos.color=Color.green;foreach(float a in new[]{direction-SectorHalfAngle,direction+SectorHalfAngle})Gizmos.DrawLine(center,center+new Vector3(Mathf.Cos(a*Mathf.Deg2Rad),0,Mathf.Sin(a*Mathf.Deg2Rad))*CurrentRadius);
        }
    }
}
