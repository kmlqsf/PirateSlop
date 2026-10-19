using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.AI;

namespace PirateSlop.Networking
{
    public struct KamikazeState
    {
        public Vector3 Position;
        public float Yaw, Speed;
        public byte Flags;
        public uint Sequence;
    }

    [DefaultExecutionOrder(-80)]
    public sealed class NetworkKamikazeEvent : NetworkBehaviour
    {
        public const int UnitCount = 12;
        public static readonly List<NetworkKamikazeEvent> ServerEvents = new();
        public LootCatalog Catalog;
        public Transform ChestSocket;
        public KamikazeUnit UnitPrefab;
        public NavMeshData Navigation;
        public Vector3[] SpawnPoints = Array.Empty<Vector3>();
        public AudioClip Scream;
        public AudioClip ExplosionSound;
        public GameObject ExplosionPrefab;
        const string ExplosionPoolKey = "KamikazeExplosion";
        public LayerMask ObstructionMask = ~0;
        public int AgentTypeID;
        public int NavigationAreas = NavMesh.AllAreas;
        [Min(.1f)] public float TriggerDistance = 3f;
        [Min(1f)] public float IslandRadius = 78f;
        [Min(.1f)] public float RunSpeed = 8.24f;
        [Range(1.001f, 1.1f)] public float SprintSpeedMultiplier = 1.03f;
        [Range(.2f, 1f)] public float LeapSeconds = .55f;
        [Min(1f)] public float LeapHeight = 6f;
        [Min(.1f)] public float DetonationDistance = 2.5f;
        [Min(0)] public float BlastDamage = 30f;
        [Min(.1f)] public float BlastInnerRadius = 2.75f, BlastRadius = 8f;
        [Min(1f)] public float BodyHitDamage = 45f;
        [Min(1f)] public float MinimumSpawnDistance = 12f;
        [Range(.05f, .25f)] public float SnapshotInterval = .1f;
        [Range(0f, 1f)] public float ScreamVolume = .22f;
        [Min(4f)] public float ScreamDistance = 140f;
        readonly SyncVar<Vector3> origin = new();
        readonly SyncVar<Quaternion> heading = new(Quaternion.identity);
        readonly SyncVar<bool> triggered = new();
        readonly SyncVar<int> remaining = new();
        readonly SyncList<KamikazeState> states = new();
        readonly KamikazeUnit[] units = new KamikazeUnit[UnitCount];
        readonly Vector3[] wavePoints = new Vector3[UnitCount];
        readonly List<Vector3> candidates = new();
        readonly List<bool> available = new();
        readonly Queue<KamikazeUnit> explosions = new();
        readonly HashSet<CombatHealth> engagedPlayers = new();
        NavMeshPath preparationPath;
        NavMeshDataInstance navigationInstance;
        NavMeshQueryFilter navigationFilter;
        NetworkLootChest chest;
        int created, prepared, leapBudget;
        int obstructionLayers;
        uint revision;
        float triggerAt, publishAt, targetCleanupAt;
        float unitRadius, unitHeight;
        Vector3 navigationCenter;
        bool configured, preparationDone, chestSpawned, serverReady;
        public bool Triggered => triggered.Value;
        public int Remaining => remaining.Value;
        public bool Ready => serverReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEvents() => ServerEvents.Clear();

        public static void SpawnForIslands(ProceduralWorld world, NetworkManager manager, LootCatalog catalog)
        {
            if (catalog == null || catalog.KamikazeEventPrefab == null || world == null || !world.Ready) return;
            var seen = new HashSet<Transform>();
            foreach (var terrain in world.GetComponentsInChildren<Terrain>())
            {
                Transform island = terrain.transform;
                while (island != null && island != world.transform && !island.name.StartsWith("SingleLandingRockIsland", StringComparison.Ordinal)) island = island.parent;
                if (island == null || island == world.transform || !seen.Add(island)) continue;
                bool exists = false;
                foreach (var item in ServerEvents)
                    if (item != null && item.gameObject.scene == world.gameObject.scene && (item.transform.position - island.position).sqrMagnitude < .01f) { exists = true; break; }
                if (exists) continue;
                var instance = Instantiate(catalog.KamikazeEventPrefab, island.position, island.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance.gameObject, world.gameObject.scene);
                instance.Catalog = catalog;
                manager.ServerManager.Spawn(instance.NetworkObject);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            preparationPath = new NavMeshPath();
            candidates.Clear(); available.Clear(); explosions.Clear(); engagedPlayers.Clear();
            prepared = 0; revision = 0; triggerAt = publishAt = targetCleanupAt = 0;
            obstructionLayers = ObstructionMask & ~LayerMask.GetMask("Water", "ShipDebris");
            preparationDone = chestSpawned = serverReady = false;
            origin.Value = transform.position; heading.Value = transform.rotation;
            triggered.Value = false; remaining.Value = 0; states.Clear();
            for (int i = 0; i < UnitCount; i++) states.Add(default);
            configured = Catalog != null && Catalog.ChestPrefab != null && ChestSocket != null && Navigation != null &&
                UnitPrefab != null && UnitPrefab.HasVisual && UnitPrefab.Hitboxes.Length >= 3 && SpawnPoints.Length >= UnitCount && Scream != null && ExplosionSound != null && ExplosionPrefab != null;
            if (!configured)
            {
                Debug.LogError("Kamikaze event requires a visual unit, body/bomb hitboxes, navigation, spawn points, chest and scream before activation.", this);
                return;
            }
            navigationInstance = NavMesh.AddNavMeshData(Navigation, transform.position, transform.rotation);
            var agent = UnitPrefab.GetComponent<NavMeshAgent>();
            unitRadius = Mathf.Max(.2f, agent.radius); unitHeight = Mathf.Max(unitRadius * 2f + .1f, agent.height);
            navigationFilter = new NavMeshQueryFilter { agentTypeID = AgentTypeID, areaMask = NavigationAreas };
            configured = navigationInstance.valid && TryNavigationPoint(ChestSocket.position, 3f, out navigationCenter);
            if (!configured)
            {
                Debug.LogError("Kamikaze event navigation does not reach the chest.", this);
                return;
            }
            ServerEvents.Add(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized) transform.SetPositionAndRotation(origin.Value, heading.Value);
            if (!Application.isBatchMode && ExplosionPrefab != null) VfxPool.Instance.Register(ExplosionPoolKey, ExplosionPrefab, UnitCount);
        }

        void Update()
        {
            if (!IsSpawned || UnitPrefab == null || Navigation == null) return;
            if (IsServerInitialized && !configured) return;
            int budget = 3;
            while (created < UnitCount && budget-- > 0)
            {
                var unit = Instantiate(UnitPrefab, transform);
                unit.name = "Kamikaze_" + created;
                unit.Initialize(this, created, IsServerInitialized);
                units[created++] = unit;
            }
            if (!IsServerInitialized) return;
            if (!preparationDone) PreparePoints();
            if (!serverReady)
            {
                serverReady = preparationDone && candidates.Count >= UnitCount && created == UnitCount;
                if (!serverReady) return;
            }
            if (!chestSpawned) SpawnChest();
            if (!Triggered)
            {
                if (Time.time < triggerAt) return;
                triggerAt = Time.time + .1f;
                foreach (var health in CombatHealth.Active)
                {
                    if (!IsTarget(health) || (health.transform.position + Vector3.up * .8f - (ChestSocket.position + Vector3.up * .4f)).sqrMagnitude > TriggerDistance * TriggerDistance) continue;
                    if (PlanWave(health.transform.position)) BeginWave(health);
                    else triggerAt = Time.time + 1f;
                    break;
                }
                return;
            }
            if (remaining.Value == 0) return;
            if (Time.time >= targetCleanupAt)
            {
                targetCleanupAt = Time.time + .5f;
                engagedPlayers.RemoveWhere(static health => health == null || health.IsDead || !health.isActiveAndEnabled ||
                    !health.TryGetComponent<NetworkPlayer>(out var player) || !player.IsSpawned);
            }
            leapBudget = 2;
            for (int i = 0; i < UnitCount; i++) units[i].ServerStep();
            budget = 4;
            while (explosions.Count > 0 && budget-- > 0) Explode(explosions.Dequeue());
            if (Time.time < publishAt) return;
            publishAt = Time.time + SnapshotInterval; revision++;
            for (int i = 0; i < UnitCount; i++)
            {
                var next = units[i].Capture(revision);
                var previous = states[i];
                if (next.Flags == previous.Flags && (next.Position - previous.Position).sqrMagnitude < .0001f &&
                    Mathf.Abs(Mathf.DeltaAngle(next.Yaw, previous.Yaw)) < .5f && Mathf.Abs(next.Speed - previous.Speed) < .1f) continue;
                states[i] = next;
            }
        }

        void LateUpdate()
        {
            if (!IsSpawned || IsServerInitialized) return;
            for (int i = 0; i < created && i < states.Count; i++) units[i].ApplySnapshot(states[i]);
        }

        void PreparePoints()
        {
            int budget = 4;
            while (prepared < SpawnPoints.Length && budget-- > 0)
            {
                var point = transform.TransformPoint(SpawnPoints[prepared++]);
                if (!TryNavigationPoint(point, .75f, out point) || !FreeSpawn(point) ||
                    !NavMesh.CalculatePath(point, navigationCenter, navigationFilter, preparationPath) || preparationPath.status != NavMeshPathStatus.PathComplete) continue;
                candidates.Add(point);
            }
            preparationDone = prepared == SpawnPoints.Length;
            if (preparationDone && candidates.Count < UnitCount)
                Debug.LogError("Kamikaze event has fewer than twelve clear reachable spawn candidates; event remains inactive.", this);
        }

        void SpawnChest()
        {
            chestSpawned = true;
            chest = Instantiate(Catalog.ChestPrefab, ChestSocket.position, ChestSocket.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = Catalog;
            var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ 0x74a9132bu));
            chest.Fill(ref random);
            chest.PlaceInKamikazeEvent(this);
            ServerManager.Spawn(chest.NetworkObject);
        }

        bool PlanWave(Vector3 player)
        {
            available.Clear();
            foreach (var point in candidates) available.Add(FreeSpawn(point) && FarFromPlayers(point));
            for (int i = 0; i < UnitCount; i++)
            {
                float angle = i * Mathf.PI * 2f / UnitCount;
                var bearing = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var desired = player + bearing * (MinimumSpawnDistance + 14f + i % 3 * 5f);
                float best = float.PositiveInfinity;
                bool found = false;
                for (int c = 0; c < candidates.Count; c++)
                {
                    if (!available[c]) continue;
                    var point = candidates[c];
                    var direction = point - player; direction.y = 0;
                    if (Vector3.Dot(direction.normalized, bearing) < .65f) continue;
                    float distance = (point - desired).sqrMagnitude;
                    if (distance >= best) continue;
                    bool separated = true;
                    for (int j = 0; j < i; j++) if ((wavePoints[j] - point).sqrMagnitude < 25f) { separated = false; break; }
                    if (!separated) continue;
                    best = distance; wavePoints[i] = point; found = true;
                }
                if (!found) return false;
            }
            return true;
        }

        void BeginWave(CombatHealth instigator)
        {
            engagedPlayers.Add(instigator);
            foreach (var health in CombatHealth.Active) if (IsTarget(health)) engagedPlayers.Add(health);
            triggered.Value = true;
            int activated = 0;
            for (int i = 0; i < UnitCount; i++)
            {
                Vector3 facing = navigationCenter - wavePoints[i]; facing.y = 0;
                if (!units[i].Activate(wavePoints[i], facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : transform.rotation))
                {
                    for (int j = 0; j <= i; j++) units[j].Hide();
                    triggered.Value = false;
                    Debug.LogError("Kamikaze wave activation failed; no partial wave was retained.", this);
                    return;
                }
                activated++;
            }
            remaining.Value = activated; revision++;
            for (int i = 0; i < UnitCount; i++) states[i] = units[i].Capture(revision);
        }

        bool FreeSpawn(Vector3 point)
        {
            return !Physics.CheckCapsule(point + Vector3.up * (unitRadius + .08f), point + Vector3.up * (unitHeight - unitRadius), unitRadius,
                obstructionLayers, QueryTriggerInteraction.Ignore);
        }

        bool FarFromPlayers(Vector3 point)
        {
            float minimum = MinimumSpawnDistance * MinimumSpawnDistance;
            foreach (var health in CombatHealth.Active)
                if (health != null && !health.IsDead && health.gameObject.scene == gameObject.scene && (health.transform.position - point).sqrMagnitude < minimum) return false;
            return true;
        }

        public bool TryNavigationPoint(Vector3 point, float distance, out Vector3 result)
        {
            bool found = NavMesh.SamplePosition(point, out var hit, distance, navigationFilter);
            result = hit.position;
            return found;
        }

        public bool IsTarget(CombatHealth health)
        {
            if (!IsLivingPlayer(health)) return false;
            var local = transform.InverseTransformPoint(health.transform.position);
            return local.x * local.x + local.z * local.z <= IslandRadius * IslandRadius &&
                Mathf.Abs(health.transform.position.y - ChestSocket.position.y) < 10f;
        }

        bool IsLivingPlayer(CombatHealth health)
        {
            return health != null && !health.IsDead && health.isActiveAndEnabled && health.gameObject.scene == gameObject.scene &&
                health.TryGetComponent<NetworkPlayer>(out var player) && player.IsSpawned;
        }

        public bool IsPursuitTarget(CombatHealth health) => IsLivingPlayer(health) && (engagedPlayers.Contains(health) || IsTarget(health));

        public bool TakeLeapBudget()
        {
            if (leapBudget <= 0) return false;
            leapBudget--; return true;
        }

        public float LeapArcHeight(Vector3 from, Vector3 to)
        {
            float height = Mathf.Max(1f, LeapHeight);
            float top = Mathf.Max(from.y, to.y) + 80f;
            for (int i = 1; i < 8; i++)
            {
                float fraction = i / 8f;
                var point = Vector3.Lerp(from, to, fraction);
                if (!Physics.Raycast(new Vector3(point.x, top, point.z), Vector3.down, out var hit, 160f, obstructionLayers, QueryTriggerInteraction.Ignore)) continue;
                height = Mathf.Max(height, (hit.point.y + 1f - point.y) / Mathf.Sin(fraction * Mathf.PI));
            }
            return Mathf.Min(height, 80f);
        }

        public bool TraceLeap(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < .000001f) { hit = default; return false; }
            return Physics.CapsuleCast(from + Vector3.up * (unitRadius + .08f), from + Vector3.up * (unitHeight - unitRadius),
                unitRadius, delta.normalized, out hit, delta.magnitude, obstructionLayers, QueryTriggerInteraction.Ignore);
        }

        public CombatHealth FindTarget(Vector3 position)
        {
            CombatHealth result = null;
            float nearest = float.PositiveInfinity;
            foreach (var health in CombatHealth.Active)
            {
                if (!IsPursuitTarget(health)) continue;
                engagedPlayers.Add(health);
                float distance = (health.transform.position - position).sqrMagnitude;
                if (distance >= nearest) continue;
                result = health; nearest = distance;
            }
            return result;
        }

        public bool ClearLine(Vector3 from, Vector3 to, Transform target = null)
        {
            if (!Physics.Linecast(from, to, out var hit, obstructionLayers, QueryTriggerInteraction.Ignore)) return true;
            return target != null && hit.transform.IsChildOf(target);
        }

        public void QueueExplosion(KamikazeUnit unit)
        {
            if (!IsServerInitialized || !IsSpawned || unit == null || unit.Event != this || !unit.Queue()) return;
            explosions.Enqueue(unit);
        }

        void Explode(KamikazeUnit unit)
        {
            if (unit == null || !unit.Alive) return;
            Vector3 point = unit.BlastPoint;
            unit.MarkExploded(); remaining.Value = Mathf.Max(0, remaining.Value - 1);
            states[unit.Index] = unit.Capture(++revision);
            foreach (var health in CombatHealth.Active)
            {
                if (health == null || health.IsDead || health.gameObject.scene != gameObject.scene) continue;
                var target = health.transform.position + Vector3.up * .9f;
                float damage = BlastAmount(Vector3.Distance(point, target));
                if (damage > 0 && ClearLine(point, target, health.transform)) health.Damage(damage, unit.gameObject, environmental: true);
            }
            ExplosionRpc(unit.Index, point);
        }

        float BlastAmount(float distance)
        {
            float outer = Mathf.Max(BlastInnerRadius + .1f, BlastRadius);
            return BlastDamage * (1f - Mathf.InverseLerp(BlastInnerRadius, outer, distance));
        }

        public static void DamageNearby(UnityEngine.SceneManagement.Scene scene, Vector3 point, float radius, float damage, GameObject attacker)
        {
            if (radius <= 0 || damage <= 0) return;
            foreach (var owner in ServerEvents)
            {
                if (owner == null || !owner.IsServerInitialized || !owner.IsSpawned || owner.gameObject.scene != scene) continue;
                for (int i = 0; i < owner.created; i++)
                {
                    var unit = owner.units[i];
                    if (!unit.Alive || unit.Queued || (unit.BlastPoint - point).sqrMagnitude > radius * radius || !owner.ClearLine(point, unit.BlastPoint)) continue;
                    unit.ReceiveHit(damage / Mathf.Max(1f, owner.BodyHitDamage), attacker);
                }
            }
        }

        [ObserversRpc(RunLocally = true)]
        void ExplosionRpc(int index, Vector3 point)
        {
            if (!IsClientInitialized || Application.isBatchMode) return;
            if (index >= 0 && index < created) units[index].MarkExploded();
            var pool = VfxPool.Instance;
            var effect = pool.Get(ExplosionPoolKey, point, Quaternion.identity);
            if (effect != null)
            {
                var particles = effect.GetComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play(true);
                pool.Return(ExplosionPoolKey, effect, 2.6f);
            }
            GameAudio.KamikazeExplosion(ExplosionSound, point);
        }

        public override void OnStopServer()
        {
            ServerEvents.Remove(this);
            if (chest != null && chest.IsSpawned && chest.KamikazeEvent == this) ServerManager.Despawn(chest.NetworkObject);
            if (navigationInstance.valid) navigationInstance.Remove();
            explosions.Clear();
            engagedPlayers.Clear();
            DestroyUnits();
            base.OnStopServer();
        }

        public override void OnStopClient()
        {
            if (!IsServerInitialized) DestroyUnits();
            base.OnStopClient();
        }

        void DestroyUnits()
        {
            for (int i = 0; i < units.Length; i++) if (units[i] != null) { units[i].Hide(); Destroy(units[i].gameObject); units[i] = null; }
            created = 0;
        }
    }
}
