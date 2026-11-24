using UnityEngine;

namespace PirateSlop
{
    public sealed class ShipSpyglass : MonoBehaviour
    {
        public Transform Viewpoint;
        public float ViewpointLift = 1.25f;
        public Vector3 EyePosition => Viewpoint.position + Viewpoint.up * Mathf.Max(1.25f, ViewpointLift);
        public Transform VisualRoot
        {
            get
            {
                for (var node = transform; node != null; node = node.parent)
                    if (node.name == "V9_Telescope_Control") return node;
                return transform;
            }
        }
    }
}
