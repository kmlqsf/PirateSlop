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
                sail.localScale = new Vector3(sail.localScale.x, Mathf.Lerp(0.1f, 1f, deployPercentage), sail.localScale.z);
            }
        }
    }
}

