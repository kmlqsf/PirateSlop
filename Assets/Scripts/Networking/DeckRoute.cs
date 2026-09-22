using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public interface IBotPathSearch
    {
        bool Searching { get; }
        void Expand();
    }

    public sealed class BotPathScheduler
    {
        static readonly Unity.Profiling.ProfilerMarker marker = new("Bots.Paths");
        readonly Queue<IBotPathSearch> pending = new();
        public void Enqueue(IBotPathSearch route) { if (!pending.Contains(route)) pending.Enqueue(route); }
        public void Tick(int budget, float milliseconds)
        {
            using var sample = marker.Auto();
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            double limit = Mathf.Clamp(milliseconds, .25f, 4f) * System.Diagnostics.Stopwatch.Frequency / 1000.0;
            for (int i = 0; i < Mathf.Clamp(budget, 1, 64) && pending.Count > 0; i++)
            {
                var route = pending.Dequeue();
                if (route.Searching) route.Expand();
                if (route.Searching) pending.Enqueue(route);
                if (System.Diagnostics.Stopwatch.GetTimestamp() - started >= limit) break;
            }
        }
        public void Clear() => pending.Clear();
    }

    public sealed class DeckRoute : IBotPathSearch
    {
        sealed class Node
        {
            public Vector3 Point;
            public float Cost, Estimate;
            public Node Parent;
            public bool Closed;
        }

        readonly NetworkPlayer player;
        readonly CharacterController capsule;
        readonly BotMotionSettings settings;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        readonly Dictionary<Vector3Int, Node> nodes = new();
        readonly List<Node> open = new();
        readonly List<Vector3> path = new();
        readonly List<Vector3> goals = new();
        NetworkShip ship;
        bool avoidPlayers;
        Vector3 start, goal;
        int index;
        public bool Searching { get; private set; }
        public bool Failed { get; private set; }
        public bool Ready => !Searching && !Failed && path.Count > 0;
        public int Remaining => Mathf.Max(0, path.Count - index);
        public Vector3 Waypoint => path[Mathf.Min(index, path.Count - 1)];
        public Vector3 Destination => goal;
        float Radius => capsule.radius + .035f;
        float Cell => Mathf.Clamp(settings.CellSize, .3f, 1f);

        public DeckRoute(NetworkPlayer player, BotMotionSettings settings)
        {
            this.player = player; this.settings = settings;
            capsule = player.GetComponent<CharacterController>();
        }

        public void Clear()
        {
            Searching = Failed = false; index = 0;
            nodes.Clear(); open.Clear(); path.Clear(); goals.Clear();
        }
        public void Configure(NetworkShip platform) => ship = platform;

        Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x / Cell), Mathf.RoundToInt(p.y / .15f), Mathf.RoundToInt(p.z / Cell));

        public bool Ground(Vector3 near, out Vector3 point, float heightRange = .34f)
        {
            if (GroundSample(near, out point, heightRange)) return true;
            float radius = capsule.radius * .8f;
            for (int i = 0; i < 4; i++)
            {
                var offset = i < 2 ? Vector3.right * (i == 0 ? radius : -radius) : Vector3.forward * (i == 2 ? radius : -radius);
                if (!GroundSample(near + offset, out var support, heightRange)) continue;
                point = new Vector3(near.x, support.y, near.z);
                return true;
            }
            return false;
        }

        bool GroundSample(Vector3 near, out Vector3 point, float heightRange)
        {
            point = default;
            if (ship == null || ship.IsSinking) return false;
            var world = ship.transform.TransformPoint(near);
            float probe = Mathf.Max(.5f, heightRange + .1f);
            int count = Physics.RaycastNonAlloc(world + Vector3.up * probe, Vector3.down, hits, probe * 2f + .05f, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.rigidbody != ship.Body || hit.normal.y < .7f || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null) continue;
                var local = ship.transform.InverseTransformPoint(hit.point + Vector3.up * .025f);
                float difference = Mathf.Abs(local.y - near.y);
                if (difference > heightRange || local.y <= best) continue;
                best = local.y; point = local;
            }
            return best > float.NegativeInfinity;
        }

        bool Ignore(Collider collider, bool includePlayers)
        {
            if (collider.transform.IsChildOf(player.transform)) return true;
            return !includePlayers && collider.GetComponentInParent<AdvancedPlayerController>() != null;
        }

        void Capsule(Vector3 foot, out Vector3 bottom, out Vector3 top, bool full = false)
        {
            var center = foot + Vector3.up * capsule.center.y;
            float half = Mathf.Max(0, capsule.height * .5f - Radius);
            bottom = center - Vector3.up * Mathf.Max(0, half - (full ? 0 : .42f));
            top = center + Vector3.up * half;
        }

        public bool ClearAt(Vector3 point, bool includePlayers = false, bool full = false)
        {
            Capsule(ship.transform.TransformPoint(point), out var bottom, out var top, full);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, Radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!Ignore(overlaps[i], includePlayers)) return false;
            return true;
        }

        public bool PlayerBlocking(Vector3 point)
        {
            Capsule(ship.transform.TransformPoint(point), out var bottom, out var top, true);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, Radius + .2f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return true;
            for (int i = 0; i < count; i++)
                if (!overlaps[i].transform.IsChildOf(player.transform) && overlaps[i].GetComponentInParent<AdvancedPlayerController>() != null) return true;
            return false;
        }

        public NetworkPlayer BlockingBot(Vector3 point)
        {
            Capsule(ship.transform.TransformPoint(point), out var bottom, out var top, true);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, Radius + .2f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = overlaps[i].GetComponentInParent<NetworkPlayer>();
                if (other != null && other != player && other.IsBot.Value && other.Ship == player.Ship) return other;
            }
            return null;
        }

        public bool Edge(Vector3 from, Vector3 to, bool includePlayers = false)
        {
            if (Mathf.Abs(to.y - from.y) < .15f)
                return Ground((from + to) * .5f, out _) && ClearAt(to, includePlayers) && Sweep(from, to, includePlayers);
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .2f));
            var previous = from;
            for (int i = 1; i <= samples; i++)
            {
                if (!Ground(Vector3.Lerp(from, to, i / (float)samples), out var point) ||
                    Mathf.Abs(point.y - previous.y) > .4f || !ClearAt(point, includePlayers) ||
                    !Sweep(previous, point, includePlayers)) return false;
                previous = point;
            }
            return true;
        }

        bool Sweep(Vector3 from, Vector3 to, bool includePlayers)
        {
            var delta = ship.transform.TransformVector(to - from);
            if (delta.sqrMagnitude < .0001f) return true;
            Capsule(ship.transform.TransformPoint(from), out var bottom, out var top);
            int count = Physics.CapsuleCastNonAlloc(bottom, top, Radius, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (!Ignore(hits[i].collider, includePlayers) && hits[i].normal.y < .7f) return false;
            return true;
        }

        public void Begin(NetworkShip platform, Vector3 origin, Vector3 destination)
            => Begin(platform, origin, new[] { destination });

        public void Begin(NetworkShip platform, Vector3 origin, IReadOnlyList<Vector3> destinations, bool includePlayers = false)
        {
            Clear(); ship = platform; start = origin; avoidPlayers = includePlayers;
            foreach (var destination in destinations)
                if (Ground(destination, out var supported) && ClearAt(supported)) goals.Add(supported);
            if (!Ground(start, out start) || goals.Count == 0) { Failed = true; return; }
            int previousGoal = -1;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int closest = -1;
                float distance = 36f;
                for (int i = 0; i < goals.Count; i++)
                {
                    float candidate = (goals[i] - start).sqrMagnitude;
                    if (i == previousGoal || candidate >= distance || Mathf.Abs(goals[i].y - start.y) > .15f) continue;
                    closest = i; distance = candidate;
                }
                if (closest < 0) break;
                previousGoal = closest;
                var from = start;
                bool clear = true;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(distance) / .4f));
                for (int step = 1; step <= steps; step++)
                {
                    var to = Vector3.Lerp(start, goals[closest], step / (float)steps);
                    if (!Ground(to, out var supported) || Mathf.Abs(supported.y - to.y) > .15f || !Edge(from, supported, includePlayers))
                    { clear = false; break; }
                    from = supported;
                }
                if (!clear) continue;
                goal = goals[closest]; path.Add(goal); return;
            }
            var first = new Node { Point = start, Estimate = Estimate(start) };
            nodes.Add(Key(start), first); open.Add(first); Searching = true;
            SessionController.Instance.BotPaths.Enqueue(this);
        }

        float Estimate(Vector3 point)
        {
            float best = float.PositiveInfinity;
            foreach (var target in goals) best = Mathf.Min(best, Vector3.Distance(point, target));
            return best;
        }

        public void Expand()
        {
            if (!Searching) return;
            if (player == null || !player.IsSpawned || ship == null || ship.IsSinking || open.Count == 0 || nodes.Count >= Mathf.Clamp(settings.MaxPathNodes, 32, 2000))
            { Searching = false; Failed = true; return; }
            int best = 0;
            for (int i = 1; i < open.Count; i++) if (open[i].Cost + open[i].Estimate < open[best].Cost + open[best].Estimate) best = i;
            var current = open[best]; open.RemoveAt(best); current.Closed = true;
            foreach (var target in goals)
            {
                if (Vector3.Distance(current.Point, target) > Cell * 1.5f || !Edge(current.Point, target, avoidPlayers)) continue;
                goal = target;
                path.Add(goal);
                for (var node = current; node.Parent != null; node = node.Parent) path.Add(node.Point);
                path.Reverse(); Searching = false; return;
            }
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                var near = current.Point + new Vector3(x * Cell, 0, z * Cell);
                if ((near - start).sqrMagnitude > settings.SearchRadius * settings.SearchRadius || !Ground(near, out var point, Cell + .1f)) continue;
                var key = Key(point);
                float cost = current.Cost + Vector3.Distance(current.Point, point);
                if (nodes.TryGetValue(key, out var known) && (known.Closed || known.Cost <= cost)) continue;
                if (!Edge(current.Point, point, avoidPlayers)) continue;
                if (known == null) { known = new Node(); nodes.Add(key, known); open.Add(known); }
                known.Point = point; known.Cost = cost; known.Estimate = Estimate(point); known.Parent = current;
            }
        }

        float nextShortcut;
        public bool Advance(Vector3 current)
        {
            if (Ready && Time.time >= nextShortcut)
            {
                nextShortcut = Time.time + .15f;
                for (int look = Mathf.Min(index + 3, path.Count - 1); look > index; look--)
                {
                    if ((path[look] - current).sqrMagnitude > 2.25f || Mathf.Abs(path[look].y - current.y) > .15f) continue;
                    bool level = true;
                    for (int k = index; k <= look; k++) if (Mathf.Abs(path[k].y - current.y) > .15f) level = false;
                    if (level && Edge(current, path[look], true)) { index = look; break; }
                }
            }
            while (Ready && Reached(current, Waypoint))
            {
                if (index == path.Count - 1) return true;
                index++;
            }
            return false;
        }

        static bool Reached(Vector3 current, Vector3 target)
        {
            var difference = current - target;
            if (difference.sqrMagnitude < .09f) return true;
            return difference.y >= 0f && difference.y <= .5f &&
                difference.x * difference.x + difference.z * difference.z < .0625f;
        }

        public bool RecoveryPoint(Vector3 current, Vector3 lastSafe, out Vector3 destination)
        {
            destination = default;
            if (Vector3.Distance(current, lastSafe) > settings.RecoveryRadius || !Ground(lastSafe, out var anchor) || !ClearAt(anchor, true, true)) return false;
            float best = float.PositiveInfinity;
            for (int i = 0; i < 17; i++)
            {
                float angle = i * Mathf.PI * .25f;
                float radius = i == 0 ? 0 : i <= 8 ? .5f : 1f;
                var near = anchor + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (!Ground(near, out var point) || Mathf.Abs(point.y - current.y) > .34f || !ClearAt(point, true, true) || !Edge(anchor, point, true)) continue;
                float distance = Vector3.Distance(current, point);
                var world = ship.transform.TransformPoint(point);
                var ocean = OceanSurface.Instance;
                if (ocean != null && ocean.Height(world) > world.y - .2f) continue;
                if (distance < .45f || distance > settings.RecoveryRadius || distance >= best) continue;
                destination = point; best = distance;
            }
            return best < float.PositiveInfinity;
        }
    }
}
