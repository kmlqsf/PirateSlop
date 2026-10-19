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
        static Bounds ShapeBounds(Transform root, Transform source, Bounds bounds)
        {
            var matrix = root.worldToLocalMatrix * source.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        static void PrepareSupportBindings(GameObject root, UpperDestructionV2 upper, ShipDestruction damage)
        {
            var definitions = damage.Profile.Sections.ToDictionary(s => s.SectionId);
            var structure = damage.Profile.Structure
                .Select(n => (Node: n, Bounds: ShapeBounds(root.transform, damage.Sections.First(s => s.SectionId == n.SectionId).RepairTransform(n.Fragment),
                    damage.Sections.First(s => s.SectionId == n.SectionId).RepairBounds(n.Fragment)))).ToArray();
            ShipFragmentConnection[] Floor(Vector3 point) => structure.Where(n => definitions[n.Node.SectionId].Type == ShipSectionType.Deck && n.Bounds.max.y < point.y + .3f)
                .OrderBy(n => (n.Bounds.ClosestPoint(point) - point).sqrMagnitude).Take(2).Select(n => n.Node).ToArray();
            var feature = root.GetComponent<ShipV3Features>();
            var shadowRenderers = new HashSet<Renderer>(root.GetComponentsInChildren<ShipV3RenderBatch>(true).Where(b => b.ShadowProxy != null).Select(b => b.ShadowProxy));
            var links = feature.Attachments.Where(a => a.Object != null).Select(a => new UpperAttachmentLink {
                Source = a.Object, Supports = a.Supports ?? Array.Empty<ShipV3Support>(), Dependencies = a.Dependencies ?? Array.Empty<Transform>() }).ToList();
            for (int i = 0; i < upper.Assemblies.Length; i++)
            {
                var assembly = upper.Assemblies[i];
                foreach (var link in links)
                    if (assembly.Attachments.Contains(link.Source)) { link.Assembly = i; link.Protected = assembly.Kind == UpperAssemblyKind.MainMast; }
                var bounds = new Bounds(); bool any = false;
                foreach (var section in assembly.Sections)
                {
                    var filter = section.Intact.GetComponent<MeshFilter>();
                    var part = ShapeBounds(root.transform, filter.transform, filter.sharedMesh.bounds);
                    if (!any) { bounds = part; any = true; } else bounds.Encapsulate(part);
                }
                if (!any) continue;
                if (assembly.Kind is UpperAssemblyKind.ForeMast or UpperAssemblyKind.MizzenMast or UpperAssemblyKind.MainMast)
                {
                    var stem = assembly.Sections.First(s => definitions[s.SectionId].SourceGroup == "V3_Mast_" + assembly.Key);
                    var stemFilter = stem.Intact.GetComponent<MeshFilter>();
                    var stemBounds = ShapeBounds(root.transform, stemFilter.transform, stemFilter.sharedMesh.bounds);
                    assembly.BaseSections = assembly.Sections.Where(s => definitions[s.SectionId].Type == ShipSectionType.Mast && s != stem &&
                        ShapeBounds(root.transform, s.Intact.transform, s.Intact.GetComponent<MeshFilter>().sharedMesh.bounds).max.y <= stemBounds.min.y + .3f).ToArray();
                    foreach (var section in assembly.BaseSections) section.Indestructible = true;
                    assembly.CollapsePivot = new Vector3(stemBounds.center.x, stemBounds.min.y, stemBounds.center.z);
                    var foot = assembly.BaseSections.Length > 0 ? assembly.BaseSections[0].Intact.GetComponent<MeshFilter>() : stemFilter;
                    var footBounds = ShapeBounds(root.transform, foot.transform, foot.sharedMesh.bounds);
                    assembly.RepairBounds = new Bounds(new Vector3(footBounds.center.x, footBounds.max.y + .15f, footBounds.center.z), new Vector3(1.5f, 1.4f, 1.5f));
                    foreach (var node in Floor(new Vector3(footBounds.center.x, footBounds.min.y + .2f, footBounds.center.z)))
                    {
                        var section = damage.Sections.First(s => s.SectionId == node.SectionId);
                        section.ProtectedFragments |= 1UL << node.Fragment;
                        node.Anchor = true;
                    }
                }
                else
                {
                    bounds.Expand(.16f);
                    assembly.RepairBounds = bounds;
                    assembly.DeckSupports = Floor(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                    var handles = root.GetComponentsInChildren<ShipControlHandle>(true).Where(h => assembly.Kind == UpperAssemblyKind.Helm ? h.Helm != null : assembly.Kind == UpperAssemblyKind.Capstan ? h.Capstan != null : assembly.Kind == UpperAssemblyKind.SailControl && h.Sails != null && h.RopeIndex == assembly.SailIndex).ToArray();
                    var controls = handles.SelectMany(h => h.GetComponents<Collider>()).ToList();
                    foreach (var handle in handles.Where(h => !handles.Any(other => other != h && other.transform.IsChildOf(h.transform))))
                    {
                        if (!assembly.Sections.Any(s => handle.transform == s.Intact.transform || handle.transform.IsChildOf(s.Intact.transform))) continue;
                        var node = new GameObject("UpperControl_" + assembly.Key + "_" + handle.name);
                        node.transform.SetParent(root.transform, false);
                        node.transform.SetPositionAndRotation(handle.transform.position, handle.transform.rotation);
                        node.transform.localScale = new Vector3(handle.transform.lossyScale.x / root.transform.lossyScale.x, handle.transform.lossyScale.y / root.transform.lossyScale.y, handle.transform.lossyScale.z / root.transform.lossyScale.z);
                        var copy = node.AddComponent<ShipControlHandle>(); EditorUtility.CopySerialized(handle, copy);
                        var collider = node.AddComponent<BoxCollider>();
                        var source = handle.GetComponent<Collider>();
                        if (source is BoxCollider box) { collider.center = box.center; collider.size = box.size; }
                        else if (source is SphereCollider sphere) { collider.center = sphere.center; collider.size = Vector3.one * sphere.radius * 2f; }
                        else if (source is MeshCollider mesh && mesh.sharedMesh != null) { collider.center = mesh.sharedMesh.bounds.center; collider.size = mesh.sharedMesh.bounds.size; }
                        else { collider.center = Vector3.zero; collider.size = Vector3.one * .3f; }
                        controls.Add(collider);
                        assembly.Sections[0].DamageColliders = assembly.Sections[0].DamageColliders.Append(collider).ToArray();
                        links.Add(new UpperAttachmentLink { Source = node.transform, Assembly = i });
                    }
                    assembly.Controls = controls.Distinct().ToArray();
                }
            }
            foreach (var link in links)
            {
                if (link.Source.name == "V9_Anchor_Chain") link.Assembly = Array.FindIndex(upper.Assemblies, a => a.Kind == UpperAssemblyKind.Capstan);
                if (link.Assembly >= 0) continue;
                var owners = link.Supports.SelectMany(s => s.Sections).Where(s => s != null)
                    .Select(s => Array.FindIndex(upper.Assemblies, a => a.Sections.Contains(s))).Where(i => i >= 0).Distinct().ToArray();
                if (owners.Length == 1 && !upper.Assemblies[owners[0]].Key.Equals("Main")) link.Assembly = owners[0];
            }
            var covered = new HashSet<Transform>();
            foreach (var s in damage.Sections)
                foreach (var visual in new[] { s.Intact, s.Damaged, s.Critical, s.Destroyed, s.Repaired }.Concat(s.Fragments))
                    if (visual != null) foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) covered.Add(renderer.transform);
            foreach (var link in links) foreach (var renderer in link.Source.GetComponentsInChildren<Renderer>(true)) covered.Add(renderer.transform);
            var handled = new HashSet<Transform>(links.Select(a => a.Source));
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (covered.Contains(renderer.transform) || renderer is LineRenderer || renderer is ParticleSystemRenderer || renderer.GetComponent<ShipV3RenderBatch>() != null) continue;
                var monkey = root.GetComponent<ShipMonkey>();
                if (monkey != null && monkey.Visual != null && renderer.transform.IsChildOf(monkey.Visual) || renderer.GetComponentInParent<Networking.NetworkFish>() != null) continue;
                if (shadowRenderers.Contains(renderer)) continue;
                Transform source = renderer.transform;
                for (var parent = source.parent; parent != null && parent != root.transform; parent = parent.parent)
                    if (parent.name is "DiceInteraction" or "AnimatedAnchorChain" or "ShipNameplate" or "SlotMachine" or "DispenserLeverLength") { source = parent; break; }
                if (!handled.Add(source)) continue;
                int owner = source.name == "DiceInteraction" ? Array.FindIndex(upper.Assemblies, a => a.Kind == UpperAssemblyKind.Dice) :
                    MastKey(source.name) is { } key ? Array.FindIndex(upper.Assemblies, a => a.Key == key) : -1;
                if (source.name == "AnimatedAnchorChain")
                {
                    var chain = feature.Attachments.FirstOrDefault(a => a.Object != null && a.Object.name == "V9_Anchor_Chain");
                    links.Add(new UpperAttachmentLink { Source = source, Dependencies = chain != null ? new[] { chain.Object } : Array.Empty<Transform>() });
                    continue;
                }
                var point = root.transform.InverseTransformPoint(renderer.bounds.center);
                if (point.y < 4.5f && owner < 0) continue;
                var bounds = renderer is SkinnedMeshRenderer skin ? ShapeBounds(root.transform, renderer.transform, skin.localBounds) :
                    renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null ? ShapeBounds(root.transform, renderer.transform, filter.sharedMesh.bounds) : new Bounds(point, Vector3.one * .1f);
                var supports = structure.Select(n => (n.Node, Distance: (n.Bounds.ClosestPoint(bounds.ClosestPoint(n.Bounds.center)) - bounds.ClosestPoint(n.Bounds.center)).sqrMagnitude))
                    .OrderBy(n => n.Distance).Take(2).Select(n => n.Node).ToArray();
                links.Add(new UpperAttachmentLink { Source = source, Assembly = owner, Protected = owner >= 0 && upper.Assemblies[owner].Kind == UpperAssemblyKind.MainMast,
                    Supports = supports.Length > 0 ? new[] { new ShipV3Support { Sections = supports.Select(n => damage.Sections.First(s => s.SectionId == n.SectionId)).ToArray(), Fragments = supports.Select(n => n.Fragment).ToArray() } } : Array.Empty<ShipV3Support>() });
                covered.UnionWith(source.GetComponentsInChildren<Renderer>(true).Select(r => r.transform));
            }
            var ordered = new List<UpperAttachmentLink>();
            while (links.Count > 0)
            {
                int index = links.FindIndex(l => !links.Any(other => other != l && (l.Dependencies.Any(d => d == other.Source || d != null && d.IsChildOf(other.Source)) || l.Source.IsChildOf(other.Source))));
                if (index < 0) throw new InvalidOperationException("Attachment support dependency cycle.");
                ordered.Add(links[index]); links.RemoveAt(index);
            }
            upper.AttachmentLinks = ordered.ToArray();
            for (int i = 0; i < upper.Assemblies.Length; i++)
                upper.Assemblies[i].Attachments = ordered.Where(l => l.Assembly == i).Select(l => l.Source).ToArray();
        }
    }
}
