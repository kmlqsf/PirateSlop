using UnityEngine;

namespace PirateSlop
{
    public sealed class SailRopeVisual : MonoBehaviour
    {
        public SailSystem Sails;
        public int Index;
        public Transform Anchor, Handle;
        public LineRenderer Line;
        public Vector3 Guide, HandleTop;
        float tension;
        void LateUpdate()
        {
            if (Sails == null || Anchor == null || Handle == null || Line == null) return;
            bool available = Sails.Efficiency(Index) > 0f;
            Line.enabled = available;
            var collider = Handle.GetComponent<Collider>();
            if (collider != null) collider.enabled = available;
            tension = Mathf.MoveTowards(tension, Sails.Tension(Index), Time.deltaTime * 3f);
            Handle.localPosition = HandleTop + Vector3.down * (.85f * tension);
            Vector3 start = transform.InverseTransformPoint(Anchor.position);
            Vector3 end = transform.InverseTransformPoint(Handle.position);
            for (int i = 0; i < 17; i++)
            {
                float t = i / 16f;
                var p = Vector3.Lerp(start, Guide, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(1.6f, .06f, tension);
                Line.SetPosition(i, p);
            }
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                var p = Vector3.Lerp(Guide, end, t);
                p += Vector3.back * (Mathf.Sin(t * Mathf.PI) * .3f * (1f - tension));
                Line.SetPosition(16 + i, p);
            }
        }
    }
}
