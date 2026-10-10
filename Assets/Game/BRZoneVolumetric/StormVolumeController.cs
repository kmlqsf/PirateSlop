using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PirateSlop.World;

namespace PirateSlop
{
    [DefaultExecutionOrder(300)]
    public sealed partial class StormVolumeController : MonoBehaviour
    {
        public bool EnableStormVolume = true;
        [Min(1)] public float InnerThickness = 80;
        [Min(1)] public float OuterThickness = 120;
        [Min(.1f)] public float EdgeSoftness = 10;
        [Min(1)] public float WeatherTransitionWidth = 40;
        [Min(1)] public float CloudTransitionWidth = 600;
        [Min(0)] public float CloudEdgeBreakup = 240;
        [Min(81)] public float CloudEdgeRiseHeight = 1200;
        [Min(20)] public float StormHeight = 3200;
        [Min(0)] public float TopInwardOffset = 65;
        [Range(0, 1)] public float DensityMultiplier = .74f;
        [Min(1)] public float NoiseScale = 260;
        public float WindSpeed = 9;
        [Range(0, 1)] public float ShapeContrast = .65f;
        [Range(0, 1)] public float MagicIntensity = .35f;
        [Min(1)] public float FeedbackDistance = 100;
        [Range(0, 1)] public float FeedbackStrength = 1;
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
        public bool Ready => isActiveAndEnabled && EnableStormVolume && CurrentRadius > 0 && CloudSettings != null && (!TestCloudWall || testWallDensity != null && testWallNormals != null && smokeNoise != null);
        public bool IsMenuPreview { get; private set; }
        public bool TestCloudWall => !IsMenuPreview && EnvironmentTestGallery.IsTest(ProceduralWorld.Instance != null ? ProceduralWorld.Instance.Layout : null);
        public Camera PreviewCamera { get; private set; }
        Vector3 previewCenter;
        float previewRadius, previewWaterLevel;
        StormZone zone;
        StormCrown crown;
        StormCloudArcController arc;
        BRZoneVisual curtain;
        bool frozen;
        float nextSearch;
        float windDistance;
        float motionTime;
        float testRollPhase;
        Volume feedbackVolume;
        VolumeProfile feedbackProfile;
        Texture3D testWallDensity;
        Texture3D testWallNormals;
        GameObject testShipSource;
        float testShipTop;
        float testRadiusVelocity;
        float testSmokeAdvance;
        float testSmokeAdvanceVelocity;
        float testSmokeTravel;
        float testSmokeFall;
        Camera feedbackCamera;
        static readonly int CenterId = Shader.PropertyToID("_StormCenterWater");
        static readonly int BandId = Shader.PropertyToID("_StormBand");
        static readonly int ShapeId = Shader.PropertyToID("_StormShape");
        static readonly int StyleId = Shader.PropertyToID("_StormStyle");
        static readonly int VisibilityId = Shader.PropertyToID("_StormVisibilityDistance");
        static readonly int WeatherId = Shader.PropertyToID("_PirateStormWeather");
        static readonly int CloudBoundaryId = Shader.PropertyToID("_PirateStormCloudBoundary");
        static readonly int BackdropId = Shader.PropertyToID("_PirateStormBackdrop");
        static readonly int CloudBandId = Shader.PropertyToID("_PirateStormCloudBand");
        static readonly int NearMediumId = Shader.PropertyToID("_PirateStormNearMedium");
        static readonly int TestCloudsId = Shader.PropertyToID("_PirateStormTestClouds");
        static readonly int TestDensityId = Shader.PropertyToID("_StormTestWallDensity");
        static readonly int TestNormalsId = Shader.PropertyToID("_StormTestWallNormals");
        static readonly int TestBaseId = Shader.PropertyToID("_PirateStormTestCloudBase");
        static readonly int TestSkyId = Shader.PropertyToID("_PirateStormTestSkyWeather");
        static readonly int TestSmokeMapId = Shader.PropertyToID("_StormTestSmokeMap");
        static readonly int TestSmokeMotionId = Shader.PropertyToID("_StormTestSmokeMotion");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Instance = null;
            Shader.SetGlobalVector(TestCloudsId, Vector4.zero);
            Shader.SetGlobalVector(TestSmokeMotionId, Vector4.zero);
            Shader.SetGlobalFloat(TestBaseId, 0);
            Shader.SetGlobalVector(TestSkyId, Vector4.zero);
            Shader.SetGlobalVector(WeatherId, Vector4.zero);
            Shader.SetGlobalVector(CloudBoundaryId, Vector4.zero);
            Shader.SetGlobalVector(BackdropId, Vector4.zero);
            Shader.SetGlobalVector(CloudBandId, Vector4.zero);
            Shader.SetGlobalFloat(NearMediumId, 0);
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "NetworkOcean" || Instance != null && !Instance.IsMenuPreview) return;
            var prefab = Resources.Load<GameObject>("BRStormVolume");
            if (prefab != null) Instantiate(prefab);
        }
        void Awake()
        {
            ClaimOwner();
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
            CloudSettings.numPrimarySteps.value = IsMenuPreview ? 96 : 40;
            CloudSettings.numLightSteps.value = IsMenuPreview ? 3 : 2;
            CloudSettings.temporalAccumulationFactor.value = IsMenuPreview ? 0 : .85f;
            CloudSettings.perceptualBlending.value = 0;
            CloudSettings.ambientLightProbeDimmer.value = .65f;
            CloudSettings.sunLightDimmer.value = .55f;
            CloudSettings.scatteringTint.value = new Color(.12f, .06f, .01f);
            CloudSettings.shadows.value = false;
            if (IsMenuPreview) return;
            feedbackProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            feedbackProfile.name = "StormBoundaryFeedback";
            feedbackProfile.hideFlags = HideFlags.HideAndDontSave;
            var vignette = feedbackProfile.Add<Vignette>();
            vignette.intensity.Override(.24f);
            vignette.smoothness.Override(.64f);
            vignette.color.Override(new Color(.045f, .052f, .056f));
            var grading = feedbackProfile.Add<ColorAdjustments>();
            grading.saturation.Override(-12);
            grading.postExposure.Override(-.12f);
            grading.colorFilter.Override(new Color(.96f, .98f, 1));
            feedbackVolume = gameObject.AddComponent<Volume>();
            feedbackVolume.isGlobal = true;
            feedbackVolume.priority = 60;
            feedbackVolume.weight = 0;
            feedbackVolume.sharedProfile = feedbackProfile;
        }
        void LateUpdate()
        {
            if (IsMenuPreview && Instance == null) ClaimOwner();
            if (Instance != this) return;
            float delta = IsMenuPreview ? Time.unscaledDeltaTime : Time.deltaTime;
            windDistance += delta * WindSpeed;
            motionTime += delta;
            if (TestCloudWall) testRollPhase = Mathf.Repeat(testRollPhase + delta * .16f, Mathf.PI * 2);
            if (IsMenuPreview)
            {
                CurrentCenter = previewCenter;
                CurrentRadius = TargetRadius = previewRadius;
                WaterLevel = previewWaterLevel;
                UpdateVolume();
                return;
            }
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
            if (zone == null) { CurrentRadius = 0; feedbackVolume.weight = 0; Shader.SetGlobalVector(TestCloudsId, Vector4.zero); Shader.SetGlobalVector(TestSmokeMotionId, Vector4.zero); Shader.SetGlobalVector(TestSkyId, Vector4.zero); Shader.SetGlobalVector(WeatherId, Vector4.zero); Shader.SetGlobalVector(CloudBoundaryId, Vector4.zero); Shader.SetGlobalVector(BackdropId, Vector4.zero);
            Shader.SetGlobalFloat(TestBaseId, 0);
            Shader.SetGlobalVector(CloudBandId, Vector4.zero);
            Shader.SetGlobalFloat(NearMediumId, 0); return; }
            if (!FreezeZoneValues || !frozen)
            {
                CurrentCenter = zone.Center;
                WaterLevel = OceanSurface.Instance != null ? OceanSurface.Instance.SeaLevel : CurrentCenter.y;
            }
            frozen = FreezeZoneValues;
            float radius = FreezeZoneValues ? Mathf.Max(10, ManualRadius) : zone.Radius;
            if (TestCloudWall && !FreezeZoneValues && CurrentRadius > 0)
                CurrentRadius = Mathf.SmoothDamp(CurrentRadius, radius, ref testRadiusVelocity, .24f, Mathf.Infinity, delta);
            else
            {
                CurrentRadius = radius;
                testRadiusVelocity = 0;
            }
            TargetRadius = zone.TargetRadius;
            if (TestCloudWall)
            {
                float advance = !FreezeZoneValues && !zone.Paused && zone.Progress < 1
                    ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .4f, -testRadiusVelocity)) : 0;
                testSmokeAdvance = Mathf.SmoothDamp(testSmokeAdvance, advance, ref testSmokeAdvanceVelocity, 1.25f, Mathf.Infinity, delta);
                testSmokeTravel += delta * testSmokeAdvance * (5 + Mathf.Clamp(-testRadiusVelocity, 0, 12));
                testSmokeFall += delta * Mathf.Lerp(3.5f, 9f, testSmokeAdvance);
            }
            UpdateVolume();
            UpdateFeedback();
        }
        void UpdateVolume()
        {
            bool testWall = TestCloudWall;
            if (testWall) UpdateSmokeFields();
            if (testWall && testWallDensity == null) testWallDensity = Resources.Load<Texture3D>("StormTestCloudWallDensity");
            if (testWall && testWallNormals == null) testWallNormals = Resources.Load<Texture3D>("StormTestCloudWallNormals");
            var layout = testWall ? ProceduralWorld.Instance.Layout : null;
            float bottom = WaterLevel - (testWall ? layout.Depth : 0);
            float volumeHeight = testWall ? TestCloudHeight() : Mathf.Max(100, StormHeight);
            float scale = Mathf.Min(1, CurrentRadius * .7f / Mathf.Max(1, InnerThickness + TopInwardOffset));
            EffectiveInnerThickness = testWall ? 2 : Mathf.Max(1, InnerThickness) * scale;
            EffectiveInwardOffset = testWall ? 0 : Mathf.Max(0, TopInwardOffset) * scale;
            CloudSettings.state.value = EnableStormVolume;
            CloudSettings.bottomAltitude.value = Mathf.Max(.01f, WaterLevel);
            CloudSettings.altitudeRange.value = volumeHeight;
            CloudSettings.densityMultiplier.value = testWall ? .95f : DensityMultiplier;
            CloudSettings.numPrimarySteps.value = IsMenuPreview ? 96 : testWall ? 48 : 40;
            CloudSettings.numLightSteps.value = IsMenuPreview ? 3 : testWall ? 1 : 2;
            CloudSettings.temporalAccumulationFactor.value = IsMenuPreview || testWall ? 0 : .85f;
            CloudSettings.shapeScale.value = NoiseScale;
            CloudSettings.globalSpeed.value = testWall ? 0 : WindSpeed;
            Shader.SetGlobalVector(TestCloudsId, Ready && testWall
                ? new Vector4(1, bottom, WaterLevel + volumeHeight, Mathf.Max(1, CurrentRadius * Mathf.PI * 2 / 64f))
                : Vector4.zero);
            Shader.SetGlobalVector(TestSmokeMotionId, Ready && testWall
                ? new Vector4(testSmokeAdvance, testSmokeTravel, testSmokeFall, 0)
                : Vector4.zero);
            var sea = testWall ? OceanSurface.Instance : null;
            float maximumWave = sea != null && sea.HeightSource is BoatAttackOcean boat ? boat.MaximumWaveHeight : 0;
            Shader.SetGlobalFloat(TestBaseId, Ready && testWall ? WaterLevel + maximumWave + .2f : 0);
            Shader.SetGlobalVector(TestSkyId, Ready && testWall
                ? new Vector4(CurrentCenter.x, CurrentCenter.z, CurrentRadius, 240)
                : Vector4.zero);
            Shader.SetGlobalVector(WeatherId, Ready && !testWall
                ? new Vector4(CurrentCenter.x, CurrentCenter.z, CurrentRadius, Mathf.Max(1, WeatherTransitionWidth))
                : Vector4.zero);
            Shader.SetGlobalVector(CloudBoundaryId, Ready
                ? new Vector4(WaterLevel, Mathf.Max(1, CloudTransitionWidth), Mathf.Max(0, CloudEdgeBreakup), Mathf.Max(81, CloudEdgeRiseHeight))
                : Vector4.zero);
            Shader.SetGlobalVector(CloudBandId, Ready
                ? new Vector4(EffectiveInnerThickness, Mathf.Max(1, OuterThickness), EffectiveInwardOffset, Mathf.Max(20, StormHeight))
                : Vector4.zero);
            Shader.SetGlobalVector(BackdropId, Ready && IsMenuPreview
                ? new Vector4(1, motionTime, StormHeight, windDistance)
                : Vector4.zero);
            float nearMedium = 0;
            var camera = Camera.main;
            var rain = StormRainController.Instance;
            if (Ready && !IsMenuPreview && !testWall && camera != null && rain != null && rain.isActiveAndEnabled
                && !rain.Submerged && rain.RainMaterial != null && OceanSurface.Instance != null && !PirateSlop.Networking.SessionController.MenuOpen && Shader.GetGlobalFloat("_PirateStormBillows") > .5f)
            {
                var eye = camera.transform.position;
                float height = eye.y - WaterLevel;
                float distance = Vector2.Distance(new Vector2(eye.x,eye.z),new Vector2(CurrentCenter.x,CurrentCenter.z)) - CurrentRadius
                    + height / Mathf.Max(20,StormHeight) * .3f * EffectiveInwardOffset;
                float feather = Mathf.Min(2,(EffectiveInnerThickness + OuterThickness)*.12f);
                if (height >= 0 && height <= 35)
                    nearMedium = Mathf.SmoothStep(0,1,Mathf.InverseLerp(-EffectiveInnerThickness,-EffectiveInnerThickness+feather,distance))
                        * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(OuterThickness-feather,OuterThickness,distance)))
                        * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(30,35,height)));
            }
            Shader.SetGlobalFloat(NearMediumId, nearMedium);
        }
        float TestCloudHeight()
        {
            var session = Networking.SessionController.Instance;
            var source = session != null && session.ShipPrefab != null ? session.ShipPrefab.gameObject : null;
            if (source != null && source != testShipSource)
            {
                testShipSource = source;
                testShipTop = 0;
                foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
                    bool visible = true;
                    for (var parent = renderer.transform; parent != null; parent = parent.parent)
                        if (!parent.gameObject.activeSelf) { visible = false; break; }
                    if (!visible) continue;
                    var bounds = renderer.localBounds;
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                            for (int z = -1; z <= 1; z += 2)
                            {
                                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                                var point = source.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                                testShipTop = Mathf.Max(testShipTop, point.y * source.transform.localScale.y);
                            }
                }
            }
            return Mathf.Max(20, testShipTop + 10) + 15;
        }
        public void ConfigureMenuPreview(Camera view, Vector3 center, float radius, float waterLevel)
        {
            IsMenuPreview = true;
            if (CloudSettings != null)
            {
                CloudSettings.numPrimarySteps.value = 96;
                CloudSettings.numLightSteps.value = 3;
                CloudSettings.temporalAccumulationFactor.value = 0;
            }
            PreviewCamera = view;
            previewCenter = center;
            previewRadius = Mathf.Max(1000, radius);
            previewWaterLevel = waterLevel;
            FeedbackStrength = 0;
        }
        public bool RendersCamera(Camera camera) => !IsMenuPreview || camera == PreviewCamera;
        void ClaimOwner()
        {
            if (Instance == null || Instance == this || !IsMenuPreview && Instance.IsMenuPreview) Instance = this;
        }
        void OnEnable() => ClaimOwner();
        void UpdateFeedback()
        {
            if (feedbackCamera == null || !feedbackCamera.isActiveAndEnabled) feedbackCamera = Camera.main;
            float target = 0;
            if (Ready && !TestCloudWall && feedbackCamera != null)
            {
                var position = feedbackCamera.transform.position;
                float signedDistance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(zone.Center.x, zone.Center.z)) - zone.Radius;
                float approach = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(-signedDistance / Mathf.Max(1, FeedbackDistance)));
                float crossing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-12, 18, signedDistance));
                target = Mathf.Clamp01(FeedbackStrength) * (approach * .35f + crossing * .65f);
            }
            feedbackVolume.weight = Mathf.Lerp(feedbackVolume.weight, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 3));
        }
        public void Apply(Material material)
        {
            bool testWall = TestCloudWall;
            if (testWall)
            {
                material.SetTexture(TestDensityId, testWallDensity);
                material.SetTexture(TestNormalsId, testWallNormals);
                material.SetVector(TestSmokeMapId, new Vector4(0, 0, ProceduralWorld.Instance.Layout.Radius, motionTime));
            }
            material.SetVector(CenterId, new Vector4(CurrentCenter.x, WaterLevel, CurrentCenter.z, TargetRadius));
            material.SetVector(BandId, new Vector4(CurrentRadius, EffectiveInnerThickness, testWall ? 18 : Mathf.Max(1, OuterThickness), testWall ? .6f : Mathf.Max(.1f, EdgeSoftness)));
            float roll = testWall ? Mathf.Repeat(testRollPhase + (ProceduralWorld.Instance.Layout.Radius - CurrentRadius) * .055f, Mathf.PI * 2) : motionTime;
            material.SetVector(ShapeId, new Vector4(testWall ? TestCloudHeight() : Mathf.Max(20, StormHeight), EffectiveInwardOffset, roll, EnableStormVolume ? 1 : 0));
            material.SetVector(StyleId, new Vector4(Mathf.Clamp01(ShapeContrast), Mathf.Clamp01(MagicIntensity), Mathf.Max(1, NoiseScale) / 100000f, windDistance));
            material.SetFloat(VisibilityId, IsMenuPreview ? Mathf.Max(10000, CurrentRadius * 2) : 10000);
        }
        public void ApplyTestCloudField(Material material)
        {
            material.SetTexture(TestDensityId, testWallDensity);
            material.SetTexture(TestNormalsId, testWallNormals);
            material.SetVector(CenterId, new Vector4(CurrentCenter.x, WaterLevel, CurrentCenter.z, TargetRadius));
            material.SetVector(BandId, new Vector4(CurrentRadius, 2, 18, .6f));
            float roll = Mathf.Repeat(testRollPhase + (ProceduralWorld.Instance.Layout.Radius - CurrentRadius) * .055f, Mathf.PI * 2);
            material.SetVector(ShapeId, new Vector4(TestCloudHeight(), 0, roll, EnableStormVolume ? 1 : 0));
        }
        void OnDestroy()
        {
            ReleaseSmokeFields();
            if (Instance == this)
            {
                Instance = null;
                Shader.SetGlobalVector(TestCloudsId, Vector4.zero);
                Shader.SetGlobalVector(TestSmokeMotionId, Vector4.zero);
                Shader.SetGlobalVector(TestSkyId, Vector4.zero);
                Shader.SetGlobalFloat(TestBaseId, 0);
                Shader.SetGlobalVector(WeatherId, Vector4.zero);
                Shader.SetGlobalVector(CloudBoundaryId, Vector4.zero);
                Shader.SetGlobalVector(BackdropId, Vector4.zero);
            Shader.SetGlobalVector(CloudBandId, Vector4.zero);
            Shader.SetGlobalFloat(NearMediumId, 0);
            }
            if (CloudSettings != null) Destroy(CloudSettings);
            if (feedbackVolume != null) Destroy(feedbackVolume);
            if (feedbackProfile != null)
            {
                foreach (var component in feedbackProfile.components) if (component != null) Destroy(component);
                Destroy(feedbackProfile);
            }
        }
        void OnDisable()
        {
            ReleaseSmokeFields();
            if (feedbackVolume != null) feedbackVolume.weight = 0;
            if (Instance == this)
            {
                Instance = null;
                Shader.SetGlobalVector(TestCloudsId, Vector4.zero);
                Shader.SetGlobalVector(TestSmokeMotionId, Vector4.zero);
                Shader.SetGlobalVector(TestSkyId, Vector4.zero);
                Shader.SetGlobalFloat(TestBaseId, 0);
                Shader.SetGlobalVector(WeatherId, Vector4.zero);
                Shader.SetGlobalVector(CloudBoundaryId, Vector4.zero);
                Shader.SetGlobalVector(BackdropId, Vector4.zero);
            Shader.SetGlobalVector(CloudBandId, Vector4.zero);
            Shader.SetGlobalFloat(NearMediumId, 0);
            }
        }
        void OnDrawGizmosSelected()
        {
            if (CurrentRadius <= 0) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(CurrentCenter.x, WaterLevel + StormHeight * .5f, CurrentCenter.z), new Vector3((CurrentRadius + OuterThickness) * 2, StormHeight, (CurrentRadius + OuterThickness) * 2));
        }
    }
}
