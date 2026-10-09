using PirateSlop.Networking;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.World
{
    public static class WorldStructureCollision
    {
        public const int Revision = 3;
        public static void Ensure(GameObject root)
        {
            var coastalRoots = new HashSet<Transform>();
            foreach (var visual in root.GetComponentsInChildren<CoastalRockVisual>(true))
            {
                var owner = visual.transform.parent;
                var collision = owner != null ? owner.Find("CoastalCollision") : null;
                if (collision == null) continue;
                foreach (var collider in collision.GetComponentsInChildren<Collider>(true))
                    if (collider.enabled && !collider.isTrigger) { coastalRoots.Add(owner); break; }
            }
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                bool coastalManaged = false;
                for (var parent = filter.transform; parent != null; parent = parent.parent)
                {
                    if (coastalRoots.Contains(parent)) { coastalManaged = true; break; }
                    if (parent == root.transform) break;
                }
                if (coastalManaged) continue;
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null ||
                    filter.GetComponentInParent<CoastalRockVisual>() != null ||
                    filter.GetComponentInParent<ShipController>() != null || filter.GetComponentInParent<NetworkShip>() != null ||
                    filter.GetComponentInParent<CombatHealth>() != null || filter.GetComponentInParent<Rigidbody>() != null) continue;
                bool covered = false;
                for (var parent = filter.transform; parent != null; parent = parent.parent)
                {
                    foreach (var collider in parent.GetComponents<Collider>())
                        if (collider.enabled && !collider.isTrigger) { covered = true; break; }
                    if (covered || parent == root.transform) break;
                }
                if (covered) continue;
                if (filter.transform.parent != null)
                {
                    foreach (var sibling in filter.transform.parent.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (sibling.transform.parent != filter.transform.parent ||
                            !sibling.name.EndsWith("_COL", System.StringComparison.OrdinalIgnoreCase)) continue;
                        var authored = sibling.GetComponent<Collider>();
                        if (authored != null && authored.enabled && !authored.isTrigger) { covered = true; break; }
                    }
                    if (covered) continue;
                }
                var group = filter.GetComponentInParent<LODGroup>();
                if (group != null)
                {
                    var levels = group.GetLODs();
                    if (levels.Length > 0 && System.Array.IndexOf(levels[0].renderers, filter.GetComponent<Renderer>()) < 0) continue;
                }
                if (filter.sharedMesh.isReadable) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                else
                {
                    var collider = filter.gameObject.AddComponent<BoxCollider>();
                    collider.center = filter.sharedMesh.bounds.center; collider.size = filter.sharedMesh.bounds.size;
                }
            }
        }
    }
}
