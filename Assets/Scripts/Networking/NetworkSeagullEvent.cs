using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-80)]
    public sealed class NetworkSeagullEvent : NetworkBehaviour
    {
        public const int BirdCount = 28;
        public static readonly List<NetworkSeagullEvent> ServerEvents = new();
        public static readonly List<NetworkSeagullEvent> Active = new();
        public LootCatalog Catalog;
        public Transform ChestSocket;
        public GameObject KeyPrefab, PadlockPrefab;
        public float PlatformRadius = 30;
        readonly SyncVar<Vector3> origin = new();
        readonly SyncVar<Quaternion> heading = new(Quaternion.identity);
        readonly SyncVar<double> started = new();
        readonly SyncVar<double> unlockedAt = new(-1);
        readonly SyncVar<Vector3> fallenLockPoint = new();
        readonly SyncVar<Quaternion> fallenLockFacing = new(Quaternion.identity);
        readonly SyncVar<int> keyBird = new();
        readonly SyncVar<uint> shotMask = new();
        readonly SyncList<SeagullFall> falls = new();
        readonly SyncVar<bool> dropped = new(), unlocked = new(), onWater = new(), keyHeld = new();
        readonly SyncVar<Vector3> keyPoint = new(), keyLocalPoint = new();
        readonly SyncVar<Quaternion> keyFacing = new(Quaternion.identity);
        readonly SyncVar<NetworkObject> keySupport = new(), keyHolder = new();
        readonly SeagullVisual[] birds = new SeagullVisual[BirdCount];
        readonly double[] shotAt = new double[BirdCount];
        GameObject keyVisual;
        Collider keyCollider;
        Transform padlock, lockShackle;
        Vector3 shackleRest;
        Quaternion shackleFacing;
        bool lockImpactPlayed;
        float lockRattleAt = -10;
        NetworkLootChest chest;
        Vector3 lastHolderPoint, lastKeyPoint;
        bool spawnChest, hadHolder, hadSupport;
        float nextFallSync, nextCall;
        uint visualTick;
        float visualTickAt;
        public bool Unlocked => unlocked.Value;
        public bool ChestAvailable => Unlocked && Clock - unlockedAt.Value >= 1.05;
        public float LockReleaseAge => !Unlocked ? 0 : Mathf.Max(0, FlightTime - (float)(unlockedAt.Value - started.Value));
        public Vector3 FallenLockPoint => transform.TransformPoint(fallenLockPoint.Value);
        public Quaternion FallenLockFacing => transform.rotation * fallenLockFacing.Value;
        public SeagullVisual BirdAt(int index) => index >= 0 && index < BirdCount ? birds[index] : null;
        public int IndexOf(SeagullVisual bird) => System.Array.IndexOf(birds, bird);
        public bool KeyDropped => dropped.Value && !Unlocked && !keyHeld.Value;
        public NetworkObject KeyHolder => keyHolder.Value;
        public Vector3 KeyPosition => keyVisual != null ? keyVisual.transform.position : keyPoint.Value;
        double Clock => IsSpawned && TimeManager != null ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;
        float FlightTime
        {
            get
            {
                if (TimeManager == null) return Time.time;
                uint tick = TimeManager.Tick;
                if (tick != visualTick) { visualTick = tick; visualTickAt = Time.unscaledTime; }
                return (float)(TimeManager.TicksToTime(tick) - started.Value) + Mathf.Min(Time.unscaledTime - visualTickAt, (float)TimeManager.TickDelta);
            }
        }

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public static NetworkSeagullEvent SpawnEvent(LootCatalog catalog, NetworkManager manager, Vector3 point, Scene scene)
        {
            if (catalog == null || catalog.SeagullEventPrefab == null) return null;
            var instance = Instantiate(catalog.SeagullEventPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            instance.Catalog = catalog;
            manager.ServerManager.Spawn(instance.NetworkObject);
            return instance;
        }
        public override void OnStartServer()
        {
            base.OnStartServer();
            origin.Value = transform.position; heading.Value = transform.rotation; started.Value = Clock;
            keyBird.Value = Random.Range(0, BirdCount);
            shotMask.Value = 0; falls.Clear();
            for (int i = 0; i < BirdCount; i++) falls.Add(default);
            dropped.Value = unlocked.Value = onWater.Value = keyHeld.Value = false;
            unlockedAt.Value = -1;
            keyHolder.Value = keySupport.Value = null;
            ServerEvents.Add(this); spawnChest = true;
        }
        void SpawnChest()
        {
            spawnChest = false;
            if (Catalog == null || Catalog.ChestPrefab == null || ChestSocket == null) return;
            chest = Instantiate(Catalog.ChestPrefab, ChestSocket.position, ChestSocket.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = Catalog;
            var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ 0x7eaf331bu));
            chest.Fill(ref random); chest.PlaceInSeagullWreck(this);
            ServerManager.Spawn(chest.NetworkObject);
        }
        void CreateFlock()
        {
            if (birds[0] != null) return;
            for (int i = 0; i < BirdCount; i++)
            {
                int index = i;
                var bird = birds[i] = SeagullVisual.Create(transform, i * 113 + NetworkObject.ObjectId);
                bird.name = "WreckSeagull_" + i;
                bird.FirearmHit = direction => ShootBird(index, direction);
                bird.SurfaceContact = fall => LandBird(index, fall);
            }
            keyVisual = Instantiate(KeyPrefab, transform, false);
            keyVisual.name = "SeagullChestKey";
            keyCollider = keyVisual.GetComponent<Collider>();
            keyVisual.AddComponent<SeagullKeyTarget>().Event = this;
            nextCall = Time.time + Random.Range(12f, 20f);
        }
        Vector3 BirdPoint(int index, float time)
        {
            float seed = index * 2.399963f + NetworkObject.ObjectId * .17f;
            float speed = (.27f + index % 7 * .029f) * (index % 3 == 0 ? -1 : 1);
            float angle = seed + time * speed + Mathf.Sin(time * .19f + seed) * .27f;
            float radius = 9f + index % 8 * 1.65f + Mathf.Sin(time * .33f + seed) * 2.4f;
            return origin.Value + heading.Value * new Vector3(Mathf.Cos(angle) * radius,
                13f + index % 6 * 1.5f + Mathf.Sin(time * .48f + seed) * 1.1f,
                Mathf.Sin(angle) * radius * (.72f + index % 4 * .09f));
        }
        void ShootBird(int index, Vector3 direction)
        {
            if (!IsServerInitialized || !IsSpawned || (shotMask.Value & (1u << index)) != 0) return;
            float time = FlightTime;
            var bird = birds[index];
            var state = bird.CaptureFall((BirdPoint(index, time + .04f) - BirdPoint(index, time)) / .04f, direction);
            falls[index] = state; shotAt[index] = Clock;
            shotMask.Value |= 1u << index;
            bird.BeginFall(state);
        }
        void LandBird(int index, SeagullFall fall)
        {
            if (!IsServerInitialized || !IsSpawned || !fall.Landed || falls[index].Landed) return;
            fall.Age = (float)(Clock - shotAt[index]); falls[index] = fall;
            if (index != keyBird.Value || dropped.Value || Unlocked) return;
            keyPoint.Value = fall.ContactPosition - fall.ContactNormal * .115f;
            keySupport.Value = fall.Support;
            keyLocalPoint.Value = fall.Support != null ? fall.Support.transform.InverseTransformPoint(keyPoint.Value) : Vector3.zero;
            var rotation = Quaternion.FromToRotation(Vector3.up, fall.ContactNormal) * Quaternion.Euler(90, fall.Rotation.eulerAngles.y, 0);
            keyFacing.Value = fall.Support != null ? Quaternion.Inverse(fall.Support.transform.rotation) * rotation : rotation;
            onWater.Value = fall.OnWater; dropped.Value = true;
            hadSupport = keySupport.Value != null;
            UpdateKey();
        }
        public void PickupKey(NetworkWeapon actor)
        {
            if (!IsServerInitialized || !IsSpawned || !KeyDropped || actor == null || !actor.IsSpawned) return;
            var motor = actor.GetComponent<AdvancedPlayerController>();
            var inventory = actor.GetComponent<PlayerInventory>();
            if (motor == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsClimbing || motor.LocomotionLocked || actor.LootHandsBusy
                || inventory.HandsOccupied || actor.GetComponent<CannonHands>().HasHeldBall) return;
            if (inventory.Fishing != null && (inventory.Fishing.CarryingCatch || inventory.Fishing.IsFishing || inventory.Fishing.IsEating)) return;
            UpdateKey();
            if (!actor.CanReach(KeyPosition, keyVisual.transform)) return;
            keyHolder.Value = actor.NetworkObject; keySupport.Value = null; keyHeld.Value = true; hadHolder = true; hadSupport = false;
            lastHolderPoint = actor.transform.position + Vector3.up * .55f;
            KeyPickupRpc(KeyPosition);
        }
        public bool Unlock(NetworkWeapon actor)
        {
            if (!IsServerInitialized || actor == null || !actor.IsSpawned) return false;
            if (Unlocked) return true;
            if (keyHolder.Value != actor.NetworkObject) return false;
            var mount = ChestSocket.TransformPoint(new Vector3(0, .335f, .337f));
            var rayPoint = mount + ChestSocket.forward * .25f;
            var point = rayPoint; point.y = ChestSocket.position.y + .055f;
            var normal = Vector3.up;
            float nearest = 6f;
            foreach (var hit in Physics.RaycastAll(rayPoint + Vector3.up * .5f, Vector3.down, nearest, ~LayerMask.GetMask("Water"), QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.collider.GetComponentInParent<NetworkLootChest>() != null) continue;
                nearest = hit.distance; point = hit.point + hit.normal * .055f; normal = hit.normal;
            }
            fallenLockPoint.Value = transform.InverseTransformPoint(point);
            fallenLockFacing.Value = Quaternion.Inverse(transform.rotation) * Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(-90, ChestSocket.eulerAngles.y + 24, 0);
            unlockedAt.Value = Clock;
            unlocked.Value = true; keyHolder.Value = keySupport.Value = null; keyHeld.Value = false; hadHolder = hadSupport = false;
            return true;
        }
        public void RattleLock() => lockRattleAt = Time.time;
        void UpdatePadlock()
        {
            if (ChestSocket == null || PadlockPrefab == null) return;
            var mount = ChestSocket.TransformPoint(new Vector3(0, .335f, .337f));
            var landing = FallenLockPoint;
            float age = LockReleaseAge;
            float fallSeconds = Mathf.Max(.12f, Mathf.Sqrt(2f * Mathf.Max(.05f, mount.y - landing.y) / 9.81f));
            if (padlock == null)
            {
                padlock = Instantiate(PadlockPrefab, ChestSocket, false).transform;
                lockShackle = padlock.Find("IronShackle");
                if (lockShackle != null) { shackleRest = lockShackle.localPosition; shackleFacing = lockShackle.localRotation; }
                lockImpactPlayed = Unlocked && age >= .4f + fallSeconds;
            }
            float release = Unlocked ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .4f)) : 0;
            if (lockShackle != null)
            {
                lockShackle.localPosition = shackleRest + Vector3.up * (.045f * release);
                float settledAngle = Mathf.Lerp(75f, 45f, Mathf.Clamp01((age - .4f) / fallSeconds));
                lockShackle.localRotation = shackleFacing * Quaternion.Euler(0, settledAngle * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, .4f, age)), 0);
            }
            if (!Unlocked || age < .4f)
            {
                float rattleAge = Time.time - lockRattleAt;
                float shake = !Unlocked && rattleAge < .55f ? Mathf.Sin(rattleAge * 53f) * Mathf.Exp(-rattleAge * 6f) : 0;
                padlock.SetPositionAndRotation(mount + ChestSocket.up * (shake * .009f), ChestSocket.rotation * Quaternion.Euler(0, 0, shake * 13f));
                return;
            }
            if (padlock.parent != transform) padlock.SetParent(transform, true);
            float progress = Mathf.Clamp01((age - .4f) / fallSeconds);
            var point = Vector3.Lerp(mount, landing, progress);
            point.y = Mathf.Lerp(mount.y, landing.y, progress * progress);
            float settled = Mathf.Max(0, age - .4f - fallSeconds);
            if (settled > 0) point += (FallenLockFacing * Vector3.forward) * (Mathf.Abs(Mathf.Sin(settled * 19f)) * .012f * Mathf.Exp(-settled * 15f));
            padlock.SetPositionAndRotation(point, Quaternion.Slerp(ChestSocket.rotation, FallenLockFacing, Mathf.SmoothStep(0, 1, progress)));
            if (!lockImpactPlayed && progress >= 1)
            {
                lockImpactPlayed = true;
                if (IsClientInitialized) GameAudio.Play(SoundCue.BulletMetal, landing, .3f);
            }
        }
        [ObserversRpc(RunLocally = true)]
        void KeyPickupRpc(Vector3 point) { if (IsClientInitialized) GameAudio.Play(SoundCue.Pickup, point); }
        void DropHeldKey()
        {
            var holder = keyHolder.Value;
            Vector3 point = holder != null ? holder.transform.position + Vector3.up * .55f : lastHolderPoint;
            keyHolder.Value = null; keySupport.Value = null; keyHeld.Value = false; onWater.Value = true;
            float nearest = 4;
            RaycastHit floor = default;
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * .5f, Vector3.down, 4, ~LayerMask.GetMask("Water"), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.collider == keyCollider || hit.distance >= nearest) continue;
                nearest = hit.distance; floor = hit;
            }
            if (floor.collider != null && (OceanSurface.Instance == null || floor.point.y > OceanSurface.Instance.Height(floor.point)))
            {
                point = floor.point + floor.normal * .025f;
                keySupport.Value = floor.collider.GetComponentInParent<NetworkObject>();
                var raft = floor.collider.GetComponentInParent<RaftPlatform>();
                if (keySupport.Value == null && raft != null && raft.Chest != null) keySupport.Value = raft.Chest.NetworkObject;
                onWater.Value = false;
                var rotation = Quaternion.FromToRotation(Vector3.up, floor.normal) * Quaternion.Euler(90, 0, 0);
                keyFacing.Value = keySupport.Value != null ? Quaternion.Inverse(keySupport.Value.transform.rotation) * rotation : rotation;
                if (keySupport.Value != null) keyLocalPoint.Value = keySupport.Value.transform.InverseTransformPoint(point);
            }
            keyPoint.Value = point; dropped.Value = true;
            hadHolder = false; hadSupport = keySupport.Value != null;
        }
        void UpdateKey()
        {
            if (keyVisual == null) return;
            keyVisual.SetActive(!Unlocked && (!keyHeld.Value || keyHolder.Value != null));
            keyCollider.enabled = KeyDropped;
            if (Unlocked) return;
            var target = keyVisual.transform;
            if (keyHolder.Value != null)
            {
                var holder = keyHolder.Value.transform;
                lastHolderPoint = holder.position + Vector3.up * .55f;
                target.SetPositionAndRotation(holder.TransformPoint(new Vector3(.27f, .7f, -.13f)), holder.rotation * Quaternion.Euler(0, 0, 12));
            }
            else if (!dropped.Value)
            {
                var bird = birds[Mathf.Clamp(keyBird.Value, 0, BirdCount - 1)].transform;
                target.SetPositionAndRotation(bird.TransformPoint(new Vector3(0, -.15f, .24f)), bird.rotation * Quaternion.Euler(0, 0, Mathf.Sin(FlightTime * 4) * 14));
            }
            else
            {
                Vector3 point = keySupport.Value != null ? keySupport.Value.transform.TransformPoint(keyLocalPoint.Value) : keyPoint.Value;
                if (onWater.Value) point.y = (OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) : origin.Value.y) + .08f;
                target.SetPositionAndRotation(point, onWater.Value ? Quaternion.Euler(87 + Mathf.Sin(FlightTime * 1.8f) * 3, FlightTime * 17, 0) :
                    (keySupport.Value != null ? keySupport.Value.transform.rotation : Quaternion.identity) * keyFacing.Value);
            }
        }
        void Update()
        {
            if (!IsSpawned) return;
            transform.SetPositionAndRotation(origin.Value, heading.Value);
            CreateFlock();
            if (IsServerInitialized)
            {
                if (spawnChest) SpawnChest();
                var holder = keyHolder.Value;
                if (holder != null) { var motor = holder.GetComponent<AdvancedPlayerController>(); if (!holder.IsSpawned || motor == null || motor.IsDead) DropHeldKey(); }
                else if (!Unlocked && hadHolder) DropHeldKey();
                if (KeyDropped && hadSupport && (keySupport.Value == null || !keySupport.Value.IsSpawned))
                { keyPoint.Value = lastKeyPoint; keySupport.Value = null; onWater.Value = true; hadSupport = false; }
                if (KeyDropped && !onWater.Value && OceanSurface.Instance != null && KeyPosition.y < OceanSurface.Instance.Height(KeyPosition) - .1f)
                { keyPoint.Value = KeyPosition; keySupport.Value = null; onWater.Value = true; hadSupport = false; }
            }
            float time = FlightTime;
            bool sync = IsServerInitialized && Time.time >= nextFallSync;
            if (sync) nextFallSync = Time.time + .2f;
            for (int i = 0; i < BirdCount; i++)
            {
                var bird = birds[i];
                if ((shotMask.Value & (1u << i)) != 0 && i < falls.Count)
                {
                    var fall = falls[i];
                    if (sync && (!fall.Landed || fall.Age < fall.Seconds + SeagullVisual.SinkSeconds))
                    { fall.Age = (float)(Clock - shotAt[i]); falls[i] = fall; }
                    bird.BeginFall(fall);
                    continue;
                }
                var point = BirdPoint(i, time);
                var forward = BirdPoint(i, time + .04f) - point;
                bird.transform.SetPositionAndRotation(point, Quaternion.LookRotation(forward) * Quaternion.Euler(0, 0, (i % 3 == 0 ? 1 : -1) * 12f));
                bird.Animate(time, SeagullVisual.FlightPower(time, i * 113), 0, i == keyBird.Value ? 1 : 0);
            }
            UpdateKey();
            UpdatePadlock();
            if (IsServerInitialized && KeyDropped) lastKeyPoint = KeyPosition;
            if (IsClientInitialized && Time.time >= nextCall && !Unlocked)
            { nextCall = Time.time + Random.Range(16f, 24f); GameAudio.Play(SoundCue.Seagull, BirdPoint((int)(time % BirdCount), time)); }
        }
        public override void OnStopServer()
        {
            ServerEvents.Remove(this);
            if (chest != null && chest.IsSpawned && chest.InSeagullWreck) ServerManager.Despawn(chest.NetworkObject);
            base.OnStopServer();
        }
    }
}
