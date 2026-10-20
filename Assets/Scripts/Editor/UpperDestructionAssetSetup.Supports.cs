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
            var sectionLookup = damage.Sections.ToDictionary(s => s.SectionId);
            var structure = damage.Profile.Structure
                .Select(n => (Node: n, Bounds: ShapeBounds(root.transform, damage.Sections.First(s => s.SectionId == n.SectionId).RepairTransform(n.Fragment),
                    damage.Sections.First(s => s.SectionId == n.SectionId).RepairBounds(n.Fragment)))).ToArray();
            ShipFragmentConnection[] Floor(Vector3 point) => structure.Where(n => definitions[n.Node.SectionId].Type == ShipSectionType.Deck && n.Bounds.max.y < point.y + .3f)
                .OrderBy(n => (n.Bounds.ClosestPoint(point) - point).sqrMagnitude).Take(2).Select(n => n.Node).ToArray();
            var feature = root.GetComponent<ShipV3Features>();
            var shadowRenderers = new HashSet<Renderer>(root.GetComponentsInChildren<ShipV3RenderBatch>(true).Where(b => b.ShadowProxy != null).Select(b => b.ShadowProxy));
            var links = feature.Attachments.Where(a => a.Object != null).Select(a => new UpperAttachmentLink {
                Source = a.Object, Supports = a.Supports ?? Array.Empty<ShipV3Support>(), Dependencies = a.Dependencies ?? Array.Empty<Transform>() }).ToList();
            foreach (var link in links)
                foreach (var support in link.Supports)
                {
                    var filters = link.Source.GetComponentsInChildren<MeshFilter>(true);
                    var point = filters.Length > 0 ? ShapeBounds(root.transform, filters[0].transform, filters[0].sharedMesh.bounds).center : root.transform.InverseTransformPoint(link.Source.position);
                    var nearest = structure.Where(n => support.Sections.Contains(sectionLookup[n.Node.SectionId]))
                        .OrderBy(n => n.Bounds.SqrDistance(point)).Take(2).ToArray();
                    support.Sections = nearest.Select(n => damage.Sections.First(s => s.SectionId == n.Node.SectionId)).ToArray();
                    support.Fragments = nearest.Select(n => n.Node.Fragment).ToArray();
                }
            for (int i = 0; i < upper.Assemblies.Length; i++)
            {
                var assembly = upper.Assemblies[i];
                foreach (var link in links)
                    if (assembly.Attachments.Contains(link.Source) && (assembly.Kind == UpperAssemblyKind.MainMast || !DeckRig(link.Source.name))) { link.Assembly = i; link.Protected = assembly.Kind == UpperAssemblyKind.MainMast; }
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
                    var grips = new List<UpperGripBinding>();
                    foreach (var handle in handles.Where(h => !handles.Any(other => other != h && other.transform.IsChildOf(h.transform))))
                    {
                        if (!assembly.Sections.Any(s => handle.transform == s.Intact.transform || handle.transform.IsChildOf(s.Intact.transform))) continue;
                        var node = new GameObject("UpperControl_" + assembly.Key + "_" + handle.name);
                        node.transform.SetParent(root.transform, false);
                        node.transform.SetPositionAndRotation(handle.transform.position, handle.transform.rotation);
                        node.transform.localScale = new Vector3(handle.transform.lossyScale.x / root.transform.lossyScale.x, handle.transform.lossyScale.y / root.transform.lossyScale.y, handle.transform.lossyScale.z / root.transform.lossyScale.z);
                        var copy = node.AddComponent<ShipControlHandle>(); EditorUtility.CopySerialized(handle, copy);
                        var collider = node.AddComponent<MeshCollider>();
                        var source = handle.GetComponent<Collider>();
                        var section = assembly.Sections.FirstOrDefault(s => handle.transform == s.Intact.transform || handle.transform.IsChildOf(s.Intact.transform));
                        node.transform.SetParent(section.Intact.transform.parent, true);
                        var filter = section.Intact.GetComponent<MeshFilter>();
                        var geometry = new Mesh { name = "Grip_" + node.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        geometry.CombineMeshes(Enumerable.Range(0, filter.sharedMesh.subMeshCount).Select(sub => new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = node.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix }).ToArray(), true, true);
                        collider.sharedMesh = SavePreparedMesh(geometry, AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset"));
                        collider.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
                        grips.Add(new UpperGripBinding { Collider = collider, Section = section });
                        controls.Add(collider);
                        assembly.Sections[0].DamageColliders = assembly.Sections[0].DamageColliders.Append(collider).ToArray();
                        links.Add(new UpperAttachmentLink { Source = node.transform, Assembly = i });
                    }
                    assembly.Controls = controls.Distinct().ToArray();
                    assembly.Grips = grips.ToArray();
                }
            }
            foreach (var link in links)
            {
                for (int sail = 0; sail < sailTags.Length; sail++)
                    if (link.Source.name.Contains("_Control_" + sailTags[sail] + "_"))
                    {
                        link.Assembly = Array.FindIndex(upper.Assemblies, a => a.Kind == UpperAssemblyKind.SailControl && a.SailIndex == sail);
                        link.Protected = false;
                        if (link.Source.name.Contains("YardLashing"))
                        {
                            link.RequiredAssembly = link.Assembly;
                            link.Assembly = Array.FindIndex(upper.Assemblies, a => a.Key == (sail < 2 ? "Main" : sail < 4 ? "Fore" : "Mizzen"));
                        }
                    }
                if (link.Source.name == "V9_Anchor_Chain") link.Assembly = Array.FindIndex(upper.Assemblies, a => a.Kind == UpperAssemblyKind.Capstan);
                if (link.Assembly >= 0) continue;
                var owners = link.Supports.SelectMany(s => s.Sections).Where(s => s != null)
                    .Select(s => Array.FindIndex(upper.Assemblies, a => a.Sections.Contains(s))).Where(i => i >= 0).Distinct().ToArray();
                if (owners.Length == 1 && !upper.Assemblies[owners[0]].Key.Equals("Main")) link.Assembly = owners[0];
            }
            var helmMount = damage.Sections.First(s => definitions[s.SectionId].Type == ShipSectionType.Helm && s.Indestructible);
            for (int sail = 0; sail < sailTags.Length; sail++)
            {
                int control = Array.FindIndex(upper.Assemblies, a => a.Kind == UpperAssemblyKind.SailControl && a.SailIndex == sail);
                foreach (var section in damage.Sections.Where(s => definitions[s.SectionId].SourceGroup.Contains("_Control_" + sailTags[sail] + "_") && definitions[s.SectionId].SourceGroup.EndsWith("_UpperBlock", StringComparison.Ordinal)))
                    links.Add(new UpperAttachmentLink { Source = section.transform, RequiredAssembly = control, Assembly = Array.FindIndex(upper.Assemblies, a => a.Sections.Contains(section)) });
            }
            var mountBounds = ShapeBounds(root.transform, helmMount.Intact.transform, helmMount.Intact.GetComponent<MeshFilter>().sharedMesh.bounds);
            var floor = Floor(new Vector3(mountBounds.center.x, mountBounds.min.y + .1f, mountBounds.center.z)).Take(1).ToArray();
            links.Add(new UpperAttachmentLink { Source = helmMount.Intact.transform, Supports = new[] { new ShipV3Support { Sections = floor.Select(n => damage.Sections.First(s => s.SectionId == n.SectionId)).ToArray(), Fragments = floor.Select(n => n.Fragment).ToArray() } } });
            upper.Assemblies.First(a => a.Kind == UpperAssemblyKind.Helm).DeckSupports = floor;
            foreach (var section in damage.Sections.Where(s => DeckRig(definitions[s.SectionId].SourceGroup) && MastKey(definitions[s.SectionId].SourceGroup) is "Fore" or "Mizzen"))
            {
                var own = structure.Where(n => n.Node.SectionId == section.SectionId).ToArray();
                foreach (var part in own)
                {
                    var support = structure.Where(n => (definitions[n.Node.SectionId].Type is ShipSectionType.Deck or ShipSectionType.Hull) && n.Node.SectionId != section.SectionId)
                        .OrderBy(n => n.Bounds.SqrDistance(part.Bounds.center)).First();
                    part.Node.Neighbours = own.Where(n => n.Node != part.Node).Select(n => Array.IndexOf(damage.Profile.Structure, n.Node)).Append(Array.IndexOf(damage.Profile.Structure, support.Node)).ToArray();
                    int id = Array.IndexOf(damage.Profile.Structure, part.Node);
                    support.Node.Neighbours = support.Node.Neighbours.Append(id).Distinct().ToArray();
                }
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
                    links.Add(new UpperAttachmentLink { Source = source, Assembly = Array.FindIndex(upper.Assemblies, a => a.Kind == UpperAssemblyKind.Capstan), Dependencies = chain != null ? new[] { chain.Object } : Array.Empty<Transform>() });
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
            foreach (var link in links.Where(l => !l.Protected && (IsFlexible(l.Source.name) || l.Source.name.Contains("Chain"))))
            {
                var filter = link.Source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) continue;
                var points = filter.sharedMesh.vertices.Select(p => root.transform.InverseTransformPoint(filter.transform.TransformPoint(p))).ToArray();
                var bounds = ShapeBounds(root.transform, filter.transform, filter.sharedMesh.bounds);
                int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 : bounds.size.y >= bounds.size.z ? 1 : 2;
                if (bounds.size[axis] < .8f) continue;
                var endpoints = new[] { points.OrderBy(p => p[axis]).First(), points.OrderByDescending(p => p[axis]).First() };
                var anchors = new List<ShipV3Support>();
                foreach (var point in endpoints)
                {
                    var nearest = structure.OrderBy(n => n.Bounds.SqrDistance(point)).Take(2).ToArray();
                    if (nearest.Length == 0 || nearest[0].Bounds.SqrDistance(point) > .25f) continue;
                    anchors.Add(new ShipV3Support { Sections = nearest.Select(n => sectionLookup[n.Node.SectionId]).ToArray(), Fragments = nearest.Select(n => n.Node.Fragment).ToArray() });
                }
                if (anchors.Count == 2) link.Supports = anchors.ToArray();
            }
            var bobstay = links.FirstOrDefault(l => l.Source.name == "V4_Bobstay_Chain");
            upper.ProtectedDispenser = null;
            if (feature.DispenserMouth != null)
            {
                var protectedRoot = new GameObject("UpperDispenser").transform;
                protectedRoot.SetParent(root.transform, false);
                var body = feature.DispenserMouth.parent;
                body.SetParent(protectedRoot, true);
                if (feature.DispenserLever != null && !feature.DispenserLever.IsChildOf(protectedRoot))
                {
                    var lever = feature.DispenserLever;
                    for (var parent = lever.parent; parent != null && parent != root.transform; parent = parent.parent)
                        if (parent.name.Contains("Dispenser")) lever = parent;
                    lever.SetParent(protectedRoot, true);
                }
                upper.ProtectedDispenser = protectedRoot;
            }
            if (upper.ProtectedDispenser != null)
            {
                var wall = structure.Where(n => definitions[n.Node.SectionId].SourceGroup.StartsWith("V6_Hold_Bulkhead_Board_", StringComparison.Ordinal)).ToArray();
                if (wall.Length == 0) throw new InvalidOperationException("The dispenser bulkhead supports are missing.");
                links.RemoveAll(l => l.Source == upper.ProtectedDispenser || l.Source.IsChildOf(upper.ProtectedDispenser));
                links.Add(new UpperAttachmentLink { Source = upper.ProtectedDispenser, Supports = new[] { new ShipV3Support { Sections = wall.Select(n => sectionLookup[n.Node.SectionId]).ToArray(), Fragments = wall.Select(n => n.Node.Fragment).ToArray() } } });
            }
            foreach (var link in links.Where(l => l.Source.name.StartsWith("V9_Anchor_", StringComparison.Ordinal))) link.Assembly = Array.FindIndex(upper.Assemblies, a => a.Kind == UpperAssemblyKind.Capstan);
            if (bobstay != null)
            {
                bobstay.Dependencies = links.Where(l => l.Source.name is "V4_Bobstay_Spar_Shackle" or "V4_Bobstay_Stem_Shackle").Select(l => l.Source).ToArray();
                var tabs = links.FirstOrDefault(l => l.Source.name == "V4_Bobstay_MountTabs");
                if (tabs != null) tabs.Dependencies = new[] { bobstay.Source };
            }
            if (upper.DiceTopSection != null)
            {
                var top = new ShipV3Support { Sections = new[] { upper.DiceTopSection }, Fragments = new[] { upper.DiceTopFragment } };
                foreach (var slot in feature.DiceSlots)
                {
                    foreach (var die in slot.Dice)
                    {
                        links.RemoveAll(l => l.Source == die.transform || l.Source.IsChildOf(die.transform));
                        links.Add(new UpperAttachmentLink { Source = die.transform, Protected = true });
                    }
                    var cup = slot.Cup.transform.parent;
                    var handles = cup.parent.GetComponentsInChildren<MeshFilter>(true).Where(f => !f.transform.IsChildOf(cup) && !slot.Dice.Any(d => d != null && (f.transform == d.transform || f.transform.IsChildOf(d.transform)))).Select(f => f.transform).ToArray();
                    foreach (var source in handles.Prepend(cup))
                    {
                        links.RemoveAll(l => l.Source == source || l.Source.IsChildOf(source));
                        links.Add(new UpperAttachmentLink { Source = source, Supports = new[] { top } });
                    }
                }
                if (feature.DiceTable != null)
                {
                    links.RemoveAll(l => l.Source == feature.DiceTable);
                    links.Add(new UpperAttachmentLink { Source = feature.DiceTable, Supports = new[] { top } });
                }
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
