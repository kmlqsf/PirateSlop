using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class CoastalShoreFoamVfx : MonoBehaviour
    {
        const int Resolution = 256;
        const float Extent = 192f;
        const float CellSize = 24f;
        const float ContactSpacing = 1.5f;
        const float ProbeSpacing = 8f;
        const float FoamDistance = 72f;
        const int MaximumContacts = 2048;
        const int MaximumProbes = 64;
        const int StampsPerFrame = 256;
        const int WaveSamplesPerFrame = 4;
        const int WaveSamplesPerBatch = 24;
        const float WaveBatchInterval = .2f;
        const float AtlasInterval = .3f;

        sealed class Source
        {
            public bool Registered = true;
            public readonly HashSet<Vector2Int> Cells = new HashSet<Vector2Int>();
            public readonly Dictionary<Vector2Int, Probe> Probes = new Dictionary<Vector2Int, Probe>();
        }

        sealed class Probe
        {
            public Source Source;
            public Vector3 Position;
            public float Foam, Height, SampleTime, Density, DensityTime;
            public bool Sampled;
        }

        sealed class Contact
        {
            public Source Source;
            public Probe Probe;
            public Vector3 Position;
            public float Fade, Density;
        }

        static readonly Dictionary<Transform, Source> Sources = new Dictionary<Transform, Source>();
        static readonly Dictionary<Vector2Int, List<Contact>> Grid = new Dictionary<Vector2Int, List<Contact>>();
        static CoastalShoreFoamVfx instance;
        static int revision;
        readonly List<Contact> contacts = new List<Contact>(MaximumContacts);
        readonly List<Probe> probes = new List<Probe>(MaximumProbes);
        readonly List<Vector2Int> visibleCells = new List<Vector2Int>(64);
        readonly HashSet<Probe> selectedProbes = new HashSet<Probe>();
        readonly Plane[] frustum = new Plane[6];
        readonly float[] controlKernel = new float[49];
        readonly float[] foamKernel = new float[49];
        Texture2D atlas;
        Color[] staticPixels, pixels;
        Camera observer;
        Vector2 origin;
        float nextSelection, nextAtlas, nextWaveBatch;
        int sourceRevision = -1, maskCursor, foamCursor, probeCursor, waveBudget;
        bool buildingMask, buildingFoam;
        static readonly int Map = Shader.PropertyToID("_CoastalShoreMap");
        static readonly int Rect = Shader.PropertyToID("_CoastalShoreRect");
        static readonly int Active = Shader.PropertyToID("_CoastalShoreActive");
        static readonly int Sea = Shader.PropertyToID("_CoastalSeaLevel");
        static readonly ProfilerMarker RegistrationMarker = new ProfilerMarker("CoastalShore.RegisterContours");
        static readonly ProfilerMarker MaskMarker = new ProfilerMarker("CoastalShore.BuildAtlas");
        static readonly ProfilerMarker WavesMarker = new ProfilerMarker("CoastalShore.SampleWaves");

        public int ActiveContactCount => contacts.Count;
        public int ActiveProbeCount => probes.Count;
        public int WaveSamplesThisFrame { get; private set; }
        public int StampCountThisFrame { get; private set; }
        public int LastHeightQueries => WaveSamplesThisFrame;
        public float LastBuildMilliseconds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            Sources.Clear();
            Grid.Clear();
            instance = null;
            revision = 0;
            Shader.SetGlobalFloat(Active, 0);
        }

        public static void RegisterContours(Transform owner, CoastalShoreContour[] contours)
        {
            using (RegistrationMarker.Auto()) Register(owner, contours);
        }

        static void Register(Transform owner, CoastalShoreContour[] contours)
        {
            if (owner == null || contours == null) return;
            RemoveSource(owner);
            var source = new Source();
            var cells = new HashSet<Vector2Int>();
            var matrix = owner.localToWorldMatrix;
            foreach (var contour in contours)
            {
                if (contour.Points == null || contour.Points.Length < 2) continue;
                int segments = contour.Closed ? contour.Points.Length : contour.Points.Length - 1;
                for (int i = 0; i < segments; i++)
                {
                    var a = matrix.MultiplyPoint3x4(contour.Points[i]);
                    var b = matrix.MultiplyPoint3x4(contour.Points[(i + 1) % contour.Points.Length]);
                    int count = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / ContactSpacing), 1, 128);
                    for (int step = 0; step < count; step++)
                    {
                        var position = Vector3.Lerp(a, b, (step + .5f) / count);
                        var key = new Vector2Int(Mathf.RoundToInt(position.x / ContactSpacing), Mathf.RoundToInt(position.z / ContactSpacing));
                        if (!cells.Add(key)) continue;
                        var probeKey = new Vector2Int(Mathf.FloorToInt(position.x / ProbeSpacing), Mathf.FloorToInt(position.z / ProbeSpacing));
                        if (!source.Probes.TryGetValue(probeKey, out var probe))
                        {
                            probe = new Probe { Source = source, Position = position };
                            source.Probes.Add(probeKey, probe);
                        }
                        var cell = Cell(position.x, position.z);
                        if (!Grid.TryGetValue(cell, out var bucket))
                        {
                            bucket = new List<Contact>();
                            Grid.Add(cell, bucket);
                        }
                        bucket.Add(new Contact { Source = source, Probe = probe, Position = position });
                        source.Cells.Add(cell);
                    }
                }
            }
            if (source.Cells.Count == 0) return;
            Sources.Add(owner, source);
            revision++;
            if (instance == null)
                instance = new GameObject("CoastalShoreFoam").AddComponent<CoastalShoreFoamVfx>();
        }

        static Vector2Int Cell(float x, float z) => new Vector2Int(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize));

        static void RemoveSource(Transform owner)
        {
            if (!Sources.TryGetValue(owner, out var source)) return;
            source.Registered = false;
            foreach (var cell in source.Cells)
            {
                var bucket = Grid[cell];
                for (int i = bucket.Count - 1; i >= 0; i--)
                    if (bucket[i].Source == source) bucket.RemoveAt(i);
                if (bucket.Count == 0) Grid.Remove(cell);
            }
            Sources.Remove(owner);
            revision++;
        }

        public static void UnregisterContours(Transform owner)
        {
            if (owner == null) return;
            RemoveSource(owner);
            if (Sources.Count == 0 && instance != null)
            {
                Shader.SetGlobalFloat(Active, 0);
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        void Awake()
        {
            atlas = new Texture2D(Resolution, Resolution, TextureFormat.RGHalf, false, true)
            {
                name = "CoastalShoreFoamAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            staticPixels = new Color[Resolution * Resolution];
            pixels = new Color[Resolution * Resolution];
            atlas.SetPixels(pixels);
            atlas.Apply(false, false);
            Shader.SetGlobalTexture(Map, atlas);
            int index = 0;
            for (int z = -3; z <= 3; z++) for (int x = -3; x <= 3; x++)
            {
                float distance = Mathf.Sqrt(x * x + z * z) * Extent / Resolution;
                controlKernel[index] = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 2.2f, distance));
                foamKernel[index++] = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 1.7f, distance));
            }
        }

        void Update()
        {
            WaveSamplesThisFrame = StampCountThisFrame = 0;
            LastBuildMilliseconds = 0;
            var ocean = OceanSurface.Instance;
            if (ocean == null) { Shader.SetGlobalFloat(Active, 0); return; }
            Shader.SetGlobalFloat(Sea, ocean.SeaLevel);
            if (observer == null || !observer.isActiveAndEnabled) observer = Camera.main;
            if (observer == null) { Shader.SetGlobalFloat(Active, 0); return; }
            var eye = observer.transform.position;
            var desiredOrigin = new Vector2(Mathf.Floor(eye.x / 12f) * 12f, Mathf.Floor(eye.z / 12f) * 12f) - Vector2.one * Extent * .5f;
            if (sourceRevision != revision || (!buildingMask && !buildingFoam && origin != desiredOrigin))
                BeginMask(desiredOrigin, eye, ocean.SeaLevel);
            else if (Time.time >= nextSelection)
                SelectProbes(eye, ocean.SeaLevel);
            SampleWaves(ocean);
            BuildAtlas();
        }

        void BuildAtlas()
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            using (MaskMarker.Auto()) BuildAtlasSteps();
            LastBuildMilliseconds += (float)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        void BuildAtlasSteps()
        {
            int budget = StampsPerFrame;
            if (buildingMask)
            {
                while (maskCursor < contacts.Count && budget > 0)
                {
                    Stamp(contacts[maskCursor++], true);
                    budget--;
                    StampCountThisFrame++;
                }
                if (maskCursor == contacts.Count)
                {
                    buildingMask = false;
                    BeginFoam();
                }
            }
            if (!buildingMask && !buildingFoam && Time.time >= nextAtlas) BeginFoam();
            if (buildingFoam)
            {
                while (foamCursor < contacts.Count && budget > 0)
                {
                    Stamp(contacts[foamCursor++], false);
                    budget--;
                    StampCountThisFrame++;
                }
                if (foamCursor == contacts.Count)
                {
                    buildingFoam = false;
                    atlas.SetPixels(pixels);
                    atlas.Apply(false, false);
                    Shader.SetGlobalVector(Rect, new Vector4(origin.x, origin.y, 1f / Extent, 1f / Extent));
                    Shader.SetGlobalFloat(Active, contacts.Count > 0 ? 1 : 0);
                    nextAtlas = Time.time + AtlasInterval;
                }
            }
        }

        void BeginMask(Vector2 nextOrigin, Vector3 eye, float seaLevel)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            using (MaskMarker.Auto()) SelectContacts(nextOrigin, eye, seaLevel);
            LastBuildMilliseconds += (float)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        void SelectContacts(Vector2 nextOrigin, Vector3 eye, float seaLevel)
        {
            origin = nextOrigin;
            sourceRevision = revision;
            contacts.Clear();
            visibleCells.Clear();
            var center = Cell(eye.x, eye.z);
            int radius = Mathf.CeilToInt(FoamDistance / CellSize) + 1;
            for (int z = -radius; z <= radius; z++) for (int x = -radius; x <= radius; x++)
            {
                var cell = center + new Vector2Int(x, z);
                if (Grid.ContainsKey(cell)) visibleCells.Add(cell);
            }
            visibleCells.Sort((a, b) => CellDistance(a, eye).CompareTo(CellDistance(b, eye)));
            foreach (var cell in visibleCells)
            {
                foreach (var contact in Grid[cell])
                {
                    if (Mathf.Abs(contact.Position.y - seaLevel) > 2f) continue;
                    float distance = Vector2.Distance(new Vector2(contact.Position.x, contact.Position.z), new Vector2(eye.x, eye.z));
                    if (distance >= FoamDistance) continue;
                    contact.Fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(52f, FoamDistance, distance));
                    contacts.Add(contact);
                    if (contacts.Count == MaximumContacts) break;
                }
                if (contacts.Count == MaximumContacts) break;
            }
            System.Array.Clear(staticPixels, 0, staticPixels.Length);
            maskCursor = 0;
            buildingMask = true;
            buildingFoam = false;
            SelectProbes(eye, seaLevel);
        }

        static float CellDistance(Vector2Int cell, Vector3 eye)
        {
            float x = (cell.x + .5f) * CellSize - eye.x, z = (cell.y + .5f) * CellSize - eye.z;
            return x * x + z * z;
        }

        void SelectProbes(Vector3 eye, float seaLevel)
        {
            nextSelection = Time.time + .5f;
            probes.Clear();
            selectedProbes.Clear();
            GeometryUtility.CalculateFrustumPlanes(observer, frustum);
            foreach (var contact in contacts)
            {
                var probe = contact.Probe;
                if (!probe.Source.Registered || !selectedProbes.Add(probe)) continue;
                var position = new Vector3(probe.Position.x, seaLevel, probe.Position.z);
                if ((position - eye).sqrMagnitude > FoamDistance * FoamDistance) continue;
                if (!GeometryUtility.TestPlanesAABB(frustum, new Bounds(position, new Vector3(12, 20, 12)))) continue;
                probes.Add(probe);
            }
            probes.Sort((a, b) => (a.Position - eye).sqrMagnitude.CompareTo((b.Position - eye).sqrMagnitude));
            if (probes.Count > MaximumProbes) probes.RemoveRange(MaximumProbes, probes.Count - MaximumProbes);
            probeCursor = probes.Count > 0 ? probeCursor % probes.Count : 0;
        }

        void SampleWaves(OceanSurface ocean)
        {
            using (WavesMarker.Auto()) SampleWaveSteps(ocean);
        }

        void SampleWaveSteps(OceanSurface ocean)
        {
            float now = Time.time;
            if (now >= nextWaveBatch)
            {
                nextWaveBatch = now + WaveBatchInterval;
                waveBudget = WaveSamplesPerBatch;
            }
            int attempts = Mathf.Min(WaveSamplesPerFrame, probes.Count);
            for (int i = 0; i < attempts && waveBudget > 0; i++)
            {
                var probe = probes[probeCursor];
                probeCursor = (probeCursor + 1) % probes.Count;
                if (!probe.Source.Registered || (probe.Sampled && now - probe.SampleTime < .2f)) continue;
                float height = ocean.Height(probe.Position);
                float elapsed = now - probe.SampleTime;
                float rise = probe.Sampled && elapsed < 1f ? Mathf.Max(0, (height - probe.Height) / Mathf.Max(elapsed, .01f)) : 0;
                float decay = probe.Sampled ? probe.Foam * Mathf.Exp(-elapsed * .75f) : 0;
                probe.Foam = Mathf.Max(decay, Mathf.Clamp01((height - probe.Position.y + .35f) * .35f + rise * .15f));
                probe.Height = height;
                probe.SampleTime = now;
                probe.Sampled = true;
                waveBudget--;
                WaveSamplesThisFrame++;
            }
        }

        void BeginFoam()
        {
            System.Array.Copy(staticPixels, pixels, pixels.Length);
            float now = Time.time;
            foreach (var contact in contacts)
            {
                float sum = 0, weight = 0;
                var key = new Vector2Int(Mathf.FloorToInt(contact.Position.x / ProbeSpacing), Mathf.FloorToInt(contact.Position.z / ProbeSpacing));
                for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                {
                    if (!contact.Source.Probes.TryGetValue(key + new Vector2Int(x, z), out var probe) || !probe.Sampled) continue;
                    if (probe.DensityTime != now)
                    {
                        probe.Density = probe.Foam * Mathf.Exp(-(now - probe.SampleTime) * .75f);
                        probe.DensityTime = now;
                    }
                    var delta = probe.Position - contact.Position;
                    float amount = 1f / (1f + (delta.x * delta.x + delta.z * delta.z) / (ProbeSpacing * ProbeSpacing));
                    sum += probe.Density * amount;
                    weight += amount;
                }
                contact.Density = weight > 0 ? sum / weight : .06f;
            }
            foamCursor = 0;
            buildingFoam = true;
        }

        void Stamp(Contact contact, bool control)
        {
            if (!contact.Source.Registered) return;
            int x = Mathf.RoundToInt((contact.Position.x - origin.x) * Resolution / Extent);
            int z = Mathf.RoundToInt((contact.Position.z - origin.y) * Resolution / Extent);
            float strength = control ? contact.Fade : contact.Density * contact.Fade;
            int kernel = 0;
            for (int dz = -3; dz <= 3; dz++) for (int dx = -3; dx <= 3; dx++)
            {
                int px = x + dx, pz = z + dz;
                float value = strength * (control ? controlKernel[kernel++] : foamKernel[kernel++]);
                if (px < 0 || pz < 0 || px >= Resolution || pz >= Resolution) continue;
                int index = pz * Resolution + px;
                if (control) staticPixels[index].r = Mathf.Max(staticPixels[index].r, value);
                else pixels[index].g = Mathf.Max(pixels[index].g, value);
            }
        }

        void OnDisable()
        {
            if (instance == this) Shader.SetGlobalFloat(Active, 0);
        }

        void OnDestroy()
        {
            if (atlas != null) Destroy(atlas);
            if (instance == this || instance == null)
            {
                Shader.SetGlobalFloat(Active, 0);
                instance = null;
            }
        }
    }
}
