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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveShips() => ActiveShips.Clear();
        void OnDestroy() => ActiveShips.Remove(this);
        [SerializeField] Vector2 hullHalfExtents = new(6.5f, 23f);
        public Vector2 HullHalfExtents => hullHalfExtents;
        public readonly SyncVar<int> ParticipantId = new();
        public readonly SyncVar<int> TeamId = new();
        public readonly SyncVar<float> DeveloperSpeedMultiplier = new(1f);
        public ShipController Motor { get; private set; }
        public HelmInteraction Helm { get; private set; }
        public Rigidbody Body { get; private set; }
        public float CollisionRadius { get; private set; }
        [SerializeField, Min(0f)] float collisionRadiusOverride;
        ShipState remoteState;
        bool hasRemoteState;
        readonly SyncVar<bool> sinking = new();
        float sinkingStarted;
        Vector3 sinkingPosition;
        float sinkingYaw;
        public bool IsSinking => sinking.Value;
        public float SinkingElapsed => IsSinking ? Time.time - sinkingStarted : 0f;
        public void BeginSinking()
        {
            if (!IsServerInitialized || IsSinking || GetComponent<ShipFlooding>() is not { Level: >= 1f }) return;
            sinking.Value = true;
            sinkingStarted = Time.time;
            sinkingPosition = transform.position;
            sinkingYaw = transform.eulerAngles.y;
            Helm.ReleaseControl();
            foreach (var cannon in GetComponentsInChildren<SimpleCannon>()) cannon.ReleaseControl();
            GetComponent<SailSystem>()?.StopAll();
        }
        int remoteDriver;
        SailSystem sails;
        float[] publishedTensions;
        int[] publishedOwners;
        void Awake()
        {
            if (GetComponent<ShipSinkingVfx>() == null) gameObject.AddComponent<ShipSinkingVfx>();
            Motor = GetComponent<ShipController>(); Motor.Networked = true;
            sails = GetComponent<SailSystem>();
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
            BeginMonkey();
            Body.interpolation = RigidbodyInterpolation.None;
            TimeManager.OnTick += Tick;
            TimeManager.OnPostTick += Publish;
        }
        public override void OnStopNetwork()
        {
            if (TimeManager != null)
            {
                TimeManager.OnTick -= Tick;
                TimeManager.OnPostTick -= Publish;
            }
            ActiveShips.Remove(this);
            ClearAmmo();
            if (monkey != null) monkey.End();
            targetMarks.Clear();
            Helm.ReleaseControl();
        }
        void Tick()
        {
            if (!IsServerInitialized) return;
            TickVortexBoost();
            if (IsSinking)
            {
                float progress = Mathf.Clamp01((Time.time - sinkingStarted) / 12f);
                var state = Motor.Capture();
                state.Position = sinkingPosition + Vector3.down * (45f * progress * progress);
                state.Yaw = sinkingYaw; state.Pitch = progress * 18f; state.Bank = progress * 12f;
                state.WaveRoll = 0f; state.Speed = state.Sail = state.Rudder = 0f; state.Controlling = false;
                Motor.Restore(state, null);
                if (progress >= 1f) ServerManager.Despawn(NetworkObject);
                return;
            }
            Helm.Simulate(default, null, (float)TimeManager.TickDelta);
            Motor.Simulate((float)TimeManager.TickDelta);
            if (VortexBoostActive.Value && AnchorDropped) anchorSeabedPoint.Value = Motor.AnchorPoint;
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
            sails.ValidateGrips();
            sails.CaptureRopes(ref publishedTensions, ref publishedOwners);
            ReceiveState(Motor.Capture(), driver, publishedTensions, publishedOwners);
        }
        public void DragWheel(float degrees, bool holding) => DragWheelServerRpc(degrees, holding);
        public void CollisionAudio(float strength, Vector3 point, bool showEffect) { if (IsServerInitialized) CollisionAudioObserversRpc(strength, point, showEffect); }
        public void ImpactVfx(Vector3 point, Vector3 normal) { if (IsServerInitialized) ImpactVfxObserversRpc(point, normal); }
        [ObserversRpc(RunLocally = true)]
        void ImpactVfxObserversRpc(Vector3 point, Vector3 normal)
        {
            CombatVfx.Impact(point, normal, true, true);
            GameAudio.Play(SoundCue.ShipHit, point);
        }
        [ObserversRpc(RunLocally = true)]
        void CollisionAudioObserversRpc(float strength, Vector3 point, bool showEffect)
        {
            GameAudio.Play(SoundCue.ShipCollision, point, Mathf.Lerp(.25f, 1f, strength));
            if (showEffect && strength > .08f) CombatVfx.Splash(point, Mathf.Lerp(.25f, 1.2f, strength));
            FirstPersonFeedback.Kick(transform.position, Vector3.up, .06f * strength);
        }
        [ServerRpc(RequireOwnership = false)]
        void DragWheelServerRpc(float degrees, bool holding, FishNet.Connection.NetworkConnection sender = null)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (player != null && !IsSinking) Helm.Drag(player.Motor, degrees, holding);
        }
        public void AdjustSails(float amount) => AdjustSailsServerRpc(amount);
        public void DragSailRope(int index, float amount, bool holding) => DragSailRopeServerRpc(index, amount, holding);
        [ServerRpc(RequireOwnership = false)]
        void DragSailRopeServerRpc(int index, float amount, bool holding, FishNet.Connection.NetworkConnection sender = null)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (player != null && (!IsSinking || !holding)) GetComponent<SailSystem>().Drag(index, player.Motor, amount, holding);
        }
        [ServerRpc(RequireOwnership = false)]
        void AdjustSailsServerRpc(float amount, FishNet.Connection.NetworkConnection sender = null)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            var sails = GetComponent<SailSystem>();
            if (player != null && !player.Motor.IsFrozen && !IsSinking && sails.InRange(player.Motor) && float.IsFinite(amount)) sails.AdjustSail(Mathf.Clamp(amount, -.2f, .2f));
        }
        [ObserversRpc(BufferLast = true)]
        void ReceiveState(ShipState state, int driver, float[] ropes, int[] holders)
        {
            if (IsServerInitialized) return;
            GetComponent<SailSystem>().ApplyRopes(ropes, holders);
            if (OceanSurface.Instance != null) OceanSurface.Instance.Synchronize(state.WaveTime);
            remoteState = state; remoteDriver = driver; hasRemoteState = true;
        }
        void Update()
        {
            UpdatePump();
            UpdateMonkey();
            UpdateAmmo();
            if (IsServerInitialized || !hasRemoteState) return;
            AdvancedPlayerController driver = null;
            if (remoteDriver > 0)
                foreach (var p in NetworkPlayer.Active)
                    if (p.ParticipantId.Value == remoteDriver) { driver = p.Motor; break; }
            Motor.ApplyRemoteState(remoteState, 1f - Mathf.Exp(-16f * Time.deltaTime), driver);
        }
    }
}
