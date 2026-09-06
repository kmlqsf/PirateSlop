using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(-100)]
    public sealed class OceanSurface : MonoBehaviour
    {
        public static OceanSurface Instance { get; private set; }
        public float WaveScale = 1f;
        public float SeaLevel;
        public Material WaterMaterial;
        static readonly Vector4[] Waves = {
            new Vector4(.94f, .342f, .65f, 42f),
            new Vector4(-.4f, .916515f, .32f, 23f),
            new Vector4(.6f, -.8f, .16f, 11f),
            new Vector4(-.8f, -.6f, .07f, 5f)
        };
        float timeOffset;
        bool synchronized;
        Mesh mesh;
        ShipController[] ships;
        readonly Vector4[] wakes = new Vector4[32];
        float refreshAt;
        public float WaveTime => Time.time + timeOffset;
        public void Synchronize(float serverTime)
        {
            float offset = serverTime - Time.time;
            timeOffset = synchronized ? Mathf.Lerp(timeOffset, offset, .05f) : offset;
            synchronized = true;
        }
        public float Height(Vector3 position)
        {
            float height = SeaLevel;
            foreach (var w in Waves)
            {
                float k = 2f * Mathf.PI / w.w;
                height += WaveScale * w.z * Mathf.Sin(k * (w.x * position.x + w.y * position.z) - Mathf.Sqrt(9.81f * k) * WaveTime);
            }
            return height;
        }
        void Awake()
        {
            Instance = this;
            const int n = 256;
            var vertices = new Vector3[(n + 1) * (n + 1)];
            var indices = new int[n * n * 6];
            for (int z = 0; z <= n; z++) for (int x = 0; x <= n; x++)
            {
                float u = (x - n / 2f) / (n / 2f), v = (z - n / 2f) / (n / 2f);
                vertices[z * (n + 1) + x] = new Vector3(u * (80f + 3920f * Mathf.Pow(Mathf.Abs(u), 3)), 0, v * (80f + 3920f * Mathf.Pow(Mathf.Abs(v), 3)));
            }
            int t = 0;
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x, b = a + n + 1;
                indices[t++] = a; indices[t++] = b; indices[t++] = a + 1;
                indices[t++] = a + 1; indices[t++] = b; indices[t++] = b + 1;
            }
            mesh = new Mesh { name = "OceanGrid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(8100, 30, 8100));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null) transform.position = new Vector3(Mathf.Floor(camera.transform.position.x / 8) * 8, SeaLevel, Mathf.Floor(camera.transform.position.z / 8) * 8);
            if (WaterMaterial == null) return;
            WaterMaterial.SetVectorArray("_Waves", Waves);
            WaterMaterial.SetFloat("_WaveTime", WaveTime);
            WaterMaterial.SetFloat("_WaveScale", WaveScale);
            if (Time.time >= refreshAt) { ships = FindObjectsByType<ShipController>(FindObjectsSortMode.None); refreshAt = Time.time + 1; }
            int count = 0;
            if (ships != null) foreach (var ship in ships)
            {
                if (ship == null || !ship.gameObject.activeInHierarchy || count >= wakes.Length) continue;
                if (camera != null && (ship.transform.position - camera.transform.position).sqrMagnitude > 40000f) continue;
                wakes[count++] = new Vector4(ship.transform.position.x, ship.transform.position.z, ship.transform.eulerAngles.y * Mathf.Deg2Rad, Mathf.Clamp01(ship.Speed / ship.MaxSpeed));
            }
            WaterMaterial.SetVectorArray("_Wakes", wakes);
            WaterMaterial.SetInt("_WakeCount", count);
        }
        void OnDestroy() { if (Instance == this) Instance = null; if (mesh != null) Destroy(mesh); }
    }
}
