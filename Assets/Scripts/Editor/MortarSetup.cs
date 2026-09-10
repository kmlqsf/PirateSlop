using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class MortarSetup
    {
        [MenuItem("PirateSlop/Configure Bow Mortar")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            const string folder = "Assets/Models/Mortar";
            Directory.CreateDirectory(folder + "/Meshes");
            Directory.CreateDirectory(folder + "/Materials");
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/ShipMortar.fbx");
            var anchors = source.GetComponentsInChildren<Transform>();
            Vector3 origin = anchors.First(t => t.name == "Anchor_Origin").position;
            var basis = Matrix4x4.identity;
            var names = new[] { "Anchor_Right", "Anchor_Up", "Anchor_Forward" };
            for (int i = 0; i < 3; i++) { var v = anchors.First(t => t.name == names[i]).position - origin; basis.SetColumn(i, new Vector4(v.x, v.y, v.z, 0)); }
            basis.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
            var root = new GameObject("BowMortar");
            try
            {
                var barrel = new GameObject("BarrelPivot").transform;
                barrel.SetParent(root.transform, false); barrel.localPosition = new Vector3(0, .85f, 0);
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    bool moving = filter.name.StartsWith("Barrel");
                    var go = new GameObject(filter.name); go.transform.SetParent(moving ? barrel : root.transform, false);
                    var matrix = Matrix4x4.Translate(moving ? new Vector3(0, -.85f, 0) : Vector3.zero) * basis.inverse * filter.transform.localToWorldMatrix;
                    var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name = filter.name;
                    mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
                    if (matrix.determinant < 0)
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            var tris = mesh.GetTriangles(sub);
                            for (int i = 0; i < tris.Length; i += 3) { int temp = tris[i]; tris[i] = tris[i + 2]; tris[i + 2] = temp; }
                            mesh.SetTriangles(tris, sub);
                        }
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    string path = folder + "/Meshes/" + filter.name + ".asset";
                    var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (old == null) AssetDatabase.CreateAsset(mesh, path);
                    else { EditorUtility.CopySerialized(mesh, old); UnityEngine.Object.DestroyImmediate(mesh); mesh = old; }
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    string materialName = filter.GetComponent<Renderer>().sharedMaterial.name;
                    Color color = materialName.Contains("Brass") ? new Color(.55f,.33f,.1f) : materialName.Contains("Wood") ? new Color(.25f,.11f,.04f) : materialName.Contains("Bore") ? new Color(.009f,.012f,.014f) : new Color(.055f,.07f,.08f);
                    go.AddComponent<MeshRenderer>().sharedMaterial = Material(folder + "/Materials/" + materialName + ".mat", color, false);
                }
                var baseCollider = root.AddComponent<BoxCollider>(); baseCollider.center = new Vector3(0,.4f,0); baseCollider.size = new Vector3(1.6f,.8f,1.65f);
                var tubeCollider = barrel.gameObject.AddComponent<BoxCollider>(); tubeCollider.center = new Vector3(0,0,.2f); tubeCollider.size = new Vector3(1f,1f,1.5f);
                var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(barrel, false); muzzle.localPosition = new Vector3(0,0,1.08f);
                var breech = new GameObject("Breech").transform; breech.SetParent(barrel, false); breech.localPosition = new Vector3(0,0,-.5f);
                barrel.localRotation = Quaternion.Euler(-3,0,0);
                var cannon = root.AddComponent<SimpleCannon>(); cannon.IsMortar = true; cannon.Muzzle = muzzle; cannon.BarrelPivot = barrel; cannon.Breech = breech;
                cannon.LaunchSpeed = 30f; cannon.MinElevation = 45f; cannon.MaxElevation = 80f; cannon.MaxTraverse = 65f;
                root.AddComponent<MortarTrajectory>().PreviewMaterial = Material(folder + "/Materials/MortarPreview.mat", new Color(1f,.55f,.08f), true);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Cannons/BowMortar.prefab");
                const string shipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
                var ship = PrefabUtility.LoadPrefabContents(shipPath);
                try
                {
                    var crate = ship.GetComponentInChildren<CannonballCrate>(true);
                    crate.MortarPrefab = prefab.GetComponent<SimpleCannon>(); crate.MortarPosition = new Vector3(0,4.3f,18f);
                    PrefabUtility.SaveAsPrefabAsset(ship, shipPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(ship); }
                var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
                config.ProtocolVersion = Math.Max(config.ProtocolVersion, 57); EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static Material Material(string path, Color color, bool unlit)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; EditorUtility.SetDirty(material); return material;
        }
    }
}
