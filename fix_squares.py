import re

with open(r'd:\projects\Pirate_BR\PirateGame\Assets\Scripts\World\WhirlpoolVFX.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# Fix the squares
old_alpha_code = r'softMat\.EnableKeyword\("_ALPHABLEND_ON"\);\s*softMat\.renderQueue = 3000;'
new_alpha_code = """softMat.EnableKeyword("_ALPHABLEND_ON");
            softMat.renderQueue = 3000;
            softMat.SetFloat("_Surface", 1.0f); // 1 = Transparent in URP
            softMat.SetFloat("_Blend", 0.0f); // 0 = Alpha in URP
            softMat.SetOverrideTag("RenderType", "Transparent");"""

text = re.sub(old_alpha_code, new_alpha_code, text)

with open(r'd:\projects\Pirate_BR\PirateGame\Assets\Scripts\World\WhirlpoolVFX.cs', 'w', encoding='utf-8') as f:
    f.write(text)
