using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class ProceduralWorld : MonoBehaviour
    {
        public static ProceduralWorld Instance { get; private set; }
        public WorldProfile Profile;
        public bool GenerateOnStart;
        public WorldLayout Layout { get; private set; }
        public bool Ready { get; private set; }
        public string Checksum { get; private set; }
        public float Progress { get; private set; }
        public event Action<WorldLayout> Generated;
        readonly List<Mesh> meshes = new List<Mesh>();
        GameObject content;
        void Awake() { Instance = this; }
        void Start()
        {
            if (GenerateOnStart) StartCoroutine(Build(WorldGenerator.Generate(Profile, Profile.Seed, 2, OceanSurface.Instance != null ? OceanSurface.Instance.SeaLevel : 0)));
        }
        public IEnumerable<WorldPoint> Points(string tag) => Layout == null ? Enumerable.Empty<WorldPoint>() : Layout.Points.Where(p => p.Tag == tag);
        public IEnumerator Build(WorldLayout layout)
        {
            if (layout.CatalogHash != WorldGenerator.CatalogHash(Profile)) throw new InvalidOperationException("Map catalog differs from the server. Update both games.");
            Clear(); Layout = layout;
            content = new GameObject("GeneratedMap"); content.transform.SetParent(transform, false);
            using var data = new MemoryStream();
            using var writer = new BinaryWriter(data);
            writer.Write(layout.ToJson());
            for (int i = 0; i < layout.Locations.Count; i++)
            {
                BuildLocation(layout.Locations[i], writer);
                Progress = (i + 1f) / Mathf.Max(1, layout.Locations.Count);
                yield return null;
            }
            foreach (var point in layout.Points)
            {
                var go = new GameObject(point.Id); go.transform.SetParent(content.transform, false);
                var pointPosition = point.Position;
                var pointDefinition = Profile.Locations.FirstOrDefault(d => d.Settings.Id == point.TypeId);
                bool atOrigin = pointDefinition != null && point.Rule >= 0 && point.Rule < pointDefinition.Points.Length && pointDefinition.Points[point.Rule].AtLocationOrigin;
                if (point.Tag != "ship_spawn" && !atOrigin) pointPosition.y = GroundHeight(pointPosition) + .15f;
                go.transform.SetPositionAndRotation(pointPosition, Quaternion.Euler(0, point.Yaw, 0));
                var marker = go.AddComponent<WorldSpawnPoint>(); marker.Id = point.Id; marker.Tag = point.Tag;
                var definition = Profile.Locations.FirstOrDefault(d => d.Settings.Id == point.TypeId);
                if (definition == null || point.Rule < 0 || point.Rule >= definition.Points.Length) continue;
                var prefab = definition.Points[point.Rule].StaticPrefab;
                if (prefab != null) Instantiate(prefab, go.transform);
            }
            var floor = new GameObject("Seabed"); floor.transform.SetParent(content.transform, false);
            floor.transform.position = new Vector3(0, layout.SeaLevel - layout.Depth - 1, 0);
            var box = floor.AddComponent<BoxCollider>(); box.size = new Vector3(layout.Radius * 2 + 400, 2, layout.Radius * 2 + 400);
            float floorSize = layout.Radius + 200;
            var floorMesh = new Mesh { name = "Seabed" };
            floorMesh.vertices = new[] { new Vector3(-floorSize, 1, -floorSize), new Vector3(-floorSize, 1, floorSize), new Vector3(floorSize, 1, -floorSize), new Vector3(floorSize, 1, floorSize) };
            floorMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 }; floorMesh.colors = Enumerable.Repeat(new Color(.25f, .29f, .22f), 4).ToArray(); floorMesh.RecalculateNormals(); meshes.Add(floorMesh);
            floor.AddComponent<MeshFilter>().sharedMesh = floorMesh; floor.AddComponent<MeshRenderer>().sharedMaterial = Profile.TerrainMaterial;
            writer.Flush();
            using var sha = System.Security.Cryptography.SHA256.Create();
            Checksum = BitConverter.ToString(sha.ComputeHash(data.ToArray())).Replace("-", "");
            Physics.SyncTransforms(); Ready = true;
            foreach (var point in Points("ship_spawn"))
                if (!CanSail(point.Position, point.Yaw)) { Ready = false; throw new InvalidOperationException("Unsafe ship spawn: " + point.Id); }
            Progress = 1; Generated?.Invoke(layout);
        }
        void BuildLocation(LocationRecord location, BinaryWriter writer)
        {
            int n = Layout.Resolution;
            float extent = location.Radius * 1.4f;
            var vertices = new Vector3[(n + 1) * (n + 1)];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[n * n * 6];
            for (int z = 0; z <= n; z++) for (int x = 0; x <= n; x++)
            {
                int index = z * (n + 1) + x;
                var p = new Vector3(Mathf.Lerp(-extent, extent, x / (float)n), 0, Mathf.Lerp(-extent, extent, z / (float)n));
                float h = WorldGenerator.Height(location, location.Position + p, Layout.Depth);
                vertices[index] = new Vector3(p.x, h, p.z); uv[index] = new Vector2(p.x, p.z) * .05f;
                writer.Write(Mathf.RoundToInt(h * 1000));
                float dx = WorldGenerator.Height(location, location.Position + p + Vector3.right, Layout.Depth) - h;
                float dz = WorldGenerator.Height(location, location.Position + p + Vector3.forward, Layout.Depth) - h;
                float slope = Mathf.Sqrt(dx * dx + dz * dz);
                Color color = Color.Lerp(location.Type.Sand, location.Type.Ground, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.5f, 5, h)));
                color = Color.Lerp(color, location.Type.Rock, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 1.2f, slope)));
                if (location.Type.Shape == Landform.SeaStack || location.Type.Shape == Landform.Reef) color = Color.Lerp(location.Type.Sand, location.Type.Rock, .8f);
                float shade = .9f + .1f * Mathf.Sin(p.x * .17f + Mathf.Sin(p.z * .13f)); colors[index] = color * shade;
            }
            int t = 0;
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x, b = a + n + 1;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
            }
            var mesh = new Mesh { name = location.Id, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject(location.Id + "_" + location.Type.Id); go.transform.SetParent(content.transform, false); go.transform.position = location.Position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = Profile.TerrainMaterial;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        public float GroundHeight(Vector3 world)
        {
            if (Layout == null) return float.NegativeInfinity;
            float result = Layout.SeaLevel - Layout.Depth;
            foreach (var location in Layout.Locations)
            {
                float extent = location.Radius * 1.4f;
                var p = world - location.Position;
                if (Mathf.Abs(p.x) >= extent || Mathf.Abs(p.z) >= extent) continue;
                float step = extent * 2 / Layout.Resolution;
                float gx = (p.x + extent) / step, gz = (p.z + extent) / step;
                int x = Mathf.FloorToInt(gx), z = Mathf.FloorToInt(gz); float u = gx - x, v = gz - z;
                var a = location.Position + new Vector3(x * step - extent, 0, z * step - extent);
                float ha = WorldGenerator.Height(location, a, Layout.Depth), hb = WorldGenerator.Height(location, a + Vector3.right * step, Layout.Depth);
                float hc = WorldGenerator.Height(location, a + Vector3.forward * step, Layout.Depth), hd = WorldGenerator.Height(location, a + (Vector3.right + Vector3.forward) * step, Layout.Depth);
                float h = u + v <= 1 ? ha + u * (hb - ha) + v * (hc - ha) : hd + (1 - u) * (hc - hd) + (1 - v) * (hb - hd);
                result = Mathf.Max(result, Layout.SeaLevel + h);
            }
            return result;
        }
        public bool CanSail(Vector3 position, float yaw)
        {
            if (!Ready) return true;
            if (new Vector2(position.x, position.z).magnitude > Layout.Radius - 25) return false;
            var rotation = Quaternion.Euler(0, yaw, 0);
            if (Physics.CheckBox(new Vector3(position.x, Layout.SeaLevel + 1, position.z), new Vector3(3.5f, 3.5f, 9), rotation, LayerMask.GetMask("WorldStatic"), QueryTriggerInteraction.Ignore)) return false;
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                if (GroundHeight(position + rotation * new Vector3(x * 3.5f, 0, z * 9)) > Layout.SeaLevel - 2.5f) return false;
            return true;
        }
        public void Clear()
        {
            Ready = false; Progress = 0; Checksum = null; Layout = null;
            if (content != null) { content.SetActive(false); Destroy(content); }
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            meshes.Clear();
        }
        void OnDestroy() { Clear(); if (Instance == this) Instance = null; }
    }
}
