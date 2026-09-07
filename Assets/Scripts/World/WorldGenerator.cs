using System;
using System.Linq;
using UnityEngine;

namespace PirateSlop.World
{
    public static class WorldGenerator
    {
        public static string CatalogHash(WorldProfile profile)
        {
            return WorldLayout.Hash(profile.CatalogRevision + "|" + string.Join("|", profile.Locations.Select(d => JsonUtility.ToJson(d.Settings) + string.Join(";", d.Points.Select(p => JsonUtility.ToJson(new LocationPointRule { Tag = p.Tag, Count = p.Count, Height = p.Height, MaxSlope = p.MaxSlope, Spacing = p.Spacing, PrefabVersion = p.PrefabVersion }) + ":" + (p.StaticPrefab != null ? p.StaticPrefab.name : ""))))));
        }
        public static WorldLayout Generate(WorldProfile profile, int seed, int players, float seaLevel)
        {
            if (profile == null || profile.Locations == null || profile.Locations.Length == 0 || profile.Locations.Any(d => d == null)) throw new InvalidOperationException("Assign location types to the map profile.");
            if (players < 1 || players > 128 || profile.LocationCount < 1 || profile.LocationCount > 80 || profile.Radius < 600 || profile.Radius > 10000 || profile.Resolution < 32 || profile.Resolution > 160) throw new InvalidOperationException("Map limits: 1-128 ships, 1-80 locations, radius 600-10000, resolution 32-160.");
            foreach (var d in profile.Locations)
            {
                var s = d.Settings;
                if (string.IsNullOrWhiteSpace(s.Id) || s.Radius.x < 5 || s.Radius.y > 400 || s.Radius.y < s.Radius.x || s.Height.x < 0 || s.Height.y > 200 || s.Height.y < s.Height.x || d.Points == null) throw new InvalidOperationException("Invalid location settings: " + d.name);
                foreach (var rule in d.Points)
                    if (rule.Count < 0 || rule.Count > 100 || rule.Height.y < rule.Height.x || string.IsNullOrWhiteSpace(rule.Tag) || rule.Tag == "ship_spawn") throw new InvalidOperationException("Invalid point rule in " + d.name);
            }
            if (profile.Locations.Select(d => d.Settings.Id).Distinct().Count() != profile.Locations.Length) throw new InvalidOperationException("Location IDs must be unique.");
            if (profile.FixedLayout != null)
            {
                var fixedMap = WorldLayout.FromJson(profile.FixedLayout.text);
                if (fixedMap.Points.Count(p => p.Tag == "ship_spawn") < players) throw new InvalidOperationException("Saved map does not contain enough ship spawn points.");
                return fixedMap;
            }
            var map = new WorldLayout { Seed = seed, Radius = profile.Radius, Depth = profile.SeaDepth, SeaLevel = seaLevel, Resolution = profile.Resolution, CatalogHash = CatalogHash(profile) };
            var rng = new MapRandom(unchecked((uint)seed));
            float spawnRing = Mathf.Max(220, players * profile.SpawnSpacing / (2 * Mathf.PI));
            if (spawnRing + profile.SpawnClearance > profile.Radius * .75f) throw new InvalidOperationException("Map too small for the requested ship spawn spacing.");
            for (int i = 0; i < players; i++)
            {
                float angle = i * Mathf.PI * 2 / players;
                map.Points.Add(new WorldPoint { Id = "ship_" + i, Tag = "ship_spawn", Position = new Vector3(Mathf.Sin(angle) * spawnRing, seaLevel, Mathf.Cos(angle) * spawnRing), Yaw = angle * Mathf.Rad2Deg });
            }
            float weight = profile.Locations.Sum(d => Mathf.Max(0, d.Settings.Weight));
            if (weight <= 0) throw new InvalidOperationException("At least one location weight must be positive.");
            for (int i = 0; i < profile.LocationCount; i++)
            {
                float pick = rng.Value() * weight;
                var definition = profile.Locations.Last();
                foreach (var candidate in profile.Locations) { pick -= Mathf.Max(0, candidate.Settings.Weight); if (pick <= 0) { definition = candidate; break; } }
                var enabled = profile.Locations.Where(d => d.Settings.Weight > 0).ToArray();
                if (i < enabled.Length) definition = enabled[i];
                var type = definition.Settings;
                float radius = rng.Range(type.Radius.x, type.Radius.y);
                bool placed = false;
                for (int attempt = 0; attempt < 1500; attempt++)
                {
                    float angle = rng.Range(0, Mathf.PI * 2), distance = Mathf.Sqrt(rng.Value()) * (map.Radius - radius * 1.4f - 70);
                    var position = new Vector3(Mathf.Sin(angle) * distance, seaLevel, Mathf.Cos(angle) * distance);
                    if (position.magnitude < radius * 1.4f + 150) continue;
                    if (map.Locations.Any(l => Vector3.Distance(l.Position, position) < (l.Radius + radius) * 1.4f + profile.ShippingGap)) continue;
                    if (map.Points.Any(p => p.Tag == "ship_spawn" && Vector3.Distance(p.Position, position) < radius * 1.4f + profile.SpawnClearance)) continue;
                    var record = new LocationRecord { Id = "location_" + i, Type = JsonUtility.FromJson<LocationSettings>(JsonUtility.ToJson(type)), Position = position, Radius = radius, Height = rng.Range(type.Height.x, type.Height.y), Yaw = rng.Range(0, 360), Seed = rng.Next() };
                    map.Locations.Add(record);
                    AddPoints(map, record, definition, ref rng);
                    placed = true; break;
                }
                if (!placed) throw new InvalidOperationException("Map cannot fit all locations. Increase radius or reduce count/spacing.");
            }
            return WorldLayout.FromJson(map.ToJson());
        }
        static void AddPoints(WorldLayout map, LocationRecord location, LocationDefinition definition, ref MapRandom rng)
        {
            for (int ruleIndex = 0; ruleIndex < definition.Points.Length; ruleIndex++)
            {
                var rule = definition.Points[ruleIndex];
                int placed = 0;
                for (int attempt = 0; attempt < rule.Count * 200 && placed < rule.Count; attempt++)
                {
                    var point = location.Position + new Vector3(rng.Range(-location.Radius, location.Radius), 0, rng.Range(-location.Radius, location.Radius));
                    float h = Height(location, point, map.Depth);
                    if (h < rule.Height.x || h > rule.Height.y) continue;
                    float dx = (Height(location, point + Vector3.right, map.Depth) - Height(location, point - Vector3.right, map.Depth)) * .5f;
                    float dz = (Height(location, point + Vector3.forward, map.Depth) - Height(location, point - Vector3.forward, map.Depth)) * .5f;
                    if (Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg > rule.MaxSlope) continue;
                    if (map.Points.Any(p => p.Tag == rule.Tag && Vector3.Distance(p.Position, new Vector3(point.x, map.SeaLevel + h, point.z)) < rule.Spacing)) continue;
                    point.y = map.SeaLevel + h + .15f;
                    map.Points.Add(new WorldPoint { Id = location.Id + "/" + rule.Tag + "/" + ruleIndex + "/" + placed++, Tag = rule.Tag, TypeId = location.Type.Id, Rule = ruleIndex, Position = point, Yaw = rng.Range(0, 360) });
                }
            }
        }
        static float Noise(int x, int z, uint seed)
        {
            uint h = unchecked((uint)x * 374761393u + (uint)z * 668265263u + seed * 1442695041u);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
        }
        static float SmoothNoise(float x, float z, uint seed)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float u = x - ix, v = z - iz; u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            return Mathf.Lerp(Mathf.Lerp(Noise(ix, iz, seed), Noise(ix + 1, iz, seed), u), Mathf.Lerp(Noise(ix, iz + 1, seed), Noise(ix + 1, iz + 1, seed), u), v);
        }
        public static float Height(LocationRecord location, Vector3 point, float depth)
        {
            var p = Quaternion.Euler(0, -location.Yaw, 0) * (point - location.Position) / location.Radius;
            float r = new Vector2(p.x, p.z).magnitude;
            if (r >= 1.4f) return -depth;
            float n = SmoothNoise(p.x * 3 + 20, p.z * 3 + 20, location.Seed) * 2 - 1;
            float fine = SmoothNoise(p.x * 12 + 40, p.z * 12 + 40, location.Seed + 19) * 2 - 1;
            float d = r + n * location.Type.Roughness * .22f;
            float mound = Mathf.Clamp01(1 - d);
            float height = -3 + location.Height * Mathf.Pow(mound, 1.25f) + fine * location.Type.Roughness * mound * 3;
            switch (location.Type.Shape)
            {
                case Landform.Mountain: height = -3 + location.Height * Mathf.Pow(mound, .8f) + n * mound * 5; break;
                case Landform.Atoll: height = -5 + location.Height * Mathf.Exp(-Mathf.Pow((d - .62f) * 6, 2)); break;
                case Landform.Crescent:
                    float bay = new Vector2(p.x, p.z - .48f).magnitude;
                    height = Mathf.Min(height, -6 + Mathf.Max(0, bay - .5f) * location.Height * 4); break;
                case Landform.Reef: height = -1.7f + n * 1.5f + fine * .35f; break;
                case Landform.SeaStack: height = -3 + location.Height * Mathf.Pow(Mathf.Clamp01(1 - d * 1.8f), .35f) + n * mound * 2; break;
            }
            float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.88f, 1.4f, r));
            return Mathf.Round(Mathf.Lerp(height, -depth, edge) * 1000) / 1000;
        }
    }
}
