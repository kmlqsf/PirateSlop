using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class RiggingTornCloth : MonoBehaviour
    {
        public float Strength = .4f;
        public Vector3 PinAxis = Vector3.up;
        public Vector3 MotionAxis = Vector3.forward;
        public float PinnedBand;
        Mesh mesh;
        Vector3[] rest, work;
        float top, height, nextUpdate;
        Renderer visual;
        int normalFrame;
        NetworkRiggingEvent owner;
        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            mesh = Instantiate(filter.sharedMesh); filter.sharedMesh = mesh; mesh.MarkDynamic();
            rest = mesh.vertices; work = new Vector3[rest.Length];
            PinAxis = PinAxis.normalized;
            MotionAxis = MotionAxis.normalized;
            float bottom = float.MaxValue; top = float.MinValue;
            foreach (var vertex in rest) { float axis = Vector3.Dot(vertex, PinAxis); bottom = Mathf.Min(bottom, axis); top = Mathf.Max(top, axis); }
            height = Mathf.Max(.1f, top - bottom);
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * Strength * 4);
            visual = GetComponent<Renderer>(); owner = GetComponentInParent<NetworkRiggingEvent>();
        }
        void LateUpdate()
        {
            if (mesh == null || owner == null || !owner.IsSpawned || visual == null || !visual.isVisible || Time.time < nextUpdate) return;
            nextUpdate = Time.time + 1f / 30;
            float t = owner.VisualClock;
            for (int i = 0; i < rest.Length; i++)
            {
                var v = rest[i]; float weight = Mathf.Clamp01((top - Vector3.Dot(v, PinAxis) - PinnedBand) / Mathf.Max(.1f,height-PinnedBand));
                float flutter = Mathf.Sin(t * 2.2f + v.x * 1.4f + v.y * .6f) + .35f * Mathf.Sin(t * 5.3f + v.x * 3.4f);
                work[i] = v + (MotionAxis * flutter + Vector3.up * (-Mathf.Abs(flutter) * .12f)) * (weight * Strength);
            }
            mesh.vertices = work;
            if((++normalFrame&1)==0)mesh.RecalculateNormals();
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
