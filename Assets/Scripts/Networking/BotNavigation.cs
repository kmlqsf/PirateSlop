using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotNavigation
    {
        sealed class Node
        {
            public Vector3 Point;
            public Node Parent;
            public float Cost, Score;
            public bool Closed;
        }
        readonly List<Node> open = new();
        readonly Dictionary<Vector3Int, Node> nodes = new();
        readonly List<Vector3> path = new();
        Transform frame;
        Vector3 goal, start;
        int step, expanded;
        bool searching, initialized;
        float retryAt, lastProgress;
        float nextSearch;
        Vector3 lastPosition;
        public bool Failed { get; private set; }
        const float Cell = .85f;
        Vector3 World(Vector3 point) => frame != null ? frame.TransformPoint(point) : point;
        Vector3 Local(Vector3 point) => frame != null ? frame.InverseTransformPoint(point) : point;
        Vector3 Up => frame != null ? frame.up : Vector3.up;
        Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x / Cell), Mathf.RoundToInt(p.y * 2f), Mathf.RoundToInt(p.z / Cell));
        public void Clear() { initialized = searching = Failed = false; open.Clear(); nodes.Clear(); path.Clear(); step = 0; }
        void Begin(Vector3 from, Vector3 destination, Transform platform)
        {
            Clear(); initialized = true; frame = platform;
            start = Local(from); goal = Local(destination); step = expanded = 0;
            var node = new Node { Point = start, Score = Vector3.Distance(start, goal) };
            nodes[Key(start)] = node; open.Add(node); searching = true;
            lastProgress = Time.time; lastPosition = start;
            nextSearch = 0f;
        }
        bool Ground(Vector3 near, out Vector3 point)
        {
            point = default;
            float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(World(near) + Up * .8f, -Up, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Dot(hit.normal, Up) < .65f || Ignore(hit.collider)) continue;
                if (frame != null && !hit.transform.IsChildOf(frame)) continue;
                var candidate = Local(hit.point + Up * .03f);
                if (Mathf.Abs(candidate.y - near.y) > 1.1f || hit.distance >= nearest) continue;
                if (frame == null && OceanSurface.Instance != null && hit.point.y < OceanSurface.Instance.SeaLevel - .2f) continue;
                point = candidate; nearest = hit.distance;
            }
            return nearest < float.PositiveInfinity;
        }
        static bool Ignore(Collider collider) => collider.GetComponentInParent<AdvancedPlayerController>() != null ||
            collider.GetComponentInParent<NetworkFish>() != null || collider.GetComponentInParent<Cannonball>() != null;
        bool ClearStep(Vector3 a, Vector3 b)
        {
            Vector3 from = World(a), delta = World(b) - from;
            foreach (var hit in Physics.CapsuleCastAll(from + Up * .55f, from + Up * 1.45f, .25f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignore(hit.collider) && Vector3.Dot(hit.normal, Up) < .65f) return false;
            foreach (var collider in Physics.OverlapCapsule(World(b) + Up * .55f, World(b) + Up * 1.45f, .24f, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignore(collider)) return false;
            return true;
        }
        void Search()
        {
            for (int budget = 0; budget < 8 && searching; budget++)
            {
                if (open.Count == 0 || expanded++ > 1200) { searching = false; Failed = true; retryAt = Time.time + 4f; break; }
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (open[i].Score < open[best].Score) best = i;
                var current = open[best]; open.RemoveAt(best); current.Closed = true;
                if (Vector2.Distance(new Vector2(current.Point.x, current.Point.z), new Vector2(goal.x, goal.z)) < .65f && Mathf.Abs(current.Point.y - goal.y) < 1.5f)
                {
                    for (var n = current; n.Parent != null; n = n.Parent) path.Add(n.Point);
                    path.Reverse(); searching = false; break;
                }
                for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue;
                    Vector3 near = current.Point + new Vector3(x * Cell, 0, z * Cell);
                    if (frame != null && (Mathf.Abs(near.x) > 8f || Mathf.Abs(near.z) > 26f) || Vector3.Distance(near, start) > 160f) continue;
                    if (!Ground(near, out var point) || !ClearStep(current.Point, point)) continue;
                    var key = Key(point);
                    float cost = current.Cost + Vector3.Distance(current.Point, point);
                    if (nodes.TryGetValue(key, out var node))
                    { if (node.Closed || node.Cost <= cost) continue; }
                    else { node = new Node { Point = point }; nodes.Add(key, node); open.Add(node); }
                    node.Parent = current; node.Cost = cost; node.Score = cost + Vector3.Distance(point, goal) * 1.1f;
                }
            }
        }
        public PlayerCommand Move(NetworkPlayer player, Vector3 destination, Transform platform = null)
        {
            var motor = player.Motor;
            var command = new PlayerCommand { Yaw = motor.transform.eulerAngles.y };
            if (!initialized || frame != platform || Vector3.Distance(Local(destination), goal) > 1.5f || (!searching && path.Count == 0 && Time.time >= retryAt))
                Begin(motor.transform.position, destination, platform);
            if (searching)
            {
                if (Time.time >= nextSearch) { nextSearch = Time.time + .1f; Search(); }
                lastProgress = Time.time;
                return command;
            }
            if (step >= path.Count)
            {
                if (path.Count > 0) { path.Clear(); retryAt = Time.time + .5f; }
                return command;
            }
            Vector3 local = Local(motor.transform.position);
            if (Vector3.Distance(local, lastPosition) > .4f) { lastPosition = local; lastProgress = Time.time; }
            if (Time.time - lastProgress > 3f) { path.Clear(); searching = false; Failed = true; retryAt = Time.time + 1f; return command; }
            Vector3 target = World(path[step]);
            Vector3 delta = target - motor.transform.position;
            if (new Vector2(delta.x, delta.z).magnitude < .4f && Mathf.Abs(delta.y) < 1f)
            { step++; return command; }
            float desired = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            command.Yaw = Mathf.MoveTowardsAngle(command.Yaw, desired, 240f * (float)player.TimeManager.TickDelta);
            if (Mathf.Abs(Mathf.DeltaAngle(command.Yaw, desired)) < 25f) command.Move = Vector2.up * .7f;
            return command;
        }
    }
}
