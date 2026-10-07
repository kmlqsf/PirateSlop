using System;
using System.Linq;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipMonkey
    {
        enum TaskKind { None, Fetch, Carry, Deliver, Offer, GoFish, Fish, GoWheel, Wheel, GoSail, Sail, GoRepair, Repair, GoSlot, SlotPull, SlotWait }
        public Transform LeftHand, RightHand;
        public GameObject RodModel, FloatModel, FishModel;
        public Material FishingLineMaterial;
        public NetworkFish FishPrefab;
        [Min(20f)] public float FishingInterval = 120f;
        [Min(20f)] public float MischiefInterval = 120f;
        [Min(5f)] public float PickupInterval = 30f;
        [Range(0f, 1f)] public float PickupChance = .35f;
        [Range(0f, .1f)] public float MischiefAmount = .1f;
        public Vector2 BiteDelay = new(4f, 12f);
        [Min(.5f)] public float ReelDuration = 2.5f;
        public Vector3 HeldPoint => LeftHand != null && RightHand != null ? (LeftHand.position + RightHand.position) * .5f + Visual.forward * .04f : Visual.TransformPoint(new Vector3(0f, .48f, .27f));
        TaskKind task;
        NetworkFish fetchItem, heldItem;
        NetworkPlayer recipient;
        NetworkShip activityShip;
        SailSystem activitySails;
        HelmInteraction activityHelm;
        int activityGoal = -1, sailIndex = -1;
        float nextFishing, nextMischief, nextPickup, nextAid, nextGoalRefresh;
        float activityTime, activityDeadline, carryUntil, fishWait, nextReelAudio, workDelta;
        float fetchUnavailableAt = -1f, wheelDuration, wheelAmplitude, wheelStart;
        byte fishingPhase;
        float fishingTime;
        Vector3 fishingPoint;
        bool pickingUp, fetchForAid;
        bool[] activityReachable;
        readonly System.Collections.Generic.Queue<int> activityQueue = new();
        bool AidTask => task == TaskKind.Deliver || task == TaskKind.Offer || task == TaskKind.Fetch && fetchForAid;
        GameObject fishingRod, fishingFloat, fishingCatch;
        FishingRodBend fishingBend;
        LineRenderer fishingLine;
        Transform placementAnchor;
        static readonly int carryIdleState = UnityEngine.Animator.StringToHash("CarryIdle"), carryWalkState = UnityEngine.Animator.StringToHash("CarryWalk");
        bool HasCarriedItem => activityShip != null && heldItem != null && heldItem.IsSpawned && heldItem.MonkeyCarrier == activityShip.NetworkObject;
        bool ShowsCarry => remoteInitialized && !initialized ? remote.Carrying : activityShip != null && HasCarriedItem;
        int ActivityGoal => task == TaskKind.None || task == TaskKind.Carry ? -1 : activityGoal;
        bool DeckTask => task == TaskKind.Fetch || task == TaskKind.Carry || task == TaskKind.Deliver || task == TaskKind.Offer || task == TaskKind.GoWheel || task == TaskKind.Wheel || task == TaskKind.GoSail || task == TaskKind.Sail || task == TaskKind.GoRepair || task == TaskKind.Repair || task == TaskKind.GoSlot || task == TaskKind.SlotPull || task == TaskKind.SlotWait;

        float Interval(float seconds) => seconds * UnityEngine.Random.Range(.85f, 1.15f);

        void BeginActivities()
        {
            activityShip = GetComponentInParent<NetworkShip>(); activitySails = GetComponentInParent<SailSystem>();
            activityHelm = activityShip != null ? activityShip.Helm : null;
            task = TaskKind.None; activityGoal = -1; fishingPhase = 0;
            nextFishing = Time.time + Interval(FishingInterval);
            nextMischief = Time.time + Interval(MischiefInterval);
            nextPickup = Time.time + Interval(PickupInterval); nextAid = Time.time;
            activityReachable = new bool[Nodes.Length];
            BeginRepair();
            BeginSlotActivity();
        }

        bool LinkAllowed(int next) => !DeckTask || Surface != ShipMonkeySurface.Deck && !ShowsCarry || Nodes[next].Surface == ShipMonkeySurface.Deck;

        int DeckNear(Vector3 world, float limit = float.PositiveInfinity, bool reachableOnly = false)
        {
            var local = transform.InverseTransformPoint(world); float best = limit * limit; int result = -1;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (Nodes[i].Surface != ShipMonkeySurface.Deck || !Nodes[i].Available || reachableOnly && !activityReachable[i]) continue;
                float score = (Point(i) - local).sqrMagnitude;
                if (score < best) { best = score; result = i; }
            }
            return result;
        }

        bool Aboard(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.Eliminated.Value || player.Motor == null || player.Motor.IsDead) return false;
            var passenger = player.GetComponent<ShipDeckPassenger>();
            if (passenger != null && passenger.Ship == GetComponentInParent<Rigidbody>()) return true;
            foreach (var hit in Physics.RaycastAll(player.transform.position + Vector3.up * .2f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (!hit.transform.IsChildOf(player.transform) && hit.collider.GetComponentInParent<NetworkFish>() == null) return hit.collider.GetComponentInParent<NetworkShip>() == activityShip;
            return false;
        }

        bool Injured(NetworkPlayer player)
        {
            if (!Aboard(player)) return false;
            var health = player.GetComponent<CombatHealth>();
            return health != null && health.Current < health.MaxHealth - .01f;
        }

        NetworkPlayer ClosestInjured(bool reachableOnly = false)
        {
            NetworkPlayer result = null; float best = float.PositiveInfinity;
            foreach (var player in NetworkPlayer.Active)
            {
                if (!Injured(player) || reachableOnly && DeckNear(player.transform.position, 2f, true) < 0) continue;
                float score = (player.transform.position - transform.TransformPoint(position)).sqrMagnitude;
                if (score < best) { best = score; result = player; }
            }
            return result;
        }

        void SetTask(TaskKind value, int goal = -1)
        {
            task = value; activityGoal = goal; activityTime = 0f; activityDeadline = Time.time + 120f;
            wantsRest = false; restTime = 0f; pause = 0f; routeRunning = AidTask; pickingUp = false;
            if (Motion == ShipMonkeyMotion.Sit || Motion == ShipMonkeyMotion.SitDown) SetMotion(ShipMonkeyMotion.StandUp);
            if (target < 0) { path.Clear(); pathIndex = 0; }
            else if (path.Count > pathIndex + 1) path.RemoveRange(pathIndex + 1, path.Count - pathIndex - 1);
        }

        void FindActivityReachability()
        {
            Array.Clear(activityReachable, 0, activityReachable.Length); activityQueue.Clear();
            activityReachable[current] = true; activityQueue.Enqueue(current);
            bool deckOnly = Surface == ShipMonkeySurface.Deck;
            while (activityQueue.Count > 0)
            {
                int from = activityQueue.Dequeue();
                foreach (int edge in adjacent[from])
                {
                    var link = Links[edge]; int next = link.A == from ? link.B : link.A;
                    if (activityReachable[next] || !Nodes[next].Available || deckOnly && Nodes[next].Surface != ShipMonkeySurface.Deck) continue;
                    activityReachable[next] = true; activityQueue.Enqueue(next);
                }
            }
        }

        void InterruptForAid()
        {
            bool sitting = Motion == ShipMonkeyMotion.Sit || Motion == ShipMonkeyMotion.SitDown;
            CancelActivities(); attacker = null; aggressionUntil = 0f;
            if (sitting) SetMotion(ShipMonkeyMotion.StandUp);
        }

        bool PrioritizeAid()
        {
            if (Time.time < nextAid || AidTask) return false;
            nextAid = Time.time + .5f;
            var injured = ClosestInjured();
            if (injured == null) return false;
            if (HasCarriedItem && heldItem.CurrentItem == InventoryItem.Fish)
            {
                FindActivityReachability();
                injured = ClosestInjured(true);
                if (injured == null) return false;
                int goal = DeckNear(injured.transform.position, 2f, true);
                if (goal < 0) return false;
                recipient = injured; attacker = null; aggressionUntil = 0f;
                SetTask(TaskKind.Deliver, goal); return true;
            }
            return StartFetch(true);
        }

        bool StartFetch(bool aid)
        {
            var injured = aid ? ClosestInjured() : null;
            if (aid && injured == null) return false;
            FindActivityReachability();
            if (aid) { injured = ClosestInjured(true); if (injured == null) return false; }
            NetworkFish selected = null; int goal = -1; float best = float.PositiveInfinity;
            foreach (var item in NetworkFish.ServerItems)
            {
                if (item == null || !item.MonkeyCanCollect || item.GroundShip != activityShip || aid && item.CurrentItem != InventoryItem.Fish) continue;
                int node = DeckNear(item.transform.position, 1.3f, true);
                if (node < 0) continue;
                float score = (item.transform.position - transform.TransformPoint(position)).sqrMagnitude;
                if (score < best) { best = score; selected = item; goal = node; }
            }
            if (selected == null) return false;
            if (aid) InterruptForAid();
            recipient = injured; fetchItem = selected; fetchForAid = aid; fetchUnavailableAt = -1f;
            SetTask(TaskKind.Fetch, goal); nextAid = Time.time + .5f; return true;
        }

        void StartFishingTrip()
        {
            nextFishing = Time.time + Interval(FishingInterval);
            if (FishPrefab == null || RodModel == null || OceanSurface.Instance == null) return;
            var candidates = Enumerable.Range(0, Nodes.Length).Where(i => Nodes[i].Available && Nodes[i].Surface == ShipMonkeySurface.Rail && Nodes[i].SeaFacing.sqrMagnitude > .1f && Point(i).y < 11f)
                .OrderBy(i => (Point(i) - position).sqrMagnitude).Take(12).ToArray();
            if (candidates.Length == 0) return;
            SetTask(TaskKind.GoFish, candidates[UnityEngine.Random.Range(0, candidates.Length)]);
        }

        bool WheelFree => activityHelm != null && activityHelm.StructurallyAvailable && !activityHelm.IsControlling && activityHelm.Wheel != null && activityHelm.Wheel.gameObject.activeInHierarchy;
        bool SailFree => activitySails != null && activitySails.CanMonkeyAdjust(sailIndex);

        void StartMischiefTrip()
        {
            nextMischief = Time.time + Interval(MischiefInterval);
            var sails = activitySails != null ? Enumerable.Range(0, activitySails.RopeCount).Where(i => activitySails.CanMonkeyAdjust(i)).ToArray() : Array.Empty<int>();
            if (WheelFree && (sails.Length == 0 || UnityEngine.Random.value < .5f))
            {
                int goal = DeckNear(activityHelm.transform.position - transform.up * .7f, 2f);
                if (goal >= 0) SetTask(TaskKind.GoWheel, goal);
                return;
            }
            if (sails.Length == 0) return;
            sailIndex = sails[UnityEngine.Random.Range(0, sails.Length)];
            int sailGoal = DeckNear(activitySails.RopeHandles[sailIndex].transform.position - transform.up * .6f, 2f);
            if (sailGoal >= 0) SetTask(TaskKind.GoSail, sailGoal);
        }

        void Face(Vector3 world, float delta)
        {
            var direction = transform.InverseTransformPoint(world) - position; direction.y = 0f;
            if (direction.sqrMagnitude > .001f) rotation = Quaternion.RotateTowards(rotation, Quaternion.LookRotation(direction), delta * 180f);
        }

        void ActivitySound(SoundCue cue, Vector3 point) => activityShip.MonkeySound(cue, point);

        bool UpdateActivities(float delta)
        {
            if (activityShip == null || !activityShip.IsServerInitialized) return false;
            if (task != TaskKind.SlotPull && task != TaskKind.SlotWait) PrioritizeAid();
            if (attacker != null) { if (task != TaskKind.None || heldItem != null) CancelActivities(); return false; }
            if ((task == TaskKind.Carry || task == TaskKind.Deliver || task == TaskKind.Offer || heldItem != null) && !HasCarriedItem) { heldItem = null; CancelActivities(); }
            if (task != TaskKind.None && Time.time > activityDeadline) { CancelActivities(); return false; }
            if (task == TaskKind.None)
            {
                if (TryStartRepair()) return false;
                if (TryStartSlotTrip()) return false;
                if (Time.time >= nextFishing) { StartFishingTrip(); if (task != TaskKind.None) return false; }
                if (Time.time >= nextMischief) { StartMischiefTrip(); if (task != TaskKind.None) return false; }
                if (Time.time >= nextPickup)
                {
                    nextPickup = Time.time + Interval(PickupInterval);
                    if (UnityEngine.Random.value < PickupChance) StartFetch(false);
                }
                return false;
            }
            if (Motion == ShipMonkeyMotion.SitDown || Motion == ShipMonkeyMotion.Sit || Motion == ShipMonkeyMotion.StandUp) return false;
            if (task == TaskKind.GoRepair || task == TaskKind.Repair) return UpdateRepair(delta);
            if (task == TaskKind.GoSlot || task == TaskKind.SlotPull || task == TaskKind.SlotWait) return UpdateSlotActivity(delta);
            if (task == TaskKind.Fetch)
            {
                if (fetchItem == null || !fetchItem.Available || fetchItem.MonkeyCarried) { CancelActivities(); return false; }
                if (!fetchItem.MonkeyCanCollect || fetchItem.GroundShip != activityShip)
                {
                    if (fetchUnavailableAt < 0f) fetchUnavailableAt = Time.time;
                    if (Time.time - fetchUnavailableAt > 2f) { CancelActivities(); return false; }
                    if (target < 0) { pickingUp = false; SetMotion(ShipMonkeyMotion.Idle); }
                    return target < 0;
                }
                fetchUnavailableAt = -1f;
                if (target >= 0) return false;
                if (!pickingUp)
                {
                    int node = DeckNear(fetchItem.transform.position, 1.3f);
                    if (node < 0) { CancelActivities(); return false; }
                    activityGoal = node;
                    if (current != node) return false;
                    if (Vector3.Distance(transform.TransformPoint(position), fetchItem.transform.position) > 1.15f) { CancelActivities(); return false; }
                    pickingUp = true; activityTime = 0f; animationSpeed = 1f; SetMotion(ShipMonkeyMotion.Pickup);
                }
                Face(fetchItem.transform.position, delta); activityTime += delta;
                if (activityTime < .7f) return true;
                if (!fetchItem.TryMonkeyCarry(activityShip, HeldPoint)) { CancelActivities(); return false; }
                heldItem = fetchItem; fetchItem = null;
                ActivitySound(SoundCue.Pickup, HeldPoint);
                carryUntil = Time.time + UnityEngine.Random.Range(18f, 32f);
                if (recipient != null && Injured(recipient)) SetTask(TaskKind.Deliver, DeckNear(recipient.transform.position));
                else { recipient = null; SetTask(TaskKind.Carry); }
                SetMotion(ShipMonkeyMotion.Idle); return true;
            }
            if (task == TaskKind.Carry)
            {
                if (Time.time >= carryUntil && target < 0) { DropHeld(); CancelActivities(); return false; }
                if (heldItem.CurrentItem == InventoryItem.Fish && Time.time >= nextAid)
                {
                    nextAid = Time.time + .5f; recipient = ClosestInjured();
                    if (recipient != null) SetTask(TaskKind.Deliver, DeckNear(recipient.transform.position));
                }
                return false;
            }
            if (task == TaskKind.Deliver || task == TaskKind.Offer)
            {
                if (!Injured(recipient)) recipient = ClosestInjured();
                if (recipient == null) { SetTask(TaskKind.Carry); carryUntil = Time.time + 8f; return false; }
                if (Time.time >= nextGoalRefresh)
                {
                    nextGoalRefresh = Time.time + 1f;
                    int goal = DeckNear(recipient.transform.position);
                    if (goal < 0) { CancelActivities(); return false; }
                    if (activityGoal != goal)
                    {
                        activityGoal = goal;
                        if (target >= 0 && path.Count > pathIndex + 1) path.RemoveRange(pathIndex + 1, path.Count - pathIndex - 1);
                    }
                }
                if (target >= 0) return false;
                float separation = Vector3.Distance(transform.TransformPoint(position), recipient.transform.position);
                bool visible = true;
                Vector3 destination = recipient.transform.position + Vector3.up * .65f;
                Vector3 direction = destination - HeldPoint;
                foreach (var hit in Physics.RaycastAll(HeldPoint, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(Visual) && !hit.transform.IsChildOf(recipient.transform) && hit.collider.GetComponentInParent<NetworkFish>() != heldItem) { visible = false; break; }
                if (separation > 1.5f || !visible)
                {
                    if (task == TaskKind.Offer) SetTask(TaskKind.Deliver, activityGoal);
                    return false;
                }
                if (task != TaskKind.Offer) { SetTask(TaskKind.Offer); SetMotion(ShipMonkeyMotion.Idle); }
                Face(recipient.transform.position, delta); activityTime += delta; animationSpeed = 1f;
                if (activityTime >= 6f) { DropHeld(recipient.transform.position); CancelActivities(); }
                return true;
            }
            if ((task == TaskKind.GoWheel || task == TaskKind.Wheel) && !WheelFree || (task == TaskKind.GoSail || task == TaskKind.Sail) && !SailFree)
            { CancelActivities(); return false; }
            if (task == TaskKind.GoFish || task == TaskKind.GoWheel || task == TaskKind.GoSail)
            {
                if (target >= 0 || current != activityGoal) return false;
                if (task == TaskKind.GoFish)
                {
                    var outward = Nodes[current].SeaFacing;
                    fishingPoint = position + outward * 10f;
                    var ocean = OceanSurface.Instance;
                    var worldPoint = transform.TransformPoint(fishingPoint);
                    var world = PirateSlop.World.ProceduralWorld.Instance;
                    if (ocean == null || world != null && world.Ready && world.GroundHeight(worldPoint) > ocean.Height(worldPoint) - 1f) { CancelActivities(); return false; }
                    task = TaskKind.Fish; fishingPhase = 1; fishingTime = 0f;
                    Face(transform.TransformPoint(fishingPoint), 10f); SetMotion(ShipMonkeyMotion.FishingCast);
                    ActivitySound(SoundCue.FishingCast, HeldPoint);
                }
                else
                {
                    bool wheel = task == TaskKind.GoWheel;
                    var grip = wheel ? activityHelm.transform : activitySails.RopeHandles[sailIndex].transform;
                    if (Vector3.Distance(HeldPoint, grip.position) > 1.7f) { CancelActivities(); return false; }
                    task = wheel ? TaskKind.Wheel : TaskKind.Sail;
                    if (wheel) { wheelDuration = UnityEngine.Random.Range(10f, 15f); wheelAmplitude = UnityEngine.Random.Range(.3f, .5f); wheelStart = activityHelm.CurrentRudderNormalized; }
                    float value = wheel ? activityHelm.CurrentRudderNormalized : activitySails.Tension(sailIndex);
                    workDelta = value > .9f ? -MischiefAmount : value < .1f ? MischiefAmount : (UnityEngine.Random.value < .5f ? -MischiefAmount : MischiefAmount);
                    Face(grip.position, 10f); SetMotion(ShipMonkeyMotion.Work);
                }
                activityTime = 0f; animationSpeed = 1f; return true;
            }
            if (task == TaskKind.Fish)
            {
                fishingTime += delta; animationSpeed = 1f;
                if (fishingPhase == 1 && fishingTime >= .667f)
                {
                    fishingPhase = 2; fishingTime = 0f; fishWait = UnityEngine.Random.Range(BiteDelay.x, BiteDelay.y);
                    SetMotion(ShipMonkeyMotion.FishingWait); ActivitySound(SoundCue.Splash, transform.TransformPoint(fishingPoint));
                }
                else if (fishingPhase == 2 && fishingTime >= fishWait)
                {
                    fishingPhase = 3; fishingTime = 0f; nextReelAudio = 0f;
                    SetMotion(ShipMonkeyMotion.FishingReel); ActivitySound(SoundCue.FishingBite, transform.TransformPoint(fishingPoint));
                }
                else if (fishingPhase == 3)
                {
                    if (Time.time >= nextReelAudio) { nextReelAudio = Time.time + .5f; ActivitySound(SoundCue.FishingReel, HeldPoint); }
                    if (fishingTime >= ReelDuration)
                    {
                        if (TryDropPlacement(FishPrefab, transform.TransformPoint(position), out var point, out var facing))
                        {
                            var fish = UnityEngine.Object.Instantiate(FishPrefab, point, facing);
                            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fish.gameObject, gameObject.scene);
                            fish.Place(activityShip.NetworkObject, point, facing); activityShip.ServerManager.Spawn(fish.NetworkObject);
                            ActivitySound(SoundCue.FishingCatch, point); ActivitySound(SoundCue.FishDrop, point);
                            CancelActivities(); nextPickup = Time.time + 10f; return true;
                        }
                        if (fishingTime > ReelDuration + 5f) CancelActivities();
                    }
                }
                return true;
            }
            if (task == TaskKind.Wheel || task == TaskKind.Sail)
            {
                var grip = task == TaskKind.Wheel ? activityHelm.transform : activitySails.RopeHandles[sailIndex].transform;
                Face(grip.position, delta);
                if (task == TaskKind.Wheel)
                {
                    activityTime += delta;
                    float desired = Mathf.Lerp(wheelStart, wheelAmplitude * Mathf.Sin(activityTime * Mathf.PI * 2f / 5f), Mathf.Clamp01(activityTime / .75f));
                    float value = activityHelm.CurrentRudderNormalized;
                    bool moved = activityHelm.MonkeyAdjust(Mathf.MoveTowards(value, desired, delta) - value);
                    if (!moved || activityTime >= wheelDuration) CancelActivities();
                    return true;
                }
                float previous = Mathf.Clamp01(activityTime / 2.5f);
                activityTime += delta;
                float step = workDelta * (Mathf.Clamp01(activityTime / 2.5f) - previous);
                bool applied = task == TaskKind.Wheel ? activityHelm.MonkeyAdjust(step) : activitySails.MonkeyAdjust(sailIndex, step);
                if (!applied || activityTime >= 2.5f) CancelActivities();
                return true;
            }
            return false;
        }

        bool TryDropPlacement(NetworkFish item, Vector3 near, out Vector3 point, out Quaternion facing)
        {
            point = default; facing = Quaternion.identity;
            if (item == null || activityShip == null) return false;
            if (placementAnchor == null) { placementAnchor = new GameObject("MonkeyDropAnchor").transform; placementAnchor.SetParent(transform, false); }
            var shapes = item.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
            bool disable = item.IsSpawned;
            if (disable) foreach (var shape in shapes) shape.enabled = false;
            try
            {
                var local = transform.InverseTransformPoint(near);
                foreach (int node in Enumerable.Range(0, Nodes.Length).Where(i => Nodes[i].Surface == ShipMonkeySurface.Deck && Nodes[i].Available).OrderBy(i => (Point(i) - local).sqrMagnitude).Take(12))
                {
                    Vector3 floor = transform.TransformPoint(Point(node));
                    placementAnchor.SetPositionAndRotation(floor - Visual.forward * .9f, Visual.rotation);
                    if (LootPlacement.Find(placementAnchor, item, item.CurrentItem, out point, out facing, out var support) && support == activityShip) return true;
                }
            }
            finally { if (disable) foreach (var shape in shapes) if (shape != null) shape.enabled = true; }
            return false;
        }

        void DropHeld(Vector3? near = null)
        {
            if (!HasCarriedItem) { heldItem = null; return; }
            if (!TryDropPlacement(heldItem, near ?? transform.TransformPoint(position), out var point, out var facing))
            {
                int node = DeckNear(near ?? transform.TransformPoint(position));
                point = node >= 0 ? transform.TransformPoint(Point(node)) + transform.up * .15f : transform.TransformPoint(position);
                facing = Visual.rotation * Quaternion.Euler(0f, 0f, heldItem.CurrentItem == InventoryItem.Fish ? 90f : 0f);
            }
            heldItem.ReleaseMonkeyCarry(activityShip.NetworkObject, point, facing);
            ActivitySound(SoundCue.FishDrop, point); heldItem = null;
        }

        public void StopActivities() => CancelActivities();

        void CancelActivities()
        {
            if (activityShip != null && activityShip.IsServerInitialized) DropHeld();
            ResetRepair();
            ResetSlotActivity();
            fetchItem = null; recipient = null; task = TaskKind.None; activityGoal = -1; fishingPhase = 0;
            pickingUp = fetchForAid = false; fetchUnavailableAt = -1f; wantsRest = false; animationSpeed = 1f; nextAid = Time.time + .5f;
            if (target < 0) { path.Clear(); pathIndex = 0; SetMotion(ShipMonkeyMotion.Idle); pause = .5f; }
        }

        void PresentActivities()
        {
            if (Application.isBatchMode) return;
            byte phase = (!initialized && !remoteInitialized) || Visual == null || !Visual.gameObject.activeInHierarchy ? (byte)0 : initialized ? fishingPhase : remote.FishingPhase;
            float elapsed = initialized ? fishingTime : remote.FishingTime + Time.unscaledTime - receivedAt;
            var localPoint = initialized ? fishingPoint : remote.FishingPoint;
            if (phase == 0)
            {
                if (fishingRod != null) fishingRod.SetActive(false);
                if (fishingFloat != null) fishingFloat.SetActive(false);
                if (fishingLine != null) fishingLine.enabled = false;
                if (fishingCatch != null) fishingCatch.SetActive(false);
                return;
            }
            if (RodModel == null || FloatModel == null || RightHand == null) return;
            if (fishingRod == null)
            {
                fishingRod = Instantiate(RodModel, Visual); fishingRod.name = "MonkeyFishingRod";
                fishingBend = fishingRod.GetComponent<FishingRodBend>();
                if (fishingBend == null) fishingBend = fishingRod.AddComponent<FishingRodBend>();
                fishingFloat = Instantiate(FloatModel, transform); fishingFloat.name = "MonkeyFishingFloat";
                var lineObject = new GameObject("MonkeyFishingLine"); lineObject.transform.SetParent(Visual, false);
                fishingLine = lineObject.AddComponent<LineRenderer>(); fishingLine.sharedMaterial = FishingLineMaterial;
                fishingLine.useWorldSpace = true; fishingLine.positionCount = 16; fishingLine.startWidth = .006f; fishingLine.endWidth = .004f;
            }
            fishingRod.SetActive(true); fishingFloat.SetActive(true); fishingLine.enabled = true;
            float bend = phase == 3 ? .10f + .025f * Mathf.Sin(elapsed * 18f) : 0f;
            fishingRod.transform.SetPositionAndRotation(RightHand.position, Visual.rotation * Quaternion.Euler(phase == 3 ? -15f : -12f, -8f, 0f));
            fishingBend.SetBend(bend);
            Vector3 end = transform.TransformPoint(localPoint);
            if (OceanSurface.Instance != null) end.y = OceanSurface.Instance.Height(end) + .04f;
            if (phase == 1)
            {
                float t = Mathf.Clamp01(elapsed / .667f);
                end = Vector3.Lerp(RightHand.position, end, t) + Vector3.up * (4f * t * (1f - t));
            }
            else if (phase == 3) end = Vector3.Lerp(end, RightHand.position, Mathf.Clamp01(elapsed / ReelDuration));
            fishingFloat.transform.position = end;
            if (phase == 3 && fishingCatch == null && FishModel != null)
            {
                fishingCatch = Instantiate(FishModel, Visual); fishingCatch.name = "MonkeyReeledFish";
            }
            if (fishingCatch != null)
            {
                fishingCatch.SetActive(phase == 3);
                fishingCatch.transform.SetPositionAndRotation(end - Vector3.up * .22f, Visual.rotation * Quaternion.Euler(0f, 90f, Mathf.Sin(elapsed * 9f) * 4f));
            }
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                fishingLine.SetPosition(i, Vector3.Lerp(fishingBend.Tip, end, t) - Vector3.up * (.2f * Mathf.Sin(t * Mathf.PI)));
            }
        }

        void OnDestroy()
        {
            if (fishingFloat != null) Destroy(fishingFloat);
            if (placementAnchor != null) Destroy(placementAnchor.gameObject);
        }
    }
}
