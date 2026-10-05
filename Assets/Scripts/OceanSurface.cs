using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(-100)]
    public sealed class OceanSurface : MonoBehaviour
    {
        public static OceanSurface Instance { get; private set; }
        public const float CentralWhirlpoolRadius = 500f;
        public const float CentralWhirlpoolDepth = 120f;
        public float WaveScale = 1f;
        [Min(0f)] public float SwellStrength = 1f;
        [Min(1f)] public float FinalSwellMultiplier = 2.5f;
        static readonly Vector4[] Swells = {
            new Vector4(.94f, .342f, 1.1f, 180f),
            new Vector4(-.4f, .916515f, .56f, 120f),
            new Vector4(.6f, -.8f, .36f, 90f),
            new Vector4(-.8f, -.6f, .24f, 64f)
        };
        readonly Vector4[] swellWaves = new Vector4[4];
        float SeaProgress => Networking.SessionController.Instance != null ? Networking.SessionController.Instance.StormProgress : 0f;
        float SeaChop => Mathf.Lerp(.22f, .55f, Mathf.SmoothStep(0f, 1f, SeaProgress));

        Vector4 Swell(int index)
        {
            float progress = Mathf.SmoothStep(0f, 1f, SeaProgress);
            var wave = Swells[index];
            wave.z *= SwellStrength * Mathf.Lerp(1f, FinalSwellMultiplier, progress);
            if (index >= 2) wave.z *= progress;
            return wave;
        }

        float SwellHeight(Vector3 position)
        {
            float height = 0f;
            for (int i = 0; i < Swells.Length; i++)
            {
                var wave = Swell(i);
                float k = 2f * Mathf.PI / wave.w;
                height += wave.z * Mathf.Sin(k * (wave.x * position.x + wave.y * position.z) - Mathf.Sqrt(9.81f * k) * WaveTime);
            }
            return height;
        }
        public float SeaLevel;
        public Vector3 WhirlpoolCenter;
        public float WhirlpoolRadius;
        public float WhirlpoolDepth;
        public float WhirlpoolTwist;
        public Material WaterMaterial;
        public OceanHeightSource HeightSource;
        static readonly Vector4[] Waves = {
            new Vector4(.94f, .342f, .65f, 42f),
            new Vector4(-.4f, .916515f, .32f, 23f),
            new Vector4(.6f, -.8f, .16f, 11f),
            new Vector4(-.8f, -.6f, .07f, 5f)
        };
        float timeOffset;
        bool synchronized;
        Mesh mesh;
        Material runtimeMaterial;
        bool simpleWater;
        float simpleWaveSpeed, simpleWaveStrength, simpleWaveScale;
        ShipController[] ships;
        readonly Vector4[] wakes = new Vector4[32];
        readonly float[] wakeStrength = new float[32];
        readonly ShipController[] wakeShips = new ShipController[32];
        float refreshAt;
        public float WaveTime => Time.time + timeOffset;
        public void Synchronize(float serverTime)
        {
            float offset = serverTime - Time.time;
            timeOffset = synchronized ? Mathf.Lerp(timeOffset, offset, .05f) : offset;
            synchronized = true;
        }

        public float GetWhirlpoolHeight(Vector3 position)
        {
            if (WhirlpoolRadius <= 0f) return 0f;
            float dist = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(WhirlpoolCenter.x, WhirlpoolCenter.z));
            float t = Mathf.Clamp01(dist / WhirlpoolRadius);
            float falloff = 1f - t;
            return -WhirlpoolDepth * (falloff * falloff);
        }

        public float Height(Vector3 position)
        {
            if (HeightSource != null) return SeaLevel + HeightSource.HeightOffset(position, WaveTime) + GetWhirlpoolHeight(position);
            float wHeight = GetWhirlpoolHeight(position) + SwellHeight(position);
            if (simpleWater)
            {
                float t = WaveTime * simpleWaveSpeed;
                float first = Mathf.Sin(position.x * simpleWaveScale + t);
                float second = Mathf.Sin((position.z + position.x * .5f) * simpleWaveScale * .8f - t * 1.3f);
                return SeaLevel + (first + second) * .5f * simpleWaveStrength * SeaChop + wHeight;
            }
            float height = SeaLevel;
            foreach (var w in Waves)
            {
                float k = 2f * Mathf.PI / w.w;
                height += WaveScale * w.z * Mathf.Sin(k * (w.x * position.x + w.y * position.z) - Mathf.Sqrt(9.81f * k) * WaveTime);
            }
            return height + wHeight;
        }
        void Awake()
        {
            Instance = this;
            if (Application.isPlaying) WaterImpactPhysics.Ensure(gameObject);
            if (gameObject.scene.name == "NetworkOcean")
            {
                WhirlpoolCenter = Vector3.zero;
                WhirlpoolRadius = CentralWhirlpoolRadius;
                WhirlpoolDepth = CentralWhirlpoolDepth;
                WhirlpoolTwist = 2f;
            }
            if (gameObject.scene.name == "NetworkOcean") SeaMistRendererFeature.InitializeGlobalFog();
            if (HeightSource != null) return;
            if (WaterMaterial != null)
            {
                runtimeMaterial = Instantiate(WaterMaterial);
                WaterMaterial = runtimeMaterial;
                GetComponent<MeshRenderer>().sharedMaterial = WaterMaterial;
            }
            ReadWaveSettings();
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
            mesh = new Mesh { name = "OceanGrid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.MarkDynamic();
            mesh.vertices = vertices; mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(8100, (CentralWhirlpoolDepth + 40f) * 2f, 8100));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        void LateUpdate()
        {
            if (HeightSource != null) return;
            var camera = Camera.main;
            if (camera != null) transform.position = new Vector3(Mathf.Floor(camera.transform.position.x / 8) * 8, SeaLevel, Mathf.Floor(camera.transform.position.z / 8) * 8);
            if (WaterMaterial == null) return;
            ReadWaveSettings();
            for (int i = 0; i < Swells.Length; i++) swellWaves[i] = Swell(i);
            WaterMaterial.SetVectorArray("_SwellWaves", swellWaves);
            WaterMaterial.SetFloat("_SeaChop", SeaChop);
            if (simpleWater)
            {
                WaterMaterial.SetFloat("_UseWaveTime", 1f);
                WaterMaterial.SetFloat("_WaveTime", WaveTime);
            }
            WaterMaterial.SetVectorArray("_Waves", Waves);
            WaterMaterial.SetFloat("_WaveTime", WaveTime);
            WaterMaterial.SetFloat("_WaveScale", WaveScale);
            WaterMaterial.SetVector("_WhirlpoolCenter", WhirlpoolCenter);
            WaterMaterial.SetFloat("_WhirlpoolRadius", WhirlpoolRadius);
            WaterMaterial.SetFloat("_WhirlpoolDepth", WhirlpoolDepth);
            WaterMaterial.SetFloat("_WhirlpoolTwist", WhirlpoolTwist);
            ships = ShipController.ActiveControllers.ToArray();
            int count = 0;
            if (ships != null) foreach (var ship in ships)
            {
                if (ship == null || !ship.gameObject.activeInHierarchy || count >= wakes.Length) continue;
                if (camera != null && (ship.transform.position - camera.transform.position).sqrMagnitude > 40000f) continue;
                if (wakeShips[count] != ship) { wakeShips[count] = ship; wakeStrength[count] = 0f; }
                wakeStrength[count] = Mathf.MoveTowards(wakeStrength[count], Mathf.Clamp01(Mathf.Abs(ship.Speed) / ship.MaxSpeed), Time.deltaTime * .65f);
                wakes[count] = new Vector4(ship.transform.position.x, ship.transform.position.z, ship.transform.eulerAngles.y * Mathf.Deg2Rad, wakeStrength[count]);
                count++;
            }
            WaterMaterial.SetVectorArray("_Wakes", wakes);
            WaterMaterial.SetInt("_WakeCount", count);
        }
        void ReadWaveSettings()
        {
            simpleWater = WaterMaterial != null && WaterMaterial.HasProperty("_WaveStrength");
            if (!simpleWater) return;
            simpleWaveSpeed = WaterMaterial.GetFloat("_WaveSpeed");
            simpleWaveStrength = WaterMaterial.GetFloat("_WaveStrength");
            simpleWaveScale = WaterMaterial.GetFloat("_WaveScale");
        }
        void OnDestroy() { if (Instance == this) Instance = null; if (mesh != null) Destroy(mesh); if (runtimeMaterial != null) Destroy(runtimeMaterial); }
    }
}

