using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop;

public static class SailRiggingArtSetup
{
    const string Folder = "Assets/Models/SailRigging/";
    const string MaterialFolder = "Assets/Materials/SailRigging/";
    [MenuItem("PirateSlop/Upgrade Sail Rigging Art")]
    public static void Configure()
    {
        const string path = "Assets/Prefabs/Networking/NetworkShip.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { Apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
    public static void Apply(GameObject root)
    {
        var rack = root.transform.Find("SailRopeRack");
        if (rack == null) throw new InvalidOperationException("Configure sail ropes first.");
        var materialMap = Materials();
        foreach (string name in new[] { "LeftPost", "RightPost", "TopBeam", "LowerBeam", "CraftedRack" })
        {
            var part = rack.Find(name);
            if (part != null) UnityEngine.Object.DestroyImmediate(part.gameObject);
        }
        rack.localPosition = Vector3.zero;
        rack.localRotation = Quaternion.identity;
        foreach (var rope in rack.GetComponentsInChildren<SailRopeVisual>(true))
        {
            PlaceAtMast(root, rope);
            InstallMesh(rope.transform, "CraftedStation", "SailRack", materialMap, true);
            var support = rope.transform.Find("CraftedStation").gameObject.AddComponent<BoxCollider>();
            support.center = new Vector3(.29f, 1.02f, 0);
            support.size = new Vector3(.20f, 2.04f, .24f);
            var handle = rope.Handle;
            StripMesh(handle.gameObject);
            handle.localScale = Vector3.one;
            var collider = handle.GetComponent<BoxCollider>();
            if (collider != null) { collider.size = new Vector3(.38f, .24f, .22f); collider.center = new Vector3(0, .035f, 0); }
            InstallMesh(handle, "CraftedGrip", "SailGrip", materialMap);
            var cleat = rope.transform.Find("Cleat_" + rope.Index);
            if (cleat != null) UnityEngine.Object.DestroyImmediate(cleat.gameObject);
            rope.Guide = new Vector3(0, 1.80f, -.115f);
            var tube = rope.GetComponent<SailRopeMesh>();
            if (tube == null) tube = rope.gameObject.AddComponent<SailRopeMesh>();
            tube.Rope = rope;
            rope.GetComponent<MeshRenderer>().sharedMaterial = materialMap["Rigging_Hemp"];
            rope.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            rope.Line.enabled = false;
            BuildRestRope(rope);
        }
    }
    static void PlaceAtMast(GameObject root, SailRopeVisual rope)
    {
        string sail = rope.Anchor.name;
        bool back = sail.Contains("Back"), front = sail.Contains("Front"), upper = sail.Contains("Mid2");
        string controlName = back ? "F2_MastBackControl" : front ? "F2_MastFrontControl" : "F2_MastMidControl";
        Transform control = null;
        foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            if (candidate.name == controlName) { control = candidate; break; }
        if (control == null) throw new InvalidOperationException("Missing mast reference " + controlName);
        Vector3 reference = root.transform.InverseTransformPoint(control.position);
        float z = back ? -17.95f : front ? 13.25f : 2.10f;
        float radius = back || front ? .26f : .35f;
        float offset = radius + .35f;
        rope.transform.localPosition = new Vector3(upper ? -offset : offset, reference.y - 1.3f, z);
        rope.transform.localRotation = Quaternion.Euler(0, upper ? 0 : 180f, 0);
        rope.transform.localScale = Vector3.one;
        rope.HandleTop = new Vector3(0, 1.65f, -.21f);
        rope.Handle.localPosition = rope.HandleTop;
    }
    static void BuildRestRope(SailRopeVisual rope)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SailRopeSample.fbx");
        if (source == null) throw new InvalidOperationException("Missing rope source.");
        var filter = rope.GetComponent<MeshFilter>();
        var mesh = new Mesh { name = "RopeRest_" + rope.Index };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int sides = 12;
        var points = new Vector3[rope.Line.positionCount];
        Vector3 start = rope.transform.InverseTransformPoint(rope.Anchor.position);
        for (int i = 0; i < 17; i++)
        {
            float t = i / 16f;
            points[i] = Vector3.Lerp(start, rope.Guide, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 1.6f);
        }
        for (int i = 1; i <= 8; i++)
        {
            float t = i / 8f;
            points[16 + i] = Vector3.Lerp(rope.Guide, rope.HandleTop, t) + Vector3.back * (Mathf.Sin(t * Mathf.PI) * .3f);
        }
        rope.Line.SetPositions(points);
        for (int j = 0; j < points.Length; j++)
        {
            Vector3 tangent = (points[Mathf.Min(j + 1, points.Length - 1)] - points[Mathf.Max(0, j - 1)]).normalized;
            Vector3 n = Vector3.Cross(tangent, Vector3.forward).normalized;
            if (n.sqrMagnitude < .1f) n = Vector3.right;
            Vector3 b = Vector3.Cross(tangent, n).normalized;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices.Add(points[j] + .023f * (Mathf.Cos(angle) * n + Mathf.Sin(angle) * b));
                if (j == points.Length - 1) continue;
                int a = j * sides + i, next = j * sides + (i + 1) % sides;
                triangles.Add(a); triangles.Add(next); triangles.Add(a + sides);
                triangles.Add(next); triangles.Add(next + sides); triangles.Add(a + sides);
            }
        }
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        filter.sharedMesh = SaveMesh(mesh, Folder + mesh.name + ".asset");
    }
    static void StripMesh(GameObject obj)
    {
        var renderer = obj.GetComponent<MeshRenderer>();
        var filter = obj.GetComponent<MeshFilter>();
        if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
        if (filter != null) UnityEngine.Object.DestroyImmediate(filter);
    }
    static void InstallMesh(Transform parent, string childName, string modelName, Dictionary<string, Material> materials, bool singleStation = false)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + modelName + ".fbx");
        if (source == null) throw new InvalidOperationException("Missing model " + modelName);
        var old = parent.Find(childName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var grouped = new Dictionary<Material, List<CombineInstance>>();
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            Matrix4x4 transform = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            if (singleStation)
            {
                string partName = filter.name.Split('.')[0];
                Vector3 center = transform.MultiplyPoint3x4(filter.sharedMesh.bounds.center);
                bool beam = partName == "CrownBeam" || partName == "BelayingRail" || partName == "BackApron";
                bool post = partName == "Upright" || partName == "Foot" || partName == "PostCap" || partName == "IronStrap" || partName == "KneeBrace" || partName == "BraceFoot" || partName == "DeckBolt" || partName.StartsWith("StrapRivet") || partName.StartsWith("BeamBolt") || partName.StartsWith("RailBolt");
                if (beam) transform = Matrix4x4.Scale(new Vector3(.29f, 1, 1)) * transform;
                else if (post)
                {
                    if (center.x < .9f) continue;
                    transform = Matrix4x4.Translate(new Vector3(-.79f, 0, 0)) * transform;
                }
                else
                {
                    if (Mathf.Abs(center.x - .78f) > .19f) continue;
                    transform = Matrix4x4.Translate(new Vector3(-.78f, 0, 0)) * transform;
                }
            }
            var renderer = filter.GetComponent<MeshRenderer>();
            for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
            {
                string name = renderer.sharedMaterials[i].name;
                int suffix = name.IndexOf('.');
                if (suffix >= 0) name = name.Substring(0, suffix);
                if (!materials.TryGetValue(name, out var material)) throw new InvalidOperationException("Unknown rigging material " + name);
                if (!grouped.TryGetValue(material, out var list)) grouped[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = i, transform = transform });
            }
        }
        var merged = new List<CombineInstance>();
        var slots = new List<Material>();
        foreach (var entry in grouped)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(entry.Value.ToArray(), true, true);
            merged.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.identity });
            slots.Add(entry.Key);
        }
        string meshName = singleStation ? "SailSingleStationCombined" : modelName + "Combined";
        var combined = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
        combined.CombineMeshes(merged.ToArray(), false, false);
        foreach (var part in merged) UnityEngine.Object.DestroyImmediate(part.mesh);
        var obj = new GameObject(childName, typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<MeshFilter>().sharedMesh = SaveMesh(combined, Folder + meshName + ".asset");
        obj.GetComponent<MeshRenderer>().sharedMaterials = slots.ToArray();
    }
    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }
    static Dictionary<string, Material> Materials()
    {
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/MainShip/Materials/Mat_StylShip_Masts.mat");
        if (wood == null) throw new InvalidOperationException("Missing ship wood material.");
        return new Dictionary<string, Material>
        {
            ["Rigging_ShipWood"] = wood,
            ["Rigging_BlackenedIron"] = Make("Rigging_BlackenedIron", wood.shader, new Color(.10f,.135f,.145f), .72f, .48f),
            ["Rigging_IronEdges"] = Make("Rigging_IronEdges", wood.shader, new Color(.20f,.245f,.25f), .65f, .57f),
            ["Rigging_OldBrass"] = Make("Rigging_OldBrass", wood.shader, new Color(.46f,.30f,.10f), .65f, .5f),
            ["Rigging_Hemp"] = Make("Rigging_Hemp", wood.shader, new Color(.48f,.36f,.20f), 0f, .05f),
            ["Rigging_HempLight"] = Make("Rigging_HempLight", wood.shader, new Color(.59f,.46f,.28f), 0f, .05f),
            ["Rigging_Recess"] = Make("Rigging_Recess", wood.shader, new Color(.04f,.028f,.016f), 0f, 0f)
        };
    }
    static Material Make(string name, Shader shader, Color color, float metallic, float smoothness)
    {
        string path = MaterialFolder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }
}
