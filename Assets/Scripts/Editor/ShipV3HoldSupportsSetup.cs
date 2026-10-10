using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PirateSlop.Ships;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipV3HoldSupportsSetup
    {
        public static void Apply(GameObject root, Dictionary<string, Transform> sources, JObject manifest)
        {
            if (manifest["supports"] == null) return;
            var destruction = root.GetComponent<ShipDestruction>();
            var profile = destruction.Profile;
            var features = root.GetComponent<ShipV3Features>();
            var template = features.Lanterns[0];
            var mountTemplate = template.Grip.parent.parent;
            var bracketTemplate = root.GetComponentsInChildren<MeshFilter>(true).First(x => x.name == "V3_Lamp_Bow_Port_Bracket");
            var oldIds = profile.Sections.Where(x => x.SourceGroup != null && x.SourceGroup.StartsWith("BilgeBeam_")).ToDictionary(x => x.SourceGroup, x => x.SectionId);
            var oldSections = new HashSet<int>(oldIds.Values);
            var sections = destruction.Sections.Where(x => x != null && !oldSections.Contains(x.SectionId)).ToList();
            var definitions = profile.Sections.Where(x => !oldSections.Contains(x.SectionId)).ToList();
            var structure = profile.Structure.Where(x => !oldSections.Contains(x.SectionId)).ToList();
            var lanterns = features.Lanterns.Where(x => x.Grip != null && !x.Grip.name.StartsWith("BilgeLamp_")).ToList();
            var bodies = new List<Rigidbody>();
            var pendulums = new List<bool>();
            for (int i = 0; i < features.PhysicsBodies.Length; i++)
                if (features.PhysicsBodies[i] != null && !features.PhysicsBodies[i].name.StartsWith("BilgeLamp_"))
                { bodies.Add(features.PhysicsBodies[i]); pendulums.Add(features.Pendulums[i]); }
            var attachments = features.Attachments.Where(x => x.Object != null && !x.Object.name.StartsWith("BilgeLamp_")).ToList();
            var previous = root.transform.Find("BilgeDestructible");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var container = new GameObject("BilgeDestructible").transform;
            container.SetParent(root.transform, false);
            int nextId = Mathf.Max(profile.Sections.Max(x => x.SectionId), oldIds.Count > 0 ? oldIds.Values.Max() : 0) + 1;
            foreach (var entry in manifest["supports"])
            {
                string key = (string)entry["name"], lampName = (string)entry["lamp"];
                int id = oldIds.TryGetValue(key, out int priorId) ? priorId : nextId++;
                var beam = new GameObject(key);
                beam.transform.SetParent(container, false);
                var section = beam.AddComponent<ShipDamageSection>();
                section.SectionId = id;
                section.SafeColliderReplacement = true;
                section.LazyFragmentColliders = true;
                section.Intact = MeshObject(sources[(string)entry["intact"]], beam.transform);
                var collider = section.Intact.AddComponent<MeshCollider>();
                collider.sharedMesh = section.Intact.GetComponent<MeshFilter>().sharedMesh;
                section.DamageColliders = section.GameplayColliders = new Collider[] { collider };
                section.Fragments = entry["fragments"].Values<string>().Select(n => MeshObject(sources[n], beam.transform)).ToArray();
                foreach (var fragment in section.Fragments) fragment.SetActive(false);
                sections.Add(section);
                var bounds = section.Intact.GetComponent<MeshFilter>().sharedMesh.bounds;
                definitions.Add(new ShipSectionDefinition { SectionId = id, Name = key, SourceGroup = key, Type = ShipSectionType.Mast,
                    MaxHealth = 180f, Repairable = true, BreachArea = 0f, BreachAnchor = bounds.center,
                    DebrisMass = 35f, MaxDebris = 4, DebrisImpulse = 4f });
                int first = structure.Count;
                for (int i = 0; i < section.Fragments.Length; i++)
                    structure.Add(new ShipFragmentConnection { SectionId = id, Fragment = i, Anchor = i < 2,
                        Neighbours = Enumerable.Range(0, section.Fragments.Length).Where(n => n != i && (n / 2 == i / 2 || n % 2 == i % 2 && Mathf.Abs(n / 2 - i / 2) == 1)).Select(n => first + n).ToArray() });
                var support = new ShipV3Support { Sections = Enumerable.Repeat(section, section.Fragments.Length).ToArray(),
                    Fragments = Enumerable.Range(0, section.Fragments.Length).ToArray() };
                var mount = UnityEngine.Object.Instantiate(mountTemplate.gameObject, container).transform;
                mount.name = lampName + "_Mount";
                mount.position = sources[mount.name].position;
                foreach (var node in mount.GetComponentsInChildren<Transform>(true))
                {
                    if (node == mount) continue;
                    node.name = node.name.Replace("V3_Lamp_Bow_Port", lampName);
                    if (node.GetComponent<MeshFilter>() != null) CopyGeometry(sources[node.name], node);
                    if (node.GetComponent<Light>() != null) node.position = sources[node.name].position;
                }
                var pivot = mount.Find(lampName + "_Pivot");
                var body = pivot.GetComponent<Rigidbody>();
                var joint = pivot.GetComponent<ConfigurableJoint>();
                joint.connectedBody = root.GetComponent<Rigidbody>();
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = root.transform.InverseTransformPoint(pivot.position);
                var glass = pivot.GetComponentInChildren<MeshRenderer>(true);
                body.centerOfMass = pivot.InverseTransformPoint(glass.bounds.center);
                var gripCollider = glass.GetComponent<BoxCollider>();
                gripCollider.center = glass.GetComponent<MeshFilter>().sharedMesh.bounds.center;
                gripCollider.size = glass.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                var target = glass.GetComponent<ShipV3InteractionTarget>();
                target.Ship = features;
                target.Index = lanterns.Count;
                target.Kind = ShipV3TargetKind.Lantern;
                var light = pivot.GetComponentInChildren<Light>(true);
                lanterns.Add(new ShipV3Lantern { Grip = glass.transform, Glass = glass, GlassSlot = template.GlassSlot, Light = light });
                bodies.Add(body);pendulums.Add(true);
                attachments.Add(new ShipV3Attachment { Object = pivot, Supports = new[] { support }, Dependencies = Array.Empty<Transform>(), Fall = true });
                var bracket = UnityEngine.Object.Instantiate(bracketTemplate.gameObject, mount).transform;
                bracket.name = lampName + "_Bracket";
                CopyGeometry(sources[bracket.name], bracket);
                attachments.Add(new ShipV3Attachment { Object = bracket, Supports = new[] { support }, Dependencies = Array.Empty<Transform>() });
                attachments.Add(new ShipV3Attachment { Object = mount.Find(lampName + "_Static_Hanger"), Supports = new[] { support }, Dependencies = Array.Empty<Transform>() });
            }
            destruction.Sections = sections.ToArray();
            profile.Sections = definitions.ToArray();
            profile.Structure = structure.ToArray();
            features.Lanterns = lanterns.ToArray();
            features.PhysicsBodies = bodies.ToArray();
            features.Pendulums = pendulums.ToArray();
            features.Attachments = attachments.ToArray();
            EditorUtility.SetDirty(profile);
        }

        static GameObject MeshObject(Transform source, Transform parent)
        {
            var node = new GameObject(source.name);
            node.transform.SetParent(parent, false);
            node.AddComponent<MeshFilter>();node.AddComponent<MeshRenderer>();
            CopyGeometry(source, node.transform);
            return node;
        }

        static void CopyGeometry(Transform source, Transform target)
        {
            var mesh = ShipV3BilgeSetup.TransformMesh(source.GetComponent<MeshFilter>().sharedMesh, target.worldToLocalMatrix * source.localToWorldMatrix);
            target.GetComponent<MeshFilter>().sharedMesh = ShipV3BilgeSetup.SaveMesh(mesh, target.name);
            var renderer = target.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials.Select(m => ShipV3BilgeSetup.MaterialFor(m, target.name, renderer.sharedMaterials)).ToArray();
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
        }
    }
}
