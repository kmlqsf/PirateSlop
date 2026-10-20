using System;
using System.Linq;
using PirateSlop.Experimental;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static partial class UpperDestructionAssetSetup
    {
        static bool IsFlexible(string name) => name.Contains("Rope") || name.Contains("Rig_") || name.Contains("Stay") || name.Contains("Shroud") || name.Contains("Ratline") || name.Contains("Lashing") || name.Contains("Lanyard") || name.Contains("Bridle") || name.Contains("Halyard") || name.Contains("Net") || name.Contains("Tie");

        public static string RefreshMastPresentation()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Unity must be idle outside Play Mode.");
            var root = PrefabUtility.LoadPrefabContents(Folder + "/Ship.prefab");
            try
            {
                var upper = root.GetComponent<UpperDestructionV2>();
                PrepareMastVisuals(root, upper);
                PrepareDrapedRigging(root, upper);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Ship.prefab");
                PruneGeometry(upper);
                AssetDatabase.SaveAssets();
                return "Mast presentation refreshed, including all rope materials.";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void PrepareClosedMastFragments(GameObject root, UpperDestructionV2 upper, ShipDestruction damage)
        {
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            if (container == null) { container = ScriptableObject.CreateInstance<UpperDebrisGeometry>(); AssetDatabase.CreateAsset(container, Folder + "/DebrisGeometry.asset"); }
            foreach (var assembly in upper.Assemblies.Where(a => a.Kind is UpperAssemblyKind.ForeMast or UpperAssemblyKind.MizzenMast))
            {
                var section = assembly.Sections.First(s => damage.Profile.Sections.First(d => d.SectionId == s.SectionId).SourceGroup == "V3_Mast_" + assembly.Key);
                var intact = section.Intact.GetComponent<MeshFilter>();
                foreach (var fragment in section.Fragments)
                {
                    var filter = fragment.GetComponent<MeshFilter>();
                    var bounds = ShapeBounds(root.transform, filter.transform, filter.sharedMesh.bounds);
                    var clipped = UpperMeshCut.Box(intact.sharedMesh, root.transform.worldToLocalMatrix * intact.transform.localToWorldMatrix, bounds);
                    var toFragment = fragment.transform.worldToLocalMatrix * intact.transform.localToWorldMatrix;
                    var vertices = clipped.vertices; var normals = clipped.normals;
                    for (int i = 0; i < vertices.Length; i++) { vertices[i] = toFragment.MultiplyPoint3x4(vertices[i]); normals[i] = toFragment.inverse.transpose.MultiplyVector(normals[i]).normalized; }
                    clipped.vertices = vertices; clipped.normals = normals; clipped.RecalculateBounds(); clipped.name = "Closed_" + fragment.name;
                    filter.sharedMesh = SavePreparedMesh(clipped, container);
                    var renderer = fragment.GetComponent<MeshRenderer>();
                    var materials = intact.GetComponent<MeshRenderer>().sharedMaterials.Take(intact.sharedMesh.subMeshCount).ToList();
                    while (materials.Count < intact.sharedMesh.subMeshCount) materials.Add(damage.Profile.SplinterMaterial);
                    materials.Add(damage.Profile.SplinterMaterial); renderer.sharedMaterials = materials.ToArray();
                    foreach (var collider in fragment.GetComponentsInChildren<MeshCollider>(true)) collider.sharedMesh = filter.sharedMesh;
                }
            }
        }

        static void PrepareDrapedRigging(GameObject root, UpperDestructionV2 upper)
        {
            var container = AssetDatabase.LoadAssetAtPath<UpperDebrisGeometry>(Folder + "/DebrisGeometry.asset");
            foreach (var assembly in upper.Assemblies)
                foreach (var recipe in assembly.MastVisuals)
                {
                    recipe.Flexible |= recipe.Source.GetComponent<SkinnedMeshRenderer>() != null;
                    if (!recipe.Flexible) continue;
                    recipe.DrapedPoses = new Mesh[recipe.Poses.Length];
                    for (int pose = 0; pose < recipe.Poses.Length; pose++)
                    {
                        var matrix = root.transform.worldToLocalMatrix * recipe.Source.localToWorldMatrix * Matrix4x4.Translate(recipe.Offsets[pose]);
                        var mesh = UpperFlexibleGeometry.Fold(recipe.Poses[pose], matrix, assembly.CollapsePivot, true);
                        mesh.name = "Draped_" + assembly.Key + "_" + recipe.Poses[pose].name;
                        recipe.DrapedPoses[pose] = SavePreparedMesh(mesh, container);
                    }
                }
            EditorUtility.SetDirty(container);
        }
    }
}
