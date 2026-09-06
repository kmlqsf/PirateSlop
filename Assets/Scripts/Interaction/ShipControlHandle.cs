using UnityEngine;

namespace PirateSlop
{
    public sealed class ShipControlHandle : MonoBehaviour
    {
        public HelmInteraction Helm;
        public SimpleCannon Cannon;
        Renderer targetRenderer;
        MaterialPropertyBlock[] saved;
        bool highlighted;

        public void Highlight(bool value)
        {
            if (Helm == null || highlighted == value) return;
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null) return;
            var materials = targetRenderer.sharedMaterials;
            if (value)
            {
                saved = new MaterialPropertyBlock[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    saved[i] = new MaterialPropertyBlock(); targetRenderer.GetPropertyBlock(saved[i], i);
                    var block = new MaterialPropertyBlock(); targetRenderer.GetPropertyBlock(block, i);
                    Color color = materials[i] != null && materials[i].HasProperty("_BaseColor") ? materials[i].GetColor("_BaseColor") : Color.gray;
                    block.SetColor("_BaseColor", Color.Lerp(color, new Color(1f, .85f, .4f), .25f));
                    targetRenderer.SetPropertyBlock(block, i);
                }
            }
            else if (saved != null)
                for (int i = 0; i < saved.Length; i++) targetRenderer.SetPropertyBlock(saved[i], i);
            highlighted = value;
        }
        void OnDisable() { Highlight(false); }
    }
}
