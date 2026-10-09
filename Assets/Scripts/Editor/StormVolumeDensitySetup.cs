using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class StormVolumeDensitySetup
    {
        const int Side=64,Width=128,Depth=128;
        const string AssetPath="Assets/Game/BRZoneVolumetric/Resources/StormVolumeDensity.asset";
        static float[] values;
        static Vector4[][] lobes;
        static int slice;
        [MenuItem("PirateSlop/VFX/Storm Volume/Prepare Density")]
        public static void Prepare()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop owned QA before baking");
            values=new float[Width*Side*Depth];
            lobes=new Vector4[4][];
            for(int tile=0;tile<4;tile++)
            {
                lobes[tile]=new Vector4[48];
                lobes[tile][0]=new Vector4(0,-.18f,0,.255f);
                lobes[tile][1]=new Vector4(-.18f,-.05f,-.03f,.205f);
                lobes[tile][2]=new Vector4(.17f,.015f,.065f,.23f);
                lobes[tile][3]=new Vector4(-.115f,.155f,.015f,.19f);
                lobes[tile][4]=new Vector4(.10f,.19f,-.055f,.215f);
                lobes[tile][5]=new Vector4((Hash((uint)(tile+61))-.5f)*.2f,.265f,.015f,.14f);
                for(int i=0;i<6;i++)
                {
                    uint seed=(uint)(tile*7919+i*137+53);
                    var p=lobes[tile][i];
                    p.x+=(Hash(seed)-.5f)*.045f;p.z+=(Hash(seed+1)-.5f)*.055f;p.w*=Mathf.Lerp(.9f,1.05f,Hash(seed+2));
                    lobes[tile][i]=p;
                }
                for(int i=6;i<48;i++)
                {
                    uint seed=(uint)(tile*7919+i*137+53);
                    var parent=lobes[tile][i%6];
                    float theta=Hash(seed)*Mathf.PI*2,y=Mathf.Lerp(-.55f,.9f,Hash(seed+1)),ring=Mathf.Sqrt(1-y*y);
                    var direction=new Vector3(Mathf.Cos(theta)*ring,y,Mathf.Sin(theta)*ring);
                    float radius=Mathf.Lerp(.052f,.098f,Hash(seed+3));
                    var center=new Vector3(parent.x,parent.y,parent.z)+direction*parent.w*Mathf.Lerp(.85f,1.08f,Hash(seed+2));
                    float bound=.465f-radius;
                    center.x=Mathf.Clamp(center.x,-bound,bound);center.y=Mathf.Clamp(center.y,-bound,bound);center.z=Mathf.Clamp(center.z,-bound,bound);
                    lobes[tile][i]=new Vector4(center.x,center.y,center.z,radius);
                }
            }
            slice=0;EditorApplication.update-=Bake;EditorApplication.update+=Bake;
        }
        [MenuItem("PirateSlop/VFX/Storm Volume/Apply Sky Envelope")]
        public static void ApplySkyEnvelope()
        {
            const string path="Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsUtilities.hlsl";
            string text=File.ReadAllText(path);
            text=text.Replace("    float stormCoverage=lerp(.75,.96,weather.y);",
                "    float2 stormPattern=PirateCloudNoise2(AnimateShapeNoisePosition(positionPS).xz/1350.0+float2(_ClearCloudSeed+151.3,_ClearCloudSeed+69.4));\n    float stormCoverage=gameStorm?lerp(.28,.92,smoothstep(.26,.58,stormPattern.x)):lerp(.75,.96,weather.y);");
            text=text.Replace("(3100.0+warp.y*250.0-bottom)","(850.0+warp.y*170.0-bottom)")
                .Replace("(4200.0+weather.y*900.0-bottom)","(1800.0+stormPattern.y*550.0-bottom)");
            File.WriteAllText(path,text);
            EditorApplication.delayCall+=()=>AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("PirateSlop/VFX/Storm Volume/Apply Cloud Filtering")]
        public static void ApplyCloudFiltering()
        {
            const string annulus="Assets/Game/BRZoneVolumetric/Shaders/StormAnnulus.hlsl";
            string text=File.ReadAllText(annulus).Replace(@"float4 StormVolumeField(float3 p)
{
    float height=p.y-_StormCenterWater.y;
    float3 local=p-_StormCenterWater.xyz;
    float sd=length(local.xz)-_StormBand.x;
    float scale=min(1.0,_StormBand.x/1200.0);
    float theta=atan2(local.z,local.x)*20.37183272,cell=floor(theta);
    float density=0.0,normalWeight=0.0;
    float3 normalWS=0.0;
    if(height>18.0)
    {
        [unroll] for(int j=-1;j<=1;j++)
        {
            float id=cell+j,angle=(id+.5)*.0490873852;
            float3 r=float3(cos(angle),0,sin(angle)),t=float3(-r.z,0,r.x);
            [unroll] for(int tier=0;tier<3;tier++)
            {
                float3 random=StormBoundaryHash(float2(fmod(id+128.0,128.0),71.19+tier*19.57));
                float extent=max(25.0,(_StormBand.x*.0490873852)*lerp(1.42,1.88,random.x));
                float width=max(25.0,scale*lerp(250.0,320.0,random.y)),center=_StormBand.z-width*.5;
                float h=max(25.0,scale*lerp(300.0,390.0,random.z));
                float centerHeight=scale*(70.0+tier*130.0+(random.x-.5)*85.0);
                float3 q=float3((theta-id-.5+(random.z-.5)*.24)*(_StormBand.x*.0490873852)/extent,(height-centerHeight)/h,(sd-center)/width);
                if(max(abs(q.x),max(abs(q.y),abs(q.z)))>.485)continue;
                float tile=floor(random.z*3.999);
                float3 uv=float3((q.x+.5)*.5+frac(tile*.5),q.y+.5,(q.z+.5)*.5+floor(tile*.5)*.5);
                float4 voxel=SAMPLE_TEXTURE3D_LOD(_StormVolumeDensity,sampler_StormVolumeDensity,uv,0);
                voxel.r*=smoothstep(18.0,50.0,height);
                density=max(density,voxel.r);
                float3 n=voxel.gba*2.0-1.0;
                normalWS+=(t*n.x/extent+float3(0,n.y/h,0)+r*n.z/width)*voxel.r;
                normalWeight+=voxel.r;
            }
        }
    }
    normalWS=normalWeight>.001?normalize(normalWS+.00001):float3(0,1,0);
    float4 nearField=SAMPLE_TEXTURE3D_LOD(_PirateStormNearNoise,sampler_PirateStormNearNoise,(p+float3(_StormShape.z*.6,0,0))/288.0,0);
    float grain=nearField.r;
    float lowerTheta=atan2(local.z,local.x)*325.9493235,lowerCell=floor(lowerTheta);
    [unroll] for(int k=-1;k<=1;k++)
    {
        float id=lowerCell+k,angle=(id+.5)*.00306796158;
        float3 r=float3(cos(angle),0,sin(angle)),t=float3(-r.z,0,r.x);
        float3 random=StormBoundaryHash(float2(fmod(id+2048.0,2048.0),193.71));
        float extent=max(2.0,_StormBand.x*.00306796158*lerp(.70,1.05,random.x));
        float h=lerp(18.0,32.0,random.y);
        float centerHeight=lerp(7.0,16.0,random.z);
        float halfWidth=(_StormBand.y+_StormBand.z)*.5;
        float3 q=float3((lowerTheta-id-.5)*_StormBand.x*.00306796158/extent,(height-centerHeight)/h,(sd-(_StormBand.z-halfWidth))/halfWidth);
        float lowerDensity=(1-smoothstep(.60,1.0,length(q)))*(.70+.30*smoothstep(.18,.82,grain));
        if(lowerDensity>density)
        {
            density=lowerDensity;
            normalWS=normalize(t*q.x/extent+float3(0,q.y/h,0)+r*q.z/halfWidth+.00001);
        }
    }
    float distanceToEye=distance(p,GetCameraPositionWS());
    if(_PirateStormNearMedium>.0001 && distanceToEye<35.0)
    {
        float localDensity=_PirateStormNearMedium*(.40+.60*smoothstep(.22,.78,grain))*(1-smoothstep(29.0,35.0,distanceToEye));
        if(localDensity>density){density=localDensity;normalWS=normalize(nearField.gba*2.0-1.0+.00001);}
    }
    return float4(saturate(density),normalWS);
}
",@"float4 StormVolumeField(float3 p,float mip)
{
    float height=p.y-_StormCenterWater.y;
    float3 local=p-_StormCenterWater.xyz;
    float sd=length(local.xz)-_StormBand.x;
    float scale=min(1.0,_StormBand.x/1200.0);
    float theta=atan2(local.z,local.x)*20.37183272,cell=floor(theta);
    float density=0.0;
    float3 normalWS=0.0;
    if(height>18.0)
    {
        [unroll] for(int j=-1;j<=1;j++)
        {
            float id=cell+j,angle=(id+.5)*.0490873852;
            float3 r=float3(cos(angle),0,sin(angle)),t=float3(-r.z,0,r.x);
            [unroll] for(int tier=0;tier<3;tier++)
            {
                float3 random=StormBoundaryHash(float2(fmod(id+128.0,128.0),71.19+tier*19.57));
                float extent=max(25.0,(_StormBand.x*.0490873852)*lerp(1.42,1.88,random.x));
                float width=max(25.0,scale*lerp(250.0,320.0,random.y)),center=_StormBand.z-width*.5;
                float h=max(25.0,scale*lerp(300.0,390.0,random.z));
                float centerHeight=scale*(70.0+tier*130.0+(random.x-.5)*85.0);
                float3 q=float3((theta-id-.5+(random.z-.5)*.24)*(_StormBand.x*.0490873852)/extent,(height-centerHeight)/h,(sd-center)/width);
                if(max(abs(q.x),max(abs(q.y),abs(q.z)))>.485)continue;
                float tile=floor(random.z*3.999);
                float3 uv=float3((q.x+.5)*.5+frac(tile*.5),q.y+.5,(q.z+.5)*.5+floor(tile*.5)*.5);
                float4 voxel=SAMPLE_TEXTURE3D_LOD(_StormVolumeDensity,sampler_StormVolumeDensity,uv,mip);
                voxel.r*=smoothstep(18.0,50.0,height);
                if(voxel.r>density)
                {
                    density=voxel.r;
                    float3 n=voxel.gba*2.0-1.0;
                    normalWS=normalize(t*n.x/extent+float3(0,n.y/h,0)+r*n.z/width+.00001);
                }
            }
        }
    }
    if(density<.001)normalWS=float3(0,1,0);
    float4 nearField=SAMPLE_TEXTURE3D_LOD(_PirateStormNearNoise,sampler_PirateStormNearNoise,(p+float3(_StormShape.z*.6,0,0))/288.0,0);
    float grain=nearField.r;
    float lowerTheta=atan2(local.z,local.x)*81.48733086,lowerCell=floor(lowerTheta);
    [unroll] for(int k=-1;k<=1;k++)
    {
        float id=lowerCell+k,angle=(id+.5)*.0122718463;
        float3 r=float3(cos(angle),0,sin(angle)),t=float3(-r.z,0,r.x);
        float3 random=StormBoundaryHash(float2(fmod(id+512.0,512.0),193.71));
        float extent=max(2.0,_StormBand.x*.0122718463*lerp(.75,1.20,random.x));
        float h=lerp(45.0,85.0,random.y);
        float centerHeight=lerp(18.0,32.0,random.z);
        float halfWidth=(_StormBand.y+_StormBand.z)*.5;
        float3 q=float3((lowerTheta-id-.5)*_StormBand.x*.0122718463/extent,(height-centerHeight)/h,(sd-(_StormBand.z-halfWidth))/halfWidth);
        float lowerDensity=(1-smoothstep(.60,1.0,length(q)))*(.70+.30*smoothstep(.18,.82,grain));
        if(lowerDensity>density)
        {
            density=lowerDensity;
            normalWS=normalize(t*q.x/extent+float3(0,q.y/h,0)+r*q.z/halfWidth+.00001);
        }
    }
    float distanceToEye=distance(p,GetCameraPositionWS());
    if(_PirateStormNearMedium>.0001 && distanceToEye<35.0)
    {
        float localDensity=_PirateStormNearMedium*(.40+.60*smoothstep(.22,.78,grain))*(1-smoothstep(29.0,35.0,distanceToEye));
        if(localDensity>density){density=localDensity;normalWS=normalize(nearField.gba*2.0-1.0+.00001);}
    }
    return float4(saturate(density),normalWS);
}
");
            File.WriteAllText(annulus,text);
            const string utility="Assets/Game/BRZoneVolumetric/Shaders/VolumetricCloudsUtilities.hlsl";
            text=File.ReadAllText(utility).Replace("StormVolumeField(positionPS)","StormVolumeField(positionPS,lightSampling?1.0:noiseMipOffset)");
            File.WriteAllText(utility,text);
            const string trace="Assets/Game/BRZoneVolumetric/Shaders/VolumetricClouds.hlsl";
            text=File.ReadAllText(trace).Replace("EvaluateCloudProperties(currentPositionPS, 0.0, erosionMipOffset, false, false, properties);","EvaluateCloudProperties(currentPositionPS, min(2.0,log2(max(1.0,stepS/5.0))), erosionMipOffset, false, false, properties);");
            File.WriteAllText(trace,text);
            EditorApplication.delayCall+=()=>{AssetDatabase.ImportAsset(annulus,ImportAssetOptions.ForceUpdate);AssetDatabase.ImportAsset(utility,ImportAssetOptions.ForceUpdate);AssetDatabase.ImportAsset(trace,ImportAssetOptions.ForceUpdate);};
        }


        [MenuItem("PirateSlop/VFX/Storm Volume/Apply Medium Lighting")]
        public static void ApplyMediumLighting()
        {
            const string screen="Assets/Resources/Storm/RainScreen.shader";
            string text=File.ReadAllText(screen).Replace("float opacity = min(.45,1-exp(-outside * distance * vertical / 550));","float exteriorWeight=smoothstep(10.0,50.0,length(start)-_RainWeather.z);\n                    float opacity = min(.45,1-exp(-outside * distance * vertical / 550))*exteriorWeight;");
            File.WriteAllText(screen,text);
            const string utility="Assets/Game/BRZoneVolumetric/Shaders/VolumetricCloudsUtilities.hlsl";
            text=File.ReadAllText(utility)
                .Replace("return exp(-opticalDepth * 0.48);","return exp(-opticalDepth * (_PirateStormVolume3D>.5 && _PirateStormBackdrop.x<.5?1.0:.48));")
                .Replace("half lightWeight=saturate(visibility*.45+diffuseFill*.55);","half lightWeight=saturate(.12+visibility*(.13+.75*diffuseFill));");
            File.WriteAllText(utility,text);
            EditorApplication.delayCall+=()=>{AssetDatabase.ImportAsset(screen,ImportAssetOptions.ForceUpdate);AssetDatabase.ImportAsset(utility,ImportAssetOptions.ForceUpdate);};
        }


        [MenuItem("PirateSlop/VFX/Storm Volume/Apply World Proportions")]
        public static void ApplyWorldProportions()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop owned QA before applying");
            const string path="Assets/Game/BRZoneVolumetric/Shaders/StormAnnulus.hlsl";
            string text=File.ReadAllText(path);
            text=text.Replace("atan2(local.z,local.x)*20.37183272","atan2(local.z,local.x)*10.18591636")
                .Replace("(id+.5)*.0490873852","(id+.5)*.0981747704")
                .Replace("fmod(id+128.0,128.0)","fmod(id+64.0,64.0)")
                .Replace("(_StormBand.x*.0490873852)*lerp(1.42,1.88,random.x)","min(650.0,(_StormBand.x*.0981747704)*lerp(1.35,1.65,random.x))")
                .Replace("scale*lerp(250.0,320.0,random.y)","scale*lerp(520.0,650.0,random.y)")
                .Replace("scale*lerp(300.0,390.0,random.z)","scale*lerp(560.0,650.0,random.z)")
                .Replace("scale*(70.0+tier*130.0+(random.x-.5)*85.0)","scale*(170.0+tier*280.0+(random.x-.5)*110.0)")
                .Replace("(_StormBand.x*.0490873852)/extent","(_StormBand.x*.0981747704)/extent")
                .Replace("atan2(local.z,local.x)*81.48733086","atan2(local.z,local.x)*162.9746617")
                .Replace("(id+.5)*.0122718463","(id+.5)*.00613592315")
                .Replace("fmod(id+512.0,512.0)","fmod(id+1024.0,1024.0)")
                .Replace("_StormBand.x*.0122718463*lerp(.75,1.20,random.x)","min(30.0,_StormBand.x*.00613592315*lerp(.75,1.20,random.x))")
                .Replace("float h=lerp(45.0,85.0,random.y);","float h=lerp(12.0,20.0,random.y);")
                .Replace("float centerHeight=lerp(18.0,32.0,random.z);","float centerHeight=lerp(3.0,8.0,random.z);")
                .Replace("(lowerTheta-id-.5)*_StormBand.x*.0122718463/extent","(lowerTheta-id-.5)*_StormBand.x*.00613592315/extent")
                .Replace("float width=320.0*min(1.0,_StormBand.x/1200.0)+2.0;","float width=(highest<=18.0?(_StormBand.y+_StormBand.z):700.0*min(1.0,_StormBand.x/1200.0))+2.0;");
            File.WriteAllText(path,text);
            string prefabPath=AssetDatabase.GetAssetPath(Resources.Load<GameObject>("BRStormVolume"));
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.GetComponent<PirateSlop.StormVolumeController>().StormHeight=1100;
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            EditorApplication.delayCall+=()=>AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        }


        [MenuItem("PirateSlop/VFX/Storm Volume/Connect Bank")]
        public static void ConnectBank()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop owned QA before applying");
            const string path="Assets/Game/BRZoneVolumetric/Shaders/StormAnnulus.hlsl";
            string text=File.ReadAllText(path)
                .Replace("if(height>18.0)","if(height>-2.0)")
                .Replace("float width=max(25.0,scale*lerp(520.0,650.0,random.y)),center=_StormBand.z-width*.5;",
                    "float fullWidth=max(25.0,scale*lerp(520.0,650.0,random.y));\n                float neck=_StormBand.y+_StormBand.z;\n                float rise=saturate((height-10.0)/80.0);\n                float width=lerp(neck,fullWidth,rise*rise*(3.0-2.0*rise)),center=_StormBand.z-width*.5;\n                float widthGradient=(fullWidth-neck)*6.0*rise*(1.0-rise)/80.0;")
                .Replace("voxel.r*=smoothstep(18.0,50.0,height);","voxel.r*=smoothstep(-2.0,2.0,height);")
                .Replace("float3(0,n.y/h,0)+r*n.z/width","float3(0,n.y/h-n.z*(sd-_StormBand.z)*widthGradient/(width*width),0)+r*n.z/width");
            File.WriteAllText(path,text);
            EditorApplication.delayCall+=()=>AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        }


        [MenuItem("PirateSlop/VFX/Storm Volume/Pack Fixed Groups")]
        public static void PackFixedGroups()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop owned QA before applying");
            const string path="Assets/Game/BRZoneVolumetric/Shaders/StormAnnulus.hlsl";
            string text=File.ReadAllText(path);
            int begin=text.IndexOf("float4 StormVolumeField("),end=text.IndexOf("float StormInward(",begin);
            if(begin<0||end<0)throw new InvalidOperationException("Storm field anchors absent");
            text=text.Substring(0,begin)+@"float4 StormVolumeField(float3 p,float mip)
{
    float height=p.y-_StormCenterWater.y;
    float3 local=p-_StormCenterWater.xyz;
    float sd=length(local.xz)-_StormBand.x;
    float scale=min(1.0,_StormBand.x/1200.0);
    float density=0.0;
    float3 normalWS=float3(0,1,0);
    [unroll] for(int tier=0;tier<3;tier++)
    {
        float count=tier==0?256.0:64.0;
        float angleStep=6.283185307/count;
        float theta=atan2(local.z,local.x)/angleStep,cell=floor(theta);
        [unroll] for(int j=-1;j<=1;j++)
        {
            float id=cell+j,angle=(id+.5)*angleStep;
            float3 r=float3(cos(angle),0,sin(angle)),t=float3(-r.z,0,r.x);
            float3 random=StormBoundaryHash(float2(fmod(id+count,count),71.19+tier*19.57));
            float extent=max(25.0,min(650.0,_StormBand.x*angleStep*lerp(tier==0?1.15:1.35,tier==0?1.40:1.65,random.x)));
            float width=max(25.0,scale*lerp(tier==0?105.0:520.0,tier==0?140.0:650.0,random.y));
            float h=max(25.0,scale*lerp(tier==0?105.0:560.0,tier==0?140.0:650.0,random.z));
            float center=_StormBand.z-width*.5;
            float centerHeight=scale*((tier==0?65.0:tier==1?305.0:675.0)+(random.x-.5)*(tier==0?22.0:110.0));
            float3 q=float3((theta-id-.5+(random.z-.5)*.24)*_StormBand.x*angleStep/extent,(height-centerHeight)/h,(sd-center)/width);
            if(max(abs(q.x),max(abs(q.y),abs(q.z)))>.485)continue;
            float tile=floor(random.z*3.999);
            float3 uv=float3((q.x+.5)*.5+frac(tile*.5),q.y+.5,(q.z+.5)*.5+floor(tile*.5)*.5);
            float4 voxel=SAMPLE_TEXTURE3D_LOD(_StormVolumeDensity,sampler_StormVolumeDensity,uv,mip);
            float rise=smoothstep(12.0,42.0,height);
            float footWidth=lerp(_StormBand.y+_StormBand.z,700.0*scale,rise);
            float footFade=smoothstep(_StormBand.z-footWidth,_StormBand.z-footWidth+lerp(.8,20.0,rise),sd);
            voxel.r*=footFade*smoothstep(-2.0,2.0,height);
            if(voxel.r>density)
            {
                density=voxel.r;
                float3 n=voxel.gba*2.0-1.0;
                normalWS=normalize(t*n.x/extent+float3(0,n.y/h,0)+r*n.z/width+.00001);
            }
        }
    }
    float4 nearField=SAMPLE_TEXTURE3D_LOD(_PirateStormNearNoise,sampler_PirateStormNearNoise,(p+float3(_StormShape.z*.6,0,0))/288.0,0);
    float grain=nearField.r;
    float lowerTheta=atan2(local.z,local.x)*162.9746617,lowerCell=floor(lowerTheta);
    [unroll] for(int k=-1;k<=1;k++)
    {
        float id=lowerCell+k,angle=(id+.5)*.00613592315;
        float3 r=float3(cos(angle),0,sin(angle)),t=float3(-r.z,0,r.x);
        float3 random=StormBoundaryHash(float2(fmod(id+1024.0,1024.0),193.71));
        float extent=max(2.0,min(30.0,_StormBand.x*.00613592315*lerp(.75,1.20,random.x)));
        float h=lerp(12.0,20.0,random.y),centerHeight=lerp(3.0,8.0,random.z);
        float halfWidth=(_StormBand.y+_StormBand.z)*.5;
        float3 q=float3((lowerTheta-id-.5)*_StormBand.x*.00613592315/extent,(height-centerHeight)/h,(sd-(_StormBand.z-halfWidth))/halfWidth);
        float lowerDensity=(1-smoothstep(.60,1.0,length(q)))*(.70+.30*smoothstep(.18,.82,grain));
        if(lowerDensity>density)
        {
            density=lowerDensity;
            normalWS=normalize(t*q.x/extent+float3(0,q.y/h,0)+r*q.z/halfWidth+.00001);
        }
    }
    float distanceToEye=distance(p,GetCameraPositionWS());
    if(_PirateStormNearMedium>.0001 && distanceToEye<35.0)
    {
        float localDensity=_PirateStormNearMedium*(.40+.60*smoothstep(.22,.78,grain))*(1-smoothstep(29.0,35.0,distanceToEye));
        if(localDensity>density){density=localDensity;normalWS=normalize(nearField.gba*2.0-1.0+.00001);}
    }
    return float4(saturate(density),normalWS);
}

"+text.Substring(end);
            File.WriteAllText(path,text);
            const string utility="Assets/Game/BRZoneVolumetric/Shaders/VolumetricCloudsUtilities.hlsl";
            text=File.ReadAllText(utility).Replace("half lightWeight=saturate(.12+visibility*(.13+.75*diffuseFill));","half lightWeight=saturate(.12+.34*diffuseFill+visibility*.45*diffuseFill);");
            File.WriteAllText(utility,text);
            EditorApplication.delayCall+=()=>{AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);AssetDatabase.ImportAsset(utility,ImportAssetOptions.ForceUpdate);};
        }


        [MenuItem("PirateSlop/VFX/Storm Volume/Stabilize Integration")]
        public static void StabilizeIntegration()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop owned QA before applying");
            const string annulus="Assets/Game/BRZoneVolumetric/Shaders/StormAnnulus.hlsl";
            string text=File.ReadAllText(annulus).Replace("tier==0?65.0:tier==1?305.0:675.0","tier==0?32.0:tier==1?260.0:675.0");
            File.WriteAllText(annulus,text);
            const string trace="Assets/Game/BRZoneVolumetric/Shaders/VolumetricClouds.hlsl";
            text=File.ReadAllText(trace).Replace("ray.integrationNoise = GenerateRandomFloat(screenUV);","ray.integrationNoise = _PirateStormVolume3D>.5 && _PirateStormBackdrop.x<.5 ? .5 : GenerateRandomFloat(screenUV);");
            File.WriteAllText(trace,text);
            EditorApplication.delayCall+=()=>{AssetDatabase.ImportAsset(annulus,ImportAssetOptions.ForceUpdate);AssetDatabase.ImportAsset(trace,ImportAssetOptions.ForceUpdate);};
        }

        [MenuItem("PirateSlop/VFX/Storm Volume/Inspect Density Data")]
        public static void InspectDensityData()
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture3D>(AssetPath);
            string result="GraphicsFormat="+texture.graphicsFormat+"\nDataSRGB="+texture.isDataSRGB+"\nFormat="+texture.format+"\nFirst="+texture.GetPixel(0,0,0)+"\n";
            foreach(var c in typeof(Texture3D).GetConstructors())result+=c+"\n";
            File.WriteAllText("Captures/VfxSkyWaterReview20261007/volume-density-data.txt",result);
        }
        static float Hash(uint value)
        {
            value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;value^=value>>16;
            return (value&0x00ffffffu)/16777216f;
        }
        static float Noise(Vector3 p)
        {
            int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y),z=Mathf.FloorToInt(p.z);
            float a=p.x-x,b=p.y-y,c=p.z-z;a=a*a*(3-2*a);b=b*b*(3-2*b);c=c*c*(3-2*c);
            float result=0;
            for(int dz=0;dz<2;dz++)for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                result+=Hash(unchecked((uint)((x+dx)*73856093^(y+dy)*19349663^(z+dz)*83492791)))*(dx==0?1-a:a)*(dy==0?1-b:b)*(dz==0?1-c:c);
            return result;
        }
        static float Worley(Vector3 p,int tile)
        {
            int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y),z=Mathf.FloorToInt(p.z);
            float minimum=4;
            for(int dz=-1;dz<=1;dz++)for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {
                uint seed=unchecked((uint)((x+dx)*73856093^(y+dy)*19349663^(z+dz)*83492791)+((uint)tile+1)*7919u);
                var center=new Vector3(x+dx+.12f+Hash(seed)*.76f,y+dy+.12f+Hash(seed+137)*.76f,z+dz+.12f+Hash(seed+337)*.76f);
                minimum=Mathf.Min(minimum,(p-center).sqrMagnitude);
            }
            return Mathf.Clamp01(1-Mathf.Sqrt(minimum)*.9f);
        }
        static float Field(Vector3 p,int tile)
        {
            float distance=-1;
            for(int i=0;i<lobes[tile].Length;i++)
            {
                var l=lobes[tile][i];var q=p-new Vector3(l.x,l.y,l.z);
                float d=l.w-q.magnitude;
                float k=.020f,h=Mathf.Clamp01(.5f+.5f*(d-distance)/k);
                distance=Mathf.Lerp(distance,d,h)+k*h*(1-h);
            }
            float envelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.035f,.065f,distance));
            if(envelope<.0001f)return 0;
            var offset=new Vector3(tile*7.73f,tile*19.11f,tile*3.37f);
            float round=Worley(p*3.8f+offset,tile);
            float broad=Noise(p*1.7f+offset);
            float fine=Noise(p*9.5f+offset+Vector3.one*5.31f);
            float shape=round*.70f+broad*.23f+fine*.07f;
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.17f,.72f,shape-(1-envelope)*.61f));
        }
        static void Bake()
        {
            try
            {
                int end=Mathf.Min(Depth,slice+4);
                for(int z=slice;z<end;z++)for(int y=0;y<Side;y++)for(int x=0;x<Width;x++)
                {
                    int tile=x/Side+2*(z/Side);
                    var p=new Vector3((x%Side+.5f)/Side-.5f,(y+.5f)/Side-.5f,(z%Side+.5f)/Side-.5f);
                    values[x+Width*(y+Side*z)]=Field(p,tile);
                }
                slice=end;if(slice<Depth)return;
                EditorApplication.update-=Bake;
                var pixels=new Color[values.Length];
                for(int z=0;z<Depth;z++)for(int y=0;y<Side;y++)for(int x=0;x<Width;x++)
                {
                    int i=x+Width*(y+Side*z);
                    float gx=values[Mathf.Max(x-1,x/Side*Side)+Width*(y+Side*z)]-values[Mathf.Min(x+1,x/Side*Side+Side-1)+Width*(y+Side*z)];
                    float gy=values[x+Width*(Mathf.Max(0,y-1)+Side*z)]-values[x+Width*(Mathf.Min(Side-1,y+1)+Side*z)];
                    float gz=values[x+Width*(y+Side*Mathf.Max(z-1,z/Side*Side))]-values[x+Width*(y+Side*Mathf.Min(z+1,z/Side*Side+Side-1))];
                    var n=new Vector3(gx,gy,gz).normalized;
                    pixels[i]=new Color(values[i],n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
                }
                var texture=new Texture3D(Width,Side,Depth,TextureFormat.RGBA32,true){name="StormVolumeDensity",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
                texture.SetPixels(pixels);texture.Apply(true,false);
                var existing=AssetDatabase.LoadAssetAtPath<Texture3D>(AssetPath);
                if(existing==null){AssetDatabase.CreateAsset(texture,AssetPath);existing=texture;}
                else{EditorUtility.CopySerialized(texture,existing);UnityEngine.Object.DestroyImmediate(texture);}
                EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);
                File.WriteAllText("Captures/VfxSkyWaterReview20261007/volume-density-ready.txt","128x64x128 RGBA32: 4 real 64 cubed density/gradient bricks");
                values=null;lobes=null;Debug.Log("STORM_VOLUME_DENSITY_READY");
            }
            catch(Exception error){EditorApplication.update-=Bake;values=null;lobes=null;Debug.LogException(error);}
        }
    }
}
