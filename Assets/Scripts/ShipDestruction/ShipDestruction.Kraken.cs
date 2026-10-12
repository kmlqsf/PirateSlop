using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using FishNet.Object;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed partial class ShipDestruction
    {
        sealed class KrakenBlock
        {
            public ShipDamageSection Section;
            public int Fragment;
            public Bounds Bounds;
            public readonly List<int> Neighbours = new();
            public long Key => ((long)Section.SectionId << 6) | (uint)Fragment;
        }
        readonly List<KrakenBlock> krakenUpperHull = new();
        readonly List<KrakenBlock> krakenHullBlocks = new();
        readonly List<KrakenBlock> krakenDeckBlocks = new();
        readonly Dictionary<int, Bounds> krakenBounds = new();
        readonly HashSet<int> krakenProtected = new();
        bool krakenGeometryReady;
        Coroutine krakenDamage, krakenPreparation;
        Bounds krakenHullBounds;
        bool krakenHullBoundsReady;
        int whirlpoolStrikeSequence;
        public Bounds KrakenHullBounds
        {
            get
            {
                if (krakenHullBoundsReady) return krakenHullBounds;
                foreach (var section in Sections)
                {
                    if (section == null || !definitions.TryGetValue(section.SectionId, out var definition) || definition.Type != ShipSectionType.Hull) continue;
                    var bounds = MastBounds(section);
                    if (!krakenHullBoundsReady) { krakenHullBounds = bounds; krakenHullBoundsReady = true; }
                    else krakenHullBounds.Encapsulate(bounds);
                }
                return krakenHullBounds;
            }
        }

        public void PrepareKrakenEncounter()
        {
            if (!ready || krakenPreparation != null || krakenGeometryReady) return;
            krakenPreparation = StartCoroutine(PrepareKrakenGeometry());
        }

        IEnumerator PrepareKrakenGeometry()
        {
            krakenGeometryReady = false;
            krakenBounds.Clear(); krakenUpperHull.Clear(); krakenHullBlocks.Clear(); krakenDeckBlocks.Clear();
            var indices = new Dictionary<long, int>();
            var warming = new List<ShipDamageSection>();
            float upperHeight = Mathf.Max(Mathf.Lerp(flooding.EmptyHeight, flooding.FullHeight, .62f), ship.Motor.InitialDraft + .65f);
            int work = 0;
            foreach (var section in Sections)
            {
                if (section == null || section.Indestructible || !definitions.TryGetValue(section.SectionId, out var definition)) continue;
                krakenBounds[section.SectionId] = MastBounds(section);
                bool upperSection = false;
                if (definition.Type == ShipSectionType.Hull && definition.CanFlood)
                    for (int i = 0; i < section.Fragments.Length; i++)
                    {
                        if ((section.ProtectedFragments & (1UL << i)) != 0) continue;
                        var filter = section.Fragments[i].GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) continue;
                        var bounds = filter.sharedMesh.bounds;
                        var matrix = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        Vector3 extent = bounds.extents;
                        Vector3 localExtent = new(
                            Mathf.Abs(matrix.m00)*extent.x+Mathf.Abs(matrix.m01)*extent.y+Mathf.Abs(matrix.m02)*extent.z,
                            Mathf.Abs(matrix.m10)*extent.x+Mathf.Abs(matrix.m11)*extent.y+Mathf.Abs(matrix.m12)*extent.z,
                            Mathf.Abs(matrix.m20)*extent.x+Mathf.Abs(matrix.m21)*extent.y+Mathf.Abs(matrix.m22)*extent.z);
                        var local = new Bounds(matrix.MultiplyPoint3x4(bounds.center), localExtent*2);
                        var block = new KrakenBlock { Section = section, Fragment = i, Bounds = local };
                        krakenHullBlocks.Add(block);
                        if (local.min.y < upperHeight || local.min.y >= flooding.FullHeight-.02f) continue;
                        indices[block.Key] = krakenUpperHull.Count;
                        krakenUpperHull.Add(block); upperSection = true;
                    }
                if (upperSection && section.LazyFragmentColliders) warming.Add(section);
                if (definition.Type != ShipSectionType.Hull && definition.Type != ShipSectionType.Helm && definition.Type != ShipSectionType.Rudder && definition.Type != ShipSectionType.Capstan && section.HullSupports.Length == 0)
                {
                    for (int i=0;i<section.Fragments.Length;i++)
                    {
                        if ((section.ProtectedFragments&(1UL<<i))!=0) continue;
                        var filter=section.Fragments[i].GetComponent<MeshFilter>();
                        if (filter==null || filter.sharedMesh==null || filter.sharedMesh.vertexCount==0) continue;
                        var bounds=filter.sharedMesh.bounds;
                        var matrix=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                        var e=bounds.extents;
                        var local=new Bounds(matrix.MultiplyPoint3x4(bounds.center),new Vector3(
                            Mathf.Abs(matrix.m00)*e.x+Mathf.Abs(matrix.m01)*e.y+Mathf.Abs(matrix.m02)*e.z,
                            Mathf.Abs(matrix.m10)*e.x+Mathf.Abs(matrix.m11)*e.y+Mathf.Abs(matrix.m12)*e.z,
                            Mathf.Abs(matrix.m20)*e.x+Mathf.Abs(matrix.m21)*e.y+Mathf.Abs(matrix.m22)*e.z)*2);
                        if (local.center.y>flooding.FullHeight+.25f) krakenDeckBlocks.Add(new KrakenBlock { Section=section,Fragment=i,Bounds=local });
                    }
                }
                if (++work % 24 == 0) yield return null;
            }
            foreach (var node in Profile.Structure)
            {
                long key = ((long)node.SectionId << 6) | (uint)node.Fragment;
                if (!indices.TryGetValue(key, out int own)) continue;
                foreach (int other in node.Neighbours)
                {
                    var neighbour = Profile.Structure[other];
                    key = ((long)neighbour.SectionId << 6) | (uint)neighbour.Fragment;
                    if (indices.TryGetValue(key, out int adjacent)) krakenUpperHull[own].Neighbours.Add(adjacent);
                }
            }
            krakenGeometryReady = true;
            foreach (var batch in GetComponentsInChildren<PirateSlop.Ships.ShipV3RenderBatch>())
            {
                batch.PrepareDamageMesh();
                yield return null;
            }
            work = 0;
            foreach (var section in warming)
                for (int i = 0; i < section.Fragments.Length; i++)
                {
                    section.PrepareFragmentCollider(i);
                    if (++work % 4 == 0) yield return null;
                }
        }

        public void KrakenStrike(Vector3 point, int wave)
        {
            if (!ready || !IsServerInitialized || ship.IsSinking || wave < 1 || krakenDamage != null) return;
            PrepareKrakenEncounter();
            if (wave >= 2) flooding.BeginKrakenCatastrophe();
            krakenDamage = StartCoroutine(ApplyKrakenStrike(transform.InverseTransformPoint(point), wave));
        }

        public void WhirlpoolStrike(Vector3 point,bool lower)
        {
            if(!ready || !IsServerInitialized || ship.IsSinking || krakenDamage!=null) return;
            PrepareKrakenEncounter();
            whirlpoolStrikeSequence++;
            krakenDamage=StartCoroutine(ApplyKrakenStrike(transform.InverseTransformPoint(point),1,true,lower));
        }

        bool KrakenBlockAvailable(int index)
        {
            var block = krakenUpperHull[index];
            return !krakenProtected.Contains(block.Section.SectionId) && block.Section.State != ShipSectionState.Destroyed
                && (block.Section.RemovedFragments & (1UL << block.Fragment)) == 0;
        }

        void ProtectKrakenControls(Vector3 point)
        {
            krakenProtected.Clear();
            foreach (var definition in definitions.Values)
                if (definition.Type == ShipSectionType.Helm)
                {
                    krakenProtected.Add(definition.SectionId);
                    foreach (int support in definition.Supports) krakenProtected.Add(support);
                }
            var livingSails = new Dictionary<string, bool>();
            foreach (var definition in definitions.Values)
                foreach (string sail in definition.SailNames)
                {
                    bool intact = sections.TryGetValue(definition.SectionId, out var section) && section.State != ShipSectionState.Destroyed;
                    livingSails[sail] = intact && (!livingSails.TryGetValue(sail, out bool existing) || existing);
                }
            var candidates = new List<ShipSectionDefinition>();
            foreach (var definition in definitions.Values)
                foreach (string sail in definition.SailNames)
                    if (livingSails[sail]) { candidates.Add(definition); break; }
            candidates.Sort((a,b) => (krakenBounds.GetValueOrDefault(b.SectionId).center-point).sqrMagnitude.CompareTo((krakenBounds.GetValueOrDefault(a.SectionId).center-point).sqrMagnitude));
            var saved = new HashSet<string>();
            foreach (var candidate in candidates)
            {
                if (saved.Count >= 2) break;
                foreach (string sail in candidate.SailNames) if (livingSails[sail]) saved.Add(sail);
                krakenProtected.Add(candidate.SectionId);
                if (mastParts.TryGetValue(candidate.SectionId, out var assembly))
                    foreach (var part in assembly.Parts) krakenProtected.Add(part.SectionId);
            }
            foreach (var definition in definitions.Values)
                foreach (string sail in definition.SailNames)
                    if (saved.Contains(sail)) { krakenProtected.Add(definition.SectionId); break; }
            foreach (var section in Sections)
            {
                if (section == null) continue;
                foreach (var renderer in section.DependentRenderers)
                    if (renderer != null && saved.Contains(renderer.gameObject.name)) { krakenProtected.Add(section.SectionId); break; }
            }
        }

        Dictionary<int, ulong> PlanKrakenStrike(Vector3 point, int wave,bool whirlpool=false,bool lower=false)
        {
            ProtectKrakenControls(point);
            var masks = new Dictionary<int, ulong>();
            if(whirlpool && lower)
            {
                KrakenBlock best=null; float distance=float.MaxValue;
                float height=Mathf.Lerp(flooding.EmptyHeight,flooding.FullHeight,.55f);
                foreach(var block in krakenHullBlocks)
                {
                    if(block.Bounds.center.y>height || block.Bounds.max.y<flooding.EmptyHeight || krakenProtected.Contains(block.Section.SectionId) || block.Section.State==ShipSectionState.Destroyed || (block.Section.RemovedFragments&(1UL<<block.Fragment))!=0) continue;
                    float score=(block.Bounds.ClosestPoint(point)-point).sqrMagnitude;
                    if(score<distance) { distance=score; best=block; }
                }
                if(best!=null) masks[best.Section.SectionId]=1UL<<best.Fragment;
            }
            else if (wave == 1)
            {
                var chosen = new HashSet<int>();
                var frontier = new HashSet<int>();
                for (int n = 0; n < (whirlpool ? 3 : 6); n++)
                {
                    int best = -1; float distance = float.MaxValue;
                    for (int i = 0; i < krakenUpperHull.Count; i++)
                    {
                        if (chosen.Contains(i) || !KrakenBlockAvailable(i)) continue;
                        var block = krakenUpperHull[i];
                        bool connected = chosen.Count == 0 || frontier.Contains(i);
                        if (!connected)
                            foreach (int selected in chosen)
                            {
                                var bounds = krakenUpperHull[selected].Bounds;
                                if ((bounds.ClosestPoint(block.Bounds.center)-block.Bounds.ClosestPoint(bounds.center)).sqrMagnitude <= .09f) { connected = true; break; }
                            }
                        if (!connected) continue;
                        float score = (block.Bounds.ClosestPoint(point)-point).sqrMagnitude;
                        if (score >= distance) continue;
                        distance = score; best = i;
                    }
                    if (best < 0) break;
                    chosen.Add(best);
                    var selectedBlock = krakenUpperHull[best];
                    masks.TryGetValue(selectedBlock.Section.SectionId, out ulong mask);
                    masks[selectedBlock.Section.SectionId] = mask | (1UL << selectedBlock.Fragment);
                    foreach (int neighbour in selectedBlock.Neighbours) frontier.Add(neighbour);
                }
            }
            else
            {
                var hull = new List<int>();
                foreach (var entry in krakenBounds)
                {
                    var definition = definitions[entry.Key];
                    if (definition.Type != ShipSectionType.Hull || !definition.CanFlood || krakenProtected.Contains(entry.Key)) continue;
                    if (entry.Value.max.y <= flooding.EmptyHeight-1f || entry.Value.min.y >= flooding.FullHeight || Mathf.Abs(entry.Value.center.z-point.z) > 14f) continue;
                    hull.Add(entry.Key);
                }
                hull.Sort((a,b) => (krakenBounds[a].ClosestPoint(point)-point).sqrMagnitude.CompareTo((krakenBounds[b].ClosestPoint(point)-point).sqrMagnitude));
                for (int i = 0; i < Mathf.Min(180,hull.Count); i++) masks[hull[i]] = sections[hull[i]].AllFragments;
            }
            if(whirlpool)
            {
                var deckRandom=new System.Random(unchecked(ObjectId*7919+whirlpoolStrikeSequence*104729));
                var deck=new List<(KrakenBlock Block,float Score)>();
                foreach(var block in krakenDeckBlocks)
                {
                    if(krakenProtected.Contains(block.Section.SectionId) || block.Section.State==ShipSectionState.Destroyed || (block.Section.RemovedFragments&(1UL<<block.Fragment))!=0) continue;
                    deck.Add((block,(block.Bounds.ClosestPoint(point)-point).sqrMagnitude*.025f+(float)deckRandom.NextDouble()*8f));
                }
                deck.Sort((a,b)=>a.Score.CompareTo(b.Score));
                int deckCount=deckRandom.Next(2,5),selected=0;
                var damagedSections=new HashSet<int>();
                foreach(var entry in deck)
                {
                    if(!damagedSections.Add(entry.Block.Section.SectionId)) continue;
                    masks.TryGetValue(entry.Block.Section.SectionId,out ulong mask);
                    masks[entry.Block.Section.SectionId]=mask|(1UL<<entry.Block.Fragment);
                    if(++selected>=deckCount) break;
                }
                return masks;
            }
            var random = new System.Random(unchecked(ObjectId*7919+wave*104729));
            var upper = new List<(int Id, float Score)>();
            float deckHeight = flooding.FullHeight+.17f;
            foreach (var entry in krakenBounds)
            {
                var section = sections[entry.Key];
                var definition = definitions[entry.Key];
                if (krakenProtected.Contains(entry.Key) || section.State == ShipSectionState.Destroyed || section.HullSupports.Length > 0) continue;
                if (definition.Type == ShipSectionType.Rudder || definition.Type == ShipSectionType.Capstan || entry.Value.min.y <= deckHeight+.08f) continue;
                float score = (entry.Value.ClosestPoint(point)-point).sqrMagnitude*.025f+(float)random.NextDouble()*8f;
                upper.Add((entry.Key,score));
            }
            upper.Sort((a,b) => a.Score.CompareTo(b.Score));
            int count = wave == 1 ? random.Next(8,13) : 32;
            for (int i = 0; i < Mathf.Min(count,upper.Count); i++) masks[upper[i].Id] = sections[upper[i].Id].AllFragments;
            return masks;
        }

        IEnumerator ApplyKrakenStrike(Vector3 localPoint, int wave,bool whirlpool=false,bool lower=false)
        {
            yield return null;
            while (!krakenGeometryReady) yield return null;
            var plan = PlanKrakenStrike(localPoint,wave,whirlpool,lower);
            ulong impact = flooding.BeginFireImpact();
            var changed = new List<ShipSectionSnapshot>(2);
            var budget = new Stopwatch();
            foreach (var damage in plan)
            {
                if (!state.TryGetValue(damage.Key, out var current) || current.State == ShipSectionState.Destroyed) continue;
                ulong mask = (current.RemovedFragments | damage.Value) & ~sections[damage.Key].ProtectedFragments;
                if (mask == current.RemovedFragments && sections[damage.Key].Fragments.Length > 0) continue;
                if (changed.Count == 0) budget.Restart();
                Change(damage.Key, 0f, ShipSectionState.Destroyed, transform.TransformPoint(localPoint), transform.up, Vector3.down*15f,
                    InventoryItem.Cannonball, gameObject, ShipDamageReason.Scripted, definitions[damage.Key].MaxHealth, mask, impact);
                changed.Add(state[damage.Key]);
                if (changed.Count < 2 && budget.Elapsed.TotalMilliseconds < 2) continue;
                PublishKrakenSections(changed); changed.Clear();
                yield return null;
            }
            if (changed.Count > 0) PublishKrakenSections(changed);
            krakenDamage = null;
        }

        void PublishKrakenSections(List<ShipSectionSnapshot> changed)
        {
            ApplyModifiers();
            ReceiveKrakenSections(changed.ToArray(), flooding.Level, revision);
        }
        [ObserversRpc]
        void ReceiveKrakenSections(ShipSectionSnapshot[] entries, float water, uint version)
        {
            if (!IsServerInitialized && snapshotReceived && version >= appliedRevision) ApplySnapshot(entries, water, version);
        }
    }
}
