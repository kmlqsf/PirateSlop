using UnityEngine;

namespace PirateSlop.World
{
    public class WhirlpoolVFX : MonoBehaviour
    {
        ParticleSystem mist, churn;
        
        void Start()
        {
            var mistObj = new GameObject("MistParticles");
            mistObj.transform.SetParent(transform, false);
            mist = mistObj.AddComponent<ParticleSystem>();
            var mistMain = mist.main;
            mistMain.duration = 5f;
            mistMain.startLifetime = 3f;
            mistMain.startSpeed = 25f;
            mistMain.startSize = 80f;
            mistMain.startColor = new Color(1f, 1f, 1f, 0.1f);
            mistMain.simulationSpace = ParticleSystemSimulationSpace.World;
            mistMain.maxParticles = 500;
            var mistEmission = mist.emission;
            mistEmission.rateOverTime = 0;
            var mistShape = mist.shape;
            mistShape.shapeType = ParticleSystemShapeType.ConeVolume;
            mistShape.angle = 75f;
            mistShape.radius = 250f;
            mistShape.position = new Vector3(0, 20f, 0);

            var mistVel = mist.velocityOverLifetime;
            mistVel.enabled = true;
            mistVel.space = ParticleSystemSimulationSpace.Local;
            mistVel.orbitalZ = 1.5f;
            mistVel.radial = -25f;
            mistVel.y = -10f;

            mistMain.startSpeed = 0f;
            mistMain.startSize = new ParticleSystem.MinMaxCurve(20f, 40f);
            mistMain.startColor = new Color(0.8f, 0.9f, 1f, 0.01f);


            var mistRend = mist.GetComponent<ParticleSystemRenderer>();
            var baseMat = Resources.Load<Material>("CombatParticles");
            var softMat = new Material(baseMat);
            var tex = new Texture2D(32, 32, TextureFormat.ARGB32, false);
            for (int y = 0; y < 32; y++) {
                for (int x = 0; x < 32; x++) {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - (d / 15.5f)), 2f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            softMat.mainTexture = tex;
            if (softMat.HasProperty("_BaseMap")) softMat.SetTexture("_BaseMap", tex);
            softMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            softMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            softMat.SetInt("_ZWrite", 0);
            if (softMat.HasProperty("_FlipbookBlending")) softMat.SetFloat("_FlipbookBlending", 0f);
            softMat.DisableKeyword("_FLIPBOOKBLENDING_ON");
            softMat.EnableKeyword("_ALPHABLEND_ON");
            softMat.renderQueue = 3000;
            softMat.SetFloat("_Surface", 1.0f); // 1 = Transparent in URP
            softMat.SetFloat("_Blend", 0.0f); // 0 = Alpha in URP
            softMat.SetOverrideTag("RenderType", "Transparent");
            mistRend.material = softMat;
            
            var churnObj = new GameObject("ChurnParticles");
            churnObj.transform.SetParent(transform, false);
            churnObj.transform.localPosition = new Vector3(0, -120f, 0);
            churn = churnObj.AddComponent<ParticleSystem>();
            var churnMain = churn.main;
            churnMain.startLifetime = 1f;
            churnMain.startSpeed = 15f;
            churnMain.startSize = 40f;
            var churnEmission = churn.emission;
            churnEmission.rateOverTime = 0;
            var churnShape = churn.shape;
            churnShape.shapeType = ParticleSystemShapeType.Sphere;
            churnShape.radius = 15f;
            var churnRend = churn.GetComponent<ParticleSystemRenderer>();
            churnRend.material = softMat;
        }

        void Update()
        {
            float intensity = OceanSurface.Instance != null ? OceanSurface.Instance.WhirlpoolDepth / 120f : 0f;
            var mistEmission = mist.emission;
            mistEmission.rateOverTime = intensity * 5f;
            if (churn != null && OceanSurface.Instance != null) churn.transform.localPosition = new Vector3(0, -OceanSurface.Instance.WhirlpoolDepth, 0);
            var churnEmission = churn.emission;
            churnEmission.rateOverTime = intensity * 100f;
        }
    }
}






