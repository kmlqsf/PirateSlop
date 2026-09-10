using System.IO;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public sealed class GltfPropImporter : EditorWindow
    {
        GameObject source;
        float scale = 1f;
        bool collision = true;

        [MenuItem("PirateSlop/Assets/glTF Prop Prefab")]
        static void Open()
        {
            var window = GetWindow<GltfPropImporter>("glTF Prop");
            window.source = Selection.activeObject as GameObject;
        }

        void OnGUI()
        {
            source = (GameObject)EditorGUILayout.ObjectField("Imported glTF / GLB", source, typeof(GameObject), false);
            scale = EditorGUILayout.FloatField("Scale", scale);
            collision = EditorGUILayout.Toggle("Static prop collision", collision);
            string path = source == null ? "" : AssetDatabase.GetAssetPath(source);
            string extension = Path.GetExtension(path).ToLowerInvariant();
            bool valid = source != null && (extension == ".glb" || extension == ".gltf") && float.IsFinite(scale) && scale > 0f;
            EditorGUILayout.HelpBox("Import the model into Assets first. This creates a reusable scenery prefab; gameplay and networking are configured separately.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!valid))
                if (GUILayout.Button("Save Prefab")) Save();
        }

        void Save()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Prop", source.name, "prefab", "Choose the prop prefab location.");
            if (string.IsNullOrEmpty(path)) return;
            var root = new GameObject(source.name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform, false);
                model.transform.localScale *= scale;
                if (collision)
                    foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                        var collider = filter.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                    }
                Selection.activeObject = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { DestroyImmediate(root); }
        }
    }
}
