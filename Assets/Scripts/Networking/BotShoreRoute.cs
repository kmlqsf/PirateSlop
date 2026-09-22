using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotShoreRoute : IBotPathSearch
    {
        sealed class Node
        {
            public Vector3 Point;
            public float Cost, Estimate;
            public Node Parent;
            public bool Closed;
        }
        const float Cell = 1.5f;
        readonly NetworkPlayer player;
        readonly RaycastHit[] hits = new RaycastHit[24];
        readonly Collider[] overlaps = new Collider[24];
        readonly Dictionary<Vector2Int, Node> nodes = new();
        readonly List<Node> open = new();
        readonly List<Vector3> path = new();
        readonly List<Vector3> goals = new();
        Vector3 start;
        int index;
        public bool Searching { get; private set; }
        public bool Failed { get; private set; }
        public bool Ready => !Searching && !Failed && path.Count > 0;
        public Vector3 Waypoint => path[Mathf.Min(index, path.Count - 1)];
        public BotShoreRoute(NetworkPlayer player) => this.player = player;
        static Vector2Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x / Cell), Mathf.RoundToInt(p.z / Cell));
        public void Clear() { nodes.Clear(); open.Clear(); path.Clear(); goals.Clear(); Searching = Failed = false; index = 0; }
        bool Ignore(Collider collider) => collider.transform.IsChildOf(player.transform) || collider.GetComponentInParent<AdvancedPlayerController>() != null;
        public bool Sample(Vector3 near, out Vector3 point)
        {
            point = near;
            if (OceanSurface.Instance == null) return false;
            float sea = OceanSurface.Instance.Height(near);
            int count = Physics.RaycastNonAlloc(new Vector3(near.x, sea + 65f, near.z), Vector3.down, hits, 100f, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            float height = sea - (player.GetComponent<NetworkEquipment>().WaterRunning ? 0f : 1.25f);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (Ignore(hit.collider) || hit.rigidbody != null || hit.point.y <= height) continue;
                if (hit.normal.y < .7f) return false;
                height = hit.point.y + .04f;
            }
            point.y = height;
            int occupied = Physics.OverlapCapsuleNonAlloc(point + Vector3.up * .55f, point + Vector3.up * 1.45f,
                .35f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (occupied == overlaps.Length) return false;
            for (int i = 0; i < occupied; i++) if (!Ignore(overlaps[i])) return false;
            return true;
        }
        public bool Edge(Vector3 a, Vector3 b, bool people = false)
        {
            if (Mathf.Abs(a.y - b.y) > Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) * .7f + .2f) return false;
            if (!Sample((a + b) * .5f, out var middle) || Mathf.Abs(middle.y - (a.y + b.y) * .5f) > .5f) return false;
            var delta = b - a;
            int count = Physics.CapsuleCastNonAlloc(a + Vector3.up * .55f, a + Vector3.up * 1.45f, .33f,
                delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(player.transform) || !people && hit.collider.GetComponentInParent<AdvancedPlayerController>() != null) continue;
                if (hit.normal.y < .7f) return false;
            }
            return true;
        }
        public void Begin(Vector3 origin, Vector3 destination, bool dryGoals = false)
        {
            Clear();
            if (!Sample(origin, out start)) { Failed = true; return; }
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * .25f;
                var offset = i == 8 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.8f;
                if (Sample(destination + offset, out var goal) && (!dryGoals || goal.y >= OceanSurface.Instance.Height(goal) - .3f)) goals.Add(goal);
            }
            if (goals.Count == 0) { Failed = true; return; }
            var first = new Node { Point = start, Estimate = Estimate(start) };
            nodes.Add(Key(start), first); open.Add(first); Searching = true;
            SessionController.Instance.BotPaths.Enqueue(this);
        }
        float Estimate(Vector3 point)
        {
            float best = float.PositiveInfinity;
            foreach (var goal in goals) best = Mathf.Min(best, Vector3.Distance(point, goal));
            return best;
        }
        public void Expand()
        {
            if (!Searching) return;
            if (player == null || !player.IsSpawned || open.Count == 0 || nodes.Count >= 1200)
            { Searching = false; Failed = true; return; }
            int best = 0;
            for (int i = 1; i < open.Count; i++) if (open[i].Cost + open[i].Estimate < open[best].Cost + open[best].Estimate) best = i;
            var current = open[best]; open.RemoveAt(best); current.Closed = true;
            foreach (var goal in goals)
                if (Vector3.Distance(current.Point, goal) < Cell * 1.5f && Edge(current.Point, goal))
                {
                    path.Add(goal);
                    for (var node = current; node.Parent != null; node = node.Parent) path.Add(node.Point);
                    path.Reverse(); Searching = false; return;
                }
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                var near = current.Point + new Vector3(x * Cell, 0, z * Cell);
                if ((near - start).sqrMagnitude > 150f * 150f || !Sample(near, out var point)) continue;
                float cost = current.Cost + Vector3.Distance(current.Point, point);
                var key = Key(point);
                if (nodes.TryGetValue(key, out var known) && (known.Closed || known.Cost <= cost)) continue;
                if (!Edge(current.Point, point)) continue;
                if (known == null) { known = new Node(); nodes.Add(key, known); open.Add(known); }
                known.Point = point; known.Parent = current; known.Cost = cost; known.Estimate = Estimate(point) * 1.1f;
            }
        }
        public bool Advance(Vector3 point)
        {
            while (Ready && new Vector2(point.x - Waypoint.x, point.z - Waypoint.z).magnitude < .45f && Mathf.Abs(point.y - Waypoint.y) < 1f)
            {
                if (index == path.Count - 1) return true;
                index++;
            }
            return false;
        }
    }
}
