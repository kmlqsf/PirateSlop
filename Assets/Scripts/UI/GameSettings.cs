using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop
{
    public enum ColorblindMode { Off, Protanopia, Deuteranopia, Tritanopia }

    public enum PicturePreset { Scene, SeaLight, Trailer, Custom }

    public struct PictureLook
    {
        public int contrast, saturation, splitTone, bloom, vignette, sunlight, seaDetail;

        public PictureLook(int contrast, int saturation, int splitTone, int bloom, int vignette, int sunlight, int seaDetail)
        {
            this.contrast = contrast;
            this.saturation = saturation;
            this.splitTone = splitTone;
            this.bloom = bloom;
            this.vignette = vignette;
            this.sunlight = sunlight;
            this.seaDetail = seaDetail;
        }
    }

    public static class GameSettings
    {
        public const float DefaultFov = 80f;
        static bool loaded;
        static float sensitivity, fov, brightness;
        static bool invertY, hitmarkers, damageIndicators, compass, captions;
        static ColorblindMode colorblind;
        static readonly PictureLook seaLight = new PictureLook(7, 5, 22, 17, 8, 45, 40);
        static readonly PictureLook trailer = new PictureLook(13, 9, 52, 26, 15, 85, 75);
        static int observerSmoothing;
        static PicturePreset preset;
        static PictureLook custom;
        static Volume volume, menuVolume;
        static SplitToning toning;
        static Bloom bloom;
        static Vignette vignette;
        static ColorAdjustments adjustments;
        static ChannelMixer mixer;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
            invertY = PlayerPrefs.GetInt("InvertY", 0) == 1;
            fov = PlayerPrefs.GetFloat("FieldOfView", DefaultFov);
            preset = (PicturePreset)Mathf.Clamp(PlayerPrefs.GetInt("PicturePreset", (int)PicturePreset.SeaLight), 0, 3);
            observerSmoothing = Mathf.Clamp(PlayerPrefs.GetInt("ObserverSmoothing", 35), 0, 100);
            custom = new PictureLook(
                PlayerPrefs.GetInt("PictureContrast", seaLight.contrast),
                PlayerPrefs.GetInt("PictureSaturation", seaLight.saturation),
                PlayerPrefs.GetInt("PictureSplitTone", seaLight.splitTone),
                PlayerPrefs.GetInt("PictureBloom", seaLight.bloom),
                PlayerPrefs.GetInt("PictureVignette", seaLight.vignette),
                PlayerPrefs.GetInt("PictureSunlight", seaLight.sunlight),
                PlayerPrefs.GetInt("PictureSeaDetail", seaLight.seaDetail));
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

        public static PicturePreset Preset
        {
            get { Load(); return preset; }
            set { Load(); preset = value; PlayerPrefs.SetInt("PicturePreset", (int)value); ApplyPost(); }
        }

        public static PictureLook Look
        {
            get { Load(); return preset switch { PicturePreset.Custom => custom, PicturePreset.Trailer => trailer, PicturePreset.SeaLight => seaLight, _ => default }; }
        }

        public static void SetCustomLook(PictureLook look)
        {
            Load();
            custom = look;
            PlayerPrefs.SetInt("PictureContrast", look.contrast);
            PlayerPrefs.SetInt("PictureSaturation", look.saturation);
            PlayerPrefs.SetInt("PictureSplitTone", look.splitTone);
            PlayerPrefs.SetInt("PictureBloom", look.bloom);
            PlayerPrefs.SetInt("PictureVignette", look.vignette);
            PlayerPrefs.SetInt("PictureSunlight", look.sunlight);
            PlayerPrefs.SetInt("PictureSeaDetail", look.seaDetail);
            Preset = PicturePreset.Custom;
        }

        public static int ObserverSmoothing
        {
            get { Load(); return observerSmoothing; }
            set { Load(); observerSmoothing = Mathf.Clamp(value, 0, 100); PlayerPrefs.SetInt("ObserverSmoothing", observerSmoothing); }
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
            toning = profile.Add<SplitToning>();
            bloom = profile.Add<Bloom>();
            vignette = profile.Add<Vignette>();
            volume.sharedProfile = profile;

            var menuHost = new GameObject("MenuSettingsVolume") { layer = 30 };
            menuHost.transform.SetParent(host.transform, false);
            menuVolume = menuHost.AddComponent<Volume>();
            menuVolume.isGlobal = true;
            menuVolume.priority = volume.priority;
            menuVolume.sharedProfile = profile;
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
            bool grade = preset != PicturePreset.Scene;
            bool expose = !Mathf.Approximately(brightness, 0f);
            bool needed = grade || expose || colorblind != ColorblindMode.Off;
            if (volume == null && !needed) return;
            EnsureVolume();
            volume.enabled = menuVolume.enabled = needed;
            var look = Look;
            adjustments.active = grade || expose;
            adjustments.postExposure.value = brightness;
            adjustments.postExposure.overrideState = expose;
            adjustments.contrast.value = look.contrast;
            adjustments.saturation.value = look.saturation;
            adjustments.contrast.overrideState = adjustments.saturation.overrideState = grade;
            toning.active = grade && look.splitTone > 0;
            float tint = look.splitTone * .01f;
            toning.shadows.Override(Color.Lerp(Color.gray, new Color(.43f, .49f, .56f), tint));
            toning.highlights.Override(Color.Lerp(Color.gray, new Color(.59f, .53f, .44f), tint));
            bloom.active = vignette.active = grade;
            bloom.intensity.Override(look.bloom * .01f);
            bloom.threshold.Override(1.15f);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(4);
            bloom.highQualityFiltering.Override(false);
            vignette.intensity.Override(look.vignette * .01f);
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
