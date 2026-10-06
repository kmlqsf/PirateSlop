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
                    float candidate = new Vector2(center.x - assembly.RepairPoint.x, center.z - assembly.RepairPoint.z).sqrMagnitude;
                    if (candidate >= distance) continue;
                    distance = candidate; nearest = assembly;
                }
                if (nearest != null) { nearest.Parts.Add(section); mastParts[section.SectionId] = nearest; }
            }
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
        bool ProtectedMast(int id) => mastParts.TryGetValue(id, out var assembly) && (!mastHits.TryGetValue(assembly.Key, out int hits) || hits < 2);

        void HitMast(int id, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker)
        {
            if (!mastParts.TryGetValue(id, out var assembly) || !struckMasts.Add(assembly.Key)) return;
            if (attacker != null) SessionController.Instance?.NotifyCombatDamage(ship, attacker);
            mastHits.TryGetValue(assembly.Key, out int hits);
            mastHits[assembly.Key] = ++hits;
            if (hits >= 2) { CollapseMast(assembly, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit); return; }
            var section = sections[id];
            ulong mask = section.Fragments.Length > 0 ? section.BreakSingleNear(point) : section.BreakNear(point, Profile.CannonDamage, false);
            Change(id, definitions[id].MaxHealth * .5f, ShipSectionState.Damaged, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit, Profile.CannonDamage, mask);
        }

        void CollapseMast(MastAssembly assembly, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker, ShipDamageReason reason, ulong? impact = null)
        {
            foreach (var part in assembly.Parts)
            {
                if (part.State == ShipSectionState.Destroyed) continue;
                Change(part.SectionId, 0f, ShipSectionState.Destroyed, point, normal, velocity, ammo, attacker, reason, definitions[part.SectionId].MaxHealth, part.Fragments.Length > 0 ? part.AllFragments : null, impact);
            }
        }

        bool MastHasDamage(MastAssembly assembly)
        {
            foreach (var part in assembly.Parts)
                if (definitions[part.SectionId].Type == ShipSectionType.Mast && (part.RemovedFragments != 0 || part.State != ShipSectionState.Intact)) return true;
            return false;
        }

        public bool MastRepairPoint(int id, out Vector3 point)
        {
            point = default;
            if (!mastParts.TryGetValue(id, out var assembly) || !MastHasDamage(assembly)) return false;
            point = transform.TransformPoint(assembly.RepairPoint);
            return true;
        }

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
