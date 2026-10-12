using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    [DefaultExecutionOrder(50)]
    public sealed class RiggingLooseRope : MonoBehaviour
    {
        public float Strength = .3f;
        LineRenderer line;
        NetworkRiggingEvent owner;
        Vector3[] rest;
        void Awake()
        {
            line = GetComponent<LineRenderer>(); owner = GetComponentInParent<NetworkRiggingEvent>();
            if (line == null) return;
            rest = new Vector3[line.positionCount]; line.GetPositions(rest);
        }
        void LateUpdate()
        {
            if (line == null || rest == null || owner == null || !owner.IsSpawned) return;
            float t = owner.VisualClock;
            for (int i = 0; i < rest.Length; i++)
            {
                float weight = i / (float)Mathf.Max(1, rest.Length - 1);
                var sway = new Vector3(Mathf.Sin(t * 1.4f + i * .2f), Mathf.Sin(t * 2.1f + i * .13f) * .15f, Mathf.Sin(t * 1.1f + i * .3f));
                line.SetPosition(i, rest[i] + sway * (Strength * weight * weight));
            }
        }
    }
}
