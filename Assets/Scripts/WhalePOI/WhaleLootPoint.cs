using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.Networking;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop
{
    public enum WhalePOIState { Idle, Agitated, Diving, Cleared }
    public struct WhaleHarpoonWork
    {
        public NetworkObject Actor;
        public float Rock, Pull;
        public int Strokes, LastSide;
        public double Heartbeat, RemovedAt;
        public Vector3 ReleasePoint;
        public Quaternion ReleaseRotation;
        public bool Removed;
    }

    [DefaultExecutionOrder(-100)]
    public sealed class WhaleLootPoint : NetworkBehaviour
    {
        static readonly List<WhaleLootPoint> active = new();
        public static IReadOnlyList<WhaleLootPoint> Active => active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActive() => active.Clear();
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);
        public static WhaleHarpoonPin WorkingPin(NetworkObject actor)
        {
            if (actor == null) return null;
            foreach (var whale in active)
                for (int i = 0; i < whale.work.Count; i++)
                    if (whale.work[i].Actor == actor) return whale.Harpoons[i];
            return null;
        }
        public Transform WhaleModel, ChestSocket;
        public Transform[] TailBones, FlipperBones;
        public List<WhaleHarpoonPin> Harpoons = new();
        public WhaleHarpoonRope[] Ropes;
        public LootCatalog Catalog;
        public float ChallengeDuration = 75f;
        public float SurfaceOffset = -5.4f;
        readonly SyncVar<Vector3> origin = new();
        readonly SyncVar<Quaternion> heading = new(Quaternion.identity);
        readonly SyncVar<double> startedAt = new(-1);
        readonly SyncList<WhaleHarpoonWork> work = new();
        NetworkLootChest chest;
        bool spawnChest;
        Quaternion[] tailRest, flipperRest;
        float lastThrash;
        public double Clock => TimeManager != null && IsSpawned ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;
        public float Age => startedAt.Value < 0 ? 0 : Mathf.Max(0, (float)(Clock - startedAt.Value));
        public WhalePOIState State => startedAt.Value < 0 ? WhalePOIState.Idle : Age >= ChallengeDuration ? WhalePOIState.Diving : WhalePOIState.Agitated;
        public float RemainingTime => Mathf.Max(0, ChallengeDuration - Age);
        public int RemainingHarpoons { get { int n = 0; foreach (var pin in Harpoons) if (!pin.IsRemoved) n++; return n; } }
        public int FreedPairs { get { int n = 0; foreach (var rope in Ropes) if (rope.A.IsRemoved || rope.B.IsRemoved) n++; return n; } }
        public bool ChestUnlocked => FreedPairs >= 3;
        public bool ChestAvailable => ChestUnlocked && State != WhalePOIState.Diving;
        public float SeaLevel => origin.Value.y - SurfaceOffset;
        public WhaleHarpoonWork Work(int index) => index >= 0 && index < work.Count ? work[index] : default;

        void Awake()
        {
            tailRest = new Quaternion[TailBones.Length]; flipperRest = new Quaternion[FlipperBones.Length];
            for (int i = 0; i < TailBones.Length; i++) tailRest[i] = TailBones[i].localRotation;
            for (int i = 0; i < FlipperBones.Length; i++) flipperRest[i] = FlipperBones[i].localRotation;
        }
        public override void OnStartServer()
        {
            base.OnStartServer();
            origin.Value = transform.position; heading.Value = transform.rotation;
            work.Clear(); startedAt.Value = -1;
            for (int i = 0; i < Harpoons.Count; i++) work.Add(default);
            spawnChest = true;
        }
        void SpawnChest()
        {
            spawnChest = false;
            chest = Instantiate(Catalog.ChestPrefab, ChestSocket.position, ChestSocket.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = Catalog;
            var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ 0xced31a73u));
            chest.Fill(ref random); chest.PlaceOnWhale(this); ServerManager.Spawn(chest.NetworkObject);
        }
        public string HarpoonBlockReason(NetworkWeapon actor, int index)
        {
            if (actor == null || !actor.IsSpawned || !IsSpawned || index < 0 || index >= Harpoons.Count) return "Гарпун больше недоступен";
            var s = Work(index);
            if (s.Removed) return "Этот гарпун уже извлечён";
            if (State == WhalePOIState.Diving) return "Кит погружается — прыгай в воду!";
            var motor = actor.GetComponent<AdvancedPlayerController>();
            if (motor == null || motor.IsDead || motor.IsDowned) return "Сейчас нельзя схватить гарпун";
            if (motor.IsFrozen || motor.IsKnockedBack) return "Дождись восстановления управления";
            if (motor.IsClimbing) return "Сначала закончи подъём на кита";
            if (motor.LocomotionLocked) return "Заверши текущее действие, чтобы схватить гарпун";
            if (actor.WorkingLoot != null || actor.CarriedLoot != null || actor.GetComponent<CannonHands>()?.HasHeldBall == true) return "Освободи руки, чтобы схватить гарпун";
            var fishing = actor.GetComponent<PlayerInventory>()?.Fishing;
            if (fishing != null && (fishing.CarryingCatch || fishing.IsFishing || fishing.IsEating)) return "Заверши действие с рыбой и освободи руки";
            if (s.Actor != null && s.Actor != actor.NetworkObject && Clock - s.Heartbeat < .6) return "Гарпун держит другой игрок";
            var heldPin = WorkingPin(actor.NetworkObject);
            if (heldPin != null && heldPin != Harpoons[index]) return "Сначала отпусти другой гарпун";
            return Harpoons[index].CanReach(actor, out var reason) ? null : reason;
        }
        public bool InputHarpoon(NetworkWeapon actor, int index, bool held, float rockDelta, float pullDelta) => InputHarpoon(actor, index, held, rockDelta, pullDelta, out _);
        public bool InputHarpoon(NetworkWeapon actor, int index, bool held, float rockDelta, float pullDelta, out string reason)
        {
            reason = "Гарпун больше недоступен";
            if (!IsServerInitialized || actor == null || index < 0 || index >= work.Count) return false;
            var s = work[index];
            if (!held) { if (s.Actor == actor.NetworkObject) { s.Actor = null; work[index] = s; } reason = null; return true; }
            reason = HarpoonBlockReason(actor, index);
            if (reason != null) return false;
            if (s.Actor != actor.NetworkObject) { s.Actor = actor.NetworkObject; s.Heartbeat = Clock; work[index] = s; if (startedAt.Value < 0) startedAt.Value = Clock; return true; }
            if (Clock - s.Heartbeat < .018) return true;
            s.Heartbeat = Clock;
            if (s.Strokes < 6)
            {
                s.Rock = Mathf.Clamp(s.Rock + Mathf.Clamp(rockDelta, -.65f, .65f), -1, 1);
                int side = s.Rock > .7f ? 1 : s.Rock < -.7f ? -1 : 0;
                if (side != 0 && side != s.LastSide) { if (s.LastSide != 0) s.Strokes++; s.LastSide = side; }
            }
            else
            {
                s.Pull = Mathf.Clamp01(s.Pull + Mathf.Clamp(pullDelta, -.18f, .18f));
                s.Rock = Mathf.MoveTowards(s.Rock, 0, .12f);
                if (s.Pull >= 1)
                {
                    s.Removed = true; s.RemovedAt = Clock; s.Actor = null;
                    s.ReleasePoint = Harpoons[index].EmbeddedPoint + Harpoons[index].Outward * 1.26f;
                    s.ReleaseRotation = Quaternion.LookRotation(Harpoons[index].Outward, transform.up);
                }
            }
            work[index] = s;
            return true;
        }
        void Update()
        {
            if (!IsSpawned) return;
            if (IsServerInitialized && spawnChest) SpawnChest();
            float age = Age, clock = (float)Clock, urgency = Mathf.Clamp01(age / ChallengeDuration), cycle = age % 4.8f;
            float thrash = State == WhalePOIState.Agitated && cycle > 2.5f ? Mathf.Sin((cycle - 2.5f) / 2.3f * Mathf.PI) : 0;
            float side = ((int)(age / 4.8f) & 1) == 0 ? 1 : -1;
            Vector3 position = origin.Value + Vector3.up * (Mathf.Sin(clock * .85f) * .15f + thrash * .45f);
            if (OceanSurface.Instance != null) position.y += OceanSurface.Instance.Height(origin.Value) - SeaLevel;
            Quaternion rotation = heading.Value * Quaternion.Euler(-thrash * (3 + urgency * 5), side * thrash * (8 + urgency * 12), Mathf.Sin(clock * .6f) * 1.3f + side * thrash * (12 + urgency * 14));
            if (State == WhalePOIState.Diving)
            {
                float d = age - ChallengeDuration, ease = Mathf.SmoothStep(0, 1, d / 2f);
                position += heading.Value * Vector3.forward * (d * d * 1.1f) + Vector3.down * (d * d * .75f);
                rotation = heading.Value * Quaternion.Euler(ease * 32, Mathf.Sin(d * .6f) * 4, 0);
            }
            transform.SetPositionAndRotation(position, rotation);
            float swim = State == WhalePOIState.Diving ? 2.8f : State == WhalePOIState.Agitated ? 1.8f : .8f;
            for (int i = 0; i < TailBones.Length; i++) TailBones[i].localRotation = tailRest[i] * Quaternion.Euler(Mathf.Sin(clock * swim - i * .45f) * (4 + i * 2 + thrash * 6), Mathf.Sin(clock * swim * .6f - i * .4f) * 2, 0);
            for (int i = 0; i < FlipperBones.Length; i++) FlipperBones[i].localRotation = flipperRest[i] * Quaternion.Euler(Mathf.Sin(clock * swim + i) * 5, 0, (i == 0 ? -1 : 1) * (Mathf.Sin(clock * swim) * 4 + thrash * 10));
            if (thrash > .8f && lastThrash <= .8f) GameAudio.Play(SoundCue.WaterSplash, transform.position);
            lastThrash = thrash;
            if (!IsServerInitialized) return;
            for (int i = 0; i < work.Count; i++) { var s = work[i]; if (s.Actor != null && (Clock - s.Heartbeat > .6 || State == WhalePOIState.Diving)) { s.Actor = null; work[i] = s; } }
            if (State == WhalePOIState.Diving && age > ChallengeDuration + 9) ServerManager.Despawn(NetworkObject);
        }
        public Vector3 SlipVelocity(bool braced)
        {
            if (State == WhalePOIState.Idle) return Vector3.zero;
            return Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(Vector3.down, transform.up), Vector3.up) * (braced ? 3 : 13);
        }
        public override void OnStopServer()
        {
            if (chest != null && chest.IsSpawned && chest.OnWhale && chest.Whale==this) ServerManager.Despawn(chest.NetworkObject);
            base.OnStopServer();
        }
    }
}

