using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.Networking
{
    [Serializable]
    public sealed class RiggingBreakPart
    {
        public Transform Pivot;
        public float AtSeconds;
        public Vector3 Fall, Rotation;
        public GameObject[] Disable = Array.Empty<GameObject>();
        [NonSerialized] public Vector3 Rest;
        [NonSerialized] public Quaternion RestRotation;
    }

    [DefaultExecutionOrder(-90)]
    public sealed class NetworkRiggingEvent : NetworkBehaviour
    {
        public static readonly List<NetworkRiggingEvent> ServerEvents = new();
        public static readonly List<NetworkRiggingEvent> Active = new();
        public LootCatalog Catalog;
        public Transform ChestSocket, BrakeGrip, Pulley, LandingPoint, HangingCradle, BrakeRotor;
        public LineRenderer HoistRope;
        public RiggingBreakPart[] BreakParts = Array.Empty<RiggingBreakPart>();
        public ShipLadder[] Ladders = Array.Empty<ShipLadder>();
        public float CollapseSeconds = 110, LowerSeconds = 24, PlatformRadius = 29;
        public Vector3 ChestHigh, ChestLow;
        readonly SyncVar<Vector3> origin = new();
        readonly SyncVar<Quaternion> heading = new(Quaternion.identity);
        readonly SyncVar<double> started = new(-1);
        readonly SyncVar<float> stoppedAge = new(-1);
        readonly SyncVar<float> lowered = new();
        readonly SyncVar<bool> failed = new();
        readonly SyncVar<NetworkObject> operatorObject = new();
        NetworkLootChest chest;
        readonly Dictionary<GameObject,float> breakTimes = new();
        Rigidbody body;
        float heartbeat, nextBoardingScan, nextCreak;
        bool spawnChest, soundInitialized;
        int soundedStage;
        uint visualTick;
        float visualTickAt;
        AudioSource creak, crack;
        public double Clock => IsSpawned && TimeManager != null ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;
        public float VisualClock
        {
            get
            {
                if (!IsSpawned || TimeManager == null) return Time.time;
                uint tick = TimeManager.Tick;
                if (tick != visualTick) { visualTick = tick; visualTickAt = Time.unscaledTime; }
                return (float)TimeManager.TicksToTime(tick) + Mathf.Min(Time.unscaledTime - visualTickAt, (float)TimeManager.TickDelta);
            }
        }
        public float Age => started.Value < 0 ? 0 : stoppedAge.Value >= 0 ? stoppedAge.Value : Mathf.Clamp((float)(Clock - started.Value), 0, CollapseSeconds + 4);
        public float Remaining => Mathf.Max(0, CollapseSeconds - Age);
        public float Progress => lowered.Value;
        public bool Started => started.Value >= 0;
        public bool Failed => failed.Value;
        public bool ChestAvailable => lowered.Value >= 1 && !failed.Value;
        public NetworkObject Operator => operatorObject.Value;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            foreach (var part in BreakParts)
            {
                if (part.Pivot != null) { part.Rest = part.Pivot.localPosition; part.RestRotation = part.Pivot.localRotation; }
                foreach (var item in part.Disable)
                    if (item != null) breakTimes[item] = breakTimes.TryGetValue(item,out float at) ? Mathf.Min(at,part.AtSeconds) : part.AtSeconds;
            }
        }
        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public static NetworkRiggingEvent SpawnEvent(LootCatalog catalog, NetworkManager manager, Vector3 point, Scene scene)
        {
            if (catalog == null || catalog.RiggingEventPrefab == null) return null;
            var instance = Instantiate(catalog.RiggingEventPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            instance.Catalog = catalog;
            manager.ServerManager.Spawn(instance.NetworkObject);
            return instance;
        }
        public override void OnStartServer()
        {
            base.OnStartServer();
            origin.Value = transform.position; heading.Value = transform.rotation;
            started.Value = -1; stoppedAge.Value = -1; lowered.Value = 0; failed.Value = false;
            operatorObject.Value = null;
            ServerEvents.Add(this); spawnChest = true;
        }
        void SpawnChest()
        {
            spawnChest = false;
            if (Catalog == null || Catalog.ChestPrefab == null) return;
            chest = Instantiate(Catalog.ChestPrefab, transform.TransformPoint(ChestHigh), transform.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = Catalog;
            var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ 0xb947ea21u));
            chest.Fill(ref random); chest.PlaceInRigging(this);
            ServerManager.Spawn(chest.NetworkObject);
        }
        public string BlockReason(NetworkWeapon actor)
        {
            if (actor == null || !actor.IsSpawned || !IsSpawned || failed.Value) return "Такелаж обрушился — сундук разбит";
            if (ChestAvailable) return "Сундук опущен на площадку";
            var motor = actor.GetComponent<AdvancedPlayerController>();
            if (motor == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsClimbing || motor.IsSwimming) return "Встаньте на рею рядом с тормозом";
            if (!motor.IsGrounded) return "Для спуска нужна опора под ногами";
            if (actor.LootHandsBusy || actor.GetComponent<CannonHands>()?.HasHeldBall == true || actor.GetComponent<PlayerInventory>()?.HandsOccupied == true) return "Освободите руки";
            if (!CanReach(actor)) return "Подойдите к тормозу на конце реи";
            if (operatorObject.Value != null && operatorObject.Value != actor.NetworkObject) return "Тормоз уже держит другой игрок";
            return null;
        }
        public bool CanReach(NetworkWeapon actor)
        {
            if (BrakeGrip == null || actor == null) return false;
            var eye = actor.transform.position + Vector3.up * 1.5f;
            var delta = BrakeGrip.position - eye;
            if (delta.sqrMagnitude > 3.2f * 3.2f) return false;
            foreach (var hit in Physics.RaycastAll(eye, delta.normalized, Mathf.Max(0, delta.magnitude - .18f), ~LayerMask.GetMask("Water"), QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(actor.transform) && !hit.transform.IsChildOf(BrakeGrip.parent)) return false;
            return true;
        }
        public bool InputBrake(NetworkWeapon actor, bool held, out string reason)
        {
            reason = null;
            if (!held)
            {
                if (actor != null && operatorObject.Value == actor.NetworkObject) operatorObject.Value = null;
                return true;
            }
            reason = BlockReason(actor);
            if (reason != null) return false;
            if (!Started) started.Value = Clock;
            operatorObject.Value = actor.NetworkObject; heartbeat = Time.time + .35f;
            return true;
        }
        void Update()
        {
            if (!IsSpawned) return;
            transform.SetPositionAndRotation(origin.Value, heading.Value);
            if (!IsServerInitialized) return;
            if (spawnChest) SpawnChest();
            if (!Started && Time.time >= nextBoardingScan)
            {
                nextBoardingScan = Time.time + .2f;
                foreach (var passenger in ShipDeckPassenger.Active)
                {
                    if (passenger == null || passenger.Support != body) continue;
                    var actor = passenger.GetComponent<NetworkWeapon>();
                    if (actor != null && actor.IsSpawned && !actor.GetComponent<AdvancedPlayerController>().IsDead) { started.Value = Clock; break; }
                }
            }
            if (!Started || ChestAvailable || failed.Value) return;
            if (Age >= CollapseSeconds)
            {
                failed.Value = true; operatorObject.Value = null;
                if (chest != null && chest.IsSpawned && chest.InRigging) ServerManager.Despawn(chest.NetworkObject);
                return;
            }
            var worker = operatorObject.Value != null ? operatorObject.Value.GetComponent<NetworkWeapon>() : null;
            if (worker == null || Time.time > heartbeat || BlockReason(worker) != null) { operatorObject.Value = null; return; }
            lowered.Value = Mathf.Min(1, lowered.Value + Time.deltaTime / Mathf.Max(1, LowerSeconds));
            if (ChestAvailable) { stoppedAge.Value = Age; operatorObject.Value = null; }
        }
        void LateUpdate()
        {
            if (!IsSpawned) return;
            float age = Age;
            foreach (var part in BreakParts)
            {
                if (part.Pivot == null) continue;
                float fall = Mathf.Clamp01((age - part.AtSeconds) / 3f);
                float warning = Started && !ChestAvailable && !Failed ? Mathf.Clamp01((age - part.AtSeconds + 7) / 7) * (1 - fall) : 0;
                part.Pivot.localPosition = part.Rest + part.Fall * fall * fall;
                part.Pivot.localRotation = part.RestRotation * Quaternion.Euler(part.Rotation * Mathf.SmoothStep(0, 1, fall) + new Vector3(0, 0, Mathf.Sin((float)Clock * 9) * warning * 1.8f));
            }
            foreach (var entry in breakTimes)
                if (entry.Key != null && entry.Key.activeSelf != (age < entry.Value)) entry.Key.SetActive(age < entry.Value);
            foreach (var ladder in Ladders) if (ladder != null && Failed) ladder.enabled = false;
            var point = Vector3.Lerp(ChestHigh, ChestLow, lowered.Value);
            float swing = (1 - lowered.Value) * .25f;
            point += new Vector3(Mathf.Sin((float)Clock * 1.3f) * swing, 0, Mathf.Sin((float)Clock * .87f) * swing * .5f);
            ChestSocket.localPosition = point;
            ChestSocket.localRotation = Quaternion.Euler(0, 0, Mathf.Sin((float)Clock * 1.3f) * swing * 8);
            if (HangingCradle != null)
            {
                HangingCradle.SetPositionAndRotation(ChestSocket.position, ChestSocket.rotation);
                HangingCradle.gameObject.SetActive(!Failed && !ChestAvailable);
            }
            if (HoistRope != null)
            {
                HoistRope.enabled = !Failed && !ChestAvailable;
                HoistRope.SetPosition(0, BrakeGrip.position);
                HoistRope.SetPosition(1, Pulley.position);
                HoistRope.SetPosition(2, ChestSocket.TransformPoint(new Vector3(0, 1.05f, 0)));
            }
            if (BrakeRotor != null) BrakeRotor.localRotation = Quaternion.Euler(0, lowered.Value * -2160, 0);
            if (!IsClientInitialized) return;
            int stage = 0;
            foreach (var part in BreakParts) if (age >= part.AtSeconds) stage++;
            if (soundInitialized && stage > soundedStage) GameAudio.Attached(ref crack, SoundCue.BarricadeBreak, transform);
            soundedStage = stage; soundInitialized = true;
            if (Started && !ChestAvailable && !Failed && Time.time >= nextCreak)
            { nextCreak = Time.time + Mathf.Lerp(8, 2.5f, age / CollapseSeconds); GameAudio.Attached(ref creak, SoundCue.Creak, BrakeGrip); }
        }
        public string Hint => Failed ? "Мачта обрушилась — сундук потерян" : ChestAvailable ? "Сундук на площадке · E — открыть · F — нести"
            : Started ? $"Сундук в такелаже · до обвала {Mathf.CeilToInt(Remaining)} с · спущен {Progress:P0}"
            : "Сундук в такелаже · F — подняться по сетке · найдите тормоз на конце реи";
        public override void OnStopServer()
        {
            ServerEvents.Remove(this);
            if (chest != null && chest.IsSpawned && chest.InRigging) ServerManager.Despawn(chest.NetworkObject);
            base.OnStopServer();
        }
    }
}
