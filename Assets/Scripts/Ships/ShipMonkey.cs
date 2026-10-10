using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Ships
{
    public enum ShipMonkeySurface : byte { Deck, Rail, Rigging, Nest, Prop }
    public enum ShipMonkeyMotion : byte { Idle, Walk, RailWalk, ClimbUp, ClimbDown, Hop, Run, SitDown, Sit, StandUp, JumpStart, JumpAir, JumpLand, Pickup, CarryIdle, CarryWalk, FishingCast, FishingWait, FishingReel, Work, Repair, BatStrike }

    [Serializable]
    public sealed class ShipMonkeyNode
    {
        public Vector3 Position;
        public ShipMonkeySurface Surface;
        public Transform Support;
        public Transform SupportParent;
        public Vector3 SupportLocalPosition;
        public Vector3 SupportLocalPoint;
        public Transform SupportShip;
        public ShipLadder Ladder;
        public bool RestSpot;
        public Vector3 SeaFacing;
        public bool Available => Support != null && Support.gameObject.activeInHierarchy && Support.parent == SupportParent && (Surface == ShipMonkeySurface.Prop ? SupportShip != null && Support.IsChildOf(SupportShip) : (Support.localPosition - SupportLocalPosition).sqrMagnitude < .0625f) && (Ladder == null || Ladder.enabled && Ladder.gameObject.activeInHierarchy);
    }

    [Serializable]
    public struct ShipMonkeyLink
    {
        public int A, B;
        public ShipMonkeyMotion Motion;
        public Vector3 Facing;
    }

    public struct ShipMonkeyPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public ShipMonkeyMotion Motion;
        public float AnimationSpeed;
        public float MotionTime;
        public bool Perched;
        public FishNet.Object.NetworkObject HeadRider;
        public bool Looking;
        public Vector3 LookPoint;
        public bool Carrying;
        public bool HoldingBat;
        public Vector3 BatTarget;
        public byte FishingPhase;
        public float FishingTime;
        public Vector3 FishingPoint;
        public uint Sequence;
    }

    [DisallowMultipleComponent]
    public sealed partial class ShipMonkey : MonoBehaviour
    {
        public Transform Visual;
        public Animator Animator;
        public ShipMonkeyNode[] Nodes = Array.Empty<ShipMonkeyNode>();
        public ShipMonkeyLink[] Links = Array.Empty<ShipMonkeyLink>();
        public int StartNode;
        [Min(.05f)] public float WalkSpeed = .45f;
        [Min(.05f)] public float RunSpeed = 1.10f;
        [Range(0f, 1f)] public float RunChance = .28f;
        [Range(0f, 1f)] public float SitChance = .6f;
        [Min(1f)] public float SitMin = 5f;
        [Min(1f)] public float SitMax = 13f;
        [Min(.05f)] public float RailSpeed = .20f;
        [Min(.05f)] public float ClimbSpeed = .40f;
        [Min(0f)] public float PauseMin = .5f;
        [Min(0f)] public float PauseMax = 1.8f;
        [Min(.05f)] public float HopHeight = .40f;
        public ShipMonkeyMotion Motion { get; private set; }
        public ShipMonkeySurface Surface => current >= 0 && current < Nodes.Length ? Nodes[current].Surface : ShipMonkeySurface.Deck;
        public Vector3 LocalPosition => position;
        public float SnapshotInterval => Motion >= ShipMonkeyMotion.JumpStart ? .05f : .12f;
        readonly List<int> path = new();
        List<int>[] adjacent;
        int current, target = -1, pathIndex, preferredSurface;
        Vector3 position, segmentStart;
        Quaternion rotation = Quaternion.identity;
        float segmentTime, segmentLength, pause, retryAt, animationSpeed = 1f;
        float motionTime, restTime;
        bool wantsRest, routeRunning, perched;
        int[] previous, previousLink;
        float[] distance;
        bool[] visited;
        bool initialized, remoteInitialized;
        Vector3 remoteFrom;
        Quaternion remoteRotationFrom;
        ShipMonkeyPose remote;
        float receivedAt;
        uint sequence;
        int shownState = -1;
        float shownTime;
        static readonly int sitDownPerch = UnityEngine.Animator.StringToHash("SitDownPerch"), sitPerch = UnityEngine.Animator.StringToHash("SitPerch"), standUpPerch = UnityEngine.Animator.StringToHash("StandUpPerch");
        static readonly int idleRail = UnityEngine.Animator.StringToHash("IdleRail");
        static readonly int[] states = { UnityEngine.Animator.StringToHash("Idle"), UnityEngine.Animator.StringToHash("Walk"), UnityEngine.Animator.StringToHash("RailWalk"), UnityEngine.Animator.StringToHash("ClimbUp"), UnityEngine.Animator.StringToHash("ClimbDown"), UnityEngine.Animator.StringToHash("RailWalk"), UnityEngine.Animator.StringToHash("Run"), UnityEngine.Animator.StringToHash("SitDown"), UnityEngine.Animator.StringToHash("Sit"), UnityEngine.Animator.StringToHash("StandUp"), UnityEngine.Animator.StringToHash("JumpStart"), UnityEngine.Animator.StringToHash("JumpAir"), UnityEngine.Animator.StringToHash("JumpLand"), UnityEngine.Animator.StringToHash("Pickup"), UnityEngine.Animator.StringToHash("CarryIdle"), UnityEngine.Animator.StringToHash("CarryWalk"), UnityEngine.Animator.StringToHash("FishingCast"), UnityEngine.Animator.StringToHash("FishingWait"), UnityEngine.Animator.StringToHash("FishingReel"), UnityEngine.Animator.StringToHash("Work"), UnityEngine.Animator.StringToHash("Repair"), UnityEngine.Animator.StringToHash("Repair") };

        public void Begin()
        {
            if (Visual == null || Nodes.Length == 0) return;
            adjacent = new List<int>[Nodes.Length];
            previous = new int[Nodes.Length]; previousLink = new int[Nodes.Length];
            distance = new float[Nodes.Length]; visited = new bool[Nodes.Length];
            for (int i = 0; i < Nodes.Length; i++) adjacent[i] = new List<int>();
            for (int i = 0; i < Links.Length; i++)
            {
                var link = Links[i];
                if (link.A < 0 || link.B < 0 || link.A >= Nodes.Length || link.B >= Nodes.Length) continue;
                adjacent[link.A].Add(i); adjacent[link.B].Add(i);
            }
            current = Mathf.Clamp(StartNode, 0, Nodes.Length - 1);
            position = Point(current);
            target = -1; path.Clear(); preferredSurface = 0; pause = UnityEngine.Random.Range(.2f, .8f);
            Motion = ShipMonkeyMotion.Idle; initialized = true; remoteInitialized = false;
            motionTime = 0f; animationSpeed = 1f; wantsRest = routeRunning = false; rotation = Quaternion.identity;
            perched = false; shownState = -1;
            ResetLook(); ResetJump(); BeginActivities();
            Visual.gameObject.SetActive(true);
            Show(position, rotation, Motion, 1f);
        }

        public void End()
        {
            CancelActivities();
            initialized = remoteInitialized = false;
            ResetLook(); ResetJump();
            if (Visual != null) Visual.gameObject.SetActive(false);
        }

        public ShipMonkeyPose Capture() => new() { Position = position + kickReactionOffset, Rotation = rotation, Motion = Motion, AnimationSpeed = animationSpeed, MotionTime = motionTime, Perched = perched, HeadRider = HeadRider, Looking = looking, LookPoint = lookPoint, Carrying = activityShip != null && HasCarriedItem, HoldingBat = DefenseTask, BatTarget = batTarget, FishingPhase = fishingPhase, FishingTime = fishingTime, FishingPoint = fishingPoint, Sequence = ++sequence };

        public void Simulate(float delta)
        {
            if (!initialized || Visual == null) return;
            perched = Surface == ShipMonkeySurface.Rail || Surface == ShipMonkeySurface.Prop;
            motionTime += delta * animationSpeed;
            if (Time.time < kickReactionUntil)
            {
                kickReactionOffset = kickReactionDirection * Mathf.Sin(Mathf.Clamp01(1f - (kickReactionUntil - Time.time) / .25f) * Mathf.PI);
                Show(position + kickReactionOffset, rotation, Motion, animationSpeed, motionTime);
                return;
            }
            kickReactionOffset = Vector3.zero;
            UpdateRetaliation();
            if (UpdateHeadRide(delta)) { Show(position, rotation, Motion, 1f, motionTime); return; }
            if (UpdateJump(delta)) { Show(position, rotation, Motion, 1f, motionTime); return; }
            if (target < 0 && Nodes[current].Available) position = Point(current);
            if (target >= 0) UpdateActivities(delta);
            if (target >= 0 && (!Nodes[current].Available || !Nodes[target].Available))
            {
                CancelActivities();
                path.Clear(); target = -1; SetMotion(ShipMonkeyMotion.Idle); pause = 1f; wantsRest = false;
            }
            if (target < 0)
            {
                if (!Nodes[current].Available)
                {
                    if (task != TaskKind.None) CancelActivities();
                    SetMotion(ShipMonkeyMotion.Idle); wantsRest = false; looking = false;
                    if (Time.time >= retryAt) { retryAt = Time.time + 1f; Recover(); }
                    return;
                }
                if (UpdateActivities(delta))
                {
                    Show(position, rotation, Motion, animationSpeed, motionTime);
                    UpdateAttention(delta); return;
                }
                if (UpdateRest(delta))
                {
                    Show(position, rotation, Motion, 1f, motionTime);
                    UpdateAttention(delta);
                    return;
                }
                pause -= delta;
                if (pause <= 0f && ChoosePath(ActivityGoal)) StartSegment();
            }
            if (target >= 0 && jumpPhase == 0)
            {
                var link = Links[path[pathIndex]];
                bool canRun = (!ShowsCarry || task == TaskKind.Deliver) && routeRunning && link.Motion == ShipMonkeyMotion.Walk && Nodes[current].Surface == ShipMonkeySurface.Deck && Nodes[target].Surface == ShipMonkeySurface.Deck && Mathf.Abs(Point(target).y - segmentStart.y) < .12f;
                float speed = link.Motion == ShipMonkeyMotion.RailWalk ? RailSpeed : link.Motion == ShipMonkeyMotion.ClimbUp ? ClimbSpeed : canRun ? RunSpeed : WalkSpeed;
                segmentTime += delta * speed / segmentLength;
                float blend = Mathf.Clamp01(segmentTime);
                position = Vector3.Lerp(segmentStart, Point(target), blend);
                if (link.Motion == ShipMonkeyMotion.Hop) position.y += Mathf.Sin(blend * Mathf.PI) * HopHeight;
                Vector3 forward = link.Motion == ShipMonkeyMotion.ClimbUp ? link.Facing : Point(target) - segmentStart;
                forward.y = 0f;
                if (forward.sqrMagnitude > .0001f) rotation = Quaternion.RotateTowards(rotation, Quaternion.LookRotation(forward), delta * 280f);
                SetMotion(link.Motion == ShipMonkeyMotion.ClimbUp && Point(target).y < segmentStart.y ? ShipMonkeyMotion.ClimbDown : canRun ? ShipMonkeyMotion.Run : link.Motion);
                animationSpeed = Motion == ShipMonkeyMotion.ClimbUp || Motion == ShipMonkeyMotion.ClimbDown ? ClimbSpeed / .18f : Motion == ShipMonkeyMotion.RailWalk ? RailSpeed / .20f : Motion == ShipMonkeyMotion.Run ? RunSpeed / .45f : WalkSpeed / .45f;
                if (blend >= 1f)
                {
                    position = Point(target); current = target; target = -1; pathIndex++;
                    perched = Surface == ShipMonkeySurface.Rail || Surface == ShipMonkeySurface.Prop;
                    if (pathIndex < path.Count) StartSegment();
                    else { SetMotion(ShipMonkeyMotion.Idle); animationSpeed = 1f; pause = wantsRest ? .7f : UnityEngine.Random.Range(PauseMin, Mathf.Max(PauseMin, PauseMax)); }
                }
            }
            Show(position, rotation, Motion, animationSpeed, motionTime);
            UpdateAttention(delta);
        }

        void SetMotion(ShipMonkeyMotion value)
        {
            if (Motion == value) return;
            Motion = value; motionTime = 0f;
        }

        bool UpdateRest(float delta)
        {
            if (Motion == ShipMonkeyMotion.SitDown)
            {
                if (motionTime >= .7f) SetMotion(ShipMonkeyMotion.Sit);
                return true;
            }
            if (Motion == ShipMonkeyMotion.Sit)
            {
                if (perched && Nodes[current].SeaFacing.sqrMagnitude > .01f) rotation = Quaternion.RotateTowards(rotation, Quaternion.LookRotation(Nodes[current].SeaFacing), delta * 110f);
                restTime -= delta;
                if (restTime <= 0f) SetMotion(ShipMonkeyMotion.StandUp);
                return true;
            }
            if (Motion == ShipMonkeyMotion.StandUp)
            {
                if (perched)
                {
                    Vector3 facing = Vector3.zero; float best = float.PositiveInfinity;
                    foreach (int edge in adjacent[current])
                    {
                        var link = Links[edge]; int next = link.A == current ? link.B : link.A;
                        if (link.Motion != ShipMonkeyMotion.RailWalk || !Nodes[next].Available) continue;
                        Vector3 direction = Point(next) - position; direction.y = 0f;
                        if (direction.sqrMagnitude < .001f) continue;
                        float angle = Quaternion.Angle(rotation, Quaternion.LookRotation(direction));
                        if (angle < best) { best = angle; facing = direction; }
                    }
                    if (facing.sqrMagnitude > .001f) rotation = Quaternion.RotateTowards(rotation, Quaternion.LookRotation(facing), delta * 160f);
                }
                if (motionTime < .7f) return true;
                SetMotion(ShipMonkeyMotion.Idle); pause = .25f;
            }
            if (!wantsRest) return false;
            var node = Nodes[current];
            perched = node.Surface == ShipMonkeySurface.Rail;
            if (!perched && node.SeaFacing.sqrMagnitude > .01f)
            {
                Quaternion facing = Quaternion.LookRotation(node.SeaFacing);
                rotation = Quaternion.RotateTowards(rotation, facing, delta * 110f);
                if (Quaternion.Angle(rotation, facing) > 3f) return true;
            }
            pause -= delta;
            if (pause > 0f) return true;
            wantsRest = false; animationSpeed = 1f;
            restTime = UnityEngine.Random.Range(SitMin, Mathf.Max(SitMin, SitMax));
            if (node.SeaFacing.sqrMagnitude > .01f) restTime *= 1.25f;
            SetMotion(ShipMonkeyMotion.SitDown);
            return true;
        }

        void StartSegment()
        {
            var link = Links[path[pathIndex]];
            target = link.A == current ? link.B : link.A;
            if (!Nodes[target].Available) { target = -1; path.Clear(); pause = 1f; return; }
            segmentStart = position; segmentTime = 0f;
            segmentLength = Mathf.Max(.01f, Vector3.Distance(segmentStart, Point(target)));
            if (link.Motion == ShipMonkeyMotion.Hop) BeginJump(Point(target), false);
        }

        bool ChoosePath(int forcedDestination = -1)
        {
            for (int i = 0; i < Nodes.Length; i++) { distance[i] = float.PositiveInfinity; visited[i] = false; previous[i] = previousLink[i] = -1; }
            distance[current] = 0f;
            for (int count = 0; count < Nodes.Length; count++)
            {
                int nearest = -1; float best = float.PositiveInfinity;
                for (int i = 0; i < Nodes.Length; i++) if (!visited[i] && distance[i] < best) { best = distance[i]; nearest = i; }
                if (nearest < 0) break;
                visited[nearest] = true;
                foreach (int edge in adjacent[nearest])
                {
                    var link = Links[edge]; int next = link.A == nearest ? link.B : link.A;
                    if (visited[next] || !Nodes[next].Available || !LinkAllowed(next) || !SlotLinkClear(nearest, next)) continue;
                    float candidate = best + Vector3.Distance(Point(nearest), Point(next));
                    if (candidate >= distance[next]) continue;
                    distance[next] = candidate; previous[next] = nearest; previousLink[next] = edge;
                }
            }
            if (forcedDestination >= Nodes.Length || forcedDestination >= 0 && !visited[forcedDestination]) { CancelActivities(); pause = 2f; return false; }
            var wanted = (ShipMonkeySurface)(preferredSurface % 4 == 0 ? 0 : preferredSurface % 4 == 1 ? 1 : preferredSurface % 4 == 2 ? 4 : 3);
            wantsRest = task == TaskKind.None && attacker == null && UnityEngine.Random.value < SitChance;
            int destination = forcedDestination;
            float totalWeight = 0f;
            bool preferSea = UnityEngine.Random.value < .64f;
            if (wantsRest)
                for (int pass = 0; pass < 2 && destination < 0; pass++)
                for (int i = 0; i < Nodes.Length; i++)
                {
                    var node = Nodes[i];
                    if (i == current || !visited[i] || distance[i] < 1.5f || !node.RestSpot) continue;
                    if (pass == 0 && (node.SeaFacing.sqrMagnitude > .01f) != preferSea) continue;
                    float weight = 1f / (1f + distance[i] * .06f);
                    totalWeight += weight;
                    if (UnityEngine.Random.value * totalWeight < weight) destination = i;
                }
            if (destination < 0) wantsRest = false;
            int eligible = 0;
            for (int pass = 0; pass < 2 && destination < 0; pass++)
                for (int i = 0; i < Nodes.Length; i++)
                {
                    if (i == current || !visited[i] || distance[i] < 1.5f || pass == 0 && Nodes[i].Surface != wanted) continue;
                    eligible++;
                    if (UnityEngine.Random.Range(0, eligible) == 0) destination = i;
                }
            if (attacker != null)
            {
                Vector3 goal = transform.InverseTransformPoint(attacker.transform.position);
                float best = float.PositiveInfinity; destination = -1;
                for (int i = 0; i < Nodes.Length; i++)
                    if (i != current && visited[i] && Nodes[i].Available)
                    { float score = (Point(i) - goal).sqrMagnitude; if (score < best) { best = score; destination = i; } }
            }
            preferredSurface++;
            if (destination < 0) { pause = 2f; return false; }
            path.Clear();
            for (int node = destination; node != current; node = previous[node]) path.Add(previousLink[node]);
            path.Reverse(); pathIndex = 0;
            routeRunning = AidTask || DefenseTask || attacker != null || task == TaskKind.None && UnityEngine.Random.value < RunChance && distance[destination] > 3f;
            return path.Count > 0;
        }

        void Recover()
        {
            float best = 2.25f; int nearest = -1;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (!Nodes[i].Available || Point(i).y > position.y + .35f) continue;
                float candidate = (Point(i) - position).sqrMagnitude;
                if (candidate < best) { best = candidate; nearest = i; }
            }
            if (nearest < 0) { Visual.gameObject.SetActive(false); return; }
            current = nearest; position = Point(current); Visual.gameObject.SetActive(true);
        }

        public void Receive(ShipMonkeyPose pose)
        {
            if (Visual == null) return;
            initialized = false;
            if (remoteInitialized && pose.Sequence == remote.Sequence) return;
            remoteFrom = remoteInitialized ? Visual.localPosition : pose.Position;
            remoteRotationFrom = remoteInitialized ? Visual.localRotation : pose.Rotation;
            remote = pose; receivedAt = Time.unscaledTime;
            remoteInitialized = true;
            Visual.gameObject.SetActive(true);
        }

        public void PresentRemote()
        {
            if (!remoteInitialized || Visual == null) return;
            float blend = Mathf.Clamp01((Time.unscaledTime - receivedAt) / (remote.Motion >= ShipMonkeyMotion.JumpStart ? .05f : .12f));
            position = Vector3.Lerp(remoteFrom, remote.Position, blend);
            rotation = Quaternion.Slerp(remoteRotationFrom, remote.Rotation, blend);
            Motion = remote.Motion;
            perched = remote.Perched;
            looking = remote.Looking;
            lookPoint = remote.LookPoint;
            Show(position, rotation, remote.Motion, remote.AnimationSpeed, remote.MotionTime + (Time.unscaledTime - receivedAt) * remote.AnimationSpeed);
        }

        void Show(Vector3 point, Quaternion facing, ShipMonkeyMotion motion, float speed, float time = 0f)
        {
            Visual.SetLocalPositionAndRotation(point, facing);
            if (Animator == null) return;
            Animator.speed = Mathf.Clamp(speed, .1f, 3f);
            int state = perched && motion == ShipMonkeyMotion.Idle ? idleRail : perched && motion == ShipMonkeyMotion.SitDown ? sitDownPerch : perched && motion == ShipMonkeyMotion.Sit ? sitPerch : perched && motion == ShipMonkeyMotion.StandUp ? standUpPerch : states[(int)motion];
            if (ShowsCarry && motion == ShipMonkeyMotion.Idle) state = carryIdleState;
            else if (ShowsCarry && (motion == ShipMonkeyMotion.Walk || motion == ShipMonkeyMotion.Run)) state = carryWalkState;
            bool repeatRepair = motion == ShipMonkeyMotion.Repair && time < shownTime - .05f;
            shownTime = time;
            if (shownState == state && !repeatRepair) return;
            shownState = state;
            Animator.CrossFadeInFixedTime(state, motion >= ShipMonkeyMotion.JumpStart ? .06f : .12f, 0, time);
        }

        void OnDrawGizmosSelected()
        {
            if (Nodes == null || Links == null) return;
            foreach (var edge in Links)
            {
                if (edge.A < 0 || edge.B < 0 || edge.A >= Nodes.Length || edge.B >= Nodes.Length) continue;
                Gizmos.color = edge.Motion == ShipMonkeyMotion.ClimbUp ? Color.cyan : edge.Motion == ShipMonkeyMotion.RailWalk ? Color.yellow : Color.green;
                Gizmos.DrawLine(transform.TransformPoint(Point(edge.A)), transform.TransformPoint(Point(edge.B)));
            }
        }
    }
}
