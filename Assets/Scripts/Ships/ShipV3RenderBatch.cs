using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Ships
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DefaultExecutionOrder(100)]
    public sealed class ShipV3RenderBatch : MonoBehaviour
    {
        public MeshRenderer[] Sources = Array.Empty<MeshRenderer>();
        public Mesh CachedMesh;
        public Material SharedMaterial;
        public Transform BatchAnchor;
        public Mesh CachedShadowMesh;
        public MeshRenderer ShadowProxy;

        MeshFilter outputFilter;
        MeshRenderer outputRenderer;
        Mesh runtimeMesh;
        MeshRenderer[] trackedSources;
        bool[] visible;
        readonly HashSet<ShipDamageSection> trackedSections = new();
        bool dirty;
        float nextVisibilityCheck;
        static readonly Unity.Profiling.ProfilerMarker marker = new("Ships.RenderBatchVisibility");

        void OnEnable()
        {
            Unsubscribe();
            outputFilter = GetComponent<MeshFilter>();
            outputRenderer = GetComponent<MeshRenderer>();
            if (Application.isPlaying && CachedMesh != null && CachedMesh.isReadable) CachedMesh.UploadMeshData(true);
            if (BatchAnchor != null && BatchAnchor != transform)
            {
                transform.SetParent(BatchAnchor, false);
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                transform.localScale = Vector3.one;
            }
            Sources ??= Array.Empty<MeshRenderer>();
            trackedSources = Sources;
            visible = new bool[trackedSources.Length];
            bool allVisible = true;
            var owner = GetComponentInParent<ShipDestruction>();
            for (int i = 0; i < trackedSources.Length; i++)
            {
                var source = trackedSources[i];
                visible[i] = source != null && source.gameObject.activeInHierarchy && !source.forceRenderingOff;
                allVisible &= visible[i];
                if (source != null) source.enabled = false;
                var section = source != null ? owner != null ? owner.SectionFor(source) : source.GetComponentInParent<ShipDamageSection>() : null;
                if (section != null && trackedSections.Add(section)) section.VisualChanged += Invalidate;
            }
            outputRenderer.sharedMaterial = SharedMaterial;
            if (ShadowProxy != null) outputRenderer.shadowCastingMode = ShadowCastingMode.Off;
            if (CachedMesh != null && allVisible)
            {
                ReleaseRuntimeMesh();
                outputFilter.sharedMesh = CachedMesh;
                outputRenderer.enabled = CachedMesh.vertexCount != 0;
                SetShadow(CachedShadowMesh ?? CachedMesh);
            }
            else Rebuild();
            dirty = false;
            nextVisibilityCheck = Time.unscaledTime + .5f + (GetEntityId().GetHashCode() & 255) / 128f;
        }

        void LateUpdate()
        {
            if (!ReferenceEquals(trackedSources, Sources))
            {
                RestoreSources();
                OnEnable();
                return;
            }
            if (!dirty && Time.unscaledTime < nextVisibilityCheck) return;
            using var sample = marker.Auto();
            dirty = false;
            nextVisibilityCheck = Time.unscaledTime + 2f;
            bool changed = false;
            for (int i = 0; i < trackedSources.Length; i++)
            {
                var source = trackedSources[i];
                bool active = source != null && source.gameObject.activeInHierarchy && !source.forceRenderingOff;
                if (active != visible[i])
                {
                    visible[i] = active;
                    changed = true;
                }
                if (source != null && source.enabled) source.enabled = false;
            }
            if (changed) Rebuild();
        }

        void Rebuild()
        {
            try
            {
                bool allVisible = true;
                for (int i = 0; i < visible.Length; i++) allVisible &= visible[i];
                if (CachedMesh != null && allVisible)
                {
                    outputFilter.sharedMesh = CachedMesh;
                    outputRenderer.enabled = CachedMesh.vertexCount != 0;
                    SetShadow(CachedShadowMesh ?? CachedMesh);
                    ReleaseRuntimeMesh();
                    return;
                }
                var next = BuildMesh(trackedSources, SharedMaterial, transform, false);
                outputFilter.sharedMesh = next;
                outputRenderer.enabled = next.vertexCount != 0;
                SetShadow(next);
                ReleaseRuntimeMesh();
                runtimeMesh = next;
                if (Application.isPlaying) runtimeMesh.UploadMeshData(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        void RestoreSources()
        {
            if (trackedSources == null) return;
            for (int i = 0; i < trackedSources.Length; i++)
                if (trackedSources[i] != null) trackedSources[i].enabled = true;
        }

        void SetShadow(Mesh mesh)
        {
            if (ShadowProxy == null) return;
            ShadowProxy.GetComponent<MeshFilter>().sharedMesh = mesh;
            ShadowProxy.enabled = mesh != null && mesh.vertexCount != 0;
        }

        void ReleaseRuntimeMesh()
        {
            if (runtimeMesh == null) return;
            if (Application.isPlaying) Destroy(runtimeMesh);
            else DestroyImmediate(runtimeMesh);
            runtimeMesh = null;
        }

        void OnDisable()
        {
            Unsubscribe();
            if (outputRenderer != null) outputRenderer.enabled = false;
            if (ShadowProxy != null) ShadowProxy.enabled = false;
            if (outputFilter != null) outputFilter.sharedMesh = null;
            RestoreSources();
            ReleaseRuntimeMesh();
        }

        void OnDestroy() => OnDisable();
        void Invalidate(ShipDamageSection section) => dirty = true;
        void Unsubscribe()
        {
            foreach (var section in trackedSections) if (section != null) section.VisualChanged -= Invalidate;
            trackedSections.Clear();
        }

        public static Mesh BuildMesh(IReadOnlyList<MeshRenderer> sources, Material material, Transform anchor, bool includeInactive)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var colors = new List<Color32>();
            var indices = new List<int>();
            bool hasNormals = false, hasTangents = false, hasUv0 = false, hasUv1 = false, hasColors = false;
            Matrix4x4 toAnchor = anchor.worldToLocalMatrix;
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                var source = sources[sourceIndex];
                if (source == null || !includeInactive && (!source.gameObject.activeInHierarchy || source.forceRenderingOff)) continue;
                var filter = source.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.vertexCount == 0) continue;
                var materials = source.sharedMaterials;
                if (materials.Length == 0) continue;
                int selectedCount = 0;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                    if (materials[Mathf.Min(subMesh, materials.Length - 1)] == material && mesh.GetIndexCount(subMesh) != 0) selectedCount++;
                if (selectedCount == 0) continue;
                if (!mesh.isReadable) throw new InvalidOperationException("Ship V3 batch source mesh is not readable: " + mesh.name);
                int offset = vertices.Count;
                Matrix4x4 matrix = toAnchor * source.transform.localToWorldMatrix;
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                float handedness = matrix.determinant < 0f ? -1f : 1f;
                var sourceVertices = mesh.vertices;
                var sourceNormals = mesh.normals;
                var sourceTangents = mesh.tangents;
                var sourceUv0 = mesh.uv;
                var sourceUv1 = mesh.uv2;
                var sourceColors = mesh.colors32;
                bool normalPresent = sourceNormals.Length == sourceVertices.Length;
                bool tangentPresent = sourceTangents.Length == sourceVertices.Length;
                bool uv0Present = sourceUv0.Length == sourceVertices.Length;
                bool uv1Present = sourceUv1.Length == sourceVertices.Length;
                bool colorPresent = sourceColors.Length == sourceVertices.Length;
                hasNormals |= normalPresent;
                hasTangents |= tangentPresent;
                hasUv0 |= uv0Present;
                hasUv1 |= uv1Present;
                hasColors |= colorPresent;
                for (int vertex = 0; vertex < sourceVertices.Length; vertex++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(sourceVertices[vertex]));
                    Vector3 normal = normalPresent ? normalMatrix.MultiplyVector(sourceNormals[vertex]).normalized : Vector3.zero;
                    normals.Add(normal);
                    if (tangentPresent)
                    {
                        Vector4 authored = sourceTangents[vertex];
                        Vector3 direction = matrix.MultiplyVector(new Vector3(authored.x, authored.y, authored.z));
                        direction = (direction - normal * Vector3.Dot(normal, direction)).normalized;
                        tangents.Add(new Vector4(direction.x, direction.y, direction.z, authored.w * handedness));
                    }
                    else tangents.Add(Vector4.zero);
                    uv0.Add(uv0Present ? sourceUv0[vertex] : Vector2.zero);
                    uv1.Add(uv1Present ? sourceUv1[vertex] : Vector2.zero);
                    colors.Add(colorPresent ? sourceColors[vertex] : new Color32(255, 255, 255, 255));
                }
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    if (materials[Mathf.Min(subMesh, materials.Length - 1)] != material) continue;
                    if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                        throw new InvalidOperationException("Ship V3 batch source requires triangle topology: " + mesh.name);
                    var triangles = mesh.GetTriangles(subMesh);
                    for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                    {
                        indices.Add(offset + triangles[triangle]);
                        indices.Add(offset + triangles[triangle + (handedness < 0f ? 2 : 1)]);
                        indices.Add(offset + triangles[triangle + (handedness < 0f ? 1 : 2)]);
                    }
                }
            }
            var combined = new Mesh { name = "ShipV3RenderBatch", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            try
            {
                combined.SetVertices(vertices);
                if (hasNormals) combined.SetNormals(normals);
                if (hasTangents) combined.SetTangents(tangents);
                if (hasUv0) combined.SetUVs(0, uv0);
                if (hasUv1) combined.SetUVs(1, uv1);
                if (hasColors) combined.SetColors(colors);
                combined.SetTriangles(indices, 0, true);
                return combined;
            }
            catch
            {
                if (Application.isPlaying) Destroy(combined);
                else DestroyImmediate(combined);
                throw;
            }
        }
    }
}
