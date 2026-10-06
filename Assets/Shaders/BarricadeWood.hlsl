#ifndef PIRATESLOP_BARRICADE_WOOD
#define PIRATESLOP_BARRICADE_WOOD
half4 BarricadeWoodFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surface;
    InitializeStandardLitSurfaceData(input.uv, surface);
    InputData data;
    InitializeInputData(input, surface.normalTS, data);
    InitializeBakedGIData(input, data);
    half4 color = UniversalFragmentPBR(data, surface);
    color.rgb = MixFog(color.rgb, data.fogCoord);
    color.a = surface.alpha;
    return color;
}
#endif
