using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public sealed class FishingRodBend : MonoBehaviour
    {
        sealed class Part
        {
            public Mesh Mesh;
            public Vector3[] Rest, Work, Offset;
        }
        readonly List<Part> parts = new();
        float bend;
        public Vector3 Tip => transform.TransformPoint(new Vector3(0f, .15f - bend, 1.7f));
        void Awake()
        {
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var rest = filter.sharedMesh.vertices;
                var offsets = new Vector3[rest.Length];
                bool affected = false;
                Vector3 down = filter.transform.InverseTransformVector(transform.TransformVector(Vector3.down));
                for (int i = 0; i < rest.Length; i++)
                {
                    float z = transform.InverseTransformPoint(filter.transform.TransformPoint(rest[i])).z;
                    float weight = Mathf.InverseLerp(.75f, 1.7f, z);
                    offsets[i] = down * weight * weight;
                    affected |= weight > 0f;
                }
                if (!affected) continue;
                var mesh = Instantiate(filter.sharedMesh); mesh.MarkDynamic(); filter.sharedMesh = mesh;
                parts.Add(new Part { Mesh = mesh, Rest = rest, Work = new Vector3[rest.Length], Offset = offsets });
            }
        }
        public void SetBend(float value)
        {
            if (parts.Count == 0) return;
            value = Mathf.Clamp(value, 0f, .22f);
            if (Mathf.Abs(value - bend) < .001f) return;
            bend = value;
            foreach (var part in parts)
            {
                for (int i = 0; i < part.Rest.Length; i++) part.Work[i] = part.Rest[i] + part.Offset[i] * bend;
                part.Mesh.vertices = part.Work;
                part.Mesh.RecalculateNormals(); part.Mesh.RecalculateBounds();
            }
        }
        void OnDestroy() { foreach (var part in parts) if (part.Mesh != null) Destroy(part.Mesh); }
    }
}
