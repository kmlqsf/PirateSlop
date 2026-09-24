using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PirateSlop
{
    public sealed class SeaMistRendererFeature : FullScreenPassRendererFeature
    {
        public static float DensityMultiplier { get; set; } = 1f;
        static readonly Vector4[] flashes = new Vector4[16];
        static readonly float[] flashTimes = new float[16];
        static readonly Vector4[] visibleFlashes = new Vector4[16];
        static int nextFlash;
        ShipController clearShip;
        float nextShipSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFlashes()
        {
            System.Array.Clear(flashes, 0, flashes.Length);
            nextFlash = 0;
            DensityMultiplier = 1f;
        }

        public static void CannonFlash(Vector3 position)
        {
            flashes[nextFlash] = new Vector4(position.x, position.y, position.z, 1);
            flashTimes[nextFlash] = Time.time;
            nextFlash = (nextFlash + 1) % flashes.Length;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (ShipSpyglassView.ClearsFog(renderingData.cameraData.camera)) return;
            if (renderingData.cameraData.cameraType != CameraType.Game || renderingData.cameraData.renderType != CameraRenderType.Base || OceanSurface.Instance == null || OceanSurface.Instance.gameObject.scene.name != "NetworkOcean" || passMaterial == null) return;
            passMaterial.SetFloat("_SeaLevel", OceanSurface.Instance.SeaLevel);
            passMaterial.SetFloat("_DensityMultiplier", Mathf.Clamp(DensityMultiplier, 0f, 3f));
            var cameraPosition = renderingData.cameraData.camera.transform.position;
            if (Time.time >= nextShipSearch)
            {
                nextShipSearch = Time.time + 2;
                clearShip = null;
                float nearest = 70 * 70;
                foreach (var ship in Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None))
                {
                    float distance = (ship.transform.position - cameraPosition).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    clearShip = ship;
                }
            }
            var center = clearShip != null ? clearShip.transform.position : cameraPosition;
            var forward = clearShip != null ? clearShip.transform.forward : Vector3.forward;
            var size = clearShip != null ? clearShip.HullFootprint * .5f : Vector2.zero;
            passMaterial.SetVector("_FogClearCenter", center);
            passMaterial.SetVector("_FogClearShape", new Vector4(forward.x, forward.z, size.x, size.y));
            int count = 0;
            for (int i = 0; i < flashes.Length; i++)
            {
                float age = Time.time - flashTimes[i];
                if (flashes[i].w == 0 || age < 0 || age >= .55f) continue;
                var flash = flashes[i];
                flash.w = Mathf.Clamp01(age / .025f) * Mathf.Pow(1 - age / .55f, 2);
                visibleFlashes[count++] = flash;
            }
            passMaterial.SetInt("_FogFlashCount", count);
            passMaterial.SetVectorArray("_FogFlashes", visibleFlashes);
            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
