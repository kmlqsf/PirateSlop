using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop
{
    public enum ColorblindMode { Off, Protanopia, Deuteranopia, Tritanopia }

    public static class GameSettings
    {
        public const float DefaultFov = 80f;
        static bool loaded;
        static float sensitivity, fov, brightness;
        static bool invertY, hitmarkers, damageIndicators, compass, captions;
        static ColorblindMode colorblind;
        static Volume volume;
        static ColorAdjustments adjustments;
        static ChannelMixer mixer;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
            invertY = PlayerPrefs.GetInt("InvertY", 0) == 1;
            fov = PlayerPrefs.GetFloat("FieldOfView", DefaultFov);
            brightness = PlayerPrefs.GetFloat("Brightness", 0f);
            hitmarkers = PlayerPrefs.GetInt("ShowHitmarkers", 1) == 1;
            damageIndicators = PlayerPrefs.GetInt("ShowDamageIndicators", 1) == 1;
            compass = PlayerPrefs.GetInt("ShowCompass", 1) == 1;
            captions = PlayerPrefs.GetInt("Captions", 0) == 1;
            colorblind = (ColorblindMode)Mathf.Clamp(PlayerPrefs.GetInt("ColorblindMode", 0), 0, 3);
        }

        public static float MouseSensitivity { get { Load(); return sensitivity; } set { Load(); sensitivity = Mathf.Clamp(value, .05f, 5f); PlayerPrefs.SetFloat("MouseSensitivity", sensitivity); } }
        public static bool InvertY { get { Load(); return invertY; } set { Load(); invertY = value; PlayerPrefs.SetInt("InvertY", value ? 1 : 0); } }
        public static bool Hitmarkers { get { Load(); return hitmarkers; } set { Load(); hitmarkers = value; PlayerPrefs.SetInt("ShowHitmarkers", value ? 1 : 0); } }
        public static bool DamageIndicators { get { Load(); return damageIndicators; } set { Load(); damageIndicators = value; PlayerPrefs.SetInt("ShowDamageIndicators", value ? 1 : 0); } }
        public static bool Compass { get { Load(); return compass; } set { Load(); compass = value; PlayerPrefs.SetInt("ShowCompass", value ? 1 : 0); } }
        public static bool Captions { get { Load(); return captions; } set { Load(); captions = value; PlayerPrefs.SetInt("Captions", value ? 1 : 0); } }

        public static float Fov
        {
            get { Load(); return fov; }
            set { Load(); fov = Mathf.Clamp(Mathf.Round(value), 60f, 110f); PlayerPrefs.SetFloat("FieldOfView", fov); }
        }

        public static float Brightness
        {
            get { Load(); return brightness; }
            set { Load(); brightness = Mathf.Clamp(value, -1f, 1f); PlayerPrefs.SetFloat("Brightness", brightness); ApplyPost(); }
        }

        public static ColorblindMode Colorblind
        {
            get { Load(); return colorblind; }
            set { Load(); colorblind = value; PlayerPrefs.SetInt("ColorblindMode", (int)value); ApplyPost(); }
        }

        public static string ColorblindName(ColorblindMode mode) => Localization.T(mode switch
        {
            ColorblindMode.Protanopia => "Протанопия",
            ColorblindMode.Deuteranopia => "Дейтеранопия",
            ColorblindMode.Tritanopia => "Тританопия",
            _ => "Выключен"
        });

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Startup()
        {
            Load();
            if (PlayerPrefs.HasKey("QualityLevel"))
            {
                int quality = PlayerPrefs.GetInt("QualityLevel");
                if (quality >= 0 && quality < QualitySettings.names.Length) QualitySettings.SetQualityLevel(quality, true);
            }
            if (PlayerPrefs.HasKey("VSync")) QualitySettings.vSyncCount = PlayerPrefs.GetInt("VSync") == 1 ? 1 : 0;
            ApplyPost();
        }

        static void EnsureVolume()
        {
            if (volume != null) return;
            var host = new GameObject("PlayerSettingsVolume") { hideFlags = HideFlags.DontSave };
            Object.DontDestroyOnLoad(host);
            volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            adjustments = profile.Add<ColorAdjustments>();
            mixer = profile.Add<ChannelMixer>();
            volume.sharedProfile = profile;
        }

        static readonly float[][] simulation =
        {
            new[] { .567f, .433f, 0f, .558f, .442f, 0f, 0f, .242f, .758f },
            new[] { .625f, .375f, 0f, .7f, .3f, 0f, 0f, .3f, .7f },
            new[] { .95f, .05f, 0f, 0f, .433f, .567f, 0f, .475f, .525f }
        };

        static void ApplyPost()
        {
            Load();
            if (Application.isBatchMode) return;
            if (volume == null && Mathf.Approximately(brightness, 0f) && colorblind == ColorblindMode.Off) return;
            EnsureVolume();
            adjustments.active = !Mathf.Approximately(brightness, 0f);
            adjustments.postExposure.Override(brightness);
            mixer.active = colorblind != ColorblindMode.Off;
            if (!mixer.active) return;
            var s = simulation[(int)colorblind - 1];
            var e = new float[9];
            for (int i = 0; i < 9; i++) e[i] = (i % 4 == 0 ? 1f : 0f) - s[i];
            var m = new float[9];
            for (int col = 0; col < 3; col++)
            {
                m[col] = (col == 0 ? 1f : 0f);
                m[3 + col] = (col == 1 ? 1f : 0f) + .7f * e[col] + e[3 + col];
                m[6 + col] = (col == 2 ? 1f : 0f) + .7f * e[col] + e[6 + col];
            }
            mixer.redOutRedIn.Override(m[0] * 100f); mixer.redOutGreenIn.Override(m[1] * 100f); mixer.redOutBlueIn.Override(m[2] * 100f);
            mixer.greenOutRedIn.Override(m[3] * 100f); mixer.greenOutGreenIn.Override(m[4] * 100f); mixer.greenOutBlueIn.Override(m[5] * 100f);
            mixer.blueOutRedIn.Override(m[6] * 100f); mixer.blueOutGreenIn.Override(m[7] * 100f); mixer.blueOutBlueIn.Override(m[8] * 100f);
        }
    }
}
