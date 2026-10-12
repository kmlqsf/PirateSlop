using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.Editor
{
    public static class StaffVfxPreview
    {
        public static string Capture(string kind, float elapsed, bool bright, string output)
        {
            var preview = new PreviewRenderUtility();
            var group = new GameObject("StaffPreview");
            var randomState = UnityEngine.Random.state;
            Material floorMaterial = null;
            Texture2D image = null;
            RenderTexture copy = null;
            VolumeProfile profile = null;
            var previousTarget = RenderTexture.active;
            try
            {
                UnityEngine.Random.InitState(8107);
                preview.AddSingleGO(group);
                var fire = AssetDatabase.LoadAssetAtPath<StaffSettings>("Assets/Settings/Weapons/FireStaff.asset");
                var ice = AssetDatabase.LoadAssetAtPath<StaffSettings>("Assets/Settings/Weapons/IceStaff.asset");
                var lightning = AssetDatabase.LoadAssetAtPath<StaffSettings>("Assets/Settings/Weapons/LightningStaff.asset");
                GameObject effect;
                bool flight = kind == "FireFlight" || kind == "FireSky";
                if (flight)
                {
                    effect = new GameObject("Projectile"); effect.transform.SetParent(group.transform, false);
                    StaffVfx.Flight(effect.transform, fire);
                    effect.transform.position = Vector3.up * 1.2f;
                }
                else if (kind == "IceBeam" || kind == "IceContact")
                {
                    effect = StaffVfx.IceBeam(); effect.transform.SetParent(group.transform, false);
                    StaffVfx.UpdateIceBeam(effect, Vector3.up, new Vector3(0, 1, kind == "IceContact" ? 5 : 30), true);
                }
                else if (kind == "Telegraph")
                {
                    effect = StaffVfx.Telegraph(lightning.Color, lightning.BlastRadius); effect.transform.SetParent(group.transform, false);
                }
                else
                {
                    StaffVfx.Impact(Vector3.zero, kind == "FireImpact" ? fire : lightning);
                    effect = GameObject.Find("StaffImpactVfx"); effect.transform.SetParent(group.transform, true);
                }
                preview.AddSingleGO(group);
                var systems = effect.GetComponentsInChildren<ParticleSystem>();
                foreach (var particles in systems) { particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particles.useAutoRandomSeed = false; particles.randomSeed = 1207; }
                int steps = Mathf.Max(1, Mathf.CeilToInt(elapsed * 60f));
                for (int step = 0; step < steps; step++)
                {
                    if (flight) effect.transform.position = new Vector3((step / (float)steps - 1f) * 3f, 1.2f, 0);
                    foreach (var particles in systems) particles.Simulate(elapsed / steps, false, step == 0, false);
                    if (flight) foreach (var trail in effect.GetComponentsInChildren<TrailRenderer>()) trail.AddPosition(effect.transform.position);
                }
                if (kind == "IceBeam" || kind == "IceContact") effect.GetComponent<StaffIceBeamPresentation>().Trim();
                foreach (var animator in effect.GetComponentsInChildren<StaffVfxEffect>()) if (!animator.Flight) animator.Evaluate(elapsed);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(group.transform, false);
                UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
                floor.transform.position = new Vector3(0, -.13f, 10); floor.transform.localScale = new Vector3(40, .2f, 60);
                floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); floorMaterial.SetColor("_BaseColor", bright ? new Color(.38f, .43f, .46f) : new Color(.085f, .1f, .12f)); floorMaterial.SetFloat("_Smoothness", .25f);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = bright ? new Color(.63f, .74f, .84f) : new Color(.012f, .018f, .03f);
                preview.camera.nearClipPlane = .05f; preview.camera.farClipPlane = 160; preview.camera.fieldOfView = 44; preview.camera.allowHDR = true;
                var cameraData = preview.camera.GetUniversalAdditionalCameraData(); cameraData.requiresDepthTexture = true; cameraData.renderPostProcessing = true;
                var volumeRoot = new GameObject("PreviewVolume"); volumeRoot.transform.SetParent(group.transform, false);
                var volume = volumeRoot.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 1000;
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.hideFlags = HideFlags.HideAndDontSave; volume.sharedProfile = profile;
                var bloom = profile.Add<Bloom>(); bloom.intensity.Override(.32f); bloom.threshold.Override(1f); bloom.scatter.Override(.65f);
                var tone = profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
                Vector3 focus, position;
                if (kind == "IceBeam") { position = new Vector3(7, 4, -6); focus = new Vector3(0, 1, 11); }
                else if (kind == "IceContact") { position = new Vector3(3, 2, -2); focus = new Vector3(0, 1, 3); }
                else if (kind == "Lightning") { position = new Vector3(10, 8, -25); focus = new Vector3(0, 12, 0); preview.camera.fieldOfView = 58; }
                else if (flight) { position = new Vector3(1.3f, kind == "FireSky" ? .6f : 1.7f, -3.4f); focus = new Vector3(-.65f, 1.2f, 0); }
                else { position = new Vector3(5, 4, -8); focus = new Vector3(0, .6f, 0); }
                preview.camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
                preview.lights[0].intensity = bright ? 2f : .3f; preview.lights[0].transform.rotation = Quaternion.Euler(40, -40, 0);
                preview.lights[1].intensity = .3f;
                preview.BeginPreview(new Rect(0, 0, 1200, 800), GUIStyle.none);
                preview.Render(true);
                var texture = preview.EndPreview();
                copy = RenderTexture.GetTemporary(1200, 800, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, copy); RenderTexture.active = copy;
                image = new Texture2D(1200, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output, image.EncodeToPNG());
                return output;
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                RenderTexture.active = previousTarget;
                if (copy != null) RenderTexture.ReleaseTemporary(copy);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (floorMaterial != null) UnityEngine.Object.DestroyImmediate(floorMaterial);
                if (profile != null)
                {
                    foreach (var component in profile.components) UnityEngine.Object.DestroyImmediate(component);
                    UnityEngine.Object.DestroyImmediate(profile);
                }
                preview.Cleanup();
            }
        }
    }
}
