using UnityEngine;

namespace PirateSlop.World
{
    public sealed class StormZone : MonoBehaviour
    {
        float elapsed, receivedAt, duration = 600, startRadius;
        Mesh mesh;
        public const float FinalRadius = 100f;
        readonly Transform[] walls = new Transform[3];
        readonly System.Collections.Generic.Dictionary<Camera, float> cameraRanges = new();
        AudioSource wind;
        GameAudioBank bank;
        float proximity;
        StormWeather weather;
        public static StormZone Instance { get; private set; }
        Networking.NetworkPlayer localPlayer;
        public Networking.NetworkShip LocalShip => localPlayer != null ? localPlayer.Ship : null;
        public float DistanceInside(Vector3 point) => Radius - new Vector2(point.x, point.z).magnitude;
        void Awake() { Instance = this; }
        public bool Paused { get; private set; }
        public float Progress => Mathf.Clamp01((elapsed + (Paused ? 0 : Time.time - receivedAt)) / duration);
        public float Radius => Mathf.Lerp(startRadius, FinalRadius, Progress);

        public void Synchronize(float time, float length, float radius, bool paused = false)
        {
            Paused = paused;
            elapsed = time;
            receivedAt = Time.time;
            duration = Mathf.Max(1, length);
            startRadius = radius;
        }

        void Start()
        {
            weather = gameObject.AddComponent<StormWeather>();
            const int segments = 256, rows = 32;
            var vertices = new Vector3[(segments + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var indices = new int[segments * rows * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                for (int y = 0; y <= rows; y++)
                {
                    int v = i * (rows + 1) + y;
                    float height = y / (float)rows;
                    float ring = 1 + Mathf.Pow(Mathf.Max(0, height - .08f), 2) * .12f;
                    vertices[v] = new Vector3(Mathf.Cos(angle) * ring, height, Mathf.Sin(angle) * ring);
                    uv[v] = new Vector2(i / (float)segments, height);
                    if (i == segments || y == rows) continue;
                    int t = (i * rows + y) * 6, next = v + rows + 1;
                    indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = next;
                    indices[t + 3] = next; indices[t + 4] = v + 1; indices[t + 5] = next + 1;
                }
            }
            mesh = new Mesh { name = "StormFront", vertices = vertices, uv = uv, triangles = indices };
            mesh.bounds = new Bounds(new Vector3(0, .5f, 0), new Vector3(4, 2, 4));
            for (int i = 0; i < walls.Length; i++)
            {
                var wall = new GameObject("StormCloudLayer" + i, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                walls[i] = wall;
                wall.SetParent(transform, false);
                wall.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = wall.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = Resources.Load<Material>("StormWall");
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var properties = new MaterialPropertyBlock();
                properties.SetFloat("_Layer", i);
                renderer.SetPropertyBlock(properties);
            }
            bank = Resources.Load<GameAudioBank>("GameAudioBank");
            wind = gameObject.AddComponent<AudioSource>();
            wind.playOnAwake = false; wind.loop = true; wind.spatialBlend = 0;
            wind.clip = bank != null ? bank.Wind : null;
            wind.pitch = .65f; wind.volume = 0;
            if (wind.clip != null) wind.Play();
        }

        void Update()
        {
            if (localPlayer == null)
                foreach (var player in FindObjectsByType<Networking.NetworkPlayer>(FindObjectsSortMode.None))
                    if (player.IsOwner) { localPlayer = player; break; }
            float progress = Progress;
            if (OceanSurface.Instance != null) OceanSurface.Instance.WaveScale = Mathf.Lerp(.06f, 2.5f, progress * progress);
            if (walls[0] == null) return;
            for (int i = 0; i < walls.Length; i++)
            {
                float radius = Radius + i * 26;
                walls[i].localScale = new Vector3(radius, 420 + i * 60, radius);
                walls[i].position = new Vector3(0, (OceanSurface.Instance != null ? OceanSurface.Instance.SeaLevel : 0) - 12, 0);
                walls[i].localRotation = Quaternion.Euler(0, i * 47, 0);
            }
            var camera = Camera.main;
            if (camera == null) return;
            if (!cameraRanges.ContainsKey(camera)) cameraRanges.Add(camera, camera.farClipPlane);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, startRadius * 2.2f + 1000);
            Vector3 point = camera.transform.position;
            proximity = Mathf.Clamp01(1 - (Radius - new Vector2(point.x, point.z).magnitude) / 140);
            float volume = bank != null ? bank.Master * bank.Ambience : .5f;
            weather.SetWeather(camera, DistanceInside(point), volume);
            float gust = .8f + Mathf.PerlinNoise(Time.time * .16f, 4.7f) * .2f;
            wind.volume = Mathf.Lerp(wind.volume, volume * Mathf.Lerp(.035f, gust, weather.Intensity), Time.deltaTime * 2);
            wind.pitch = Mathf.Lerp(.62f, .9f + gust * .12f, weather.Intensity);
        }

        void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null || !camera.enabled || Networking.SessionController.MenuOpen) return;
            Color old = GUI.color;
            GUI.color = Color.white;
            int remaining = Mathf.CeilToInt(duration * (1 - Progress));
            float distance = DistanceInside(localPlayer != null ? localPlayer.transform.position : camera.transform.position);
            string shipStatus = LocalShip != null ? (DistanceInside(LocalShip.transform.position) > 0 ? "Корабль в зоне" : "КОРАБЛЬ В ШТОРМЕ") : "";
            if (Paused) shipStatus = "ЗОНА НА ПАУЗЕ · урон отключён";
            GUI.color = distance <= 0 ? new Color(1, .55f, .4f) : new Color(.65f, 1, .94f);
            PirateHudStyle.Panel(new Rect(Screen.width - 404, 24, 380, 80), $"ШТОРМ  {remaining / 60:00}:{remaining % 60:00}   •   Радиус {Radius:0} м\n" + (distance <= 0 ? $"ВЫ В ШТОРМЕ • До зоны {Mathf.Abs(distance):0} м" : $"В безопасной зоне • До шторма {distance:0} м") + "\n" + shipStatus + " • Карта: M");
            GUI.color = old;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var entry in cameraRanges) if (entry.Key != null) entry.Key.farClipPlane = entry.Value;
            if (mesh != null) Destroy(mesh);
            if (OceanSurface.Instance != null) OceanSurface.Instance.WaveScale = .06f;
        }
    }
}
