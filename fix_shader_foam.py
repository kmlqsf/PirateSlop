import re

with open(r'd:\projects\Pirate_BR\PirateGame\Assets\Houidisoft technology\Simple water\Shaders\SimpleWaterURP.shader', 'r', encoding='utf-8') as f:
    text = f.read()

# Replace centerFoam logic
old_foam = r'float centerFoam = smoothstep\(0\.3, 0\.8, vortexFoamNoise\) \* vortexMask \* 2\.0;'
new_foam = """float foamFadeAtCenter = smoothstep(0.0, 0.2, w_t);
                    float centerFoam = smoothstep(0.4, 0.8, vortexFoamNoise) * vortexMask * foamFadeAtCenter * 1.5;"""

text = re.sub(old_foam, new_foam, text)

# Just to be completely sure abyssColor works and is applied properly
# Find the abyssColor logic
old_abyss = r'float3 abyssColor = float3\(0\.01, 0\.03, 0\.05\);\s*waterColor = lerp\(waterColor, abyssColor, vortexMask \* 0\.9\);'
new_abyss = """float3 abyssColor = float3(0.01, 0.03, 0.05);
                  waterColor = lerp(waterColor, abyssColor, vortexMask);"""

text = re.sub(old_abyss, new_abyss, text)

with open(r'd:\projects\Pirate_BR\PirateGame\Assets\Houidisoft technology\Simple water\Shaders\SimpleWaterURP.shader', 'w', encoding='utf-8') as f:
    f.write(text)
