using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(10)]
    public sealed class AnchorCableVisual : MonoBehaviour
    {
        [SerializeField] Vector3 localHawseHole = new Vector3(0f, 3.2f, 22.8f);
        NetworkShip ship;
        LineRenderer line;

        void Awake()
        {
            ship = GetComponentInParent<NetworkShip>();
            line = GetComponent<LineRenderer>();
            if (line == null) line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.startWidth = 0.14f;
            line.endWidth = 0.14f;
            line.positionCount = 8;
        }

        void LateUpdate()
        {
            if (ship == null || line == null) return;
            bool dropped = ship.AnchorDropped;
            line.enabled = dropped;
            if (!dropped) return;

            Vector3 start = ship.transform.TransformPoint(localHawseHole);
            Vector3 target = ship.AnchorSeabedPoint;
            if (target.sqrMagnitude < 1f) target = start + Vector3.down * 15f + ship.transform.forward * 4f;

            var ocean = OceanSurface.Instance;
            float waterY = ocean != null ? ocean.Height(target) : 0f;
            if (target.y > waterY - 8f) target.y = waterY - 12f;

            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                Vector3 p = Vector3.Lerp(start, target, t);
                float sag = Mathf.Sin(t * Mathf.PI) * 1.2f;
                p.y -= sag;
                line.SetPosition(i, p);
            }
        }
    }
}
