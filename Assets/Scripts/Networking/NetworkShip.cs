using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using PirateSlop;
namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-20)]
    public sealed class NetworkShip : NetworkBehaviour
    {
        public readonly SyncVar<int> ParticipantId = new();
        public ShipController Motor { get; private set; }
        public HelmInteraction Helm { get; private set; }
        public Rigidbody Body { get; private set; }
        public float CollisionRadius { get; private set; }
        [SerializeField, Min(0f)] float collisionRadiusOverride;
        ShipState remoteState;
        bool hasRemoteState;
        int remoteDriver;
        void Awake()
        {
            Motor = GetComponent<ShipController>(); Motor.Networked = true;
            Helm = GetComponentInChildren<HelmInteraction>(); Helm.Networked = true;
            Body = GetComponent<Rigidbody>(); Body.interpolation = RigidbodyInterpolation.None;
            float radius = 1f;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                var extent = collider.bounds.extents;
                radius = Mathf.Max(radius, Mathf.Max(extent.x, extent.z) * .85f);
            }
            CollisionRadius = collisionRadiusOverride > 0f ? collisionRadiusOverride : radius;
        }
        public override void OnStartNetwork()
        {
            Body.interpolation = RigidbodyInterpolation.None;
            TimeManager.OnTick += Tick;
            TimeManager.OnPostTick += Publish;
        }
        public override void OnStopNetwork()
        {
            TimeManager.OnTick -= Tick;
            TimeManager.OnPostTick -= Publish;
            Helm.ReleaseControl();
        }
        void Tick()
        {
            if (!IsServerInitialized) return;
            if (!Helm.IsControlling) Helm.Simulate(default, null, (float)TimeManager.TickDelta);
            Motor.Simulate((float)TimeManager.TickDelta);
            Physics.SyncTransforms();
        }
        void Publish()
        {
            if (!IsServerInitialized) return;
            int driver = 0;
            if (Helm.Driver != null)
            {
                var player = Helm.Driver.GetComponent<NetworkPlayer>();
                if (player != null) driver = player.ParticipantId.Value;
            }
            ReceiveState(Motor.Capture(), driver);
        }
        [ObserversRpc(BufferLast = true)]
        void ReceiveState(ShipState state, int driver)
        {
            if (IsServerInitialized) return;
            remoteState = state; remoteDriver = driver; hasRemoteState = true;
        }
        void Update()
        {
            if (IsServerInitialized || !hasRemoteState) return;
            AdvancedPlayerController driver = null;
            if (remoteDriver > 0)
                foreach (var p in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                    if (p.ParticipantId.Value == remoteDriver) { driver = p.Motor; break; }
            Motor.ApplyRemoteState(remoteState, 1f - Mathf.Exp(-16f * Time.deltaTime), driver);
        }
    }
}
