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
        static void PrepareAdditionalObjects(GameObject root, ShipDestruction damage)
        {
            var slot = root.GetComponentInChildren<ShipSlotMachine>(true);
            if (slot == null) return;
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            if (container == null) { container = ScriptableObject.CreateInstance<UpperDebrisGeometry>(); AssetDatabase.CreateAsset(container, Folder + "/DebrisGeometry.asset"); }
            var sectionRoot = new GameObject("UpperCasino"); sectionRoot.transform.SetParent(slot.transform.parent, false);
            sectionRoot.transform.SetPositionAndRotation(slot.transform.position, slot.transform.rotation);
            slot.transform.SetParent(sectionRoot.transform, true);
            var section = sectionRoot.AddComponent<ShipDamageSection>();
            section.SectionId = damage.Profile.Sections.Max(s => s.SectionId) + 1;
            section.Intact = slot.gameObject; section.LazyFragmentColliders = true;
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in slot.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null) continue;
                var materials = renderer.sharedMaterials;
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount && materials.Length > 0; sub++)
                {
                    var material = materials[Mathf.Min(sub, materials.Length - 1)] ?? damage.Profile.SplinterMaterial;
                    if (!groups.TryGetValue(material, out var parts)) groups[material] = parts = new List<CombineInstance>();
                    parts.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = slot.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
            }
            var intermediate = new List<Mesh>();
            foreach (var group in groups.Values)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(group.ToArray(), true, true); intermediate.Add(mesh);
            }
            var combined = new Mesh { name = "CasinoIntact", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            combined.CombineMeshes(intermediate.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(), false, true);
            foreach (var mesh in intermediate) UnityEngine.Object.DestroyImmediate(mesh);
            combined = SavePreparedMesh(combined, container);
            var source = slot.gameObject.AddComponent<MeshFilter>(); source.sharedMesh = combined;
            var sourceRenderer = slot.gameObject.AddComponent<MeshRenderer>(); sourceRenderer.sharedMaterials = groups.Keys.ToArray(); sourceRenderer.enabled = false; sourceRenderer.forceRenderingOff = true;
            section.GameplayColliders = slot.GetComponentsInChildren<Collider>(true); section.DamageColliders = section.GameplayColliders;
            section.Fragments = new GameObject[3];
            for (int bit = 0; bit < 3; bit++)
            {
                var fragment = new GameObject("CasinoFragment_" + bit); fragment.transform.SetParent(sectionRoot.transform, false);
                fragment.transform.SetPositionAndRotation(slot.transform.position, slot.transform.rotation); fragment.transform.localScale = slot.transform.localScale;
                float min = bit == 0 ? float.NegativeInfinity : Mathf.Lerp(combined.bounds.min.x, combined.bounds.max.x, bit / 3f);
                float max = bit == 2 ? float.PositiveInfinity : Mathf.Lerp(combined.bounds.min.x, combined.bounds.max.x, (bit + 1) / 3f);
                var mesh = UpperMeshCut.Band(combined, 0, min, max, .012f); mesh.name = fragment.name;
                fragment.AddComponent<MeshFilter>().sharedMesh = SavePreparedMesh(mesh, container);
                fragment.AddComponent<MeshRenderer>().sharedMaterials = groups.Keys.Concat(new[] { damage.Profile.SplinterMaterial }).ToArray();
                fragment.SetActive(false); section.Fragments[bit] = fragment;
            }
            damage.Sections = damage.Sections.Append(section).ToArray();
            damage.Profile.Sections = damage.Profile.Sections.Append(new ShipSectionDefinition { SectionId = section.SectionId, SourceGroup = "UpperCasino", Name = "Casino", Type = ShipSectionType.Fitting, Repairable = true }).ToArray();
            var nodes = damage.Profile.Structure.ToList();
            for (int bit = 0; bit < 3; bit++) nodes.Add(new ShipFragmentConnection { SectionId = section.SectionId, Fragment = bit });
            damage.Profile.Structure = nodes.ToArray();
        }
    }
}
