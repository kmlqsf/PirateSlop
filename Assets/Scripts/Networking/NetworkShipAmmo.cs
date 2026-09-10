using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct ShipFirePatch
    {
        public int Id;
        public Vector3 Position;
        public Vector3 BlastCenter;
        public float BlastRadius;
    }

    public sealed partial class NetworkShip
    {
        readonly SyncList<ShipFirePatch> firePatches = new();
        readonly SyncVar<bool> frozen = new();
        readonly Dictionary<int, float> fireExpiry = new();
        readonly Dictionary<int, GameObject> fireAttackers = new();
        readonly Dictionary<int, CannonAmmoVfx> fireVisuals = new();
        readonly HashSet<CombatHealth> burnedPlayers = new();
        readonly List<int> expiredVisuals = new();
        CannonAmmoVfx iceVisual;
        int nextFireId;
        float nextFireDamage;
        const float FireDuration = 10f, FireDamagePerSecond = 15f;
        static readonly Vector3 FireHalfExtents = new(2f, 1.5f, 2f);

        public void Ignite(Vector3 point, GameObject attacker = null, float radius = 0f)
        {
            if (!IsServerInitialized) return;
            if (radius > 0f)
            {
                IgniteArea(point, attacker, radius);
                return;
            }
            float center = Mathf.Clamp(transform.InverseTransformPoint(point).z, -11f, 11f);
            for (int row = 0; row < 6; row++)
                for (int column = 0; column < 3; column++)
                {
                    var origin = transform.TransformPoint(new Vector3((column - 1) * 4f, 8.6f, center + (row - 2.5f) * 4f));
                    RaycastHit deck = default;
                    float distance = 5f;
                    foreach (var hit in Physics.RaycastAll(origin, -transform.up, distance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.attachedRigidbody != Body || Vector3.Dot(hit.normal, transform.up) < .7f) continue;
                        float height = transform.InverseTransformPoint(hit.point).y;
                        if (height < 3.8f || height > 7.6f || hit.distance >= distance) continue;
                        deck = hit; distance = hit.distance;
                    }
                    if (deck.collider == null) continue;
                    AddFirePatch(transform.InverseTransformPoint(deck.point + transform.up * .06f), attacker);
                }
        }

        void IgniteArea(Vector3 point, GameObject attacker, float radius)
        {
            for (float x = -radius; x <= radius; x += 2f)
                for (float z = -radius; z <= radius; z += 2f)
                {
                    if (x * x + z * z > radius * radius) continue;
                    Vector3 origin = point + new Vector3(x, radius, z);
                    RaycastHit nearest = default;
                    float distance = radius * 2f;
                    foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.attachedRigidbody != Body || hit.normal.y < .7f || hit.distance >= distance) continue;
                        nearest = hit; distance = hit.distance;
                    }
                    if (nearest.collider != null && (nearest.point - point).sqrMagnitude <= radius * radius)
                        AddFirePatch(transform.InverseTransformPoint(nearest.point + Vector3.up * .06f), attacker, transform.InverseTransformPoint(point), radius);
                }
        }

        void AddFirePatch(Vector3 position, GameObject attacker, Vector3 blastCenter = default, float blastRadius = 0f)
        {
            if (firePatches.Count >= 54)
            {
                fireAttackers.Remove(firePatches[0].Id);
                fireExpiry.Remove(firePatches[0].Id);
                firePatches.RemoveAt(0);
            }
            var patch = new ShipFirePatch { Id = ++nextFireId, Position = position, BlastCenter = blastCenter, BlastRadius = blastRadius };
            firePatches.Add(patch);
            fireExpiry[patch.Id] = Time.time + FireDuration;
            fireAttackers[patch.Id] = attacker;
        }
        public void FreezeFromShot()
        {
            if (!IsServerInitialized) return;
            Motor.Freeze(5f);
            frozen.Value = true;
        }

        void UpdateAmmo()
        {
            if (!IsSpawned) return;
            if (IsServerInitialized)
            {
                frozen.Value = Motor.IsFrozen;
                for (int i = firePatches.Count - 1; i >= 0; i--)
                    if (Time.time >= fireExpiry[firePatches[i].Id])
                    {
                        fireAttackers.Remove(firePatches[i].Id);
                        fireExpiry.Remove(firePatches[i].Id);
                        firePatches.RemoveAt(i);
                    }
                if (Time.time >= nextFireDamage)
                {
                    nextFireDamage = Time.time + .5f;
                    burnedPlayers.Clear();
                    foreach (var patch in firePatches)
                        foreach (var collider in Physics.OverlapBox(transform.TransformPoint(patch.Position + Vector3.up * 1.5f), FireHalfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (patch.BlastRadius > 0f && (collider.ClosestPoint(transform.TransformPoint(patch.BlastCenter)) - transform.TransformPoint(patch.BlastCenter)).sqrMagnitude > patch.BlastRadius * patch.BlastRadius) continue;
                            var health = collider.GetComponentInParent<CombatHealth>();
                            if (health != null && !health.IsDead && burnedPlayers.Add(health))
                                health.Damage(FireDamagePerSecond * .5f, fireAttackers[patch.Id]);
                        }
                }
            }
            if (!IsClientInitialized || Application.isBatchMode) return;
            if (frozen.Value && iceVisual == null) iceVisual = CannonAmmoVfx.Create(transform, Vector3.up * 3f, true);
            if (!frozen.Value && iceVisual != null) { Destroy(iceVisual.gameObject); iceVisual = null; }
            foreach (var patch in firePatches)
                if (!fireVisuals.ContainsKey(patch.Id))
                    fireVisuals[patch.Id] = CannonAmmoVfx.Create(transform, patch.Position, false);
            expiredVisuals.Clear();
            foreach (var pair in fireVisuals)
            {
                bool present = false;
                foreach (var patch in firePatches) if (patch.Id == pair.Key) { present = true; break; }
                if (!present) expiredVisuals.Add(pair.Key);
            }
            foreach (int id in expiredVisuals)
            {
                if (fireVisuals[id] != null) Destroy(fireVisuals[id].gameObject);
                fireVisuals.Remove(id);
            }
        }

        void ClearAmmo()
        {
            foreach (var visual in fireVisuals.Values) if (visual != null) Destroy(visual.gameObject);
            fireVisuals.Clear(); fireExpiry.Clear(); fireAttackers.Clear();
            if (iceVisual != null) Destroy(iceVisual.gameObject);
            iceVisual = null;
        }
    }
}
