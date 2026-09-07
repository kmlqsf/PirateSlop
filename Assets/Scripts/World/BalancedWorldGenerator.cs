using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PirateSlop.World
{
    public static class BalancedWorldGenerator
    {
        public static WorldLayout Generate(WorldProfile profile, int seed, int shipCount, float seaLevel)
        {
            int sectors = profile.StartingSectors;
            if (sectors < 4 || sectors > 24 || shipCount > sectors * 2 || profile.ContestedLocations < 0 || profile.ContestedLocations > 12 || profile.HazardLocations < 0 || profile.HazardLocations > 16)
                throw new InvalidOperationException("Balanced maps support at most two ship slots per starting sector.");
            if (profile.RouteClearance < 20 || profile.RouteGridSize < 20 || profile.RouteGridSize > 80)
                throw new InvalidOperationException("Invalid route clearance or grid size.");
            ValidateCatalog(profile, profile.StartingSupplies, "starting supplies");
            if (profile.ContestedLocations > 0) ValidateCatalog(profile, profile.ContestedSites, "contested locations");
            if (profile.HazardLocations > 0) ValidateCatalog(profile, profile.Hazards, "hazards");
            var reference = profile.StartingSupplies[0].Settings;
            int lootCount = profile.StartingSupplies[0].Points.Count(p => p.Tag == "loot");
            if (lootCount == 0 || profile.StartingSupplies.Any(d => d.Settings.Shape != Landform.SupplyIsland || d.Settings.Radius != reference.Radius || d.Settings.Height != reference.Height || d.Settings.PierCount != reference.PierCount || d.Points.Count(p => p.Tag == "loot") != lootCount || !d.Points.Any(p => p.Tag == "landing" && p.AtLocationOrigin)))
                throw new InvalidOperationException("Opening supplies must have matching sizes, piers and loot-marker counts.");
            string reason = "No candidate generated.";
            for (int attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    uint layoutSeed = unchecked((uint)seed + (uint)attempt * 2654435761u);
                    var map = Place(profile, seed, layoutSeed, shipCount, seaLevel);
                    WorldRoutePlanner.Connect(map, profile.RouteGridSize, profile.RouteClearance);
                    return WorldLayout.FromJson(map.ToJson());
                }
                catch (InvalidOperationException error) { reason = error.Message; }
            }
            throw new InvalidOperationException("Cannot create a balanced map after 12 attempts: " + reason);
        }

        static void ValidateCatalog(WorldProfile profile, LocationDefinition[] choices, string role)
        {
            if (choices == null || choices.Length == 0 || choices.Any(d => d == null || !profile.Locations.Contains(d) || d.Settings.Weight <= 0))
                throw new InvalidOperationException("Assign enabled catalog types for " + role + ".");
        }

        static WorldLayout Place(WorldProfile profile, int seed, uint layoutSeed, int shipCount, float seaLevel)
        {
            var map = new WorldLayout { Seed = seed, Radius = profile.Radius, Depth = profile.SeaDepth, SeaLevel = seaLevel, Resolution = profile.Resolution, CatalogHash = WorldGenerator.CatalogHash(profile) };
            var rng = new MapRandom(layoutSeed);
            float orientation = rng.Range(0, 360);
            float supplyRadius = profile.StartingSupplies[0].Settings.Radius.x;
            float ring = profile.Radius * .70f;
            if (2 * ring * Mathf.Sin(Mathf.PI / profile.StartingSectors) < supplyRadius * 2.8f + profile.ShippingGap)
                throw new InvalidOperationException("Starting islands do not fit with the configured water gaps.");
            float spawnDistance = supplyRadius * 1.4f + 175;
            if (ring + spawnDistance + 40 > profile.Radius) throw new InvalidOperationException("Map is too small for safe ship starts.");
            var opening = new List<LocationRecord>();
            for (int sector = 0; sector < profile.StartingSectors; sector++)
            {
                float angle = orientation + sector * 360f / profile.StartingSectors;
                var outward = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                var definition = profile.StartingSupplies[sector % profile.StartingSupplies.Length];
                var record = Add(map, definition, outward * ring + Vector3.up * seaLevel, angle + 180, "supply_" + sector, ref rng);
                opening.Add(record);
                var approach = record.Position + outward * (record.Radius * 1.4f + profile.RouteClearance + 45);
                map.Points.Add(new WorldPoint { Id = "approach_" + sector, Tag = "ship_approach", Position = approach, Yaw = angle + 180 });
            }
            var order = SpreadOrder(profile.StartingSectors);
            for (int slot = 0; slot < shipCount; slot++)
            {
                int sector = order[slot % order.Count];
                var island = opening[sector];
                var outward = new Vector3(island.Position.x, 0, island.Position.z).normalized;
                var tangent = new Vector3(outward.z, 0, -outward.x);
                float side = shipCount > profile.StartingSectors ? (slot < profile.StartingSectors ? -1 : 1) * profile.SpawnSpacing * .5f : 0;
                var position = island.Position + outward * spawnDistance + tangent * side;
                if (new Vector2(position.x, position.z).magnitude + profile.SpawnClearance > map.Radius)
                    throw new InvalidOperationException("Ship start clearance exceeds the map boundary.");
                var approach = map.Points.First(p => p.Id == "approach_" + sector);
                var direction = approach.Position - position;
                var id = "ship_" + slot;
                map.Points.Add(new WorldPoint { Id = id, Tag = "ship_spawn", Position = position, Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg });
                map.StartingAccess.Add(new StartingAccess { SpawnId = id, LocationId = island.Id, ApproachId = approach.Id, SailingDistance = direction.magnitude });
            }
            PlaceRole(map, profile, profile.ContestedSites, profile.ContestedLocations, "contested", .25f, .47f, ref rng);
            PlaceRole(map, profile, profile.Hazards, profile.HazardLocations, "hazard", .20f, .53f, ref rng);
            return map;
        }

        static List<int> SpreadOrder(int sectors)
        {
            var result = new List<int> { 0 };
            while (result.Count < sectors)
            {
                int best = -1, distance = -1;
                for (int candidate = 0; candidate < sectors; candidate++)
                {
                    if (result.Contains(candidate)) continue;
                    int nearest = result.Min(s => Mathf.Min(Mathf.Abs(s - candidate), sectors - Mathf.Abs(s - candidate)));
                    if (nearest > distance) { best = candidate; distance = nearest; }
                }
                result.Add(best);
            }
            return result;
        }

        static void PlaceRole(WorldLayout map, WorldProfile profile, LocationDefinition[] choices, int count, string role, float minRadius, float maxRadius, ref MapRandom rng)
        {
            int offset = count == 0 ? 0 : (int)(rng.Next() % choices.Length);
            for (int i = 0; i < count; i++)
            {
                var definition = choices[(i + offset) % choices.Length];
                bool placed = false;
                for (int attempt = 0; attempt < 1500; attempt++)
                {
                    float angle = rng.Range(0, Mathf.PI * 2);
                    float distance = rng.Range(map.Radius * minRadius, map.Radius * maxRadius);
                    if (profile.StartingSectors == 16 && profile.ContestedLocations == 4 && profile.HazardLocations == 8 && attempt < 80)
                    {
                        float rotation = map.Locations[0].Yaw - 180;
                        if (role == "contested")
                        {
                            angle = (rotation + i * 90 + rng.Range(-2, 2)) * Mathf.Deg2Rad;
                            distance = map.Radius * .36f + rng.Range(-5, 5);
                        }
                        else
                        {
                            angle = (rotation + i / 2 * 90 + (i % 2 == 0 ? 45 : 56.25f) + rng.Range(-1, 1)) * Mathf.Deg2Rad;
                            distance = map.Radius * (i % 2 == 0 ? .227f : .483f) + rng.Range(-3, 3);
                        }
                    }
                    var p = new Vector3(Mathf.Sin(angle) * distance, map.SeaLevel, Mathf.Cos(angle) * distance);
                    float footprint = definition.Settings.Radius.y * 1.4f;
                    if (distance < footprint + 190) continue;
                    if (map.Locations.Any(l => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(l.Position.x, l.Position.z)) < l.Radius * 1.4f + footprint + profile.ShippingGap)) continue;
                    if (map.Points.Any(point => point.Tag == "ship_approach" && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(point.Position.x, point.Position.z)) < footprint + profile.RouteClearance + 10)) continue;
                    float yaw = angle * Mathf.Rad2Deg;
                    var approach = p + Quaternion.Euler(0, yaw, 0) * Vector3.back * (footprint + profile.RouteClearance + 45);
                    if (role == "contested" && map.Locations.Any(l => Vector2.Distance(new Vector2(approach.x, approach.z), new Vector2(l.Position.x, l.Position.z)) < l.Radius * 1.4f + profile.RouteClearance + 10)) continue;
                    var record = Add(map, definition, p, yaw, role + "_" + i, ref rng);
                    if (role == "contested") map.Points.Add(new WorldPoint { Id = record.Id + "_approach", Tag = "ship_approach", Position = approach, Yaw = record.Yaw });
                    placed = true; break;
                }
                if (!placed) throw new InvalidOperationException("Cannot place all " + role + " locations without blocking water gaps.");
            }
        }

        static LocationRecord Add(WorldLayout map, LocationDefinition definition, Vector3 position, float yaw, string id, ref MapRandom rng)
        {
            var type = definition.Settings;
            var record = new LocationRecord { Id = id, Type = JsonUtility.FromJson<LocationSettings>(JsonUtility.ToJson(type)), Position = position, Radius = rng.Range(type.Radius.x, type.Radius.y), Height = rng.Range(type.Height.x, type.Height.y), Yaw = yaw, Seed = rng.Next() };
            map.Locations.Add(record);
            var pointRng = new MapRandom(record.Seed ^ 0x9e3779b9u);
            WorldGenerator.AddPoints(map, record, definition, ref pointRng);
            return record;
        }
    }
}
