using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public struct ShipSectionSnapshot
    {
        public int SectionId;
        public ShipSectionState State;
        public ushort Health;
        public bool Breach, Repair;
        public uint Revision;
        public Vector3 BreachPoint;
        public ulong RemovedFragments;
        public ulong LeakingFragments;
    }
    public struct ShipDestructionEvent
    {
        public ulong EventId;
        public uint Revision;
        public int SectionId, Seed;
        public ShipSectionState Previous, Current;
        public Vector3 LocalPoint, LocalNormal, LocalVelocity, PointVelocity, AngularVelocity;
        public float Impulse, BaseDamage;
        public InventoryItem Ammo;
        public int AttackerId;
        public ShipDamageReason Reason;
        public ulong RemovedFragments, DetachedFragments;
    }
    public sealed class ShipDestruction : NetworkBehaviour
    {
        readonly FishNet.Object.Synchronizing.SyncVar<int> openImpacts = new();
        public ShipDestructionProfile Profile;
        public ShipDamageSection[] Sections = Array.Empty<ShipDamageSection>();
        public event Action<ShipDestructionEvent> Hit;
        public event Action<ShipDamageSection> SupportRemoved;
        readonly Dictionary<int, ShipDamageSection> sections = new();
        readonly Dictionary<int, ShipSectionDefinition> definitions = new();
        readonly Dictionary<Collider, int> colliders = new();
        readonly Dictionary<int, ShipSectionSnapshot> state = new();
        readonly List<ShipDestructionEvent> pending = new();
        readonly Queue<int> branch = new();
        readonly Dictionary<int, float> nextBurn = new();
        readonly Dictionary<string, int> mastHits = new();
        readonly HashSet<string> struckMasts = new();
        ShipStructuralGraph graph;
        NetworkShip ship;
        ShipFlooding flooding;
        ShipDestructionVisuals visuals;
        uint revision, appliedRevision;
        ulong sequence, appliedEvent;
        float nextFloodPublish, nextDiagnostic, publishedWater;
        bool ready, snapshotReceived;
        public ShipFlooding Flooding => flooding;
        public uint Revision => revision;
        void Awake()
        {
            ship = GetComponent<NetworkShip>();
            flooding = GetComponent<ShipFlooding>();
            visuals = GetComponent<ShipDestructionVisuals>();
            if (Profile == null || flooding == null || ship == null) return;
            graph = new ShipStructuralGraph(Profile);
            foreach (var definition in Profile.Sections) definitions.Add(definition.SectionId, definition);
            foreach (var section in Sections)
            {
                if (section == null || !definitions.ContainsKey(section.SectionId)) continue;
                sections.Add(section.SectionId, section); section.Owner = this;
                foreach (var collider in section.DamageColliders)
                    if (collider != null && !colliders.TryAdd(collider, section.SectionId)) throw new ArgumentException("Ambiguous damage collider " + collider.name);
            }
            if (!sections.ContainsKey(Profile.FallbackSectionId)) throw new ArgumentException("Missing fallback section");
            ready = true;
        }
        public override void OnStartServer()
        {
            if (!ready) return;
            state.Clear(); revision = 0; sequence = 0;
            nextBurn.Clear(); publishedWater = 0f;
            mastHits.Clear();
            flooding.Clear();
            foreach (var entry in definitions)
            {
                state[entry.Key] = new ShipSectionSnapshot { SectionId = entry.Key, Health = ushort.MaxValue };
                if (sections.TryGetValue(entry.Key, out var section)) { section.Health = entry.Value.MaxHealth; section.Apply(ShipSectionState.Intact); }
            }
            ApplyModifiers();
        }
        public override void OnStartClient()
        {
            if (!ready) return;
            visuals?.Initialize(this);
            if (!IsServerInitialized) RequestSnapshot(); else snapshotReceived = true;
        }
        [ServerRpc(RequireOwnership = false)]
        void RequestSnapshot(NetworkConnection sender = null)
        {
            if (sender != null && ready) ReceiveSnapshot(sender, Snapshot(), flooding.Level, revision, sequence);
        }
        [TargetRpc]
        void ReceiveSnapshot(NetworkConnection connection, ShipSectionSnapshot[] entries, float water, uint version, ulong eventId)
        {
            if (IsServerInitialized || (snapshotReceived && version < appliedRevision)) return;
            ApplySnapshot(entries, water, version);
            snapshotReceived = true; appliedEvent = Math.Max(appliedEvent, eventId);
            foreach (var entry in pending) if (entry.Revision > appliedRevision) Present(entry);
            pending.Clear();
        }
        ShipSectionSnapshot[] Snapshot()
        {
            var result = new ShipSectionSnapshot[state.Count]; int i = 0;
            foreach (var item in state.Values) result[i++] = item;
            return result;
        }
        public override void OnStopNetwork()
        {
            visuals?.Clear(); flooding?.Clear(); pending.Clear(); state.Clear();
            snapshotReceived = false; appliedEvent = 0; appliedRevision = 0;
        }
        public ShipDamageSection Resolve(Collider collider, Vector3 point)
        {
            if (!ready) return null;
            if (collider != null && collider.transform.IsChildOf(transform))
            {
                if (colliders.TryGetValue(collider, out int id)) return sections[id];
                for (var current = collider.transform; current != null && current != transform; current = current.parent)
                    if (current.TryGetComponent<ShipDamageSection>(out var section) && section.Owner == this) return section;
            }
            if (Time.time >= nextDiagnostic)
            {
                Debug.LogWarning("Ship section fallback: " + (collider != null ? collider.name : "none"), this);
                nextDiagnostic = Time.time + 10f;
            }
            ShipDamageSection nearest = null;
            float distance = float.MaxValue;
            foreach (var section in sections.Values)
            {
                float candidate = section.Distance(point);
                if (candidate < distance) { distance = candidate; nearest = section; }
            }
            return nearest ?? sections[Profile.FallbackSectionId];
        }
        public void Damage(Collider collider, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker, float radius = 0f)
        {
            if (!ready || !IsServerInitialized || ship.IsSinking || !float.IsFinite(point.sqrMagnitude) || !float.IsFinite(velocity.sqrMagnitude)) return;
            var direct = Resolve(collider, point);
            flooding.BeginImpact();
            struckMasts.Clear();
            var affected = new Dictionary<int, float>();
            if (radius <= 0f)
            {
                affected[direct.SectionId] = 1f;
                var definition = definitions[direct.SectionId];
                if (!string.IsNullOrEmpty(definition.SourceGroup))
                    foreach (var section in sections.Values)
                        if (definitions[section.SectionId].SourceGroup == definition.SourceGroup && section.Distance(point) < (definition.Type == ShipSectionType.Mast ? 1.2f : .25f)) affected[section.SectionId] = 1f;
            }
            else
            {
                foreach (var section in sections.Values)
                {
                    float distance = section.Distance(point);
                    if (distance < radius) affected[section.SectionId] = 1f - distance / radius;
                }
                if (affected.Count == 0) affected[direct.SectionId] = 1f;
            }
            foreach (var target in affected)
                ApplyDamage(target.Key, Profile.CannonDamage * target.Value, point, normal, velocity, ammo, attacker, ShipDamageReason.Hit);
            if (definitions[direct.SectionId].Type == ShipSectionType.Hull)
            {
                ShipDamageSection deck = null;
                Vector3 deckPoint = point;
                float nearest = Profile.HullDeckDamageRadius;
                foreach (var candidate in sections.Values)
                {
                    if ((!candidate.SurfaceDamage && definitions[candidate.SectionId].Type != ShipSectionType.Deck) || affected.ContainsKey(candidate.SectionId) || candidate.Distance(point) >= nearest) continue;
                    var visual = candidate.Intact.transform;
                    var bounds = candidate.Intact.GetComponent<MeshFilter>().sharedMesh.bounds;
                    var local = visual.InverseTransformPoint(point);
                    var top = new Vector3(Mathf.Clamp(local.x, bounds.min.x, bounds.max.x), bounds.max.y, Mathf.Clamp(local.z, bounds.min.z, bounds.max.z));
                    var world = visual.TransformPoint(top);
                    float distance = Vector3.Distance(point, world);
                    if (distance >= nearest) continue;
                    nearest = distance;deck = candidate;deckPoint = world;
                }
                if (deck != null) ApplyDamage(deck.SectionId, Profile.CannonDamage * Profile.HullDeckDamageFraction, deckPoint, transform.up, velocity, ammo, attacker, ShipDamageReason.Hit);
            }
            DetachUnsupported(point, normal, velocity, ammo, attacker);
            Publish();
        }
        public void Burn(Vector3 point, float seconds, GameObject attacker)
        {
            if (!ready || !IsServerInitialized || ship.IsSinking) return;
            ShipDamageSection nearest = null; float distance = 3f;
            foreach (var section in sections.Values)
            {
                float candidate = section.Distance(point);
                if (candidate < distance) { distance = candidate; nearest = section; }
            }
            if (nearest == null) return;
            if (nextBurn.TryGetValue(nearest.SectionId, out float next) && Time.time < next) return;
            nextBurn[nearest.SectionId] = Time.time + seconds * .9f;
            ApplyDamage(nearest.SectionId, Profile.FireDamagePerSecond * seconds, point, transform.up, Vector3.zero, InventoryItem.FireCannonball, attacker, ShipDamageReason.Fire);
            DetachUnsupported(point, transform.up, Vector3.zero, InventoryItem.FireCannonball, attacker);
            Publish();
        }
        void ApplyDamage(int id, float amount, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker, ShipDamageReason reason)
        {
            if (attacker != null)
                PirateSlop.Networking.SessionController.Instance?.NotifyCombatDamage(ship, attacker);
            if (!state.TryGetValue(id, out var current)) return;
            if (current.State == ShipSectionState.Destroyed && (!sections.TryGetValue(id, out var fracturedPart) || fracturedPart.Fragments.Length == 0 && !fracturedPart.SurfaceDamage))
            {
                int redirect = definitions[id].RedirectSectionId;
                if (redirect == 0 || redirect == id || !state.TryGetValue(redirect, out current) || current.State == ShipSectionState.Destroyed) return;
                id = redirect;
            }
            var definition = definitions[id];
            amount *= definition.DamageMultiplier(ammo);
            if (amount <= 0f) return;
            if (definition.Type == ShipSectionType.Mast && reason == ShipDamageReason.Hit)
            {
                string key = string.IsNullOrEmpty(definition.SourceGroup) ? definition.Name : definition.SourceGroup;
                mastHits.TryGetValue(key, out int hits);
                if (struckMasts.Add(key)) mastHits[key] = ++hits;
                if (mastHits[key] < 2) return;
            }
            float health = sections.TryGetValue(id, out var part) ? part.Health : current.Health / (float)ushort.MaxValue * definition.MaxHealth;
            health = Mathf.Max(0f, health - amount);
            float fraction = health / definition.MaxHealth;
            var next = health <= 0f ? ShipSectionState.Destroyed : fraction <= definition.CriticalThreshold ? ShipSectionState.Critical : fraction <= definition.DamagedThreshold ? ShipSectionState.Damaged : ShipSectionState.Intact;
            Change(id, health, next, point, normal, velocity, ammo, attacker, reason, amount);
            if (Profile.Structure.Length > 0) return;
            branch.Clear(); branch.Enqueue(id);
            var visited = new HashSet<int>();
            while (branch.Count > 0)
                foreach (int child in graph.Dependents(branch.Dequeue()))
                    if (!visited.Contains(child) && state[child].State != ShipSectionState.Destroyed && !graph.Supported(child, parent => state[parent].State != ShipSectionState.Destroyed))
                    {
                        visited.Add(child);
                        Change(child, 0f, ShipSectionState.Destroyed, point, normal, velocity, ammo, attacker, ShipDamageReason.SupportLost, 0f);
                        branch.Enqueue(child);
                    }
        }
        void DetachUnsupported(Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker)
        {
            foreach (var entry in graph.Unsupported(id => state[id].RemovedFragments))
                Change(entry.Key, 0f, ShipSectionState.Destroyed, point, normal, velocity, ammo, attacker, ShipDamageReason.SupportLost, 0f, state[entry.Key].RemovedFragments | entry.Value);
        }
        void Change(int id, float health, ShipSectionState next, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker, ShipDamageReason reason, float damage, ulong? fragmentMask = null)
        {
            var current = state[id]; var previous = current.State; var definition = definitions[id];
            ulong previousFragments = current.RemovedFragments;
            if (sections.TryGetValue(id, out var fragmentSection))
            {
                current.RemovedFragments = fragmentMask ?? fragmentSection.BreakNear(point, damage, reason == ShipDamageReason.SupportLost);
                if (fragmentSection.Fragments.Length > 0)
                {
                    int remaining = 0;
                    for (int i = 0; i < fragmentSection.Fragments.Length; i++) if ((current.RemovedFragments & (1UL << i)) == 0) remaining++;
                    float fraction = remaining / (float)fragmentSection.Fragments.Length;
                    health = definition.MaxHealth * fraction;
                    next = remaining == 0 ? ShipSectionState.Destroyed : fraction <= definition.CriticalThreshold ? ShipSectionState.Critical : ShipSectionState.Damaged;
                }
            }
            current.State = next; current.Health = (ushort)Mathf.RoundToInt(health / definition.MaxHealth * ushort.MaxValue);
            current.Revision = ++revision;
            if (Profile.EnableFlooding && definition.CanFlood && next != ShipSectionState.Intact && reason == ShipDamageReason.Hit && flooding.CanOpenBreach(point))
            {
                if (!current.Breach) current.BreachPoint = transform.InverseTransformPoint(point);
                current.Breach = true;
                current.LeakingFragments |= current.RemovedFragments & ~previousFragments;
                flooding.RegisterImpact(id, fragmentSection != null && fragmentSection.Fragments.Length > 0 ? current.RemovedFragments & ~previousFragments : 1UL);
            }
            state[id] = current;
            if (sections.TryGetValue(id, out var section))
            {
                section.Health = health; section.Apply(next, current.RemovedFragments);
                if (next == ShipSectionState.Destroyed && previous != next) SupportRemoved?.Invoke(section);
            }
            SetBreach(current);
            var player = attacker != null ? attacker.GetComponent<NetworkPlayer>() : null;
            var impact = new ShipDestructionEvent
            {
                EventId = ++sequence, Revision = revision, SectionId = id, Previous = previous, Current = next,
                LocalPoint = transform.InverseTransformPoint(point), LocalNormal = transform.InverseTransformDirection(normal),
                LocalVelocity = transform.InverseTransformDirection(velocity), PointVelocity = ship.Motor.CannonPointVelocity(section != null ? section.transform.position : point),
                Impulse = Mathf.Clamp(velocity.magnitude / 40f, 0f, 2f), Seed = unchecked((int)(sequence * 2654435761UL) ^ ObjectId),
                AngularVelocity = ship.Motor.MotionAngularVelocity,
                RemovedFragments = current.RemovedFragments, DetachedFragments = current.RemovedFragments & ~previousFragments,
                Ammo = ammo, AttackerId = player != null ? player.ParticipantId.Value : 0, Reason = reason, BaseDamage = damage
            };
            if (IsClientInitialized) Present(impact);
            ReceiveEvent(impact);
        }
        [ObserversRpc]
        void ReceiveEvent(ShipDestructionEvent impact)
        {
            if (IsServerInitialized) return;
            if (!snapshotReceived) { if (pending.Count < 256) pending.Add(impact); return; }
            if (impact.Revision > appliedRevision) Present(impact);
        }
        void Present(ShipDestructionEvent impact)
        {
            if (impact.EventId <= appliedEvent) return;
            appliedEvent = impact.EventId;
            if (sections.TryGetValue(impact.SectionId, out var section)) section.Apply(impact.Current, impact.RemovedFragments);
            visuals?.Present(impact);
            Hit?.Invoke(impact);
            if (impact.BaseDamage > 0f)
            {
                foreach (var p in PirateSlop.Networking.NetworkPlayer.Active)
                {
                    if (p.IsOwner)
                    {
                        if (p.Ship != null && p.Ship.gameObject == gameObject) GameAudio.LastDamageTime = Time.time;
                        if (impact.AttackerId != 0 && p.ParticipantId.Value == impact.AttackerId) GameAudio.LastDamageTime = Time.time;
                    }
                }
            }
        }
        void SetBreach(ShipSectionSnapshot entry)
        {
            var definition = definitions[entry.SectionId];
            if (sections.TryGetValue(entry.SectionId, out var section))
                flooding.SetSectionBreaches(section, entry, definition, Profile.EnableFlooding && definition.CanFlood);
        }
        public bool RepairFragment(int id, int fragment)
        {
            if (!ready || !IsServerInitialized || ship.IsSinking || !state.TryGetValue(id, out var entry) || !sections.TryGetValue(id, out var section)) return false;
            if (fragment < 0 || fragment >= section.RepairCount || fragment >= 64) return false;
            ulong bit = 1UL << fragment;
            if ((entry.RemovedFragments & bit) == 0) return false;
            entry.RemovedFragments &= ~bit;
            entry.LeakingFragments &= ~bit;
            entry.Breach = entry.LeakingFragments != 0;
            int remaining = 0;
            for (int i = 0; i < section.RepairCount; i++) if ((entry.RemovedFragments & (1UL << i)) == 0) remaining++;
            float fraction = remaining / (float)section.RepairCount;
            var definition = definitions[id];
            entry.State = entry.RemovedFragments == 0 ? ShipSectionState.Intact : fraction <= definition.CriticalThreshold ? ShipSectionState.Critical : ShipSectionState.Damaged;
            entry.Health = (ushort)Mathf.RoundToInt(fraction * ushort.MaxValue);
            entry.Repair = true;
            entry.Revision = ++revision;
            state[id] = entry;
            section.Health = fraction * definition.MaxHealth;
            section.Apply(entry.State, entry.RemovedFragments);
            SetBreach(entry);
            Publish();
            return true;
        }
        public void RepairNearby(int id, int fragment, Vector3 point)
        {
            if (!RepairFragment(id, fragment)) return;
            var candidates = new List<(int Id, int Fragment, float Distance)>();
            foreach (var section in Sections)
            {
                for (int i = 0; i < section.RepairCount; i++)
                {
                    if ((section.RemovedFragments & (1UL << i)) == 0) continue;
                    var anchor = section.RepairTransform(i);
                    var closest = anchor.TransformPoint(section.RepairBounds(i).ClosestPoint(anchor.InverseTransformPoint(point)));
                    float distance = Vector3.Distance(point, closest);
                    if (distance <= 1.5f) candidates.Add((section.SectionId, i, distance));
                }
            }
            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int i = 0; i < Mathf.Min(2, candidates.Count); i++) RepairFragment(candidates[i].Id, candidates[i].Fragment);
            var repaired = definitions[id];
            if (repaired.Type == ShipSectionType.Mast)
            {
                string key = string.IsNullOrEmpty(repaired.SourceGroup) ? repaired.Name : repaired.SourceGroup;
                bool complete = true;
                foreach (var section in Sections) if (definitions[section.SectionId].SourceGroup == repaired.SourceGroup && section.RemovedFragments != 0) complete = false;
                if (complete) mastHits.Remove(key);
            }
        }
        public bool MastRepairPoint(int id, out Vector3 point)
        {
            point = Vector3.zero;
            if (!definitions.TryGetValue(id, out var definition) || definition.Type != ShipSectionType.Mast) return false;
            var source = transform.Find("MainShipVisual/" + definition.SourceGroup);
            if (source == null) return false;
            bool damaged = false;
            foreach (var section in Sections) if (definitions[section.SectionId].SourceGroup == definition.SourceGroup && section.RemovedFragments != 0) damaged = true;
            if (!damaged) return false;
            var bounds = source.GetComponent<MeshFilter>().sharedMesh.bounds;
            point = source.TransformPoint(bounds.center);
            var local = transform.InverseTransformPoint(point);
            local.x = 0f;
            local.y = definition.SourceGroup.Contains("Back") ? 7.6f : 4.9f;
            point = transform.TransformPoint(local);
            return true;
        }
        public bool RepairMast(int id)
        {
            if (!ready || !IsServerInitialized || ship.IsSinking || !MastRepairPoint(id, out _)) return false;
            string group = definitions[id].SourceGroup;
            string suffix = group.Replace("F2_Mast", "");
            foreach (var section in Sections)
            {
                var definition = definitions[section.SectionId];
                if (definition.SourceGroup != group && !definition.SourceGroup.StartsWith("F2_Sail" + suffix)) continue;
                var entry = state[section.SectionId];
                entry.RemovedFragments = entry.LeakingFragments = 0;
                entry.State = ShipSectionState.Intact; entry.Health = ushort.MaxValue;
                entry.Breach = false; entry.Repair = true; entry.Revision = ++revision;
                state[section.SectionId] = entry;
                section.Health = definition.MaxHealth; section.Apply(ShipSectionState.Intact);
                SetBreach(entry);
            }
            mastHits.Remove(group);
            Publish();
            return true;
        }
        void Publish()
        {
            ApplyModifiers();
            ReceiveState(Snapshot(), flooding.Level, revision);
        }
        [ObserversRpc(BufferLast = true)]
        void ReceiveState(ShipSectionSnapshot[] entries, float water, uint version)
        {
            if (!IsServerInitialized && snapshotReceived && version >= appliedRevision) ApplySnapshot(entries, water, version);
        }
        void ApplySnapshot(ShipSectionSnapshot[] entries, float water, uint version)
        {
            appliedRevision = version;
            foreach (var entry in entries)
            {
                if (!definitions.ContainsKey(entry.SectionId)) continue;
                state[entry.SectionId] = entry;
                if (sections.TryGetValue(entry.SectionId, out var section))
                {
                    section.Health = entry.Health / (float)ushort.MaxValue * definitions[entry.SectionId].MaxHealth;
                    section.Apply(entry.State, entry.RemovedFragments);
                }
                SetBreach(entry);
            }
            flooding.SetLevel(water); ApplyModifiers();
        }
        void Update()
        {
            if (flooding != null)
            {
                if (IsServerInitialized) openImpacts.Value = flooding.OpenImpactCount;
                flooding.ReportedOpenImpacts = openImpacts.Value;
            }
            if (!ready || !IsServerInitialized || ship.IsSinking || !Profile.EnableFlooding) return;
            flooding.Simulate(Time.deltaTime, Profile);
            ApplyFloodModifiers();
            if (Time.time >= nextFloodPublish)
            {
                nextFloodPublish = Time.time + .25f;
                if (Mathf.Abs(publishedWater - flooding.Level) >= .001f || flooding.Level >= 1f && publishedWater < 1f)
                {
                    publishedWater = flooding.Level; ApplyModifiers(); ReceiveFlood(publishedWater, ++revision);
                }
            }
        }
        [ObserversRpc(BufferLast = true)]
        void ReceiveFlood(float water, uint version)
        {
            if (IsServerInitialized || !snapshotReceived || version < appliedRevision) return;
            appliedRevision = version; flooding.SetLevel(water); ApplyModifiers();
        }
        void ApplyModifiers()
        {
            float rudder = 1f;
            bool helmAvailable = true;
            bool capstanAvailable = true;
            var sailEfficiency = new Dictionary<string, float>();
            foreach (var entry in state)
            {
                var definition = definitions[entry.Key]; float efficiency = definition.Efficiency(entry.Value.State);
                if (definition.Type == ShipSectionType.Rudder) rudder = Mathf.Min(rudder, efficiency);
                if (definition.Type == ShipSectionType.Helm && entry.Value.State == ShipSectionState.Destroyed) helmAvailable = false;
                if (definition.Type == ShipSectionType.Capstan && entry.Value.State == ShipSectionState.Destroyed) capstanAvailable = false;
                foreach (string sail in definition.SailNames)
                    sailEfficiency[sail] = sailEfficiency.TryGetValue(sail, out float existing) ? Mathf.Min(existing, efficiency) : efficiency;
            }
            var sails = GetComponent<SailSystem>();
            if (ship.Helm != null)
            {
                ship.Helm.StructurallyAvailable = helmAvailable;
                if (!helmAvailable) ship.Helm.ReleaseControl();
            }
            if (ship.Capstan != null)
            {
                ship.Capstan.StructurallyAvailable = capstanAvailable;
                if (!capstanAvailable) ship.Capstan.CancelPush();
            }
            if (sails != null) sails.SetStructuralEfficiency(sailEfficiency);
            ship.Motor.SetDestructionModifiers(1f, 1f, rudder, 0f);
            ApplyFloodModifiers();
        }
        void ApplyFloodModifiers()
        {
            ship.Motor.SetFlooding(Profile.EnableFlooding ? flooding.Level : 0f, flooding.FullWaterline, flooding.FullBowPitch);
        }
        public ShipSectionDefinition Definition(int id) => definitions[id];
        public ShipDamageSection Section(int id) => sections.TryGetValue(id, out var section) ? section : null;
    }
}
