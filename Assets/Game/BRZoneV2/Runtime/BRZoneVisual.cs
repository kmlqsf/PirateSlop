using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PirateSlop.World;

namespace PirateSlop
{
    public sealed class BRZoneVisual : MonoBehaviour
    {
        public Material WallMaterial, FoamMaterial, ParticleMaterial;
        public ParticleSystem MistPrefab, SprayPrefab;
        public bool Wall = true, Foam = true, Particles = true, PostFX = true, Lightning = true;
        [Range(32, 256)] public int Segments = 128;
        [Min(1)] public float WallHeight = 90, Thickness = 12, FoamWidth = 7, WarningDistance = 85, StrongDistance = 28;
        [Range(.05f,.48f)] public float Opacity = .38f;
        [Range(0,100)] public float ParticleDensity = 24;
        public bool Preview;
        public Vector3 PreviewCenter, PreviewTargetCenter;
        public float PreviewRadius = 1500, PreviewTargetRadius = 100;
        [Range(0,1)] public float PreviewProgress;
        [SerializeField] float currentRadius, targetRadius, distanceToBorder;
        Mesh wallMesh, foamMesh;
        MeshRenderer wallRenderer, foamRenderer, innerRenderer, edgeRenderer, lowMistRenderer;
        Vector3[] wallVertices, foamVertices;
        Vector2[] wallHeightData;
        Transform wallTransform, foamTransform;
        ParticleSystem mist, spray;
        LineRenderer bolt;
        MaterialPropertyBlock properties;
        Volume volume;
        VolumeProfile profile;
        ColorAdjustments grade;
        Vignette vignette;
        Camera target;
        UniversalAdditionalCameraData cameraData;
        bool oldDepth, oldPost;
        float oldFar, intensity, nextBolt, boltUntil, emissionRemainder, nextSplash;
        AudioSource wind;
        GameAudioBank bank;
        Vector3 center;
        readonly System.Random random = new System.Random();
        public float Intensity => intensity;
        public float DistanceToBorder => distanceToBorder;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            wallMesh = CreateMesh(false, out wallVertices);
            wallHeightData = new Vector2[wallVertices.Length];
            foamMesh = CreateMesh(true, out foamVertices);
            wallRenderer = CreateRenderer("ZoneWallRenderer", wallMesh, WallMaterial);
            innerRenderer = CreateRenderer("StormInnerVolume", wallMesh, WallMaterial);
            edgeRenderer = CreateRenderer("StormEdgeVolume", wallMesh, WallMaterial);
            lowMistRenderer = CreateRenderer("SeaContactMist", wallMesh, WallMaterial);
            foamRenderer = CreateRenderer("SeaContactRing", foamMesh, FoamMaterial);
            wallTransform = wallRenderer.transform; foamTransform = foamRenderer.transform;
            mist = MistPrefab != null ? Instantiate(MistPrefab, transform) : CreateParticles("StormMistVFX", false);
            spray = SprayPrefab != null ? Instantiate(SprayPrefab, transform) : CreateParticles("StormSprayVFX", true);
            mist.Play(); spray.Play();
            var lightning = new GameObject("LightningVFX"); lightning.transform.SetParent(transform, false);
            bolt = lightning.AddComponent<LineRenderer>(); bolt.sharedMaterial = ParticleMaterial;
            bolt.positionCount = 9; bolt.widthMultiplier = .35f; bolt.useWorldSpace = true;
            bolt.startColor = bolt.endColor = new Color(.7f,.78f,.82f,.8f); bolt.enabled = false;
            bolt.shadowCastingMode = ShadowCastingMode.Off;
            nextBolt = Time.time + Range(5,12);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            grade = profile.Add<ColorAdjustments>(); vignette = profile.Add<Vignette>();
            var post = new GameObject("ZonePostFX"); post.transform.SetParent(transform,false);
            volume = post.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 12; volume.sharedProfile = profile;
            grade.saturation.Override(0); grade.postExposure.Override(0); grade.colorFilter.Override(Color.white);
            vignette.intensity.Override(0); vignette.smoothness.Override(.65f);
            bank = Resources.Load<GameAudioBank>("GameAudioBank");
            wind = gameObject.AddComponent<AudioSource>(); wind.playOnAwake = false; wind.loop = true;
            wind.spatialBlend = 0; wind.volume = 0; wind.clip = bank != null ? bank.Wind : null;
            if(wind.clip != null) wind.Play();
        }

        Mesh CreateMesh(bool foam, out Vector3[] vertices)
        {
            int rows = foam ? 1 : 8;
            vertices = new Vector3[(Segments+1)*(rows+1)];
            var uv = new Vector2[vertices.Length]; var indices = new int[Segments*rows*6];
            for(int i=0;i<=Segments;i++) for(int j=0;j<=rows;j++)
            {
                int v=i*(rows+1)+j; uv[v]=new Vector2(i/(float)Segments,j/(float)rows);
                if(i==Segments || j==rows) continue;
                int t=(i*rows+j)*6, n=v+rows+1;
                indices[t]=v; indices[t+1]=v+1; indices[t+2]=n;
                indices[t+3]=n; indices[t+4]=v+1; indices[t+5]=n+1;
            }
            var mesh=new Mesh { name=foam?"BRZoneFoam":"BRZoneCurtain", vertices=vertices, uv=uv, triangles=indices };
            mesh.MarkDynamic(); return mesh;
        }

        MeshRenderer CreateRenderer(string name, Mesh mesh, Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.GetComponent<MeshRenderer>(); r.sharedMaterial=material; r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false;
            return r;
        }

        ParticleSystem CreateParticles(string name, bool rain)
        {
            var go=new GameObject(name); go.transform.SetParent(transform,false);
            var ps=go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var m=ps.main; m.playOnAwake=false; m.simulationSpace=ParticleSystemSimulationSpace.World;
            m.startSpeed=0; m.startLifetime=rain?1.2f:3f; m.maxParticles=rain?160:100;
            m.startSize=rain?.06f:2.5f; m.startColor=new Color(.62f,.68f,.7f,rain?.32f:.12f);
            var emission=ps.emission; emission.enabled=false;
            var shape=ps.shape; shape.enabled=false;
            var size=ps.sizeOverLifetime; size.enabled=!rain;
            size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.4f,1,1.6f));
            var color=ps.colorOverLifetime; color.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var r=ps.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial=ParticleMaterial;
            r.renderMode=rain?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;
            r.velocityScale=.06f; r.lengthScale=3; r.shadowCastingMode=ShadowCastingMode.Off;
            ps.Play(); return ps;
        }

        void LateUpdate()
        {
            var source=StormZone.Instance;
            if(source==null && !Preview) return;
            center=Preview?Vector3.Lerp(PreviewCenter,PreviewTargetCenter,PreviewProgress):source.Center;
            currentRadius=Preview?Mathf.Lerp(PreviewRadius,PreviewTargetRadius,PreviewProgress):source.Radius;
            targetRadius=Preview?PreviewTargetRadius:source.TargetRadius;
            UpdateMeshes();
            var camera=Camera.main;
            if(camera==null) return;
            if(target!=camera) { ReleaseCamera(); target=camera; oldFar=camera.farClipPlane; cameraData=camera.GetUniversalAdditionalCameraData(); oldDepth=cameraData.requiresDepthTexture; oldPost=cameraData.renderPostProcessing; cameraData.requiresDepthTexture=true; cameraData.renderPostProcessing=true; }
            camera.farClipPlane=Mathf.Max(oldFar,currentRadius*2.2f+WallHeight);
            Vector3 point=camera.transform.position;
            distanceToBorder=currentRadius-new Vector2(point.x-center.x,point.z-center.z).magnitude;
            float near=1-Mathf.InverseLerp(0,Mathf.Max(1,WarningDistance),distanceToBorder);
            float strong=1-Mathf.InverseLerp(0,Mathf.Max(1,StrongDistance),distanceToBorder);
            float outside=Mathf.SmoothStep(0,1,Mathf.Clamp01(-distanceToBorder/28));
            intensity=Mathf.Lerp(intensity,Mathf.Max(near*.25f,strong*.5f)+outside*.5f,1-Mathf.Exp(-Time.deltaTime*2));
            bool underwater=point.y<Water(point)-.15f;
            volume.weight=PostFX&&!underwater?Mathf.Clamp01(near*.2f+strong*.2f+outside):0;
            grade.saturation.value=-18*outside; grade.postExposure.value=-.25f*outside;
            grade.colorFilter.value=Color.Lerp(Color.white,new Color(.86f,.93f,1),outside*.5f);
            vignette.intensity.value=near*.025f+strong*.02f+outside*.175f;
            float gain=bank!=null?bank.Master*bank.Ambience:.5f;
            wind.volume=gain*intensity*(underwater?.15f:.65f); wind.pitch=.72f+intensity*.2f;
            EmitParticles(point,underwater);
            if(Lightning && Time.time>=nextBolt) Strike(point);
            bolt.enabled=Lightning&&Time.time<boltUntil;
        }

        void UpdateMeshes()
        {
            wallTransform.position=center; foamTransform.position=center;
            innerRenderer.transform.position=center;
            edgeRenderer.transform.position=center;
            lowMistRenderer.transform.position=center;
            lowMistRenderer.transform.localScale=new Vector3(1,8f/Mathf.Max(1,WallHeight),1);
            int segments=foamVertices.Length/2-1;
            for(int i=0;i<=segments;i++)
            {
                float angle=i*Mathf.PI*2/segments; var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 basePoint=center+radial*currentRadius; float water=Water(basePoint)-center.y;
                for(int j=0;j<=8;j++)
                {
                    wallVertices[i*9+j]=radial*currentRadius+Vector3.up*(water+j/8f*WallHeight);
                    wallHeightData[i*9+j]=new Vector2(water,WallHeight);
                }
                for(int j=0;j<2;j++) { var p=radial*(currentRadius+(j-.5f)*FoamWidth); p.y=Water(center+p)-center.y+.15f; foamVertices[i*2+j]=p; }
            }
            wallMesh.vertices=wallVertices; wallMesh.RecalculateBounds();
            wallMesh.uv2=wallHeightData;
            var bounds=wallMesh.bounds; bounds.Expand(Thickness); wallMesh.bounds=bounds;
            foamMesh.vertices=foamVertices; foamMesh.RecalculateBounds();
            properties.SetFloat("_Opacity",Opacity*.8f); properties.SetFloat("_Thickness",Thickness);
            properties.SetFloat("_Layer",0); properties.SetFloat("_Mist",0); wallRenderer.SetPropertyBlock(properties);
            properties.SetFloat("_Layer",1); innerRenderer.SetPropertyBlock(properties);
            properties.SetFloat("_Layer",2); edgeRenderer.SetPropertyBlock(properties);
            properties.SetFloat("_Layer",0); properties.SetFloat("_Mist",1); lowMistRenderer.SetPropertyBlock(properties);
            wallRenderer.enabled=Wall; innerRenderer.enabled=Wall; edgeRenderer.enabled=Wall; foamRenderer.enabled=Foam; lowMistRenderer.enabled=Foam;
        }

        void EmitParticles(Vector3 point, bool underwater)
        {
            if(!Particles||underwater||Mathf.Abs(distanceToBorder)>WarningDistance) { mist.Clear(); spray.Clear(); return; }
            emissionRemainder+=Time.deltaTime*ParticleDensity;
            int count=Mathf.Min(12,(int)emissionRemainder); emissionRemainder-=count;
            float angle=Mathf.Atan2(point.z-center.z,point.x-center.x);
            for(int i=0;i<count;i++)
            {
                float a=angle+Range(-45,45)/Mathf.Max(1,currentRadius);
                Vector3 p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(currentRadius+Range(-Thickness*.5f,Thickness*.5f));
                p.y=Water(p)+Range(1,12);
                var emit=new ParticleSystem.EmitParams { position=p,velocity=new Vector3(2,-14,1) }; spray.Emit(emit,1);
                if(i%3==0) { emit.position=new Vector3(p.x,Water(p)+1,p.z); emit.velocity=new Vector3(1,1,.5f); mist.Emit(emit,1); }
            }
            if(Time.time>=nextSplash)
            {
                nextSplash=Time.time+Range(2.5f,6);
                float a=angle+Range(-30,30)/Mathf.Max(1,currentRadius);
                Vector3 p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*currentRadius;
                p.y=Water(p)+.2f;
                for(int i=0;i<14;i++) spray.Emit(new ParticleSystem.EmitParams { position=p+new Vector3(Range(-2,2),0,Range(-2,2)),velocity=new Vector3(Range(-2,2),Range(3,8),Range(-2,2)),startLifetime=Range(.5f,1.3f),startSize=Range(.08f,.18f) },1);
                mist.Emit(new ParticleSystem.EmitParams { position=p+Vector3.up,velocity=Vector3.up,startSize=4,startLifetime=3 },3);
            }
        }

        void Strike(Vector3 point)
        {
            nextBolt=Time.time+Range(5,12); boltUntil=Time.time+.18f;
            float a=Mathf.Atan2(point.z-center.z,point.x-center.x)+Range(-.65f,.65f);
            Vector3 p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(currentRadius+Thickness*.25f);
            float sea=Water(p);
            for(int i=0;i<9;i++) bolt.SetPosition(i,new Vector3(p.x+Range(-2,2),sea+WallHeight*(.8f-i*.085f),p.z+Range(-2,2)));
        }

        float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        static float Water(Vector3 p)=>OceanSurface.Instance!=null?OceanSurface.Instance.Height(p):0;
        void ReleaseCamera() { if(target!=null) target.farClipPlane=oldFar; if(cameraData!=null) { cameraData.requiresDepthTexture=oldDepth; cameraData.renderPostProcessing=oldPost; } target=null; cameraData=null; }
        void OnDisable() { ReleaseCamera(); if(volume!=null) volume.weight=0; if(wind!=null) wind.volume=0; }
        void OnDestroy() { ReleaseCamera(); if(wallMesh!=null) Destroy(wallMesh); if(foamMesh!=null) Destroy(foamMesh); if(profile!=null) Destroy(profile); }
        void OnDrawGizmosSelected()
        {
            var source=StormZone.Instance;
            Vector3 c=Preview?Vector3.Lerp(PreviewCenter,PreviewTargetCenter,PreviewProgress):source!=null?source.Center:center;
            float r=Preview?Mathf.Lerp(PreviewRadius,PreviewTargetRadius,PreviewProgress):currentRadius;
            DrawCircle(c,r,new Color(.35f,.65f,.6f));
            DrawCircle(Preview?PreviewTargetCenter:source!=null?source.TargetCenter:c,Preview?PreviewTargetRadius:targetRadius,new Color(.77f,.63f,.36f));
        }
        static void DrawCircle(Vector3 c,float r,Color color) { Gizmos.color=color; for(int i=0;i<128;i++) { float a=i*Mathf.PI/64,b=(i+1)*Mathf.PI/64; Gizmos.DrawLine(c+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r,c+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*r); } }
    }
}
