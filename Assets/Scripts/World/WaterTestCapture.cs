using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PirateSlop.Networking;
using PirateSlop.World;

namespace PirateSlop
{
    public sealed class WaterTestCapture : MonoBehaviour
    {
        string directory;
        Camera captureCamera;
        ShipController ship;
        bool driving, followShip;
        Vector3 driveStart;
        float driveTime, waterlineOffset;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-watercapture");
            if (index < 0 || index + 1 >= args.Length) return;
            var runner = new GameObject("Water Test Capture");
            DontDestroyOnLoad(runner);
            runner.AddComponent<WaterTestCapture>().directory = args[index + 1];
        }
        IEnumerator Start()
        {
            yield return null;
            Directory.CreateDirectory(directory);
            float timeout = Time.realtimeSinceStartup + 120f;
            while (ShipController.ActiveControllers.Count == 0 || Camera.main == null || ProceduralWorld.Instance == null || !ProceduralWorld.Instance.Ready)
            {
                if (Time.realtimeSinceStartup > timeout) { Debug.LogError("WATER_CAPTURE_START_TIMEOUT"); Application.Quit(2); yield break; }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(4f);
            ship = ShipController.ActiveControllers[0];
            var surface = OceanSurface.Instance;
            Debug.Log("WATER_CAPTURE_READY scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                " sea=" + surface.SeaLevel + " center=" + surface.WhirlpoolCenter + " radius=" + surface.WhirlpoolRadius + " depth=" + surface.WhirlpoolDepth);
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>().Where(m => m.shader != null && m.shader.name == "Boat Attack/Water"))
                Debug.Log("WATER_CAPTURE_MATERIAL name=" + material.name + " supported=" + material.shader.isSupported + " instanced=" + material.enableInstancing + " keywords=" + string.Join(",", material.shaderKeywords));
            yield return Capture("01-player");
            var source = Camera.main;
            source.enabled = false;
            source.tag = "Untagged";
            captureCamera = new GameObject("Water Verification Camera").AddComponent<Camera>();
            captureCamera.CopyFrom(source);
            captureCamera.tag = "MainCamera";
            captureCamera.enabled = true;
            captureCamera.GetUniversalAdditionalCameraData().requiresColorTexture = true;
            captureCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            captureCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var p = ship.transform.position;
            Frame(p + new Vector3(35f, 13f, -37f), p + Vector3.up);
            yield return Capture("02-hull");
            Frame(p + new Vector3(0f, 8f, 20f), surface.WhirlpoolCenter + new Vector3(0f, -25f, 0f));
            yield return Capture("03-deck-vortex");
            Frame(surface.WhirlpoolCenter + new Vector3(-370f, 230f, -450f), surface.WhirlpoolCenter + new Vector3(0f, -45f, 0f));
            yield return Capture("04-vortex-overview");
            Frame(surface.WhirlpoolCenter + new Vector3(-135f, 25f, -205f), surface.WhirlpoolCenter + new Vector3(0f, -90f, 0f));
            yield return Capture("05-vortex-approach");
            var inside = surface.WhirlpoolCenter + new Vector3(-150f, 0f, -90f);
            inside.y = surface.Height(inside) + 12f;
            Frame(inside, surface.WhirlpoolCenter + new Vector3(0f, -surface.WhirlpoolDepth + 3f, 0f));
            yield return Capture("06-vortex-inside");
            driveStart = new Vector3(-200f, p.y, -180f);
            waterlineOffset = p.y - surface.Height(p);
            driveTime = Time.realtimeSinceStartup;
            driving = followShip = true;
            yield return new WaitForSecondsRealtime(12f);
            yield return Capture("07-moving-wake");
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("08-moving-wake-later");
            driving = followShip = false;
            Debug.Log("WATER_CAPTURE_COMPLETE");
            Application.Quit();
        }
        void LateUpdate()
        {
            if (driving && ship != null)
            {
                var state = ship.Capture();
                state.Position = driveStart + Vector3.forward * ((Time.realtimeSinceStartup - driveTime) * 8f);
                state.Position.y = OceanSurface.Instance.Height(state.Position) + waterlineOffset;
                state.Yaw = state.Pitch = state.Bank = state.WaveRoll = 0f;
                state.Speed = 8f;
                state.Sail = 1f;
                state.Controlling = false;
                ship.Restore(state, null);
            }
            if (followShip && captureCamera != null)
                Frame(ship.transform.position + new Vector3(34f, 20f, -65f), ship.transform.position + new Vector3(0f, -1f, -28f));
        }
        void Frame(Vector3 position, Vector3 target)
        {
            captureCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.6f);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(directory, name + ".png");
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(pixels);
            }
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("WATER_CAPTURE_FRAME " + path + " camera=" + Camera.main.transform.position);
        }
    }
}
