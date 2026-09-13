using System.Collections.Generic;

namespace PirateSlop
{
    public sealed class ShipStructuralGraph
    {
        readonly Dictionary<int, List<int>> children = new();
        readonly Dictionary<int, ShipSectionDefinition> definitions = new();
        readonly ShipFragmentConnection[] fragments;
        public ShipStructuralGraph(ShipDestructionProfile profile)
        {
            fragments = profile.Structure;
            foreach (var definition in profile.Sections)
            {
                if (!definitions.TryAdd(definition.SectionId, definition)) throw new System.ArgumentException("Duplicate SectionId " + definition.SectionId);
                children[definition.SectionId] = new List<int>();
            }
            foreach (var definition in profile.Sections)
                foreach (int support in definition.Supports)
                {
                    if (!children.ContainsKey(support)) throw new System.ArgumentException("Missing support " + support);
                    children[support].Add(definition.SectionId);
                }
            foreach (var id in definitions.Keys) Visit(id, new HashSet<int>(), new HashSet<int>());
        }
        void Visit(int id, HashSet<int> path, HashSet<int> done)
        {
            if (done.Contains(id)) return;
            if (!path.Add(id)) throw new System.ArgumentException("Structural cycle at " + id);
            foreach (int child in children[id]) Visit(child, path, done);
            path.Remove(id); done.Add(id);
        }
        public IEnumerable<int> Dependents(int id) => children[id];
        public Dictionary<int, ulong> Unsupported(System.Func<int, ulong> removed)
        {
            var reached = new bool[fragments.Length];
            var queue = new Queue<int>();
            for (int i = 0; i < fragments.Length; i++)
                if (fragments[i].Anchor && (removed(fragments[i].SectionId) & (1UL << fragments[i].Fragment)) == 0)
                { reached[i] = true; queue.Enqueue(i); }
            while (queue.Count > 0)
            {
                var node = fragments[queue.Dequeue()];
                foreach (int next in node.Neighbours)
                {
                    var target = fragments[next];
                    if (!node.LoadBearing && target.LoadBearing) continue;
                    if (reached[next] || (removed(target.SectionId) & (1UL << target.Fragment)) != 0) continue;
                    reached[next] = true; queue.Enqueue(next);
                }
            }
            var result = new Dictionary<int, ulong>();
            for (int i = 0; i < fragments.Length; i++)
            {
                var node = fragments[i];
                ulong mask = removed(node.SectionId);
                if (reached[i] || (mask & (1UL << node.Fragment)) != 0) continue;
                result.TryGetValue(node.SectionId, out ulong detached);
                result[node.SectionId] = detached | (1UL << node.Fragment);
            }
            return result;
        }
        public bool Supported(int id, System.Func<int, bool> alive)
        {
            var definition = definitions[id];
            if (definition.Supports.Length == 0) return true;
            int remaining = 0;
            foreach (int support in definition.Supports) if (alive(support)) remaining++;
            return definition.RequireAllSupports ? remaining == definition.Supports.Length : remaining > 0;
        }
    }
}
