using System;
using UnityEditor;
using UnityEngine;
using PirateSlop;
using PirateSlop.Networking;

public static class SailRopeSetup
{
    const string ShipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
    [MenuItem("PirateSlop/Configure Sail Ropes")]
    public static void Configure()
    {
        var root = PrefabUtility.LoadPrefabContents(ShipPath);
        try
        {
            var sails = root.GetComponent<SailSystem>();
            var serialized = new SerializedObject(sails);
            var meshes = serialized.FindProperty("sailMeshes");
            if (meshes.arraySize < 3 || meshes.arraySize > 5) throw new InvalidOperationException("Expected three to five sail meshes.");
            var old = root.transform.Find("SailRopeRack");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var rack = new GameObject("SailRopeRack").transform;
            rack.SetParent(root.transform, false);
            rack.localPosition = new Vector3(2.8f, 7.0f, -12.3f);
            var helmRenderer = root.GetComponentInChildren<HelmInteraction>().Wheel.GetComponent<Renderer>();
            var shader = helmRenderer != null && helmRenderer.sharedMaterial != null ? helmRenderer.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Ship material shader unavailable.");
            var wood = MaterialAsset("SailRackWood", shader, new Color(.22f, .11f, .045f));
            var ropeMaterial = MaterialAsset("SailRopeHemp", shader, new Color(.55f, .39f, .20f));
            var brass = MaterialAsset("SailRackBrass", shader, new Color(.65f, .44f, .16f));
            Part(rack, "LeftPost", new Vector3(-1.08f, 1.0f, 0), new Vector3(.16f, 2f, .18f), wood);
            Part(rack, "RightPost", new Vector3(1.08f, 1.0f, 0), new Vector3(.16f, 2f, .18f), wood);
            Part(rack, "TopBeam", new Vector3(0, 1.95f, 0), new Vector3(2.3f, .18f, .22f), wood);
            Part(rack, "LowerBeam", new Vector3(0, .55f, 0), new Vector3(2.3f, .13f, .18f), wood);
            sails.RopeHandles = new ShipControlHandle[meshes.arraySize];
            sails.RopeNames = new string[meshes.arraySize];
            for (int i = 0; i < meshes.arraySize; i++)
            {
                var sail = meshes.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (sail == null) throw new InvalidOperationException("Missing sail mesh.");
                float x = Mathf.Lerp(-.78f, .78f, i / (meshes.arraySize - 1f));
                var ropeRoot = new GameObject("SailRope_" + i).transform;
                ropeRoot.SetParent(rack, false);
                var handle = Part(ropeRoot, "Grip_" + i, new Vector3(x, 1.65f, -.21f), new Vector3(.27f, .14f, .20f), wood);
                var control = handle.AddComponent<ShipControlHandle>();
                control.Sails = sails; control.RopeIndex = i;
                sails.RopeHandles[i] = control;
                sails.RopeNames[i] = sail.name.Contains("Back") ? "Бизань" : sail.name.Contains("Front") ? "Фок" : sail.name.Contains("Mid2") ? "Грот-марсель" : "Грот";
                Part(ropeRoot, "Cleat_" + i, new Vector3(x, .58f, -.17f), new Vector3(.23f, .055f, .16f), brass);
                var line = ropeRoot.gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial = ropeMaterial; line.useWorldSpace = false;
                line.positionCount = 25; line.startWidth = line.endWidth = .035f;
                line.numCornerVertices = 2; line.numCapVertices = 2;
                var visual = ropeRoot.gameObject.AddComponent<SailRopeVisual>();
                visual.Sails = sails; visual.Index = i; visual.Anchor = sail;
                visual.Handle = handle.transform; visual.Line = line;
                visual.Guide = new Vector3(x, 1.93f, -.14f);
                visual.HandleTop = handle.transform.localPosition;
                var start = ropeRoot.InverseTransformPoint(sail.position);
                for (int j = 0; j < 17; j++)
                {
                    float t = j / 16f;
                    line.SetPosition(j, Vector3.Lerp(start, visual.Guide, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 1.6f));
                }
                for (int j = 1; j <= 8; j++) line.SetPosition(16 + j, Vector3.Lerp(visual.Guide, visual.HandleTop, j / 8f));
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SailRigging/SailRack.fbx") != null) SailRiggingArtSetup.Apply(root);
            PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
        if (config != null) { config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 93); EditorUtility.SetDirty(config); }
        AssetDatabase.SaveAssets();
    }
    static GameObject Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name; part.transform.SetParent(parent, false);
        part.transform.localPosition = position; part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }
    static Material MaterialAsset(string name, Shader shader, Color color)
    {
        const string folder = "Assets/Materials/SailRigging";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials", "SailRigging");
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }
}
