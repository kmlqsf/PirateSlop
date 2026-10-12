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
    public struct FortGateState
    {
        public byte Hits;
        public double ChangedAt;
        public Vector3 Impact, Direction;
    }

    public struct FortBreakState
    {
        public byte Gate, Group;
        public double Started;
        public Vector3 Direction;
    }

    [Serializable]
    public sealed class FortGateFragment
    {
        public Transform Root;
        public BoxCollider Solid;
        public byte Group;
    }

    [Serializable]
    public sealed class FortBreachGate
    {
        public Transform Root;
        public BoxCollider Blocker;
        public FortGateFragment[] Fragments = Array.Empty<FortGateFragment>();
        [NonSerialized] public Vector3[] Rest;
        [NonSerialized] public Quaternion[] Rotations;
        [NonSerialized] public bool[] Detached;
    }

    [DefaultExecutionOrder(-80)]
    public sealed class NetworkCannonBreachEvent : NetworkBehaviour
    {
        public static readonly List<NetworkCannonBreachEvent> ServerEvents = new();
        public LootCatalog Catalog;
        public Transform ChestSocket;
        public Terrain IslandTerrain;
        public FortBreachGate[] Gates = Array.Empty<FortBreachGate>();
        public int HitsToBreach = 6;
        public float IslandRadius = 32;
        readonly SyncVar<Vector3> origin = new();
        readonly SyncList<FortGateState> states = new();
        readonly SyncList<FortBreakState> breaks = new();
        readonly List<GameObject> debris = new();
        NetworkLootChest chest;
        bool spawnChest;
        uint visualTick;
        float visualTickAt;
        public bool ChestAvailable
        {
            get { for (int i = 0; i < states.Count; i++) if (states[i].Hits >= HitsToBreach) return true; return false; }
        }
        public int Hits(int index) => index >= 0 && index < states.Count ? states[index].Hits : 0;
        double Clock => IsSpawned && TimeManager != null ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;
        double VisualClock
        {
            get
            {
                if (!IsSpawned || TimeManager == null) return Time.timeAsDouble;
                uint tick = TimeManager.Tick;
                if (tick != visualTick) { visualTick = tick; visualTickAt = Time.unscaledTime; }
                return TimeManager.TicksToTime(tick) + Mathf.Min(Time.unscaledTime - visualTickAt, (float)TimeManager.TickDelta);
            }
        }

        void Awake()
        {
            foreach (var gate in Gates)
            {
                gate.Rest = new Vector3[gate.Fragments.Length]; gate.Rotations = new Quaternion[gate.Fragments.Length]; gate.Detached = new bool[gate.Fragments.Length];
                for (int i = 0; i < gate.Fragments.Length; i++)
                {
                    gate.Rest[i] = gate.Fragments[i].Root.localPosition;
                    gate.Rotations[i] = gate.Fragments[i].Root.localRotation;
                }
            }
        }

        public static void SpawnEvent(LootCatalog catalog, NetworkManager manager, Vector3 point, Scene scene)
        {
            if (catalog == null || catalog.CannonBreachEventPrefab == null) return;
            var instance = Instantiate(catalog.CannonBreachEventPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            instance.Catalog = catalog;
            manager.ServerManager.Spawn(instance.NetworkObject);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            origin.Value = transform.position; states.Clear(); breaks.Clear();
            foreach (var gate in Gates) states.Add(new FortGateState { ChangedAt = -1 });
            ServerEvents.RemoveAll(item => item == null || item == this); ServerEvents.Add(this);
            spawnChest = true;
        }

        public override void OnStartClient()
        {
            base.OnStartClient(); transform.position = origin.Value; ApplyGates();
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServerInitialized && spawnChest)
            {
                spawnChest = false;
                if (Catalog != null && Catalog.ChestPrefab != null)
                {
                    chest = Instantiate(Catalog.ChestPrefab, ChestSocket.position, ChestSocket.rotation);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
                    chest.Catalog = Catalog;
                    var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ 0x72b391afu));
                    chest.Fill(ref random); chest.PlaceInFort(this); ServerManager.Spawn(chest.NetworkObject);
                }
            }
            ApplyGates();
        }

        public bool Hit(int index, CannonShotDamage shot, Vector3 point)
        {
            if (index < 0 || index >= Gates.Length || shot.Ammo == InventoryItem.BoardingHook) return false;
            if (!shot.Authoritative) return true;
            if (!IsServerInitialized || !IsSpawned || index >= states.Count || states[index].Hits >= HitsToBreach) return true;
            var state = states[index]; state.Hits++; state.ChangedAt = Clock;
            state.Impact = point; state.Direction = shot.Velocity.normalized;
            int group = NearestGroup(index, point);
            if (group >= 0) breaks.Add(new FortBreakState { Gate = (byte)index, Group = (byte)group, Started = Clock, Direction = state.Direction });
            states[index] = state; ApplyGates();
            GateImpactRpc(point, state.Direction);
            return true;
        }

        [ObserversRpc(RunLocally = true)]
        void GateImpactRpc(Vector3 point, Vector3 direction)
        {
            CombatVfx.Impact(point, -direction, true); GameAudio.Play(SoundCue.Impact, point);
        }

        void ApplyGates()
        {
            for (int g = 0; g < Gates.Length; g++)
            {
                var gate = Gates[g];
                var state = g < states.Count ? states[g] : new FortGateState { ChangedAt = -1 };
                gate.Blocker.enabled = false;
                float age = state.ChangedAt < 0 ? 99 : Mathf.Max(0, (float)(VisualClock - state.ChangedAt));
                for (int p = 0; p < gate.Fragments.Length; p++)
                {
                    var fragment = gate.Fragments[p]; var piece = fragment.Root;
                    bool detached = FindBreak(g, fragment.Group, out var impact);
                    if (fragment.Group == 6 && state.Hits >= HitsToBreach)
                    {
                        detached = true;
                        impact = new FortBreakState { Gate = (byte)g, Group = 6, Started = state.ChangedAt, Direction = state.Direction };
                    }
                    if (detached && !gate.Detached[p])
                    {
                        piece.localPosition = gate.Rest[p]; piece.localRotation = gate.Rotations[p];
                        float elapsed = Mathf.Max(0, (float)(VisualClock - impact.Started));
                        if (IsClientInitialized && elapsed < .6f) SpawnDebris(g, p, impact, elapsed);
                    }
                    fragment.Solid.enabled = !detached;
                    gate.Detached[p] = detached; piece.gameObject.SetActive(!detached);
                    if (detached) continue;
                    float shake = state.Hits == 0 ? 0 : Mathf.Sin(age * 30) * Mathf.Exp(-age * 8) * .018f;
                    piece.localPosition = gate.Rest[p] + Vector3.forward * shake;
                    piece.localRotation = gate.Rotations[p];
                }
            }
        }

        bool FindBreak(int gate, int group, out FortBreakState result)
        {
            for (int i = 0; i < breaks.Count; i++)
                if (breaks[i].Gate == gate && breaks[i].Group == group) { result = breaks[i]; return true; }
            result = default; return false;
        }

        int NearestGroup(int index, Vector3 point)
        {
            var gate = Gates[index]; Vector3 local = gate.Root.InverseTransformPoint(point);
            int nearest = -1; float distance = float.MaxValue;
            for (int group = 0; group < 6; group++)
            {
                if (FindBreak(index, group, out _)) continue;
                Vector3 center = Vector3.zero; int count = 0;
                for (int p = 0; p < gate.Fragments.Length; p++)
                    if (gate.Fragments[p].Group == group) { center += gate.Rest[p]; count++; }
                if (count == 0) continue;
                center /= count;
                float next = (new Vector2(local.x, local.y) - new Vector2(center.x, center.y)).sqrMagnitude;
                if (next < distance) { nearest = group; distance = next; }
            }
            return nearest;
        }

        void SpawnDebris(int gateIndex, int partIndex, FortBreakState impact, float age)
        {
            var gate = Gates[gateIndex]; var fragment = gate.Fragments[partIndex];
            var chunk = Instantiate(fragment.Root.gameObject, fragment.Root.position, fragment.Root.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chunk, gameObject.scene);
            chunk.name = "FortGateDebris";
            bool support = fragment.Group == 6;
            foreach (var t in chunk.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("ShipDebris");
            foreach (var c in chunk.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = true;
                if (c is BoxCollider box)
                {
                    var colliderSize = box.size;
                    if (support) colliderSize.y *= .94f; else colliderSize.x *= .76f;
                    box.size = colliderSize;
                }
                foreach (var solid in gate.Root.GetComponentsInChildren<Collider>(true))
                    if (solid.enabled && solid.gameObject.activeInHierarchy) Physics.IgnoreCollision(c, solid);
            }
            var random = new System.Random(gateIndex * 197 + partIndex * 7919 + impact.Group * 31);
            float force = support ? .35f : 1.8f + (float)random.NextDouble() * 1.2f;
            Vector3 velocity = impact.Direction * force + gate.Root.right * ((float)random.NextDouble() - .5f) * (support ? .15f : .8f) + Vector3.up * (support ? .05f : .45f + (float)random.NextDouble() * .45f);
            var body = chunk.AddComponent<Rigidbody>();
            var size = fragment.Solid.bounds.size;
            body.mass = Mathf.Clamp(size.x * size.y * size.z * 600, 30, 400);
            body.linearDamping = .55f; body.angularDamping = 1.25f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            chunk.transform.position += velocity * age + Physics.gravity * (.5f * age * age);
            chunk.SetActive(true); body.linearVelocity = velocity + Physics.gravity * age;
            body.angularVelocity = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f) * (support ? .55f : 1.8f);
            WaterImpactBody.Ensure(chunk);
            debris.RemoveAll(item => item == null); debris.Add(chunk); Destroy(chunk, 7);
        }

        public override void OnStopNetwork()
        {
            foreach (var chunk in debris) if (chunk != null) Destroy(chunk);
            debris.Clear(); base.OnStopNetwork();
        }

        public override void OnStopServer()
        {
            ServerEvents.Remove(this);
            if (chest != null && chest.IsSpawned && chest.InFort) ServerManager.Despawn(chest.NetworkObject);
            base.OnStopServer();
        }
    }
}
