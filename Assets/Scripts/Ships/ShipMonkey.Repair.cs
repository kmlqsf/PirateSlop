using System.Collections.Generic;
using System.Linq;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipMonkey
    {
        public GameObject MalletModel;
        public const float RepairSpeedFactor = .15f;
        ShipDestruction repairOwner;
        ShipDamageSection repairSection;
        int repairFragment = -1, repairStrikes;
        float nextRepairScan, repairWork;
        GameObject repairMallet;
        readonly Dictionary<ulong, float> blockedRepairs = new();
        readonly Queue<int> repairQueue = new();
        bool[] repairReachable;

        ulong RepairKey(ShipDamageSection section, int fragment) => ((ulong)(uint)section.SectionId << 7) | (uint)(fragment + 1);

        void BeginRepair()
        {
            repairOwner = GetComponent<ShipDestruction>();
            repairSection = null; repairStrikes = 0; repairWork = 0f;
            nextRepairScan = Time.time + 3f; blockedRepairs.Clear();
            repairReachable = new bool[Nodes.Length];
        }

        void ResetRepair()
        {
            if (repairSection != null && (task == TaskKind.GoRepair || task == TaskKind.Repair))
                blockedRepairs[RepairKey(repairSection, repairFragment)] = Time.time + 30f;
            repairSection = null; repairStrikes = 0; repairWork = 0f; nextRepairScan = Time.time + 2f;
        }

        bool RepairPoint(ShipDamageSection section, int fragment, Vector3 origin, out Vector3 point)
        {
            point = default;
            if (section == null || section.Owner != repairOwner) return false;
            if (fragment < 0)
            {
                if (!repairOwner.MastRepairPoint(section.SectionId, out var center)) return false;
                point = new Bounds(center, Vector3.one * 1.4f).ClosestPoint(origin);
                return true;
            }
            if (fragment >= section.RepairCount || fragment >= 64 || (section.RemovedFragments & (1UL << fragment)) == 0) return false;
            var anchor = section.RepairTransform(fragment);
            if (anchor == null) return false;
            point = anchor.TransformPoint(section.RepairBounds(fragment).ClosestPoint(anchor.InverseTransformPoint(origin)));
            return true;
        }

        bool RepairReach(Vector3 origin, Vector3 point)
        {
            Vector3 delta = point - origin;
            if (delta.sqrMagnitude > 3.5f * 3.5f) return false;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, Mathf.Max(0f, delta.magnitude - .08f), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(Visual)) return false;
            return true;
        }

        bool TryStartRepair()
        {
            if (Time.time < nextRepairScan || repairOwner == null || !repairOwner.IsServerInitialized || MalletModel == null || !Nodes[current].Available) return false;
            nextRepairScan = Time.time + 5f;
            System.Array.Clear(repairReachable, 0, repairReachable.Length); repairQueue.Clear();
            repairReachable[current] = true; repairQueue.Enqueue(current);
            bool deckOnly = Surface == ShipMonkeySurface.Deck;
            while (repairQueue.Count > 0)
            {
                int from = repairQueue.Dequeue();
                foreach (int edge in adjacent[from])
                {
                    var link = Links[edge]; int next = link.A == from ? link.B : link.A;
                    if (repairReachable[next] || !Nodes[next].Available || deckOnly && Nodes[next].Surface != ShipMonkeySurface.Deck) continue;
                    repairReachable[next] = true; repairQueue.Enqueue(next);
                }
            }
            var nodes = Enumerable.Range(0, Nodes.Length).Where(i => repairReachable[i] && Nodes[i].Surface == ShipMonkeySurface.Deck)
                .OrderBy(i => (Point(i) - position).sqrMagnitude).ToArray();
            var candidates = new List<(ShipDamageSection Section, int Fragment, float Distance)>();
            var mastGroups = new HashSet<string>();
            Vector3 actor = transform.TransformPoint(position);
            foreach (var section in repairOwner.Sections)
            {
                if (section == null || section.RemovedFragments == 0) continue;
                var definition = repairOwner.Definition(section.SectionId);
                if (definition.Type == ShipSectionType.Mast && repairOwner.MastRepairPoint(section.SectionId, out var center))
                {
                    if (mastGroups.Add(repairOwner.MastGroupKey(section.SectionId)))
                        candidates.Add((section, -1, (center - actor).sqrMagnitude));
                    continue;
                }
                for (int fragment = 0; fragment < Mathf.Min(64, section.RepairCount); fragment++)
                {
                    if ((section.RemovedFragments & (1UL << fragment)) == 0 || !RepairPoint(section, fragment, actor, out var point)) continue;
                    candidates.Add((section, fragment, (point - actor).sqrMagnitude));
                }
            }
            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            float best = float.PositiveInfinity; int goal = -1, chosenFragment = -1; ShipDamageSection chosen = null;
            foreach (var candidate in candidates)
            {
                if (blockedRepairs.TryGetValue(RepairKey(candidate.Section, candidate.Fragment), out var until) && Time.time < until) continue;
                foreach (int node in nodes)
                {
                    float distance = (Point(node) - position).sqrMagnitude;
                    if (distance * .1f >= best) break;
                    Vector3 origin = transform.TransformPoint(Point(node)) + transform.up * .55f;
                    if (!RepairPoint(candidate.Section, candidate.Fragment, origin, out var point) || !RepairReach(origin, point)) continue;
                    float reach = Mathf.Max(0f, (point - origin).magnitude - .75f);
                    float score = distance * .1f + reach * reach * 4f;
                    if (score >= best) continue;
                    best = score; chosen = candidate.Section; chosenFragment = candidate.Fragment; goal = node;
                }
            }
            if (chosen == null) return false;
            repairSection = chosen; repairFragment = chosenFragment; repairStrikes = 0; repairWork = 0f;
            SetTask(TaskKind.GoRepair, goal); return true;
        }

        bool UpdateRepair(float delta)
        {
            Vector3 origin = transform.TransformPoint(position) + transform.up * .55f;
            if (!RepairPoint(repairSection, repairFragment, origin, out var point))
            { repairSection = null; CancelActivities(); return false; }
            if (target >= 0) return false;
            if (task == TaskKind.GoRepair && current != activityGoal) return false;
            if (!RepairReach(origin, point)) { CancelActivities(); return false; }
            Face(point, delta); animationSpeed = 1f;
            if (task == TaskKind.GoRepair)
            {
                task = TaskKind.Repair; activityDeadline = Time.time + 120f;
                SetMotion(ShipMonkeyMotion.Repair); shownState = -1;
            }
            repairWork += delta * RepairSpeedFactor;
            if (repairWork < NetworkHullRepair.StrikeInterval) return true;
            repairWork -= NetworkHullRepair.StrikeInterval;
            motionTime = 0f; shownState = -1;
            ActivitySound(SoundCue.BulletWood, point);
            repairStrikes++;
            int required = repairFragment < 0 && repairOwner.IsMastCollapsed(repairSection.SectionId) ? NetworkHullRepair.MastStrikes : NetworkHullRepair.FragmentStrikes;
            if (repairStrikes < required) return true;
            if (repairFragment < 0) repairOwner.RepairMast(repairSection.SectionId);
            else repairOwner.RepairNearby(repairSection.SectionId, repairFragment, point);
            repairSection = null; CancelActivities(); return true;
        }

        void PresentRepair()
        {
            if (Application.isBatchMode || Visual == null) return;
            bool active = (initialized || remoteInitialized) && Visual.gameObject.activeInHierarchy && Motion == ShipMonkeyMotion.Repair;
            if (!active)
            {
                if (repairMallet != null) repairMallet.SetActive(false);
                return;
            }
            if (MalletModel == null || RightHand == null) return;
            if (repairMallet == null)
            {
                repairMallet = Instantiate(MalletModel, Visual); repairMallet.name = "MonkeyRepairMallet";
                foreach (var shape in repairMallet.GetComponentsInChildren<Collider>()) Destroy(shape);
            }
            repairMallet.SetActive(true);
            float elapsed = initialized ? motionTime : remote.MotionTime + (Time.unscaledTime - receivedAt) * remote.AnimationSpeed;
            float swing = Mathf.Sin(Mathf.Clamp01(elapsed / .35f) * Mathf.PI);
            repairMallet.transform.SetPositionAndRotation(RightHand.position, Visual.rotation * Quaternion.Euler(-15f + swing * 75f, 0f, -15f));
        }
    }
}
