using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class StaffCastingPreview
    {
        public static string Capture(string name, float elapsed, string output, bool world = false)
        {
            var preview = new PreviewRenderUtility();
            var group = new GameObject("StaffCastingPreview");
            Texture2D image = null;
            RenderTexture copy = null;
            var previousTarget = RenderTexture.active;
            try
            {
                preview.AddSingleGO(group);
                var settings = AssetDatabase.LoadAssetAtPath<StaffSettings>("Assets/Settings/Weapons/" + name + ".asset");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Staves/" + name + "Visual.prefab");
                var item = Object.Instantiate(model, group.transform).transform;
                foreach (var collider in item.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                var pose = elapsed < 0 ? StaffCastingAnimation.Idle : elapsed <= settings.CastSeconds ? StaffCastingAnimation.Charge(settings.Item, elapsed / settings.CastSeconds) : StaffCastingAnimation.Release(settings.Item, elapsed - settings.CastSeconds, name == "IceStaff");
                if (!world) pose = StaffCastingAnimation.FirstPerson(pose);
                Vector3 position = settings.ViewPosition + pose.Position;
                if (world) position = StaffCastingAnimation.WorldPosition(pose);
                else position.z = Mathf.Max(settings.ViewPosition.z, position.z);
                item.SetLocalPositionAndRotation(position, Quaternion.Euler(pose.Rotation));
                if (world)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkPlayer.prefab");
                    var source = prefab.GetComponent<WeaponArmRig>().BodyRig;
                    var arms = Object.Instantiate(source.gameObject, group.transform).transform;
                    arms.name = source.name;
                    arms.gameObject.SetActive(true);
                    arms.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
                    arms.localScale = source.localScale;
                    var armType = typeof(WeaponArmRig).GetNestedType("Arm", BindingFlags.NonPublic);
                    var right = System.Activator.CreateInstance(armType, new object[] { arms, "R" });
                    var left = System.Activator.CreateInstance(armType, new object[] { arms, "L" });
                    var solve = typeof(WeaponArmRig).GetMethod("SolveStaff", BindingFlags.Static | BindingFlags.NonPublic);
                    solve.Invoke(null, new object[] { item, right, left, group.transform, pose.Support, pose.Wrist });
                    foreach (var renderer in arms.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.enabled = true; renderer.forceRenderingOff = false;
                        if (renderer is SkinnedMeshRenderer skin) { var bounds = skin.localBounds; bounds.Expand(1.5f); skin.localBounds = bounds; }
                    }
                }
                var socket = item.Find("Muzzle");
                StaffVfx.Held(socket, settings);
                foreach (var particles in item.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(1.2f, false, true, true);
                foreach (var effect in item.GetComponentsInChildren<StaffHeldPresentation>()) effect.Evaluate(.08f);
                preview.AddSingleGO(group);
                preview.camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (world)
                {
                    var cameraPosition = new Vector3(-.6f, 1.4f, 1.8f);
                    preview.camera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(new Vector3(.1f, 1.22f, .22f) - cameraPosition));
                }
                preview.camera.fieldOfView = 70; preview.camera.nearClipPlane = .02f; preview.camera.farClipPlane = 20;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.08f, .105f, .14f);
                preview.lights[0].intensity = 2f; preview.lights[0].transform.rotation = Quaternion.Euler(25, -35, 0);
                preview.lights[1].intensity = 1f; preview.lights[1].transform.rotation = Quaternion.Euler(0, 150, 0);
                preview.BeginPreview(new Rect(0, 0, 900, 600), GUIStyle.none); preview.Render(true);
                var texture = preview.EndPreview();
                copy = RenderTexture.GetTemporary(900, 600, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, copy); RenderTexture.active = copy;
                image = new Texture2D(900, 600, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 900, 600), 0, 0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output, image.EncodeToPNG());
                return output;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (copy != null) RenderTexture.ReleaseTemporary(copy);
                if (image != null) Object.DestroyImmediate(image);
                preview.Cleanup();
            }
        }
    }
}
