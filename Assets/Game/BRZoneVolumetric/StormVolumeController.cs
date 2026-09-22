using UnityEngine;
using UnityEngine.SceneManagement;
using PirateSlop.World;

namespace PirateSlop
{
    [DefaultExecutionOrder(300)]
    public sealed class StormVolumeController : MonoBehaviour
    {
        public bool EnableStormVolume = true;
        [Min(1)] public float InnerThickness = 80;
        [Min(1)] public float OuterThickness = 120;
        [Min(.1f)] public float EdgeSoftness = 10;
        [Min(20)] public float StormHeight = 260;
        [Min(0)] public float TopInwardOffset = 65;
        [Range(0, 1)] public float DensityMultiplier = .74f;
        [Min(1)] public float NoiseScale = 260;
        public float WindSpeed = 9;
        [Range(0, 1)] public float ShapeContrast = .65f;
        [Range(0, 1)] public float MagicIntensity = .35f;
        public bool FreezeZoneValues;
        [Min(10)] public float ManualRadius = 1400;
        public static StormVolumeController Instance { get; private set; }
        public VolumetricClouds CloudSettings { get; private set; }
        public Vector3 CurrentCenter { get; private set; }
        public float CurrentRadius { get; private set; }
        public float TargetRadius { get; private set; }
        public float WaterLevel { get; private set; }
        public float EffectiveInnerThickness { get; private set; }
        public float EffectiveInwardOffset { get; private set; }
        public bool Ready => isActiveAndEnabled && EnableStormVolume && CurrentRadius > 0 && CloudSettings != null;
        StormZone zone;
        StormCrown crown;
        StormCloudArcController arc;
        BRZoneVisual curtain;
        bool frozen;
        float nextSearch;
        float windDistance;
        float motionTime;
        static readonly int CenterId = Shader.PropertyToID("_StormCenterWater");
        static readonly int BandId = Shader.PropertyToID("_StormBand");
        static readonly int ShapeId = Shader.PropertyToID("_StormShape");
        static readonly int StyleId = Shader.PropertyToID("_StormStyle");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Instance = null;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "NetworkOcean" || Instance != null) return;
            var prefab = Resources.Load<GameObject>("BRStormVolume");
            if (prefab != null) Instantiate(prefab);
        }
        void Awake()
        {
            Instance = this;
            CloudSettings = ScriptableObject.CreateInstance<VolumetricClouds>();
            CloudSettings.hideFlags = HideFlags.HideAndDontSave;
            CloudSettings.cloudPreset = VolumetricClouds.CloudPresets.Custom;
            CloudSettings.state.value = true;
            CloudSettings.localClouds.value = true;
            CloudSettings.densityCurve.value = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.04f, 1), new Keyframe(.8f, 1), new Keyframe(1, 0));
            CloudSettings.erosionCurve.value = AnimationCurve.Linear(0, .8f, 1, .8f);
            CloudSettings.ambientOcclusionCurve.value = new AnimationCurve(new Keyframe(0, .25f), new Keyframe(.35f, .5f), new Keyframe(1, 0));
            CloudSettings.shapeFactor.value = .75f;
            CloudSettings.erosionFactor.value = .35f;
            CloudSettings.erosionScale.value = 1800;
            CloudSettings.microErosion.value = false;
            CloudSettings.numPrimarySteps.value = 128;
            CloudSettings.numLightSteps.value = 4;
            CloudSettings.temporalAccumulationFactor.value = 0;
            CloudSettings.perceptualBlending.value = 0;
            CloudSettings.ambientLightProbeDimmer.value = .65f;
            CloudSettings.sunLightDimmer.value = .55f;
            CloudSettings.scatteringTint.value = new Color(.12f, .06f, .01f);
            CloudSettings.shadows.value = false;
        }
        void LateUpdate()
        {
            windDistance += Time.deltaTime * WindSpeed;
            motionTime += Time.deltaTime;
            if (Time.unscaledTime >= nextSearch && (crown == null || arc == null || curtain == null))
            {
                nextSearch = Time.unscaledTime + 1;
                if (crown == null) crown = FindFirstObjectByType<StormCrown>();
                if (arc == null) arc = FindFirstObjectByType<StormCloudArcController>();
                if (curtain == null) curtain = FindFirstObjectByType<BRZoneVisual>();
            }
            if (crown != null && crown.gameObject.activeSelf) crown.gameObject.SetActive(false);
            if (arc != null && arc.gameObject.activeSelf) arc.gameObject.SetActive(false);
            if (curtain != null && curtain.gameObject.activeSelf) curtain.gameObject.SetActive(false);
            zone = StormZone.Instance;
            if (zone == null) { CurrentRadius = 0; return; }
            if (!FreezeZoneValues || !frozen)
            {
                CurrentCenter = zone.Center;
                WaterLevel = OceanSurface.Instance != null ? OceanSurface.Instance.SeaLevel : CurrentCenter.y;
            }
            frozen = FreezeZoneValues;
            CurrentRadius = FreezeZoneValues ? Mathf.Max(10, ManualRadius) : zone.Radius;
            TargetRadius = zone.TargetRadius;
            float scale = Mathf.Min(1, CurrentRadius * .7f / Mathf.Max(1, InnerThickness + TopInwardOffset));
            EffectiveInnerThickness = Mathf.Max(1, InnerThickness) * scale;
            EffectiveInwardOffset = Mathf.Max(0, TopInwardOffset) * scale;
            CloudSettings.state.value = EnableStormVolume;
            CloudSettings.bottomAltitude.value = Mathf.Max(.01f, WaterLevel);
            CloudSettings.altitudeRange.value = Mathf.Max(100, StormHeight);
            CloudSettings.densityMultiplier.value = DensityMultiplier;
            CloudSettings.shapeScale.value = NoiseScale;
            CloudSettings.globalSpeed.value = WindSpeed;
        }
        public void Apply(Material material)
        {
            material.SetVector(CenterId, new Vector4(CurrentCenter.x, WaterLevel, CurrentCenter.z, TargetRadius));
            material.SetVector(BandId, new Vector4(CurrentRadius, EffectiveInnerThickness, Mathf.Max(1, OuterThickness), Mathf.Max(.1f, EdgeSoftness)));
            material.SetVector(ShapeId, new Vector4(Mathf.Max(20, StormHeight), EffectiveInwardOffset, motionTime, EnableStormVolume ? 1 : 0));
            material.SetVector(StyleId, new Vector4(Mathf.Clamp01(ShapeContrast), Mathf.Clamp01(MagicIntensity), Mathf.Max(1, NoiseScale) / 100000f, windDistance));
        }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (CloudSettings != null) Destroy(CloudSettings);
        }
        void OnDrawGizmosSelected()
        {
            if (CurrentRadius <= 0) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(CurrentCenter.x, WaterLevel + StormHeight * .5f, CurrentCenter.z), new Vector3((CurrentRadius + OuterThickness) * 2, StormHeight, (CurrentRadius + OuterThickness) * 2));
        }
    }
}

