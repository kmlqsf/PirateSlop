using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public struct ShipFireTarget
    {
        public int SectionId, Fragment;
        public Vector3 Position, Normal;
    }

    public sealed partial class ShipDestruction
    {
        sealed class FireSurface
        {
            public int Index;
            public Transform Anchor;
            public Matrix4x4 LocalToWorld, WorldToLocal;
            public Bounds Bounds;
            public Vector3 Point;
            public float Distance;
            public Vector3 Closest(Vector3 point) => LocalToWorld.MultiplyPoint3x4(Bounds.ClosestPoint(WorldToLocal.MultiplyPoint3x4(point)));
        }

        static bool FireGapClear(Vector3 own, Vector3 other, List<FireSurface> missing)
        {
            Vector3 delta = other - own;
            if (delta.magnitude <= .06f) return true;
            Vector3 start = own + delta.normalized * .03f, end = other - delta.normalized * .03f;
            foreach (var surface in missing)
            {
                Vector3 localStart = surface.WorldToLocal.MultiplyPoint3x4(start);
                Vector3 localDelta = surface.WorldToLocal.MultiplyPoint3x4(end) - localStart;
                if (surface.Bounds.IntersectRay(new Ray(localStart, localDelta.normalized), out float distance) && distance <= localDelta.magnitude) return false;
            }
            return true;
        }

        Vector3 FireSurfaceNormal(ShipFragmentConnection node, FireSurface surface, Vector3 point, Vector3 fallback)
        {
            if (definitions[node.SectionId].Type == ShipSectionType.Deck) return transform.up;
            Vector3 local = surface.Anchor.InverseTransformPoint(point);
            var bounds = surface.Bounds;
            Vector3 axis = Vector3.right;
            float nearest = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float low = Mathf.Abs(local[i] - bounds.min[i]), high = Mathf.Abs(local[i] - bounds.max[i]);
                if (Mathf.Min(low, high) >= nearest) continue;
                nearest = Mathf.Min(low, high); axis = Vector3.zero; axis[i] = low < high ? -1f : 1f;
            }
            Vector3 normal = surface.Anchor.worldToLocalMatrix.transpose.MultiplyVector(axis).normalized;
            return normal.sqrMagnitude > .01f ? normal : fallback;
        }

        public ulong BeginFireImpact() => flooding != null ? flooding.BeginFireImpact() : 0;

        public bool FireTargetAlive(int id, int fragment)
        {
            return sections.TryGetValue(id, out var section) && section.gameObject.activeInHierarchy
                && section.State != ShipSectionState.Destroyed && fragment >= 0 && fragment < section.RepairCount
                && (section.RemovedFragments & (1UL << fragment)) == 0;
        }

        public Vector3 FireTargetPoint(int id, int fragment, Vector3 fallback)
        {
            if (!sections.TryGetValue(id, out var section) || fragment < 0 || fragment >= section.RepairCount) return fallback;
            var anchor = section.RepairTransform(fragment);
            var bounds = section.RepairBounds(fragment);
            return anchor.TransformPoint(bounds.ClosestPoint(anchor.InverseTransformPoint(fallback)));
        }

        public List<ShipFireTarget> PlanFire(Collider collider, Vector3 point, Vector3 normal)
        {
            var result = new List<ShipFireTarget>();
            if (!ready || !IsServerInitialized || ship.IsSinking) return result;
            var direct = Resolve(collider, point);
            if (direct == null) return result;
            var affected = new Dictionary<int, float> { [direct.SectionId] = Profile.CannonDamage };
            var directDefinition = definitions[direct.SectionId];
            if (!string.IsNullOrEmpty(directDefinition.SourceGroup))
                foreach (var section in Sections)
                    if (definitions[section.SectionId].SourceGroup == directDefinition.SourceGroup
                        && section.Distance(point) < (directDefinition.Type == ShipSectionType.Mast ? 1.2f : .25f))
                        affected[section.SectionId] = Profile.CannonDamage;
            int ordinary = 0;
            foreach (var target in affected)
            {
                var section = sections[target.Key];
                ordinary += CountBits(section.BreakNear(point, target.Value * definitions[target.Key].DamageMultiplier(InventoryItem.Cannonball), false) & ~section.RemovedFragments);
            }
            if (Profile.DamageAdjacentFragments)
                foreach (int id in graph.AdjacentSections(direct.SectionId))
                {
                    if (affected.ContainsKey(id) || !sections.TryGetValue(id, out var neighbour) || neighbour.State == ShipSectionState.Destroyed || !neighbour.gameObject.activeInHierarchy) continue;
                    ordinary += CountBits(neighbour.BreakSingleNear(point) & ~neighbour.RemovedFragments);
                    affected[id] = 0f;
                }
            if (directDefinition.Type == ShipSectionType.Hull)
            {
                ShipDamageSection deck = null;
                float nearest = Profile.HullDeckDamageRadius;
                Vector3 deckPoint = point;
                foreach (var candidate in Sections)
                {
                    if ((!candidate.SurfaceDamage && definitions[candidate.SectionId].Type != ShipSectionType.Deck) || affected.ContainsKey(candidate.SectionId) || candidate.Distance(point) >= nearest) continue;
                    var visual = candidate.Intact.transform;
                    var bounds = candidate.Intact.GetComponent<MeshFilter>().sharedMesh.bounds;
                    var local = visual.InverseTransformPoint(point);
                    var top = visual.TransformPoint(new Vector3(Mathf.Clamp(local.x, bounds.min.x, bounds.max.x), bounds.max.y, Mathf.Clamp(local.z, bounds.min.z, bounds.max.z)));
                    float distance = Vector3.Distance(point, top);
                    if (distance >= nearest) continue;
                    nearest = distance; deck = candidate; deckPoint = top;
                }
                if (deck != null) ordinary += CountBits(deck.BreakNear(deckPoint, Profile.CannonDamage * Profile.HullDeckDamageFraction * definitions[deck.SectionId].DamageMultiplier(InventoryItem.Cannonball), false) & ~deck.RemovedFragments);
            }
            int minimum = Mathf.Clamp(Profile.MinimumFireFragments, 1, 64);
            int budget = Mathf.Clamp(Mathf.Max(minimum, Mathf.CeilToInt(ordinary * 1.5f) * 2), minimum, Mathf.Clamp(Profile.MaximumFireFragments, minimum, 64));
            var connections = Profile.Structure;
            var candidates = new List<FireSurface>();
            var missing = new List<FireSurface>();
            var surfaces = new Dictionary<int, FireSurface>();
            FireSurface start = null;
            for (int i = 0; i < connections.Length; i++)
            {
                var node = connections[i];
                if (!sections.TryGetValue(node.SectionId, out var section) || node.Fragment < 0 || node.Fragment >= section.RepairCount) continue;
                var anchor = section.RepairTransform(node.Fragment);
                var candidate = new FireSurface { Index = i, Anchor = anchor, LocalToWorld = anchor.localToWorldMatrix, WorldToLocal = anchor.worldToLocalMatrix, Bounds = section.RepairBounds(node.Fragment) };
                candidate.Point = candidate.Closest(point);
                float distance = (candidate.Point - point).sqrMagnitude;
                if (distance > 72f) continue;
                candidate.Distance = distance;
                if (!FireTargetAlive(node.SectionId, node.Fragment)) { missing.Add(candidate); continue; }
                candidates.Add(candidate); surfaces[i] = candidate;
                if (node.SectionId == direct.SectionId && (start == null || distance < start.Distance)) start = candidate;
            }
            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            if (candidates.Count == 0) return result;
            start ??= candidates[0];
            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            queue.Enqueue(start.Index); visited.Add(start.Index);
            float gapSquared = Mathf.Pow(Mathf.Max(.05f, Profile.FireSurfaceGap), 2f);
            while (queue.Count > 0 && result.Count < budget)
            {
                int index = queue.Dequeue();
                var node = connections[index];
                if (!FireTargetAlive(node.SectionId, node.Fragment)) continue;
                var surface = surfaces[index];
                var at = surface.Point;
                Vector3 surfaceNormal = FireSurfaceNormal(node, surface, at, normal);
                result.Add(new ShipFireTarget { SectionId = node.SectionId, Fragment = node.Fragment,
                    Position = transform.InverseTransformPoint(at + surfaceNormal * .04f), Normal = transform.InverseTransformDirection(surfaceNormal) });
                var neighbours = new List<FireSurface>();
                var added = new HashSet<int>();
                foreach (int next in node.Neighbours)
                {
                    if (visited.Contains(next) || !surfaces.TryGetValue(next, out var neighbour)) continue;
                    neighbours.Add(neighbour); added.Add(next);
                }
                var spatial = new List<(FireSurface Surface, float Gap, Vector3 Own, Vector3 Other)>();
                foreach (var candidate in candidates)
                {
                    if (visited.Contains(candidate.Index) || added.Contains(candidate.Index)) continue;
                    Vector3 other = candidate.Closest(surface.Point);
                    Vector3 own = surface.Closest(other);
                    other = candidate.Closest(own);
                    if ((own - point).sqrMagnitude > 72f || (other - point).sqrMagnitude > 72f) continue;
                    float gap = (own - other).sqrMagnitude;
                    if (gap <= gapSquared) spatial.Add((candidate, gap, own, other));
                }
                spatial.Sort((a, b) =>
                {
                    if (definitions[node.SectionId].Type == ShipSectionType.Railing)
                    {
                        bool deckA = definitions[connections[a.Surface.Index].SectionId].Type == ShipSectionType.Deck;
                        bool deckB = definitions[connections[b.Surface.Index].SectionId].Type == ShipSectionType.Deck;
                        if (deckA != deckB) return deckA ? -1 : 1;
                    }
                    int gapOrder = a.Gap.CompareTo(b.Gap);
                    return gapOrder != 0 ? gapOrder : a.Surface.Distance.CompareTo(b.Surface.Distance);
                });
                int spatialCount = 0;
                foreach (var candidate in spatial)
                {
                    if (!FireGapClear(candidate.Own, candidate.Other, missing)) continue;
                    neighbours.Add(candidate.Surface);
                    if (++spatialCount >= 6) break;
                }
                neighbours.Sort((a, b) =>
                {
                    if (definitions[node.SectionId].Type == ShipSectionType.Railing)
                    {
                        bool deckA = definitions[connections[a.Index].SectionId].Type == ShipSectionType.Deck;
                        bool deckB = definitions[connections[b.Index].SectionId].Type == ShipSectionType.Deck;
                        if (deckA != deckB) return deckA ? -1 : 1;
                    }
                    return a.Distance.CompareTo(b.Distance);
                });
                foreach (var neighbour in neighbours)
                    if (visited.Add(neighbour.Index)) queue.Enqueue(neighbour.Index);
            }
            var masts = new HashSet<string>();
            foreach (var target in result)
            {
                var definition = definitions[target.SectionId];
                if (definition.Type != ShipSectionType.Mast) continue;
                string key = MastGroupKey(target.SectionId);
                if (masts.Add(key)) { mastHits.TryGetValue(key, out int hits); mastHits[key] = hits + 1; }
            }
            return result;
        }

        public void BurnFragment(ShipFireTarget target, GameObject attacker, ulong impactId)
        {
            if (!ready || !IsServerInitialized || ship.IsSinking || !FireTargetAlive(target.SectionId, target.Fragment)) return;
            var definition = definitions[target.SectionId];
            if (definition.Type == ShipSectionType.Mast)
            {
                if (!mastParts.TryGetValue(target.SectionId, out var mast)) return;
                if (mastHits.TryGetValue(mast.Key, out int hits) && hits >= 2)
                {
                    CollapseMast(mast, transform.TransformPoint(target.Position), transform.TransformDirection(target.Normal), Vector3.zero, InventoryItem.FireCannonball, attacker, ShipDamageReason.Fire, impactId);
                    DetachUnsupported(transform.TransformPoint(target.Position), transform.up, Vector3.zero, InventoryItem.FireCannonball, attacker, impactId);
                    if (attacker != null) SessionController.Instance?.NotifyCombatDamage(ship, attacker);
                    Publish();
                    return;
                }
                if (MastHasDamage(mast)) return;
            }
            var section = sections[target.SectionId];
            ulong mask = section.RemovedFragments | (1UL << target.Fragment);
            Vector3 point = FireTargetPoint(target.SectionId, target.Fragment, transform.TransformPoint(target.Position));
            Change(target.SectionId, section.Health, section.State, point, transform.TransformDirection(target.Normal), Vector3.zero,
                InventoryItem.FireCannonball, attacker, ShipDamageReason.Fire, definition.MaxHealth / Mathf.Max(1, section.RepairCount), mask, impactId);
            DetachUnsupported(point, transform.up, Vector3.zero, InventoryItem.FireCannonball, attacker, impactId);
            if (attacker != null) SessionController.Instance?.NotifyCombatDamage(ship, attacker);
            Publish();
        }

        static int CountBits(ulong value)
        {
            int count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }
    }
}
