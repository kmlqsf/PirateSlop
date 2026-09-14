using UnityEngine;

namespace PirateSlop.Networking
{
    public static class LootPlacement
    {
        public static bool Find(Transform owner, NetworkFish prefab, InventoryItem item, out Vector3 point, out Quaternion rotation, out NetworkShip support)
        {
            point = default; rotation = Quaternion.identity; support = null;
            var box = prefab.GetComponent<BoxCollider>();
            var sphere = prefab.GetComponent<SphereCollider>();
            Vector3 size = box != null ? Vector3.Scale(box.size, prefab.transform.localScale) : Vector3.one * (sphere != null ? sphere.radius * 2f * Mathf.Max(prefab.transform.localScale.x, prefab.transform.localScale.y, prefab.transform.localScale.z) : .25f);
            Vector3 center = box != null ? Vector3.Scale(box.center, prefab.transform.localScale) : Vector3.zero;
            for (int attempt = 0; attempt < 9; attempt++)
            {
                float side = attempt == 0 ? 0f : (attempt % 2 == 0 ? 1f : -1f) * ((attempt + 1) / 2) * .45f;
                Vector3 origin = owner.position + Vector3.up * 1.5f + owner.forward * .9f + owner.right * side;
                Vector3 from = owner.position + Vector3.up * 1.5f;
                if (FirearmTrace.Cast(owner.gameObject, from, origin, out _)) continue;
                RaycastHit floor = default;
                float distance = 5f;
                foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(owner) && hit.collider.GetComponentInParent<NetworkFish>() == null && hit.collider.GetComponentInParent<AdvancedPlayerController>() == null && hit.normal.y > .55f && hit.distance < distance)
                    { floor = hit; distance = hit.distance; }
                if (floor.collider == null) continue;
                rotation = Quaternion.FromToRotation(Vector3.up, floor.normal) * Quaternion.Euler(0, owner.eulerAngles.y, item == InventoryItem.Fish ? 90f : 0f);
                float lowest = float.PositiveInfinity;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = center + Vector3.Scale(size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    lowest = Mathf.Min(lowest, Vector3.Dot(rotation * corner, floor.normal));
                }
                point = floor.point + floor.normal * (.035f - lowest);
                bool blocked = false;
                foreach (var collider in Physics.OverlapBox(point + rotation * center, size * .48f, rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (!collider.transform.IsChildOf(owner)) { blocked = true; break; }
                if (blocked) continue;
                support = floor.collider.GetComponentInParent<NetworkShip>();
                return true;
            }
            return false;
        }
    }
}
