using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(60)]
    [RequireComponent(typeof(MeshCollider))]
    public sealed class ShipV3CollisionBatch : MonoBehaviour
    {
        public MeshCollider[] Sources = Array.Empty<MeshCollider>();
        public Bounds[] SourceBounds = Array.Empty<Bounds>();
        public Mesh CachedMesh;
        ShipDamageSection[] sections;
        MeshCollider output;
        Mesh runtimeMesh;
        bool dirty;

        void OnEnable()
        {
            output = GetComponent<MeshCollider>();
            output.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
            sections = new ShipDamageSection[Sources.Length];
            var owner = GetComponentInParent<ShipDestruction>();
            for (int i = 0; i < Sources.Length; i++)
            {
                if (Sources[i] == null) continue;
                Sources[i].enabled = false;
                sections[i] = owner != null ? owner.SectionFor(Sources[i]) : Sources[i].GetComponentInParent<ShipDamageSection>();
                if (sections[i] == null) continue;
                sections[i].CollisionBatch = this;
                sections[i].VisualChanged += Invalidate;
            }
            dirty = true;
        }

        void Invalidate(ShipDamageSection section)
        {
            for (int i = 0; i < Sources.Length; i++)
                if (sections[i] == section && Sources[i] != null) Sources[i].enabled = false;
            dirty = true;
        }

        bool Visible(int index) => Sources[index] != null && Sources[index].gameObject.activeInHierarchy;

        void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            bool all = true;
            for (int i = 0; i < Sources.Length; i++) all &= Visible(i);
            Mesh next = all ? CachedMesh : BuildMesh(Sources, transform, false);
            output.enabled = next != null && next.vertexCount != 0;
            output.sharedMesh = output.enabled ? next : null;
            if (runtimeMesh != null) Destroy(runtimeMesh);
            runtimeMesh = all ? null : next;
        }

        public ShipDamageSection Resolve(Vector3 point)
        {
            if (sections == null) return null;
            Vector3 local = transform.InverseTransformPoint(point);
            float best = float.MaxValue;
            ShipDamageSection nearest = null;
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i] == null || !Visible(i)) continue;
                var bounds = SourceBounds[i];
                float score = (bounds.ClosestPoint(local) - local).sqrMagnitude + (bounds.center - local).sqrMagnitude * .00001f;
                if (score >= best) continue;
                best = score;
                nearest = sections[i];
            }
            return nearest;
        }

        public float Distance(ShipDamageSection section, Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            float distance = float.MaxValue;
            for (int i = 0; i < sections.Length; i++)
                if (sections[i] == section && Visible(i))
                    distance = Mathf.Min(distance, Vector3.Distance(transform.TransformPoint(SourceBounds[i].ClosestPoint(local)), point));
            return distance;
        }

        public static ShipDamageSection ResolveSection(Collider collider, Vector3 point)
        {
            if (collider == null) return null;
            var batch = collider.GetComponent<ShipV3CollisionBatch>();
            if (batch != null) return batch.Resolve(point);
            var owner = collider.GetComponentInParent<ShipDestruction>();
            return owner != null ? owner.SectionFor(collider) : collider.GetComponentInParent<ShipDamageSection>();
        }

        public bool ContainsSurface(Transform parent, Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            for (int i = 0; i < Sources.Length; i++)
                if (Sources[i] != null && Visible(i) && Sources[i].transform.IsChildOf(parent) &&
                    (SourceBounds[i].ClosestPoint(local) - local).sqrMagnitude < .0064f) return true;
            return false;
        }

        public static Mesh BuildMesh(IReadOnlyList<MeshCollider> sources, Transform anchor, bool includeInactive)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || source.sharedMesh == null || !includeInactive && !source.gameObject.activeInHierarchy) continue;
                var mesh = source.sharedMesh;
                if (!mesh.isReadable) throw new InvalidOperationException("Collision mesh is not readable: " + mesh.name);
                int offset = vertices.Count;
                var matrix = anchor.worldToLocalMatrix * source.transform.localToWorldMatrix;
                foreach (var vertex in mesh.vertices) vertices.Add(matrix.MultiplyPoint3x4(vertex));
                foreach (int index in mesh.triangles) indices.Add(offset + index);
            }
            var result = new Mesh { name = "ShipV3CollisionBatch", indexFormat = IndexFormat.UInt32 };
            result.SetVertices(vertices);
            result.SetTriangles(indices, 0);
            result.RecalculateBounds();
            return result;
        }

        void OnDisable()
        {
            if (output != null) output.enabled = false;
            if (sections != null)
                for (int i = 0; i < sections.Length; i++)
                    if (sections[i] != null)
                    {
                        sections[i].VisualChanged -= Invalidate;
                        if (sections[i].CollisionBatch == this) sections[i].CollisionBatch = null;
                        if (Sources[i] != null) Sources[i].enabled = true;
                    }
            if (runtimeMesh != null) Destroy(runtimeMesh);
            runtimeMesh = null;
        }
    }
}
