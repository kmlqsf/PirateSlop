using System;
using System.Linq;
using UnityEngine;

namespace PirateSlop.World
{
    public static class WorldDecorationPlacer
    {
        public const string Tag = "environment_decoration";

        public static void Place(WorldLayout map, WorldProfile profile)
        {
            var definitions = profile.Decorations ?? Array.Empty<WorldDecoration>();
            var rng = new MapRandom(unchecked((uint)map.Seed) ^ 0xD15EA5E5u);
            for (int index = 0; index < definitions.Length; index++)
            {
                var decoration = definitions[index];
                if (decoration == null || decoration.Prefab == null || decoration.ClearanceRadius < 5 || decoration.MinCount < 1 || decoration.MaxCount < decoration.MinCount)
                    throw new InvalidOperationException("Invalid environment decoration at index " + index);
                int count = decoration.MinCount + (int)(rng.Next() % (uint)(decoration.MaxCount - decoration.MinCount + 1));
                for (int copy = 0; copy < count; copy++)
                {
                    bool placed = false;
                    for (int attempt = 0; attempt < 2000; attempt++)
                    {
                        float angle = rng.Range(0f, Mathf.PI * 2f);
                        float distance = Mathf.Sqrt(rng.Value()) * (map.Radius - decoration.ClearanceRadius - 80f);
                        var position = new Vector3(Mathf.Sin(angle) * distance, map.SeaLevel, Mathf.Cos(angle) * distance);
                        var point = new Vector2(position.x, position.z);
                        if (distance < decoration.ClearanceRadius + 160f) continue;
                        if (map.Locations.Any(l => Vector2.Distance(point, new Vector2(l.Position.x, l.Position.z)) < decoration.ClearanceRadius + l.Radius * 1.4f + profile.ShippingGap)) continue;
                        if (map.Points.Any(p => p.Tag == "ship_spawn" || p.Tag == "ship_approach" ? Vector2.Distance(point, new Vector2(p.Position.x, p.Position.z)) < decoration.ClearanceRadius + profile.SpawnClearance + 30f : p.Tag == Tag && Vector2.Distance(point, new Vector2(p.Position.x, p.Position.z)) < decoration.ClearanceRadius + definitions[p.Rule].ClearanceRadius + profile.ShippingGap)) continue;
                        if (map.Routes.Any(r => RouteTooClose(r, point, decoration.ClearanceRadius + profile.RouteClearance + 26f))) continue;
                        map.Points.Add(new WorldPoint { Id = "environment_" + index + "_" + copy, Tag = Tag, TypeId = decoration.Prefab.name, Rule = index, Position = position, Yaw = rng.Range(0f, 360f) });
                        placed = true;
                        break;
                    }
                    if (!placed) throw new InvalidOperationException("Cannot place environment decoration " + decoration.Prefab.name + ".");
                }
            }
        }

        static bool RouteTooClose(WorldRoute route, Vector2 point, float clearance)
        {
            for (int i = 1; i < route.Waypoints.Count; i++)
            {
                var a = new Vector2(route.Waypoints[i - 1].x, route.Waypoints[i - 1].z);
                var b = new Vector2(route.Waypoints[i].x, route.Waypoints[i].z);
                var segment = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / Mathf.Max(.0001f, segment.sqrMagnitude));
                if (Vector2.Distance(point, a + segment * t) < clearance) return true;
            }
            return false;
        }
    }
}
