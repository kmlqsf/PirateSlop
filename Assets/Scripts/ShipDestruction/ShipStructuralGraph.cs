using System.Collections.Generic;

namespace PirateSlop
{
    public sealed class ShipStructuralGraph
    {
        readonly Dictionary<int, List<int>> children = new();
        readonly Dictionary<int, ShipSectionDefinition> definitions = new();
        public ShipStructuralGraph(ShipDestructionProfile profile)
        {
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
