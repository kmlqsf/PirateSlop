using UnityEngine;

[ExecuteAlways]
public sealed class BakeoffSphereField : MonoBehaviour
{
    public bool stormScale = true;
    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }
    void Apply()
    {
        var target = GetComponent<Renderer>();
        if (target == null) return;
        var properties = new MaterialPropertyBlock();
        var spheres = new Vector4[20];
        int count = stormScale ? 8 : 3;
        for (int i = 0; i < count; i++)
            spheres[i] = stormScale ? new Vector4(-310 + i * 88, 90, 100, 90) : new Vector4((i - 1) * 2, 3, 0, 2.8f);
        properties.SetVectorArray("_Spheres", spheres);
        properties.SetFloat("_SphereCount", count);
        properties.SetFloat("_DisplacementAmpl", 1);
        target.SetPropertyBlock(properties);
    }
    void OnDisable() { GetComponent<Renderer>().SetPropertyBlock(null); }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
