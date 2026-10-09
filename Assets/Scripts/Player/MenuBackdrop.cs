using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PirateSlop
{
    public sealed class MenuBackdrop : MonoBehaviour
    {
        public Camera View;
        public Transform Ship;
        public Material Water;
        public GameObject Content;
        public Vector3 FocusPoint = new(0, 14, 0);
        public float FramingRadius = 28f;
        Vector3 shipRest;
        Quaternion shipRotation;
        Material runtimeWater;
        WaterSystem.Water menuOcean;
        World.TestSkyDayNight menuSky;
        UniversalRenderPipelineAsset menuPipeline;
        Transform menuMonkey;
        bool wasVisible;
        void Awake()
        {
            var currentShip = Resources.Load<GameObject>("Ships/ShipV3Menu");
            if (currentShip != null && Ship != null)
            {
                var previous = Ship;
                var replacement = Instantiate(currentShip, previous.parent).transform;
                replacement.name = "MenuShip";
                replacement.localPosition = previous.localPosition;
                replacement.localRotation = previous.localRotation;
                replacement.localScale = previous.localScale;
                foreach (var part in replacement.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = previous.gameObject.layer;
                previous.gameObject.SetActive(false);
                previous.name = "LegacyMenuShip";
                Ship = replacement;
            }
            FocusPoint = transform.InverseTransformPoint(Ship.TransformPoint(new Vector3(0, 11.5f, -9)));
            FramingRadius = 18f;
            if (View != null) View.farClipPlane = Mathf.Max(View.farClipPlane, 36000f);
            shipRest = Ship.localPosition;
            shipRotation = Ship.localRotation;
            CreateEnvironment();
            if (Water != null && menuOcean == null)
            {
                runtimeWater = Instantiate(Water);
                foreach (var renderer in Content.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterial == Water) renderer.sharedMaterial = runtimeWater;
            }
            PositionView(0f);
        }
        void CreateEnvironment()
        {
            var oceanPrefab = Resources.Load<GameObject>("EnvironmentTest/Ocean");
            var skyPrefab = Resources.Load<GameObject>("EnvironmentTest/SkyDayNight");
            if (oceanPrefab == null || skyPrefab == null || Content == null) return;
            var environment = new GameObject("MenuEnvironment");
            environment.SetActive(false);
            environment.transform.SetParent(Content.transform, false);
            var sea = new GameObject("MenuOcean");
            sea.transform.SetParent(environment.transform, false);
            sea.layer = 30;
            menuOcean = sea.AddComponent<WaterSystem.Water>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(oceanPrefab.GetComponent<WaterSystem.Water>()), menuOcean);
            menuOcean.dynamicSkyReflection = true;
            menuOcean.waveStrength = .55f;
            menuOcean.waveSteepness = .75f;
            menuOcean.whirlpoolRadius = menuOcean.whirlpoolDepth = 0f;
            var sky = Instantiate(skyPrefab, environment.transform);
            menuSky = sky.GetComponent<World.TestSkyDayNight>();
            menuSky.TargetBlend = .05f;
            menuSky.DayHazeDistance = 24000f;
            if (menuSky.Pipeline != null)
            {
                menuPipeline = Instantiate(menuSky.Pipeline);
                menuPipeline.name = "MenuShipPipeline";
                menuPipeline.msaaSampleCount = Mathf.Max(menuPipeline.msaaSampleCount, 4);
                menuPipeline.shadowDistance = Mathf.Max(menuPipeline.shadowDistance, 160f);
                menuPipeline.mainLightShadowmapResolution = Mathf.Max(menuPipeline.mainLightShadowmapResolution, 4096);
                menuPipeline.shadowCascadeCount = 2;
                menuPipeline.cascade2Split = .7f;
                menuSky.Pipeline = menuPipeline;
            }
            foreach (var light in sky.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << 30;
            foreach (var volume in sky.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true)) volume.gameObject.layer = 30;
            var oldSea = Content.transform.Find("MenuSea");
            if (oldSea != null) oldSea.gameObject.SetActive(false);
            var beam = Content.transform.Find("ShipSunBeam");
            if (beam != null) beam.gameObject.SetActive(false);
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.name == "MenuLight" && root.TryGetComponent<Light>(out var oldLight)) oldLight.enabled = false;
            var sceneryPrefab = Resources.Load<GameObject>("Menu/Scenery");
            if (sceneryPrefab != null)
            {
                var scenery = Instantiate(sceneryPrefab, environment.transform);
                scenery.transform.position = Ship.position;
                scenery.transform.rotation = Ship.rotation;
                menuMonkey = scenery.transform.Find("MonkeyOnCrowNest");
                if (menuMonkey != null) menuMonkey.SetParent(Ship, false);
                var perch = scenery.transform.Find("CrowNestPerch");
                if (perch != null) perch.SetParent(Ship, false);
            }
            var stormPrefab = Resources.Load<GameObject>("Menu/StormBackdrop");
            if (stormPrefab != null)
            {
                var storm = Instantiate(stormPrefab, environment.transform).GetComponent<StormVolumeController>();
                storm.ConfigureMenuPreview(View, Ship.position, 24000f, environment.transform.position.y);
            }
            var data = View.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.requiresColorTexture = true;
            data.requiresDepthTexture = true;
            data.volumeLayerMask |= 1 << 30;
            View.allowHDR = true;
            View.clearFlags = CameraClearFlags.Skybox;
            environment.SetActive(true);
        }
        public void PositionView(float time)
        {
            if (View == null) return;
            float tangent = Mathf.Tan(View.fieldOfView * Mathf.Deg2Rad * .5f);
            float distance = Mathf.Max(FramingRadius / (tangent * .8f), FramingRadius / (tangent * Mathf.Max(.6f, View.aspect) * .48f));
            Vector3 focus = transform.TransformPoint(FocusPoint);
            Vector3 offset = Ship.TransformDirection(new Vector3(-.2f, .19f, -1f).normalized) * distance;
            View.transform.position = focus + offset + new Vector3(Mathf.Sin(time * .08f) * 1.2f, Mathf.Sin(time * .12f) * .35f, 0);
            Vector3 right = Vector3.Cross(Vector3.up, -offset.normalized).normalized;
            View.transform.LookAt(focus - right * (distance * tangent * View.aspect * .34f));
            if (menuMonkey != null)
            {
                var facing = Ship.InverseTransformDirection(View.transform.position - menuMonkey.position);
                facing.y = 0;
                if (facing.sqrMagnitude > .01f) menuMonkey.localRotation = Quaternion.LookRotation(facing);
            }
        }
        void Start()
        {
            ApplyShipCustomization();
        }

        public void ApplyShipCustomization()
        {
            if (Ship != null)
            {
                var customizer = Ship.GetComponent<PirateSlop.Customization.SailCustomizer>();
                if (customizer == null) customizer = Ship.gameObject.AddComponent<PirateSlop.Customization.SailCustomizer>();
                customizer.InitializeSails();
                if (PirateSlop.Customization.SailCustomizationStorage.Load(out var savedData, out var savedTextures))
                {
                    customizer.ApplyCustomization(savedData, savedTextures, false);
                }
            }
        }

        void Update()
        {
            if (PirateSlop.Customization.SailCustomizationUI.IsOpen) return;
            bool visible=View!=null && View.gameObject.activeInHierarchy;
            if (visible && !wasVisible && menuSky != null) menuSky.enabled = true;
            Content.SetActive(visible);
            if(visible!=wasVisible)
            {
                wasVisible=visible;
                if (menuSky == null)
                    foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                        if(light.type==LightType.Directional && light.enabled && ((light.cullingMask & (1<<30))!=0)==visible) { RenderSettings.sun=light; break; }
            }
            if(!visible) return;
            float t=Time.unscaledTime;
            Ship.localPosition=shipRest+Vector3.up*Mathf.Sin(t*.55f)*.25f;
            Ship.localRotation=shipRotation * Quaternion.Euler(Mathf.Sin(t*.4f)*.6f,0f,Mathf.Sin(t*.65f)*.9f);
            PositionView(t);
            if (menuOcean != null) menuOcean.waveTime = t;
            if(runtimeWater!=null)
            {
                runtimeWater.SetFloat("_UseWaveTime",1f);
                runtimeWater.SetFloat("_WaveTime",t);
            }
            GameAudio.Ambience(.15f);
        }
        void OnDestroy()
        {
            if (menuSky != null) menuSky.enabled = false;
            if (menuPipeline != null) Destroy(menuPipeline);
            if (runtimeWater != null) Destroy(runtimeWater);
        }
    }
}
