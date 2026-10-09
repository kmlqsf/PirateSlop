#ifndef PIRATESLOP_STORM_TEST_FIELD
#define PIRATESLOP_STORM_TEST_FIELD
TEXTURE3D(_StormTestWallDensity);
SAMPLER(sampler_StormTestWallDensity);
TEXTURE3D(_StormTestWallNormals);
SAMPLER(sampler_StormTestWallNormals);
struct StormTestVoxel
{
    float4 value;
    float3 normal;
};
StormTestVoxel StormTestCloudBlend(StormTestVoxel a,StormTestVoxel b,float blend)
{
    float weightA=a.value.r*(1.0-blend),weightB=b.value.r*blend;
    float density=weightA+weightB;
    StormTestVoxel voxel;
    voxel.value=float4(density,(a.value.gba*weightA+b.value.gba*weightB)/max(.001,density));
    voxel.normal=(a.normal*weightA+b.normal*weightB)/max(.001,density);
    return voxel;
}
StormTestVoxel StormTestCloudTile(float3 coordinates,float mip,float id,bool sampleNormal)
{
    float3 random=StormBoundaryHash(float2(id,77.43));
    float mirror=random.z<.5?-1.0:1.0;
    float s,c;
    sincos(random.y*6.283185307,s,c);
    float2 crossSection=(coordinates.yz-.5)*float2(64,20);
    crossSection=float2(c*crossSection.x-s*crossSection.y,s*crossSection.x+c*crossSection.y);
    float3 uv=float3(coordinates.x*mirror+random.x,crossSection/float2(64,20)+.5);
    StormTestVoxel voxel;
    voxel.value=SAMPLE_TEXTURE3D_LOD(_StormTestWallDensity,sampler_StormTestWallDensity,uv,mip);
    voxel.normal=0;
    if(sampleNormal && voxel.value.r>.001)
    {
        voxel.normal=SAMPLE_TEXTURE3D_LOD(_StormTestWallNormals,sampler_StormTestWallNormals,uv,mip).rgb*2.0-1.0;
        voxel.normal.x*=mirror;
        voxel.normal.yz=float2(c*voxel.normal.y+s*voxel.normal.z,-s*voxel.normal.y+c*voxel.normal.z);
    }
    return voxel;
}
StormTestVoxel StormTestCloudTexture(float3 uv,float mip,bool sampleNormal)
{
    float coordinate=uv.x*64.0/48.0;
    float id=floor(coordinate),local=frac(coordinate);
    StormTestVoxel voxel=StormTestCloudTile(uv,mip,id,sampleNormal);
    float blend=.5*(1.0-smoothstep(0.0,8.0/48.0,min(local,1.0-local)));
    if(blend>0)
    {
        StormTestVoxel adjacent=StormTestCloudTile(uv,mip,id+(local<.5?-1.0:1.0),sampleNormal);
        voxel=StormTestCloudBlend(voxel,adjacent,blend);
    }
    return voxel;
}
StormTestVoxel StormTestCloudTexture(float3 uv,float mip)
{
    return StormTestCloudTexture(uv,mip,true);
}
float4 StormTestCloudField(float3 p,float mip,float pixelWidth,bool sampleNormal,out float3 normal)
{
    normal=0;
    float3 local=p-_StormCenterWater.xyz;
    float radius=length(local.xz);
    float sd=radius-_StormBand.x;
    if(sd<=-2.0 || sd>=18.0 || p.y<=max(_PirateStormTestClouds.y,_PirateStormTestCloudBase) || p.y>=_PirateStormTestClouds.z)
        return float4(0,0,1,0);
    float theta=atan2(local.z,local.x);
    float tangent=theta*max(1.0,_PirateStormTestClouds.w)/6.283185307;
    float seam=.5*smoothstep(_PirateStormTestClouds.w*.5-.125,_PirateStormTestClouds.w*.5,abs(tangent));
    float lower=_PirateStormTestCloudBase+10.0;
    float upper=_PirateStormTestClouds.z-10.0;
    float lowerY=lower+StormBoundaryNoise(float2(tangent*64.0/27.0,51.37))*2.8;
    float upperY=upper-StormBoundaryNoise(float2(tangent*64.0/31.0,90.83))*2.8;
    float variation=StormBoundaryNoise(float2(tangent*64.0/19.0,71.19));
    StormTestVoxel combined;
    combined.value=0;combined.normal=0;
    [unroll] for(int layer=0;layer<4;layer++)
    {
        float centerY=layer==3?_PirateStormTestCloudBase+4.5+(variation-.5)*.3:
            layer==0?lowerY:layer==2?upperY:lerp(lowerY,upperY,.5)+(variation-.5)*.5;
        float2 q=float2(p.y-centerY,sd-8.0);
        if(dot(q,q)>=100.0)continue;
        float s,c;
        sincos(_StormShape.z+layer*2.39996323,s,c);
        float2 rotated=float2(c*q.x-s*q.y,s*q.x+c*q.y);
        float3 uv=float3(tangent,(rotated.x+32.0)/64.0,(rotated.y+10.0)/20.0);
        StormTestVoxel voxel=StormTestCloudTexture(uv,mip,sampleNormal);
        if(seam>0)
        {
            uv.x-=sign(tangent)*_PirateStormTestClouds.w;
            voxel=StormTestCloudBlend(voxel,StormTestCloudTexture(uv,mip,sampleNormal),seam);
        }
        voxel.normal.yz=float2(c*voxel.normal.y+s*voxel.normal.z,-s*voxel.normal.y+c*voxel.normal.z);
        float blend=smoothstep(-.15,.15,voxel.value.r-combined.value.r);
        float density=max(combined.value.r,voxel.value.r);
        combined=StormTestCloudBlend(combined,voxel,blend);
        combined.value.r=density;
    }
    float footWidth=max(.25,pixelWidth);
    if(p.y<_PirateStormTestCloudBase+2.4+footWidth)
    {
        float s,c;
        sincos(_StormShape.z+3.0*2.39996323,s,c);
        float2 reference=float2(-6.0,-2.0);
        reference=float2(c*reference.x-s*reference.y,s*reference.x+c*reference.y);
        float3 footUV=float3(tangent,(reference.x+32.0)/64.0,(reference.y+10.0)/20.0);
        StormTestVoxel footVoxel=StormTestCloudTexture(footUV,max(2.0,mip),false);
        if(seam>0)
        {
            footUV.x-=sign(tangent)*_PirateStormTestClouds.w;
            footVoxel=StormTestCloudBlend(footVoxel,StormTestCloudTexture(footUV,max(2.0,mip),false),seam);
        }
        float recession=1.0-saturate(footVoxel.value.r);
        float foot=_PirateStormTestCloudBase+2.4*recession*recession*recession;
        combined.value.r*=smoothstep(foot,foot+footWidth,p.y);
        combined.normal=lerp(float3(0,-1,0),combined.normal,saturate((p.y-foot)/max(.8,footWidth*2.0)));
    }
    normal=combined.normal;
    return combined.value;
}
float4 StormTestCloudField(float3 p,float mip,float pixelWidth,out float3 normal)
{
    return StormTestCloudField(p,mip,pixelWidth,true,normal);
}
float4 StormTestCloudField(float3 p,float mip,out float3 normal)
{
    return StormTestCloudField(p,mip,0,normal);
}
float4 StormTestCloudField(float3 p,float mip)
{
    float3 normal;
    return StormTestCloudField(p,mip,normal);
}
#endif
