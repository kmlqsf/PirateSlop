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
            foreach (var path in new[] { "Assets/Scenes/NetworkOcean.unity" })
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
            var shader = Shader.Find("Custom/SimpleWaterURP");
            if (shader == null) throw new System.InvalidOperationException("Ocean shader missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ocean.mat");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, "Assets/Materials/Ocean.mat"); }
            material.shader = shader;
            var sample = AssetDatabase.LoadAssetAtPath<Material>("Assets/Houidisoft technology/Simple water/Resources/water material sample.mat");
            if (sample == null) throw new System.InvalidOperationException("Simple Water sample material missing.");
            material.CopyPropertiesFromMaterial(sample);
            material.SetFloat("_WaveSpeed", .65f);
            material.SetFloat("_WaveStrength", .38f);
            material.SetFloat("_WaveScale", .18f);
            material.SetFloat("_UseWaveTime", 0f);
            material.SetFloat("_WorldSpaceUV", 1f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_NormalTiling", .022f);
            material.SetFloat("_NormalStrength", .32f);
            material.SetFloat("_NormalSpeed", .08f);
            material.SetFloat("_FoamTiling", .35f);
            material.SetFloat("_FoamSpeed", .06f);
            material.SetFloat("_FoamDistance", .55f);
            material.SetFloat("_WaterDepth", 7f);
            material.SetFloat("_ReflectionStrength", .65f);
            material.SetFloat("_FresnelPower", 4f);
            material.SetFloat("_SunStrength", .85f);
            material.SetFloat("_SunSharpness", 160f);
            material.SetColor("_ShallowColor", new Color(.22f, .68f, .58f, .42f));
            material.SetColor("_DeepColor", new Color(.025f, .30f, .28f, .98f));
            material.SetTexture("_OceanReflection", CaptureSky(scene));
            material.SetFloat("_UseOceanReflection", 1f);
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
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 74); EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(material);
            PrefabUtility.SaveAsPrefabAsset(water, "Assets/Prefabs/Ocean.prefab");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        static Cubemap CaptureSky(UnityEngine.SceneManagement.Scene scene)
        {
            const string path = "Assets/Materials/OceanSkyReflection.asset";
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cube == null)
            {
                cube = new Cubemap(128, TextureFormat.RGBAHalf, true);
                cube.name = "OceanSkyReflection";
                AssetDatabase.CreateAsset(cube, path);
            }
            var go = new GameObject("OceanSkyCapture");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = 0;
            camera.allowHDR = true;
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBHalf);
            target.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            var pixels = new Texture2D(128, 128, TextureFormat.RGBAHalf, false, true);
            var previous = RenderTexture.active;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            try
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                if (!camera.RenderToCubemap(target)) throw new System.InvalidOperationException("Ocean sky capture failed.");
                for (int face = 0; face < 6; face++)
                {
                    Graphics.SetRenderTarget(target, 0, (CubemapFace)face);
                    pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                    cube.SetPixels(pixels.GetPixels(), (CubemapFace)face);
                }
                cube.Apply(true, false);
                EditorUtility.SetDirty(cube);
                return cube;
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(go);
            }
        }
    }
}
