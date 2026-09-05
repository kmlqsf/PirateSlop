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
        int autoTicks;
        public AdvancedPlayerController Motor => motor;
        public ShipDeckPassenger Passenger => passenger;
        public NetworkShip Ship => ShipObject.Value == null ? null : ShipObject.Value.GetComponent<NetworkShip>();
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
            // Test clients start at a valid helm spawn; no bypass of server checks.
            result.Use = autoTicks == 30;
            if (autoTicks > 30) result.Move = new Vector2(Mathf.Sin(autoTicks / 180f) * .35f, autoTicks < 150 ? 1 : 0);
            return result;
        }
        void PostTick() { if (bound) CreateReconcile(); }
        [Replicate]
        void Move(CaptainInput input, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (!TryBind()) return;
            var command = input.Command;
            if (!command.IsValid) command = default;
            // Missing input cannot repeat a one-shot action or leave throttle held indefinitely.
            if (!state.ContainsCreated()) command = new PlayerCommand { Yaw = motor.transform.eulerAngles.y };
            float dt = (float)TimeManager.TickDelta;
            Ship.Helm.Simulate(command, motor, dt);
            Ship.Motor.Simulate(dt);
            Physics.SyncTransforms();
            passenger.Carry();
            motor.Simulate(command, dt);
            passenger.Detect();
            if (motor.transform.position.y < Ship.transform.position.y - 30) ReturnHome();
        }
        public override void CreateReconcile()
        {
            if (Ship == null) return;
            var state = new CaptainState { Ship = Ship.Motor.Capture(), Player = motor.Capture() };
            if (passenger.Ship != null)
            {
                state.Platform = passenger.Ship.GetComponent<NetworkObject>();
                state.RelativePosition = passenger.Ship.transform.InverseTransformPoint(motor.transform.position);
            }
            Reconcile(state);
        }
        [Reconcile]
        void Reconcile(CaptainState state, Channel channel = Channel.Unreliable)
        {
            if (!TryBind()) return;
            Ship.Motor.Restore(state.Ship, motor);
            var platform = state.Platform == null ? null : state.Platform.GetComponent<Rigidbody>();
            if (platform != null) state.Player.Position = platform.transform.TransformPoint(state.RelativePosition);
            motor.Restore(state.Player);
            passenger.Attach(platform);
            Physics.SyncTransforms();
        }
        public void ReturnHome()
        {
            if (Ship == null) return;
            Ship.Helm.ReleaseControl();
            var state = new PlayerState { Position = Ship.transform.TransformPoint(SessionController.Instance.Config.PlayerLocalSpawn), Yaw = Ship.transform.eulerAngles.y };
            motor.Restore(state); passenger.Attach(Ship.Body);
        }
    }
}
