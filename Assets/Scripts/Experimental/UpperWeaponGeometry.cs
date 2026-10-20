#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public static class UpperWeaponGeometry
    {
        public static Bounds[] Wheels(Transform root)
        {
            var parts = new List<(Bounds Bounds, bool Named)>();
            var whole = new Bounds(); bool any = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var local = filter.sharedMesh.bounds; var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
                for (int corner = 0; corner < 8; corner++) bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                string name = filter.name + filter.sharedMesh.name;
                for (var parent = filter.transform.parent; parent != null && parent != root; parent = parent.parent) name += parent.name;
                parts.Add((bounds, name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0));
                if (!any) { whole = bounds; any = true; } else whole.Encapsulate(bounds);
            }
            var candidates = parts.Where(p => p.Named || p.Bounds.min.y < whole.min.y + .08f && p.Bounds.size.y > .08f && p.Bounds.size.x < whole.size.x * .65f).OrderBy(p => p.Named ? 0 : 1).ThenBy(p => p.Bounds.min.y).ToArray();
            var wheels = new List<Bounds>(4);
            foreach (var candidate in candidates)
            {
                if (wheels.Any(w => (w.center - candidate.Bounds.center).sqrMagnitude < .0064f)) continue;
                wheels.Add(candidate.Bounds);
                if (wheels.Count == 4) break;
            }
            return wheels.Count == 4 ? wheels.ToArray() : Array.Empty<Bounds>();
        }
    }
}
#endif
