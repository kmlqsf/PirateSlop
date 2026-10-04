using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct ShipFirePatch
    {
        public int Id, SectionId, Fragment;
        public Vector3 Position, Normal;
    }

    public sealed partial class NetworkShip
    {
        sealed class FireExposure
        {
            public ShipFireTarget Target;
            public float Start, Burn, End;
            public GameObject Attacker;
            public ulong Impact;
        }

        readonly SyncList<ShipFirePatch> firePatches = new();
        readonly SyncVar<bool> frozen = new();
        readonly Dictionary<int, FireExposure> fireExposure = new();
        readonly Dictionary<int, ShipFireVfx> fireVisuals = new();
        readonly HashSet<CombatHealth> burnedPlayers = new();
        readonly List<int> expiredVisuals = new();
        readonly List<int> pendingFires = new();
        readonly Dictionary<long, float> wetFragments = new();
        CannonAmmoVfx iceVisual;
        ShipDestruction fireDestruction;
        int nextFireId;
        float nextFireContact;
        const float FireDamagePerSecond = 15f;

        public void Ignite(Vector3 point, GameObject attacker = null, float radius = 0f, Collider surface = null, Vector3 normal = default)
        {
            if (!IsServerInitialized || IsSinking) return;
            fireDestruction ??= GetComponent<ShipDestruction>();
            if (fireDestruction == null) return;
            if (normal.sqrMagnitude < .01f) normal = transform.up;
            var targets = fireDestruction.PlanFire(surface, point, normal);
            float duration = Random.Range(7f, 9f);
            ulong impact = fireDestruction.BeginFireImpact();
            int initial = Mathf.Max(1, Mathf.CeilToInt(targets.Count / 3f));
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                long key = ((long)target.SectionId << 6) | (uint)target.Fragment;
                if (wetFragments.TryGetValue(key, out float wetUntil) && Time.time < wetUntil) continue;
                bool present = false;
                foreach (var exposure in fireExposure.Values)
                    if (exposure.Target.SectionId == target.SectionId && exposure.Target.Fragment == target.Fragment) { present = true; break; }
                if (present) continue;
                if (fireExposure.Count >= 128) break;
                float spread = i < initial ? 0f : Mathf.Lerp(.65f, 2f, (i - initial) / (float)Mathf.Max(1, targets.Count - initial - 1));
                fireExposure[++nextFireId] = new FireExposure { Target = target, Attacker = attacker, Impact = impact,
                    Start = Time.time + spread, Burn = Time.time + duration, End = Time.time + duration };
            }
            FireImpactObserversRpc(point, normal);
        }

        [FishNet.Object.ObserversRpc(RunLocally = true)]
        void FireImpactObserversRpc(Vector3 point, Vector3 normal)
        {
            if (IsClientInitialized && !Application.isBatchMode) ShipFireVfx.Impact(point, normal);
        }

        public bool ExtinguishFire(Vector3 point, float radius = 2.5f)
        {
            if (!IsServerInitialized) return false;
            pendingFires.Clear();
            foreach (var pair in fireExposure)
            {
                var target = pair.Value.Target;
                var at = fireDestruction.FireTargetPoint(target.SectionId, target.Fragment, transform.TransformPoint(target.Position));
                if ((at - point).sqrMagnitude > radius * radius) continue;
                wetFragments[((long)target.SectionId << 6) | (uint)target.Fragment] = Time.time + 3f;
                pendingFires.Add(pair.Key);
            }
            bool changed = pendingFires.Count > 0;
            foreach (int id in pendingFires) RemoveFire(id);
            foreach (var player in CombatHealth.Active)
                if (player != null && (player.transform.position - point).sqrMagnitude <= radius * radius)
                    player.GetComponent<NetworkHealth>()?.Extinguish();
            return changed;
        }

        public void FreezeFromShot()
        {
            if (!IsServerInitialized) return;
            Motor.Freeze(5f);
            frozen.Value = true;
        }

        void RemoveFire(int id)
        {
            fireExposure.Remove(id);
            for (int i = firePatches.Count - 1; i >= 0; i--)
                if (firePatches[i].Id == id) { firePatches.RemoveAt(i); break; }
        }

        void UpdateAmmo()
        {
            if (!IsSpawned) return;
            fireDestruction ??= GetComponent<ShipDestruction>();
            if (IsServerInitialized && fireDestruction != null)
            {
                frozen.Value = Motor.IsFrozen;
                pendingFires.Clear();
                foreach (var pair in fireExposure)
                {
                    var fire = pair.Value;
                    var target = fire.Target;
                    var point = fireDestruction.FireTargetPoint(target.SectionId, target.Fragment, transform.TransformPoint(target.Position));
                    bool submerged = OceanSurface.Instance != null && point.y < OceanSurface.Instance.Height(point) - .05f;
                    if (!fireDestruction.FireTargetAlive(target.SectionId, target.Fragment) || submerged)
                    { pendingFires.Add(pair.Key); continue; }
                    if (Time.time < fire.Start) continue;
                    bool present = false;
                    foreach (var patch in firePatches) if (patch.Id == pair.Key) { present = true; break; }
                    if (!present) firePatches.Add(new ShipFirePatch { Id = pair.Key, SectionId = target.SectionId, Fragment = target.Fragment, Position = target.Position, Normal = target.Normal });
                    if (Time.time >= fire.Burn)
                    {
                        fireDestruction.BurnFragment(target, fire.Attacker, fire.Impact);
                        fire.Burn = float.PositiveInfinity;
                    }
                    if (Time.time >= fire.End) pendingFires.Add(pair.Key);
                }
                foreach (int id in pendingFires) RemoveFire(id);
                if (Time.time >= nextFireContact)
                {
                    nextFireContact = Time.time + .25f;
                    burnedPlayers.Clear();
                    foreach (var patch in firePatches)
                    {
                        if (!fireExposure.TryGetValue(patch.Id, out var exposure)) continue;
                        var point = fireDestruction.FireTargetPoint(patch.SectionId, patch.Fragment, transform.TransformPoint(patch.Position));
                        foreach (var collider in Physics.OverlapCapsule(point + Vector3.up * .15f, point + Vector3.up * 1.25f, .65f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            var health = collider.GetComponentInParent<CombatHealth>();
                            if (health == null || health.IsDead || !burnedPlayers.Add(health)) continue;
                            var network = health.GetComponent<NetworkHealth>();
                            if (network != null) network.Ignite(exposure.Attacker);
                            else health.Damage(FireDamagePerSecond * .25f, exposure.Attacker);
                        }
                    }
                }
            }
            if (!IsClientInitialized || Application.isBatchMode) return;
            if (frozen.Value && iceVisual == null) iceVisual = CannonAmmoVfx.Create(transform, Vector3.up * 3f, true);
            if (!frozen.Value && iceVisual != null) { Destroy(iceVisual.gameObject); iceVisual = null; }
            foreach (var patch in firePatches)
            {
                if (!fireVisuals.TryGetValue(patch.Id, out var visual) || visual == null)
                    fireVisuals[patch.Id] = visual = ShipFireVfx.Create(transform, patch.Position, patch.Normal);
                if (visual != null && fireDestruction != null)
                    visual.transform.position = fireDestruction.FireTargetPoint(patch.SectionId, patch.Fragment, transform.TransformPoint(patch.Position));
            }
            expiredVisuals.Clear();
            foreach (var pair in fireVisuals)
            {
                bool present = false;
                foreach (var patch in firePatches) if (patch.Id == pair.Key) { present = true; break; }
                if (!present) expiredVisuals.Add(pair.Key);
            }
            foreach (int id in expiredVisuals)
            {
                if (fireVisuals[id] != null) fireVisuals[id].Finish();
                fireVisuals.Remove(id);
            }
        }

        void ClearAmmo()
        {
            foreach (var visual in fireVisuals.Values) if (visual != null) Destroy(visual.gameObject);
            fireVisuals.Clear(); fireExposure.Clear(); wetFragments.Clear();
            if (IsServerInitialized) firePatches.Clear();
            if (iceVisual != null) Destroy(iceVisual.gameObject);
            iceVisual = null;
        }
    }
}
