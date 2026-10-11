using UnityEngine;

namespace PirateSlop.World
{
    [ExecuteAlways]
    public sealed class SandyIslandFoliageWind : MonoBehaviour
    {
        public float Height = 7f;
        public float Strength = .22f;
        public float Phase;

        void OnEnable() => Apply();
        void Start() => Apply();

        public void Apply()
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                block.SetVector("_WindRoot", transform.position);
                block.SetFloat("_WindHeight", Mathf.Max(.3f, Height));
                block.SetFloat("_WindStrength", Strength);
                block.SetFloat("_WindPhase", Phase);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
