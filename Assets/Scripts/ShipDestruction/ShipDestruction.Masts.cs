using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed partial class ShipDestruction
    {
        sealed class MastAssembly
        {
            public string Key;
            public ShipDamageSection Stem;
            public Vector3 RepairPoint;
            public readonly List<ShipDamageSection> Parts = new();
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            public readonly List<ShipFragmentConnection> RepairOrder = new();
#endif
        }
        readonly Dictionary<string, MastAssembly> mastAssemblies = new();
        readonly Dictionary<int, MastAssembly> mastParts = new();

        void BuildMasts()
        {
            foreach (var section in Sections)
            {
                if (section == null || !definitions.TryGetValue(section.SectionId, out var definition) || definition.Type != ShipSectionType.Mast) continue;
                string source = definition.SourceGroup ?? definition.Name;
                string key = source.Contains("Fore") ? "Fore" : source.Contains("Main") ? "Main" : source.Contains("Mizzen") ? "Mizzen" : source;
                if (!mastAssemblies.TryGetValue(key, out var assembly)) mastAssemblies[key] = assembly = new MastAssembly { Key = key };
                assembly.Parts.Add(section);
                mastParts[section.SectionId] = assembly;
                if (assembly.Stem == null || MastBounds(section).size.y > MastBounds(assembly.Stem).size.y) assembly.Stem = section;
            }
            foreach (var assembly in mastAssemblies.Values)
            {
                var bounds = MastBounds(assembly.Stem);
                var bottom = bounds;
                foreach (var part in assembly.Parts)
                    if (MastBounds(part).min.y < bottom.min.y) bottom = MastBounds(part);
                float deck = bottom.min.y;
                foreach (var section in Sections)
                {
                    if (section == null || !definitions.TryGetValue(section.SectionId, out var definition) || definition.Type != ShipSectionType.Deck) continue;
                    var floor = MastBounds(section);
                    if (bottom.center.x >= floor.min.x - .1f && bottom.center.x <= floor.max.x + .1f && bottom.center.z >= floor.min.z - .1f && bottom.center.z <= floor.max.z + .1f && floor.max.y <= bounds.min.y + 2f)
                        deck = Mathf.Max(deck, floor.max.y);
                }
                assembly.RepairPoint = new Vector3(bottom.center.x, deck + .6f, bottom.center.z);
            }
            foreach (var section in Sections)
            {
                if (section == null || !definitions.TryGetValue(section.SectionId, out var definition) || definition.Type != ShipSectionType.Yard) continue;
                MastAssembly nearest = null;
                float distance = float.MaxValue;
                var center = MastBounds(section).center;
                foreach (var assembly in mastAssemblies.Values)
                {
                    if (assembly.Key.StartsWith("BilgeBeam_")) continue;
                    float candidate = new Vector2(center.x - assembly.RepairPoint.x, center.z - assembly.RepairPoint.z).sqrMagnitude;
                    if (candidate >= distance) continue;
                    distance = candidate; nearest = assembly;
                }
                if (nearest != null) { nearest.Parts.Add(section); mastParts[section.SectionId] = nearest; }
            }
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (Upper != null)
                foreach (var source in Upper.Assemblies)
                    if (Upper.IsMast(Upper.AssemblyFor(source.Sections.Length > 0 ? source.Sections[0].SectionId : -1)) && mastAssemblies.TryGetValue(source.Key, out var assembly))
                    {
                        assembly.Stem = System.Array.Find(source.Sections, part => definitions[part.SectionId].SourceGroup == "V3_Mast_" + source.Key) ?? assembly.Stem;
                        foreach (var part in source.Sections)
                        {
                            if (!assembly.Parts.Contains(part)) assembly.Parts.Add(part);
                            mastParts[part.SectionId] = assembly;
                        }
                        int index = Upper.AssemblyFor(source.Sections[0].SectionId);
                        if (source.Kind != Experimental.UpperAssemblyKind.MainMast)
                            foreach (var child in Upper.Assemblies)
                                if (child.ParentMast == index)
                                    foreach (var part in child.Sections)
                                    {
                                        if (!assembly.Parts.Contains(part)) assembly.Parts.Add(part);
                                        mastParts[part.SectionId] = assembly;
                                    }
                        var order = new List<(ShipFragmentConnection Fragment, float Height)>();
                        foreach (var part in assembly.Parts)
                            for (int fragment = 0; fragment < part.RepairCount; fragment++)
                            {
                                var anchor = part.RepairTransform(fragment);
                                float height = transform.InverseTransformPoint(anchor.TransformPoint(part.RepairBounds(fragment).center)).y;
                                order.Add((new ShipFragmentConnection { SectionId = part.SectionId, Fragment = fragment }, height));
                            }
                        order.Sort((a, b) => a.Height.CompareTo(b.Height));
                        foreach (var entry in order) assembly.RepairOrder.Add(entry.Fragment);
                    }
#endif
        }

        Bounds MastBounds(ShipDamageSection section)
        {
            var filter = section.Intact != null ? section.Intact.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return new Bounds(transform.InverseTransformPoint(section.transform.position), Vector3.one);
            var bounds = filter.sharedMesh.bounds;
            var matrix = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var local = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                local.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            return local;
        }

        public string MastGroupKey(int id) => mastParts.TryGetValue(id, out var assembly) ? assembly.Key : definitions.TryGetValue(id, out var definition) ? definition.SourceGroup : "";
        public bool IsMastCollapsed(int id) => mastParts.TryGetValue(id, out var assembly) && assembly.Stem.State == ShipSectionState.Destroyed;
        bool ProtectedMast(int id) => mastParts.TryGetValue(id, out var assembly) && (!mastHits.TryGetValue(assembly.Key, out int hits) || hits < MastHitLimitFor(id));

        int MastHitLimitFor(int id)
        {
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (Upper != null && Upper.IsUpper(id)) return 3;
#endif
            return 2;
        }

        void HitMast(int id, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker)
        {
            if (!mastParts.TryGetValue(id, out var assembly) || !struckMasts.Add(assembly.Key)) return;
            if (attacker != null) SessionController.Instance?.NotifyCombatDamage(ship, attacker);
            mastHits.TryGetValue(assembly.Key, out int hits);
            mastHits[assembly.Key] = ++hits;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (Upper != null) Upper.SetStage(Upper.AssemblyFor(id), hits);
#endif
            if (hits >= MastHitLimitFor(id)) { CollapseMast(assembly, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit); return; }
            var section = sections[id];
            ulong mask = section.Fragments.Length > 0 ? section.BreakSingleNear(point) : section.BreakNear(point, Profile.CannonDamage, false);
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (Upper != null && hits == 2 && section.Fragments.Length > 3)
            {
                mask = section.RemovedFragments;
                int first = 0;
                while (first < section.Fragments.Length && (mask & (1UL << first)) == 0) first++;
                var center = transform.TransformPoint(MastBounds(section).center);
                Vector3 side = first < section.Fragments.Length ? Vector3.ProjectOnPlane(section.Fragments[first].transform.TransformPoint(section.Fragments[first].GetComponent<MeshFilter>().sharedMesh.bounds.center) - center, transform.up) : transform.right;
                for (int count = 0; count < 2; count++)
                {
                    int closest = -1; float distance = float.PositiveInfinity;
                    for (int i = 0; i < section.Fragments.Length; i++)
                    {
                        if (((mask | section.ProtectedFragments) & (1UL << i)) != 0) continue;
                        var part = section.Fragments[i].transform;
                        var bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
                        if (Vector3.Dot(Vector3.ProjectOnPlane(part.TransformPoint(bounds.center) - center, transform.up), side) <= 0f) continue;
                        float candidate = (part.TransformPoint(bounds.ClosestPoint(part.InverseTransformPoint(point))) - point).sqrMagnitude;
                        if (candidate < distance) { closest = i; distance = candidate; }
                    }
                    if (closest >= 0) mask |= 1UL << closest;
                }
            }
#endif
            Change(id, definitions[id].MaxHealth * .5f, ShipSectionState.Damaged, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit, Profile.CannonDamage, mask);
        }

        void CollapseMast(MastAssembly assembly, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker, ShipDamageReason reason, ulong? impact = null)
        {
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (Upper != null)
            {
                if (Upper.IsMain(assembly.Stem.SectionId)) return;
                Upper.BeginMastCollapse(Upper.AssemblyFor(assembly.Stem.SectionId), velocity);
            }
#endif
            foreach (var part in assembly.Parts)
            {
                if (part.State == ShipSectionState.Destroyed) continue;
                Change(part.SectionId, 0f, ShipSectionState.Destroyed, point, normal, velocity, ammo, attacker, reason, definitions[part.SectionId].MaxHealth, part.Fragments.Length > 0 ? part.AllFragments : null, impact);
            }
        }

        bool MastHasDamage(MastAssembly assembly)
        {
            foreach (var part in assembly.Parts)
                if ((definitions[part.SectionId].Type == ShipSectionType.Mast
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                    || Upper != null && Upper.IsUpper(part.SectionId)
#endif
                    ) && (part.RemovedFragments != 0 || part.State != ShipSectionState.Intact)) return true;
            return false;
        }

        public bool MastRepairPoint(int id, out Vector3 point)
        {
            point = default;
            if (!mastParts.TryGetValue(id, out var assembly) || !MastHasDamage(assembly)) return false;
            point = transform.TransformPoint(assembly.RepairPoint);
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (Upper != null && Upper.DirectMastAssembly(id, out int index) && Upper.Assemblies[index].RepairBounds.size.sqrMagnitude > .001f)
                point = transform.TransformPoint(Upper.Assemblies[index].RepairBounds.center);
#endif
            return true;
        }

        public bool MastRepairFragment(int id, out int sectionId, out int fragmentId)
        {
            sectionId = fragmentId = -1;
            if (!mastParts.TryGetValue(id, out var assembly)) return false;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (Upper != null)
            {
                foreach (var entry in assembly.RepairOrder)
                {
                    if ((sections[entry.SectionId].RemovedFragments & (1UL << entry.Fragment)) == 0) continue;
                    long key = ((long)entry.SectionId << 6) | (uint)entry.Fragment;
                    if (trimSupports.TryGetValue(key, out var support) && (sections[support.SectionId].RemovedFragments & (1UL << support.Fragment)) != 0) continue;
                    sectionId = entry.SectionId; fragmentId = entry.Fragment;
                    return true;
                }
                return false;
            }
#endif
            foreach (var part in assembly.Parts)
                for (int i = 0; i < Mathf.Min(64, part.RepairCount); i++)
                    if ((part.RemovedFragments & (1UL << i)) != 0)
                    { sectionId = part.SectionId; fragmentId = i; return true; }
            return false;
        }
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
        public Mesh MastRepairPreview(int id)
            => Upper != null && Upper.MastAssembly(id, out int index) ? Upper.Assemblies[index].RepairPreview : null;
#endif
        public bool RepairMast(int id)
        {
            if (!ready || !IsServerInitialized || ship.IsSinking || !mastParts.TryGetValue(id, out var assembly) || !MastHasDamage(assembly)) return false;
            foreach (var section in assembly.Parts)
            {
                var entry = state[section.SectionId];
                entry.RemovedFragments = entry.LeakingFragments = 0;
                entry.State = ShipSectionState.Intact; entry.Health = ushort.MaxValue;
                entry.Breach = false; entry.Repair = true; entry.Revision = ++revision;
                state[section.SectionId] = entry;
                section.Health = definitions[section.SectionId].MaxHealth;
                section.Apply(ShipSectionState.Intact);
                SetBreach(entry);
            }
            mastHits.Remove(assembly.Key);
            Publish();
            return true;
        }
    }
}
