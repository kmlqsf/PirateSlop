using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public sealed class ShipDamageSection : MonoBehaviour
    {
        public int SectionId;
        public Collider[] DamageColliders = Array.Empty<Collider>();
        public Collider[] GameplayColliders = Array.Empty<Collider>();
        public Collider[] DamagedColliders = Array.Empty<Collider>();
        public Collider[] CriticalColliders = Array.Empty<Collider>();
        public Collider[] ReplacementColliders = Array.Empty<Collider>();
        public bool SafeColliderReplacement;
        public bool SurfaceDamage;
        Mesh surfaceMesh;
        Vector3[] surfaceVertices, surfaceNormals;
        int[][] surfaceTriangles;
        float[] surfaceDamage;
        ulong appliedSurface;
        public GameObject Intact, Damaged, Critical, Destroyed, Repaired;
        public GameObject[] Debris = Array.Empty<GameObject>();
        public GameObject[] Fragments = Array.Empty<GameObject>();
        public ulong RemovedFragments { get; private set; }
        public Renderer[] DependentRenderers = Array.Empty<Renderer>();
        public Collider[] DisabledControls = Array.Empty<Collider>();
        public ShipDestruction Owner { get; internal set; }
        public ShipSectionState State { get; internal set; }
        public float Health { get; internal set; }
        public Vector3 LocalCenter => Owner.transform.InverseTransformPoint(transform.position);
        public void Apply(ShipSectionState state, ulong removedFragments = 0)
        {
            State = state;
            RemovedFragments = removedFragments;
            if (SurfaceDamage) ApplySurface(removedFragments);
            if (Fragments.Length > 0)
            {
                bool fractured = removedFragments != 0;
                foreach (var visual in new[] { Intact, Damaged, Critical, Destroyed, Repaired }) if (visual != null) visual.SetActive(!fractured && visual == Intact);
                foreach (var collider in GameplayColliders) if (collider != null) collider.enabled = !fractured;
                foreach (var collider in DamagedColliders) if (collider != null) collider.enabled = false;
                foreach (var collider in CriticalColliders) if (collider != null) collider.enabled = false;
                foreach (var collider in ReplacementColliders) if (collider != null) collider.enabled = false;
                for (int i = 0; i < Fragments.Length; i++) Fragments[i].SetActive(fractured && (removedFragments & (1UL << i)) == 0);
                foreach (var collider in DisabledControls) if (collider != null) collider.enabled = state != ShipSectionState.Destroyed;
                foreach (var renderer in DependentRenderers) if (renderer != null) renderer.enabled = state != ShipSectionState.Destroyed;
                return;
            }
            var visible = state == ShipSectionState.Intact ? Intact : state == ShipSectionState.Damaged ? Damaged : state == ShipSectionState.Critical ? Critical : state == ShipSectionState.Destroyed ? Destroyed : Repaired;
            if (!SafeColliderReplacement) visible = Intact;
            if (visible == null && state != ShipSectionState.Destroyed) visible = Intact;
            foreach (var item in new[] { Intact, Damaged, Critical, Destroyed, Repaired }) if (item != null) item.SetActive(item == visible);
            foreach (var renderer in DependentRenderers) if (renderer != null) renderer.enabled = state != ShipSectionState.Destroyed;
            foreach (var collider in DisabledControls) if (collider != null) collider.enabled = state != ShipSectionState.Destroyed;
            if (!SafeColliderReplacement) return;
            bool damaged = state == ShipSectionState.Damaged && DamagedColliders.Length > 0;
            bool critical = state == ShipSectionState.Critical && CriticalColliders.Length > 0;
            foreach (var collider in ReplacementColliders) if (collider != null) collider.enabled = state == ShipSectionState.Destroyed;
            foreach (var collider in DamagedColliders) if (collider != null) collider.enabled = damaged;
            foreach (var collider in CriticalColliders) if (collider != null) collider.enabled = critical;
            foreach (var collider in GameplayColliders) if (collider != null) collider.enabled = state != ShipSectionState.Destroyed && !damaged && !critical;
        }
        public ulong BreakNear(Vector3 point, float damage, bool supportLost)
        {
            if (SurfaceDamage && damage > 0f)
            {
                var bounds = Intact.GetComponent<MeshFilter>().sharedMesh.bounds;
                var local = Intact.transform.InverseTransformPoint(point);
                int x = Mathf.Clamp(Mathf.FloorToInt((local.x - bounds.min.x) / bounds.size.x * 8f), 0, 7);
                int z = Mathf.Clamp(Mathf.FloorToInt((local.z - bounds.min.z) / bounds.size.z * 8f), 0, 7);
                int selected = z * 8 + x;
                if ((RemovedFragments & (1UL << selected)) != 0)
                {
                    float nearest = float.MaxValue;
                    for (int bit = 0; bit < 64; bit++)
                    {
                        if ((RemovedFragments & (1UL << bit)) != 0) continue;
                        float dx = bounds.min.x + (bit % 8 + .5f) * bounds.size.x / 8f - local.x;
                        float dz = bounds.min.z + (bit / 8 + .5f) * bounds.size.z / 8f - local.z;
                        float distance = dx * dx + dz * dz;
                        if (distance < nearest) { nearest = distance; selected = bit; }
                    }
                }
                return RemovedFragments | (1UL << selected);
            }
            if (Fragments.Length == 0 || damage <= 0f && !supportLost) return RemovedFragments;
            if (supportLost) return Fragments.Length == 64 ? ulong.MaxValue : (1UL << Fragments.Length) - 1UL;
            ulong mask = RemovedFragments;
            int count = Mathf.Clamp(Mathf.CeilToInt(damage / 28f), 1, 5);
            for (int n = 0; n < count; n++)
            {
                int nearest = -1; float distance = float.MaxValue;
                for (int i = 0; i < Fragments.Length; i++)
                {
                    if ((mask & (1UL << i)) != 0) continue;
                    var bounds = Fragments[i].GetComponent<MeshFilter>().sharedMesh.bounds;
                    Vector3 local = Fragments[i].transform.InverseTransformPoint(point);
                    float score = (bounds.ClosestPoint(local) - local).sqrMagnitude + .15f * (bounds.center - local).sqrMagnitude;
                    if (score < distance) { distance = score; nearest = i; }
                }
                if (nearest < 0) break;
                mask |= 1UL << nearest;
            }
            return mask;
        }
        public float Distance(Vector3 point)
        {
            float distance = float.MaxValue;
            foreach (var collider in DamageColliders)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy) distance = Mathf.Min(distance, Vector3.Distance(collider is MeshCollider mesh && !mesh.convex ? collider.bounds.ClosestPoint(point) : collider.ClosestPoint(point), point));
            return distance;
        }
        void ApplySurface(ulong mask)
        {
            if (mask == appliedSurface) return;
            if (surfaceMesh == null)
            {
                var filter = Intact.GetComponent<MeshFilter>();
                if (!filter.sharedMesh.isReadable) return;
                surfaceMesh = Instantiate(filter.sharedMesh);
                surfaceMesh.MarkDynamic();
                surfaceVertices = surfaceMesh.vertices;
                surfaceNormals = surfaceMesh.normals;
                surfaceDamage = new float[surfaceVertices.Length];
                surfaceTriangles = new int[surfaceMesh.subMeshCount][];
                for (int sub = 0; sub < surfaceTriangles.Length; sub++) surfaceTriangles[sub] = surfaceMesh.GetTriangles(sub);
                var renderer = Intact.GetComponent<MeshRenderer>();
                var materials = new List<Material>(renderer.sharedMaterials);
                materials.Add(Owner != null ? Owner.Profile.SplinterMaterial : materials[0]);
                renderer.sharedMaterials = materials.ToArray();
                filter.sharedMesh = surfaceMesh;
            }
            appliedSurface = mask;
            Array.Clear(surfaceDamage, 0, surfaceDamage.Length);
            var bounds = surfaceMesh.bounds;
            var vertices = (Vector3[])surfaceVertices.Clone();
            float width = bounds.size.x / 8f, length = bounds.size.z / 8f;
            for (int bit = 0; bit < 64; bit++)
            {
                if ((mask & (1UL << bit)) == 0) continue;
                float x = bounds.min.x + (bit % 8 + .5f) * width;
                float z = bounds.min.z + (bit / 8 + .5f) * length;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (surfaceNormals[i].y < .5f) continue;
                    float dx = (surfaceVertices[i].x - x) / Mathf.Max(.45f, width * .55f);
                    float dz = (surfaceVertices[i].z - z) / Mathf.Max(.8f, length * .7f);
                    float edge = .12f * Mathf.Sin(surfaceVertices[i].x * 39f + bit * 2.7f);
                    float influence = Mathf.Clamp01(1f - dx * dx - Mathf.Abs(dz) + edge);
                    surfaceDamage[i] = Mathf.Max(surfaceDamage[i], influence);
                }
            }
            for (int i = 0; i < vertices.Length; i++) vertices[i].y -= .065f * surfaceDamage[i];
            surfaceMesh.vertices = vertices;
            surfaceMesh.subMeshCount = surfaceTriangles.Length + 1;
            var exposed = new List<int>();
            for (int sub = 0; sub < surfaceTriangles.Length; sub++)
            {
                var remaining = new List<int>();
                var triangles = surfaceTriangles[sub];
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    var target = (surfaceDamage[a] + surfaceDamage[b] + surfaceDamage[c]) > .35f ? exposed : remaining;
                    target.Add(a);target.Add(b);target.Add(c);
                }
                surfaceMesh.SetTriangles(remaining, sub, false);
            }
            surfaceMesh.SetTriangles(exposed, surfaceTriangles.Length, false);
            surfaceMesh.RecalculateNormals();
            surfaceMesh.RecalculateTangents();
        }
        void OnDestroy()
        {
            if (surfaceMesh != null) Destroy(surfaceMesh);
        }
    }
}
