using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PirateSlop;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class MainShipSetup
    {
        const string Folder = "Assets/Models/Ships/MainShip";
        const string ShipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";

        [MenuItem("PirateSlop/Configure Main Ship")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var sub in new[] { "Meshes", "Materials", "Textures" }) Directory.CreateDirectory(Folder + "/" + sub);
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/MainShip.fbx");
            if (source == null) throw new InvalidOperationException("MainShip.fbx is missing.");
            var anchors = source.GetComponentsInChildren<Transform>();
            var origin = anchors.First(t => t.name == "Anchor_Origin").position;
            var basis = Matrix4x4.identity;
            var names = new[] { "Anchor_Right", "Anchor_Up", "Anchor_Forward" };
            for (int i = 0; i < 3; i++)
            {
                var v = anchors.First(t => t.name == names[i]).position - origin;
                basis.SetColumn(i, new Vector4(v.x, v.y, v.z, 0));
            }
            basis.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
            var correction = basis.inverse;
            var materials = source.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct().ToDictionary(n => n, MakeMaterial);
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                var boarding = root.GetComponentsInChildren<ShipLadder>(true).FirstOrDefault(l => l.name == "BoardingLadder");
                if (boarding != null) boarding.transform.SetParent(root.transform, false);
                foreach (string name in new[] { "SM_PirateSloop_Model", "SchoonerVisual", "SchoonerCollision", "FrigateVisual", "FrigateCollision", "FrigateLadders", "ClimbingRigging", "MainShipVisual", "MainShipCollision", "MainShipRigging" })
                {
                    var old = root.transform.Find(name);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                var visual = Child(root.transform, "MainShipVisual");
                var collision = Child(root.transform, "MainShipCollision");
                var rigging = Child(root.transform, "MainShipRigging");
                var sails = new List<Transform>();
                var mastControls = new List<Collider>();
                Transform wheel = null;
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    string name = filter.name.Replace("_Game", "");
                    if (name.StartsWith("F2_Box") || name.StartsWith("F2_Barrel")) continue;
                    var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    mesh.name = name;
                    var matrix = correction * filter.transform.localToWorldMatrix;
                    var vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                    if (name == "F2_Wheel" || name == "F2_WheelStand")
                    {
                        var anchor = new Vector3(0, 7.024f, -10.42f);
                        vertices = vertices.Select(v => anchor + (v - anchor) * .64f).ToArray();
                    }
                    if (name.StartsWith("F2_Fencing"))
                    {
                        float deck = name.EndsWith("Back") ? 7.024f : name.EndsWith("Front") ? 5.459f : 4.3f;
                        vertices = vertices.Select(v => new Vector3(v.x, deck + (v.y - deck) * .8f, v.z)).ToArray();
                    }
                    var bounds = new Bounds(vertices[0], Vector3.zero);
                    foreach (var v in vertices) bounds.Encapsulate(v);
                    var pivot = name == "F2_Wheel" ? bounds.center : name.StartsWith("F2_Sail") ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : Vector3.zero;
                    mesh.vertices = vertices.Select(v => v - pivot).ToArray();
                    if (matrix.determinant < 0)
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            var tris = mesh.GetTriangles(sub);
                            for (int i = 0; i < tris.Length; i += 3) (tris[i], tris[i + 2]) = (tris[i + 2], tris[i]);
                            mesh.SetTriangles(tris, sub);
                        }
                    var normalMatrix = matrix.inverse.transpose;
                    if (name == "F2_MastMid")
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            var tris = mesh.GetTriangles(sub);
                            var kept = new List<int>();
                            for (int i = 0; i < tris.Length; i += 3)
                            {
                                var points = new[] { vertices[tris[i]], vertices[tris[i + 1]], vertices[tris[i + 2]] };
                                bool entrance = points.All(v => v.y > 21.3f && v.y < 22.65f) && points.Any(v => v.x > 1.45f && Mathf.Abs(v.z - 1.9f) < .75f);
                                if (!entrance) { kept.Add(tris[i]); kept.Add(tris[i + 1]); kept.Add(tris[i + 2]); }
                            }
                            mesh.SetTriangles(kept, sub);
                        }
                    mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    mesh = StoreMesh(mesh, name);
                    var go = Child(visual, name);
                    go.localPosition = pivot;
                    go.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.gameObject.AddComponent<MeshRenderer>().sharedMaterials = filter.GetComponent<Renderer>().sharedMaterials.Select(m => materials[m.name]).ToArray();
                    if (name == "F2_Wheel") wheel = go;
                    if (name.StartsWith("F2_Sail")) sails.Add(go);
                    bool solid = name == "F2_Body" || name.StartsWith("F2_Floor") || name.StartsWith("F2_Closed") || name == "F2_SealedCentralHatch" || name.StartsWith("F2_Fencing") || name.StartsWith("F2_Mast") || name == "F2_Wall" || name == "F2_WheelStand" || name == "F2_Prow";
                    if (solid) go.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
                    if (name.StartsWith("F2_Mast"))
                    {
                        float deck = name.EndsWith("Back") ? 7.024f : name.EndsWith("Front") ? 5.459f : 4.3f;
                        float z = name.EndsWith("Back") ? -17.7f : name.EndsWith("Front") ? 12.6f : 2.1f;
                        var mast = Child(collision, name + "Control");
                        mast.localPosition = new Vector3(0, deck + 1.3f, z);
                        var shape = mast.gameObject.AddComponent<CapsuleCollider>();
                        shape.radius = .65f; shape.height = 2.6f;
                        mastControls.Add(shape);
                    }
                    if (name == "F2_StairsTop" || name == "F2_StairsSmall")
                        foreach (int side in new[] { -1, 1 })
                        {
                            var points = vertices.Where(v => v.x * side > .5f).ToArray();
                            var b = new Bounds(points[0], Vector3.zero);
                            foreach (var v in points) b.Encapsulate(v);
                            AddRamp(collision, name + side, b, name == "F2_StairsTop");
                        }
                }
                var helm = root.GetComponentInChildren<HelmInteraction>(true);
                helm.transform.localPosition = wheel.localPosition;
                helm.Configure(wheel);
                wheel.gameObject.AddComponent<ShipControlHandle>().Helm = helm;
                var wheelBox = wheel.gameObject.AddComponent<BoxCollider>();
                wheelBox.size = wheel.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                var sailSystem = root.GetComponent<SailSystem>();
                sailSystem.MastControls = mastControls.ToArray();
                var so = new SerializedObject(sailSystem);
                var array = so.FindProperty("sailMeshes"); array.arraySize = sails.Count;
                for (int i = 0; i < sails.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = sails[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                var crate = root.GetComponentInChildren<CannonballCrate>(true);
                crate.transform.localPosition = new Vector3(-3.4f, 4.34f, -15f);
                crate.Kit.transform.localPosition = new Vector3(3.4f, 4.34f, -15f);
                crate.DeckSupplyPoint.localPosition = new Vector3(-3.4f, 4.6f, -13f);
                crate.MortarPosition = new Vector3(0, 5.46f, 18f);
                var shelf = root.GetComponentInChildren<RumShelf>(true);
                shelf.transform.localPosition = new Vector3(4.4f, 5.35f, -18.5f);
                var spyglass = root.GetComponentInChildren<ShipSpyglass>(true);
                spyglass.transform.localPosition = new Vector3(0, 22.6f, 3.3f);
                if (boarding != null)
                {
                    boarding.transform.SetParent(rigging, false);
                    boarding.transform.localPosition = new Vector3(6.65f, -2f, 0);
                    boarding.Height = 6.4f;
                    foreach (var renderer in boarding.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = materials["Mat_StylShip_Masts"];
                }
                foreach (int side in new[] { -1, 1 })
                {
                    var go = Child(rigging, side < 0 ? "PortShrouds" : "StarboardShrouds");
                    go.localPosition = new Vector3(side * 6.83f, 4.73f, -2.1f);
                    go.localRotation = Quaternion.LookRotation(new Vector3(side * 5.45f, 0, -3.7f));
                    var ladder = go.gameObject.AddComponent<ShipLadder>();
                    ladder.RopeClimb = true; ladder.Height = 16.52f; ladder.TopLean = -6.59f; ladder.Speed = 2f;
                    ladder.ExitPoint = go.InverseTransformPoint(root.transform.TransformPoint(new Vector3(side * 1.2f, 21.25f, 1f)));
                }
                var mastLadder = Child(rigging, "CrowNestLadder");
                mastLadder.localPosition = new Vector3(2.15f, 4.3f, 1.9f);
                mastLadder.localRotation = Quaternion.Euler(0, 90, 0);
                var climb = mastLadder.gameObject.AddComponent<ShipLadder>();
                climb.Height = 16.95f; climb.HalfWidth = .65f; climb.ExitDepth = 1.1f;
                var ladderMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                ladderMaterial.color = new Color(.25f, .12f, .055f);
                string ladderMaterialPath = Folder + "/Materials/LadderWood.mat";
                var existingLadderMaterial = AssetDatabase.LoadAssetAtPath<Material>(ladderMaterialPath);
                if (existingLadderMaterial == null) AssetDatabase.CreateAsset(ladderMaterial, ladderMaterialPath);
                else { UnityEngine.Object.DestroyImmediate(ladderMaterial); ladderMaterial = existingLadderMaterial; }
                for (float y = .2f; y < 17.15f; y += .32f) LadderPart(mastLadder, "Rung", new Vector3(0, y, 0), new Vector3(1f, .075f, .1f), ladderMaterial);
                foreach (float x in new[] { -.53f, .53f }) LadderPart(mastLadder, "Rail", new Vector3(x, 8.7f, 0), new Vector3(.085f, 17.4f, .1f), ladderMaterial);
                root.GetComponent<Rigidbody>().isKinematic = true;
                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ShipComparisonEnabled = false;
            config.ComparisonShips = Array.Empty<PirateSlop.World.ShipComparison>();
            config.PlayerLocalSpawn = new Vector3(-1.5f, 7.15f, -13f);
            config.ProtocolVersion = Math.Max(config.ProtocolVersion, 69);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void LadderPart(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Mesh StoreMesh(Mesh mesh, string name)
        {
            string path = Folder + "/Meshes/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, old); UnityEngine.Object.DestroyImmediate(mesh); mesh = old; }
            return mesh;
        }

        static Material MakeMaterial(string name)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            string key = name.Replace("Mat_", "");
            var old = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/Comparison/Ship1/Materials/" + key + ".mat");
            if (old == null) throw new InvalidOperationException("Missing source material: " + key);
            mat = new Material(old); mat.name = name;
            foreach (string property in mat.GetTexturePropertyNames())
            {
                var tex = mat.GetTexture(property);
                if (tex == null) continue;
                string src = AssetDatabase.GetAssetPath(tex);
                string dst = Folder + "/Textures/" + Path.GetFileName(src);
                if (AssetDatabase.LoadAssetAtPath<Texture>(dst) == null && !AssetDatabase.CopyAsset(src, dst)) throw new InvalidOperationException("Texture copy failed: " + src);
                mat.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture>(dst));
            }
            mat.SetTexture("_OcclusionMap", null);
            mat.DisableKeyword("_OCCLUSIONMAP");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void AddRamp(Transform parent, string name, Bounds b, bool aft)
        {
            float lo = 4.3f, hi = aft ? 7.024f : 5.459f;
            float y0 = aft ? hi : lo, y1 = aft ? lo : hi;
            float x0 = b.min.x + .08f, x1 = b.max.x - .08f;
            var vertices = new[] { new Vector3(x0,y0,b.min.z),new Vector3(x1,y0,b.min.z),new Vector3(x1,y1,b.max.z),new Vector3(x0,y1,b.max.z) };
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices.Concat(vertices.Select(v => v - Vector3.up * .2f)).ToArray();
            mesh.triangles = new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var shape = Child(parent,name).gameObject.AddComponent<MeshCollider>();
            shape.sharedMesh = StoreMesh(mesh,name); shape.convex = true;
        }
    }
}
