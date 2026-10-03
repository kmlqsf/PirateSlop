using UnityEngine;

namespace PirateSlop
{
    public static class ShipGripAim
    {
        static readonly RaycastHit[] obstacles = new RaycastHit[64];

        public static bool CanAim(Transform eyes, Transform player, Transform grip, float radius, out float score)
        {
            var shape = grip.GetComponent<Collider>();
            var renderer = shape == null ? grip.GetComponent<Renderer>() : null;
            Vector3 point = shape != null ? shape.bounds.center : renderer != null ? renderer.bounds.center : grip.position;
            float depth = Vector3.Dot(point - eyes.position, eyes.forward);
            Vector3 aimed = eyes.position + eyes.forward * Mathf.Clamp(depth, 0f, 3f);
            if (shape is MeshCollider) point = shape.bounds.ClosestPoint(aimed);
            else if (shape != null) point = shape.ClosestPoint(aimed);
            else if (renderer != null) point = renderer.bounds.ClosestPoint(aimed);
            Vector3 delta = point - eyes.position;
            depth = Vector3.Dot(delta, eyes.forward);
            float miss = Vector3.ProjectOnPlane(delta, eyes.forward).magnitude;
            score = miss / radius + delta.magnitude * .08f;
            if (depth < .05f || depth > 3f || delta.magnitude > 3.2f || miss > radius) return false;
            int count = Physics.RaycastNonAlloc(eyes.position, delta.normalized, obstacles, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == obstacles.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = obstacles[i];
                if (hit.transform.IsChildOf(player) || hit.transform.IsChildOf(grip)) continue;
                var handle = grip.GetComponent<ShipControlHandle>();
                if (handle != null && hit.collider.GetComponentInParent<ShipControlHandle>() == handle) continue;
                var target = grip.GetComponent<PirateSlop.Ships.ShipV3InteractionTarget>();
                if (target != null && hit.collider.GetComponentInParent<PirateSlop.Ships.ShipV3InteractionTarget>() == target) continue;
                if (hit.distance + .06f < delta.magnitude) return false;
            }
            return true;
        }
    }
}
