using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.Networking
{
    public sealed class NetworkPlatformingEvent : NetworkBehaviour
    {
        public static readonly List<NetworkPlatformingEvent> ServerEvents = new();
        public LootCatalog Catalog;
        public Transform ChestSocket;
        public Transform CourseRoot, IntactRoot, LeverRoot, LeverPivot, LeverGrip, ChainsRoot, LockShackle;
        public Transform[] CollapsePieces;
        public float PlatformRadius = 46;
        readonly SyncVar<Vector3> origin = new();
        readonly SyncVar<double> releasedAt = new(-1);
        NetworkLootChest chest;
        bool spawnChest, chestReleased, collisionsDisabled, pullPlayed, breakPlayed;
        Vector3[] piecePositions;
        Quaternion[] pieceRotations;
        Vector3 chainsPosition, shacklePosition;
        Quaternion chainsRotation, leverRotation, shackleRotation;
        Collider[] courseColliders;
        Light[] courseLights;
        uint clockTick;
        float clockFrameAt;
        public bool Released => releasedAt.Value >= 0;
        public double Clock
        {
            get
            {
                if (!IsSpawned || TimeManager == null) return Time.timeAsDouble;
                uint tick = TimeManager.Tick;
                if (tick != clockTick) { clockTick = tick; clockFrameAt = Time.unscaledTime; }
                return TimeManager.TicksToTime(tick) + Mathf.Min(Time.unscaledTime - clockFrameAt, (float)TimeManager.TickDelta);
            }
        }
        public float ReleaseAge => Released ? Mathf.Max(0, (float)(Clock - releasedAt.Value)) : 0;

        public static NetworkPlatformingEvent SpawnEvent(LootCatalog catalog, NetworkManager manager, Vector3 point, Scene scene)
        {
            if (catalog == null || catalog.PlatformingEventPrefab == null || catalog.ChestPrefab == null || manager == null) return null;
            var instance = Instantiate(catalog.PlatformingEventPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            instance.Catalog = catalog;
            manager.ServerManager.Spawn(instance.NetworkObject);
            return instance;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            origin.Value = transform.position;
            ServerEvents.Add(this);
            spawnChest = true;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            transform.position = origin.Value;
            CacheParts();
            ApplyRelease();
        }

        public void PullLever(NetworkWeapon actor)
        {
            if (!IsServerInitialized || !IsSpawned || Released || chest == null || !chest.IsSpawned || actor == null || !actor.IsSpawned || LeverGrip == null) return;
            var motor = actor.GetComponent<AdvancedPlayerController>();
            var inventory = actor.GetComponent<PlayerInventory>();
            if (motor == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsSwimming || motor.IsClimbing || motor.LocomotionLocked || actor.LootHandsBusy
                || inventory == null || inventory.HandsOccupied || actor.GetComponent<CannonHands>().HasHeldBall) return;
            if (inventory.Fishing != null && (inventory.Fishing.IsFishing || inventory.Fishing.IsEating || inventory.Fishing.HasFish)) return;
            if (Vector3.Distance(actor.transform.position, LeverGrip.position) > 3.5f || !actor.CanReach(LeverGrip.position, LeverRoot)) return;
            releasedAt.Value = Clock;
        }

        void CacheParts()
        {
            if (piecePositions != null) return;
            CollapsePieces ??= System.Array.Empty<Transform>();
            piecePositions = new Vector3[CollapsePieces.Length]; pieceRotations = new Quaternion[CollapsePieces.Length];
            for (int i = 0; i < CollapsePieces.Length; i++)
                if (CollapsePieces[i] != null) { piecePositions[i] = CollapsePieces[i].localPosition; pieceRotations[i] = CollapsePieces[i].localRotation; }
            courseColliders = CourseRoot != null ? CourseRoot.GetComponentsInChildren<Collider>(true) : System.Array.Empty<Collider>();
            courseLights = CourseRoot != null ? CourseRoot.GetComponentsInChildren<Light>(true) : System.Array.Empty<Light>();
            if (ChainsRoot != null) { chainsPosition = ChainsRoot.localPosition; chainsRotation = ChainsRoot.localRotation; }
            if (LeverPivot != null) leverRotation = LeverPivot.localRotation;
            if (LockShackle != null) { shacklePosition = LockShackle.localPosition; shackleRotation = LockShackle.localRotation; }
        }

        void ApplyRelease()
        {
            if (!Released) return;
            CacheParts();
            float age = ReleaseAge;
            if (IntactRoot != null)
            {
                IntactRoot.gameObject.SetActive(age < .6f);
                if (age < .6f) IntactRoot.localPosition = new Vector3(Mathf.Sin(age * 71) * .02f, 0, Mathf.Cos(age * 63) * .02f) * Mathf.Clamp01(age * 5);
            }
            if (LeverPivot != null) LeverPivot.localRotation = leverRotation * Quaternion.Euler(-65 * Mathf.SmoothStep(0, 1, age / .6f), 0, 0);
            if (LockShackle != null)
            {
                float t = Mathf.Clamp01((age - .2f) / .25f);
                LockShackle.localPosition = shacklePosition + Vector3.up * (.055f * t);
                LockShackle.localRotation = shackleRotation * Quaternion.Euler(0, 55 * t, 0);
            }
            if (age >= .6f && !collisionsDisabled)
            {
                collisionsDisabled = true;
                foreach (var collider in courseColliders) if (collider != null) collider.enabled = false;
                foreach (var light in courseLights) if (light != null) light.enabled = false;
            }
            for (int i = 0; i < CollapsePieces.Length; i++)
            {
                var piece = CollapsePieces[i]; if (piece == null) continue;
                if (piece != LeverRoot && age < .6f) continue;
                float fall = Mathf.Max(0, age - .6f - i % 7 * .018f);
                if (fall > 5) { piece.gameObject.SetActive(false); continue; }
                piece.gameObject.SetActive(true);
                float angle = i * 2.399963f;
                var outward = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                float shake = age < .6f ? Mathf.Sin(age * 71 + i * 3) * .035f * Mathf.Clamp01(age * 5) : 0;
                piece.localPosition = piecePositions[i] + outward * (shake + fall * (.4f + i % 4 * .3f)) + Vector3.down * (4.905f * fall * fall);
                piece.localRotation = pieceRotations[i] * Quaternion.Euler(fall * (12 + i % 5 * 7), fall * (i % 3 - 1) * 17, fall * (i % 2 == 0 ? 22 : -27));
            }
            if (ChainsRoot != null)
            {
                float fall = Mathf.Max(0, age - .4f);
                ChainsRoot.localPosition = chainsPosition + new Vector3(fall * .55f, -4.905f * fall * fall, fall * .7f);
                ChainsRoot.localRotation = chainsRotation * Quaternion.Euler(fall * 65, 0, fall * 35);
                ChainsRoot.gameObject.SetActive(fall < 5);
            }
            if (!IsClientInitialized) return;
            if (!pullPlayed) { pullPlayed = true; if (age < .8f) GameAudio.Play(SoundCue.ClamLeverPull, LeverGrip != null ? LeverGrip.position : transform.position); }
            if (age >= .6f && !breakPlayed)
            {
                breakPlayed = true;
                if (age < 1.5f)
                {
                    GameAudio.Play(SoundCue.LockpickBreak, ChestSocket.position);
                    GameAudio.Play(SoundCue.BarricadeBreak, ChestSocket.position);
                }
            }
        }

        void Update()
        {
            if (!IsSpawned) return;
            transform.position = origin.Value;
            if (IsServerInitialized && Released && !chestReleased && ReleaseAge >= .6f)
            {
                chestReleased = true;
                if (chest != null && chest.IsSpawned) chest.ReleasePlatforming();
            }
            ApplyRelease();
            if (!IsServerInitialized || !spawnChest) return;
            spawnChest = false;
            if (Catalog == null || Catalog.ChestPrefab == null || ChestSocket == null) return;
            chest = Instantiate(Catalog.ChestPrefab, ChestSocket.position, ChestSocket.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = Catalog;
            var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ 0x93a27b61u));
            chest.Fill(ref random);
            chest.PlaceOnPlatforming(this);
            ServerManager.Spawn(chest.NetworkObject);
        }

        public override void OnStopServer()
        {
            ServerEvents.Remove(this);
            if (chest != null && chest.IsSpawned && chest.OnPlatforming && !chest.PlatformingFalling) ServerManager.Despawn(chest.NetworkObject);
            base.OnStopServer();
        }
    }
}
