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
            int budget = Mathf.Max(1, Mathf.CeilToInt(ordinary * 1.5f));
            var connections = Profile.Structure;
            var candidates = new List<(int Index, float Distance)>();
            for (int i = 0; i < connections.Length; i++)
            {
                var node = connections[i];
                if (!FireTargetAlive(node.SectionId, node.Fragment)) continue;
                var at = FireTargetPoint(node.SectionId, node.Fragment, point);
                candidates.Add((i, (at - point).sqrMagnitude));
            }
            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            if (candidates.Count == 0) return result;
            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            queue.Enqueue(candidates[0].Index); visited.Add(candidates[0].Index);
            while (queue.Count > 0 && result.Count < budget)
            {
                int index = queue.Dequeue();
                var node = connections[index];
                if (!FireTargetAlive(node.SectionId, node.Fragment)) continue;
                var at = FireTargetPoint(node.SectionId, node.Fragment, point);
                if ((at - point).sqrMagnitude > 36f) continue;
                result.Add(new ShipFireTarget { SectionId = node.SectionId, Fragment = node.Fragment,
                    Position = transform.InverseTransformPoint(at + normal * .04f), Normal = transform.InverseTransformDirection(normal) });
                var neighbours = new List<(int Index, float Distance)>();
                foreach (int next in node.Neighbours)
                {
                    if (next < 0 || next >= connections.Length || !visited.Add(next)) continue;
                    var target = connections[next];
                    neighbours.Add((next, (FireTargetPoint(target.SectionId, target.Fragment, point) - point).sqrMagnitude));
                }
                neighbours.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                foreach (var neighbour in neighbours) queue.Enqueue(neighbour.Index);
            }
            var masts = new HashSet<string>();
            foreach (var target in result)
            {
                var definition = definitions[target.SectionId];
                if (definition.Type != ShipSectionType.Mast) continue;
                string key = string.IsNullOrEmpty(definition.SourceGroup) ? definition.Name : definition.SourceGroup;
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
                string key = string.IsNullOrEmpty(definition.SourceGroup) ? definition.Name : definition.SourceGroup;
                if (!mastHits.TryGetValue(key, out int hits) || hits < 2) return;
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
