using UnityEngine;
using Oceana;
namespace PirateSlop
{
    [ExecuteAlways, DefaultExecutionOrder(-90)]
    public sealed class OceanaOcean : OceanHeightSource
    {
        public static OceanaOcean Instance { get; private set; }
        public OceanaSettings Settings;
        public OceanSurface Surface;
        readonly OceanaWorldMesh worldMesh = new();
        public Mesh GetSurfaceMesh(Camera camera) => worldMesh.Get(camera, Surface);
        float[][] heights;
        int resolution;
        public float WaveTime => Application.isPlaying && Surface != null ? Surface.WaveTime : Time.realtimeSinceStartup;
        void OnEnable()
        {
            Instance = this;
            if (Surface == null) Surface = GetComponent<OceanSurface>();
            if (Application.isPlaying) { CacheHeights(); WaterShipFoam.Ensure(gameObject); }
        }
        void OnDisable()
        {
            if (Instance == this) Instance = null;
            heights = null;
            worldMesh.Dispose();
        }
        void CacheHeights()
        {
            if (Settings == null || heights != null) return;
            resolution = Settings.ScrollArray.width;
            heights = new float[Settings.ScrollArray.depth][];
            for (int layer = 0; layer < heights.Length; layer++)
            {
                var pixels = Settings.ScrollArray.GetPixels(layer, 0);
                var values = new float[pixels.Length];
                for (int i = 0; i < values.Length; i++) values[i] = pixels[i].b;
                heights[layer] = values;
            }
        }
        float Sample(int layer, float u, float v)
        {
            float x = Mathf.Repeat(u, 1f) * resolution - .5f;
            float y = Mathf.Repeat(v, 1f) * resolution - .5f;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float tx = x - ix, ty = y - iy;
            int x0 = (ix + resolution) % resolution, y0 = (iy + resolution) % resolution;
            int x1 = (x0 + 1) % resolution, y1 = (y0 + 1) % resolution;
            var data = heights[layer];
            return Mathf.Lerp(Mathf.Lerp(data[y0 * resolution + x0], data[y0 * resolution + x1], tx),
                Mathf.Lerp(data[y1 * resolution + x0], data[y1 * resolution + x1], tx), ty);
        }

        float GridHeight(int x, int y, float time)
        {
            int size = (int)Settings.ScrollResolution;
            float u = (x + size) % size / (float)size;
            float v = (y + size) % size / (float)size;
            float height = 0f;
            for (int i = 0; i < heights.Length; i++)
            {
                var st = Settings.ScrollArrayST[i];
                height += Sample(i, u * st.x + time * st.z, v * st.y + time * st.w);
            }
            return height / heights.Length;
        }
        public override float HeightOffset(Vector3 position, float time)
        {
            CacheHeights();
            if (heights == null) return 0f;
            int size = (int)Settings.ScrollResolution;
            float x = Mathf.Repeat(position.x * Settings.ScrollST.x + Settings.ScrollST.z, 1f) * size - .5f;
            float y = Mathf.Repeat(position.z * Settings.ScrollST.y + Settings.ScrollST.w, 1f) * size - .5f;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float height = Mathf.Lerp(
                Mathf.Lerp(GridHeight(ix, iy, time), GridHeight(ix + 1, iy, time), x - ix),
                Mathf.Lerp(GridHeight(ix, iy + 1, time), GridHeight(ix + 1, iy + 1, time), x - ix), y - iy);
            return (height - .5f) * Settings.DisplaceHeight;
        }
    }
}
