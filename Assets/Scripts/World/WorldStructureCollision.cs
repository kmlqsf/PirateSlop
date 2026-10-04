using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    public static class WorldStructureCollision
    {
        public const int Revision = 2;
        public static void Ensure(GameObject root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null ||
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
