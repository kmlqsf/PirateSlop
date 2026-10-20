using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using Unity.Jobs;
#endif

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
        readonly HashSet<ShipDamageSection> trackedSections = new();
        readonly Dictionary<ShipDamageSection, List<int>> sectionSources = new();
        bool dirty;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
        bool upper;
        Mesh upperMesh;
        Mesh spareMesh;
        JobHandle bake;
        bool baking;
        static int activeBakes;
        struct BakeCollision : IJob
        {
            public EntityId MeshId;
            public MeshColliderCookingOptions Options;
            public void Execute() => Physics.BakeMesh(MeshId, false, Options);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBakes() => activeBakes = 0;
        readonly List<Vector3> upperVertices = new();
        readonly List<int> upperIndices = new();
        readonly List<Vector3> sourceVertices = new();
        readonly List<int> sourceIndices = new();
        public bool CombinesFragments => upper;
#endif

        void OnEnable()
        {
            output = GetComponent<MeshCollider>();
            output.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
            sections = new ShipDamageSection[Sources.Length];
            var owner = GetComponentInParent<ShipDestruction>();
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            upper = owner != null && owner.Upper != null;
#endif
            for (int i = 0; i < Sources.Length; i++)
            {
                if (Sources[i] == null) continue;
                Sources[i].enabled = false;
                sections[i] = owner != null ? owner.SectionFor(Sources[i]) : Sources[i].GetComponentInParent<ShipDamageSection>();
                if (sections[i] == null) continue;
                sections[i].CollisionBatch = this;
                if (!sectionSources.TryGetValue(sections[i], out var members)) sectionSources[sections[i]] = members = new List<int>();
                members.Add(i);
                if (trackedSections.Add(sections[i])) sections[i].VisualChanged += Invalidate;
            }
            dirty = true;
        }

        void Invalidate(ShipDamageSection section)
        {
            dirty = true;
        }

        static bool HasGeometry(MeshCollider source)
        {
            if (source == null || source.sharedMesh == null || source.sharedMesh.vertexCount == 0) return false;
            var filter = source.GetComponent<MeshFilter>();
            return filter == null || filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0;
        }

        bool Visible(int index) => HasGeometry(Sources[index]) && Sources[index].gameObject.activeInHierarchy;

        static readonly Unity.Profiling.ProfilerMarker performanceCollision = new("Performance.CollisionBatch");
        void LateUpdate()
        {
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (baking)
            {
                if (!bake.IsCompleted) return;
                CompleteBake(true);
            }
            if (upper && activeBakes >= 2) return;
#endif
            if (!dirty) return;
            using var performanceSample = performanceCollision.Auto();
            using var cpuTiming = PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipCollisionBatch);
            dirty = false;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (upper) { RebuildUpper(); return; }
#endif
            bool all = true;
            for (int i = 0; i < Sources.Length; i++) all &= Visible(i);
            bool batched = all && CachedMesh != null && CachedMesh.vertexCount > 0;
            output.enabled = batched;
            output.sharedMesh = batched ? CachedMesh : null;
            for (int i = 0; i < Sources.Length; i++) ShipDamageSection.SetColliderEnabled(Sources[i], !batched && Visible(i));
        }

        public ShipDamageSection Resolve(Vector3 point)
        {
            if (sections == null) return null;
            Vector3 local = transform.InverseTransformPoint(point);
            float best = float.MaxValue;
            ShipDamageSection nearest = null;
            for (int i = 0; i < sections.Length; i++)
            {
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                if (upper)
                {
                    var part = sections[i];
                    if (part == null) continue;
                    float distance = UpperDistance(part, point);
                    if (distance < best) { best = distance; nearest = part; }
                    continue;
                }
#endif
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
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (upper) return UpperDistance(section, point);
#endif
            if (!sectionSources.TryGetValue(section, out var members)) return float.MaxValue;
            Vector3 local = transform.InverseTransformPoint(point);
            float distance = float.MaxValue;
            foreach (int i in members)
                if (Visible(i))
                    distance = Mathf.Min(distance, Vector3.Distance(transform.TransformPoint(SourceBounds[i].ClosestPoint(local)), point));
            return distance;
        }

#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
        static float UpperDistance(ShipDamageSection part, Vector3 point)
        {
            float nearest = float.MaxValue;
            void Measure(GameObject source)
            {
                if (source == null || !source.activeInHierarchy) return;
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) return;
                var closest = filter.transform.TransformPoint(filter.sharedMesh.bounds.ClosestPoint(filter.transform.InverseTransformPoint(point)));
                nearest = Mathf.Min(nearest, Vector3.Distance(point, closest));
            }
            if (part.RemovedFragments == 0 || part.Fragments.Length == 0) Measure(part.Intact);
            else foreach (var fragment in part.Fragments) Measure(fragment);
            return nearest;
        }

        void RebuildUpper()
        {
            upperVertices.Clear(); upperIndices.Clear();
            bool intact = true;
            foreach (var section in trackedSections) intact &= section.RemovedFragments == 0 && section.Intact != null && section.Intact.activeInHierarchy;
            foreach (var source in Sources) if (source != null) source.enabled = false;
            foreach (var section in trackedSections)
            {
                if (intact) continue;
                void Append(GameObject source)
                {
                    if (source == null || !source.activeInHierarchy) return;
                    var filter = source.GetComponent<MeshFilter>();
                    var mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null || mesh.vertexCount == 0) return;
                    int offset = upperVertices.Count;
                    mesh.GetVertices(sourceVertices);
                    var matrix = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    foreach (var vertex in sourceVertices) upperVertices.Add(matrix.MultiplyPoint3x4(vertex));
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        mesh.GetTriangles(sourceIndices, sub);
                        foreach (int index in sourceIndices) upperIndices.Add(offset + index);
                    }
                }
                if (section.RemovedFragments == 0 || section.Fragments.Length == 0) Append(section.Intact);
                else foreach (var fragment in section.Fragments) Append(fragment);
                foreach (var fragment in section.Fragments)
                    foreach (var collider in fragment.GetComponents<Collider>()) collider.enabled = false;
            }
            if (intact) output.sharedMesh = CachedMesh;
            else if (upperIndices.Count > 0)
            {
                if (spareMesh == null) spareMesh = new Mesh { name = "UpperCollisionBatch", indexFormat = IndexFormat.UInt32 };
                spareMesh.Clear(); spareMesh.SetVertices(upperVertices); spareMesh.SetTriangles(upperIndices, 0); spareMesh.RecalculateBounds();
                bake = new BakeCollision { MeshId = spareMesh.GetEntityId(), Options = output.cookingOptions }.Schedule();
                baking = true; activeBakes++;
                JobHandle.ScheduleBatchedJobs();
            }
            else output.sharedMesh = null;
            output.enabled = output.sharedMesh != null;
        }
        void CompleteBake(bool apply)
        {
            if (!baking) return;
            bake.Complete(); baking = false; activeBakes = Mathf.Max(0, activeBakes - 1);
            if (!apply) return;
            output.sharedMesh = spareMesh; output.enabled = true;
            (upperMesh, spareMesh) = (spareMesh, upperMesh);
        }
        void OnDestroy()
        {
            CompleteBake(false);
            if (upperMesh != null) Destroy(upperMesh);
            if (spareMesh != null) Destroy(spareMesh);
        }
#endif

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
                if (!HasGeometry(source) || !includeInactive && !source.gameObject.activeInHierarchy) continue;
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
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            CompleteBake(false);
#endif
            if (output != null) output.enabled = false;
            if (sections != null)
                for (int i = 0; i < sections.Length; i++)
                    if (sections[i] != null)
                    {
                        if (sections[i].CollisionBatch == this) sections[i].CollisionBatch = null;
                        ShipDamageSection.SetColliderEnabled(Sources[i], true);
                    }
            foreach (var section in trackedSections) if (section != null) section.VisualChanged -= Invalidate;
            trackedSections.Clear();
            sectionSources.Clear();
        }
    }
}
