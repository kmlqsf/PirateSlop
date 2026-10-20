using System;
using System.Collections.Generic;
using System.Linq;
using PirateSlop.Experimental;
using PirateSlop.Ships;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static partial class UpperDestructionAssetSetup
    {
        static bool DeckRig(string name) => name.Contains("Shroud_Channel") || name.Contains("Channel_Bracket") || name.Contains("Chainplate") || name.Contains("Deadeye_Lower");

        static void PrepareAssemblyGeometry(GameObject root, UpperDestructionV2 upper, ShipDestruction damage)
        {
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            if (container == null) { container = ScriptableObject.CreateInstance<UpperDebrisGeometry>(); AssetDatabase.CreateAsset(container, Folder + "/DebrisGeometry.asset"); }
            var definitions = damage.Profile.Sections.ToDictionary(s => s.SectionId);
            foreach (var assembly in upper.Assemblies.Where(a => a.Kind is UpperAssemblyKind.ForeMast or UpperAssemblyKind.MizzenMast))
                assembly.Sections = assembly.Sections.Where(s => !DeckRig(definitions[s.SectionId].SourceGroup)).ToArray();
            var helm = upper.Assemblies.First(a => a.Kind == UpperAssemblyKind.Helm);
            var mount = helm.Sections.OrderBy(s => ShapeBounds(root.transform, s.Intact.transform, s.Intact.GetComponent<MeshFilter>().sharedMesh.bounds).size.x).First();
            mount.Indestructible = true;
            helm.Sections = helm.Sections.Where(s => s != mount).ToArray();
            var changed = new HashSet<MeshRenderer>();
            var changedSections = new HashSet<int>();
            var nodes = damage.Profile.Structure.ToList();
            foreach (var section in damage.Sections)
            {
                var definition = definitions[section.SectionId];
                if (section.Indestructible || definition.Type is ShipSectionType.Hull or ShipSectionType.Rudder || definition.SourceGroup.StartsWith("BilgeBeam_", StringComparison.Ordinal) || section.Fragments.Length == 0) continue;
                var intact = section.Intact.GetComponent<MeshFilter>();
                var bounds = intact.sharedMesh.bounds;
                var expanded = bounds; expanded.Expand(.0002f);
                bool wheel = helm.Sections.Contains(section);
                bool capstan = definition.Type == ShipSectionType.Capstan;
                bool barrel = definition.SourceGroup.Contains("Dice_Game_Barrel");
                bool railing = definition.Type == ShipSectionType.Railing;
                bool invalid = section.Fragments.Any(g => { var f = g.GetComponent<MeshFilter>(); return !expanded.Contains((intact.transform.worldToLocalMatrix * f.transform.localToWorldMatrix).MultiplyPoint3x4(f.sharedMesh.bounds.center)); });
                if (!invalid && !wheel && !capstan && !barrel && !railing) continue;
                bool drum = capstan && definition.SourceGroup.Contains("Rotating_Drum");
                int count = barrel ? 9 : wheel ? 3 : drum ? 18 : section.Fragments.Length;
                var fragments = section.Fragments.ToList();
                while (fragments.Count < count)
                {
                    var copy = UnityEngine.Object.Instantiate(fragments[0], fragments[0].transform.parent);
                    copy.name = "UpperFragment_" + section.SectionId + "_" + fragments.Count;
                    fragments.Add(copy);
                    var existing = nodes.First(n => n.SectionId == section.SectionId);
                    nodes.Add(new ShipFragmentConnection { SectionId = section.SectionId, Fragment = fragments.Count - 1, LoadBearing = existing.LoadBearing, Neighbours = Array.Empty<int>() });
                }
                section.Fragments = fragments.ToArray();
                changedSections.Add(section.SectionId);
                int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 : bounds.size.y >= bounds.size.z ? 1 : 2;
                if (barrel)
                {
                    var up = intact.transform.InverseTransformDirection(root.transform.up);
                    axis = Mathf.Abs(up.x) >= Mathf.Abs(up.y) && Mathf.Abs(up.x) >= Mathf.Abs(up.z) ? 0 : Mathf.Abs(up.y) >= Mathf.Abs(up.z) ? 1 : 2;
                }
                if (capstan && !definition.SourceGroup.Contains("Beam")) axis = bounds.size.x >= bounds.size.y ? 0 : 1;
                int normalAxis = bounds.size.x <= bounds.size.y && bounds.size.x <= bounds.size.z ? 0 : bounds.size.y <= bounds.size.z ? 1 : 2;
                for (int bit = 0; bit < count; bit++)
                {
                    var fragment = fragments[bit]; var filter = fragment.GetComponent<MeshFilter>();
                    var slice = bounds;
                    var min = bounds.min; var max = bounds.max;
                    min[axis] = Mathf.Lerp(bounds.min[axis], bounds.max[axis], bit / (float)count);
                    max[axis] = Mathf.Lerp(bounds.min[axis], bounds.max[axis], (bit + 1f) / count);
                    slice.SetMinMax(min, max);
                    Mesh mesh;
                    if (barrel)
                    {
                        float lid = Mathf.Lerp(bounds.min[axis], bounds.max[axis], .87f);
                        mesh = UpperMeshCut.Band(intact.sharedMesh, axis, bit == 0 ? lid : float.NegativeInfinity, bit == 0 ? float.PositiveInfinity : lid, .00008f);
                        if (bit > 0)
                        {
                            var sector = UpperMeshCut.Sector(mesh, axis, bit - 1, 8);
                            UnityEngine.Object.DestroyImmediate(mesh); mesh = sector;
                        }
                        upper.DiceTopSection = section; upper.DiceTopFragment = 0;
                    }
                    else if (wheel) mesh = UpperMeshCut.Sector(intact.sharedMesh, normalAxis, bit);
                    else if (drum)
                    {
                        mesh = UnityEngine.Object.Instantiate(intact.sharedMesh);
                        var cells = new[] { bit % 3, bit / 3 % 3, bit / 9 };
                        var sizes = new[] { 3, 3, 2 };
                        for (int cutAxis = 0; cutAxis < 3; cutAxis++)
                        {
                            float low = cells[cutAxis] == 0 ? float.NegativeInfinity : Mathf.Lerp(bounds.min[cutAxis], bounds.max[cutAxis], cells[cutAxis] / (float)sizes[cutAxis]);
                            float high = cells[cutAxis] == sizes[cutAxis] - 1 ? float.PositiveInfinity : Mathf.Lerp(bounds.min[cutAxis], bounds.max[cutAxis], (cells[cutAxis] + 1f) / sizes[cutAxis]);
                            var next = UpperMeshCut.Band(mesh, cutAxis, low, high, .00012f);
                            UnityEngine.Object.DestroyImmediate(mesh); mesh = next;
                        }
                    }
                    else mesh = UpperMeshCut.Band(intact.sharedMesh, axis, bit == 0 ? float.NegativeInfinity : min[axis], bit == count - 1 ? float.PositiveInfinity : max[axis], .00016f);
                    mesh.name = "Aligned_" + section.SectionId + "_" + bit;
                    fragment.transform.SetPositionAndRotation(intact.transform.position, intact.transform.rotation);
                    fragment.transform.localScale = Vector3.Scale(fragment.transform.localScale, new Vector3(intact.transform.lossyScale.x / fragment.transform.lossyScale.x, intact.transform.lossyScale.y / fragment.transform.lossyScale.y, intact.transform.lossyScale.z / fragment.transform.lossyScale.z));
                    filter.sharedMesh = SavePreparedMesh(mesh, container);
                    var renderer = fragment.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = intact.GetComponent<MeshRenderer>().sharedMaterials.Take(intact.sharedMesh.subMeshCount).Concat(new[] { damage.Profile.SplinterMaterial }).ToArray();
                    foreach (var collider in fragment.GetComponents<MeshCollider>()) collider.sharedMesh = filter.sharedMesh;
                    changed.Add(renderer);
                }
                var ids = Enumerable.Range(0, nodes.Count).Where(i => nodes[i].SectionId == section.SectionId).ToArray();
                foreach (int id in ids) nodes[id].Neighbours = nodes[id].Neighbours.Concat(ids.Where(i => i != id)).Distinct().ToArray();
            }
            var sections = damage.Sections.ToDictionary(s => s.SectionId);
            var nodeBounds = nodes.Select(n => ShapeBounds(root.transform, sections[n.SectionId].RepairTransform(n.Fragment), sections[n.SectionId].RepairBounds(n.Fragment))).ToArray();
            for (int id = 0; id < nodes.Count; id++)
            {
                var node = nodes[id];
                node.Neighbours = node.Neighbours.Where(other => {
                    if (!changedSections.Contains(node.SectionId) && !changedSections.Contains(nodes[other].SectionId)) return true;
                    var a = nodeBounds[id]; var b = nodeBounds[other];
                    var p = a.ClosestPoint(b.center); var q = b.ClosestPoint(p);
                    return (p - q).sqrMagnitude <= .0064f;
                }).ToArray();
            }
            damage.Profile.Structure = nodes.ToArray();
            ConnectIntactStructure(root, upper, damage);
            foreach (var batch in root.GetComponentsInChildren<ShipV3RenderBatch>(true))
            {
                if (!batch.Sources.Any(changed.Contains)) continue;
                var mesh = ShipV3RenderBatch.BuildMesh(batch.Sources, batch.SharedMaterial, batch.transform, true);
                mesh.name = "UpperAlignedBatch_" + batch.name;
                batch.CachedMesh = SavePreparedMesh(mesh, container);
            }
            EditorUtility.SetDirty(container);
        }
    }
}
