using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-10000)]
public sealed class BakeoffSession : MonoBehaviour
{
    public enum Candidate { A, B, C }
    public Candidate candidate;
    public UniversalRenderPipelineAsset pipeline;
    public UniversalRendererData rendererData;
    public GameObject cloudA;
    public GameObject cloudB;
    public GameObject fogC;
    RenderPipelineAsset previous;
    void OnEnable()
    {
        previous = QualitySettings.renderPipeline;
        foreach (var feature in rendererData.rendererFeatures)
            feature.SetActive(feature.name == "Candidate" + candidate);
        rendererData.SetDirty();
        QualitySettings.renderPipeline = pipeline;
        cloudA.SetActive(candidate == Candidate.A);
        cloudB.SetActive(candidate == Candidate.B);
        fogC.SetActive(candidate == Candidate.C);
    }
    void OnDisable() { QualitySettings.renderPipeline = previous; }
}
