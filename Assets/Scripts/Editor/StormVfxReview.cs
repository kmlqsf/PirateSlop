using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditorInternal;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.EditorTools
{
    public static class StormVfxReview
    {
        [MenuItem("PirateSlop/VFX/Review/Status")]
        public static void Status()
        {
            var scene = SceneManager.GetActiveScene();
            var shaderPaths = new[] { "Assets/Game/BRZoneVolumetric/Shaders/VolumetricStorm.shader", "Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricClouds.shader", "Assets/Resources/Storm/RainScreen.shader", "Assets/Resources/Storm/RainStreak.shader", "Assets/Game/BRZoneVolumetric/Shaders/StormWaterline.shader", "Assets/Game/BRZoneVolumetric/Shaders/StormWeather.shader", "Assets/Game/BRZoneVolumetric/Shaders/StormBillows.shader" };
            var text = "Project=" + Application.dataPath + "\nScene=" + scene.name + "\nDirty=" + scene.isDirty + "\nPlay=" + EditorApplication.isPlaying + "\nPaused=" + EditorApplication.isPaused;
            text+="\nProfiler="+ProfilerDriver.enabled;
            var viewType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            foreach(var view in Resources.FindObjectsOfTypeAll(viewType))text+="\nGameViewIndex="+viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(view);
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>().Where(c => c.gameObject.scene.IsValid())) text += "\nCamera=" + camera.name + ";enabled=" + camera.enabled + ";tag=" + camera.tag + ";hide=" + camera.gameObject.hideFlags + ";target=" + (camera.targetTexture ? camera.targetTexture.name : "null");
            foreach (var path in shaderPaths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                text += "\nShader=" + path + ";messages=" + (shader ? ShaderUtil.GetShaderMessages(shader).Length : -1);
                if (shader) foreach (var message in ShaderUtil.GetShaderMessages(shader)) text += "\n" + message.severity + ":" + message.message;
            }
            const string folder = "Captures/VfxSkyWaterReview20261007";
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/native-status.txt", text);
            Debug.Log("VFX_REVIEW_STATUS " + text);
        }
    }
}