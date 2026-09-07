using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class WorldRoutePlanner
    {
        readonly WorldLayout map;
        readonly float cell, clearance;
        readonly int size;
        readonly bool[] water;
        readonly int[] dx = { 1, 0, -1, 0 }, dz = { 0, 1, 0, -1 };

        WorldRoutePlanner(WorldLayout layout, float cellSize, float margin)
        {
            map = layout; cell = cellSize; clearance = margin;
            size = Mathf.CeilToInt(map.Radius * 2 / cell) + 1;
            if (size > 251) throw new InvalidOperationException("Navigation grid exceeds 251 cells per side.");
            water = new bool[size * size];
            for (int i = 0; i < water.Length; i++) water[i] = Clear(Position(i), Position(i));
        }

        public static void Connect(WorldLayout map, float cellSize, float clearance)
        {
            var planner = new WorldRoutePlanner(map, cellSize, clearance);
            var hub = new WorldPoint { Id = "water_hub", Tag = "sea_route", Position = Vector3.up * map.SeaLevel };
            map.Points.Add(hub);
            foreach (var access in map.StartingAccess)
            {
                var start = map.Points.First(p => p.Id == access.SpawnId);
                var approach = map.Points.First(p => p.Id == access.ApproachId);
                if (!planner.Clear(start.Position, approach.Position)) throw new InvalidOperationException("Opening supply route is obstructed.");
                planner.Add(start, approach, new List<Vector3> { start.Position, approach.Position });
            }
            var approaches = map.Points.Where(p => p.Tag == "ship_approach").ToArray();
            foreach (var approach in approaches) planner.Add(approach, hub, planner.Path(approach.Position, hub.Position, false));
            var ring = approaches.Where(p => p.Id.StartsWith("approach_")).OrderBy(p => Mathf.Atan2(p.Position.x, p.Position.z)).ToArray();
            for (int i = 0; i < ring.Length; i++)
            {
                var from = ring[i]; var to = ring[(i + 1) % ring.Length];
                planner.Add(from, to, planner.Path(from.Position, to.Position, true));
            }
        }

        void Add(WorldPoint from, WorldPoint to, List<Vector3> points)
        {
            map.Routes.Add(new WorldRoute { Id = "route_" + map.Routes.Count, FromId = from.Id, ToId = to.Id, Waypoints = points });
        }

        List<Vector3> Path(Vector3 from, Vector3 to, bool aroundCentre)
        {
            if (Clear(from, to) && (!aroundCentre || SegmentDistance(Vector3.zero, from, to) >= 220)) return new List<Vector3> { from, to };
            int start = Nearest(from), target = Nearest(to);
            var previous = Enumerable.Repeat(-1, water.Length).ToArray();
            var queue = new Queue<int>(); previous[start] = start; queue.Enqueue(start);
            while (queue.Count > 0 && previous[target] < 0)
            {
                int current = queue.Dequeue(), x = current % size, z = current / size;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + dx[direction], nz = z + dz[direction];
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                    int next = nz * size + nx;
                    if (!water[next] || previous[next] >= 0 || (aroundCentre && Horizontal(Position(next)).magnitude < 220)) continue;
                    if (!Clear(Position(current), Position(next))) continue;
                    previous[next] = current; queue.Enqueue(next);
                }
            }
            if (previous[target] < 0) throw new InvalidOperationException("A required sailing route is disconnected.");
            var reverse = new List<Vector3> { to };
            for (int index = target; index != start; index = previous[index]) reverse.Add(Position(index));
            reverse.Add(Position(start)); reverse.Add(from); reverse.Reverse();
            var path = new List<Vector3> { from };
            int anchor = 0;
            while (anchor < reverse.Count - 1)
            {
                int end = reverse.Count - 1;
                while (end > anchor + 1 && (!Clear(reverse[anchor], reverse[end]) || (aroundCentre && SegmentDistance(Vector3.zero, reverse[anchor], reverse[end]) < 220))) end--;
                path.Add(reverse[end]); anchor = end;
            }
            return path;
        }

        int Nearest(Vector3 point)
        {
            int best = -1;
            float distance = float.PositiveInfinity;
            int cx = Mathf.RoundToInt((point.x + map.Radius) / cell), cz = Mathf.RoundToInt((point.z + map.Radius) / cell);
            for (int z = Mathf.Max(0, cz - 3); z <= Mathf.Min(size - 1, cz + 3); z++)
                for (int x = Mathf.Max(0, cx - 3); x <= Mathf.Min(size - 1, cx + 3); x++)
                {
                    int index = z * size + x;
                    if (!water[index]) continue;
                    float d = (Position(index) - point).sqrMagnitude;
                    if (d >= distance || !Clear(point, Position(index))) continue;
                    best = index; distance = d;
                }
            if (best < 0) throw new InvalidOperationException("A route endpoint has no clear connection to open water.");
            return best;
        }

        bool Clear(Vector3 a, Vector3 b)
        {
            if (Horizontal(a).magnitude > map.Radius - clearance - 15 || Horizontal(b).magnitude > map.Radius - clearance - 15) return false;
            foreach (var location in map.Locations)
                if (SegmentDistance(location.Position, a, b) < location.Radius * 1.4f + clearance) return false;
            return true;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = Horizontal(b - a);
            float t = ab.sqrMagnitude < .001f ? 0 : Mathf.Clamp01(Vector2.Dot(Horizontal(p - a), ab) / ab.sqrMagnitude);
            return (Horizontal(p - a) - ab * t).magnitude;
        }
        static Vector2 Horizontal(Vector3 p) => new Vector2(p.x, p.z);
        Vector3 Position(int index) => new Vector3(index % size * cell - map.Radius, map.SeaLevel, index / size * cell - map.Radius);
    }
}
