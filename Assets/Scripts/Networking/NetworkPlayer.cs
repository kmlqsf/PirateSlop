using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
namespace PirateSlop.Networking
{
    public struct CaptainInput : IReplicateData
    {
        public PlayerCommand Command;
        uint tick;
        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value;
        public void Dispose() { }
    }
    public struct CaptainState : IReconcileData
    {
        public ShipState Ship;
        public PlayerState Player;
        public NetworkObject ActiveShip;
        public NetworkObject Platform;
        public Vector3 RelativePosition;
        uint tick;
        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value;
        public void Dispose() { }
    }
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public readonly SyncVar<NetworkObject> ShipObject = new();
        public readonly SyncVar<int> ParticipantId = new();
        AdvancedPlayerController motor;
        ShipDeckPassenger passenger;
        bool bound;
        CaptainState observerState;
        bool hasObserverState;
        int autoTicks;
        int receivedSnapshots;
        float nextDiagnostic;
        readonly bool diagnostics = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-netdiag") >= 0;
        public AdvancedPlayerController Motor => motor;
        public ShipDeckPassenger Passenger => passenger;
        NetworkShip resolvedShip;
        public NetworkShip Ship
        {
            get
            {
                if (ShipObject.Value != null) return ShipObject.Value.GetComponent<NetworkShip>();
                if (resolvedShip != null) return resolvedShip;
                // A late observer can receive the player before its ship spawn.
                // FishNet resolves that initial object reference to null; it does
                // not resend an unchanged SyncVar after the ship becomes visible.
                if (ParticipantId.Value <= 0) return null;
                foreach (var candidate in FindObjectsByType<NetworkShip>(FindObjectsSortMode.None))
                    if (candidate.ParticipantId.Value == ParticipantId.Value) return resolvedShip = candidate;
                return null;
            }
        }
        NetworkShip ActiveShip
        {
            get
            {
                foreach (var candidate in FindObjectsByType<NetworkShip>(FindObjectsSortMode.None))
                    if (candidate.Helm.IsControlledBy(motor)) return candidate;
                return Ship;
            }
        }
        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>(); motor.ConfigureNetwork(false);
            passenger = GetComponent<ShipDeckPassenger>(); passenger.Networked = true;
        }
        public override void OnStartNetwork()
        {
            TimeManager.OnTick += Tick;
            TimeManager.OnPostTick += PostTick;
            TryBind();
        }
        public override void OnStartClient()
        {
            motor.ConfigureNetwork(IsOwner && !SessionController.Instance.Automated);
            if (IsOwner) SessionController.Instance.PlayerReady(this);
            TryBind();
        }
        public override void OnStopNetwork()
        {
            TimeManager.OnTick -= Tick; TimeManager.OnPostTick -= PostTick;
            if (Ship != null) Ship.Helm.ReleaseControl();
        }
        bool TryBind()
        {
            if (bound) return true;
            if (Ship == null) return false;
            Ship.Helm.Bind(motor); passenger.Attach(Ship.Body); bound = true; return true;
        }
        void Tick()
        {
            // State-forwarding may invoke prediction on observers.  Only the owner
            // and the server are allowed to simulate this player.
            if (!IsOwner && !IsServerInitialized) return;
            if (!TryBind()) return;
            var input = default(CaptainInput);
            if (IsOwner)
            {
                input.Command = motor.ConsumeCommand();
                if (SessionController.Instance.Automated) input.Command = AutomatedCommand();
            }
            Move(input);
        }
        PlayerCommand AutomatedCommand()
        {
            autoTicks++;
            var result = new PlayerCommand { Yaw = motor.transform.eulerAngles.y };
            if (autoTicks < 20) result.Move = Vector2.down;
            // Walk from the authored spawn toward the helm before pressing E.
            // The normal range/occupancy checks still apply.
            result.Use = autoTicks == 30;
            if (autoTicks > 30) result.Move = new Vector2(Mathf.Sin(autoTicks / 180f) * .35f, autoTicks < 150 ? 1 : 0);
            return result;
        }
        void PostTick()
        {
            if (!bound) return;
            if (IsOwner || IsServerInitialized) CreateReconcile();
            if (IsServerInitialized) SendObserverState(BuildState());
        }
        [Replicate]
        void Move(CaptainInput input, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (!IsOwner && !IsServerInitialized) return;
            if (!TryBind()) return;
            var activeShip = ActiveShip;
            if (activeShip == null) return;
            // While walking, only a nearby helm accepts E. Once it is taken, the
            // same player keeps driving that ship until E/Q releases it.
            if (!activeShip.Helm.IsControlledBy(motor))
                foreach (var candidate in FindObjectsByType<NetworkShip>(FindObjectsSortMode.None))
                    if (candidate.Helm.InRange(motor)) { activeShip = candidate; break; }
            var command = input.Command;
            if (!command.IsValid) command = default;
            // Missing input cannot repeat a one-shot action or leave throttle held indefinitely.
            if (!state.ContainsCreated()) command = new PlayerCommand { Yaw = motor.transform.eulerAngles.y };
            float dt = (float)TimeManager.TickDelta;
            activeShip.Helm.Simulate(command, motor, dt);
            activeShip.Motor.Simulate(dt);
            Physics.SyncTransforms();
            passenger.Carry();
            motor.Simulate(command, dt);
            passenger.Detect();
            if (motor.transform.position.y < Ship.transform.position.y - 30) ReturnHome();
        }
        public override void CreateReconcile()
        {
            if (Ship == null) return;
            Reconcile(BuildState());
        }
        CaptainState BuildState()
        {
            var activeShip = ActiveShip ?? Ship;
            var state = new CaptainState { Ship = activeShip.Motor.Capture(), Player = motor.Capture(), ActiveShip = activeShip.NetworkObject };
            if (passenger.Ship != null)
            {
                state.Platform = passenger.Ship.GetComponent<NetworkObject>();
                state.RelativePosition = passenger.Ship.transform.InverseTransformPoint(motor.transform.position);
            }
            return state;
        }
        [ObserversRpc(BufferLast = true, ExcludeOwner = true, RunLocally = true)]
        void SendObserverState(CaptainState state)
        {
            // The server already owns the authoritative transforms; the owning
            // client receives prediction reconciliation instead.
            if (IsOwner || IsServerInitialized) return;
            observerState = state;
            hasObserverState = true;
            receivedSnapshots++;
        }
        void Update()
        {
            if (diagnostics && Time.unscaledTime >= nextDiagnostic)
            {
                nextDiagnostic = Time.unscaledTime + 2f;
                var graphics = transform.Find("PlayerGraphics");
                Debug.Log($"NET_VISUAL participant={ParticipantId.Value} position={(graphics == null ? transform.position : graphics.position):F3} forwarding={NetworkObject.EnableStateForwarding}");
                Debug.Log($"NET_STATE participant={ParticipantId.Value} owner={IsOwner} server={IsServerInitialized} bound={bound} received={receivedSnapshots} player={transform.position:F3} ship={(Ship == null ? Vector3.zero : Ship.transform.position):F3} targetPlayer={observerState.Player.Position:F3} targetShip={observerState.Ship.Position:F3}");
            }
            if (!hasObserverState || IsOwner || IsServerInitialized || Ship == null) return;
            float blend = 1f - Mathf.Exp(-16f * Time.deltaTime);
            var activeShip = observerState.ActiveShip == null ? Ship : observerState.ActiveShip.GetComponent<NetworkShip>();
            if (activeShip != null) activeShip.Motor.ApplyRemoteState(observerState.Ship, blend);
            var position = observerState.Player.Position;
            if (observerState.Platform != null)
            {
                var platform = observerState.Platform.GetComponent<Rigidbody>();
                if (platform != null) position = platform.transform.TransformPoint(observerState.RelativePosition);
                passenger.Attach(platform);
            }
            motor.ApplyRemoteState(position, observerState.Player.Yaw, blend);
        }
        [Reconcile]
        void Reconcile(CaptainState state, Channel channel = Channel.Unreliable)
        {
            // Observers are driven exclusively by snapshots, never prediction replay.
            if (!IsOwner && !IsServerInitialized) return;
            if (!TryBind()) return;
            var activeShip = state.ActiveShip == null ? Ship : state.ActiveShip.GetComponent<NetworkShip>();
            if (activeShip != null) activeShip.Motor.Restore(state.Ship, motor);
            var platform = state.Platform == null ? null : state.Platform.GetComponent<Rigidbody>();
            if (platform != null) state.Player.Position = platform.transform.TransformPoint(state.RelativePosition);
            motor.Restore(state.Player);
            passenger.Attach(platform);
            Physics.SyncTransforms();
        }
        public void ReturnHome()
        {
            if (Ship == null) return;
            ActiveShip?.Helm.ReleaseControl();
            var state = new PlayerState { Position = Ship.transform.TransformPoint(SessionController.Instance.Config.PlayerLocalSpawn), Yaw = Ship.transform.eulerAngles.y };
            motor.Restore(state); passenger.Attach(Ship.Body);
        }
    }
}
