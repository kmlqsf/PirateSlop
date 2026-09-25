using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PirateSlop.Customization
{
    [System.Serializable]
    public class SailData
    {
        public Color baseColor = Color.white;
        public float weathering = 0f;
        public float grime = 0f;

        // Layer 1 (Primary Decal)
        public Color decalColor = Color.white;
        public int blendMode = 0;
        public Vector2 scale = Vector2.one;
        public Vector2 offset = Vector2.zero;
        public float rotation = 0f;
        public bool hasDecal = false;
        public string imageName = "";
        public string imageHash = "";

        // Layer 2 (Secondary Decal)
        public Color decalColor2 = Color.white;
        public int blendMode2 = 0;
        public Vector2 scale2 = Vector2.one;
        public Vector2 offset2 = Vector2.zero;
        public float rotation2 = 0f;
        public bool hasDecal2 = false;
        public string imageName2 = "";
        public string imageHash2 = "";

        public void Reset()
        {
            baseColor = Color.white;
            weathering = 0f;
            grime = 0f;

            decalColor = Color.white;
            blendMode = 0;
            scale = Vector2.one;
            offset = Vector2.zero;
            rotation = 0f;
            hasDecal = false;
            imageName = "";
            imageHash = "";

            decalColor2 = Color.white;
            blendMode2 = 0;
            scale2 = Vector2.one;
            offset2 = Vector2.zero;
            rotation2 = 0f;
            hasDecal2 = false;
            imageName2 = "";
            imageHash2 = "";
        }

        public void CopyFrom(SailData other)
        {
            if (other == null) return;
            baseColor = other.baseColor;
            weathering = other.weathering;
            grime = other.grime;

            decalColor = other.decalColor;
            blendMode = other.blendMode;
            scale = other.scale;
            offset = other.offset;
            rotation = other.rotation;
            hasDecal = other.hasDecal;
            imageName = other.imageName;
            imageHash = other.imageHash;

            decalColor2 = other.decalColor2;
            blendMode2 = other.blendMode2;
            scale2 = other.scale2;
            offset2 = other.offset2;
            rotation2 = other.rotation2;
            hasDecal2 = other.hasDecal2;
            imageName2 = other.imageName2;
            imageHash2 = other.imageHash2;
        }

        public bool GetHasDecal(int layer) => layer == 0 ? hasDecal : hasDecal2;
        public void SetHasDecal(int layer, bool val)
        {
            if (layer == 0) hasDecal = val; else hasDecal2 = val;
        }

        public Color GetDecalColor(int layer) => layer == 0 ? decalColor : decalColor2;
        public void SetDecalColor(int layer, Color c)
        {
            if (layer == 0) decalColor = c; else decalColor2 = c;
        }

        public int GetBlendMode(int layer) => layer == 0 ? blendMode : blendMode2;
        public void SetBlendMode(int layer, int m)
        {
            if (layer == 0) blendMode = m; else blendMode2 = m;
        }

        public Vector2 GetScale(int layer) => layer == 0 ? scale : scale2;
        public void SetScale(int layer, Vector2 s)
        {
            if (layer == 0) scale = s; else scale2 = s;
        }

        public Vector2 GetOffset(int layer) => layer == 0 ? offset : offset2;
        public void SetOffset(int layer, Vector2 o)
        {
            if (layer == 0) offset = o; else offset2 = o;
        }

        public float GetRotation(int layer) => layer == 0 ? rotation : rotation2;
        public void SetRotation(int layer, float r)
        {
            if (layer == 0) rotation = r; else rotation2 = r;
        }

        public string GetImageHash(int layer) => layer == 0 ? imageHash : imageHash2;
        public void SetImageHash(int layer, string h)
        {
            if (layer == 0) imageHash = h; else imageHash2 = h;
        }
    }

    [System.Serializable]
    public class ShipCustomizationData
    {
        public const int TotalParts = 7; // 0..3: Sails, 4..6: Flags
        public const int TotalLayers = 2; // 0: Layer 1, 1: Layer 2

        public SailData[] sails;

        public ShipCustomizationData()
        {
            sails = new SailData[TotalParts];
            for (int i = 0; i < TotalParts; i++) sails[i] = new SailData();
        }

        public void EnsureCapacity()
        {
            if (sails == null) sails = new SailData[TotalParts];
            if (sails.Length < TotalParts)
            {
                var newArr = new SailData[TotalParts];
                for (int i = 0; i < TotalParts; i++)
                {
                    newArr[i] = (i < sails.Length && sails[i] != null) ? sails[i] : new SailData();
                }
                sails = newArr;
            }
        }

        public ShipCustomizationData Clone()
        {
            var clone = new ShipCustomizationData();
            EnsureCapacity();
            for (int i = 0; i < TotalParts; i++)
            {
                clone.sails[i].CopyFrom(sails[i]);
            }
            return clone;
        }
    }

    public static class SailCustomizationStorage
    {
        public static string RootDirectory => Path.Combine(Application.persistentDataPath, "SailCustomization");
        public static string PresetsDirectory => Path.Combine(RootDirectory, "Presets");
        static string DefaultConfigPath => Path.Combine(RootDirectory, "sails_config.json");

        static string PartImagePath(string folder, int partIndex, int layerIndex) =>
            Path.Combine(folder, $"part_{partIndex}_layer_{layerIndex}.png");

        public static void Save(ShipCustomizationData data, Texture2D[,] textures)
        {
            SaveToFolder(RootDirectory, DefaultConfigPath, data, textures);
        }

        public static bool Load(out ShipCustomizationData data, out Texture2D[,] textures)
        {
            return LoadFromFolder(RootDirectory, DefaultConfigPath, out data, out textures);
        }

        public static List<string> GetAvailablePresets()
        {
            var list = new List<string>();
            try
            {
                if (Directory.Exists(PresetsDirectory))
                {
                    var dirs = Directory.GetDirectories(PresetsDirectory);
                    foreach (var d in dirs)
                    {
                        list.Add(Path.GetFileName(d));
                    }
                }
            }
            catch { }

            if (list.Count == 0)
            {
                list.Add("Слот 1");
                list.Add("Слот 2");
                list.Add("Слот 3");
            }
            return list;
        }

        public static void SavePreset(string presetName, ShipCustomizationData data, Texture2D[,] textures)
        {
            if (string.IsNullOrEmpty(presetName)) return;
            string folder = Path.Combine(PresetsDirectory, presetName);
            string configFile = Path.Combine(folder, "config.json");
            SaveToFolder(folder, configFile, data, textures);
        }

        public static bool LoadPreset(string presetName, out ShipCustomizationData data, out Texture2D[,] textures)
        {
            if (string.IsNullOrEmpty(presetName))
            {
                data = new ShipCustomizationData();
                textures = new Texture2D[ShipCustomizationData.TotalParts, ShipCustomizationData.TotalLayers];
                return false;
            }
            string folder = Path.Combine(PresetsDirectory, presetName);
            string configFile = Path.Combine(folder, "config.json");
            return LoadFromFolder(folder, configFile, out data, out textures);
        }

        public static void DeletePreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;
            string folder = Path.Combine(PresetsDirectory, presetName);
            if (Directory.Exists(folder))
            {
                try { Directory.Delete(folder, true); } catch { }
            }
        }

        static void SaveToFolder(string folder, string configFilePath, ShipCustomizationData data, Texture2D[,] textures)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                data.EnsureCapacity();

                for (int p = 0; p < ShipCustomizationData.TotalParts; p++)
                {
                    for (int l = 0; l < ShipCustomizationData.TotalLayers; l++)
                    {
                        string imgFile = PartImagePath(folder, p, l);
                        bool has = data.sails[p].GetHasDecal(l);
                        Texture2D tex = (textures != null && p < textures.GetLength(0) && l < textures.GetLength(1)) ? textures[p, l] : null;

                        if (has && tex != null)
                        {
                            byte[] bytes = SailImageLoader.CompressImageToBytes(tex);
                            if (bytes != null)
                            {
                                File.WriteAllBytes(imgFile, bytes);
                                string hash = SailImageLoader.ComputeHash(bytes);
                                data.sails[p].SetImageHash(l, hash);
                                SailImageLoader.CacheTexture(hash, bytes, tex);
                            }
                        }
                        else
                        {
                            data.sails[p].SetHasDecal(l, false);
                            data.sails[p].SetImageHash(l, "");
                            if (File.Exists(imgFile)) File.Delete(imgFile);
                        }
                    }
                }

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SailCustomizationStorage] Save failed to {folder}: {ex.Message}");
            }
        }

        static bool LoadFromFolder(string folder, string configFilePath, out ShipCustomizationData data, out Texture2D[,] textures)
        {
            data = new ShipCustomizationData();
            textures = new Texture2D[ShipCustomizationData.TotalParts, ShipCustomizationData.TotalLayers];

            if (!File.Exists(configFilePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(configFilePath);
                data = JsonUtility.FromJson<ShipCustomizationData>(json);
                if (data == null)
                {
                    data = new ShipCustomizationData();
                    return false;
                }
                data.EnsureCapacity();

                for (int p = 0; p < ShipCustomizationData.TotalParts; p++)
                {
                    for (int l = 0; l < ShipCustomizationData.TotalLayers; l++)
                    {
                        string hash = data.sails[p].GetImageHash(l);
                        if (!string.IsNullOrEmpty(hash) && SailImageLoader.TryGetFromCache(hash, out var cachedTex))
                        {
                            textures[p, l] = cachedTex;
                            data.sails[p].SetHasDecal(l, true);
                            continue;
                        }

                        string imgFile = PartImagePath(folder, p, l);
                        if (data.sails[p].GetHasDecal(l) && File.Exists(imgFile))
                        {
                            byte[] bytes = File.ReadAllBytes(imgFile);
                            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            if (tex.LoadImage(bytes))
                            {
                                tex.wrapMode = TextureWrapMode.Clamp;
                                textures[p, l] = tex;
                                if (!string.IsNullOrEmpty(hash))
                                {
                                    SailImageLoader.CacheTexture(hash, bytes, tex);
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SailCustomizationStorage] Load failed from {folder}: {ex.Message}");
                data = new ShipCustomizationData();
                textures = new Texture2D[ShipCustomizationData.TotalParts, ShipCustomizationData.TotalLayers];
                return false;
            }
        }
    }
}
