using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PirateSlop.World
{
    [Serializable]
    public sealed class WorldPoint
    {
        public string Id, Tag, TypeId;
        public int Rule;
        public Vector3 Position;
        public float Yaw;
    }
    [Serializable]
    public sealed class LocationRecord
    {
        public string Id;
        public LocationSettings Type;
        public Vector3 Position;
        public float Radius, Height, Yaw;
        public uint Seed;
    }
    [Serializable]
    public sealed class WorldLayout
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion, Seed, Resolution;
        public float Radius, Depth, SeaLevel;
        public string CatalogHash;
        public List<LocationRecord> Locations = new List<LocationRecord>();
        public List<WorldPoint> Points = new List<WorldPoint>();
        public string ToJson() => JsonUtility.ToJson(this);
        public static string Hash(string text)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
        public static WorldLayout FromJson(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 1000000) throw new InvalidOperationException("Invalid map payload.");
            var map = JsonUtility.FromJson<WorldLayout>(json);
            if (map == null || map.Version != CurrentVersion || !float.IsFinite(map.Radius) || map.Radius < 600 || map.Radius > 10000 || map.Resolution < 32 || map.Resolution > 160 || !float.IsFinite(map.Depth) || map.Depth < 35 || map.Depth > 500 || !float.IsFinite(map.SeaLevel) || Mathf.Abs(map.SeaLevel) > 1000 || map.Locations == null || map.Locations.Count > 80 || map.Points == null || map.Points.Count > 10000) throw new InvalidOperationException("Unsupported map settings.");
            foreach (var island in map.Locations)
                if (island.Type == null || !float.IsFinite(island.Position.sqrMagnitude) || !float.IsFinite(island.Radius) || island.Radius < 5 || island.Radius > 400 || !float.IsFinite(island.Height) || Mathf.Abs(island.Height) > 200 || !float.IsFinite(island.Yaw) || !float.IsFinite(island.Type.Roughness)) throw new InvalidOperationException("Invalid location.");
            var ids = new HashSet<string>();
            foreach (var point in map.Points)
                if (point == null || string.IsNullOrEmpty(point.Id) || !ids.Add(point.Id) || !float.IsFinite(point.Position.sqrMagnitude) || !float.IsFinite(point.Yaw)) throw new InvalidOperationException("Invalid map point.");
            return map;
        }
    }
    public struct MapRandom
    {
        uint state;
        public MapRandom(uint seed) { state = seed == 0 ? 1 : seed; }
        public uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public float Value() => (Next() & 0xFFFFFF) / 16777216f;
        public float Range(float min, float max) => Mathf.Lerp(min, max, Value());
    }
}
