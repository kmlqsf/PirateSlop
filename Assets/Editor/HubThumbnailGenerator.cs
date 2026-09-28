using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Pirateslop.Editor
{
    public static class HubThumbnailGenerator
    {
        public const string DefaultTargetDir = "D:/projects/pirateslop hub/public/thumbnails";

        [MenuItem("Tools/Pirateslop/Generate Hub Previews", false, 100)]
        public static void GenerateAllMenuItem()
        {
            int count = GenerateAll(DefaultTargetDir);
            EditorUtility.DisplayDialog("Hub Previews", $"Успешно создано/обновлено {count} 3D превью моделей и префабов!", "OK");
        }

        public static int GenerateAll(string targetDir = DefaultTargetDir)
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string[] searchFolders = new[] { "Assets/Models", "Assets/Prefabs", "Assets/Game" };
            string[] guids = AssetDatabase.FindAssets("t:Model t:Prefab", searchFolders);

            List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            List<string> paths = new List<string>();

            // 1. Prime all previews
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(p) || p.Contains("/Packages/") || p.Contains("/Plugins/")) continue;

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p);
                if (asset != null)
                {
                    assets.Add(asset);
                    paths.Add(p);
                    AssetPreview.GetAssetPreview(asset); // Warm up async renderer
                }
            }

            int savedCount = 0;
            Dictionary<string, string> manifest = new Dictionary<string, string>();

            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                var p = paths[i];
                string safeName = p.Replace('/', '_').Replace('\\', '_') + ".png";
                string fullSavePath = Path.Combine(targetDir, safeName);

                Texture2D preview = AssetPreview.GetAssetPreview(asset);
                int attempts = 0;
                while (preview == null && attempts < 25)
                {
                    System.Threading.Thread.Sleep(20);
                    attempts++;
                    preview = AssetPreview.GetAssetPreview(asset);
                }

                if (preview != null)
                {
                    try
                    {
                        RenderTexture rt = RenderTexture.GetTemporary(preview.width, preview.height, 0);
                        Graphics.Blit(preview, rt);
                        RenderTexture prevRt = RenderTexture.active;
                        RenderTexture.active = rt;

                        Texture2D readableTex = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
                        readableTex.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
                        readableTex.Apply();

                        RenderTexture.active = prevRt;
                        RenderTexture.ReleaseTemporary(rt);

                        byte[] pngBytes = readableTex.EncodeToPNG();
                        Object.DestroyImmediate(readableTex);

                        File.WriteAllBytes(fullSavePath, pngBytes);
                        savedCount++;
                        manifest[p] = "/thumbnails/" + safeName;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[HubThumbnailGenerator] Error saving preview for {p}: {ex.Message}");
                    }
                }
            }

            // Save manifest JSON
            try
            {
                string manifestPath = Path.Combine(targetDir, "manifest.json");
                string json = "{\n" + string.Join(",\n", GetManifestJsonLines(manifest)) + "\n}";
                File.WriteAllText(manifestPath, json);
            }
            catch {}

            Debug.Log($"[HubThumbnailGenerator] Completed! Rendered {savedCount} 3D previews to {targetDir}");
            return savedCount;
        }

        private static IEnumerable<string> GetManifestJsonLines(Dictionary<string, string> manifest)
        {
            foreach (var kvp in manifest)
            {
                yield return $"  \"{kvp.Key.Replace("\\", "/")}\": \"{kvp.Value}\"";
            }
        }
    }
}
