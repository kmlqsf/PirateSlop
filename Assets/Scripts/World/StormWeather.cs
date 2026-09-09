using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.World
{
    public sealed class StormWeather : MonoBehaviour
    {
        Camera target;
        UniversalAdditionalCameraData cameraData;
        bool previousDepth;
        GameObject screen;
        Mesh screenMesh;
        Material atmosphere, rainMaterial;
        ParticleSystem rain;
        AudioSource rainfall;
        AudioClip rainClip;
        float intensity, wetness, exposure = 1, shelterAt;
        readonly RaycastHit[] shelterHits = new RaycastHit[24];
        public float Intensity => intensity;
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCamera; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeforeCamera; }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (screen != null) screen.GetComponent<MeshRenderer>().enabled = camera == target;
        }

        void Start()
        {
            atmosphere = new Material(Resources.Load<Shader>("StormWeather"));
            rainMaterial = new Material(Resources.Load<Shader>("StormRain"));
            screenMesh = new Mesh { name = "StormWeatherScreen" };
            screenMesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) };
            screenMesh.triangles = new[] { 0,1,2,0,2,3 };
            screenMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
            screen = new GameObject("StormAtmosphere", typeof(MeshFilter), typeof(MeshRenderer));
            screen.GetComponent<MeshFilter>().sharedMesh = screenMesh;
            var screenRenderer = screen.GetComponent<MeshRenderer>();
            screenRenderer.sharedMaterial = atmosphere;
            screenRenderer.shadowCastingMode = ShadowCastingMode.Off;
            screenRenderer.receiveShadows = false;
            screen.SetActive(false);
            var rainObject = new GameObject("WindDrivenRain");
            rainObject.transform.SetParent(transform, false);
            rain = rainObject.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.25f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f);
            main.startColor = new Color(.66f,.74f,.78f,.32f);
            main.maxParticles = 4500;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(48, 16, 48);
            var emission = rain.emission;
            emission.rateOverTime = 0;
            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 10; velocity.y = -30; velocity.z = 5;
            var collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.enableDynamicColliders = true;
            collision.lifetimeLoss = 1;
            collision.radiusScale = .2f;
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = rainMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .045f;
            renderer.lengthScale = 4;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            rain.Play();
            rainfall = rainObject.AddComponent<AudioSource>();
            rainfall.loop = true;
            rainfall.spatialBlend = 0;
            rainfall.volume = 0;
            var samples = new float[44100 * 4];
            var random = new System.Random(7351);
            float low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float noise = (float)random.NextDouble() * 2 - 1;
                low = Mathf.Lerp(low, noise, .12f);
                samples[i] = noise * .18f + low * .65f;
            }
            rainClip = AudioClip.Create("StormRainLoop", samples.Length, 1, 44100, false);
            rainClip.SetData(samples, 0);
            rainfall.clip = rainClip;
            rainfall.Play();
        }

        public void SetWeather(Camera camera, float distanceInside, float volume)
        {
            if (atmosphere == null || camera == null) return;
            if (target != camera)
            {
                ReleaseCamera();
                target = camera;
                cameraData = camera.GetUniversalAdditionalCameraData();
                previousDepth = cameraData.requiresDepthTexture;
                cameraData.requiresDepthTexture = true;
                screen.transform.SetParent(camera.transform, false);
            }
            float sea = OceanSurface.Instance != null ? OceanSurface.Instance.Height(camera.transform.position) : 0;
            bool underwater = camera.transform.position.y < sea - .15f;
            float desired = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(180, -65, distanceInside));
            intensity = Mathf.Lerp(intensity, desired, 1 - Mathf.Exp(-Time.deltaTime * .8f));
            if (Time.time >= shelterAt)
            {
                shelterAt = Time.time + .2f;
                exposure = 1;
                var origin = camera.transform.position + Vector3.up * .15f;
                int count = Physics.RaycastNonAlloc(origin, Vector3.up, shelterHits, 60, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (shelterHits[i].collider.GetComponentInParent<Networking.NetworkPlayer>() != null) continue;
                    exposure = 0;
                    break;
                }
            }
            float gust = .75f + .25f * Mathf.PerlinNoise(Time.time * .13f, 7.2f);
            float rainAmount = intensity * intensity * (underwater ? 0 : 1);
            wetness = Mathf.MoveTowards(wetness, rainAmount * exposure, Time.deltaTime * (rainAmount * exposure > wetness ? .35f : .09f));
            rain.transform.position = camera.transform.position + new Vector3(-9, 15, -4);
            var emission = rain.emission;
            emission.rateOverTime = rainAmount * 3200 * gust;
            var velocity = rain.velocityOverLifetime;
            velocity.x = 12 * gust; velocity.z = 5 + Mathf.Sin(Time.time * .17f) * 2;
            rainfall.volume = volume * rainAmount * Mathf.Lerp(.15f, .65f, exposure) * gust;
            rainfall.pitch = .92f + gust * .1f;
            screen.SetActive(!underwater && intensity > .005f && camera.enabled);
            atmosphere.SetFloat("_Intensity", intensity);
            atmosphere.SetFloat("_Wetness", underwater ? 0 : wetness);
            atmosphere.SetFloat("_Shelter", 1 - exposure);
            atmosphere.SetVector("_WeatherCamera", camera.transform.position);
            atmosphere.SetMatrix("_WeatherInverseVP", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix).inverse);
        }

        void ReleaseCamera()
        {
            if (cameraData != null) cameraData.requiresDepthTexture = previousDepth;
            cameraData = null;
        }

        void OnDestroy()
        {
            ReleaseCamera();
            if (screen != null) Destroy(screen);
            if (screenMesh != null) Destroy(screenMesh);
            if (atmosphere != null) Destroy(atmosphere);
            if (rainMaterial != null) Destroy(rainMaterial);
            if (rainClip != null) Destroy(rainClip);
        }
    }
}
