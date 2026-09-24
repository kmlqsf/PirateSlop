using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(50)]
    public sealed class SailRopeMesh : MonoBehaviour
    {
        public SailRopeVisual Rope;
        public float Radius = .023f;
        MeshRenderer meshRenderer;

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = false;
            var mf = GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = null;
        }

        void LateUpdate()
        {
            if (Rope == null || Rope.Line == null || Rope.Sails == null) return;
            Rope.Line.enabled = Rope.Sails.Efficiency(Rope.Index) > 0f;
        }
    }
}
