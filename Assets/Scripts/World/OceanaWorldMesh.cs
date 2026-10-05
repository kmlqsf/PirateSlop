using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace PirateSlop
{
    public sealed class OceanaWorldMesh : IDisposable
    {
        readonly SortedSet<float> xCoordinates = new();
        readonly SortedSet<float> zCoordinates = new();
        readonly List<Vector3> windows = new();
        readonly List<float> xs = new();
        readonly List<float> zs = new();
        Mesh mesh;
        int signature;
        bool initialized;
        public Mesh Get(Camera camera, OceanSurface surface)
        {
            windows.Clear();
            windows.Add(Snap(camera.transform.position));
            int shipCount = 0;
            foreach (var ship in ShipController.ActiveControllers)
            {
                if (ship == null || !ship.gameObject.activeInHierarchy || shipCount >= 4) continue;
                if (new Vector2(ship.transform.position.x - camera.transform.position.x, ship.transform.position.z - camera.transform.position.z).sqrMagnitude > 90000f) continue;
                var window = Snap(ship.transform.position);
                if (!windows.Contains(window)) windows.Add(window);
                shipCount++;
            }
            float extent = Mathf.Max(1000f, camera.farClipPlane * 1.37f);
            int next = surface.WhirlpoolCenter.GetHashCode();
            unchecked
            {
                next = next * 31 + surface.WhirlpoolRadius.GetHashCode();
                next = next * 31 + surface.WhirlpoolDepth.GetHashCode();
                next = next * 31 + surface.SeaLevel.GetHashCode();
                next = next * 31 + extent.GetHashCode();
                foreach (var window in windows) next = next * 31 + window.GetHashCode();
            }
            if (initialized && signature == next && mesh != null) return mesh;
            signature = next;
            initialized = true;
            BuildAxis(xCoordinates, xs, surface.WhirlpoolCenter.x, surface.WhirlpoolRadius, extent, true);
            BuildAxis(zCoordinates, zs, surface.WhirlpoolCenter.z, surface.WhirlpoolRadius, extent, false);
            var vertices = new Vector3[xs.Count * zs.Count];
            var indices = new int[(xs.Count - 1) * (zs.Count - 1) * 6];
            for (int z = 0; z < zs.Count; z++)
                for (int x = 0; x < xs.Count; x++)
                    vertices[z * xs.Count + x] = new Vector3(xs[x], 0, zs[z]);
            int index = 0;
            for (int z = 0; z < zs.Count - 1; z++)
                for (int x = 0; x < xs.Count - 1; x++)
                {
                    int a = z * xs.Count + x, b = a + xs.Count;
                    indices[index++] = a; indices[index++] = b; indices[index++] = a + 1;
                    indices[index++] = a + 1; indices[index++] = b; indices[index++] = b + 1;
                }
            if (mesh == null)
            {
                mesh = new Mesh { name = "Oceana World Grid", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.bounds = new Bounds(
                new Vector3((xs[0] + xs[^1]) * .5f, surface.SeaLevel - surface.WhirlpoolDepth * .5f, (zs[0] + zs[^1]) * .5f),
                new Vector3(xs[^1] - xs[0], surface.WhirlpoolDepth + 80f, zs[^1] - zs[0]));
            return mesh;
        }
        static Vector3 Snap(Vector3 position) => new Vector3(Mathf.Floor(position.x / 8f) * 8f, 0, Mathf.Floor(position.z / 8f) * 8f);
        void BuildAxis(SortedSet<float> coordinates, List<float> result, float center, float radius, float extent, bool xAxis)
        {
            coordinates.Clear();
            int steps = Mathf.CeilToInt((Mathf.Max(0, radius) + 12f) / 6f);
            for (int i = -steps; i <= steps; i++) coordinates.Add(center + i * 6f);
            foreach (var window in windows)
            {
                float anchor = xAxis ? window.x : window.z;
                for (int i = -36; i <= 36; i++) coordinates.Add(anchor + i * 2f);
            }
            result.Clear();
            result.AddRange(coordinates);
            for (int i = 0; i < result.Count - 1; i++)
            {
                float left = result[i], right = result[i + 1], gapStep = 6f;
                while (right - left > gapStep * 2.2f)
                {
                    left += gapStep; right -= gapStep;
                    coordinates.Add(left); coordinates.Add(right);
                    gapStep = Mathf.Min(gapStep * 1.22f, 512f);
                }
            }
            float cameraAnchor = xAxis ? windows[0].x : windows[0].z;
            float low = Mathf.Min(coordinates.Min, cameraAnchor - extent);
            float high = Mathf.Max(coordinates.Max, cameraAnchor + extent);
            float edge = coordinates.Min, spacing = 6f;
            while (edge > low)
            {
                edge = Mathf.Max(low, edge - spacing);
                coordinates.Add(edge);
                spacing = Mathf.Min(spacing * 1.22f, 512f);
            }
            edge = coordinates.Max; spacing = 6f;
            while (edge < high)
            {
                edge = Mathf.Min(high, edge + spacing);
                coordinates.Add(edge);
                spacing = Mathf.Min(spacing * 1.22f, 512f);
            }
            result.Clear();
            result.AddRange(coordinates);
        }
        public void Dispose()
        {
            if (mesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            mesh = null;
            initialized = false;
        }
    }
}
