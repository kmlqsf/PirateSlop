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
    public sealed class SternRopeStation
    {
        public Transform Grip, Knot, Pulley, Outside, Inside, Landing;
    }
    public sealed class NetworkSternCargoEvent : NetworkBehaviour
    {
        public static readonly List<NetworkSternCargoEvent> Active = new();
        public LootCatalog Catalog;
        public NetworkTowedCargo CargoPrefab;
        public SternRopeStation[] RopeStations = Array.Empty<SternRopeStation>();
        public float InitialCable = 38, ReelSeconds = 20, CruiseSpeed = 3.5f;
        readonly SyncVar<Vector3> origin = new();
        readonly SyncVar<float> radius = new(250);
        readonly SyncList<NetworkObject> cargos = new();
        readonly SyncVar<bool> chestRecovered = new(), intercepted = new();
        readonly SyncVar<int> recoveredCount = new();
        NetworkShip ship;
        bool spawnCargo;
        public NetworkShip Ship => ship;
        public string Hint => chestRecovered.Value ? intercepted.Value ? "Груз за кормой · сундук перехвачен" : "Сундук на палубе · E — открыть · F — нести"
            : $"Груз за кормой · шесть узлов на кормовом ограждении · доставлено {recoveredCount.Value}/6";
        void Awake() { ship = GetComponent<NetworkShip>(); }
        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public static void SpawnEvent(LootCatalog catalog, NetworkManager manager, Vector3 point, Scene scene, bool test)
        {
            if (catalog == null || catalog.SternCargoEventPrefab == null) return;
            var owner = Instantiate(catalog.SternCargoEventPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner.gameObject, scene);
            owner.Catalog = catalog; owner.radius.Value = test ? 130 : 250;
            owner.GetComponent<NetworkShip>().ParticipantId.Value = -100000 - manager.ServerManager.Objects.Spawned.Count;
            owner.GetComponent<NetworkShip>().TeamId.Value = -1;
            manager.ServerManager.Spawn(owner.NetworkObject);
        }
        public override void OnStartServer()
        {
            base.OnStartServer(); origin.Value = transform.position;
            GetComponent<SailSystem>().SetDeploy(.8f); spawnCargo = true;
        }
        public void BeforeShipTick(float dt)
        {
            if (!IsServerInitialized || ship.IsSinking) return;
            if (spawnCargo)
            {
                spawnCargo = false;
                var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ (uint)DateTime.UtcNow.Ticks));
                int chestIndex = (int)(random.Next() % 6);
                var order = new[] {0,1,2,3,4,5};
                for (int i = 5; i > 0; i--) { int j = (int)(random.Next() % (uint)(i+1)); (order[i],order[j])=(order[j],order[i]); }
                for (int i = 0; i < RopeStations.Length; i++)
                {
                    var local = new Vector3((order[i]-2.5f)*2.1f, -1.8f, -20.5f-InitialCable);
                    var point = transform.TransformPoint(local);
                    point.y = (OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) : transform.position.y)-1.8f;
                    var cargo = Instantiate(CargoPrefab, point, transform.rotation);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cargo.gameObject, gameObject.scene);
                    cargo.Configure(this, i, i == chestIndex);
                    ServerManager.Spawn(cargo.NetworkObject); cargos.Add(cargo.NetworkObject);
                }
            }
            var flat = Vector3.ProjectOnPlane(transform.position-origin.Value, Vector3.up);
            if (ship.Helm.Driver == null)
            {
                float drift = .12f+Mathf.Sin(Time.time*.13f)*.09f;
                if (flat.magnitude>radius.Value*.65f)
                {
                    float yaw = Mathf.Atan2(-flat.x,-flat.z)*Mathf.Rad2Deg;
                    drift = Mathf.Clamp(Mathf.DeltaAngle(transform.eulerAngles.y,yaw)/40,-1,1);
                }
                ship.Helm.Restore(drift,false,null);
            }
            ship.Motor.SetTowingSpeedLimit(CruiseSpeed);
        }
        public void ConstrainMotion(ref Vector3 next)
        {
            if (!IsServerInitialized) return;
            var offset = Vector3.ProjectOnPlane(next-origin.Value,Vector3.up);
            if (offset.sqrMagnitude <= radius.Value*radius.Value) return;
            var bounded = origin.Value+offset.normalized*radius.Value;
            next.x = bounded.x; next.z = bounded.z; ship.Motor.SetTowingSpeedLimit(0);
        }
        public bool CanReach(NetworkWeapon actor, int index, bool holding = false, bool extended = false)
        {
            if (actor == null || index < 0 || index >= RopeStations.Length || RopeStations[index].Grip == null) return false;
            var grip = RopeStations[index].Grip;
            var feet = transform.InverseTransformPoint(actor.transform.position);
            var passenger = actor.GetComponent<ShipDeckPassenger>();
            if (extended)
            {
                var motor = actor.GetComponent<AdvancedPlayerController>();
                return passenger != null && passenger.Support == ship.Body && motor != null && !motor.IsSwimming
                    && (motor.IsGrounded || passenger.HasDryDeckSupport());
            }
            float deckY = feet.y;
            if (deckY < 6.4f || deckY > 8.7f) return false;
            var local = transform.InverseTransformPoint(actor.transform.position+Vector3.up*1.15f);
            if (Vector3.Distance(local, transform.InverseTransformPoint(grip.position))>(holding?3.3f:2.8f)) return false;
            if (holding) return passenger != null && passenger.Support == ship.Body;
            var eye = actor.transform.position+Vector3.up*1.5f;
            var delta = grip.position-eye;
            foreach (var hit in Physics.RaycastAll(eye,delta.normalized,Mathf.Max(0,delta.magnitude-.2f),~LayerMask.GetMask("Water"),QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(actor.transform) && !hit.transform.IsChildOf(transform)) return false;
            return true;
        }
        public void CargoRecovered(NetworkShip target, bool isChest)
        {
            if (!IsServerInitialized) return;
            recoveredCount.Value++;
            if (isChest) { chestRecovered.Value = true; intercepted.Value = target != ship; }
        }
        public override void OnStopServer()
        {
            foreach (var item in cargos) if (item != null && item.TryGetComponent<NetworkTowedCargo>(out var cargo) && cargo.IsSpawned) cargo.ReleaseTow();
            base.OnStopServer();
        }
    }
}
