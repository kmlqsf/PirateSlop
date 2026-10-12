using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.Harpoon;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct TowedCargoPose { public Vector3 Position; public Quaternion Rotation; }
    public struct SternRopeFloorDeposit { public Vector3 Position; public float Length; }
    public enum SternCargoPhase : byte { Towing, Hoisting, Crossing, Lowering, Landed }
    [DefaultExecutionOrder(55)]
    public sealed class NetworkTowedCargo : NetworkBehaviour
    {
        public static readonly List<NetworkTowedCargo> Active = new();
        public Transform ChestSocket, SlingEye;
        public GameObject BarrelVisual, BarrelSling, ChestSling;
        public LineRenderer TowRope;
        public float EyeHeight = 1.3f, HoistSpeed = 1.6f;
        readonly SyncVar<NetworkObject> source = new(), operatorObject = new(), poseSupport = new();
        readonly SyncVar<TowedCargoPose> pose = new();
        readonly SyncVar<int> stationIndex = new();
        readonly SyncVar<bool> chestKind = new(), broken = new(), recovered = new(), contested = new(), identified = new(), lootReady = new();
        readonly SyncVar<float> theft = new(), knot = new(), towDistance = new(38), travel = new(), selectedRope = new();
        readonly SyncVar<Vector3> liftStart = new(), freeGripLocal = new(), gripStartLocal = new();
        readonly SyncVar<uint> gripVersion = new();
        readonly SyncVar<float> floorRopeLength = new();
        readonly SyncList<SternRopeFloorDeposit> floorDeposits = new();
        readonly SyncVar<SternCargoPhase> phase = new();
        readonly SyncVar<Vector2> hand = new();
        NetworkLootChest chest;
        Rigidbody body;
        SternCargoRopeTail ropeTail;
        float heartbeat, nextPublish, visualTravel, freeVerticalVelocity, lastRopeInput, liftDemand, nextFloorUpdate;
        Vector3 lastWalkLocal;
        uint gripAttempt;
        bool spawnChest, poseReady, completionReported;
        Vector3 visualLocal;
        Quaternion visualRotation = Quaternion.identity;
        NetworkObject previousSupport;
        HarpoonProjectile towingHook;
        public NetworkSternCargoEvent Source => source.Value != null ? source.Value.GetComponent<NetworkSternCargoEvent>() : null;
        public SternRopeStation Station => Source != null && stationIndex.Value >= 0 && stationIndex.Value < Source.RopeStations.Length ? Source.RopeStations[stationIndex.Value] : null;
        public NetworkObject Operator => operatorObject.Value;
        public bool TowBroken => broken.Value;
        public bool Recovered => recovered.Value;
        public bool IsChest => chestKind.Value;
        public bool Contested => contested.Value;
        public bool LootReady => lootReady.Value;
        public float TheftProgress => theft.Value;
        public bool CanWork => IsSpawned && !TowBroken && !Recovered && Station != null && (Phase == SternCargoPhase.Towing || Phase == SternCargoPhase.Hoisting);
        public bool KnotUntied => knot.Value >= 1;
        public SternCargoPhase Phase => phase.Value;
        public Vector2 HandMotion => hand.Value;
        public byte WorkStage => !KnotUntied ? (byte)0 : Phase == SternCargoPhase.Towing ? (byte)1 : (byte)2;
        public float SelectedRope => selectedRope.Value;
        public float FloorRopeLength => floorRopeLength.Value;
        public int FloorDepositCount => floorDeposits.Count;
        public SternRopeFloorDeposit FloorDeposit(int index) => floorDeposits[index];
        public uint GripVersion => gripVersion.Value;
        public Vector3 GripStart => Source.transform.TransformPoint(gripStartLocal.Value);
        public int StationIndex => stationIndex.Value;
        public float WorkProgress => !KnotUntied ? knot.Value : WalkingPull ? 1-towDistance.Value/Mathf.Max(1,Source.InitialCable) : travel.Value;
        public bool WalkingPull => KnotUntied && Phase == SternCargoPhase.Towing;
        public Vector3 RestGrip => Source != null ? Source.transform.TransformPoint(freeGripLocal.Value) : transform.position;
        public string MotionHint => !KnotUntied ? "Свободно двигайте мышью — развязать узел"
            : WalkingPull ? "Отходите от кормы к носу с канатом · WASD"
            : "ЛКМ + мышь вниз — тянуть · отпустить и снова ЛКМ — перехватить";
        public string PayloadName => identified.Value ? IsChest ? "Сундук" : "Бочка" : "Неизвестный груз";
        public string WorkHint => knot.Value < 1 ? $"1/3 · Развязать узел: {knot.Value:P0}"
            : phase.Value == SternCargoPhase.Towing ? $"2/3 · Подтянуть к кораблю: {1-towDistance.Value/Mathf.Max(1,Source.InitialCable):P0}"
            : phase.Value == SternCargoPhase.Hoisting ? $"3/3 · {PayloadName} · поднять на палубу: {travel.Value:P0}"
            : phase.Value == SternCargoPhase.Crossing || phase.Value == SternCargoPhase.Lowering ? $"3/3 · {PayloadName} · укладываем на палубу" : PayloadName+" на палубе";
        void Awake() { body = GetComponent<Rigidbody>(); ropeTail = GetComponent<SternCargoRopeTail>(); }
        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public void Configure(NetworkSternCargoEvent owner, int index, bool isChest)
        { source.Value = owner.NetworkObject; stationIndex.Value = index; chestKind.Value = isChest; towDistance.Value = owner.InitialCable; freeGripLocal.Value = owner.transform.InverseTransformPoint(Station.Grip.position); }
        public override void OnStartServer() { base.OnStartServer(); spawnChest = IsChest; PublishPose(); }
        void SpawnChest()
        {
            spawnChest = false;
            var catalog = Source != null ? Source.Catalog : null;
            if (catalog == null || catalog.ChestPrefab == null) return;
            chest = Instantiate(catalog.ChestPrefab, ChestSocket.position, ChestSocket.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = catalog;
            var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId*2654435761u ^ 0xd287fc35u));
            chest.Fill(ref random); chest.PlaceInTow(this); ServerManager.Spawn(chest.NetworkObject);
        }
        public bool InputRope(NetworkWeapon actor, bool held, Vector2 motion, Vector3 grabLocal, byte stage, uint request, bool begin, out string reason)
        {
            reason = null;
            bool validMotion = float.IsFinite(motion.x) && float.IsFinite(motion.y);
            if (!held)
            {
                if (actor != null && Operator == actor.NetworkObject && gripAttempt == request)
                {
                    if (validMotion && stage == WorkStage && BlockReason(actor,true) == null) MoveHands(motion,Mathf.Clamp(Time.time-lastRopeInput,0,.15f));
                    ReleaseOperator();
                }
                return true;
            }
            bool holding = actor != null && Operator == actor.NetworkObject;
            reason = BlockReason(actor, holding);
            if (reason != null) return false;
            if (!validMotion) { reason = "Неверное движение каната"; return false; }
            if (!holding)
            {
                if (!begin) { reason = "Захват каната завершён"; return false; }
                if (!float.IsFinite(grabLocal.sqrMagnitude) || KnotUntied && !CanGripPoint(actor,Source.transform.TransformPoint(grabLocal)))
                { reason = "Подойдите к доступному участку каната"; return false; }
                gripAttempt = request; hand.Value = Vector2.zero;
                gripStartLocal.Value = KnotUntied ? grabLocal : Source.transform.InverseTransformPoint(Station.Grip.position); gripVersion.Value++;
                lastRopeInput = Time.time; lastWalkLocal = Source.transform.InverseTransformPoint(actor.transform.position);
            }
            if (gripAttempt != request) { reason = "Захват каната завершён"; return false; }
            operatorObject.Value = actor.NetworkObject; heartbeat = Time.time+.4f;
            actor.GetComponent<ShipDeckPassenger>().Attach(Source.Ship.Body);
            actor.GetComponent<AdvancedPlayerController>().ShipActivityLocked = true;
            if (!begin && stage == WorkStage) MoveHands(motion,Mathf.Clamp(Time.time-lastRopeInput,0,.15f));
            actor.GetComponent<AdvancedPlayerController>().CargoRopeWalking = WalkingPull;
            if (begin) UpdateFloorRope(actor,true);
            lastRopeInput = Time.time; return true;
        }
        string BlockReason(NetworkWeapon actor, bool holding)
        {
            if (!CanWork || Source.Ship.IsSinking || actor == null || !actor.IsSpawned) return "Этот канат уже освобождён";
            var motor = actor.GetComponent<AdvancedPlayerController>();
            if (motor == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsClimbing || motor.IsSwimming || !holding && !motor.IsGrounded) return "Встаньте на кормовую палубу";
            if (actor.LootHandsBusy || actor.GetComponent<CannonHands>()?.HasHeldBall == true || actor.GetComponent<PlayerInventory>()?.HandsOccupied == true) return "Освободите руки";
            if (!CanReach(actor,holding)) return KnotUntied ? "Подойдите к доступному участку каната" : "Подойдите к узлу между балясинами";
            if (Operator != null && Operator != actor.NetworkObject) return "Этот канат уже выбирает другой игрок";
            if (Source.Ship.Helm.Driver == motor) return "Сначала отпустите штурвал";
            if (!holding) foreach (var cargo in Active) if (cargo != this && cargo.Operator == actor.NetworkObject) return "Сначала отпустите другой канат";
            return null;
        }
        public bool CanReach(NetworkWeapon actor, bool holding = false)
        {
            if (Source == null || actor == null) return false;
            if (!KnotUntied) return Source.CanReach(actor,stationIndex.Value,holding);
            return Source.CanReach(actor,stationIndex.Value,true,true) && (holding || FindReachableGrip(actor,out _));
        }
        public bool FindReachableGrip(NetworkWeapon actor,out Vector3 point)
        {
            point = RestGrip;
            if (actor == null || !KnotUntied) return false;
            var center = actor.transform.position+Vector3.up*1.15f;
            float best = float.PositiveInfinity;
            ClosestOnLine(TowRope,12,center,ref best,ref point);
            if (ropeTail != null) ClosestOnLine(ropeTail.Tail,int.MaxValue,center,ref best,ref point);
            return best<3.3f*3.3f;
        }
        public bool CanGripPoint(NetworkWeapon actor,Vector3 point)
        {
            if (actor == null || Source == null || !Source.CanReach(actor,stationIndex.Value,true,true) ||
                Vector3.Distance(actor.transform.position+Vector3.up*1.15f,point)>3.3f) return false;
            float best = float.PositiveInfinity; var closest = point;
            ClosestOnLine(TowRope,12,point,ref best,ref closest);
            if (ropeTail != null) ClosestOnLine(ropeTail.Tail,int.MaxValue,point,ref best,ref closest);
            if (best>.45f*.45f) return false;
            var motor = actor.GetComponent<AdvancedPlayerController>();
            var eye = actor.IsOwner && motor.PlayerCamera != null ? motor.PlayerCamera.transform.position
                : actor.transform.position+Vector3.up*(1.5f-motor.CrouchBlend*.7f);
            var delta = point-eye;
            foreach (var hit in Physics.RaycastAll(eye,delta.normalized,Mathf.Max(0,delta.magnitude-.12f),~LayerMask.GetMask("Water"),QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(actor.transform) && !hit.transform.IsChildOf(transform)) return false;
            return true;
        }
        static Vector3 LinePoint(LineRenderer line,int index) => line.useWorldSpace ? line.GetPosition(index) : line.transform.TransformPoint(line.GetPosition(index));
        static void ClosestOnLine(LineRenderer line,int limit,Vector3 center,ref float best,ref Vector3 result)
        {
            if (line == null || !line.enabled) return;
            int count = Mathf.Min(limit,line.positionCount);
            for (int i=1;i<count;i++)
            {
                var a = LinePoint(line,i-1); var arm = LinePoint(line,i)-a;
                var point = a+arm*Mathf.Clamp01(Vector3.Dot(center-a,arm)/Mathf.Max(.000001f,arm.sqrMagnitude));
                float distance = (point-center).sqrMagnitude;
                if (distance<best) { best = distance; result = point; }
            }
        }
        public bool AimRope(Ray ray,out float distance,out Vector3 point)
        {
            distance = float.PositiveInfinity; point = Station != null ? Station.Grip.position : transform.position;
            if (!KnotUntied)
            {
                var offset = point-ray.origin; distance = Vector3.Dot(offset,ray.direction);
                return distance>0 && distance<3.5f && (offset-ray.direction*distance).sqrMagnitude<.3f*.3f;
            }
            AimLine(TowRope,12,ray,ref distance,ref point);
            if (ropeTail != null) AimLine(ropeTail.Tail,int.MaxValue,ray,ref distance,ref point);
            return !float.IsInfinity(distance);
        }
        static void AimLine(LineRenderer line,int limit,Ray ray,ref float best,ref Vector3 result)
        {
            if (line == null || !line.enabled) return;
            int count = Mathf.Min(limit,line.positionCount);
            for (int i=1;i<count;i++)
            {
                var a = LinePoint(line,i-1); var arm = LinePoint(line,i)-a;
                float length = arm.sqrMagnitude; if (length<.000001f) continue;
                var offset = ray.origin-a;
                float parallel = Vector3.Dot(ray.direction,arm), denominator = length-parallel*parallel;
                float t = denominator>.000001f ? (Vector3.Dot(offset,arm)-parallel*Vector3.Dot(offset,ray.direction))/denominator
                    : Vector3.Dot(ray.origin+ray.direction*.1f-a,arm)/length;
                var point = a+arm*Mathf.Clamp01(t);
                float along = Vector3.Dot(point-ray.origin,ray.direction);
                if (along<.05f || along>3.5f || along>=best || (point-ray.GetPoint(along)).sqrMagnitude>.18f*.18f) continue;
                best = along; result = point;
            }
        }
        void UpdateFloorRope(NetworkWeapon actor,bool force = false)
        {
            if (!KnotUntied || Source == null || Station == null || !force && Time.unscaledTime<nextFloorUpdate) return;
            if (actor != null && !Source.CanReach(actor,stationIndex.Value,true,true)) return;
            nextFloorUpdate = Time.unscaledTime+.1f;
            var grip = actor != null ? GripPoint(actor.transform,hand.Value) : RestGrip;
            var drop = actor != null ? actor.transform.position+Source.transform.right*.25f+Source.transform.up*.025f : RestGrip;
            var local = Source.transform.InverseTransformPoint(drop);
            float extra = Vector3.Distance(grip,Station.Pulley.position)-Vector3.Distance(Station.Grip.position,Station.Outside.position);
            float available = Mathf.Max(.15f,2+SelectedRope-extra);
            float added = Mathf.Max(0,available-FloorRopeLength);
            if (floorDeposits.Count == 0 || Vector3.Distance(local,floorDeposits[floorDeposits.Count-1].Position)>.65f)
                floorDeposits.Add(new SternRopeFloorDeposit { Position = local });
            int last = floorDeposits.Count-1;
            var deposit = floorDeposits[last]; deposit.Length += added;
            if (added>.001f) floorDeposits[last] = deposit;
            floorRopeLength.Value = available;
            float route = Vector3.Distance(Source.transform.InverseTransformPoint(grip),floorDeposits[last].Position);
            for (int i=1;i<floorDeposits.Count;i++) route += Vector3.Distance(floorDeposits[i-1].Position,floorDeposits[i].Position);
            while (floorDeposits.Count>1 && (route>available+1.2f || floorDeposits.Count>64))
            {
                route -= Vector3.Distance(floorDeposits[0].Position,floorDeposits[1].Position);
                floorDeposits.RemoveAt(0);
            }
        }
        void ReleaseOperator()
        {
            if (Operator != null)
            {
                var actor = Operator.GetComponent<NetworkWeapon>();
                if (actor != null) UpdateFloorRope(actor,true);
                if (Source != null && KnotUntied && floorDeposits.Count>0) freeGripLocal.Value = floorDeposits[floorDeposits.Count-1].Position;
                if (Operator.TryGetComponent<AdvancedPlayerController>(out var motor)) motor.ShipActivityLocked = motor.CargoRopeWalking = false;
            }
            operatorObject.Value = null;
        }
        public void ReleaseTow()
        {
            if (!IsServerInitialized || Recovered) return;
            broken.Value = true; contested.Value = false; lootReady.Value = false;
            phase.Value = SternCargoPhase.Towing; poseSupport.Value = null; freeVerticalVelocity = 0; ReleaseOperator();
        }
        void PublishPose()
        {
            var support = poseSupport.Value != null ? poseSupport.Value.transform : null;
            pose.Value = new TowedCargoPose { Position = support != null ? support.InverseTransformPoint(transform.position) : transform.position,
                Rotation = support != null ? Quaternion.Inverse(support.rotation)*transform.rotation : transform.rotation };
        }
        void Update()
        {
            if (!IsSpawned) return;
            if (!IsServerInitialized) { ApplyRemotePose(); return; }
            if (spawnChest) SpawnChest();
            if (!Recovered)
            {
                if (IsChest && !spawnChest && (chest == null || !chest.IsSpawned)) { Complete(towingHook != null ? towingHook.Gun.NetworkShip : Source != null ? Source.Ship : null); return; }
                TickWork(Time.deltaTime); Simulate(Time.deltaTime);
                UpdateFloorRope(Operator != null ? Operator.GetComponent<NetworkWeapon>() : null);
            }
            else if (poseSupport.Value != null)
                Place(poseSupport.Value.transform.TransformPoint(pose.Value.Position),poseSupport.Value.transform.rotation*pose.Value.Rotation);
            if (Time.unscaledTime >= nextPublish) { nextPublish = Time.unscaledTime+.066f; PublishPose(); }
        }
        void ApplyRemotePose()
        {
            if (!poseReady || previousSupport != poseSupport.Value)
            { visualLocal = pose.Value.Position; visualRotation = pose.Value.Rotation; poseReady = true; previousSupport = poseSupport.Value; }
            float blend = 1-Mathf.Exp(-20*Time.deltaTime);
            visualLocal = Vector3.Lerp(visualLocal,pose.Value.Position,blend); visualRotation = Quaternion.Slerp(visualRotation,pose.Value.Rotation,blend);
            var support = poseSupport.Value != null ? poseSupport.Value.transform : null;
            Place(support != null ? support.TransformPoint(visualLocal) : visualLocal,support != null ? support.rotation*visualRotation : visualRotation);
        }
        void TickWork(float dt)
        {
            if (Source == null || Station == null) { ReleaseOperator(); return; }
            if (Phase != SternCargoPhase.Towing && Phase != SternCargoPhase.Landed)
            {
                float distance = Phase == SternCargoPhase.Hoisting ? Mathf.Abs(HoistLocal().y-liftStart.Value.y)
                    : Phase == SternCargoPhase.Crossing ? Vector3.Distance(Station.Outside.position,Station.Inside.position)
                    : Mathf.Abs(HoistLocal().y-Source.transform.InverseTransformPoint(Station.Landing.position).y);
                float previous = travel.Value;
                travel.Value = Mathf.MoveTowards(previous,Phase == SternCargoPhase.Hoisting ? liftDemand : 1,dt*HoistSpeed/Mathf.Max(.2f,distance));
                if (Phase != SternCargoPhase.Crossing) selectedRope.Value = Mathf.Max(0,SelectedRope+(travel.Value-previous)*distance*(Phase == SternCargoPhase.Lowering ? -1 : 1));
            }
            var actor = Operator != null ? Operator.GetComponent<NetworkWeapon>() : null;
            if (actor == null) return;
            if (Time.time>heartbeat || BlockReason(actor,true) != null) { ReleaseOperator(); return; }
            actor.GetComponent<AdvancedPlayerController>().CargoRopeWalking = WalkingPull;
            var local = Source.transform.InverseTransformPoint(actor.transform.position);
            if (WalkingPull && actor.GetComponent<AdvancedPlayerController>().PlanarSpeed>.15f)
            {
                float pull = Mathf.Min(Mathf.Max(0,local.z-lastWalkLocal.z),Mathf.Max(.15f,dt*6));
                float previous = towDistance.Value;
                towDistance.Value = Mathf.Max(.1f,previous-pull); selectedRope.Value += previous-towDistance.Value;
                hand.Value = new Vector2(0,.35f);
            }
            lastWalkLocal = local;
        }
        void MoveHands(Vector2 input, float dt)
        {
            if (!KnotUntied)
            {
                knot.Value = Mathf.Min(1,knot.Value+Mathf.Min(Mathf.Max(0,input.x),dt*4)/3.5f);
                hand.Value = new Vector2(Mathf.Sin(knot.Value*28),Mathf.Sin(knot.Value*41))*.12f;
                if (KnotUntied) hand.Value = Vector2.zero;
                return;
            }
            if (Phase != SternCargoPhase.Hoisting) return;
            float next = Mathf.Clamp01(hand.Value.y+Mathf.Clamp(input.y,0,1));
            float stroke = next-hand.Value.y; hand.Value = new Vector2(0,next);
            float distance = Mathf.Abs(HoistLocal().y-liftStart.Value.y);
            liftDemand = Mathf.Min(1,liftDemand+stroke*2.4f/Mathf.Max(.2f,distance));
        }
        public Vector3 GripPoint(Transform actor, Vector2 motion)
        {
            if (WalkingPull)
            {
                var towards = Vector3.ProjectOnPlane(Station.Grip.position-actor.position,Vector3.up).normalized;
                return actor.position+Vector3.up*1.23f+towards*.42f;
            }
            return actor.position+Vector3.up*(1.34f-motion.y*.18f)+actor.forward*(.57f-motion.y*.28f)+actor.right*motion.x*.14f;
        }
        Vector3 HoistLocal()
        {
            var point = Source.transform.InverseTransformPoint(Station.Outside.position); point.y -= EyeHeight+.25f; return point;
        }
        void Simulate(float dt)
        {
            var owner = Source; var slot = Station;
            if (!TowBroken && (owner == null || !owner.IsSpawned || owner.Ship.IsSinking)) ReleaseTow();
            towingHook = null; float strongest = float.NegativeInfinity;
            foreach (var hook in HarpoonProjectile.ActiveAttached)
            {
                if (hook == null || hook.CargoTarget != this || hook.Gun == null || hook.Gun.Muzzle == null || hook.Gun.NetworkShip == null || hook.Gun.NetworkShip.IsSinking) continue;
                if (!TowBroken && owner != null && hook.Gun.NetworkShip == owner.Ship) continue;
                float tension = Vector3.Distance(hook.Gun.Muzzle.position,hook.AttachedPoint)-hook.CurrentCableLength;
                if (tension>strongest) { towingHook = hook; strongest = tension; }
            }
            contested.Value = towingHook != null;
            if (!TowBroken && towingHook != null && strongest>.3f)
            { theft.Value = Mathf.Min(1,TheftProgress+dt/8); if (TheftProgress>=1) ReleaseTow(); }
            else theft.Value = Mathf.Max(0,TheftProgress-dt/5);
            var next = transform.position; var rotation = transform.rotation;
            if (!TowBroken && slot != null && phase.Value != SternCargoPhase.Towing)
            {
                poseSupport.Value = owner.NetworkObject;
                var high = HoistLocal(); var inside = owner.transform.InverseTransformPoint(slot.Inside.position); inside.y = high.y;
                var landing = owner.transform.InverseTransformPoint(slot.Landing.position);
                var local = phase.Value == SternCargoPhase.Hoisting ? Vector3.Lerp(liftStart.Value,high,travel.Value)
                    : phase.Value == SternCargoPhase.Crossing ? Vector3.Lerp(high,inside,travel.Value) : Vector3.Lerp(inside,landing,travel.Value);
                next = owner.transform.TransformPoint(local); rotation = owner.transform.rotation;
                Place(next,rotation);
                if (next.y+(IsChest?.52f:1.08f)>Water(next)-.12f) identified.Value = true;
                if (travel.Value >= 1)
                {
                    if (phase.Value == SternCargoPhase.Lowering) { FinishLanding(owner.Ship); return; }
                    phase.Value = phase.Value == SternCargoPhase.Hoisting ? SternCargoPhase.Crossing : SternCargoPhase.Lowering; travel.Value = 0;
                    hand.Value = Vector2.zero; ReleaseOperator();
                }
                return;
            }
            poseSupport.Value = null;
            if (!TowBroken && slot != null)
            {
                var arm = Vector3.ProjectOnPlane(next-slot.Outside.position,Vector3.up);
                float excess = Mathf.Max(0,arm.magnitude-towDistance.Value);
                next -= arm.normalized*Mathf.Min(excess,(8+excess*.4f)*dt);
                next.y = Water(next)-1.8f;
                
                if (towingHook != null && strongest>.3f) next += Vector3.ProjectOnPlane(towingHook.Gun.Muzzle.position-next,Vector3.up).normalized*3*dt;
                Place(next,FloatRotation());
                if (towDistance.Value<=.11f && Vector3.ProjectOnPlane(next-slot.Outside.position,Vector3.up).magnitude<.3f)
                { phase.Value = SternCargoPhase.Hoisting; travel.Value = liftDemand = 0; hand.Value = Vector2.zero; liftStart.Value = owner.transform.InverseTransformPoint(next); poseSupport.Value = owner.NetworkObject; }
                return;
            }
            float surface = Water(next)-.28f;
            if (next.y>surface+.02f) { freeVerticalVelocity -= 9.81f*dt; next.y=Mathf.Max(surface,next.y+freeVerticalVelocity*dt); }
            else { next.y=Mathf.MoveTowards(next.y,surface,.7f*dt); freeVerticalVelocity=0; }
            if (towingHook != null)
            {
                var muzzle = towingHook.Gun.Muzzle.position;
                var delta = muzzle-towingHook.AttachedPoint;
                float excess = Mathf.Max(0,delta.magnitude-towingHook.CurrentCableLength);
                next += delta.normalized*Mathf.Min(excess,(6+excess*.5f)*dt);
                if (excess>0) freeVerticalVelocity=0;
                if (towingHook.CurrentCableLength<3)
                    next.y = Mathf.Max(next.y,muzzle.y-EyeHeight-towingHook.CurrentCableLength);
                next = OutsideShip(next,towingHook.Gun.NetworkShip);
                if (Vector3.Distance(next,muzzle)<10) identified.Value = true;
                lootReady.Value = IsChest && towingHook.CurrentCableLength<=1.2f && Vector3.Distance(next+Vector3.up*EyeHeight,muzzle)<2;
            }
            else lootReady.Value = false;
            Place(next,FloatRotation());
        }
        Vector3 OutsideShip(Vector3 next, NetworkShip ship)
        {
            var delta = next-transform.position; float distance = delta.magnitude;
            if (distance<.0001f) return next;
            foreach (var hit in Physics.SphereCastAll(transform.position+Vector3.up*.5f,.42f,delta/distance,distance,~LayerMask.GetMask("Water"),QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<NetworkShip>() == ship && hit.distance<distance)
                {
                    var stop = transform.position+delta.normalized*Mathf.Max(0,hit.distance-.025f);
                    var slide = Vector3.ProjectOnPlane(next-stop,hit.normal);
                    return stop+slide;
                }
            return next;
        }
        float Water(Vector3 point) => OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) : Source != null ? Source.transform.position.y : 0;
        Quaternion FloatRotation() => Quaternion.Euler(Mathf.Sin(Time.time*1.8f+stationIndex.Value)*3, transform.eulerAngles.y, Mathf.Sin(Time.time*1.1f+stationIndex.Value)*4);
        void Place(Vector3 point, Quaternion rotation) { transform.SetPositionAndRotation(point,rotation); body.position = point; body.rotation = rotation; }
        void FinishLanding(NetworkShip target)
        {
            phase.Value = SternCargoPhase.Landed; poseSupport.Value = target.NetworkObject;
            if (IsChest && chest != null && chest.IsSpawned) chest.PlaceTowRewardOnDeck(target,ChestSocket.position,ChestSocket.rotation);
            PublishPose(); Complete(target);
        }
        public void ChestTaken(NetworkShip target) { if (IsServerInitialized && IsChest) Complete(target); }
        void Complete(NetworkShip target)
        {
            if (completionReported) return;
            completionReported = true; recovered.Value = true; lootReady.Value = false; ReleaseOperator();
            if (Source != null) Source.CargoRecovered(target,IsChest);
            for (int i = HarpoonProjectile.ActiveAttached.Count-1; i >= 0; i--)
                if (HarpoonProjectile.ActiveAttached[i] != null && HarpoonProjectile.ActiveAttached[i].CargoTarget == this) HarpoonProjectile.ActiveAttached[i].DetachAndRewind();
        }
        void LateUpdate()
        {
            if (BarrelVisual != null) BarrelVisual.SetActive(!IsChest);
            if (BarrelSling != null) BarrelSling.SetActive(!IsChest && !Recovered);
            if (ChestSling != null) ChestSling.SetActive(IsChest && !Recovered);
            var slot = Station; var owner = Source;
            if (slot != null)
            {
                float target = phase.Value == SternCargoPhase.Crossing ? travel.Value : phase.Value >= SternCargoPhase.Lowering ? 1 : 0;
                visualTravel = Mathf.Lerp(visualTravel,target,1-Mathf.Exp(-18*Time.deltaTime));
                slot.Pulley.position = Vector3.Lerp(slot.Outside.position,slot.Inside.position,visualTravel);
                if (slot.Knot != null) slot.Knot.gameObject.SetActive(!Recovered && !TowBroken && (knot.Value<1 || Operator == null));
            }
            if (TowRope == null) return;
            TowRope.enabled = IsSpawned && slot != null && owner != null && !TowBroken && !Recovered;
            if (slot == null || owner == null) return;
            var grip = KnotUntied ? RestGrip : slot.Grip.position;
            if (Operator != null)
            {
                var interaction = Operator.GetComponent<SternCargoPlayerInteraction>();
                grip = GripPoint(Operator.transform,interaction != null && interaction.HeldCargo == this ? interaction.HandMotion : hand.Value);
            }
            if (ropeTail != null) { ropeTail.FollowGrip(grip); grip = ropeTail.GripWorld; }
            if (!TowRope.enabled) return;
            var end = SlingEye.position;
            var guide = slot.Pulley.position;
            var back = Operator != null ? owner.transform.TransformPoint(new Vector3(owner.transform.InverseTransformPoint(slot.Grip.position).x,8.4f,-20.1f)) : slot.Grip.position-owner.transform.forward*.3f;
            int count = TowRope.positionCount;
            const int guideIndex = 12;
            for (int i=0;i<guideIndex;i++)
            {
                var point = Vector3.Lerp(grip,back,i/(float)(guideIndex-1));
                if (i>0 && Operator != null && ropeTail != null) point = ropeTail.AboveDeck(point,.055f);
                TowRope.SetPosition(i,point);
            }
            TowRope.SetPosition(guideIndex,guide);
            bool mixed = phase.Value == SternCargoPhase.Towing && towDistance.Value>8;
            float conceal = Mathf.InverseLerp(8,20,towDistance.Value);
            var middle = Vector3.Lerp(guide,end,.6f);
            if (mixed)
            {
                var knotPoint = owner.transform.TransformPoint(new Vector3(Mathf.Sin(stationIndex.Value*2.1f)*.35f,0,-30));
                knotPoint.y = Water(knotPoint)-1.1f;
                middle = Vector3.Lerp(middle,knotPoint,conceal);
                middle.y = Mathf.Min(middle.y,Water(middle)-1.1f);
            }
            for (int i=guideIndex+1;i<count;i++)
            {
                float t = (i-guideIndex)/(float)(count-guideIndex-1);
                var point = mixed ? t<.4f ? Vector3.Lerp(guide,middle,t/.4f) : Vector3.Lerp(middle,end,(t-.4f)/.6f) : Vector3.Lerp(guide,end,t);
                point.y -= Mathf.Sin(t*Mathf.PI)*(phase.Value == SternCargoPhase.Towing?.24f:.015f);
                if (mixed) point += owner.transform.right*(Mathf.Sin(t*Mathf.PI*4+stationIndex.Value)*.045f*Mathf.Sin(t*Mathf.PI)*conceal);
                float flow = ropeTail != null ? Mathf.Clamp01(ropeTail.Flow) : 0;
                point += owner.transform.right*(Mathf.Sin(Time.time*9-t*22)*.018f*Mathf.Sin(t*Mathf.PI)*flow);
                TowRope.SetPosition(i,point);
            }
        }
        public override void OnStopServer()
        {
            ReleaseOperator();
            if (chest != null && chest.IsSpawned && chest.InTow) { chest.ReleaseTowChest(); chest.ConfigureFloatingTowReward(); }
            base.OnStopServer();
        }
    }
}
