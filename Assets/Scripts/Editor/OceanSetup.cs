using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.EditorTools
{
    public static class OceanSetup
    {
        [MenuItem("PirateSlop/Configure Ocean")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/NetworkOcean.unity" })
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                ConfigureScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
        }
        static void ConfigureScene(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject water = null;
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "OceanWater") water = root;
            if (water == null) throw new System.InvalidOperationException("OceanWater missing.");
            var shader = Shader.Find("PirateSlop/Ocean");
            if (shader == null) throw new System.InvalidOperationException("Ocean shader missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ocean.mat");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, "Assets/Materials/Ocean.mat"); }
            material.shader = shader;
            water.transform.localScale = Vector3.one;
            water.transform.rotation = Quaternion.identity;
            foreach (var collider in water.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            var ocean = water.GetComponent<OceanSurface>();
            if (ocean == null) ocean = water.AddComponent<OceanSurface>();
            ocean.SeaLevel = water.transform.position.y; ocean.WaterMaterial = material;
            water.GetComponent<MeshRenderer>().sharedMaterial = material;
            water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipeline == null) continue;
                pipeline.supportsCameraDepthTexture = true; pipeline.supportsCameraOpaqueTexture = true; EditorUtility.SetDirty(pipeline);
            }
            var config = AssetDatabase.LoadAssetAtPath<PirateSlop.Networking.SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 7); EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(material);
            PrefabUtility.SaveAsPrefabAsset(water, "Assets/Prefabs/Ocean.prefab");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
    }
}
