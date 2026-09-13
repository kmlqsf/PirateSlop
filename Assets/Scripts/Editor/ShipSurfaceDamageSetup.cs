using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipSurfaceDamageSetup
    {
        public static string Configure()
        {
            const string path = "Assets/Prefabs/Networking/NetworkShip.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var destruction = root.GetComponent<ShipDestruction>();
                var meshes = destruction.Sections.SelectMany(s => s.GetComponentsInChildren<MeshFilter>(true)).Select(f => f.sharedMesh).Where(m => m != null).Distinct().ToArray();
                foreach (var mesh in meshes)
                {
                    mesh.RecalculateTangents();
                    EditorUtility.SetDirty(mesh);
                }
                foreach (var section in destruction.Sections.Where(s => destruction.Profile.Sections.First(d => d.SectionId == s.SectionId).Type == ShipSectionType.Deck))
                {
                    string meshPath = "Assets/Models/Ships/MainShip/Destruction/Meshes/SD_" + section.SectionId + "_Surface.asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        mesh = Object.Instantiate(section.Intact.GetComponent<MeshFilter>().sharedMesh);
                        for (int pass = 0; pass < 3; pass++) Subdivide(mesh);
                        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                        AssetDatabase.CreateAsset(mesh, meshPath);
                    }
                    section.Intact.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var serialized = new SerializedObject(mesh);
                    serialized.FindProperty("m_IsReadable").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mesh);
                    section.SurfaceDamage = true;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                ShipWoodFractureSetup.SaveSections();
                return "Tangents updated: " + meshes.Length + "; surface damage: 4 decks";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void Subdivide(Mesh mesh)
        {
            var vertices = mesh.vertices.ToList();
            var uv = mesh.uv.ToList();
            var edges = new Dictionary<ulong,int>();
            int Mid(int a, int b)
            {
                ulong key = ((ulong)(uint)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
                if (edges.TryGetValue(key, out int index)) return index;
                index = vertices.Count;
                vertices.Add((vertices[a] + vertices[b]) * .5f);
                uv.Add((uv[a] + uv[b]) * .5f);
                edges.Add(key,index);
                return index;
            }
            var submeshes = new List<int[]>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var original = mesh.GetTriangles(sub);var triangles = new List<int>();
                for (int i = 0; i < original.Length; i += 3)
                {
                    int a=original[i],b=original[i+1],c=original[i+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);
                    triangles.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
                }
                submeshes.Add(triangles.ToArray());
            }
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
            for (int sub=0;sub<submeshes.Count;sub++) mesh.SetTriangles(submeshes[sub],sub);
        }
    }
}
