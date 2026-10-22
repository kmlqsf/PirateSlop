#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System.Collections.Generic;
using PirateSlop.Experimental;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed partial class ShipDestruction
    {
        public UpperDestructionV2 Upper { get; private set; }
        readonly HashSet<ShipDamageSection> upperImpactSections = new();

        public bool UpperRepairTarget(int id, out int index, out Bounds bounds, out int fragment)
        {
            bounds = default; fragment = -1;
            index = Upper != null ? Upper.AssemblyFor(id) : -1;
            if (index < 0) return false;
            var assembly = Upper.Assemblies[index];
            if (Upper.DirectMastAssembly(id, out index))
            {
                if (!MastRepairPoint(id, out _)) return false;
            }
            else
            {
                if (!Upper.RepairableMechanism(id, out index)) return false;
                bool damaged = Upper.Stage(index) > 0;
                foreach (var part in assembly.Sections) damaged |= part.RemovedFragments != 0;
                if (!damaged) return false;
                fragment = -2;
            }
            bounds = assembly.RepairBounds;
            return bounds.size.sqrMagnitude > .001f;
        }

        public bool UpperRepairFragment(int id, Vector3 point, out int sectionId, out int fragmentId)
        {
            sectionId = fragmentId = -1;
            if (Upper == null || !Upper.RepairableMechanism(id, out int index)) return false;
            float nearest = float.PositiveInfinity;
            foreach (var section in Upper.Assemblies[index].Sections)
            {
                if (section.Indestructible || !Upper.CanRepairSection(section.SectionId)) continue;
                for (int fragment = 0; fragment < Mathf.Min(64, section.RepairCount); fragment++)
                {
                    if ((section.RemovedFragments & (1UL << fragment)) == 0) continue;
                    long key = ((long)section.SectionId << 6) | (uint)fragment;
                    if (trimSupports.TryGetValue(key, out var support) && (sections[support.SectionId].RemovedFragments & (1UL << support.Fragment)) != 0) continue;
                    var source = section.RepairTransform(fragment);
                    float distance = (source.TransformPoint(section.RepairBounds(fragment).ClosestPoint(source.InverseTransformPoint(point))) - point).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance; sectionId = section.SectionId; fragmentId = fragment;
                }
            }
            return sectionId >= 0;
        }

        public void DamageUpperAssembly(int index, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker)
        {
            if (Upper == null || !IsServerInitialized || index < 0 || index >= Upper.Assemblies.Length) return;
            var parts = Upper.Assemblies[index].Sections;
            if (parts.Length == 0) return;
            DamageUpperSection(parts[0], point, normal, velocity, ammo, attacker, new HashSet<int>());
            DetachUnsupported(point, normal, velocity, ammo, attacker);
            Publish();
        }

        public bool RepairUpperAssembly(int index)
        {
            if (Upper == null || !IsServerInitialized || index < 0 || index >= Upper.Assemblies.Length) return false;
            bool complete = true;
            foreach (var section in Upper.Assemblies[index].Sections)
                for (int i = 0; i < section.RepairCount; i++)
                    if ((section.RemovedFragments & (1UL << i)) != 0 && !RepairFragment(section.SectionId, i, false)) complete = false;
            Publish();
            return complete;
        }

        public void DetachUpperMechanisms()
        {
            if (Upper == null || !IsServerInitialized) return;
            bool changed = false;
            for (int index = 0; index < Upper.Assemblies.Length; index++)
            {
                if (!Upper.MechanismLostSupport(index)) continue;
                Upper.SetStage(index, 3);
                foreach (var section in Upper.Assemblies[index].Sections)
                    if (!section.Indestructible)
                        Change(section.SectionId, 0f, ShipSectionState.Destroyed, section.Intact.transform.position, transform.up, Vector3.zero,
                            InventoryItem.Cannonball, null, ShipDamageReason.SupportLost, 0f, section.AllFragments);
                changed = true;
            }
            if (changed) Publish();
        }

        bool TryUpperDamage(Collider collider, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker, float radius)
        {
            if (Upper == null || ammo == InventoryItem.BoardingHook || !CannonAmmo.IsBall(ammo)) return false;
            if (collider != null && Upper.ProtectedDispenser != null && collider.transform.IsChildOf(Upper.ProtectedDispenser)) return true;
            var direct = Resolve(collider, point);
            if (direct == null || !Upper.IsUpper(direct.SectionId)) return false;
            if (!ready || !IsServerInitialized || ship.IsSinking || !float.IsFinite(point.sqrMagnitude) || !float.IsFinite(velocity.sqrMagnitude)) return true;
            var player = attacker != null ? attacker.GetComponent<NetworkPlayer>() : null;
            if (!SessionController.FriendlyFire && player != null && player.TeamId.Value > 0 && player.TeamId.Value == ship.TeamId.Value) return true;
            Upper.MarkImpact(); struckMasts.Clear();
            var assemblies = new HashSet<int>();
            bool structural = Upper.ImpactStructure(direct);
            if (!structural) DamageUpperSection(direct, point, normal, velocity, ammo, attacker, assemblies);
            if (structural || radius > 0f)
            {
                float spread = Mathf.Max(radius, structural ? Upper.StructureImpactRadius(direct) : 0f);
                Upper.CollectImpactSections(point, spread * 1.15f, upperImpactSections);
                foreach (var section in upperImpactSections)
                {
                    ulong mask = Upper.StructureImpactMask(section, point, spread);
                    if (section == direct && !section.Indestructible && mask == section.RemovedFragments) mask = section.BreakSingleNear(point);
                    if (mask == section.RemovedFragments) continue;
                    Change(section.SectionId, 0f, ShipSectionState.Damaged, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit, Profile.CannonDamage, mask);
                }
            }
            if (radius > 0f)
                foreach (var section in Sections)
                    if (section != direct && !section.Indestructible && !Upper.ImpactStructure(section) && Upper.IsUpper(section.SectionId) && section.Distance(point) <= radius)
                        DamageUpperSection(section, point, normal, velocity, ammo, attacker, assemblies);
            DetachUnsupported(point, normal, velocity, ammo, attacker);
            Publish();
            return true;
        }

        void DamageUpperSection(ShipDamageSection section, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker, HashSet<int> affected)
        {
            int index = Upper.HitAssemblyFor(section.SectionId);
            if (section.Indestructible && index == Upper.AssemblyFor(section.SectionId)) return;
            if (index >= 0)
            {
                if (!affected.Add(index)) return;
                var assembly = Upper.Assemblies[index];
                if (assembly.Kind == UpperAssemblyKind.MainMast) return;
                if (Upper.IsMast(index))
                {
                    int mast = section.SectionId;
                    if (definitions[mast].Type != ShipSectionType.Mast)
                        foreach (var part in assembly.Sections) if (definitions[part.SectionId].SourceGroup == "V3_Mast_" + assembly.Key) { mast = part.SectionId; break; }
                    HitMast(mast, point, normal, velocity, ammo, attacker);
                    return;
                }
                int hits = assembly.Kind == UpperAssemblyKind.SailControl ? 3 : Mathf.Min(3, Upper.Stage(index) + 1);
                Upper.SetStage(index, hits);
                if (hits == 3)
                {
                    foreach (var part in assembly.Sections)
                        if (!part.Indestructible) Change(part.SectionId, 0f, ShipSectionState.Destroyed, point, normal, velocity, ammo, attacker,
                            ShipDamageReason.Hit, Profile.CannonDamage, part.AllFragments);
                    return;
                }
                if (assembly.Kind == UpperAssemblyKind.Capstan)
                {
                    ShipDamageSection selected = null; float nearest = float.PositiveInfinity;
                    foreach (var part in assembly.Sections)
                    {
                        var source = part.Intact.GetComponent<MeshFilter>();
                        float distance = (source.transform.TransformPoint(source.sharedMesh.bounds.ClosestPoint(source.transform.InverseTransformPoint(point))) - point).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; selected = part; }
                    }
                    if (selected != null)
                    {
                        ulong mask = selected.RemovedFragments;
                        bool beam = selected.Intact.name.Contains("Capstan_Beam_");
                        int count = beam ? selected.Fragments.Length : selected.Fragments.Length > 3 ? 3 : 1;
                        for (int chip = 0; chip < count; chip++)
                        {
                            int chosen = -1; float best = float.PositiveInfinity;
                            for (int bit = 0; bit < selected.Fragments.Length; bit++)
                            {
                                if ((mask & (1UL << bit)) != 0 || !beam && !Upper.CanChipFragment(index, selected, bit)) continue;
                                var source = selected.RepairTransform(bit);
                                float distance = (source.TransformPoint(selected.RepairBounds(bit).ClosestPoint(source.InverseTransformPoint(point))) - point).sqrMagnitude;
                                if (distance < best) { best = distance; chosen = bit; }
                            }
                            if (chosen < 0) break;
                            mask |= 1UL << chosen;
                        }
                        Change(selected.SectionId, 0f, ShipSectionState.Damaged, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit, Profile.CannonDamage, mask);
                    }
                    return;
                }
                int total = 0, removed = 0;
                foreach (var part in assembly.Sections) { total += part.RepairCount; removed += CountBits(part.RemovedFragments); }
                int wanted = Mathf.CeilToInt(total * hits / 3f);
                while (removed < wanted)
                {
                    ShipDamageSection closest = null; int closestBit = -1; float best = float.PositiveInfinity;
                    foreach (var part in assembly.Sections)
                    {
                        if (part.Indestructible || part.RemovedFragments == part.AllFragments) continue;
                        for (int fragment = 0; fragment < part.Fragments.Length; fragment++)
                        {
                            if (((part.RemovedFragments | part.ProtectedFragments) & (1UL << fragment)) != 0) continue;
                            if (!Upper.CanChipFragment(index, part, fragment)) continue;
                            var anchor = part.RepairTransform(fragment);
                            float distance = (anchor.TransformPoint(part.RepairBounds(fragment).ClosestPoint(anchor.InverseTransformPoint(point))) - point).sqrMagnitude;
                            if (distance < best) { best = distance; closest = part; closestBit = fragment; }
                        }
                    }
                    if (closest == null) break;
                    ulong mask = closest.RemovedFragments | (1UL << closestBit);
                    if (mask == closest.RemovedFragments) break;
                    Change(closest.SectionId, 0f, ShipSectionState.Damaged, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit, Profile.CannonDamage, mask);
                    removed++;
                }
                return;
            }
            if (section.Fragments.Length > 0)
                Change(section.SectionId, 0f, ShipSectionState.Damaged, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit, Profile.CannonDamage,
                    section.BreakNear(point, Mathf.Max(84f, Profile.CannonDamage), false));
            else ApplyDamage(section.SectionId, Profile.CannonDamage, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit);
        }
    }
}
#endif
