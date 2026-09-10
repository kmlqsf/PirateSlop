using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using PirateSlop;
namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-20)]
    public sealed partial class NetworkShip : NetworkBehaviour
    {
        public static readonly System.Collections.Generic.List<NetworkShip> ActiveShips = new();
        [SerializeField] Vector2 hullHalfExtents = new(6.5f, 23f);
        public Vector2 HullHalfExtents => hullHalfExtents;
        public readonly SyncVar<int> ParticipantId = new();
        public readonly SyncVar<int> TeamId = new();
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
            if (!ActiveShips.Contains(this)) ActiveShips.Add(this);
            Body.interpolation = RigidbodyInterpolation.None;
            TimeManager.OnTick += Tick;
            TimeManager.OnPostTick += Publish;
        }
        public override void OnStopNetwork()
        {
            ActiveShips.Remove(this);
            ClearAmmo();
            TimeManager.OnTick -= Tick;
            TimeManager.OnPostTick -= Publish;
            Helm.ReleaseControl();
        }
        void Tick()
        {
            if (!IsServerInitialized) return;
            Helm.Simulate(default, null, (float)TimeManager.TickDelta);
            Motor.Simulate((float)TimeManager.TickDelta);
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
        public void DragWheel(float degrees, bool holding) => DragWheelServerRpc(degrees, holding);
        public void CollisionAudio() { if (IsServerInitialized) CollisionAudioObserversRpc(); }
        public void ImpactVfx(Vector3 point, Vector3 normal) { if (IsServerInitialized) ImpactVfxObserversRpc(point, normal); }
        [ObserversRpc(RunLocally = true)]
        void ImpactVfxObserversRpc(Vector3 point, Vector3 normal)
        {
            CombatVfx.Impact(point, normal, true);
            GameAudio.Play(SoundCue.ShipHit, point);
        }
        [ObserversRpc(RunLocally = true)]
        void CollisionAudioObserversRpc() => GameAudio.Play(SoundCue.ShipCollision, transform.position);
        [ServerRpc(RequireOwnership = false)]
        void DragWheelServerRpc(float degrees, bool holding, FishNet.Connection.NetworkConnection sender = null)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (player != null) Helm.Drag(player.Motor, degrees, holding);
        }
        public void AdjustSails(float amount) => AdjustSailsServerRpc(amount);
        [ServerRpc(RequireOwnership = false)]
        void AdjustSailsServerRpc(float amount, FishNet.Connection.NetworkConnection sender = null)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            var sails = GetComponent<SailSystem>();
            if (player != null && sails.InRange(player.Motor) && float.IsFinite(amount)) sails.AdjustSail(Mathf.Clamp(amount, -.2f, .2f));
        }
        [ObserversRpc(BufferLast = true)]
        void ReceiveState(ShipState state, int driver)
        {
            if (IsServerInitialized) return;
            if (OceanSurface.Instance != null) OceanSurface.Instance.Synchronize(state.WaveTime);
            remoteState = state; remoteDriver = driver; hasRemoteState = true;
        }
        void Update()
        {
            UpdateAmmo();
            if (IsServerInitialized || !hasRemoteState) return;
            AdvancedPlayerController driver = null;
            if (remoteDriver > 0)
                foreach (var p in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                    if (p.ParticipantId.Value == remoteDriver) { driver = p.Motor; break; }
            Motor.ApplyRemoteState(remoteState, 1f - Mathf.Exp(-16f * Time.deltaTime), driver);
        }
    }
}
