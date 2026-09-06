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
    [DefaultExecutionOrder(-10)]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public readonly SyncVar<NetworkObject> ShipObject = new();
        public readonly SyncVar<int> ParticipantId = new();
        AdvancedPlayerController motor;
        ShipDeckPassenger passenger;
        bool bound;
        CaptainState observerState;
        bool hasObserverState;
        Vector3 visualLocalPosition;
        Rigidbody visualPlatform;
        Transform graphicsRoot;
        Vector3 renderedPosition;
        Rigidbody renderedPlatform;
        bool renderInitialized;
        PlayerCommand lastServerCommand;
        int missingServerInputs;
        Vector3 observerLocalPosition;
        NetworkObject observerPlatform;
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
            graphicsRoot = transform.Find("PlayerGraphics");
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
            var active = ActiveShip;
            if (active != null && active.Helm.IsControlledBy(motor)) active.Helm.ReleaseControl();
        }
        bool TryBind()
        {
            if (bound) return true;
            if (Ship == null) return false;
            passenger.Attach(Ship.Body); bound = true; return true;
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
            // Approach only when necessary; authored ship spawns may be on either side of the helm.
            if (autoTicks < 30 && Ship != null && !Ship.Helm.InRange(motor))
            {
                Vector3 toward = Ship.Helm.transform.position - motor.transform.position;
                toward.y = 0;
                Vector3 local = motor.transform.InverseTransformDirection(toward.normalized);
                result.Move = new Vector2(local.x, local.z);
            }
            result.Use = autoTicks >= 30 && autoTicks % 30 == 0 && !motor.LocomotionLocked;
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
            // Only extrapolate live server input for a bounded interval. Prediction replay
            // must use its own historical command, not a cache from a later tick.
            if (IsServerInitialized && !state.ContainsReplayed())
            {
                if (state.ContainsCreated()) { lastServerCommand = command; missingServerInputs = 0; }
                else
                {
                    missingServerInputs++;
                    command = missingServerInputs <= 3 ? lastServerCommand : new PlayerCommand { Yaw = motor.transform.eulerAngles.y };
                }
            }
            if (!state.ContainsCreated())
            {
                command.Jump = command.Slide = command.Use = command.Release = false;
            }
            float dt = (float)TimeManager.TickDelta;
            if (IsServerInitialized) activeShip.Helm.Simulate(command, motor, dt);
            Physics.SyncTransforms();
            passenger.Carry();
            motor.Simulate(command, dt);
            passenger.Detect();
            CaptureVisualAnchor();
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
            motor.ApplyAnimationState(observerState.Player);
            float blend = 1f - Mathf.Exp(-16f * Time.deltaTime);
            var activeShip = observerState.ActiveShip == null ? Ship : observerState.ActiveShip.GetComponent<NetworkShip>();
            // NetworkShip is the only writer of ship state on every client.
            var position = observerState.Player.Position;
            if (observerState.Platform != null)
            {
                var platform = observerState.Platform.GetComponent<Rigidbody>();
                if (observerPlatform != observerState.Platform)
                {
                    observerPlatform = observerState.Platform;
                    observerLocalPosition = observerState.RelativePosition;
                }
                observerLocalPosition = Vector3.Lerp(observerLocalPosition, observerState.RelativePosition, blend);
                if (platform != null) position = platform.transform.TransformPoint(observerLocalPosition);
                passenger.Attach(platform);
                motor.ApplyRemoteState(position, observerState.Player.Yaw, 1f);
            }
            else
            {
                observerPlatform = null;
                passenger.Attach(null);
                motor.ApplyRemoteState(position, observerState.Player.Yaw, blend);
            }
            CaptureVisualAnchor();
        }
        void CaptureVisualAnchor()
        {
            visualPlatform = passenger.Ship;
            if (visualPlatform != null) visualLocalPosition = visualPlatform.transform.InverseTransformPoint(motor.transform.position);
        }
        void LateUpdate()
        {
            // Keep a render position independent of the controller and FishNet's graphical
            // transform writes. Tick/reconcile/replay may move the controller several times
            // in one frame; only the final target is consumed here.
            if (graphicsRoot == null) return;
            Vector3 target = visualPlatform != null ? visualLocalPosition : motor.transform.position;
            if (!renderInitialized)
            {
                renderedPosition = target; renderedPlatform = visualPlatform; renderInitialized = true;
            }
            else if (renderedPlatform != visualPlatform)
            {
                Vector3 world = renderedPlatform != null ? renderedPlatform.transform.TransformPoint(renderedPosition) : renderedPosition;
                renderedPosition = visualPlatform != null ? visualPlatform.transform.InverseTransformPoint(world) : world;
                renderedPlatform = visualPlatform;
            }
            if ((renderedPosition - target).sqrMagnitude > 16f) renderedPosition = target;
            else renderedPosition = Vector3.Lerp(renderedPosition, target, 1f - Mathf.Exp(-22f * Time.deltaTime));
            graphicsRoot.position = visualPlatform != null ? visualPlatform.transform.TransformPoint(renderedPosition) : renderedPosition;
            graphicsRoot.rotation = motor.transform.rotation;
        }
        [Reconcile]
        void Reconcile(CaptainState state, Channel channel = Channel.Unreliable)
        {
            // Observers are driven exclusively by snapshots, never prediction replay.
            if (!IsOwner && !IsServerInitialized) return;
            if (!TryBind()) return;
            var activeShip = state.ActiveShip == null ? Ship : state.ActiveShip.GetComponent<NetworkShip>();
            // Player reconciliation must never rewind a shared ship.
            var platform = state.Platform == null ? null : state.Platform.GetComponent<Rigidbody>();
            if (platform != null) state.Player.Position = platform.transform.TransformPoint(state.RelativePosition);
            motor.Restore(state.Player);
            passenger.Attach(platform);
            CaptureVisualAnchor();
            Physics.SyncTransforms();
        }
        public void ReturnHome()
        {
            if (Ship == null) return;
            renderInitialized = false;
            var active = ActiveShip;
            if (active != null && active.Helm.IsControlledBy(motor)) active.Helm.ReleaseControl();
            var state = new PlayerState { Position = Ship.transform.TransformPoint(SessionController.Instance.Config.PlayerLocalSpawn), Yaw = Ship.transform.eulerAngles.y };
            motor.Restore(state); passenger.Attach(Ship.Body);
        }
    }
}
