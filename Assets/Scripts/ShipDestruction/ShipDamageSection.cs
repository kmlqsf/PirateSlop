using System;
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
        public GameObject Intact, Damaged, Critical, Destroyed, Repaired;
        public GameObject[] Debris = Array.Empty<GameObject>();
        public GameObject[] Fragments = Array.Empty<GameObject>();
        public ulong RemovedFragments { get; private set; }
        public Renderer[] DependentRenderers = Array.Empty<Renderer>();
        public Collider[] DisabledControls = Array.Empty<Collider>();
        public int[] ControlFragments = Array.Empty<int>();
        public ShipDestruction Owner { get; internal set; }
        public ShipSectionState State { get; internal set; }
        public float Health { get; internal set; }
        public ulong AllFragments => Fragments.Length >= 64 ? ulong.MaxValue : (1UL << Fragments.Length) - 1UL;
        public Vector3 LocalCenter => Owner.transform.InverseTransformPoint(transform.position);
        public void Apply(ShipSectionState state, ulong removedFragments = 0)
        {
            State = state;
            RemovedFragments = removedFragments;
            if (Fragments.Length > 0)
            {
                bool fractured = removedFragments != 0 || state == ShipSectionState.Destroyed;
                foreach (var visual in new[] { Intact, Damaged, Critical, Destroyed, Repaired }) if (visual != null) visual.SetActive(!fractured && visual == Intact);
                foreach (var collider in GameplayColliders) if (collider != null) collider.enabled = !fractured;
                foreach (var collider in DamagedColliders) if (collider != null) collider.enabled = false;
                foreach (var collider in CriticalColliders) if (collider != null) collider.enabled = false;
                foreach (var collider in ReplacementColliders) if (collider != null) collider.enabled = false;
                for (int i = 0; i < Fragments.Length; i++) Fragments[i].SetActive(fractured && state != ShipSectionState.Destroyed && (removedFragments & (1UL << i)) == 0);
                for (int i = 0; i < DisabledControls.Length; i++)
                    if (DisabledControls[i] != null) DisabledControls[i].enabled = state != ShipSectionState.Destroyed && (i >= ControlFragments.Length || (removedFragments & (1UL << ControlFragments[i])) == 0);
                foreach (var renderer in DependentRenderers) if (renderer != null) renderer.enabled = state != ShipSectionState.Destroyed;
                return;
            }
            var visible = state == ShipSectionState.Intact ? Intact : state == ShipSectionState.Damaged ? Damaged : state == ShipSectionState.Critical ? Critical : state == ShipSectionState.Destroyed ? Destroyed : Repaired;
            if (!SafeColliderReplacement && state != ShipSectionState.Destroyed) visible = Intact;
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
            if (Fragments.Length == 0 || damage <= 0f && !supportLost) return RemovedFragments;
            if (supportLost) return AllFragments;
            ulong mask = RemovedFragments;
            int count = Owner != null && (Owner.Definition(SectionId).Type == ShipSectionType.Mast || Owner.Definition(SectionId).Type == ShipSectionType.Yard) ? 1 : Mathf.Clamp(Mathf.CeilToInt(damage / 28f), 1, 5);
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
                if (Owner != null && Owner.Definition(SectionId).Type == ShipSectionType.Mast)
                {
                    var chosen = Fragments[nearest].GetComponent<MeshFilter>().sharedMesh.bounds;
                    Vector3 localPoint = transform.InverseTransformPoint(point);
                    for (int i = 0; i < Fragments.Length; i++)
                    {
                        var bounds = Fragments[i].GetComponent<MeshFilter>().sharedMesh.bounds;
                        var closest = bounds.ClosestPoint(localPoint);
                        if (Mathf.Abs(bounds.center.y - chosen.center.y) < .55f && new Vector2(closest.x - localPoint.x, closest.z - localPoint.z).sqrMagnitude < 1.44f) mask |= 1UL << i;
                    }
                }
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
    }
}
