using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.Networking
{
    public sealed class NetworkSkullEvent : NetworkBehaviour
    {
        public static readonly List<NetworkSkullEvent> ClientEvents = new();
        public static readonly List<NetworkSkullEvent> ServerEvents = new();
        public LootCatalog Catalog;
        public Transform RotatingRoot, Mouth;
        public SkullFireVfx Fire;
        public float PlatformRadius = 50f;
        readonly SyncVar<bool> cleared = new();
        readonly SyncVar<float> yaw = new();
        readonly SyncVar<float> frozenYaw = new();
        float serverYaw, nextYaw, rewardAt, observedYaw = float.NaN, observedAt;
        bool rewardCreated;
        NetworkLootChest rewardChest;
        NetworkShip winningShip;
        Vector3 fallbackPoint;
        Vector3 fixedPosition;
        public bool Cleared => cleared.Value;
        public bool LootHintEligible => IsSpawned && !Cleared;
        public Vector3 LootHintPoint => transform.position + Vector3.up * 2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEvents() { ClientEvents.Clear(); ServerEvents.Clear(); }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            fixedPosition = transform.position;
        }

        void LateUpdate()
        {
            if (IsSpawned) transform.position = fixedPosition;
        }

        public static NetworkSkullEvent SpawnEvent(LootCatalog catalog, FishNet.Managing.NetworkManager manager, Vector3 point, Scene scene)
        {
            if (catalog == null || catalog.SkullEventPrefab == null) return null;
            var altar = Instantiate(catalog.SkullEventPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(altar.gameObject, scene);
            altar.Catalog = catalog;
            manager.ServerManager.Spawn(altar.NetworkObject);
            return altar;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerEvents.Add(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ClientEvents.Add(this);
            Fire.SetBurning(!Cleared, true);
        }

        public override void OnStopNetwork()
        {
            ClientEvents.Remove(this);
            ServerEvents.Remove(this);
            base.OnStopNetwork();
        }

        public override void OnStopServer()
        {
            if (rewardChest != null && rewardChest.IsSpawned) ServerManager.Despawn(rewardChest.NetworkObject);
            ServerEvents.Remove(this);
            base.OnStopServer();
        }

        void Update()
        {
            if (!IsSpawned || Catalog == null) return;
            float angle;
            if (IsServerInitialized)
            {
                if (!Cleared)
                {
                    serverYaw = Mathf.Repeat(serverYaw + Catalog.SkullRotationSpeed * Time.deltaTime, 360f);
                    if (Time.time >= nextYaw) { yaw.Value = serverYaw; nextYaw = Time.time + .1f; }
                }
                else if (!rewardCreated && Time.time >= rewardAt) EjectReward();
                angle = Cleared ? frozenYaw.Value : serverYaw;
            }
            else
            {
                if (observedYaw != yaw.Value) { observedYaw = yaw.Value; observedAt = Time.time; }
                angle = Cleared ? frozenYaw.Value : observedYaw + Catalog.SkullRotationSpeed * Mathf.Min(.3f, Time.time - observedAt);
            }
            RotatingRoot.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (IsClientInitialized) Fire.SetBurning(!Cleared);
        }

        public bool HitMouth(CannonShotDamage shot, Vector3 point)
        {
            if (!IsSpawned || Cleared || shot == null || !CannonAmmo.IsBall(shot.Ammo) || shot.Ammo == InventoryItem.BoardingHook) return false;
            if (Vector3.Dot(shot.Velocity.normalized, Mouth.forward) >= 0f) return false;
            var local = Mouth.InverseTransformPoint(point);
            var mouthCollider = Mouth.GetComponent<BoxCollider>();
            if (mouthCollider == null) return false;
            local -= mouthCollider.center;
            Vector3 opening = mouthCollider.size * .5f;
            float radius = shot.Radius / Mathf.Max(.001f, Mouth.lossyScale.x);
            if (Mathf.Abs(local.x) > opening.x + radius || Mathf.Abs(local.y) > opening.y + radius || Mathf.Abs(local.z) > opening.z + radius + .05f) return false;
            if (!IsServerInitialized || !shot.Authoritative) return true;
            winningShip = shot.Source != null ? shot.Source.GetComponentInParent<NetworkShip>() : null;
            if (winningShip == null && shot.Attacker != null)
            {
                var player = shot.Attacker.GetComponent<NetworkPlayer>();
                if (player != null) winningShip = player.Ship;
            }
            fallbackPoint = transform.position + Mouth.forward * (PlatformRadius + 8f);
            frozenYaw.Value = serverYaw;
            cleared.Value = true;
            rewardAt = Time.time + Catalog.SkullRewardDelay;
            return true;
        }

        void EjectReward()
        {
            rewardCreated = true;
            NetworkShip targetShip = winningShip != null && winningShip.IsSpawned && !winningShip.IsSinking ? winningShip : null;
            Vector3 destination = fallbackPoint;
            if (targetShip == null || !NetworkLootChest.TryFindRewardDeckPoint(targetShip, Catalog.ChestPrefab, out destination))
            {
                targetShip = null;
                destination = fallbackPoint;
                destination.y = OceanSurface.Instance != null ? OceanSurface.Instance.Height(destination) : transform.position.y;
            }
            var chest = Instantiate(Catalog.ChestPrefab, Mouth.position, Mouth.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = Catalog;
            try
            {
                var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ (uint)Random.Range(1, int.MaxValue)));
                chest.Fill(ref random);
                chest.LaunchReward(Mouth.position + Mouth.forward * (1.95f * Mouth.lossyScale.z), destination, Catalog.SkullRewardFlightSeconds, targetShip != null ? targetShip.transform.rotation : Mouth.rotation, targetShip);
                ServerManager.Spawn(chest.NetworkObject);
                rewardChest = chest;
            }
            catch (System.InvalidOperationException error)
            {
                Destroy(chest.gameObject);
                Debug.LogError("Skull reward could not be filled: " + error.Message, this);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !IsSpawned || !IsClientInitialized || !DeveloperMenu.Available || !DeveloperMenu.ShowLootEventLabels || SessionController.MenuOpen) return;
            var camera = Camera.main;
            if (camera == null) return;
            var viewer = camera.GetComponentInParent<NetworkPlayer>();
            if (viewer == null || !viewer.IsOwner || viewer.Motor.IsDead) return;
            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * 60f);
            if (screen.z <= 0f) return;
            string label = Cleared ? "Череп потушен" : "Огненный череп · попади ядром в рот";
            PirateHudStyle.Panel(new Rect(screen.x - 190f, Screen.height - screen.y, 380f, 28f), label);
        }
    }
}
