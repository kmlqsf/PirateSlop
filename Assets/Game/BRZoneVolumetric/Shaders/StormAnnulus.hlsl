#ifndef PIRATESLOP_STORM_ANNULUS
#define PIRATESLOP_STORM_ANNULUS
#include "StormCloudBoundary.hlsl"
float4 _StormCenterWater;
float4 _StormBand;
float4 _StormShape;
float4 _StormStyle;
float4 _StormLightning;
float4 _StormLightningSecondary;
float4 _StormLightningColor;
float4 _StormTestLightningTiming;
float4 _StormSmokeFlashVolumes[12];
float4 _StormSmokeFlashChannelStarts[72];
float4 _StormSmokeFlashChannelEnds[72];

TEXTURE3D(_StormVolumeDensity);
SAMPLER(sampler_StormVolumeDensity);
TEXTURE3D(_PirateStormNearNoise);
SAMPLER(sampler_PirateStormNearNoise);
#include "StormTestCloudField.hlsl"
#include "StormTestSmokeField.hlsl"
float StormTestLightningScatter(float3 positionWS,float4 field,float3 normal,float4 flash,int group)
{
    if(flash.w<=0.0 || dot(positionWS-flash.xyz,positionWS-flash.xyz)>3364.0)return 0.0;
    float flux=0.0;
    float3 source=0.0;
    float2 radial=normalize(positionWS.xz-_StormCenterWater.xz);
    float3 tangent=float3(-radial.y,0,radial.x);
    [unroll] for(int channel=0;channel<6;channel++)
    {
        float4 start=_StormSmokeFlashChannelStarts[group*6+channel];
        float3 edge=_StormSmokeFlashChannelEnds[group*6+channel].xyz-start.xyz;
        float along=saturate(dot(positionWS-start.xyz,edge)/max(.001,dot(edge,edge)));
        float3 channelPosition=start.xyz+edge*along;
        float3 separation=channelPosition-positionWS;
        float sideways=dot(separation,tangent);
        float distanceSquared=dot(separation,separation)-sideways*sideways*.74;
        float energy=start.w*(.7/(1.0+distanceSquared*.09)+.3/(1.0+distanceSquared*.019))
            *(1.0-smoothstep(484.0,1296.0,distanceSquared));
        flux+=energy;source+=channelPosition*energy;
    }
    if(flux<.005)return 0.0;
    source/=flux;
    float3 toSource=source-positionWS;
    float distanceWS=length(toSource);
    float3 unusedNormal;
    float middleDensity=StormTestSmokeField(positionWS+toSource*.55,1.5,false,unusedNormal).r;
    float opticalDepth=distanceWS*(middleDensity*.7+field.r*.3);
    float transport=exp(-opticalDepth*.155);
    float escape=lerp(.35,1.15,saturate(field.a));
    float facing=.55+.45*abs(dot(normal,toSource/max(.001,distanceWS)));
    return flash.w*flux*transport*escape*facing;
}
float4 StormVolumeField(float3 p,float mip)
{
#if defined(_STORM_TEST_CLOUD_WALL)
    return StormTestCloudField(p,mip);
#else
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
    float3 coreUV=(p+float3(_StormShape.z*.6,0,0))/1024.0;
    float4 coreNoise=SAMPLE_TEXTURE3D_LOD(_PirateStormNearNoise,sampler_PirateStormNearNoise,coreUV,1);
    float rise=smoothstep(0.0,60.0,height);
    float width=_StormBand.y+_StormBand.z;
    float inward=_StormBand.z-width+(coreNoise.r-.5)*lerp(1.0,5.0,rise);
    float innerFade=smoothstep(inward,inward+lerp(.7,12.0,rise),sd);
    float outerFade=1-smoothstep(_StormBand.z-1.5,_StormBand.z,sd);
    float crown=scale*(550.0+coreNoise.r*90.0);
    float core=innerFade*outerFade*(1-smoothstep(crown-35.0,crown,height))*(.82+.18*coreNoise.r)*lerp(1.0,10.0,smoothstep(20.0,100.0,height))*smoothstep(-2.0,0.0,height);
    if(core>density)
    {
        density=core;
        float3 radial=normalize(float3(local.x,0,local.z)+.00001);
        float3 curl=coreNoise.gba*2.0-1.0;
        normalWS=normalize(-radial+curl*.45+float3(0,.25,0));
    }
    float distanceToEye=distance(p,GetCameraPositionWS());
    if(_PirateStormNearMedium>.0001 && distanceToEye<35.0)
    {
        float localDensity=_PirateStormNearMedium*(.40+.60*smoothstep(.22,.78,grain))*(1-smoothstep(29.0,35.0,distanceToEye));
        if(localDensity>density){density=localDensity;normalWS=normalize(nearField.gba*2.0-1.0+.00001);}
    }
    return float4(density,normalWS);
#endif
}

float StormInward(float h)
{
    return StormBoundaryInward(h, _StormShape.y);
}
float2 StormRotate(float2 p, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}
float StormDensityMask(float3 p, float mip, bool lightSampling, out float macro, out float billow, out float3 flow)
{
    macro = 0.0;
    billow = 0.0;
    flow = 0.0;
    float h = (p.y - _StormCenterWater.y) / max(1.0, _StormShape.x);
    if (h <= (_PirateStormBackdrop.x > .5 ? 0.0 : -.01) || h >= 1.0 || _StormShape.w < 0.5) return 0.0;
    float radius = _StormBand.x - StormInward(h) + StormBoundaryOffset(p, _StormBand.x);
    float d = length(p.xz - _StormCenterWater.xz);
    float innerWidth=StormWallInner(p.y,_StormBand.y);
    float outerWidth=StormWallOuter(p.y,_StormBand.z);
    if (d <= radius - innerWidth || d >= radius + outerWidth) return 0.0;

    float3 local = p - _StormCenterWater.xyz;
    float t = _StormShape.z;
    float scale = max(0.00001, _StormStyle.z);
    float3 rolling = local;
    rolling.xz = StormRotate(local.xz, -_StormStyle.w * 0.00042);
    float3 q = rolling * scale * float3(1.0, _PirateStormBackdrop.x > .5 ? 1.5 : 1.4, 1.0);
    float3 phase = q.zxy * 2.7 + q.yzx * 1.1;
    float3 primaryCurl = sin(phase + float3(t * 0.137, t * 0.173 + 2.1, -t * 0.119 + 4.7));
    float3 primaryCoords = q * 0.52 + primaryCurl * 0.105;
    primaryCoords.y -= t * 0.0031;
    float primaryA = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, primaryCoords, mip).r;
    float3 overlapCoords = q * 0.61 + float3(0.37, 0.63, 0.19) - primaryCurl.yzx * 0.08;
    overlapCoords.y += t * 0.0023;
    float primaryB = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, overlapCoords, mip).r;
    float unionBlend = saturate(0.5 + 0.5 * (primaryA - primaryB) / 0.12);
    macro = lerp(primaryB, primaryA, unionBlend) + 0.12 * unionBlend * (1.0 - unionBlend);
    macro = smoothstep(0.54, 0.94, macro);

    billow = 0.65;
    if (!lightSampling)
    {
        float3 secondary = local;
        secondary.xz = StormRotate(local.xz, _StormStyle.w * 0.00027 + 0.71);
        float3 secondaryQ = secondary * scale;
        float3 secondaryCurl = sin(secondaryQ.yzx * 3.9 + float3(-t * 0.487 + 1.7, t * 0.373 + 3.2, t * 0.563));
        flow = secondaryQ * 1.85 + secondaryCurl * 0.145 + primaryCurl * 0.065;
        flow.y -= t * 0.011;
        billow = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, flow + float3(0.19, 0.47, 0.83), mip).r;
        billow = smoothstep(0.46, 0.94, billow);
    }

    float softness = max(.1,min(max(_StormBand.w,StormWallSpread(p.y)*55.0),(innerWidth+outerWidth)*.30));
    float lobes = saturate(macro * 0.68 + billow * 0.32);
    bool backdrop = _PirateStormBackdrop.x > 0.5;
    float largeMass = backdrop ? StormBackdropMacro(p) : lobes;
    if (backdrop) lobes = saturate(largeMass * 0.65 + lobes * 0.35);
    float carve = (1.0 - lobes) * (backdrop ? .72 : .35);
    float innerEdge = radius - innerWidth + innerWidth * carve;
    float outerEdge = radius + outerWidth - outerWidth * carve;
    float inner = smoothstep(innerEdge, innerEdge + softness, d);
    float outer = 1.0 - smoothstep(outerEdge - softness, outerEdge, d);
    float crownHeight = StormBoundaryCrownHeight(p, _StormShape.x) / max(1.0, _StormShape.x);
    float crown = 1.0 - smoothstep(crownHeight - (backdrop ? 0.065 : 0.14), crownHeight, h);
    float baseFade = smoothstep(_PirateStormBackdrop.x > .5 ? 0.0 : -3.0, _PirateStormBackdrop.x > .5 ? max(0.1,min(6.0,_StormShape.x*.018)) : 1.0,p.y-_StormCenterWater.y);
    float cavities = backdrop ? smoothstep(0.14, 0.40, largeMass) : 1.0;
    return inner * outer * baseFade * crown * cavities;
}
bool StormCylinder(float2 p, float2 d, float radius, out float2 span)
{
    float a = dot(d, d);
    float c = dot(p, p) - radius * radius;
    span = float2(-1e19, 1e19);
    if (a < 1e-8) return c <= 0.0;
    float b = dot(p, d);
    float discriminant = b * b - a * c;
    if (discriminant < 0.0) return false;
    float root = sqrt(max(0.0, discriminant));
    span = float2(-b - root, -b + root) / a;
    return true;
}
bool StormRayIntervals(float3 origin, float3 direction, float maxDistance, out float4 spans)
{
    spans = 0.0;
    if (_StormShape.w < 0.5) return false;
    if(_PirateStormTestClouds.x>.5)
    {
        float2 shell;
        float2 p=origin.xz-_StormCenterWater.xz;
        if(!StormCylinder(origin.xz-_StormTestSmokeMap.xy,direction.xz,_StormTestSmokeMap.z,shell))return false;
        float2 vertical=float2(-1e19,1e19);
        if(abs(direction.y)>.00001)
        {
            vertical=(float2(StormTestSmokeBottom(),_PirateStormTestClouds.z)-origin.y)/direction.y;
            vertical=float2(min(vertical.x,vertical.y),max(vertical.x,vertical.y));
        }
        else if(origin.y<StormTestSmokeBottom() || origin.y>_PirateStormTestClouds.z)return false;
        float begin=max(0.0,max(shell.x,vertical.x));
        float end=min(maxDistance,min(shell.y,vertical.y));
        if(end<=begin)return false;
        float2 hole;
        if(!StormCylinder(p,direction.xz,max(0.0,_StormBand.x-STORM_SMOKE_FRINGE),hole))
        {
            spans=float4(begin,end,end,end);
            return true;
        }
        float front=clamp(hole.x,begin,end),back=clamp(hole.y,begin,end);
        spans=float4(begin,front,back,end);
        if(front<=begin+.001)spans=float4(back,end,end,end);
        return (spans.y-spans.x)+(spans.w-spans.z)>.001;
    }
    if(_PirateStormVolume3D>.5 && _PirateStormBackdrop.x<.5)
    {
        float2 shell;
        float2 p=origin.xz-_StormCenterWater.xz;
        if(!StormCylinder(p,direction.xz,_StormBand.x+_StormBand.z,shell))return false;
        float2 vertical=float2(-1e19,1e19);
        if(abs(direction.y)>.00001)
        {
            vertical=(float2(_StormCenterWater.y-2,_StormCenterWater.y+_StormShape.x)-origin.y)/direction.y;
            vertical=float2(min(vertical.x,vertical.y),max(vertical.x,vertical.y));
        }
        else if(origin.y<_StormCenterWater.y-2||origin.y>_StormCenterWater.y+_StormShape.x)return false;
        float begin=max(0,max(shell.x,vertical.x)),end=min(maxDistance,min(shell.y,vertical.y));
        if(_PirateStormNearMedium>.001){spans=float4(0,max(end,min(maxDistance,35.0)),0,0);spans.zw=spans.yy;return spans.y>.001;}
        if(end<=begin)return false;
        float highest=max(origin.y+begin*direction.y,origin.y+end*direction.y)-_StormCenterWater.y;
        float width=(highest<=18.0?(_StormBand.y+_StormBand.z):700.0*min(1.0,_StormBand.x/1200.0))+2.0;
        float2 hole;
        if(!StormCylinder(p,direction.xz,max(0.0,_StormBand.x-width+_StormBand.z),hole)){spans=float4(begin,end,end,end);return true;}
        float front=clamp(hole.x,begin,end),back=clamp(hole.y,begin,end);
        spans=float4(begin,front,back,end);
        if(front<=begin+.001)spans=float4(back,end,end,end);
        return (spans.y-spans.x)+(spans.w-spans.z)>.001;
    }
    float2 radial;
    float2 p = origin.xz - _StormCenterWater.xz;
    float boundaryAmplitude = StormBoundaryAmplitude(_StormBand.x);
    if (!StormCylinder(p,direction.xz,_StormBand.x+_StormBand.z+(_PirateStormBackdrop.x>.5?0.0:640.0)+boundaryAmplitude+2.0,radial))return false;
    float highestRay=max(origin.y,origin.y+direction.y*max(0.0,radial.y));
    float spread=StormWallSpread(highestRay);
    if (!StormCylinder(p,direction.xz,_StormBand.x+_StormBand.z+spread*640.0+boundaryAmplitude+2.0,radial))return false;
    float bottom = _StormCenterWater.y;
    float top = bottom + _StormShape.x;
    if(_PirateStormBackdrop.x<.5 && _PirateStormBillows>.5 && GetCameraPositionWS().y-bottom<35.0)
        top=min(top,bottom+55.0);
    float2 vertical = float2(-1e19, 1e19);
    if (abs(direction.y) < 1e-6)
    {
        if (origin.y < bottom || origin.y > top) return false;
    }
    else
    {
        vertical = (float2(bottom, top) - origin.y) / direction.y;
        vertical = float2(min(vertical.x, vertical.y), max(vertical.x, vertical.y));
    }
    float entry = max(0.0, max(radial.x, vertical.x));
    float exit = min(maxDistance, min(radial.y, vertical.y));
    if (exit <= entry) return false;
    spans = float4(entry, exit, exit, exit);
    float clearRadius = max(0.0, _StormBand.x - _StormBand.y - _StormShape.y - boundaryAmplitude - 2.0);
    float2 hole;
    if (clearRadius > 0.0 && StormCylinder(p, direction.xz, clearRadius, hole))
    {
        float holeStart = max(entry, hole.x);
        float holeEnd = min(exit, hole.y);
        if (holeEnd > holeStart)
        {
            spans = float4(entry, holeStart, holeEnd, exit);
            if (spans.y - spans.x < 0.001) spans = float4(holeEnd, exit, exit, exit);
        }
    }
    return (spans.y - spans.x) + (spans.w - spans.z) > 0.001;
}
float StormRayDistance(float4 spans, float compressedDistance)
{
    float firstLength = spans.y - spans.x;
    return compressedDistance < firstLength ? spans.x + compressedDistance : spans.z + compressedDistance - firstLength;
}
#endif
