using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using PirateSlop.World;

namespace PirateSlop
{
    public sealed class StormCloudArcController : MonoBehaviour
    {
        public ParticleSystem CloudPrefab;
        public Material[] BandMaterials;
        [Range(20,90)] public float SectorHalfAngle = 65;
        [Range(48,144)] public int AngularCells = 144;
        public int Seed = 4271;
        public bool DrawSector = true;
        public int ActiveParticles { get; private set; }
        public int ActiveRenderers { get; private set; }
        readonly List<Slot> pool = new List<Slot>();
        readonly Dictionary<int,Slot> active = new Dictionary<int,Slot>();
        readonly List<int> expired = new List<int>();
        static readonly ProfilerMarker Placement = new ProfilerMarker("BRZoneV3.CloudPlacement");
        BRZoneVisual backing;
        float oldOpacity, direction;
        Vector3 center;
        float radius;
        int capacity, cellCount;
        sealed class Slot
        {
            public ParticleSystem system;
            public ParticleSystemRenderer renderer;
            public int key = -1;
            public float angle, offset, height;
            public MaterialPropertyBlock block = new MaterialPropertyBlock();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if(scene.name!="NetworkOcean" || FindFirstObjectByType<StormCloudArcController>()!=null) return;
            var prefab=Resources.Load<GameObject>("StormCloudArc");
            if(prefab!=null) Instantiate(prefab);
        }

        void Start()
        {
            if(CloudPrefab==null || BandMaterials==null || BandMaterials.Length<3) { enabled=false; return; }
            cellCount=Mathf.Clamp(AngularCells,48,144);
            capacity=Mathf.Min(cellCount,Mathf.CeilToInt(SectorHalfAngle*2/(360f/cellCount))+5)*3;
            for(int i=0;i<capacity;i++)
            {
                var ps=Instantiate(CloudPrefab,transform);
                ps.name="PooledCloud"; ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.transform.localScale=Vector3.one;
                var main=ps.main; main.playOnAwake=false; main.simulationSpace=ParticleSystemSimulationSpace.Local;
                main.scalingMode=ParticleSystemScalingMode.Local; main.loop=true; main.prewarm=false;
                main.startLifetime=new ParticleSystem.MinMaxCurve(36,52); main.startSpeed=0; main.startColor=Color.white;
                var color=ps.colorOverLifetime; color.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.18f),new GradientAlphaKey(.85f,.78f),new GradientAlphaKey(0,1)});
                color.color=gradient;
                var velocity=ps.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.Local;
                velocity.x=.35f; velocity.y=.12f; velocity.z=0;
                ps.gameObject.SetActive(false);
                pool.Add(new Slot { system=ps,renderer=ps.GetComponent<ParticleSystemRenderer>() });
            }
        }

        void LateUpdate()
        {
            var zone=StormZone.Instance; var camera=Camera.main;
            if(zone==null || camera==null || pool.Count==0) return;
            using(Placement.Auto())
            {
                if(backing==null)
                {
                    backing=FindFirstObjectByType<BRZoneVisual>();
                    if(backing!=null)
                    {
                        oldOpacity=backing.Opacity; backing.Opacity=.075f;
                        foreach(var name in new[]{"StormInnerVolume","StormEdgeVolume"})
                        { var child=backing.transform.Find(name); if(child!=null) child.gameObject.SetActive(false); }
                    }
                }
                center=zone.Center; radius=zone.Radius;
                Vector3 radial=camera.transform.position-center;
                if(radial.x*radial.x+radial.z*radial.z>1) direction=Mathf.Atan2(radial.z,radial.x)*Mathf.Rad2Deg;
                float step=360f/cellCount;
                expired.Clear();
                foreach(var pair in active)
                    if(Mathf.Abs(Mathf.DeltaAngle(direction,(pair.Key/3)*step))>SectorHalfAngle+step) expired.Add(pair.Key);
                foreach(int key in expired) { var slot=active[key]; slot.system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); slot.system.gameObject.SetActive(false); slot.key=-1; active.Remove(key); }
                int middle=Mathf.RoundToInt(direction/step), extent=Mathf.CeilToInt(SectorHalfAngle/step);
                for(int n=-extent;n<=extent;n++)
                {
                    int cell=((middle+n)%cellCount+cellCount)%cellCount;
                    for(int band=0;band<3;band++)
                    {
                        int key=cell*3+band;
                        if(active.ContainsKey(key)) continue;
                        Slot slot=null; foreach(var candidate in pool) if(candidate.key<0) { slot=candidate; break; }
                        if(slot==null) continue;
                        Configure(slot,key,cell,band,step); active.Add(key,slot);
                    }
                }
                ActiveParticles=0; ActiveRenderers=0;
                foreach(var pair in active)
                {
                    var slot=pair.Value; float a=slot.angle*Mathf.Deg2Rad;
                    var normal=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    Vector3 p=center+normal*(radius+slot.offset);
                    p.y=(OceanSurface.Instance!=null?OceanSurface.Instance.Height(p):0)+slot.height;
                    slot.system.transform.SetPositionAndRotation(p,Quaternion.LookRotation(normal));
                    float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(SectorHalfAngle-8,SectorHalfAngle+step,Mathf.Abs(Mathf.DeltaAngle(direction,slot.angle))));
                    Color tint=BandMaterials[pair.Key%3].color; tint.a*=fade;
                    slot.block.SetColor("_Color",tint); slot.renderer.SetPropertyBlock(slot.block);
                    ActiveParticles+=slot.system.particleCount; if(fade>.01f) ActiveRenderers++;
                }
            }
        }

        void Configure(Slot slot,int key,int cell,int band,float step)
        {
            var random=new System.Random(unchecked(Seed+key*7919));
            float jitter=(float)random.NextDouble();
            slot.key=key; slot.angle=cell*step+(jitter-.5f)*step*.5f;
            slot.offset=band==0?Mathf.Lerp(8,16,jitter):band==1?Mathf.Lerp(-3,3,jitter):Mathf.Lerp(-10,-5,jitter);
            slot.height=band==0?48:band==1?28:15;
            slot.height+=(jitter-.5f)*(band==2?5:12);
            var ps=slot.system; ps.useAutoRandomSeed=false; ps.randomSeed=(uint)(Seed+key*7919+1);
            var main=ps.main; int count=band==0?8:band==1?6:4; main.maxParticles=count;
            main.startSize3D=true; float size=band==0?155:band==1?120:65;
            main.startSizeX=new ParticleSystem.MinMaxCurve(size*.75f,size*1.15f);
            main.startSizeY=new ParticleSystem.MinMaxCurve(band==2?25:65,band==2?40:90);
            main.startSizeZ=size;
            main.startRotation=new ParticleSystem.MinMaxCurve(-.3f,.3f);
            var emission=ps.emission; emission.enabled=true; emission.rateOverTime=count/36f;
            var shape=ps.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Box;
            shape.scale=new Vector3(band==2?60:65,band==2?5:20,6);
            slot.renderer.sharedMaterial=BandMaterials[band];
            slot.renderer.maxParticleSize=3;
            slot.renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            slot.system.gameObject.SetActive(true);
            ps.Simulate(48+jitter*12,true,true,true); ps.Play();
        }

        void OnDisable()
        {
            foreach(var slot in pool) if(slot.system!=null) slot.system.gameObject.SetActive(false);
            active.Clear(); foreach(var slot in pool) slot.key=-1;
            if(backing!=null)
            {
                backing.Opacity=oldOpacity;
                foreach(var name in new[]{"StormInnerVolume","StormEdgeVolume"})
                { var child=backing.transform.Find(name); if(child!=null) child.gameObject.SetActive(true); }
                backing=null;
            }
        }
        void OnDrawGizmos()
        {
            if(!DrawSector || radius<=0) return;
            Gizmos.color=Color.yellow;
            for(int i=0;i<128;i++) { float a=i*Mathf.PI/64,b=(i+1)*Mathf.PI/64; Gizmos.DrawLine(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius,center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius); }
            Gizmos.color=Color.cyan;
            foreach(float angle in new[]{direction-SectorHalfAngle,direction+SectorHalfAngle})
            { float a=angle*Mathf.Deg2Rad; Gizmos.DrawLine(center,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius); }
        }
    }
}
