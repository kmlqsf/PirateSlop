using UnityEngine;

public class SailSystem : MonoBehaviour
{
    [Header("Deploy Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float deployPercentage = 0f;
    [SerializeField] private float deploySpeed = 0.35f;

    [Header("Optional Visuals")]
    [SerializeField] private Transform[] sailMeshes;

    public float DeployPercentage => deployPercentage;
    readonly System.Collections.Generic.Dictionary<string, float> structuralEfficiency = new();
    public float EffectiveDeploy
    {
        get
        {
            if (sailMeshes == null || sailMeshes.Length == 0) return deployPercentage;
            float total = 0f;
            foreach (var sail in sailMeshes) if (sail != null) total += structuralEfficiency.TryGetValue(sail.name, out float efficiency) ? efficiency : 1f;
            return deployPercentage * total / sailMeshes.Length;
        }
    }
    public void SetStructuralEfficiency(System.Collections.Generic.Dictionary<string, float> values)
    {
        structuralEfficiency.Clear();
        foreach (var entry in values) structuralEfficiency[entry.Key] = entry.Value;
        UpdateVisuals();
    }
    public Collider[] MastControls;
    public bool InRange(AdvancedPlayerController player)
    {
        if (player == null || player.IsDead || MastControls == null) return false;
        Vector3 point = player.transform.position + Vector3.up;
        foreach (var mast in MastControls)
            if (mast != null && mast.enabled && Vector3.Distance(point, mast.ClosestPoint(point)) <= 2.5f) return true;
        return false;
    }

    public void AdjustSail(float delta)
    {
        deployPercentage = Mathf.Clamp01(deployPercentage + delta);
        UpdateVisuals();
    }

    public void SetDeploy(float value) { deployPercentage = Mathf.Clamp01(value); UpdateVisuals(); }
    private void UpdateVisuals()
    {
        if (sailMeshes == null) return;
        foreach (var sail in sailMeshes)
        {
            if (sail != null)
            {
                float efficiency = structuralEfficiency.TryGetValue(sail.name, out float value) ? value : 1f;
                var renderer = sail.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = efficiency > 0f;
                sail.localScale = new Vector3(sail.localScale.x, Mathf.Lerp(0.1f, 1f, deployPercentage), sail.localScale.z);
            }
        }
    }
}

